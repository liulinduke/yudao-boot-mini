import puppeteer from 'puppeteer-core';

async function test() {
    try {
        console.log('🔌 尝试连接到 CefSharp CDP...');
        const browser = await puppeteer.connect({
            browserURL: 'http://127.0.0.1:9222',
            defaultViewport: null
        });
        console.log('✅ 成功连接到 CefSharp');

        const pages = await browser.pages();
        console.log(`📄 当前打开的页面数量: ${pages.length}`);

        if (pages.length === 0) {
            console.log('❌ 没有打开的页面');
            return;
        }

        const page = pages[0];
        const url = page.url();
        console.log(`📍 当前页面 URL: ${url}`);

        const title = await page.title();
        console.log(`🎯 页面标题: ${title}`);

        // 先执行一个简单的测试脚本，看看是否有输出
        console.log('📝 执行测试脚本...');
        
        await page.evaluate(() => {
            console.log('========== 测试脚本执行 ==========');
            console.log('✅ 脚本执行成功');
            console.log('📋 document.readyState:', document.readyState);
            console.log('🖼️ 页面URL:', window.location.href);
            console.log('====================================');
        });

        // 查找按钮
        console.log('🔍 查找页面上的按钮...');
        
        const buttons = await page.evaluate(() => {
            const result = [];
            const allButtons = document.querySelectorAll('button, div[role=button], span[role=button]');
            console.log('🔍 找到', allButtons.length, '个按钮');
            
            for (let i = 0; i < allButtons.length; i++) {
                const btn = allButtons[i];
                const ariaLabel = btn.getAttribute('aria-label') || '';
                const text = btn.textContent || '';
                const classList = Array.from(btn.classList).join(',');
                
                if (ariaLabel.toLowerCase().includes('join') || text.toLowerCase().includes('join')) {
                    result.push({
                        index: i,
                        ariaLabel: ariaLabel,
                        text: text.trim(),
                        classList: classList,
                        visible: btn.offsetParent !== null
                    });
                    console.log('✅ 找到加入按钮 #', i, ':', ariaLabel, text.trim());
                }
            }
            
            return result;
        });

        console.log(`🎉 找到 ${buttons.length} 个相关按钮`);
        
        if (buttons.length > 0) {
            console.log('🔘 点击第一个按钮...');
            await page.evaluate((idx) => {
                const btn = document.querySelectorAll('button, div[role=button], span[role=button]')[idx];
                if (btn) {
                    console.log('👆 点击按钮:', btn.getAttribute('aria-label') || btn.textContent);
                    btn.click();
                    return true;
                }
                return false;
            }, buttons[0].index);
            console.log('✅ 点击命令已发送');
        }

        // 等待一下再断开
        await new Promise(r => setTimeout(r, 2000));

        await browser.disconnect();
        console.log('🔴 已断开连接');
    } catch (error) {
        console.error('❌ 错误:', error.message);
        console.error(error.stack);
    }
}

test();
