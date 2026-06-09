const puppeteer = require('puppeteer-core');

/**
 * 使用 Puppeteer 连接 CefSharp CDP 分析页面
 * 
 * 前提条件：
 * 1. WPF 应用已启动（CefSharp 监听 9222 端口）
 * 2. 已导航到 Facebook 群组页面
 */

async function connectToCefSharp() {
    console.log('🔍 尝试连接到 CefSharp CDP...');
    
    try {
        // 连接到 CefSharp 的远程调试端口
        const browser = await puppeteer.connect({
            browserURL: 'http://localhost:9222',
            defaultViewport: null
        });

        console.log('✅ 成功连接到 CefSharp');

        // 获取所有页面
        const pages = await browser.pages();
        console.log(`📄 当前打开的页面数量: ${pages.length}`);

        if (pages.length === 0) {
            console.error('❌ 没有找到任何页面');
            await browser.disconnect();
            return;
        }

        // 使用第一个页面（假设是群组页面）
        const page = pages[0];
        const url = await page.url();
        console.log(`📍 当前页面 URL: ${url}`);

        // 分析页面结构，查找加群按钮
        await analyzeGroupPage(page);

        await browser.disconnect();
        console.log('🔌 已断开连接');

    } catch (error) {
        console.error('❌ 连接失败:', error.message);
        console.log('💡 请确保：');
        console.log('   1. WPF 应用已启动');
        console.log('   2. CefSharp 已打开 Facebook 群组页面');
        console.log('   3. 9222 端口未被其他程序占用');
    }
}

async function analyzeGroupPage(page) {
    console.log('\n🔍 开始分析群组页面结构...');

    // 获取页面标题
    const title = await page.title();
    console.log(`📝 页面标题: ${title}`);

    // 查找所有按钮元素
    console.log('\n🔘 查找页面上的按钮：');
    const buttons = await page.evaluate(() => {
        const result = [];
        const allButtons = document.querySelectorAll('button');
        allButtons.forEach((btn, idx) => {
            const ariaLabel = btn.getAttribute('aria-label');
            const text = btn.textContent?.trim() || '';
            const classList = Array.from(btn.classList).join(',');
            result.push({
                index: idx,
                ariaLabel,
                text: text.substring(0, 50),
                class: classList.substring(0, 100),
                tagName: btn.tagName
            });
        });
        return result;
    });

    buttons.forEach(btn => {
        console.log(`  [${btn.index}] aria-label="${btn.ariaLabel}" text="${btn.text}"`);
    });

    // 专门查找加入按钮
    console.log('\n🎯 查找加入群组按钮：');
    const joinButtons = await page.evaluate(() => {
        const result = [];
        
        // 方式1: aria-label 包含 Join
        const ariaButtons = document.querySelectorAll('[aria-label*="Join"]');
        ariaButtons.forEach((btn, idx) => {
            result.push({
                type: 'aria-label',
                index: idx,
                ariaLabel: btn.getAttribute('aria-label'),
                text: btn.textContent?.trim()?.substring(0, 50) || ''
            });
        });

        // 方式2: 包含 Join 文本的按钮
        const textButtons = document.querySelectorAll('button');
        textButtons.forEach((btn, idx) => {
            const text = btn.textContent?.trim() || '';
            if (text.toLowerCase() === 'join' || text.toLowerCase() === 'join group') {
                result.push({
                    type: 'text',
                    index: idx,
                    ariaLabel: btn.getAttribute('aria-label'),
                    text: text
                });
            }
        });

        // 方式3: data-testid
        const testidButtons = document.querySelectorAll('[data-testid*="join"]');
        testidButtons.forEach((btn, idx) => {
            result.push({
                type: 'data-testid',
                index: idx,
                ariaLabel: btn.getAttribute('aria-label'),
                text: btn.textContent?.trim()?.substring(0, 50) || ''
            });
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

    // 检查是否已加入
    console.log('\n🔍 检查当前加入状态：');
    const status = await page.evaluate(() => {
        const joinedEl = Array.from(document.querySelectorAll('span')).find(el => 
            el.textContent?.trim() === 'Joined'
        );
        const pendingEl = Array.from(document.querySelectorAll('span')).find(el => 
            el.textContent?.includes('membership is pending')
        );
        return {
            isJoined: !!joinedEl,
            isPending: !!pendingEl,
            joinedText: joinedEl?.textContent?.trim(),
            pendingText: pendingEl?.textContent?.trim()
        };
    });

    if (status.isJoined) {
        console.log(`✅ 已加入该群组: ${status.joinedText}`);
    } else if (status.isPending) {
        console.log(`⏳ 加入申请待审核: ${status.pendingText}`);
    } else {
        console.log('🔄 尚未加入该群组');
    }

    // 测试点击加入按钮
    if (joinButtons.length > 0 && !status.isJoined && !status.isPending) {
        console.log('\n👆 尝试点击加入按钮...');
        try {
            await page.evaluate(() => {
                // 查找并点击加入按钮
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
                } else {
                    console.log('❌ 点击失败：未找到按钮');
                }
            });
            console.log('✅ 点击成功');
        } catch (error) {
            console.error('❌ 点击失败:', error.message);
        }
    }
}

// 执行测试
connectToCefSharp();
