import puppeteer from 'puppeteer-core';

async function analyzeDmPage() {
  console.log('🔍 连接 CefSharp CDP (9222)...');
  const browser = await puppeteer.connect({
    browserURL: 'http://localhost:9222',
    defaultViewport: null
  });

  const pages = await browser.pages();
  console.log(`📄 页面数量: ${pages.length}`);

  // 找 Facebook messages 页面
  let page = null;
  for (const p of pages) {
    const url = await p.url();
    console.log(`  - ${url}`);
    if (url.includes('facebook.com/messages') || url.includes('facebook.com')) {
      page = p;
    }
  }
  if (!page) page = pages[pages.length - 1];

  const url = await page.url();
  const title = await page.title();
  console.log(`\n📍 分析页面: ${url}`);
  console.log(`📝 标题: ${title}`);

  const analysis = await page.evaluate(() => {
    const normalizeText = (text) => (text || '').replace(/\s+/g, ' ').trim();
    const isVisibleElement = (el) => {
      if (!el) return false;
      const rect = el.getBoundingClientRect();
      const style = window.getComputedStyle(el);
      return rect.width > 0 && rect.height > 0 && style.display !== 'none' && style.visibility !== 'hidden';
    };

    const result = {
      url: location.href,
      continueButtons: [],
      editors: [],
      sendButtons: [],
      dialogs: [],
      allButtonsSample: [],
      pinInputs: []
    };

    // Continue 按钮
    const selectors = ['button', 'div[role="button"]', 'span[role="button"]', 'a[role="button"]', '[aria-label*="Continue"]', '[aria-label*="continue"]', '[aria-label*="继续"]'];
    for (const selector of selectors) {
      document.querySelectorAll(selector).forEach((el, idx) => {
        if (!isVisibleElement(el)) return;
        const ariaLabel = normalizeText(el.getAttribute('aria-label'));
        const text = normalizeText(el.innerText || el.textContent);
        const title = normalizeText(el.getAttribute('title'));
        if (ariaLabel.toLowerCase().includes('continue') || ariaLabel.includes('继续') ||
            text.toLowerCase().includes('continue') || text.includes('继续') ||
            title.toLowerCase().includes('continue') || title.includes('继续')) {
          result.continueButtons.push({
            selector,
            tag: el.tagName,
            ariaLabel,
            text: text.substring(0, 80),
            title,
            className: (el.className || '').toString().substring(0, 100),
            outerHTML: el.outerHTML.substring(0, 300)
          });
        }
      });
    }

    // 编辑器
    document.querySelectorAll('div[data-lexical-editor="true"], div[contenteditable="true"], [role="textbox"]').forEach((el, idx) => {
      if (!isVisibleElement(el)) return;
      result.editors.push({
        tag: el.tagName,
        dataLexical: el.getAttribute('data-lexical-editor'),
        role: el.getAttribute('role'),
        ariaLabel: el.getAttribute('aria-label'),
        className: (el.className || '').toString().substring(0, 100),
        outerHTML: el.outerHTML.substring(0, 300)
      });
    });

    // 发送按钮
    document.querySelectorAll('button, div[role="button"], span[role="button"]').forEach((el) => {
      if (!isVisibleElement(el)) return;
      const ariaLabel = normalizeText(el.getAttribute('aria-label'));
      const text = normalizeText(el.innerText || el.textContent);
      if (ariaLabel.includes('Send') || ariaLabel.includes('发送') || text === 'Send' || text === '发送') {
        result.sendButtons.push({ tag: el.tagName, ariaLabel, text, outerHTML: el.outerHTML.substring(0, 300) });
      }
    });

    // 对话框
    document.querySelectorAll('[role="dialog"]').forEach((el) => {
      if (!isVisibleElement(el)) return;
      result.dialogs.push({
        ariaLabel: el.getAttribute('aria-label'),
        text: normalizeText(el.innerText).substring(0, 200),
        outerHTML: el.outerHTML.substring(0, 500)
      });
    });

    // PIN
    document.querySelectorAll('input[type="number"], input[aria-label*="PIN"], input[aria-label*="pin"]').forEach((el) => {
      if (!isVisibleElement(el)) return;
      result.pinInputs.push({ ariaLabel: el.getAttribute('aria-label'), type: el.type });
    });

    // 采样前20个可见按钮
    let count = 0;
    document.querySelectorAll('button, div[role="button"]').forEach((el) => {
      if (!isVisibleElement(el) || count >= 30) return;
      const ariaLabel = normalizeText(el.getAttribute('aria-label'));
      const text = normalizeText(el.innerText || el.textContent);
      if (ariaLabel || text) {
        result.allButtonsSample.push({ tag: el.tagName, ariaLabel, text: text.substring(0, 60) });
        count++;
      }
    });

    return result;
  });

  console.log('\n=== Continue 按钮 ===');
  console.log(JSON.stringify(analysis.continueButtons, null, 2));

  console.log('\n=== 编辑器 ===');
  console.log(JSON.stringify(analysis.editors, null, 2));

  console.log('\n=== 发送按钮 ===');
  console.log(JSON.stringify(analysis.sendButtons, null, 2));

  console.log('\n=== 对话框 ===');
  console.log(JSON.stringify(analysis.dialogs, null, 2));

  console.log('\n=== PIN 输入 ===');
  console.log(JSON.stringify(analysis.pinInputs, null, 2));

  console.log('\n=== 可见按钮采样 ===');
  analysis.allButtonsSample.forEach((b, i) => console.log(`  [${i}] ${b.tag} aria="${b.ariaLabel}" text="${b.text}"`));

  await browser.disconnect();
}

analyzeDmPage().catch(e => {
  console.error('❌', e.message);
  process.exit(1);
});
