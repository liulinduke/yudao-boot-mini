using Newtonsoft.Json;
using SocialMatrix.WpfHost.Services;

namespace SocialMatrix.WpfHost.Services
{
    public class DmScriptBuilder : FacebookScriptBuilder
    {
        private readonly string _messageText;
        
        public DmScriptBuilder(string fbUserId, string messageText)
        {
            _messageText = messageText;
        }
        
        /// <summary>
        /// 点击 Continue / Get started 按钮（短脚本，禁止页面导航）
        /// </summary>
        public static string BuildClickContinueScript()
        {
            return @"
(async function() {
    const normalize = (text) => (text || '').replace(/\s+/g, ' ').trim().toLowerCase();
    const sleep = (ms) => new Promise(resolve => setTimeout(resolve, ms));
    const isVisible = (el) => {
        if (!el) return false;
        const rect = el.getBoundingClientRect();
        const style = window.getComputedStyle(el);
        return rect.width > 0 && rect.height > 0 && style.display !== 'none' && style.visibility !== 'hidden';
    };
    const robustClick = async (el) => {
        el.scrollIntoView({ block: 'center', inline: 'center' });
        await sleep(120);
        const rect = el.getBoundingClientRect();
        const x = rect.left + rect.width / 2;
        const y = rect.top + rect.height / 2;
        for (const type of ['pointerdown', 'mousedown', 'pointerup', 'mouseup', 'click']) {
            const event = type.startsWith('pointer')
                ? new PointerEvent(type, { bubbles: true, cancelable: true, clientX: x, clientY: y, pointerId: 1, pointerType: 'mouse', isPrimary: true })
                : new MouseEvent(type, { bubbles: true, cancelable: true, clientX: x, clientY: y, button: 0 });
            el.dispatchEvent(event);
        }
        if (typeof el.click === 'function') el.click();
    };
    const labels = [
        'continue',
        'get started',
        'get start',
        'start',
        '开始使用',
        '开始',
        '继续'
    ];
    const candidates = Array.from(document.querySelectorAll(
        '[aria-label], div[role=""button""], button, span[role=""button""], a[role=""button""], input[type=""button""], input[type=""submit""]'
    ));
    for (const el of candidates) {
        if (!isVisible(el)) continue;
        const text = normalize([
            el.getAttribute('aria-label'),
            el.getAttribute('title'),
            el.getAttribute('value'),
            el.innerText || el.textContent
        ].filter(Boolean).join(' '));
        if (labels.some(label => text === label || text.includes(label))) {
            await robustClick(el);
            return JSON.stringify({ success: true, action: 'clicked', text });
        }
    }
    return JSON.stringify({ success: true, action: 'no_start_button' });
})();";
        }
        
        /// <summary>
        /// 检测私信编辑器是否就绪
        /// </summary>
        public static string BuildEditorReadyCheckScript()
        {
            return @"
(function() {
    const isVisible = (el) => {
        if (!el) return false;
        const rect = el.getBoundingClientRect();
        const style = window.getComputedStyle(el);
        return rect.width > 0 && rect.height > 0 && style.display !== 'none' && style.visibility !== 'hidden';
    };
    const editor = document.querySelector('div[data-lexical-editor=""true""], [role=""textbox""][contenteditable=""true""]');
    return isVisible(editor);
})();";
        }
        
        /// <summary>
        /// 输入消息并发送（编辑器已就绪后执行）
        /// </summary>
        public override string Build()
        {
            BeginScript();
            AddDmHelpers();
            FindEditor();
            InputLexicalMessage();
            SendMessage();
            VerifySendComplete();
            return EndScript();
        }
        
        private void FindEditor()
        {
            _js.AppendLine("            let editor = document.querySelector('div[data-lexical-editor=\"true\"]');");
            _js.AppendLine("            if (!editor || !isVisibleElement(editor)) {");
            _js.AppendLine("                editor = document.querySelector('[role=\"textbox\"][contenteditable=\"true\"]');");
            _js.AppendLine("            }");
            _js.AppendLine("            if (!editor || !isVisibleElement(editor)) throw new Error('未找到私信编辑器');");
            _js.AppendLine("            console.log('[私信发送] 找到编辑器');");
            _js.AppendLine("");
        }
        
