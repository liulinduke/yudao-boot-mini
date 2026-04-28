using System;
using System.Threading.Tasks;
using CefSharp;
using CefSharp.Wpf;

namespace SocialMatrix.WpfHost.Windows
{
    /// <summary>
    /// BrowserMatrixWindow 的私信发送功能扩展
    /// </summary>
    public partial class BrowserMatrixWindow
    {
        /// <summary>
        /// 生成私信发送脚本
        /// </summary>
        private string GenerateDmSendScript(string fbUserId, string messageText)
        {
            var js = new System.Text.StringBuilder();
            
            js.AppendLine("(async function() {");
            js.AppendLine("    try {");
            js.AppendLine($"        console.log('[私信发送] 开始向用户 {fbUserId} 发送消息');");
            js.AppendLine("");
            
            // 1. 导航到私信页面（如果不在的话）
            js.AppendLine($"        const targetUrl = 'https://www.facebook.com/messages/t/{fbUserId}/';");
            js.AppendLine("        if (window.location.href !== targetUrl) {");
            js.AppendLine("            console.log('[私信发送] 导航到:', targetUrl);");
            js.AppendLine("            window.location.href = targetUrl;");
            js.AppendLine("            ");
            js.AppendLine("            // 等待新页面加载");
            js.AppendLine("            await new Promise((resolve) => {");
            js.AppendLine("                setTimeout(resolve, 3000);");
            js.AppendLine("            });");
            js.AppendLine("        }");
            js.AppendLine("");
            
            // 2. 等待页面加载完成（最多15秒）
            js.AppendLine("        // 等待私信编辑器出现");
            js.AppendLine("        await new Promise((resolve, reject) => {");
            js.AppendLine("            const timeout = setTimeout(() => reject(new Error('页面加载超时')), 15000);");
            js.AppendLine("            ");
            js.AppendLine("            const checkInterval = setInterval(() => {");
            js.AppendLine("                const editor = document.querySelector('div[data-lexical-editor=true]');");
            js.AppendLine("                if (editor) {");
            js.AppendLine("                    clearTimeout(timeout);");
            js.AppendLine("                    clearInterval(checkInterval);");
            js.AppendLine("                    resolve();");
            js.AppendLine("                }");
            js.AppendLine("            }, 500);");
            js.AppendLine("        });");
            js.AppendLine("        console.log('[私信发送] 页面加载完成');");
            js.AppendLine("");
            
            // 3. 检查是否达到上限
            js.AppendLine("        // 检查是否达到发送上限");
            js.AppendLine("        const pageText = document.body.innerText;");
            js.AppendLine("        if (pageText.includes('limit') || pageText.includes('上限') || pageText.includes('terbatas')) {");
            js.AppendLine("            throw new Error('24小时内陌生人发送已达上限');");
            js.AppendLine("        }");
            js.AppendLine("        console.log('[私信发送] 未达到发送上限');");
            js.AppendLine("");
            
            // 添加辅助函数（带人类行为模拟）
            js.AppendLine("");
            js.AppendLine("    // ===== 辅助函数（带人类行为模拟）=====");
            js.AppendLine("");
            
            // 随机延迟（使用正态分布，更接近人类行为）
            js.AppendLine("    const randomDelay = (min, max) => {");
            js.AppendLine("        // 使用 Box-Muller 变换生成正态分布的随机数");
            js.AppendLine("        const u1 = Math.random();");
            js.AppendLine("        const u2 = Math.random();");
            js.AppendLine("        const z = Math.sqrt(-2.0 * Math.log(u1)) * Math.cos(2.0 * Math.PI * u2);");
            js.AppendLine("        const mean = (min + max) / 2;");
            js.AppendLine("        const stdDev = (max - min) / 6; // 99.7% 的值在范围内");
            js.AppendLine("        const delay = Math.max(min, Math.min(max, mean + z * stdDev));");
            js.AppendLine("        return new Promise(resolve => setTimeout(resolve, Math.floor(delay)));");
            js.AppendLine("    };");
            js.AppendLine("");
            
            // 贝塞尔曲线鼠标轨迹模拟
            js.AppendLine("    // 贝塞尔曲线鼠标轨迹模拟");
            js.AppendLine("    const simulateMouseMovement = async (targetElement) => {");
            js.AppendLine("        try {");
            js.AppendLine("            if (!targetElement) return;");
            js.AppendLine("            ");
            js.AppendLine("            const rect = targetElement.getBoundingClientRect();");
            js.AppendLine("            const targetX = rect.left + rect.width / 2;");
            js.AppendLine("            const targetY = rect.top + rect.height / 2;");
            js.AppendLine("            ");
            js.AppendLine("            // 生成贝塞尔曲线路径点");
            js.AppendLine("            const startX = Math.random() * window.innerWidth;");
            js.AppendLine("            const startY = Math.random() * window.innerHeight;");
            js.AppendLine("            const controlX = (startX + targetX) / 2 + (Math.random() - 0.5) * 200;");
            js.AppendLine("            const controlY = (startY + targetY) / 2 + (Math.random() - 0.5) * 200;");
            js.AppendLine("            ");
            js.AppendLine("            // 生成路径点（20个中间点）");
            js.AppendLine("            const steps = 20;");
            js.AppendLine("            for (let i = 0; i <= steps; i++) {");
            js.AppendLine("                const t = i / steps;");
            js.AppendLine("                // 二次贝塞尔曲线公式");
            js.AppendLine("                const x = Math.pow(1-t, 2) * startX + 2 * (1-t) * t * controlX + Math.pow(t, 2) * targetX;");
            js.AppendLine("                const y = Math.pow(1-t, 2) * startY + 2 * (1-t) * t * controlY + Math.pow(t, 2) * targetY;");
            js.AppendLine("                ");
            js.AppendLine("                // 添加微小抖动");
            js.AppendLine("                const jitterX = x + (Math.random() - 0.5) * 4;");
            js.AppendLine("                const jitterY = y + (Math.random() - 0.5) * 4;");
            js.AppendLine("                ");
            js.AppendLine("                // 触发 mousemove 事件");
            js.AppendLine("                const event = new MouseEvent('mousemove', {");
            js.AppendLine("                    view: window,");
            js.AppendLine("                    bubbles: true,");
            js.AppendLine("                    cancelable: true,");
            js.AppendLine("                    clientX: jitterX,");
            js.AppendLine("                    clientY: jitterY");
            js.AppendLine("                });");
            js.AppendLine("                document.dispatchEvent(event);");
            js.AppendLine("                ");
            js.AppendLine("                // 随机延迟（模拟人手移动速度变化）");
            js.AppendLine("                await randomDelay(30, 80);");
            js.AppendLine("            }");
            js.AppendLine("        } catch (e) {");
            js.AppendLine("            console.warn('[私信] 鼠标轨迹模拟失败:', e);");
            js.AppendLine("        }");
            js.AppendLine("    };");
            js.AppendLine("");
            
            // 模拟人类点击（包含鼠标移动 + 停留 + 点击）
            js.AppendLine("    // 模拟人类点击行为");
            js.AppendLine("    const humanClick = async (element) => {");
            js.AppendLine("        try {");
            js.AppendLine("            if (!element) return false;");
            js.AppendLine("            ");
            js.AppendLine("            // 1. 鼠标移动到元素");
            js.AppendLine("            await simulateMouseMovement(element);");
            js.AppendLine("            ");
            js.AppendLine("            // 2. 短暂停留（模拟人眼确认）");
            js.AppendLine("            await randomDelay(100, 300);");
            js.AppendLine("            ");
            js.AppendLine("            // 3. 执行点击");
            js.AppendLine("            element.click();");
            js.AppendLine("            return true;");
            js.AppendLine("        } catch (e) {");
            js.AppendLine("            console.warn('[私信] 人类点击失败:', e);");
            js.AppendLine("            return false;");
            js.AppendLine("        }");
            js.AppendLine("    };");
            js.AppendLine("");
            
            // 模拟人类打字（速度变化 + 偶尔停顿）
            js.AppendLine("    // 模拟人类打字行为");
            js.AppendLine("    const humanTypeText = async (element, text) => {");
            js.AppendLine("        try {");
            js.AppendLine("            if (!element || !text) return false;");
            js.AppendLine("            ");
            js.AppendLine("            element.focus();");
            js.AppendLine("            await randomDelay(200, 500);");
            js.AppendLine("            ");
            js.AppendLine("            // 逐字输入，速度有变化");
            js.AppendLine("            for (let i = 0; i < text.length; i++) {");
            js.AppendLine("                const char = text[i];");
            js.AppendLine("                ");
            js.AppendLine("                // 使用 insertText 命令插入字符");
            js.AppendLine("                document.execCommand('insertText', false, char);");
            js.AppendLine("                ");
            js.AppendLine("                // 打字速度变化（基础速度 + 随机波动）");
            js.AppendLine("                let delay = randomDelay(80, 200);");
            js.AppendLine("                ");
            js.AppendLine("                // 偶尔停顿（模拟思考或犹豫）");
            js.AppendLine("                if (Math.random() < 0.1) { // 10% 概率停顿");
            js.AppendLine("                    await randomDelay(500, 1500);");
            js.AppendLine("                }");
            js.AppendLine("                ");
            js.AppendLine("                // 标点符号后停顿更长");
            js.AppendLine("                if (['.', ',', '!', '?', '。', '，', '！', '？'].includes(char)) {");
            js.AppendLine("                    await randomDelay(300, 800);");
            js.AppendLine("                }");
            js.AppendLine("                ");
            js.AppendLine("                await delay;");
            js.AppendLine("            }");
            js.AppendLine("            ");
            js.AppendLine("            return true;");
            js.AppendLine("        } catch (e) {");
            js.AppendLine("            console.warn('[私信] 人类打字失败:', e);");
            js.AppendLine("            return false;");
            js.AppendLine("        }");
            js.AppendLine("    };");
            js.AppendLine("");
            
            // 4. 插入文本（带人类行为模拟）
            js.AppendLine("        // 查找私信编辑器");
            js.AppendLine("        const editor = document.querySelector('div[data-lexical-editor=true]');");
            js.AppendLine("        if (!editor) {");
            js.AppendLine("            throw new Error('未找到私信编辑器');");
            js.AppendLine("        }");
            js.AppendLine("");
            js.AppendLine("        // 使用人类打字行为输入消息");
            js.AppendLine($"        const message = `{messageText.Replace("`", "\\`")}`;");
            js.AppendLine("        await humanTypeText(editor, message);");
            js.AppendLine("        console.log('[私信发送] 消息输入完成');");
            js.AppendLine("");
            
            // 5. 点击发送按钮（带人类行为模拟）
            js.AppendLine("        // 等待一下再发送");
            js.AppendLine("        await randomDelay(500, 1000);");
            js.AppendLine("");
            js.AppendLine("        // 查找发送按钮（多种方式）");
            js.AppendLine("        let sendButton = null;");
            js.AppendLine("        ");
            js.AppendLine("        // 方式1: 查找 aria-label 包含 'enter' 或 'send' 的按钮");
            js.AppendLine("        const buttons = Array.from(document.querySelectorAll('div[role=button], button, span[role=button]'));");
            js.AppendLine("        for (const btn of buttons) {");
            js.AppendLine("            const ariaLabel = btn.getAttribute('aria-label') || '';");
            js.AppendLine("            const text = btn.innerText || '';");
            js.AppendLine("            if (ariaLabel.toLowerCase().includes('enter') || ");
            js.AppendLine("                ariaLabel.toLowerCase().includes('send') ||");
            js.AppendLine("                text.toLowerCase().includes('send') ||");
            js.AppendLine("                text.toLowerCase().includes('kirim')) {");
            js.AppendLine("                sendButton = btn;");
            js.AppendLine("                break;");
            js.AppendLine("            }");
            js.AppendLine("        }");
            js.AppendLine("        ");
            js.AppendLine("        // 方式2: 如果没找到，尝试查找特定位置的按钮");
            js.AppendLine("        if (!sendButton) {");
            js.AppendLine("            sendButton = document.querySelector('div[role=group] > span:nth-child(3) > div[role=button]');");
            js.AppendLine("        }");
            js.AppendLine("        ");
            js.AppendLine("        if (!sendButton) {");
            js.AppendLine("            throw new Error('未找到发送按钮');");
            js.AppendLine("        }");
            js.AppendLine("");
            js.AppendLine("        console.log('[私信发送] 点击发送按钮');");
            js.AppendLine("        // 使用人类点击行为");
            js.AppendLine("        await humanClick(sendButton);");
            js.AppendLine("");
            
            // 6. 等待发送完成
            js.AppendLine("        // 等待发送完成（检查是否有新消息出现）");
            js.AppendLine("        await new Promise((resolve, reject) => {");
            js.AppendLine("            const timeout = setTimeout(() => reject(new Error('发送超时')), 10000);");
            js.AppendLine("            ");
            js.AppendLine("            const checkInterval = setInterval(() => {");
            js.AppendLine("                // 检查是否有新的消息气泡");
            js.AppendLine("                const messages = document.querySelectorAll('span[role=presentation] span.html-span');");
            js.AppendLine("                if (messages.length > 0) {");
            js.AppendLine("                    clearTimeout(timeout);");
            js.AppendLine("                    clearInterval(checkInterval);");
            js.AppendLine("                    resolve();");
            js.AppendLine("                }");
            js.AppendLine("            }, 500);");
            js.AppendLine("        });");
            js.AppendLine("        console.log('[私信发送] 发送成功');");
            js.AppendLine("");
            
            // 7. 返回成功结果
            js.AppendLine("        return JSON.stringify({");
            js.AppendLine("            success: true,");
            js.AppendLine("            message: '发送成功'");
            js.AppendLine("        });");
            js.AppendLine("");
            js.AppendLine("    } catch (e) {");
            js.AppendLine("        console.error('[私信发送] 错误:', e);");
            js.AppendLine("        return JSON.stringify({");
            js.AppendLine("            success: false,");
            js.AppendLine("            message: e.message");
            js.AppendLine("        });");
            js.AppendLine("    }");
            js.AppendLine("})();");
            
            return js.ToString();
        }

