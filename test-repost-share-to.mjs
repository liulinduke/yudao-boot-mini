import puppeteer from 'puppeteer-core';

async function clickByText(page, text) {
  return page.evaluate((t) => {
    const els = [...document.querySelectorAll('[role="button"], [role="menuitem"], span, div')];
    const el = els.find((e) => (e.textContent || '').trim() === t);
    if (!el) return false;
    (el.closest('[role="button"]') || el).click();
    return true;
  }, text);
}

async function dumpDialog(page) {
  return page.evaluate(() => {
    const texts = [];
    document.querySelectorAll('[role="dialog"] *').forEach((el) => {
      const t = (el.textContent || '').trim();
      const aria = el.getAttribute('aria-label') || '';
      if ((t && t.length > 1 && t.length < 80 && el.children.length === 0) || aria) {
        texts.push({ t, aria: aria.slice(0, 80) });
      }
    });
    const uniq = [];
    const seen = new Set();
    for (const x of texts) {
      const key = x.t + '|' + x.aria;
      if (!seen.has(key)) {
        seen.add(key);
        uniq.push(x);
      }
    }
    return uniq.slice(0, 60);
  });
}

async function main() {
  const browser = await puppeteer.connect({ browserURL: 'http://localhost:9222', defaultViewport: null });
  const page = (await browser.pages()).find((p) => (p.url() || '').includes('permalink'));

  await page.evaluate(() => {
    const btn = [...document.querySelectorAll('[role="button"]')].find((el) =>
      (el.getAttribute('aria-label') || '').includes('Send this to friends or post it on your profile')
    );
    btn?.click();
  });
  await new Promise((r) => setTimeout(r, 2000));
  console.log('after share click:', await dumpDialog(page));

  const ok = await clickByText(page, 'Share to');
  console.log('click Share to:', ok);
  await new Promise((r) => setTimeout(r, 2500));
  console.log('after Share to:', await dumpDialog(page));

  await browser.disconnect();
}

main().catch(console.error);