        private void InputLexicalMessage()
        {
            var messageJson = JsonConvert.SerializeObject(_messageText);
            _js.AppendLine("            editor.focus();");
            _js.AppendLine("            await randomDelay(300, 600);");
            _js.AppendLine($"            const messageText = {messageJson};");
            _js.AppendLine("            document.execCommand('selectAll', false, null);");
            _js.AppendLine("            document.execCommand('delete', false, null);");
            _js.AppendLine("            await randomDelay(200, 400);");
            _js.AppendLine("            // 不使用整段 paste/insertText，逐字输入并加入不规则停顿。");
            _js.AppendLine("            for (const ch of messageText) {");
            _js.AppendLine("                document.execCommand('insertText', false, ch);");
            _js.AppendLine("                editor.dispatchEvent(new InputEvent('input', { data: ch, bubbles: true, inputType: 'insertText' }));");
            _js.AppendLine("                const pause = 45 + Math.random() * 125 + (Math.random() > 0.92 ? 250 + Math.random() * 500 : 0);");
            _js.AppendLine("                await randomDelay(pause, pause + 45);");
            _js.AppendLine("            }");
            _js.AppendLine("            await randomDelay(500, 1000);");
            _js.AppendLine("            const typedText = (editor.textContent || editor.innerText || '').trim();");
            _js.AppendLine("            if (!typedText) throw new Error('消息输入失败');");
            _js.AppendLine("            console.log('[私信发送] 消息已输入, 长度:', typedText.length);");
            _js.AppendLine("");
        }
        
        private void SendMessage()
        {
            _js.AppendLine("            // 当前 Messenger 文本消息通常没有独立 Send 按钮，回车是主发送动作。");
            _js.AppendLine("            let sendButton = null;");
            _js.AppendLine("            const sendSelectors = [");
            _js.AppendLine("                '[aria-label=\"Press Enter to send\"]',");
            _js.AppendLine("                '[aria-label=\"Send\"]',");
            _js.AppendLine("                '[aria-label=\"发送\"]',");
            _js.AppendLine("                '[data-testid*=\"send\" i]'");
            _js.AppendLine("            ];");
            _js.AppendLine("            for (const sel of sendSelectors) {");
            _js.AppendLine("                const el = document.querySelector(sel);");
            _js.AppendLine("                if (el && isVisibleElement(el)) { sendButton = el; break; }");
            _js.AppendLine("            }");
            _js.AppendLine("            if (!sendButton) {");
            _js.AppendLine("                for (const btn of document.querySelectorAll('div[role=\"button\"], button')) {");
            _js.AppendLine("                    if (!isVisibleElement(btn)) continue;");
            _js.AppendLine("                    const aria = normalizeText(btn.getAttribute('aria-label'));");
            _js.AppendLine("                    if ((aria === 'send' || aria === '发送' || aria.includes('enter to send')) && !aria.includes('like') && !aria.includes('voice')) {");
            _js.AppendLine("                        sendButton = btn; break;");
            _js.AppendLine("                    }");
            _js.AppendLine("                }");
            _js.AppendLine("            }");
            _js.AppendLine("            if (sendButton) {");
            _js.AppendLine("                console.log('[私信发送] 点击发送按钮');");
            _js.AppendLine("                await humanClick(sendButton);");
            _js.AppendLine("            }");
            _js.AppendLine("            if (!sendButton) {");
            _js.AppendLine("                console.log('[私信发送] 未找到文本发送按钮，使用 Enter 键发送');");
            _js.AppendLine("                editor.focus();");
            _js.AppendLine("                const enterOpts = { key: 'Enter', code: 'Enter', keyCode: 13, which: 13, bubbles: true, cancelable: true };");
            _js.AppendLine("                for (const type of ['keydown', 'keypress', 'keyup']) editor.dispatchEvent(new KeyboardEvent(type, enterOpts));");
            _js.AppendLine("            }");
            _js.AppendLine("            await randomDelay(1000, 2000);");
            _js.AppendLine("");
        }
        
        private void VerifySendComplete()
        {
            _js.AppendLine("            // 发送后编辑器清空即可确认；某些 E2EE 页面不会立即清空，因此只做软校验。");
            _js.AppendLine("            await randomDelay(500, 900);");
            _js.AppendLine("            const remainingText = (editor.textContent || editor.innerText || '').trim();");
            _js.AppendLine("            if (remainingText && remainingText === messageText) console.warn('[私信发送] 编辑器仍有文本，已完成发送动作但页面尚未刷新');");
            _js.AppendLine("            console.log('[私信发送] 发送动作完成');");
            _js.AppendLine("");
        }
    }
}
