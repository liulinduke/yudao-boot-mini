import puppeteer from 'puppeteer-core';

const sleep = (ms) => new Promise((r) => setTimeout(r, ms));

async function main() {
  const browser = await puppeteer.connect({ browserURL: 'http://localhost:9222', defaultViewport: null });
  const page = (await browser.pages()).find((p) => p.url().includes('facebook.com'));

  await page.evaluate(() => {
    document.querySelectorAll('[aria-label="Close"]').forEach((b) => b.click());
  });
  await sleep(500);

  await page.evaluate(() => {
    for (const btn of document.querySelectorAll('[role="button"]')) {
      if ((btn.getAttribute('aria-label') || '').includes('Send this to friends')) {
        btn.click();
        return;
      }
    }
  });
  await sleep(2000);

  await page.evaluate(() => {
    const d = [...document.querySelectorAll('[role="dialog"]')].pop();
    for (const btn of d?.querySelectorAll('[role="button"]') || []) {
      if (btn.getAttribute('aria-label') === 'Share to a group') {
        btn.click();
        return;
      }
    }
  });
  await sleep(2000);

  await page.evaluate(() => {
    const d = [...document.querySelectorAll('[role="dialog"]')].pop();
    const search = d?.querySelector('input[aria-label="Search for groups"]');
    if (search) {
      search.value = 'JAC Liner';
      search.dispatchEvent(new Event('input', { bubbles: true }));
    }
  });
  await sleep(2500);

  await page.evaluate(() => {
    const d = [...document.querySelectorAll('[role="dialog"]')].pop();
    for (const btn of d?.querySelectorAll('[role="button"]') || []) {
      if ((btn.textContent || '').toLowerCase().includes('jac liner')) {
        btn.click();
        return;
      }
    }
  });
  await sleep(3000);

  const state = await page.evaluate(() => {
    const dialogs = [...document.querySelectorAll('[role="dialog"]')];
    return dialogs.map((d, di) => {
      const buttons = [];
      d.querySelectorAll('[role="button"]').forEach((b) => {
        const t = (b.textContent || '').trim();
        const a = b.getAttribute('aria-label') || '';
        if (t || a) buttons.push({ t: t.slice(0, 50), a: a.slice(0, 70) });
      });
      const editors = [];
      d.querySelectorAll('[role="textbox"]').forEach((e) => {
        editors.push({
          ce: e.getAttribute('contenteditable'),
          aria: e.getAttribute('aria-label') || '',
          text: (e.textContent || '').slice(0, 40),
          vis: e.getBoundingClientRect().width > 0
        });
      });
      const inputs = [...d.querySelectorAll('input')].map((i) => ({
        type: i.type,
        aria: i.getAttribute('aria-label') || '',
        ph: i.placeholder || ''
      }));
      return { di, buttons: buttons.slice(0, 25), editors, inputs };
    });
  });

  console.log(JSON.stringify(state, null, 2));
  await browser.disconnect();
}

main().catch(console.error);
