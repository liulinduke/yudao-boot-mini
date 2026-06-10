import puppeteer from 'puppeteer-core';

const CDP = 'http://127.0.0.1:9222';

async function main() {
  const browser = await puppeteer.connect({ browserURL: CDP, defaultViewport: null, protocolTimeout: 180000 });
  const pages = await browser.pages();
  const page = pages.find((p) => p.url().includes('facebook.com') && !p.url().includes('fbsbx')) || pages[0];
  console.log('URL:', page.url());

  const nav = await page.evaluate(() => {
    const out = { menuCandidates: [], createCandidates: [], postBoxCandidates: [] };
    document.querySelectorAll('[role="navigation"] [aria-label]').forEach((el) => {
      const a = el.getAttribute('aria-label') || '';
      if (/menu|菜单|account|账户|你的/i.test(a)) out.menuCandidates.push({ tag: el.tagName, role: el.getAttribute('role'), a });
    });
    document.querySelectorAll('[aria-label]').forEach((el) => {
      const a = el.getAttribute('aria-label') || '';
      if (/create|创建|post|发帖|write|写/i.test(a)) out.createCandidates.push({ tag: el.tagName, role: el.getAttribute('role'), a: a.slice(0, 80) });
    });
    document.querySelectorAll('[role="button"]').forEach((el) => {
      const t = (el.textContent || '').trim();
      if (/what.s on your mind|在想什么|创建帖子|create post/i.test(t)) {
        out.postBoxCandidates.push({ t: t.slice(0, 80), a: el.getAttribute('aria-label') || '' });
      }
    });
    return out;
  });
  console.log(JSON.stringify(nav, null, 2));

  browser.disconnect();
}

main().catch((e) => { console.error(e); process.exit(1); });
