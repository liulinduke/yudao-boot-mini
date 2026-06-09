// 模拟 DmScriptBuilder 的 Promise 包装，测试是否会因 URL 导航卡住
import puppeteer from 'puppeteer-core';

async function testScriptHang(fbUserId) {
  const browser = await puppeteer.connect({ browserURL: 'http://localhost:9222', defaultViewport: null });
  const page = (await browser.pages())[0];
  const beforeUrl = await page.url();
  console.log('执行前 URL:', beforeUrl);

  const targetUrl = `https://www.facebook.com/messages/t/${fbUserId}/`;
  console.log('targetUrl:', targetUrl);
  console.log('href === targetUrl:', beforeUrl === targetUrl);

  const script = `
    new Promise(function(resolve, reject) {
      (async function() {
        try {
          const targetUrl = '${targetUrl}';
          console.log('[test] current href:', window.location.href);
          console.log('[test] targetUrl:', targetUrl);
          if (window.location.href !== targetUrl) {
            console.log('[test] NAVIGATING - this may kill script!');
            window.location.href = targetUrl;
            await new Promise(r => setTimeout(r, 3000));
          }
          const btn = document.querySelector('[aria-label="Continue"]');
          resolve(JSON.stringify({ success: true, hasContinue: !!btn, href: location.href }));
        } catch(e) {
          reject(JSON.stringify({ success: false, message: e.message }));
        }
      })();
    });
  `;

  try {
    const result = await page.evaluate((s) => eval(s), script);
    console.log('脚本返回:', result);
  } catch (e) {
    console.log('脚本异常/超时:', e.message);
  }

  await new Promise(r => setTimeout(r, 2000));
  console.log('执行后 URL:', await page.url());
  await browser.disconnect();
}

// 从当前页面 URL 提取 fbUserId
const fbUserId = '61584830882800';
testScriptHang(fbUserId).catch(e => console.error(e));
