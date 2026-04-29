using System;
using System.Collections.Generic;
using System.Text;
using CefSharp;
using CefSharp.Wpf;

namespace SocialMatrix.WpfHost.Services
{
    /// <summary>
    /// Facebook自动化脚本生成器基类
    /// 提供通用的人类行为模拟函数和工具方法
    /// </summary>
    public abstract class FacebookScriptBuilder
    {
        protected readonly StringBuilder _js = new();
        
        /// <summary>
        /// 开始构建脚本
        /// </summary>
        protected void BeginScript()
        {
            _js.AppendLine("(async function() {");
            _js.AppendLine("    try {");
            _js.AppendLine("        console.log('[Facebook自动化] 开始执行');");
            _js.AppendLine("");
            
            // 注入人类行为模拟辅助函数
            AddHumanBehaviorHelpers();
        }
        
        /// <summary>
        /// 结束脚本构建
        /// </summary>
        protected string EndScript()
        {
            _js.AppendLine("        return JSON.stringify({ success: true, message: '执行成功' });");
            _js.AppendLine("");
            _js.AppendLine("    } catch (e) {");
            _js.AppendLine("        console.error('[Facebook自动化] 错误:', e);");
            _js.AppendLine("        return JSON.stringify({ success: false, message: e.message });");
            _js.AppendLine("    }");
            _js.AppendLine("})();");
            
            return _js.ToString();
        }
        
        /// <summary>
        /// 添加人类行为模拟辅助函数（所有脚本共用）
        /// </summary>
        protected void AddHumanBehaviorHelpers()
        {
            // 随机延迟（使用正态分布，更接近人类行为）
            _js.AppendLine("        // ===== 人类行为模拟辅助函数 =====");
            _js.AppendLine("        const randomDelay = (min, max) => {");
            _js.AppendLine("            const u1 = Math.random();");
            _js.AppendLine("            const u2 = Math.random();");
            _js.AppendLine("            const z = Math.sqrt(-2.0 * Math.log(u1)) * Math.cos(2.0 * Math.PI * u2);");
            _js.AppendLine("            const mean = (min + max) / 2;");
            _js.AppendLine("            const stdDev = (max - min) / 6; // 99.7% 的值在范围内");
            _js.AppendLine("            const delay = Math.max(min, Math.min(max, mean + z * stdDev));");
            _js.AppendLine("            return new Promise(resolve => setTimeout(resolve, Math.floor(delay)));");
            _js.AppendLine("        };");
            _js.AppendLine("");
            
            // 贝塞尔曲线鼠标轨迹模拟
            _js.AppendLine("        const simulateMouseMovement = async (targetElement) => {");
            _js.AppendLine("            try {");
            _js.AppendLine("                if (!targetElement) return;");
            _js.AppendLine("                ");
            _js.AppendLine("                const rect = targetElement.getBoundingClientRect();");
            _js.AppendLine("                const targetX = rect.left + rect.width / 2;");
            _js.AppendLine("                const targetY = rect.top + rect.height / 2;");
            _js.AppendLine("                ");
            _js.AppendLine("                const startX = Math.random() * window.innerWidth;");
            _js.AppendLine("                const startY = Math.random() * window.innerHeight;");
            _js.AppendLine("                const controlX = (startX + targetX) / 2 + (Math.random() - 0.5) * 200;");
            _js.AppendLine("                const controlY = (startY + targetY) / 2 + (Math.random() - 0.5) * 200;");
            _js.AppendLine("                ");
            _js.AppendLine("                const steps = 20;");
            _js.AppendLine("                for (let i = 0; i <= steps; i++) {");
            _js.AppendLine("                    const t = i / steps;");
            _js.AppendLine("                    const x = Math.pow(1-t, 2) * startX + 2 * (1-t) * t * controlX + Math.pow(t, 2) * targetX;");
            _js.AppendLine("                    const y = Math.pow(1-t, 2) * startY + 2 * (1-t) * t * controlY + Math.pow(t, 2) * targetY;");
            _js.AppendLine("                    ");
            _js.AppendLine("                    const jitterX = x + (Math.random() - 0.5) * 4;");
            _js.AppendLine("                    const jitterY = y + (Math.random() - 0.5) * 4;");
            _js.AppendLine("                    ");
            _js.AppendLine("                    const event = new MouseEvent('mousemove', {");
            _js.AppendLine("                        view: window, bubbles: true, cancelable: true,");
            _js.AppendLine("                        clientX: jitterX, clientY: jitterY");
            _js.AppendLine("                    });");
            _js.AppendLine("                    document.dispatchEvent(event);");
            _js.AppendLine("                    await randomDelay(30, 80);");
            _js.AppendLine("                }");
            _js.AppendLine("            } catch (e) { console.warn('[人类行为] 鼠标轨迹失败:', e); }");
            _js.AppendLine("        };");
            _js.AppendLine("");
            
            // 模拟人类点击（包含鼠标移动 + 停留 + 点击）
            _js.AppendLine("        const humanClick = async (element) => {");
            _js.AppendLine("            try {");
            _js.AppendLine("                if (!element) return false;");
            _js.AppendLine("                await simulateMouseMovement(element);");
            _js.AppendLine("                await randomDelay(100, 300);");
            _js.AppendLine("                element.click();");
            _js.AppendLine("                return true;");
            _js.AppendLine("            } catch (e) { console.warn('[人类行为] 点击失败:', e); return false; }");
            _js.AppendLine("        };");
            _js.AppendLine("");
            
            // 模拟人类打字（速度变化 + 偶尔停顿）
            _js.AppendLine("        const humanTypeText = async (element, text) => {");
            _js.AppendLine("            try {");
            _js.AppendLine("                if (!element || !text) return false;");
            _js.AppendLine("                element.focus();");
            _js.AppendLine("                await randomDelay(200, 500);");
            _js.AppendLine("                ");
            _js.AppendLine("                for (let i = 0; i < text.length; i++) {");
            _js.AppendLine("                    document.execCommand('insertText', false, text[i]);");
            _js.AppendLine("                    let delay = randomDelay(80, 200);");
            _js.AppendLine("                    ");
            _js.AppendLine("                    if (Math.random() < 0.1) await randomDelay(500, 1500);");
            _js.AppendLine("                    if (['.', ',', '!', '?', '。', '，', '！', '？'].includes(text[i])) {");
            _js.AppendLine("                        await randomDelay(300, 800);");
            _js.AppendLine("                    }");
            _js.AppendLine("                    await delay;");
            _js.AppendLine("                }");
            _js.AppendLine("                return true;");
            _js.AppendLine("            } catch (e) { console.warn('[人类行为] 打字失败:', e); return false; }");
            _js.AppendLine("        };");
            _js.AppendLine("");
        }
        
