using System;
using System.Collections.Generic;
using System.Text;
using CefSharp;
using CefSharp.Wpf;

namespace SocialMatrix.WpfHost.Services
{
    public abstract class FacebookScriptBuilder
    {
        protected readonly StringBuilder _js = new();
        
        protected void BeginScript()
        {
            _js.AppendLine("new Promise(function(resolve, reject) {");
            _js.AppendLine("    (async function() {");
            _js.AppendLine("        try {");
            _js.AppendLine("            console.log('[Facebook自动化] 开始执行');");
            _js.AppendLine("");
            
            AddHumanBehaviorHelpers();
        }
        
        protected string EndScript()
        {
            _js.AppendLine("                var result = JSON.stringify({ success: true, message: '执行成功' });");
            _js.AppendLine("                console.log('[Facebook自动化] 执行完成:', result);");
            _js.AppendLine("                resolve(result);");
            _js.AppendLine("");
            _js.AppendLine("            } catch (e) {");
            _js.AppendLine("                console.error('[Facebook自动化] 错误:', e);");
            _js.AppendLine("                var errorResult = JSON.stringify({ success: false, message: e.message });");
            _js.AppendLine("                reject(errorResult);");
            _js.AppendLine("            }");
            _js.AppendLine("        })();");
            _js.AppendLine("    });");
            
            return _js.ToString();
        }
        
