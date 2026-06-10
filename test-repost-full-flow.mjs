/**
 * CDP 逐步验证转帖流程（与 RepostScriptBuilder 逻辑对齐）
 */
import puppeteer from 'puppeteer-core';

const POST_URL = 'https://www.facebook.com/share/p/1BEpgnxWR2/';
const GROUP_SEARCH = '1711415712822334'; // 群 URL 片段或群名
const COMMENT_TEXT = 'CDP test comment - please ignore';

const sleep = (ms) => new Promise((r) => setTimeout(r, ms));

function log(step, msg, extra) {
  console.log(`\n[${step}] ${msg}`);
  if (extra !== undefined) console.log(typeof extra === 'string' ? extra : JSON.stringify(extra, null, 2));
}

async function evalHelpers(page) {
  return page.evaluate(() => {
    const normalizeText = (text) => (text || '').replace(/\s+/g, ' ').trim().toLowerCase();
    const isVisibleElement = (el) => {
      if (!el) return false;
      const rect = el.getBoundingClientRect();
      const style = window.getComputedStyle(el);
      return rect.width > 0 && rect.height > 0 && style.display !== 'none' && style.visibility !== 'hidden';
    };
    const findShareButton = () => {
      const labels = [
        'send this to friends or post it on your profile',
        '发送给好友或发布到你的个人主页',
        '分享给好友或发布到你的主页'
      ];
      for (const btn of document.querySelectorAll('[role="button"]')) {
        if (!isVisibleElement(btn)) continue;
        const aria = normalizeText(btn.getAttribute('aria-label'));
        if (labels.some((l) => aria.includes(l))) return { found: true, aria: btn.getAttribute('aria-label') };
      }
      return { found: false };
    };
    const getShareDialog = () => {
      const dialogs = [...document.querySelectorAll('[role="dialog"]')];
      const d =
        dialogs.find((x) => {
          const t = normalizeText(x.textContent);
          return t.includes('share') || t.includes('分享') || t.includes('messenger');
        }) || dialogs[dialogs.length - 1];
      if (!d) return null;
      const texts = [];
      d.querySelectorAll('[role="button"], span, div').forEach((el) => {
        if (!isVisibleElement(el)) return;
        const text = (el.textContent || '').trim();
        const aria = el.getAttribute('aria-label') || '';
        if ((text && text.length < 60) || aria) {
          texts.push({ text: text.slice(0, 50), aria: aria.slice(0, 80) });
        }
      });
      return { textSample: texts.slice(0, 25) };
    };
    const likeBtn = document.querySelector('[aria-label="Like" i], [aria-label="赞" i]');
    const unlikeBtn = document.querySelector('[aria-label="Remove Like" i], [aria-label="Unlike" i]');
    const commentBox = document.querySelector(
      'div[role="textbox"][contenteditable="true"], div[role="textbox"]'
    );
    const commentSubmit = document.querySelector(
      '[aria-label*="Comment" i][role="button"], [aria-label*="评论" i][role="button"]'
    );
    return {
      url: location.href,
      title: document.title,
      like: { found: !!likeBtn, aria: likeBtn?.getAttribute('aria-label') },
      unlike: { found: !!unlikeBtn },
      shareBtn: findShareButton(),
      comment: {
        box: !!commentBox,
        submit: !!commentSubmit,
        submitAria: commentSubmit?.getAttribute('aria-label')
      }
    };
  });
}

async function clickShareButton(page) {
  return page.evaluate(() => {
    const normalizeText = (t) => (t || '').replace(/\s+/g, ' ').trim().toLowerCase();
    const labels = [
      'send this to friends or post it on your profile',
      '发送给好友或发布到你的个人主页'
    ];
    for (const btn of document.querySelectorAll('[role="button"]')) {
      const aria = normalizeText(btn.getAttribute('aria-label'));
      if (labels.some((l) => aria.includes(l))) {
        btn.click();
        return true;
      }
    }
    return false;
  });
}

