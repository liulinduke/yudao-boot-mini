import puppeteer from 'puppeteer-core';
const GROUP_URL = 'https://www.facebook.com/groups/1237486591506861';
const sleep = (ms) => new Promise((r) => setTimeout(r, ms));

async function main() {
  const browser = await puppeteer.connect({ browserURL: 'http://127.0.0.1:9222', defaultViewport: null, protocolTimeout: 180000 });
  const page = (await browser.pages()).find((p) => p.url().includes('facebook.com') && !p.url().includes('fbsbx'));
  await page.goto(GROUP_URL, { waitUntil: 'domcontentloaded', timeout: 60000 });
  await sleep(4000);

  await page.evaluate(() => {
    [...document.querySelectorAll('[role="button"]')].find((b) => /write something/i.test((b.textContent || '').trim()))?.click();
  });
  await sleep(2500);

  const before = await page.evaluate(() => {
    const dlg = [...document.querySelectorAll('[role="dialog"]')].reverse().find((d) => d.querySelector('[role="textbox"]'));
    const btn = dlg?.querySelector('[role="button"][aria-label="Post"]');
    return btn?.getAttribute('aria-disabled');
  });
  console.log('Post disabled before text:', before);

  await page.evaluate(() => {
    const tb = [...document.querySelectorAll('[role="dialog"]')].reverse().find((d) => d.querySelector('[role="textbox"]'))?.querySelector('[role="textbox"]');
    tb?.focus();
    document.execCommand('insertText', false, 'enable post test');
    tb?.blur();
  });
  await sleep(1500);

  const after = await page.evaluate(() => {
    const dlg = [...document.querySelectorAll('[role="dialog"]')].reverse().find((d) => d.querySelector('[role="textbox"]'));
    const btn = dlg?.querySelector('[role="button"][aria-label="Post"]');
    return { disabled: btn?.getAttribute('aria-disabled'), text: dlg?.querySelector('[role="textbox"]')?.textContent?.slice(0, 30) };
  });
  console.log('After typing:', after);

  browser.disconnect();
}
main().catch(console.error);
