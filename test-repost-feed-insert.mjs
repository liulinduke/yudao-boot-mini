import puppeteer from 'puppeteer-core';

const sleep = (ms) => new Promise((r) => setTimeout(r, ms));

async function insertText(page, msg) {
  return page.evaluate((messageText) => {
    const dialog = [...document.querySelectorAll('[role="dialog"]')].pop();
    const editors = [...(dialog?.querySelectorAll('div[role="textbox"][contenteditable="true"]') || [])].filter(
      (e) => e.getBoundingClientRect().width > 0
    );
    const editor = editors[0];
    if (!editor) return { ok: false, reason: 'no visible editor' };
    editor.focus();
    for (const ch of messageText) {
      document.execCommand('insertText', false, ch);
      editor.dispatchEvent(new InputEvent('input', { data: ch, bubbles: true, inputType: 'insertText' }));
    }
    return { ok: true, content: (editor.textContent || '').trim(), count: editors.length };
  }, msg);
}

async function main() {
  const browser = await puppeteer.connect({ browserURL: 'http://localhost:9222', defaultViewport: null });
  const page = (await browser.pages()).find((p) => p.url().includes('facebook.com'));

  await page.evaluate(() => {
    document.querySelectorAll('[aria-label="Close"]').forEach((b) => b.click());
  });
  await sleep(500);
  await page.evaluate(() => {
    for (const btn of document.querySelectorAll('[role="button"]')) {
      if ((btn.getAttribute('aria-label') || '').includes('Send this to friends')) btn.click();
    }
  });
  await sleep(2000);

  const feed = await insertText(page, '动态附言insertText测试');
  console.log('feed insertText:', feed);

  await browser.disconnect();
}

main().catch(console.error);
