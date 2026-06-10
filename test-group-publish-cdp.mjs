import puppeteer from 'puppeteer-core';

const GROUP_URL = 'https://www.facebook.com/groups/1237486591506861';
const sleep = (ms) => new Promise((r) => setTimeout(r, ms));

async function dumpComposer(page) {
  return page.evaluate(() => {
    const dialogs = [...document.querySelectorAll('[role="dialog"]')].filter((d) => {
      const r = d.getBoundingClientRect();
      return r.width > 0 && r.height > 0;
    });
    return dialogs.map((d, i) => {
      const btns = [];
      d.querySelectorAll('[role="button"]').forEach((b) => {
        const t = (b.textContent || '').trim().slice(0, 40);
        const a = b.getAttribute('aria-label') || '';
        const dis = b.getAttribute('aria-disabled');
        if (t || a) btns.push({ t, a: a.slice(0, 70), dis });
      });
      const tb = d.querySelector('[role="textbox"]');
      const fileInput = d.querySelector('input[type="file"]');
      return { i, hasTextbox: !!tb, hasFileInput: !!fileInput, btns: btns.slice(0, 20) };
    });
  });
}

async function findPostBoxCandidates(page) {
  return page.evaluate(() => {
    const out = { buttons: [], regions: [] };
    document.querySelectorAll('[role="button"]').forEach((b) => {
      const t = (b.textContent || '').trim();
      const a = b.getAttribute('aria-label') || '';
      if (/write something|写点什么|create.*post|发帖|what.*mind|匿名|anonymous/i.test(t + ' ' + a)) {
        out.buttons.push({ t: t.slice(0, 80), a: a.slice(0, 80) });
      }
    });
    document.querySelectorAll('[role="region"], [aria-label]').forEach((el) => {
      const a = el.getAttribute('aria-label') || '';
      if (/create.*post|发帖|write/i.test(a)) out.regions.push(a.slice(0, 80));
    });
    return out;
  });
}

async function main() {
  const browser = await puppeteer.connect({ browserURL: 'http://127.0.0.1:9222', defaultViewport: null, protocolTimeout: 180000 });
  const page = (await browser.pages()).find((p) => p.url().includes('facebook.com') && !p.url().includes('fbsbx')) || (await browser.pages())[0];

  console.log('Current URL:', page.url());
  await page.goto(GROUP_URL, { waitUntil: 'domcontentloaded', timeout: 60000 });
  await sleep(4000);
  console.log('Group URL:', page.url());

  const candidates = await findPostBoxCandidates(page);
  console.log('Post box candidates:', JSON.stringify(candidates, null, 2));

  // Try multiple selectors for post box
  const openResult = await page.evaluate(() => {
    const tries = [];
    const sels = [
      'span:has(>span a[href])+div[role=button]:has(>div+div[role=none][data-visualcompletion])',
      '[aria-label*="Create a post" i]',
      '[aria-label*="Write something" i]',
      '[aria-label*="写点什么" i]',
      '[aria-label*="创建帖子" i]',
    ];
    for (const sel of sels) {
      try {
        const el = document.querySelector(sel);
        if (el) { tries.push({ sel, ok: true, tag: el.tagName, role: el.getAttribute('role'), text: (el.textContent || '').slice(0, 60) }); }
        else tries.push({ sel, ok: false });
      } catch (e) { tries.push({ sel, ok: false, err: e.message }); }
    }
    for (const btn of document.querySelectorAll('[role="button"]')) {
      const t = (btn.textContent || '').trim();
      const a = btn.getAttribute('aria-label') || '';
      if (/write something|写点什么|what.*mind|匿名发帖/i.test(t + ' ' + a)) {
        btn.click();
        return { clicked: { t: t.slice(0, 80), a: a.slice(0, 80) }, tries };
      }
    }
    const region = document.querySelector('[aria-label="Create a post" i], [aria-label*="Create a post" i]');
    if (region) {
      const btn = region.querySelector('[role="button"]') || region;
      btn.click();
      return { clicked: { via: 'region', a: region.getAttribute('aria-label') }, tries };
    }
    return { clicked: null, tries };
  });
  console.log('Open composer:', JSON.stringify(openResult, null, 2));
  await sleep(3000);

  let dialogs = await dumpComposer(page);
  console.log('Dialogs after open:', JSON.stringify(dialogs, null, 2));

  if (!dialogs.some((d) => d.hasTextbox)) {
    console.log('Composer not opened, abort');
    browser.disconnect();
    return;
  }

  // Type content
  await page.evaluate(() => {
    const dlg = [...document.querySelectorAll('[role="dialog"]')].reverse().find((d) => d.querySelector('[role="textbox"]'));
    const tb = dlg?.querySelector('[role="textbox"]');
    if (tb) {
      tb.focus();
      document.execCommand('insertText', false, 'CDP group publish test ' + Date.now());
      tb.blur();
    }
  });
  await sleep(1500);

  const media = await page.evaluate(() => {
    const dlg = [...document.querySelectorAll('[role="dialog"]')].reverse().find((d) => d.querySelector('[role="textbox"]'));
    const root = dlg || document;
    const photo = root.querySelector('[role="button"][aria-label="Photo/video"]:not([aria-disabled="true"]), [role="button"][aria-label="照片/视频"]:not([aria-disabled="true"])');
    const file = root.querySelector('input[type="file"]');
    return {
      photoBtn: photo ? { a: photo.getAttribute('aria-label'), dis: photo.getAttribute('aria-disabled') } : null,
      fileInput: !!file,
    };
  });
  console.log('Media upload UI:', media);

  const postBtn = await page.evaluate(() => {
    const dlg = [...document.querySelectorAll('[role="dialog"]')].reverse().find((d) => d.querySelector('[role="textbox"]'));
    const btn = dlg?.querySelector('[role="button"][aria-label="Post"]:not([aria-disabled="true"]), [role="button"][aria-label="发帖"]:not([aria-disabled="true"]), [role="button"][aria-label="发布"]:not([aria-disabled="true"]), [role="button"][aria-label="Submit"]:not([aria-disabled="true"]), [role="button"][aria-label="提交"]:not([aria-disabled="true"])');
    return btn ? { found: true, label: btn.getAttribute('aria-label'), text: (btn.textContent || '').trim() } : { found: false };
  });
  console.log('Post button (NOT clicking):', postBtn);

  browser.disconnect();
  console.log('Done');
}

main().catch((e) => { console.error(e); process.exit(1); });
