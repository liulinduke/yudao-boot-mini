/**
 * CDP 验证：Feed / 好友 / 群组(Profile photo of JAC Liner, Inc. Community) / 评论
 * 仅探测 DOM 与点击路径，不点最终 Share now / Send 避免真实发帖
 */
import puppeteer from 'puppeteer-core';

const GROUP_NAME = 'Profile photo of JAC Liner, Inc. Community';
const sleep = (ms) => new Promise((r) => setTimeout(r, ms));

async function connect() {
  const browser = await puppeteer.connect({
    browserURL: 'http://localhost:9222',
    defaultViewport: null
  });
  const pages = await browser.pages();
  const page = pages.find((p) => (p.url() || '').includes('facebook.com')) || pages[0];
  return { browser, page };
}

async function closeDialogs(page) {
  await page.evaluate(() => {
    document.querySelectorAll('[role="dialog"] [aria-label="Close"]').forEach((b) => b.click());
  });
  await sleep(600);
}

async function openShare(page) {
  await closeDialogs(page);
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

async function getShareDialog(page) {
  return page.evaluate(() => {
    const dialogs = [...document.querySelectorAll('[role="dialog"]')];
    const d = dialogs[dialogs.length - 1];
    if (!d) return null;
    const items = [];
    d.querySelectorAll('[role="button"]').forEach((b) => {
      const t = (b.textContent || '').trim();
      const a = b.getAttribute('aria-label') || '';
      if (t || a) items.push({ t: t.slice(0, 50), a: a.slice(0, 80) });
    });
    return { count: dialogs.length, items };
  });
}

async function verifyFeed(page) {
  const opened = await openShare(page);
  await sleep(2000);
  const dialog = await getShareDialog(page);
  const shareNow = dialog?.items?.find(
    (x) => x.a === 'Share now' || x.t === 'Share now'
  );
  const feed = dialog?.items?.find((x) => x.t === 'Feed' || x.a?.includes('Feed'));
  await closeDialogs(page);
  return {
    step: 'feed',
    ok: !!opened && (!!shareNow || !!feed),
    opened,
    shareNow: !!shareNow,
    feed: !!feed,
    dialog
  };
}

async function verifyFriend(page) {
  const opened = await openShare(page);
  await sleep(2000);
  const friends = await page.evaluate(() => {
    const dialog = [...document.querySelectorAll('[role="dialog"]')].pop();
    if (!dialog) return [];
    return [...dialog.querySelectorAll('[role="button"]')]
      .map((b) => b.getAttribute('aria-label') || '')
      .filter((a) => a.includes('Send to') && a.includes('via Messenger'));
  });
  await closeDialogs(page);
  return {
    step: 'friend',
    ok: friends.length > 0,
    count: friends.length,
    samples: friends.slice(0, 5)
  };
}

async function verifyGroup(page, groupName) {
  const opened = await openShare(page);
  await sleep(2000);
  const clickedGroup = await page.evaluate(() => {
    const dialog = [...document.querySelectorAll('[role="dialog"]')].pop();
    for (const btn of dialog?.querySelectorAll('[role="button"]') || []) {
      const aria = btn.getAttribute('aria-label') || '';
      const text = (btn.textContent || '').trim();
      if (aria === 'Share to a group' || text === 'Group') {
        btn.click();
        return { ok: true, aria, text };
      }
    }
    return { ok: false };
  });
  await sleep(2500);

  const after = await page.evaluate((name) => {
    const dialog = [...document.querySelectorAll('[role="dialog"]')].pop();
    const inputs = [...(dialog?.querySelectorAll('input') || [])].map((i) => ({
      type: i.type,
      placeholder: i.placeholder,
      aria: i.getAttribute('aria-label')
    }));
    const key = (name || '').toLowerCase();
    const matches = [];
    dialog?.querySelectorAll('[role="button"], [role="row"], [role="option"], span, div').forEach((el) => {
      const t = (el.textContent || '').trim().toLowerCase();
      if (key && t.includes(key)) matches.push(t.slice(0, 80));
    });
    const buttons = [];
    dialog?.querySelectorAll('[role="button"]').forEach((b) => {
      const t = (b.textContent || '').trim();
      const a = b.getAttribute('aria-label') || '';
      if (t || a) buttons.push({ t: t.slice(0, 50), a: a.slice(0, 60) });
    });
    return {
      inputs,
      matches: [...new Set(matches)].slice(0, 10),
      buttons: buttons.slice(0, 20)
    };
  }, groupName);

  await closeDialogs(page);
  return {
    step: 'group',
    ok: clickedGroup.ok && (after.matches.length > 0 || after.inputs.length > 0),
    clickedGroup,
    after
  };
}

async function verifyComment(page) {
  const r = await page.evaluate(() => {
    const box = document.querySelector('div[role="textbox"][contenteditable="true"]');
    if (!box) return { ok: false, reason: 'no box' };
    box.focus();
    const text = 'cdp verify comment';
    try {
      const dt = new DataTransfer();
      dt.setData('text/plain', text);
      box.dispatchEvent(
        new ClipboardEvent('paste', { bubbles: true, cancelable: true, clipboardData: dt })
      );
    } catch {
      box.textContent = text;
      box.dispatchEvent(new InputEvent('input', { bubbles: true, data: text }));
    }
    const content = (box.textContent || '').trim();
    const submit = [...document.querySelectorAll('[role="button"]')].find((b) => {
      const a = (b.getAttribute('aria-label') || '').toLowerCase();
      const t = (b.textContent || '').trim().toLowerCase();
      return a === 'comment' || t === 'comment' || a === '评论' || t === '评论';
    });
    return {
      ok: content.length > 3,
      content: content.slice(0, 40),
      submitFound: !!submit,
      submitAria: submit?.getAttribute('aria-label')
    };
  });
  return { step: 'comment', ...r };
}

async function main() {
  const { browser, page } = await connect();
  console.log('URL:', page.url());

  const results = [
    await verifyFeed(page),
    await verifyFriend(page),
    await verifyGroup(page, GROUP_NAME),
    await verifyComment(page)
  ];

  console.log('\n=== CDP VERIFY SUMMARY ===');
  console.log(JSON.stringify(results, null, 2));
  const allOk = results.every((r) => r.ok);
  console.log('\nALL_OK:', allOk);

  await browser.disconnect();
  process.exit(allOk ? 0 : 1);
}

main().catch((e) => {
  console.error(e);
  process.exit(1);
});