async function clickInDialog(page, ...candidates) {
  return page.evaluate((labels) => {
    const normalizeText = (t) => (t || '').replace(/\s+/g, ' ').trim().toLowerCase();
    const isVisible = (el) => {
      if (!el) return false;
      const r = el.getBoundingClientRect();
      const s = window.getComputedStyle(el);
      return r.width > 0 && r.height > 0 && s.display !== 'none';
    };
    const dialogs = [...document.querySelectorAll('[role="dialog"]')];
    const dialog =
      dialogs.find((d) => {
        const t = normalizeText(d.textContent);
        return t.includes('share') || t.includes('分享') || t.includes('messenger');
      }) || dialogs[dialogs.length - 1];
    if (!dialog) return { ok: false, reason: 'no dialog' };

    for (const label of labels) {
      const target = normalizeText(label);
      for (const el of dialog.querySelectorAll('[role="button"], [role="menuitem"], span, div')) {
        if (!isVisible(el)) continue;
        const text = normalizeText(el.textContent);
        const aria = normalizeText(el.getAttribute('aria-label'));
        if (text === target || aria === target || text.includes(target) || aria.includes(target)) {
          (el.closest('[role="button"]') || el).click();
          return { ok: true, clicked: label, text: el.textContent?.trim().slice(0, 40) };
        }
      }
    }
    const available = [];
    dialog.querySelectorAll('[role="button"]').forEach((b) => {
      const t = (b.textContent || '').trim();
      if (t && t.length < 40) available.push(t);
    });
    return { ok: false, reason: 'not found', available: [...new Set(available)].slice(0, 15) };
  }, candidates);
}

async function closeDialogs(page) {
  await page.keyboard.press('Escape');
  await sleep(500);
  await page.keyboard.press('Escape');
  await sleep(800);
}

async function dumpDialog(page) {
  return page.evaluate(() => {
    const dialogs = [...document.querySelectorAll('[role="dialog"]')];
    return dialogs.map((d, i) => {
      const items = [];
      d.querySelectorAll('[role="button"]').forEach((b) => {
        const t = (b.textContent || '').trim();
        const a = b.getAttribute('aria-label') || '';
        if (t || a) items.push({ t: t.slice(0, 50), a: a.slice(0, 60) });
      });
      return { i, items: items.slice(0, 20) };
    });
  });
}

async function testLike(page) {
  const before = await evalHelpers(page);
  if (before.unlike.found) {
    return { step: 'like', status: 'skip', reason: 'already liked' };
  }
  const clicked = await page.evaluate(() => {
    const btn = document.querySelector('[aria-label="Like" i], [aria-label="赞" i]');
    if (!btn) return false;
    btn.click();
    return true;
  });
  await sleep(1500);
  const after = await evalHelpers(page);
  return {
    step: 'like',
    status: clicked && after.unlike.found ? 'ok' : 'fail',
    clicked,
    afterUnlike: after.unlike.found
  };
}

async function testSharePath(page, name, clicks) {
  await closeDialogs(page);
  const opened = await clickShareButton(page);
  await sleep(2000);
  if (!opened) return { step: name, status: 'fail', reason: 'share button not clicked' };

  const trace = [];
  for (const c of clicks) {
    const r = await clickInDialog(page, c);
    trace.push({ try: c, ...r });
    if (!r.ok) {
      const dialog = await dumpDialog(page);
      return { step: name, status: 'fail', trace, dialog };
    }
    await sleep(2000);
    trace.push({ after: await dumpDialog(page) });
  }
  // 不点最终 Post，避免多次发帖；只验证能否走到确认前
  const finalDialog = await dumpDialog(page);
  const hasPost = JSON.stringify(finalDialog).toLowerCase().includes('post');
  await closeDialogs(page);
  return { step: name, status: 'ok', trace, finalDialog, hasPostButton: hasPost };
}

async function testFriendPath(page) {
  await closeDialogs(page);
  await clickShareButton(page);
  await sleep(2000);
  let r = await clickInDialog(page, 'send in messenger', '在 messenger 中发送', 'messenger');
  if (!r.ok) return { step: 'friend', status: 'fail', r, dialog: await dumpDialog(page) };
  await sleep(2000);
  const friends = await page.evaluate(() => {
    const dialog = [...document.querySelectorAll('[role="dialog"]')].pop();
    if (!dialog) return [];
    const items = [];
    dialog.querySelectorAll('[role="button"], [role="row"], [role="listitem"]').forEach((el, i) => {
      const t = (el.textContent || '').trim().slice(0, 50);
      if (t) items.push({ i, t });
    });
    return items.slice(0, 15);
  });
  await closeDialogs(page);
  return { step: 'friend', status: friends.length > 0 ? 'ok' : 'fail', friendItems: friends };
}