        /// <summary>
        /// 安全转义字符串用于JavaScript模板
        /// </summary>
        protected string EscapeForJsTemplate(string text)
        {
            return text.Replace("`", "\\`").Replace("${", "\\${");
        }
        
        /// <summary>
        /// 等待元素出现的通用脚本
        /// </summary>
        protected void WaitForElement(string selector, int timeoutMs = 10000)
        {
            _js.AppendLine($"        await new Promise((resolve, reject) => {{");
            _js.AppendLine($"            const timeout = setTimeout(() => reject(new Error('等待元素超时: {selector}')), {timeoutMs});");
            _js.AppendLine($"            const checkInterval = setInterval(() => {{");
            _js.AppendLine($"                if (document.querySelector('{selector}')) {{");
            _js.AppendLine($"                    clearTimeout(timeout);");
            _js.AppendLine($"                    clearInterval(checkInterval);");
            _js.AppendLine($"                    resolve();");
            _js.AppendLine($"                }}");
            _js.AppendLine($"            }}, 500);");
            _js.AppendLine($"        }});");
            _js.AppendLine("");
        }
        
        /// <summary>
        /// 等待对话框关闭的通用脚本
        /// </summary>
        protected void WaitForDialogClose(int timeoutMs = 30000)
        {
            _js.AppendLine($"        await new Promise((resolve, reject) => {{");
            _js.AppendLine($"            const timeout = setTimeout(() => reject(new Error('等待超时')), {timeoutMs});");
            _js.AppendLine($"            const checkInterval = setInterval(() => {{");
            _js.AppendLine($"                const dialog = document.querySelector('div[role=dialog] form');");
            _js.AppendLine($"                if (!dialog) {{");
            _js.AppendLine($"                    clearTimeout(timeout);");
            _js.AppendLine($"                    clearInterval(checkInterval);");
            _js.AppendLine($"                    resolve();");
            _js.AppendLine($"                }}");
            _js.AppendLine($"            }}, 500);");
            _js.AppendLine($"        }});");
            _js.AppendLine("");
        }
    }
}
