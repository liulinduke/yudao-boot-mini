import puppeteer from 'puppeteer-core';

const sleep = (ms) => new Promise((r) => setTimeout(r, ms));

async function main() {
  const browser = await puppeteer.connect({ browserURL: 'http://localhost:9222', defaultViewport: null });
  const page = (await browser.pages()).find((p) => p.url().includes('facebook.com'));

  await page.evaluate(() => {
    for (const btn of document.querySelectorAll('[role="button"]')) {
      const aria = (btn.getAttribute('aria-label') || '').toLowerCase();
      if (aria.includes('send this to friends or post it on your profile')) {
        btn.click();
        return;
      }
    }
  });
  await sleep(2000);

  const shareNow = await page.evaluate(() => {
    const dialog = [...document.querySelectorAll('[role="dialog"]')].pop();
    for (const btn of dialog?.querySelectorAll('[role="button"]') || []) {
      const aria = btn.getAttribute('aria-label') || '';
      if (aria === 'Share now') {
        return { found: true, text: btn.textContent?.trim() };
      }
    }
    return { found: false };
  });
  console.log('shareNow available:', shareNow);

  const commentTest = await page.evaluate(() => {
    const box = document.querySelector('div[role="textbox"][contenteditable="true"]');
    if (!box) return { ok: false };
    box.focus();
    const text = 'test comment cdp';
    try {
      const dt = new DataTransfer();
      dt.setData('text/plain', text);
      box.dispatchEvent(new ClipboardEvent('paste', { bubbles: true, cancelable: true, clipboardData: dt }));
    } catch (e) {
      box.textContent = text;
      box.dispatchEvent(new InputEvent('input', { bubbles: true, data: text }));
    }
    const content = (box.textContent || '').trim();
    const buttons = [];
    document.querySelectorAll('[role="button"]').forEach((b) => {
      const a = b.getAttribute('aria-label') || '';
      if (/comment|评论/i.test(a)) buttons.push(a);
    });
    return { ok: true, content, commentButtons: buttons.slice(0, 10) };
  });
  console.log('commentTest', commentTest);

  await browser.disconnect();
}

main().catch(console.error);