        protected void AddHumanBehaviorHelpers()
        {
            _js.AppendLine("            // ===== 人类行为模拟辅助函数 =====");
            _js.AppendLine("            const randomDelay = (min, max) => {");
            _js.AppendLine("                const u1 = Math.random();");
            _js.AppendLine("                const u2 = Math.random();");
            _js.AppendLine("                const z = Math.sqrt(-2.0 * Math.log(u1)) * Math.cos(2.0 * Math.PI * u2);");
            _js.AppendLine("                const mean = (min + max) / 2;");
            _js.AppendLine("                const stdDev = (max - min) / 6;");
            _js.AppendLine("                const delay = Math.max(min, Math.min(max, mean + z * stdDev));");
            _js.AppendLine("                return new Promise(resolve => setTimeout(resolve, Math.floor(delay)));");
            _js.AppendLine("            };");
            _js.AppendLine("");
            
            _js.AppendLine("            const simulateMouseMovement = async (targetElement) => {");
            _js.AppendLine("                try {");
            _js.AppendLine("                    if (!targetElement) return;");
            _js.AppendLine("                    const rect = targetElement.getBoundingClientRect();");
            _js.AppendLine("                    const targetX = rect.left + rect.width / 2;");
            _js.AppendLine("                    const targetY = rect.top + rect.height / 2;");
            _js.AppendLine("                    const startX = Math.random() * window.innerWidth;");
            _js.AppendLine("                    const startY = Math.random() * window.innerHeight;");
            _js.AppendLine("                    const controlX = (startX + targetX) / 2 + (Math.random() - 0.5) * 200;");
            _js.AppendLine("                    const controlY = (startY + targetY) / 2 + (Math.random() - 0.5) * 200;");
            _js.AppendLine("                    const steps = 20;");
            _js.AppendLine("                    for (let i = 0; i <= steps; i++) {");
            _js.AppendLine("                        const t = i / steps;");
            _js.AppendLine("                        const x = Math.pow(1-t, 2) * startX + 2 * (1-t) * t * controlX + Math.pow(t, 2) * targetX;");
            _js.AppendLine("                        const y = Math.pow(1-t, 2) * startY + 2 * (1-t) * t * controlY + Math.pow(t, 2) * targetY;");
            _js.AppendLine("                        const jitterX = x + (Math.random() - 0.5) * 4;");
            _js.AppendLine("                        const jitterY = y + (Math.random() - 0.5) * 4;");
            _js.AppendLine("                        const event = new MouseEvent('mousemove', {");
            _js.AppendLine("                            view: window, bubbles: true, cancelable: true,");
            _js.AppendLine("                            clientX: jitterX, clientY: jitterY");
            _js.AppendLine("                        });");
            _js.AppendLine("                        document.dispatchEvent(event);");
            _js.AppendLine("                        await randomDelay(30, 80);");
            _js.AppendLine("                    }");
            _js.AppendLine("                } catch (e) { console.warn('[人类行为] 鼠标轨迹失败:', e); }");
            _js.AppendLine("            };");
            _js.AppendLine("");
            
            _js.AppendLine("            const humanClick = async (element) => {");
            _js.AppendLine("                try {");
            _js.AppendLine("                    if (!element) return false;");
            _js.AppendLine("                    element.scrollIntoView({ block: 'center', inline: 'center' });");
            _js.AppendLine("                    await randomDelay(100, 300);");
            _js.AppendLine("                    await simulateMouseMovement(element);");
            _js.AppendLine("                    await randomDelay(100, 300);");
            _js.AppendLine("                    const rect = element.getBoundingClientRect();");
            _js.AppendLine("                    const clientX = rect.left + rect.width / 2;");
            _js.AppendLine("                    const clientY = rect.top + rect.height / 2;");
            _js.AppendLine("                    const eventOptions = { view: window, bubbles: true, cancelable: true, clientX, clientY };");
            _js.AppendLine("                    element.dispatchEvent(new MouseEvent('mouseover', eventOptions));");
            _js.AppendLine("                    element.dispatchEvent(new MouseEvent('mousemove', eventOptions));");
            _js.AppendLine("                    element.dispatchEvent(new MouseEvent('mousedown', eventOptions));");
            _js.AppendLine("                    await randomDelay(80, 180);");
            _js.AppendLine("                    element.dispatchEvent(new MouseEvent('mouseup', eventOptions));");
            _js.AppendLine("                    element.dispatchEvent(new MouseEvent('click', eventOptions));");
            _js.AppendLine("                    if (typeof element.click === 'function') element.click();");
            _js.AppendLine("                    return true;");
            _js.AppendLine("                } catch (e) { console.warn('[人类行为] 点击失败:', e); return false; }");
            _js.AppendLine("            };");
            _js.AppendLine("");
            
            _js.AppendLine("            const humanTypeText = async (element, text) => {");
            _js.AppendLine("                try {");
            _js.AppendLine("                    if (!element || !text) return false;");
            _js.AppendLine("                    element.focus();");
            _js.AppendLine("                    await randomDelay(200, 500);");
            _js.AppendLine("                    for (let i = 0; i < text.length; i++) {");
            _js.AppendLine("                        const char = text[i];");
            _js.AppendLine("                        const typeDelay = 50 + Math.random() * 150 + (Math.random() > 0.9 ? 200 + Math.random() * 300 : 0);");
            _js.AppendLine("                        element.value += char;");
            _js.AppendLine("                        element.dispatchEvent(new InputEvent('input', { data: char, bubbles: true }));");
            _js.AppendLine("                        await randomDelay(typeDelay, typeDelay + 50);");
            _js.AppendLine("                    }");
            _js.AppendLine("                    return true;");
            _js.AppendLine("                } catch (e) { console.warn('[人类行为] 打字失败:', e); return false; }");
            _js.AppendLine("            };");
            _js.AppendLine("");
        }
        
