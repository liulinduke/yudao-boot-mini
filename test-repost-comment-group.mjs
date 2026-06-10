import puppeteer from 'puppeteer-core';

const POST_URL = 'https://www.facebook.com/share/p/1BEpgnxWR2/';
const GROUP_SEARCH = 'JAC Liner';
const sleep = (ms) => new Promise((r) => setTimeout(r, ms));

async function main() {
  const browser = await puppeteer.connect({ browserURL: 'http://localhost:9222', defaultViewport: null });
  let page = (await browser.pages()).find((p) => (p.url() || '').includes('facebook.com'));
  if (!page.url().includes('pfbid') && !page.url().includes('permalink')) {
    await page.goto(POST_URL, { waitUntil: 'networkidle2', timeout: 60000 }).catch(() => {});
    await sleep(4000);
  }
  console.log('url', page.url());

  // comment
  const comment = await page.evaluate(() => {
    const box = document.querySelector('div[role="textbox"][contenteditable="true"]');
    if (!box) return { ok: false, reason: 'no box' };
    box.focus();
    const text = 'cdp verify comment full';
    try {
      const dt = new DataTransfer();
      dt.setData('text/plain', text);
      box.dispatchEvent(new ClipboardEvent('paste', { bubbles: true, cancelable: true, clipboardData: dt }));
    } catch {
      for (const ch of text) {
        document.execCommand('insertText', false, ch);
      }
    }
    const content = (box.textContent || '').trim();
    const buttons = [];
    document.querySelectorAll('[role="button"]').forEach((b) => {
      const a = b.getAttribute('aria-label') || '';
      const t = (b.textContent || '').trim();
      if (/^comment$/i.test(t) || /^comment$/i.test(a) || t === '评论' || a === '评论') {
        buttons.push({ t, a });
      }
    });
    return { ok: content.length > 5, content, buttons };
  });
  console.log('comment', comment);

  // group search
  await page.evaluate(() => {
    document.querySelectorAll('[aria-label="Close"]').forEach((b) => b.click());
  });
  await sleep(500);
  await page.evaluate(() => {
    for (const btn of document.querySelectorAll('[role="button"]')) {
      if ((btn.getAttribute('aria-label') || '').includes('Send this to friends')) {
        btn.click();
        return;
      }
    }
  });
  await sleep(2000);
  await page.evaluate(() => {
    const dialog = [...document.querySelectorAll('[role="dialog"]')].pop();
    for (const btn of dialog?.querySelectorAll('[role="button"]') || []) {
      if (btn.getAttribute('aria-label') === 'Share to a group') {
        btn.click();
        return;
      }
    }
  });
  await sleep(2000);
  const group = await page.evaluate((key) => {
    const dialog = [...document.querySelectorAll('[role="dialog"]')].pop();
    const search = dialog?.querySelector('input[aria-label="Search for groups"], input[placeholder*="Search"]');
    if (search) {
      search.focus();
      search.value = key;
      search.dispatchEvent(new Event('input', { bubbles: true }));
    }
    return { hasSearch: !!search };
  }, GROUP_SEARCH);
  await sleep(2500);
  const groupMatch = await page.evaluate((key) => {
    const k = key.toLowerCase();
    const found = [];
    const dialog = [...document.querySelectorAll('[role="dialog"]')].pop();
    dialog?.querySelectorAll('[role="button"]').forEach((b) => {
      const t = (b.textContent || '').trim();
      if (t.toLowerCase().includes(k)) found.push(t.slice(0, 80));
    });
    return found;
  }, GROUP_SEARCH);
  console.log('group', { ...group, matches: groupMatch });

  await browser.disconnect();
}

main().catch(console.error);
