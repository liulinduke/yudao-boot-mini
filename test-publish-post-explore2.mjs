import puppeteer from 'puppeteer-core';

const CDP = 'http://127.0.0.1:9222';
const sleep = (ms) => new Promise((r) => setTimeout(r, ms));

async function dumpDialogs(page) {
  return page.evaluate(() => {
    return [...document.querySelectorAll('[role="dialog"]')].map((d, i) => {
      const btns = [];
      d.querySelectorAll('[role="button"], [role="radio"], label, div[aria-checked]').forEach((el) => {
        const t = (el.textContent || '').trim().slice(0, 50);
        const a = el.getAttribute('aria-label') || '';
        if (t || a) btns.push({ role: el.getAttribute('role') || el.tagName, t, a: a.slice(0, 70), checked: el.getAttribute('aria-checked') });
      });
      return { i, btns: btns.slice(0, 25), hasTextbox: !!d.querySelector('[role="textbox"]') };
    });
  });
}

async function main() {
  const browser = await puppeteer.connect({ browserURL: CDP, defaultViewport: null, protocolTimeout: 180000 });
  const page = (await browser.pages()).find((p) => p.url() === 'https://www.facebook.com/' || p.url().startsWith('https://www.facebook.com/?'));
  console.log('URL:', page?.url());

  // Method A: click "What's on your mind"
  const openedA = await page.evaluate(() => {
    const btns = [...document.querySelectorAll('[role="button"]')];
    const pick = btns.find((b) => /what.s on your mind|在想什么|创建帖子/i.test((b.textContent || '').trim()));
    if (!pick) return false;
    pick.click();
    return (pick.textContent || '').trim().slice(0, 60);
  });
  console.log('Opened via feed box:', openedA);
  await sleep(2500);
  console.log('Dialogs A:', JSON.stringify(await dumpDialogs(page), null, 2));

  // close
  await page.keyboard.press('Escape');
  await sleep(1000);

  // Method B: Facebook menu -> Post
  const menuOk = await page.evaluate(() => {
    const menu = document.querySelector('[aria-label="Facebook menu"], [aria-label*="Facebook menu" i]');
    if (!menu) return { ok: false, reason: 'no menu' };
    menu.click();
    return { ok: true };
  });
  console.log('Menu:', menuOk);
  await sleep(1500);

  const postItem = await page.evaluate(() => {
    const spans = [...document.querySelectorAll('div[role="listitem"] span[id], div[role="menuitem"] span, a[role="menuitem"]')];
    const found = spans.map((s) => (s.textContent || '').trim()).filter(Boolean).slice(0, 20);
    const item = spans.find((x) => ['Post', '帖子', 'Create post', '发帖'].includes((x.textContent || '').trim()));
    if (item) { item.click(); return { clicked: (item.textContent || '').trim(), all: found }; }
    return { clicked: null, all: found };
  });
  console.log('Post menu item:', postItem);
  await sleep(2500);
  console.log('Dialogs B:', JSON.stringify(await dumpDialogs(page), null, 2));

  // privacy in composer
  const privacy = await page.evaluate(() => {
    const dlg = [...document.querySelectorAll('[role="dialog"]')].pop();
    if (!dlg) return null;
    const btn = [...dlg.querySelectorAll('[role="button"][aria-label]')].find((x) => {
      const a = x.getAttribute('aria-label') || '';
      return /edit privacy|编辑隐私|audience|受众|public|friends|好友|private|仅/i.test(a);
    });
    return btn ? { label: btn.getAttribute('aria-label') } : { labels: [...dlg.querySelectorAll('[role="button"][aria-label]')].map((b) => b.getAttribute('aria-label')).slice(0, 10) };
  });
  console.log('Privacy btn:', privacy);

  browser.disconnect();
}

main().catch((e) => { console.error(e); process.exit(1); });
