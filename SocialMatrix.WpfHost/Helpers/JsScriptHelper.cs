using System.Text;

namespace SocialMatrix.WpfHost.Helpers
{
    /// <summary>
    /// JavaScript 脚本辅助类 - 提供通用的 JS 函数模板
    /// </summary>
    public static class JsScriptHelper
    {
        /// <summary>
        /// 获取随机延迟函数
        /// </summary>
        public static string GetRandomDelayFunction()
        {
            return @"
        const randomDelay = (min, max) => {
            return Math.floor(Math.random() * (max - min + 1)) + min;
        };";
        }

        /// <summary>
        /// 获取贝塞尔曲线鼠标轨迹模拟函数
        /// </summary>
        public static string GetMouseMovementFunction()
        {
            return @"
        // 贝塞尔曲线鼠标轨迹模拟
        const simulateMouseMovement = async (targetX, targetY) => {
            const startX = Math.random() * window.innerWidth;
            const startY = Math.random() * window.innerHeight;
            const steps = randomDelay(5, 10);
            const controlX = (startX + targetX) / 2 + randomDelay(-100, 100);
            const controlY = (startY + targetY) / 2 + randomDelay(-100, 100);
            
            for (let i = 1; i <= steps; i++) {
                const t = i / steps;
                const x = Math.pow(1-t, 2) * startX + 2 * (1-t) * t * controlX + Math.pow(t, 2) * targetX + randomDelay(-2, 2);
                const y = Math.pow(1-t, 2) * startY + 2 * (1-t) * t * controlY + Math.pow(t, 2) * targetY + randomDelay(-2, 2);
                
                const event = new MouseEvent('mousemove', { clientX: Math.floor(x), clientY: Math.floor(y), bubbles: true });
                document.dispatchEvent(event);
                await new Promise(resolve => setTimeout(resolve, randomDelay(20, 50)));
            }
        };";
        }

        /// <summary>
        /// 获取人类化滚动函数
        /// </summary>
        public static string GetHumanScrollFunction()
        {
            return @"
        // 人类化滚动
        const humanScroll = async () => {
            const viewportHeight = window.innerHeight || document.documentElement.clientHeight;
            const minScroll = Math.max(600, viewportHeight * 0.8);
            const maxScroll = Math.max(1000, viewportHeight * 1.2);
            const scrollDistance = randomDelay(Math.floor(minScroll), Math.floor(maxScroll));
            
            const scrollSteps = randomDelay(3, 7);
            const stepSize = scrollDistance / scrollSteps;
            for (let i = 0; i < scrollSteps; i++) {
                window.scrollBy({ top: stepSize + randomDelay(-10, 10), behavior: 'auto' });
                await new Promise(resolve => setTimeout(resolve, randomDelay(50, 150)));
            }
            
            const readPause = randomDelay(1000, 3000);
            await new Promise(resolve => setTimeout(resolve, readPause));
        };";
        }

        /// <summary>
        /// 获取通用采集循环模板
        /// </summary>
        public static string GetCollectionLoopTemplate(string extractFunctionName, string selector, int timeoutMs = 300000)
        {
            return $@"
        let scrollCount = 0;
        const maxScrolls = 50;
        let consecutiveNoNewItems = 0;
        const maxConsecutiveNoNew = 5;
        let finished = false;
        let interval = null;

        const finishCollection = (payload, isError) => {{
            if (finished) return;
            finished = true;
            if (interval) clearInterval(interval);
            if (isError) reject(payload);
            else resolve(payload);
        }};

        const doScroll = () => {{
            if (finished) return;
            try {{
                const cards = document.querySelectorAll('{selector}');
                console.log('Selector: ' + '{selector}' + ', Found: ' + cards.length + ' items');
                let newItemsFound = 0;

                cards.forEach(card => {{
                    if (results.length >= targetCount) return;
                    const data = {extractFunctionName}(card);
                    if (data) {{
                        results.push(data);
                        newItemsFound++;
                    }}
                }});

                if (newItemsFound > 0) {{
                    consecutiveNoNewItems = 0;
                }} else {{
                    consecutiveNoNewItems++;
                }}
                console.log('Collection progress: results=' + results.length + '/' + targetCount
                    + ', cards=' + cards.length
                    + ', newItems=' + newItemsFound
                    + ', scrollCount=' + scrollCount + '/' + maxScrolls
                    + ', noNew=' + consecutiveNoNewItems + '/' + maxConsecutiveNoNew
                    + ', scrollY=' + Math.round(window.scrollY || document.documentElement.scrollTop || 0)
                    + ', scrollHeight=' + (document.documentElement.scrollHeight || 0));

                if (results.length >= targetCount) {{
                    console.log('Collection complete: ' + results.length + '/' + targetCount);
                    finishCollection(JSON.stringify(results.slice(0, targetCount)), false);
                    return;
                }}

                if (consecutiveNoNewItems >= maxConsecutiveNoNew || scrollCount >= maxScrolls) {{
                    const stopReason = consecutiveNoNewItems >= maxConsecutiveNoNew ? 'no_new_items' : 'max_scrolls';
                    console.log('Collection ended: reason=' + stopReason
                        + ', results=' + results.length + '/' + targetCount
                        + ', cards=' + cards.length
                        + ', scrollCount=' + scrollCount + '/' + maxScrolls
                        + ', noNew=' + consecutiveNoNewItems + '/' + maxConsecutiveNoNew
                        + ', scrollY=' + Math.round(window.scrollY || document.documentElement.scrollTop || 0)
                        + ', scrollHeight=' + (document.documentElement.scrollHeight || 0));
                    finishCollection(JSON.stringify(results), false);
                    return;
                }}

                window.scrollBy({{ top: randomDelay(600, 1000), behavior: 'smooth' }});
                scrollCount++;

                const nextDelay = randomDelay(1500, 3000);
                setTimeout(doScroll, nextDelay);
            }} catch (e) {{
                console.error('Collection error:', e);
            }}
        }};
        
        interval = setInterval(doScroll, 2000);

        setTimeout(() => {{
            if (finished) return;
            if (interval) clearInterval(interval);
            if (results.length > 0) {{
                console.log('Timeout: returning ' + results.length + ' items after {timeoutMs}ms');
                finishCollection(JSON.stringify(results), false);
            }} else {{
                finishCollection(new Error('Collection timeout with no data'), true);
            }}
        }}, {timeoutMs});";
        }

        /// <summary>
        /// 创建 Promise 包装器
        /// </summary>
        public static string CreatePromiseWrapper(string body)
        {
            return $@"(function() {{
    return new Promise((resolve, reject) => {{
        const results = [];
{body}
    }});
}})();";
        }
    }
}