        /// <summary>
        /// 执行私信发送
        /// </summary>
        public async Task SendDirectMessage(string accountId, string fbUserId, string messageText)
        {
            if (!_browsers.TryGetValue(accountId, out var browser))
            {
                System.Diagnostics.Debug.WriteLine($"❌ 账号 {accountId} 的浏览器不存在");
                OnCollectionError?.Invoke(accountId, "浏览器不存在");
                return;
            }

            try
            {
                System.Diagnostics.Debug.WriteLine($"📨 开始发送私信: 账号={accountId}, 目标={fbUserId}");
                
                // 生成并执行JS脚本
                var script = GenerateDmSendScript(fbUserId, messageText);
                var result = await browser.EvaluateScriptAsync(script);
                
                if (result.Success && result.Result != null)
                {
                    var resultStr = result.Result.ToString();
                    System.Diagnostics.Debug.WriteLine($"✅ 私信发送结果: {resultStr}");
                    
                    // 解析结果
                    var resultObj = Newtonsoft.Json.JsonConvert.DeserializeObject<dynamic>(resultStr);
                    if (resultObj != null && resultObj.success == true)
                    {
                        System.Diagnostics.Debug.WriteLine($"✅ 私信发送成功");
                    }
                    else
                    {
                        var errorMsg = resultObj?.message?.ToString() ?? "未知错误";
                        System.Diagnostics.Debug.WriteLine($"❌ 私信发送失败: {errorMsg}");
                        OnCollectionError?.Invoke(accountId, $"私信发送失败: {errorMsg}");
                    }
                }
                else
                {
                    System.Diagnostics.Debug.WriteLine($"❌ JS执行失败");
                    OnCollectionError?.Invoke(accountId, "JS执行失败");
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"❌ 私信发送异常: {ex.Message}");
                OnCollectionError?.Invoke(accountId, $"私信发送异常: {ex.Message}");
            }
        }
    }
}
