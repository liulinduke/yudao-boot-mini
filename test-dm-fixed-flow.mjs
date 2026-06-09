import puppeteer from 'puppeteer-core';

async function testFixedFlow() {
  const browser = await puppeteer.connect({ browserURL: 'http://localhost:9222', defaultViewport: null });
  const page = (await browser.pages())[0];
  console.log('URL:', await page.url());

  // Step 1: Click Continue (no navigation!)
  const clickResult = await page.evaluate(() => {
    const btn = document.querySelector('[aria-label="Continue"], [aria-label="继续"]');
    if (btn) { btn.click(); return 'clicked'; }
    return 'no_continue';
  });
  console.log('Step1 Continue:', clickResult);
  await new Promise(r => setTimeout(r, 2500));

  // Step 2: Wait for editor
  let editorReady = false;
  for (let i = 0; i < 10; i++) {
    editorReady = await page.evaluate(() => {
      const e = document.querySelector('div[data-lexical-editor="true"], [role="textbox"][contenteditable="true"]');
      if (!e) return false;
      const r = e.getBoundingClientRect();
      return r.width > 0 && r.height > 0;
    });
    if (editorReady) break;
    await new Promise(r => setTimeout(r, 500));
  }
  console.log('Step2 Editor ready:', editorReady);
  if (!editorReady) { await browser.disconnect(); return; }

  // Step 3: Input + Enter send
  const sendResult = await page.evaluate(async () => {
    const delay = (ms) => new Promise(r => setTimeout(r, ms));
    const editor = document.querySelector('div[data-lexical-editor="true"]');
    editor.focus();
    await delay(300);
    const msg = 'fixed flow test ' + Date.now();
    document.execCommand('selectAll', false, null);
    document.execCommand('delete', false, null);
    for (const ch of msg) {
      document.execCommand('insertText', false, ch);
      await delay(40);
    }
    await delay(500);
    const enterOpts = { key: 'Enter', code: 'Enter', keyCode: 13, which: 13, bubbles: true };
    editor.dispatchEvent(new KeyboardEvent('keydown', enterOpts));
    editor.dispatchEvent(new KeyboardEvent('keyup', enterOpts));
    await delay(2000);
    return { typed: editor.textContent, empty: !(editor.textContent || '').trim() };
  });
  console.log('Step3 Send result:', sendResult);

  await browser.disconnect();
}

testFixedFlow().catch(e => { console.error(e); process.exit(1); });
