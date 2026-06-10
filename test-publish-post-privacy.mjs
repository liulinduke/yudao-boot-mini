import puppeteer from 'puppeteer-core';

const CDP = 'http://127.0.0.1:9222';
const sleep = (ms) => new Promise((r) => setTimeout(r, ms));

async function openComposer(page) {
  await page.evaluate(() => {
    const btns = [...document.querySelectorAll('[role="button"]')];
    const pick = btns.find((b) => /what.s on your mind|在想什么/i.test((b.textContent || '').trim()));
    if (pick) pick.click();
  });
  await sleep(2000);
}

async function getComposerDialog(page) {
  return page.evaluate(() => {
    const dialogs = [...document.querySelectorAll('[role="dialog"]')];
    for (let i = dialogs.length - 1; i >= 0; i--) {
      if (dialogs[i].querySelector('[role="textbox"]')) return i;
    }
    return -1;
  });
}

async function main() {
  const browser = await puppeteer.connect({ browserURL: CDP, defaultViewport: null, protocolTimeout: 180000 });
  const page = (await browser.pages()).find((p) => p.url().includes('facebook.com') && !p.url().includes('fbsbx'));

  await openComposer(page);
  console.log('Composer index:', await getComposerDialog(page));

  // Type first (wrong order - reproduces issue)
  await page.evaluate(() => {
    const dlg = [...document.querySelectorAll('[role="dialog"]')].find((d) => d.querySelector('[role="textbox"]'));
    const tb = dlg?.querySelector('[role="textbox"]');
    if (tb) { tb.focus(); document.execCommand('insertText', false, 'test privacy while focused'); }
  });
  await sleep(1000);

  const tryPrivacyWithoutBlur = await page.evaluate(() => {
    const dlg = [...document.querySelectorAll('[role="dialog"]')].find((d) => d.querySelector('[role="textbox"]'));
    const btn = dlg && [...dlg.querySelectorAll('[role="button"][aria-label]')].find((x) => (x.getAttribute('aria-label') || '').startsWith('Edit privacy'));
    if (!btn) return { ok: false, step: 'no btn' };
    btn.click();
    return { ok: true, active: document.activeElement?.getAttribute?.('role') || document.activeElement?.tagName };
  });
  console.log('Privacy click WITHOUT blur:', tryPrivacyWithoutBlur);
  await sleep(1500);

  const radios1 = await page.evaluate(() => {
    const dlg = [...document.querySelectorAll('[role="dialog"]')].pop();
    return [...(dlg?.querySelectorAll('[role="radio"], div[aria-checked], label') || [])].map((el, i) => ({
      i, role: el.getAttribute('role'), text: (el.textContent || '').trim().slice(0, 40), checked: el.getAttribute('aria-checked')
    }));
  });
  console.log('Radios without blur:', radios1);

  await page.keyboard.press('Escape');
  await sleep(800);

  // Correct order: privacy BEFORE content
  await openComposer(page);

  const tryPrivacyBefore = await page.evaluate(() => {
    const dlg = [...document.querySelectorAll('[role="dialog"]')].find((d) => d.querySelector('[role="textbox"]'));
    const btn = dlg && [...dlg.querySelectorAll('[role="button"][aria-label]')].find((x) => (x.getAttribute('aria-label') || '').startsWith('Edit privacy'));
    if (!btn) return false;
    btn.click();
    return btn.getAttribute('aria-label');
  });
  console.log('Privacy BEFORE content:', tryPrivacyBefore);
  await sleep(1500);

  const privacyDlg = await page.evaluate(() => {
    const dlg = [...document.querySelectorAll('[role="dialog"]')].pop();
    const opts = [...(dlg?.querySelectorAll('[role="radio"], div[aria-checked]') || [])];
    return {
      count: opts.length,
      items: opts.map((el, i) => ({ i, text: (el.textContent || '').trim().slice(0, 50), aria: el.getAttribute('aria-label') || '', checked: el.getAttribute('aria-checked') }))
    };
  });
  console.log('Privacy options:', JSON.stringify(privacyDlg, null, 2));

  // Select Public (index 0 typically)
  const setPublic = await page.evaluate(() => {
    const dlg = [...document.querySelectorAll('[role="dialog"]')].pop();
    const opts = [...(dlg?.querySelectorAll('[role="radio"], div[aria-checked]') || [])];
    const pub = opts.find((el) => /public|公开/i.test(el.textContent || el.getAttribute('aria-label') || ''));
    if (pub) { pub.click(); return 'public'; }
    if (opts[0]) { opts[0].click(); return 'index0'; }
    return false;
  });
  console.log('Set public:', setPublic);
  await sleep(800);

  const done = await page.evaluate(() => {
    const btn = [...document.querySelectorAll('[role="dialog"] div[role="button"][aria-label]')].find((b) => /done|完成|save|保存/i.test(b.getAttribute('aria-label') || ''));
    if (btn) { btn.click(); return btn.getAttribute('aria-label'); }
    return null;
  });
  console.log('Done btn:', done);
  await sleep(1500);

  // Now type content
  await page.evaluate(() => {
    const dlg = [...document.querySelectorAll('[role="dialog"]')].find((d) => d.querySelector('[role="textbox"]'));
    const tb = dlg?.querySelector('[role="textbox"]');
    if (tb) { tb.focus(); document.execCommand('insertText', false, 'CDP publish test'); }
  });
  await sleep(1000);

  const postBtn = await page.evaluate(() => {
    const dlg = [...document.querySelectorAll('[role="dialog"]')].find((d) => d.querySelector('[role="textbox"]'));
    const btn = dlg?.querySelector('[role="button"][aria-label="Post"]:not([aria-disabled]), [role="button"][aria-label="发帖"]:not([aria-disabled])');
    return !!btn;
  });
  console.log('Post button ready:', postBtn);

  browser.disconnect();
}

main().catch((e) => { console.error(e); process.exit(1); });
