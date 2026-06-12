import puppeteer from 'puppeteer-core';
const GROUP_URL = process.argv[2] || 'https://www.facebook.com/groups/1237486591506861';
const sleep = (ms) => new Promise((r) => setTimeout(r, ms));

async function main() {
  const browser = await puppeteer.connect({ browserURL: 'http://127.0.0.1:9222', defaultViewport: null, protocolTimeout: 180000 });
  const pages = await browser.pages();
  console.log('Pages:', pages.map((p) => p.url()).join('\n  '));

  let page = pages.find((p) => p.url().includes('/groups/') && !p.url().includes('fbsbx'));
  if (!page) {
    page = pages.find((p) => p.url().includes('facebook.com') && !p.url().includes('fbsbx'));
  }
  if (!page) throw new Error('no fb page');

  if (!page.url().includes('/groups/1237486591506861')) {
    await page.goto(GROUP_URL, { waitUntil: 'domcontentloaded', timeout: 120000 }).catch(() => {});
    await sleep(5000);
  }
  console.log('Using:', page.url());

  const dump = await page.evaluate(() => {
    const norm = (t) => (t || '').replace(/\s+/g, ' ').trim();
    const vis = (el) => {
      if (!el) return false;
      const r = el.getBoundingClientRect();
      const s = getComputedStyle(el);
      return r.width > 0 && r.height > 0 && s.display !== 'none' && s.visibility !== 'hidden';
    };
    const buttons = [];
    document.querySelectorAll('[role="button"], [role="link"]').forEach((el) => {
      if (!vis(el)) return;
      const t = norm(el.textContent);
      const a = el.getAttribute('aria-label') || '';
      if (t.length > 80) return;
      if (/write|post|发帖|匿名|anonymous|comment|评论|something|创建/i.test(t + ' ' + a) || t.length < 40) {
        buttons.push({ tag: el.tagName, role: el.getAttribute('role'), t, a: a.slice(0, 80) });
      }
    });
    const cssHit = document.querySelector('span:has(>span a[href])+div[role=button]:has(>div+div[role=none][data-visualcompletion])');
    const main = document.querySelector('[role="main"]');
    return {
      title: document.title,
      href: location.href,
      cssSelectorHit: !!cssHit,
      cssText: cssHit ? norm(cssHit.textContent) : null,
      mainText: main ? norm(main.innerText).slice(0, 300) : null,
      buttons: buttons.slice(0, 40),
    };
  });
  console.log(JSON.stringify(dump, null, 2));
  browser.disconnect();
}
main().catch((e) => { console.error(e); process.exit(1); });
