/**
 * CDP 验证：动态附言 / 好友附言 / 群组附言 输入框位置
 * 不点击 Share now / Send，只探测 editor 并测试 paste
 */
import puppeteer from 'puppeteer-core';

const GROUP_SEARCH = 'JAC Liner';
const TEST_MSG = 'CDP附言测试-请忽略';

const sleep = (ms) => new Promise((r) => setTimeout(r, ms));

async function openShare(page) {
  await page.evaluate(() => {
    document.querySelectorAll('[aria-label="Close"]').forEach((b) => b.click());
  });
  await sleep(500);
  return page.evaluate(() => {
    for (const btn of document.querySelectorAll('[role="button"]')) {
      const aria = (btn.getAttribute('aria-label') || '').toLowerCase();
      if (aria.includes('send this to friends or post it on your profile')) {
        btn.click();
        return true;
      }
    }
    return false;
  });
}

async function findEditors(page) {
  return page.evaluate(() => {
    const dialogs = [...document.querySelectorAll('[role="dialog"]')];
    const d = dialogs[dialogs.length - 1];
    if (!d) return { dialog: false, editors: [] };
    const editors = [];
    d.querySelectorAll('div[role="textbox"]').forEach((el, i) => {
      const rect = el.getBoundingClientRect();
      editors.push({
        i,
        contenteditable: el.getAttribute('contenteditable'),
        aria: el.getAttribute('aria-label') || '',
        placeholder: el.getAttribute('placeholder') || '',
        text: (el.textContent || '').trim().slice(0, 30),
        visible: rect.width > 0 && rect.height > 0
      });
    });
    const shareNow = [...d.querySelectorAll('[role="button"]')].some(
      (b) => b.getAttribute('aria-label') === 'Share now'
    );
    return { dialog: true, shareNow, editors };
  });
}

async function tryPaste(page, msg) {
  return page.evaluate((messageText) => {
    const dialog = [...document.querySelectorAll('[role="dialog"]')].pop();
    const editor = dialog?.querySelector('div[role="textbox"][contenteditable="true"], div[role="textbox"]');
    if (!editor) return { ok: false, reason: 'no editor' };
    editor.focus();
    try {
      const dt = new DataTransfer();
      dt.setData('text/plain', messageText);
      editor.dispatchEvent(
        new ClipboardEvent('paste', { bubbles: true, cancelable: true, clipboardData: dt })
      );
    } catch (e) {
      editor.textContent = messageText;
      editor.dispatchEvent(new InputEvent('input', { bubbles: true, data: messageText }));
    }
    return { ok: true, content: (editor.textContent || '').trim().slice(0, 50) };
  }, msg);
}

async function verifyFeed(page) {
  const opened = await openShare(page);
  await sleep(2500);
  const before = await findEditors(page);
  const pasted = await tryPaste(page, TEST_MSG);
  await page.evaluate(() => {
    document.querySelectorAll('[aria-label="Close"]').forEach((b) => b.click());
  });
  await sleep(800);
  return { step: 'feed附言', opened, before, pasted };
}

async function verifyFriend(page) {
  const opened = await openShare(page);
  await sleep(2500);
  const beforePick = await findEditors(page);
  const pasteBeforePick = await tryPaste(page, TEST_MSG + '-好友前');

  const pick = await page.evaluate(() => {
    const dialog = [...document.querySelectorAll('[role="dialog"]')].pop();
    const btn = [...(dialog?.querySelectorAll('[role="button"]') || [])].find((b) => {
      const a = b.getAttribute('aria-label') || '';
      return a.includes('Send to') && a.includes('via Messenger');
    });
    if (!btn) return { ok: false };
    btn.click();
    return { ok: true, aria: btn.getAttribute('aria-label') };
  });
  await sleep(2500);

  const afterPick = await findEditors(page);
  const pasteAfterPick = await tryPaste(page, TEST_MSG + '-好友后');

  const sendBtn = await page.evaluate(() => {
    const dialog = [...document.querySelectorAll('[role="dialog"]')].pop();
    const btns = [];
    dialog?.querySelectorAll('[role="button"]').forEach((b) => {
      const a = b.getAttribute('aria-label') || '';
      const t = (b.textContent || '').trim();
      if (a === 'Send' || t === 'Send' || a === '发送') btns.push({ a, t });
    });
    return btns;
  });

  await page.evaluate(() => {
    document.querySelectorAll('[aria-label="Close"]').forEach((b) => b.click());
  });
  await sleep(800);

  return {
    step: '好友附言',
    opened,
    beforePick,
    pasteBeforePick,
    pick,
    afterPick,
    pasteAfterPick,
    sendBtn
  };
}

async function verifyGroup(page) {
  const opened = await openShare(page);
  await sleep(2500);

  await page.evaluate(() => {
    const dialog = [...document.querySelectorAll('[role="dialog"]')].pop();
    for (const btn of dialog?.querySelectorAll('[role="button"]') || []) {
      if (btn.getAttribute('aria-label') === 'Share to a group') {
        btn.click();
        return;
      }
    }
  });
  await sleep(2500);

  await page.evaluate((key) => {
    const dialog = [...document.querySelectorAll('[role="dialog"]')].pop();
    const search = dialog?.querySelector('input[aria-label="Search for groups"]');
    if (search) {
      search.focus();
      search.value = key;
      search.dispatchEvent(new Event('input', { bubbles: true }));
    }
  }, GROUP_SEARCH);
  await sleep(2500);

  const groupPick = await page.evaluate(() => {
    const dialog = [...document.querySelectorAll('[role="dialog"]')].pop();
    for (const btn of dialog?.querySelectorAll('[role="button"]') || []) {
      const t = (btn.textContent || '').trim().toLowerCase();
      if (t.includes('jac liner')) {
        btn.click();
        return { ok: true, text: btn.textContent?.trim().slice(0, 60) };
      }
    }
    return { ok: false };
  });
  await sleep(2000);

  const afterGroup = await findEditors(page);
  const pasteAfterGroup = await tryPaste(page, TEST_MSG + '-群组');

  await page.evaluate(() => {
    document.querySelectorAll('[aria-label="Close"]').forEach((b) => b.click());
  });
  await sleep(800);

  return { step: '群组附言', opened, groupPick, afterGroup, pasteAfterGroup };
}

async function main() {
  const browser = await puppeteer.connect({
    browserURL: 'http://localhost:9222',
    defaultViewport: null
  });
  const pages = await browser.pages();
  let page = pages.find((p) => (p.url() || '').includes('facebook.com')) || pages[0];
  console.log('当前页面:', page.url());

  if (!page.url().includes('pfbid') && !page.url().includes('permalink')) {
    console.log('导航到帖子页...');
    await page
      .goto('https://www.facebook.com/share/p/1BEpgnxWR2/', {
        waitUntil: 'networkidle2',
        timeout: 60000
      })
      .catch(() => {});
    await sleep(4000);
    console.log('导航后:', page.url());
  }

  const results = [await verifyFeed(page), await verifyFriend(page), await verifyGroup(page)];

  console.log('\n=== 附言 CDP 验证结果 ===');
  console.log(JSON.stringify(results, null, 2));

  const ok =
    results[0].pasted?.ok &&
    (results[1].pasteBeforePick?.ok || results[1].pasteAfterPick?.ok) &&
    results[2].pasteAfterGroup?.ok;

  console.log('\nALL_OK:', ok);
  await browser.disconnect();
  process.exit(ok ? 0 : 1);
}

main().catch((e) => {
  console.error(e);
  process.exit(1);
});
