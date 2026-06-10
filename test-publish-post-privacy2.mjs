import puppeteer from 'puppeteer-core';
const sleep = (ms) => new Promise((r) => setTimeout(r, ms));

async function main() {
  const browser = await puppeteer.connect({ browserURL: 'http://127.0.0.1:9222', defaultViewport: null, protocolTimeout: 180000 });
  const page = (await browser.pages()).find((p) => p.url().includes('facebook.com') && !p.url().includes('fbsbx'));

  await page.evaluate(() => {
    [...document.querySelectorAll('[role="button"]')].find((b) => /what.s on your mind|在想什么/i.test((b.textContent || '').trim()))?.click();
  });
  await sleep(2000);

  await page.evaluate(() => {
    const dlg = [...document.querySelectorAll('[role="dialog"]')].find((d) => d.querySelector('[role="textbox"]'));
    const btn = dlg && [...dlg.querySelectorAll('[role="button"][aria-label]')].find((x) => (x.getAttribute('aria-label') || '').startsWith('Edit privacy'));
    btn?.click();
  });
  await sleep(2000);

  const dump = await page.evaluate(() => {
    const dialogs = [...document.querySelectorAll('[role="dialog"]')];
    return dialogs.map((d, i) => {
      const all = [];
      d.querySelectorAll('*').forEach((el) => {
        const role = el.getAttribute('role');
        const aria = el.getAttribute('aria-label') || '';
        const text = (el.childNodes.length <= 2 ? (el.textContent || '') : '').trim().slice(0, 40);
        const checked = el.getAttribute('aria-checked');
        if ((role === 'radio' || role === 'menuitemradio' || checked != null || /public|friends|only me|公开|好友|仅/i.test(text + aria)) && (text || aria)) {
          all.push({ tag: el.tagName, role, aria: aria.slice(0, 60), text, checked });
        }
      });
      return { i, count: all.length, sample: all.slice(0, 20) };
    });
  });
  console.log(JSON.stringify(dump, null, 2));

  // try clicking by text Public
  const clicked = await page.evaluate(() => {
    for (const el of document.querySelectorAll('[role="dialog"] *')) {
      const t = (el.textContent || '').trim();
      if (t === 'Public' || t === '公开') {
        el.click();
        return t;
      }
    }
    for (const el of document.querySelectorAll('[role="dialog"] [role="menuitemradio"], [role="dialog"] [role="option"]')) {
      const t = (el.textContent || '').trim();
      if (/public|friends|only|公开|好友/i.test(t)) {
        el.click();
        return t;
      }
    }
    return null;
  });
  console.log('Clicked:', clicked);

  browser.disconnect();
}
main().catch(console.error);
