import puppeteer from 'puppeteer-core';

/**
 * 使用 Puppeteer 连接 CefSharp CDP 分析页面
 */

async function connectToCefSharp() {
    console.log('🔍 尝试连接到 CefSharp CDP...');
    
    try {
        const browser = await puppeteer.connect({
            browserURL: 'http://localhost:9222',
            defaultViewport: null
        });

        console.log('✅ 成功连接到 CefSharp');

        const pages = await browser.pages();
        console.log(`📄 当前打开的页面数量: ${pages.length}`);

        if (pages.length === 0) {
            console.error('❌ 没有找到任何页面');
            await browser.disconnect();
            return;
        }

        const page = pages[0];
        const url = await page.url();
        console.log(`📍 当前页面 URL: ${url}`);
        console.log(`📝 页面标题: ${await page.title()}`);

        await analyzeGroupPage(page);

        await browser.disconnect();
        console.log('🔌 已断开连接');

    } catch (error) {
        console.error('❌ 连接失败:', error.message);
    }
}

async function analyzeGroupPage(page) {
    console.log('\n🔍 开始分析群组页面结构...');

    console.log('\n🔘 查找页面上的按钮：');
    const buttons = await page.evaluate(() => {
        const result = [];
        document.querySelectorAll('button').forEach((btn, idx) => {
            const ariaLabel = btn.getAttribute('aria-label');
            const text = btn.textContent?.trim()?.substring(0, 50);
            if (ariaLabel || text) {
                result.push({ idx, ariaLabel, text });
            }
        });
        return result;
    });

    console.log(`找到 ${buttons.length} 个按钮:`);
    buttons.forEach(btn => {
        console.log(`  [${btn.idx}] aria-label="${btn.ariaLabel}" text="${btn.text}"`);
    });

    console.log('\n🎯 查找加入群组按钮：');
    const joinButtons = await page.evaluate(() => {
        const result = [];
        
        document.querySelectorAll('[aria-label*="Join"]').forEach((btn, idx) => {
            result.push({ type: 'aria-label', idx, ariaLabel: btn.getAttribute('aria-label'), text: btn.textContent?.trim()?.substring(0, 50) });
        });

        document.querySelectorAll('button').forEach((btn, idx) => {
            const text = btn.textContent?.trim() || '';
            if (text.toLowerCase() === 'join' || text.toLowerCase() === 'join group') {
                result.push({ type: 'text', idx, ariaLabel: btn.getAttribute('aria-label'), text });
            }
        });

        return result;
    });

    if (joinButtons.length > 0) {
        console.log('✅ 找到加入按钮：');
        joinButtons.forEach(btn => {
            console.log(`   ${btn.type}: aria-label="${btn.ariaLabel}" text="${btn.text}"`);
        });
    } else {
        console.log('❌ 未找到加入按钮');
    }

    console.log('\n🔍 检查当前加入状态：');
    const status = await page.evaluate(() => {
        const joinedEl = Array.from(document.querySelectorAll('span')).find(el => el.textContent?.trim() === 'Joined');
        const pendingEl = Array.from(document.querySelectorAll('span')).find(el => el.textContent?.includes('pending'));
        return { isJoined: !!joinedEl, isPending: !!pendingEl };
    });

    if (status.isJoined) {
        console.log('✅ 已加入该群组');
    } else if (status.isPending) {
        console.log('⏳ 加入申请待审核');
    } else {
        console.log('🔄 尚未加入该群组');
    }

    if (joinButtons.length > 0 && !status.isJoined && !status.isPending) {
        console.log('\n👆 尝试点击加入按钮...');
        try {
            await page.evaluate(() => {
                let joinButton = document.querySelector('[aria-label*="Join"]');
                if (!joinButton) {
                    const buttons = document.querySelectorAll('button');
                    joinButton = Array.from(buttons).find(btn => {
                        const text = btn.textContent?.trim() || '';
                        return text.toLowerCase() === 'join' || text.toLowerCase() === 'join group';
                    });
                }
                if (joinButton) {
                    joinButton.click();
                    console.log('✅ 已点击加入按钮');
                }
            });
            console.log('✅ 点击成功');
        } catch (error) {
            console.error('❌ 点击失败:', error.message);
        }
    }
}

connectToCefSharp();
