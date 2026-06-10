import puppeteer from 'puppeteer-core';
const sleep = (ms) => new Promise((r) => setTimeout(r, ms));

async function main() {
  const browser = await puppeteer.connect({ browserURL: 'http://127.0.0.1:9222', defaultViewport: null, protocolTimeout: 180000 });
  const page = (await browser.pages()).find((p) => p.url().includes('facebook.com') && !p.url().includes('fbsbx'));
  for (let i = 0; i < 5; i++) { await page.keyboard.press('Escape'); await sleep(300); }

  await page.evaluate(() => {
    [...document.querySelectorAll('[role="button"]')].find((b) => /what.s on your mind/i.test((b.textContent || '').trim()))?.click();
  });
  await sleep(2000);
  await page.evaluate(() => document.activeElement?.blur?.());
  await page.evaluate(() => {
    const c = [...document.querySelectorAll('[role="dialog"]')].reverse().find((d) => d.querySelector('[role="textbox"]'));
    [...(c?.querySelectorAll('[role="button"][aria-label]') || [])].find((x) => (x.getAttribute('aria-label') || '').startsWith('Edit privacy'))?.click();
  });
  await sleep(2000);

  const hits = await page.evaluate(() => {
    const out = [];
    document.querySelectorAll('*').forEach((el) => {
      const t = (el.textContent || '').trim();
      if (['Public', 'Friends', 'Only me', '公开', '好友', '仅自己'].includes(t) && el.children.length <= 2) {
        out.push({ tag: el.tagName, role: el.getAttribute('role'), aria: (el.getAttribute('aria-label') || '').slice(0, 50), t, rect: el.getBoundingClientRect().width > 0 });
      }
    });
    return out.slice(0, 30);
  });
  console.log(JSON.stringify(hits, null, 2));
  browser.disconnect();
}
main().catch(console.error);
