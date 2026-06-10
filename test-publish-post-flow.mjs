import puppeteer from 'puppeteer-core';
const sleep = (ms) => new Promise((r) => setTimeout(r, ms));

async function main() {
  const browser = await puppeteer.connect({ browserURL: 'http://127.0.0.1:9222', defaultViewport: null, protocolTimeout: 180000 });
  const page = (await browser.pages()).find((p) => p.url().includes('facebook.com') && !p.url().includes('fbsbx'));

  for (let i = 0; i < 5; i++) { await page.keyboard.press('Escape'); await sleep(300); }

  // open composer
  await page.evaluate(() => {
    const btn = [...document.querySelectorAll('[role="button"]')].find((b) => /what.s on your mind|在想什么/i.test((b.textContent || '').trim()));
    btn?.click();
  });
  await sleep(2500);

  // privacy BEFORE content
  await page.evaluate(() => document.activeElement?.blur?.());
  await sleep(300);
  const privacy = await page.evaluate(() => {
    const composer = [...document.querySelectorAll('[role="dialog"]')].reverse().find((d) => d.querySelector('[role="textbox"]'));
    const btn = composer && [...composer.querySelectorAll('[role="button"][aria-label]')].find((x) => (x.getAttribute('aria-label') || '').startsWith('Edit privacy'));
    if (!btn) return { ok: false };
    btn.click();
    return { ok: true };
  });
  console.log('Privacy open:', privacy);
  await sleep(1500);

  const setPublic = await page.evaluate(() => {
    for (const dlg of [...document.querySelectorAll('[role="dialog"]')].reverse()) {
      for (const el of dlg.querySelectorAll('[role="radio"], [role="menuitemradio"], [role="menuitem"], [role="button"], div[aria-checked]')) {
        const t = (el.textContent || '').trim();
        if (t === 'Public' || t === '公开') { el.click(); return t; }
      }
    }
    return null;
  });
  console.log('Set public:', setPublic);
  await sleep(800);

  await page.evaluate(() => {
    const done = [...document.querySelectorAll('[role="dialog"] [role="button"][aria-label]')].find((b) => /done|完成/i.test(b.getAttribute('aria-label') || ''));
    done?.click();
  });
  await sleep(1200);

  // type content
  await page.evaluate(() => {
    const composer = [...document.querySelectorAll('[role="dialog"]')].reverse().find((d) => d.querySelector('[role="textbox"]'));
    const tb = composer?.querySelector('[role="textbox"]');
    if (tb) { tb.focus(); document.execCommand('insertText', false, 'WPF publish test ' + Date.now()); tb.blur(); }
  });
  await sleep(1000);

  const postReady = await page.evaluate(() => {
    const composer = [...document.querySelectorAll('[role="dialog"]')].reverse().find((d) => d.querySelector('[role="textbox"]'));
    const btn = composer?.querySelector('[role="button"][aria-label="Post"]:not([aria-disabled="true"])');
    return { ready: !!btn, text: composer?.querySelector('[role="textbox"]')?.textContent?.slice(0, 40) };
  });
  console.log('Ready to post (NOT clicking):', postReady);

  browser.disconnect();
}
main().catch(console.error);
