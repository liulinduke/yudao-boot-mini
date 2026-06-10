import puppeteer from 'puppeteer-core';

const sleep = (ms) => new Promise((r) => setTimeout(r, ms));

async function main() {
  const browser = await puppeteer.connect({ browserURL: 'http://localhost:9222', defaultViewport: null });
  const page = (await browser.pages()).find((p) => p.url().includes('facebook.com'));

  const openShare = async () => {
    await page.keyboard.press('Escape');
    await sleep(500);
    return page.evaluate(() => {
      for (const btn of document.querySelectorAll('[role="button"]')) {
        const aria = (btn.getAttribute('aria-label') || '').toLowerCase();
        if (aria.includes('send this to friends or post it on your profile')) {
          btn.click();
          return true;
        }
      }
      return false;
    });
  };

  await openShare();
  await sleep(2000);

  const clickGroup = await page.evaluate(() => {
    const dialog = [...document.querySelectorAll('[role="dialog"]')].pop();
    for (const btn of dialog?.querySelectorAll('[role="button"]') || []) {
      const aria = btn.getAttribute('aria-label') || '';
      const text = (btn.textContent || '').trim();
      if (aria === 'Share to a group' || text === 'Group') {
        btn.click();
        return { ok: true, aria, text };
      }
    }
    return { ok: false };
  });
  console.log('clickGroup', clickGroup);
  await sleep(2500);

  const afterGroup = await page.evaluate(() => {
    const dialog = [...document.querySelectorAll('[role="dialog"]')].pop();
    const inputs = [...(dialog?.querySelectorAll('input') || [])].map((i) => ({
      type: i.type,
      placeholder: i.placeholder,
      aria: i.getAttribute('aria-label')
    }));
    const buttons = [];
    dialog?.querySelectorAll('[role="button"]').forEach((b) => {
      const t = (b.textContent || '').trim();
      const a = b.getAttribute('aria-label') || '';
      if (t || a) buttons.push({ t: t.slice(0, 40), a: a.slice(0, 60) });
    });
    return { inputs, buttons: buttons.slice(0, 25) };
  });
  console.log('afterGroup', JSON.stringify(afterGroup, null, 2));

  await browser.disconnect();
}

main().catch(console.error);
