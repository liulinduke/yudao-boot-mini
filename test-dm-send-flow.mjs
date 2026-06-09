import puppeteer from 'puppeteer-core';

async function testSendFlow() {
  const browser = await puppeteer.connect({
    browserURL: 'http://localhost:9222',
    defaultViewport: null
  });
  const page = (await browser.pages())[0];
  console.log('URL:', await page.url());

  const result = await page.evaluate(async () => {
    const delay = (ms) => new Promise(r => setTimeout(r, ms));
    const isVisible = (el) => {
      if (!el) return false;
      const r = el.getBoundingClientRect();
      const s = window.getComputedStyle(el);
      return r.width > 0 && r.height > 0 && s.display !== 'none' && s.visibility !== 'hidden';
    };

    // 点击 Continue（如果存在）
    const cont = document.querySelector('[aria-label="Continue"]');
    if (cont && isVisible(cont)) {
      cont.click();
      await delay(2000);
    }

    // 找编辑器
    const editor = document.querySelector('div[data-lexical-editor="true"]') ||
                   document.querySelector('[role="textbox"][contenteditable="true"]');
    if (!editor) return { error: 'no editor' };

    editor.focus();
    // Lexical 编辑器输入方式
    const text = 'test message hello';
    document.execCommand('selectAll', false, null);
    document.execCommand('insertText', false, text);
    editor.dispatchEvent(new InputEvent('input', { bubbles: true, data: text }));
    await delay(1500);

    // 找发送按钮
    const candidates = [];
    document.querySelectorAll('div[role="button"], button, span[role="button"]').forEach(el => {
      if (!isVisible(el)) return;
      const aria = (el.getAttribute('aria-label') || '').trim();
      const t = (el.textContent || '').trim();
      if (aria === 'Send' || aria === '发送' || aria === 'Press Enter to send' ||
          t === 'Send' || t === '发送' ||
          aria.includes('Enter to send')) {
        candidates.push({ tag: el.tagName, aria, text: t.substring(0, 30), html: el.outerHTML.substring(0, 250) });
      }
    });

    // 也查找 svg send 图标父级
    const svgParents = [];
    document.querySelectorAll('svg').forEach(svg => {
      const label = svg.getAttribute('aria-label') || '';
      const parent = svg.closest('div[role="button"], button');
      if (parent && isVisible(parent)) {
        const aria = parent.getAttribute('aria-label') || '';
        if (aria || label) svgParents.push({ svgLabel: label, parentAria: aria, parentTag: parent.tagName });
      }
    });

    const editorText = editor.textContent || editor.innerText || '';
    return { editorText: editorText.substring(0, 50), sendCandidates: candidates, svgParents: svgParents.slice(0, 10) };
  });

  console.log(JSON.stringify(result, null, 2));
  await browser.disconnect();
}

testSendFlow().catch(e => { console.error(e); process.exit(1); });