        protected void AddDmHelpers()
        {
            _js.AppendLine("            // ===== 私信专用辅助函数 =====");
            _js.AppendLine("            const normalizeText = (text) => (text || '').replace(/\\s+/g, ' ').trim();");
            _js.AppendLine("            const isVisibleElement = (el) => {");
            _js.AppendLine("                if (!el) return false;");
            _js.AppendLine("                const rect = el.getBoundingClientRect();");
            _js.AppendLine("                const style = window.getComputedStyle(el);");
            _js.AppendLine("                return rect.width > 0 && rect.height > 0 && style.display !== 'none' && style.visibility !== 'hidden';");
            _js.AppendLine("            };");
            _js.AppendLine("            const findContinueButton = () => {");
            _js.AppendLine("                const exact = document.querySelector('[aria-label=\"Continue\"], [aria-label=\"继续\"]');");
            _js.AppendLine("                if (exact && isVisibleElement(exact)) return exact;");
            _js.AppendLine("                const selectors = [");
            _js.AppendLine("                    '[aria-label*=\"Continue\"]',");
            _js.AppendLine("                    'div[role=\"button\"]',");
            _js.AppendLine("                    'button',");
            _js.AppendLine("                    'span[role=\"button\"]',");
            _js.AppendLine("                    'a[role=\"button\"]',");
            _js.AppendLine("                    '[aria-label*=\"continue\"]',");
            _js.AppendLine("                    '[aria-label*=\"继续\"]',");
            _js.AppendLine("                    'input[type=\"button\"]',");
            _js.AppendLine("                    'input[type=\"submit\"]'");
            _js.AppendLine("                ];");
            _js.AppendLine("                for (const selector of selectors) {");
            _js.AppendLine("                    const elements = document.querySelectorAll(selector);");
            _js.AppendLine("                    console.log('[私信发送] 选择器', selector, '找到', elements.length, '个元素');");
            _js.AppendLine("                    for (const el of elements) {");
            _js.AppendLine("                        if (!isVisibleElement(el)) continue;");
            _js.AppendLine("                        const ariaLabel = normalizeText(el.getAttribute('aria-label'));");
            _js.AppendLine("                        const text = normalizeText(el.innerText || el.textContent);");
            _js.AppendLine("                        const title = normalizeText(el.getAttribute('title'));");
            _js.AppendLine("                        console.log('[私信发送] 检查元素 - ariaLabel:', ariaLabel, ', text:', text, ', title:', title);");
            _js.AppendLine("                        if (ariaLabel.includes('Continue') || ariaLabel.includes('continue') || ariaLabel.includes('继续') ||");
            _js.AppendLine("                            text.includes('Continue') || text.includes('continue') || text.includes('继续') ||");
            _js.AppendLine("                            title.includes('Continue') || title.includes('continue') || title.includes('继续')) {");
            _js.AppendLine("                            console.log('[私信发送] ✅ 找到 Continue 按钮');");
            _js.AppendLine("                            return el;");
            _js.AppendLine("                        }");
            _js.AppendLine("                    }");
            _js.AppendLine("                }");
            _js.AppendLine("                console.log('[私信发送] ❌ 未找到 Continue 按钮');");
            _js.AppendLine("                return null;");
            _js.AppendLine("            };");
            _js.AppendLine("");
        }
        
        protected void WaitForElement(string selector, int timeoutMs = 15000)
        {
            _js.AppendLine("            // 等待元素出现");
            _js.AppendLine("            await new Promise((resolve, reject) => {");
            _js.AppendLine("                const timeout = setTimeout(() => reject(new Error('等待元素超时: " + selector + "')), " + timeoutMs + ");");
            _js.AppendLine("                const checkInterval = setInterval(() => {");
            _js.AppendLine("                    const el = document.querySelector('" + selector + "');");
            _js.AppendLine("                    if (el && isVisibleElement(el)) {");
            _js.AppendLine("                        clearTimeout(timeout);");
            _js.AppendLine("                        clearInterval(checkInterval);");
            _js.AppendLine("                        resolve(el);");
            _js.AppendLine("                    }");
            _js.AppendLine("                }, 500);");
            _js.AppendLine("            });");
        }
        
        protected void WaitForDialogClose(int timeoutMs = 30000)
        {
            _js.AppendLine("            // 等待对话框关闭");
            _js.AppendLine("            await new Promise((resolve) => {");
            _js.AppendLine("                const timeout = setTimeout(() => resolve(), " + timeoutMs + ");");
            _js.AppendLine("                const checkInterval = setInterval(() => {");
            _js.AppendLine("                    const dialog = document.querySelector('[role=\"dialog\"], .modal, .fb_dialog');");
            _js.AppendLine("                    if (!dialog || !isVisibleElement(dialog)) {");
            _js.AppendLine("                        clearTimeout(timeout);");
            _js.AppendLine("                        clearInterval(checkInterval);");
            _js.AppendLine("                        resolve();");
            _js.AppendLine("                    }");
            _js.AppendLine("                }, 500);");
            _js.AppendLine("            });");
        }
        
        protected string EscapeForJsTemplate(string text)
        {
            if (text == null) return "";
            return text.Replace("`", "\\`").Replace("\\", "\\\\").Replace("'", "\\'").Replace("\"", "\\\"");
        }
        
        public abstract string Build();
    }
}
