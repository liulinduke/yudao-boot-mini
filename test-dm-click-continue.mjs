import puppeteer from 'puppeteer-core';

async function testDmFlow() {
  const browser = await puppeteer.connect({
    browserURL: 'http://localhost:9222',
    defaultViewport: null
  });

  const pages = await browser.pages();
  const page = pages.find(async p => (await p.url()).includes('messages')) || pages[0];
  console.log('URL:', await page.url());

  // 模拟 findContinueButton + humanClick
  const clickResult = await page.evaluate(async () => {
    const randomDelay = (min, max) => new Promise(r => setTimeout(r, min + Math.random() * (max - min)));
    const isVisibleElement = (el) => {
      if (!el) return false;
      const rect = el.getBoundingClientRect();
      const style = window.getComputedStyle(el);
      return rect.width > 0 && rect.height > 0 && style.display !== 'none' && style.visibility !== 'hidden';
    };
    const normalizeText = (t) => (t || '').replace(/\s+/g, ' ').trim();

    const findContinueButton = () => {
      const selectors = ['div[role="button"]', '[aria-label*="Continue"]', 'button'];
      for (const selector of selectors) {
        for (const el of document.querySelectorAll(selector)) {
          if (!isVisibleElement(el)) continue;
          const ariaLabel = normalizeText(el.getAttribute('aria-label'));
          const text = normalizeText(el.innerText || el.textContent);
          if (ariaLabel.toLowerCase().includes('continue') || text.toLowerCase().includes('continue')) {
            return el;
          }
        }
      }
      return null;
    };

    const btn = findContinueButton();
    if (!btn) return { step: 'no_continue', found: false };

    // 尝试多种点击方式
    btn.scrollIntoView({ block: 'center' });
    btn.click();
    await randomDelay(2000, 3000);

    const editor = document.querySelector('div[data-lexical-editor="true"]');
    const contentEditable = document.querySelector('[contenteditable="true"][role="textbox"]');
    const textbox = document.querySelector('[role="textbox"]');

    return {
      step: 'after_click',
      found: true,
      tag: btn.tagName,
      ariaLabel: btn.getAttribute('aria-label'),
      editor: !!editor,
      contentEditable: !!contentEditable,
      textbox: !!textbox,
      editorInfo: editor ? editor.outerHTML.substring(0, 200) : null,
      textboxInfo: textbox ? { role: textbox.getAttribute('role'), ariaLabel: textbox.getAttribute('aria-label'), tag: textbox.tagName, html: textbox.outerHTML.substring(0, 300) } : null
    };
  });

  console.log('点击 Continue 结果:', JSON.stringify(clickResult, null, 2));

  // 再等一会看编辑器是否出现
  await new Promise(r => setTimeout(r, 3000));

  const afterWait = await page.evaluate(() => {
    const editors = [];
    document.querySelectorAll('div[data-lexical-editor="true"], [contenteditable="true"], [role="textbox"]').forEach(el => {
      const rect = el.getBoundingClientRect();
      if (rect.width > 0 && rect.height > 0) {
        editors.push({
          tag: el.tagName,
          role: el.getAttribute('role'),
          ariaLabel: el.getAttribute('aria-label'),
          dataLexical: el.getAttribute('data-lexical-editor'),
          contentEditable: el.getAttribute('contenteditable'),
          html: el.outerHTML.substring(0, 400)
        });
      }
    });

    const sendBtns = [];
    document.querySelectorAll('div[role="button"], button').forEach(el => {
      const aria = el.getAttribute('aria-label') || '';
      if (aria.includes('Send') || aria.includes('发送')) {
        sendBtns.push({ tag: el.tagName, ariaLabel: aria });
      }
    });

    const continueStill = document.querySelector('[aria-label="Continue"]');
    return { editors, sendBtns, continueStillVisible: !!continueStill };
  });

  console.log('等待后状态:', JSON.stringify(afterWait, null, 2));

  await browser.disconnect();
}

testDmFlow().catch(e => { console.error(e); process.exit(1); });