async function testGroupPath(page, searchKey) {
  await closeDialogs(page);
  await clickShareButton(page);
  await sleep(2000);
  let r = await clickInDialog(page, 'share to', '分享到');
  if (!r.ok) return { step: 'group', status: 'fail', phase: 'share to', r };
  await sleep(2000);

  const searchResult = await page.evaluate((key) => {
    const dialog = [...document.querySelectorAll('[role="dialog"]')].pop();
    const search = dialog?.querySelector(
      'input[type="search"], input[placeholder*="Search" i], input[placeholder*="搜索"]'
    );
    if (search) {
      search.focus();
      search.value = key;
      search.dispatchEvent(new Event('input', { bubbles: true }));
      return { hasSearch: true, placeholder: search.placeholder };
    }
    return { hasSearch: false, inputs: [...(dialog?.querySelectorAll('input') || [])].map((i) => i.placeholder) };
  }, searchKey);
  await sleep(2500);

  const matches = await page.evaluate((key) => {
    const dialog = [...document.querySelectorAll('[role="dialog"]')].pop();
    const k = (key || '').toLowerCase();
    const found = [];
    dialog?.querySelectorAll('[role="button"], [role="row"], span, div').forEach((el) => {
      const t = (el.textContent || '').trim().toLowerCase();
      if (k && t.includes(k)) found.push(t.slice(0, 60));
    });
    return [...new Set(found)].slice(0, 10);
  }, searchKey);

  await closeDialogs(page);
  return {
    step: 'group',
    status: matches.length > 0 || searchResult.hasSearch ? 'ok' : 'partial',
    searchResult,
    matches
  };
}

async function testComment(page, text) {
  const r = await page.evaluate((commentText) => {
    const box = document.querySelector('div[role="textbox"][contenteditable="true"], div[role="textbox"]');
    if (!box) return { ok: false, reason: 'no comment box' };
    box.focus();
    box.textContent = commentText;
    box.dispatchEvent(new InputEvent('input', { bubbles: true, data: commentText }));
    const submit = document.querySelector(
      '[aria-label*="Comment" i][role="button"], [aria-label*="评论" i][role="button"], [aria-label="Leave a comment" i]'
    );
    return {
      ok: true,
      typed: (box.textContent || '').slice(0, 30),
      submitFound: !!submit,
      submitAria: submit?.getAttribute('aria-label')
    };
  }, text);
  return { step: 'comment', ...r };
}

async function main() {
  const browser = await puppeteer.connect({
    browserURL: 'http://localhost:9222',
    defaultViewport: null
  });

  const pages = await browser.pages();
  let page = pages.find((p) => (p.url() || '').includes('facebook.com')) || pages[0];
  log('init', `pages=${pages.length}, using=${page?.url()}`);

  if (!page.url().includes('1BEpgnxWR2') && !page.url().includes('pfbid')) {
    log('nav', `goto ${POST_URL}`);
    await page.goto(POST_URL, { waitUntil: 'networkidle2', timeout: 60000 }).catch((e) =>
      log('nav', 'warn: ' + e.message)
    );
    await sleep(4000);
  }

  const snapshot = await evalHelpers(page);
  log('snapshot', 'page elements', snapshot);

  const results = [];
  results.push(await testLike(page));
  results.push(
    await testSharePath(page, 'timeline', ['share to', 'feed', 'news feed', '动态', 'share to feed'])
  );
  results.push(
    await testSharePath(page, 'profile', ['share to', 'profile', 'your profile', '个人主页', 'share to profile'])
  );
  results.push(await testFriendPath(page));
  results.push(await testGroupPath(page, GROUP_SEARCH));
  results.push(await testComment(page, COMMENT_TEXT));

  log('summary', 'ALL RESULTS', results);
  await browser.disconnect();
}

main().catch((e) => {
  console.error(e);
  process.exit(1);
});
