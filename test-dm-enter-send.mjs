import puppeteer from 'puppeteer-core';

async function test() {
  const browser = await puppeteer.connect({ browserURL: 'http://localhost:9222', defaultViewport: null });
  const page = (await browser.pages())[0];
  console.log('URL:', await page.url());

  const info = await page.evaluate(async () => {
    const delay = (ms) => new Promise(r => setTimeout(r, ms));
    const isVisible = (el) => {
      if (!el) return false;
      const r = el.getBoundingClientRect();
      return r.width > 0 && r.height > 0;
    };

    const cont = document.querySelector('[aria-label="Continue"]');
    if (cont && isVisible(cont)) { cont.click(); await delay(2000); }

    const editor = document.querySelector('div[data-lexical-editor="true"]');
    if (!editor) return { error: 'no editor' };

    editor.focus();
    const text = 'auto test ' + Date.now();
    document.execCommand('insertText', false, text);
    await delay(1000);

    // 扫描所有可见按钮找发送相关
    const allBtns = [];
    document.querySelectorAll('div[role="button"], button').forEach(el => {
      if (!isVisible(el)) return;
      const aria = el.getAttribute('aria-label') || '';
      const t = (el.textContent || '').trim();
      if (aria || t) allBtns.push({ aria, text: t.substring(0, 40), tag: el.tagName });
    });

    // 找输入区域附近的按钮
    const composeArea = editor.closest('form') || editor.parentElement?.parentElement?.parentElement;
    const nearbyBtns = [];
    if (composeArea) {
      composeArea.querySelectorAll('div[role="button"], button').forEach(el => {
        if (!isVisible(el)) return;
        nearbyBtns.push({ aria: el.getAttribute('aria-label'), text: (el.textContent||'').trim().substring(0,30), html: el.outerHTML.substring(0,200) });
      });
    }

    return { editorText: editor.textContent, allBtns: allBtns.filter(b => b.aria.toLowerCase().includes('send') || b.text.toLowerCase().includes('send')), nearbyBtns, totalBtns: allBtns.length };
  });

  console.log(JSON.stringify(info, null, 2));
  await browser.disconnect();
}

test().catch(e => { console.error(e); process.exit(1); });
