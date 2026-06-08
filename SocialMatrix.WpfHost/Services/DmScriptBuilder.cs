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
        /// 点击 Continue 按钮（短脚本，禁止页面导航）
        /// </summary>
        public static string BuildClickContinueScript()
        {
            return @"
(function() {
    const isVisible = (el) => {
        if (!el) return false;
        const rect = el.getBoundingClientRect();
        const style = window.getComputedStyle(el);
        return rect.width > 0 && rect.height > 0 && style.display !== 'none' && style.visibility !== 'hidden';
    };
    const btn = document.querySelector('[aria-label=""Continue""], [aria-label=""继续""]');
    if (btn && isVisible(btn)) {
        btn.click();
        return JSON.stringify({ success: true, action: 'clicked' });
    }
    return JSON.stringify({ success: true, action: 'no_continue' });
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
            _js.AppendLine("            let editor = document.querySelector('div[data-lexical-editor=true]');");
            _js.AppendLine("            if (!editor || !isVisibleElement(editor)) {");
            _js.AppendLine("                editor = document.querySelector('[role=\"textbox\"][contenteditable=\"true\"]');");
            _js.AppendLine("            }");
            _js.AppendLine("            if (!editor || !isVisibleElement(editor)) throw new Error('未找到私信编辑器');");
            _js.AppendLine("            console.log('[私信发送] 找到编辑器');");
            _js.AppendLine("");
        }
        
        private void InputLexicalMessage()
        {
            _js.AppendLine("            editor.focus();");
            _js.AppendLine("            await randomDelay(300, 600);");
            _js.AppendLine("            const messageText = `" + _messageText.Replace("`", "\\`").Replace("\\", "\\\\") + "`;");
            _js.AppendLine("            document.execCommand('selectAll', false, null);");
            _js.AppendLine("            document.execCommand('delete', false, null);");
            _js.AppendLine("            await randomDelay(200, 400);");
            _js.AppendLine("            for (let i = 0; i < messageText.length; i++) {");
            _js.AppendLine("                const ch = messageText[i];");
            _js.AppendLine("                document.execCommand('insertText', false, ch);");
            _js.AppendLine("                editor.dispatchEvent(new InputEvent('input', { data: ch, bubbles: true, inputType: 'insertText' }));");
            _js.AppendLine("                await randomDelay(30, 80);");
            _js.AppendLine("            }");
            _js.AppendLine("            await randomDelay(500, 1000);");
            _js.AppendLine("            const typedText = (editor.textContent || editor.innerText || '').trim();");
            _js.AppendLine("            if (!typedText) throw new Error('消息输入失败');");
            _js.AppendLine("            console.log('[私信发送] 消息已输入, 长度:', typedText.length);");
            _js.AppendLine("");
        }
        
        private void SendMessage()
        {
            _js.AppendLine("            // Messenger 通常用 Enter 发送；也尝试点击 Send 按钮");
            _js.AppendLine("            let sendButton = null;");
            _js.AppendLine("            const sendSelectors = [");
            _js.AppendLine("                '[aria-label=\"Press Enter to send\"]',");
            _js.AppendLine("                '[aria-label=\"Send\"]',");
            _js.AppendLine("                '[aria-label=\"发送\"]'");
            _js.AppendLine("            ];");
            _js.AppendLine("            for (const sel of sendSelectors) {");
            _js.AppendLine("                const el = document.querySelector(sel);");
            _js.AppendLine("                if (el && isVisibleElement(el)) { sendButton = el; break; }");
            _js.AppendLine("            }");
            _js.AppendLine("            if (!sendButton) {");
            _js.AppendLine("                for (const btn of document.querySelectorAll('div[role=\"button\"], button')) {");
            _js.AppendLine("                    if (!isVisibleElement(btn)) continue;");
            _js.AppendLine("                    const aria = normalizeText(btn.getAttribute('aria-label'));");
            _js.AppendLine("                    if (aria === 'Send' || aria === '发送' || aria.includes('Enter to send')) {");
            _js.AppendLine("                        sendButton = btn; break;");
            _js.AppendLine("                    }");
            _js.AppendLine("                }");
            _js.AppendLine("            }");
            _js.AppendLine("            if (sendButton) {");
            _js.AppendLine("                console.log('[私信发送] 点击发送按钮');");
            _js.AppendLine("                await humanClick(sendButton);");
            _js.AppendLine("            } else {");
            _js.AppendLine("                console.log('[私信发送] 未找到发送按钮，使用 Enter 键发送');");
            _js.AppendLine("                const enterOpts = { key: 'Enter', code: 'Enter', keyCode: 13, which: 13, bubbles: true, cancelable: true };");
            _js.AppendLine("                editor.dispatchEvent(new KeyboardEvent('keydown', enterOpts));");
            _js.AppendLine("                editor.dispatchEvent(new KeyboardEvent('keypress', enterOpts));");
            _js.AppendLine("                editor.dispatchEvent(new KeyboardEvent('keyup', enterOpts));");
            _js.AppendLine("            }");
            _js.AppendLine("            await randomDelay(1000, 2000);");
            _js.AppendLine("");
        }
        
        private void VerifySendComplete()
        {
            _js.AppendLine("            await new Promise((resolve, reject) => {");
            _js.AppendLine("                const timeout = setTimeout(() => reject(new Error('发送超时')), 15000);");
            _js.AppendLine("                let emptyCount = 0;");
            _js.AppendLine("                const checkInterval = setInterval(() => {");
            _js.AppendLine("                    const text = (editor.textContent || editor.innerText || '').trim();");
            _js.AppendLine("                    if (!text) {");
            _js.AppendLine("                        emptyCount++;");
            _js.AppendLine("                        if (emptyCount >= 3) {");
            _js.AppendLine("                            clearTimeout(timeout);");
            _js.AppendLine("                            clearInterval(checkInterval);");
            _js.AppendLine("                            console.log('[私信发送] 发送完成');");
            _js.AppendLine("                            resolve();");
            _js.AppendLine("                        }");
            _js.AppendLine("                    }");
            _js.AppendLine("                }, 500);");
            _js.AppendLine("            });");
            _js.AppendLine("");
        }
    }
}
