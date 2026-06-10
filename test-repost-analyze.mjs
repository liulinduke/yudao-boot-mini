import puppeteer from 'puppeteer-core';

const postUrl = 'https://www.facebook.com/share/p/1BEpgnxWR2/';

async function main() {
  const browser = await puppeteer.connect({
    browserURL: 'http://localhost:9222',
    defaultViewport: null
  });

  const pages = await browser.pages();
  console.log('pages:', pages.length);
  for (const p of pages) {
    console.log(' -', await p.url());
  }

  let page = pages.find((p) => (p.url() || '').includes('facebook.com')) || pages[0];
  if (!page) {
    console.error('no page');
    await browser.disconnect();
    return;
  }

  if (!page.url().includes('1BEpgnxWR2')) {
    console.log('navigating to', postUrl);
    await page.goto(postUrl, { waitUntil: 'networkidle2', timeout: 60000 }).catch((e) =>
      console.log('goto warn:', e.message)
    );
    await new Promise((r) => setTimeout(r, 4000));
  }

  console.log('current url:', page.url());

  const info = await page.evaluate(() => {
    const btns = [];
    document.querySelectorAll('[role="button"], button').forEach((el, i) => {
      const aria = el.getAttribute('aria-label') || '';
      const text = (el.textContent || '').trim().slice(0, 50);
      if (/like|share|赞|分享|发送|send|comment|评论|转发/i.test(aria + text)) {
        btns.push({ i, aria, text });
      }
    });

    const shareEls = [...document.querySelectorAll('[role="button"]')].filter((el) =>
      /share|分享|send|发送/i.test(el.getAttribute('aria-label') || '')
    );

    return {
      title: document.title,
      btns: btns.slice(0, 40),
      shareLabels: shareEls.slice(0, 15).map((el) => el.getAttribute('aria-label'))
    };
  });

  console.log(JSON.stringify(info, null, 2));
  await browser.disconnect();
}

main().catch((e) => {
  console.error(e);
  process.exit(1);
});
