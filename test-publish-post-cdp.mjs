import puppeteer from 'puppeteer-core';

const CDP = 'http://127.0.0.1:9222';

async function dumpComposer(page) {
  return page.evaluate(() => {
    const dialogs = [...document.querySelectorAll('[role="dialog"]')];
    return dialogs.map((d, i) => {
      const items = [];
      d.querySelectorAll('[role="button"], [role="radio"], label, input[type="radio"], [aria-checked]').forEach((el) => {
        const t = (el.textContent || '').trim().slice(0, 60);
        const a = el.getAttribute('aria-label') || '';
        const role = el.getAttribute('role') || el.tagName;
        const checked = el.getAttribute('aria-checked');
        if (t || a) items.push({ role, t, a: a.slice(0, 80), checked });
      });
      const textbox = d.querySelector('[role="textbox"]');
      return {
        i,
        hasTextbox: !!textbox,
        textboxFocused: textbox === document.activeElement,
        items: items.slice(0, 30),
      };
    });
  });
}

async function main() {
  const browser = await puppeteer.connect({ browserURL: CDP, defaultViewport: null });
  const pages = await browser.pages();
  const page = pages.find((p) => p.url().includes('facebook.com')) || pages[0];
  console.log('Connected:', page.url());

  // Step 1: open menu
  const menuSel = 'div[role="navigation"] div[aria-label*="Menu" i], div[role="navigation"] div[aria-label*="菜单"]';
  await page.waitForSelector(menuSel, { timeout: 15000 });
  await page.click(menuSel);
  await new Promise((r) => setTimeout(r, 1500));

  // Step 2: click Post menu item
  const postClicked = await page.evaluate(() => {
    const item = [...document.querySelectorAll('div[role="listitem"] span[id]')].find(
      (x) => x.innerText === 'Post' || x.innerText === '帖子'
    );
    if (!item) return false;
    item.click();
    return true;
  });
  console.log('Post menu clicked:', postClicked);
  await new Promise((r) => setTimeout(r, 2500));

  let dialog = await dumpComposer(page);
  console.log('After open composer:', JSON.stringify(dialog, null, 2));

  // Step 3: privacy BEFORE content
  const privacyOpen = await page.evaluate(() => {
    const btn = [...document.querySelectorAll('div[role="dialog"] div[role="button"][aria-label]')].find((x) => {
      const label = x.getAttribute('aria-label') || '';
      return label.startsWith('Edit privacy') || label.startsWith('编辑隐私') || label.includes('Public') || label.includes('公开');
    });
    if (!btn) return { ok: false, reason: 'no privacy btn' };
    btn.click();
    return { ok: true, label: btn.getAttribute('aria-label') };
  });
  console.log('Privacy open:', privacyOpen);
  await new Promise((r) => setTimeout(r, 1500));

  const privacyOptions = await page.evaluate(() => {
    const dlg = [...document.querySelectorAll('[role="dialog"]')].pop();
    if (!dlg) return [];
    const opts = [];
    dlg.querySelectorAll('[role="radio"], label, div[aria-checked], input[type="radio"]').forEach((el, i) => {
      opts.push({
        i,
        tag: el.tagName,
        role: el.getAttribute('role'),
        aria: el.getAttribute('aria-label') || '',
        text: (el.textContent || '').trim().slice(0, 50),
        checked: el.getAttribute('aria-checked'),
      });
    });
    return opts;
  });
  console.log('Privacy options:', JSON.stringify(privacyOptions, null, 2));

  // click Friends option (index 1)
  const privacySet = await page.evaluate(() => {
    const dlg = [...document.querySelectorAll('[role="dialog"]')].pop();
    if (!dlg) return false;
    const radios = [...dlg.querySelectorAll('[role="radio"], div[aria-checked], label')];
    const pick = radios.find((el) => /friends|好友/i.test(el.textContent || el.getAttribute('aria-label') || ''));
    if (pick) { pick.click(); return 'friends'; }
    if (radios[1]) { radios[1].click(); return 'index1'; }
    return false;
  });
  console.log('Privacy set:', privacySet);
  await new Promise((r) => setTimeout(r, 800));

  const done = await page.evaluate(() => {
    const btn = document.querySelector('div[role="dialog"] div[role="button"][aria-label*="Done" i], div[role="dialog"] div[role="button"][aria-label*="完成"]');
    if (btn) { btn.click(); return true; }
    return false;
  });
  console.log('Privacy done:', done);
  await new Promise((r) => setTimeout(r, 1500));

  // Step 4: type content
  await page.evaluate(() => {
    const textbox = document.querySelector('div[role="dialog"] form div[role="textbox"], div[role="dialog"] div[role="textbox"]');
    if (textbox) {
      textbox.focus();
      document.execCommand('insertText', false, 'CDP test post ' + Date.now());
    }
  });
  await new Promise((r) => setTimeout(r, 1500));
  dialog = await dumpComposer(page);
  console.log('After typing:', JSON.stringify(dialog, null, 2));

  // Step 5: try privacy AFTER typing (reproduce user issue)
  const privacyAfterType = await page.evaluate(() => {
    const btn = [...document.querySelectorAll('div[role="dialog"] div[role="button"][aria-label]')].find((x) => {
      const label = x.getAttribute('aria-label') || '';
      return label.startsWith('Edit privacy') || label.startsWith('编辑隐私') || /public|friends|private|公开|好友|仅/i.test(label);
    });
    if (!btn) return { ok: false };
    document.activeElement?.blur?.();
    btn.click();
    return { ok: true, label: btn.getAttribute('aria-label') };
  });
  console.log('Privacy after type (blur first):', privacyAfterType);

  // find Post button without clicking
  const postBtn = await page.evaluate(() => {
    const btn = document.querySelector('div[role="dialog"] div[role="button"][aria-label="Post"]:not([aria-disabled]), div[role="dialog"] div[role="button"][aria-label="发帖"]:not([aria-disabled])');
    return btn ? { found: true, label: btn.getAttribute('aria-label'), disabled: btn.getAttribute('aria-disabled') } : { found: false };
  });
  console.log('Post button:', postBtn);

  console.log('Done (Post NOT clicked to avoid spam)');
  browser.disconnect();
}

main().catch((e) => {
  console.error(e);
  process.exit(1);
});
