import puppeteer from 'puppeteer-core';

async function main() {
  const browser = await puppeteer.connect({
    browserURL: 'http://localhost:9222',
    defaultViewport: null
  });
  const pages = await browser.pages();
  const page = pages.find((p) => (p.url() || '').includes('permalink')) || pages[0];

  const clickShare = await page.evaluate(() => {
    const btn = [...document.querySelectorAll('[role="button"]')].find((el) => {
      const aria = el.getAttribute('aria-label') || '';
      return aria.includes('Send this to friends or post it on your profile');
    });
    if (!btn) return { ok: false, reason: 'no share btn' };
    btn.click();
    return { ok: true };
  });
  console.log('clickShare', clickShare);
  await new Promise((r) => setTimeout(r, 2500));

  const dialog = await page.evaluate(() => {
    const dialogs = [...document.querySelectorAll('[role="dialog"]')];
    const result = { dialogCount: dialogs.length, items: [] };
    for (const d of dialogs) {
      const options = [];
      d.querySelectorAll('[role="button"], [role="menuitem"], [role="menuitemradio"], span').forEach((el) => {
        const text = (el.textContent || '').trim();
        const aria = el.getAttribute('aria-label') || '';
        if (text && text.length < 80) {
          options.push({ text, aria });
        }
      });
      result.items.push({ options: options.slice(0, 40) });
    }

    const allText = [];
    document.querySelectorAll('[role="dialog"] *').forEach((el) => {
      const t = (el.textContent || '').trim();
      if (t && t.length > 2 && t.length < 60 && el.children.length === 0) {
        allText.push(t);
      }
    });
    result.uniqueTexts = [...new Set(allText)].slice(0, 50);
    return result;
  });

  console.log(JSON.stringify(dialog, null, 2));
  await browser.disconnect();
}

main().catch(console.error);
