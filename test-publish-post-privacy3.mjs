import puppeteer from 'puppeteer-core';
const sleep = (ms) => new Promise((r) => setTimeout(r, ms));

async function closeAll(page) {
  for (let i = 0; i < 5; i++) {
    await page.keyboard.press('Escape');
    await sleep(400);
  }
}

async function main() {
  const browser = await puppeteer.connect({ browserURL: 'http://127.0.0.1:9222', defaultViewport: null, protocolTimeout: 180000 });
  const page = (await browser.pages()).find((p) => p.url().includes('facebook.com') && !p.url().includes('fbsbx'));
  await closeAll(page);
  await sleep(1000);

  await page.evaluate(() => {
    const btn = [...document.querySelectorAll('[role="button"]')].find((b) => /what.s on your mind|在想什么/i.test((b.textContent || '').trim()));
    btn?.click();
  });
  await sleep(2500);

  const dialogCount = await page.evaluate(() => document.querySelectorAll('[role="dialog"]').length);
  console.log('Dialog count after open:', dialogCount);

  // blur any focus
  await page.evaluate(() => document.activeElement?.blur?.());
  await sleep(300);

  const privacyOpened = await page.evaluate(() => {
    const composer = [...document.querySelectorAll('[role="dialog"]')].find((d) => d.querySelector('[role="textbox"]'));
    if (!composer) return { ok: false, reason: 'no composer' };
    const btn = [...composer.querySelectorAll('[role="button"][aria-label]')].find((x) => (x.getAttribute('aria-label') || '').startsWith('Edit privacy'));
    if (!btn) return { ok: false, reason: 'no privacy btn', labels: [...composer.querySelectorAll('[role="button"][aria-label]')].map((b) => b.getAttribute('aria-label')).slice(0, 8) };
    btn.click();
    return { ok: true };
  });
  console.log('Privacy opened:', privacyOpened);
  await sleep(2000);

  const after = await page.evaluate(() => {
    const dialogs = [...document.querySelectorAll('[role="dialog"]')];
    return {
      count: dialogs.length,
      titles: dialogs.map((d) => (d.querySelector('h2,h3,[role=heading]')?.textContent || d.textContent || '').trim().slice(0, 80)),
      options: dialogs.flatMap((d) => [...d.querySelectorAll('[role="radio"], [role="menuitemradio"], [role="option"], div[aria-checked]')].map((el) => ({
        text: (el.textContent || '').trim().slice(0, 40),
        aria: el.getAttribute('aria-label') || '',
        role: el.getAttribute('role'),
        checked: el.getAttribute('aria-checked'),
      }))),
      listitems: dialogs.flatMap((d) => [...d.querySelectorAll('[role="listitem"], [role="menuitem"]')].map((el) => (el.textContent || '').trim().slice(0, 50)).filter(Boolean).slice(0, 10)),
    };
  });
  console.log(JSON.stringify(after, null, 2));

  browser.disconnect();
}
main().catch(console.error);
