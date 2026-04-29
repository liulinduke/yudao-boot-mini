using SocialMatrix.WpfHost.Services;

namespace SocialMatrix.WpfHost.Services
{
    /// <summary>
    /// 私信发送脚本生成器
    /// </summary>
    public class DmScriptBuilder : FacebookScriptBuilder
    {
        private readonly string _fbUserId;
        private readonly string _messageText;
        
        public DmScriptBuilder(string fbUserId, string messageText)
        {
            _fbUserId = fbUserId;
            _messageText = messageText;
        }
        
        /// <summary>
        /// 生成完整的私信发送脚本
        /// </summary>
        public string Build()
        {
            BeginScript();
            
            // 1. 导航到私信页面
            NavigateToDmPage();
            
            // 2. 多重状态检测
            DetectPageState();
            
            // 3. 根据页面状态处理
            HandlePageStates();
            
            // 4. 尝试通过历史PIN验证
            TryPassHistoryPIN();
            
            // 5. 插入文本
            InputMessage();
            
            // 6. 点击发送按钮
            ClickSendButton();
            
            // 7. 等待发送完成
            WaitForSendComplete();
            
            return EndScript();
        }
        
        private void NavigateToDmPage()
        {
            _js.AppendLine($"        const targetUrl = 'https://www.facebook.com/messages/t/{_fbUserId}/';");
            _js.AppendLine("        if (window.location.href !== targetUrl) {");
            _js.AppendLine("            console.log('[私信发送] 导航到:', targetUrl);");
            _js.AppendLine("            window.location.href = targetUrl;");
            _js.AppendLine("            await randomDelay(2000, 3000);");
            _js.AppendLine("        }");
            _js.AppendLine("");
        }
        
        private void DetectPageState()
        {
            _js.AppendLine("        // 检测页面状态（处理各种异常情况）");
            _js.AppendLine("        const pageState = await new Promise((resolve, reject) => {");
            _js.AppendLine("            const timeout = setTimeout(() => reject(new Error('页面状态检测超时')), 10000);");
            _js.AppendLine("            ");
            _js.AppendLine("            const checkInterval = setInterval(async () => {");
            _js.AppendLine("                try {");
            _js.AppendLine("                    // 状态1: Continue/继续按钮（历史PIN验证）");
            _js.AppendLine("                    const continueBtn = Array.from(document.querySelectorAll('div[role=navigation]+div:not([aria-hidden=true]) div[role=main] div[role=button]'))");
            _js.AppendLine("                        .find(x => x.innerText === 'Continue' || x.innerText === '继续');");
            _js.AppendLine("                    if (continueBtn) {");
            _js.AppendLine("                        clearTimeout(timeout);");
            _js.AppendLine("                        clearInterval(checkInterval);");
            _js.AppendLine("                        resolve({ state: 'continue', element: continueBtn });");
            _js.AppendLine("                        return;");
            _js.AppendLine("                    }");
            _js.AppendLine("                    ");
            _js.AppendLine("                    // 状态2: 用户不存在");
            _js.AppendLine("                    if (document.querySelector('div[role=navigation]+div>div>div>div>div>div>i')) {");
            _js.AppendLine("                        clearTimeout(timeout);");
            _js.AppendLine("                        clearInterval(checkInterval);");
            _js.AppendLine("                        resolve({ state: 'user_not_found' });");
            _js.AppendLine("                        return;");
            _js.AppendLine("                    }");
            _js.AppendLine("                    ");
            _js.AppendLine("                    // 状态3: 无法发送消息图标");
            _js.AppendLine("                    if (document.querySelector('div[role=main]>div>div>div>svg')) {");
            _js.AppendLine("                        clearTimeout(timeout);");
            _js.AppendLine("                        clearInterval(checkInterval);");
            _js.AppendLine("                        resolve({ state: 'cannot_send' });");
            _js.AppendLine("                        return;");
            _js.AppendLine("                    }");
            _js.AppendLine("                    ");
            _js.AppendLine("                    // 状态4: 错误提示框");
            _js.AppendLine("                    if (document.querySelector('div[role=navigation]+div div[role=alert] span')) {");
            _js.AppendLine("                        clearTimeout(timeout);");
            _js.AppendLine("                        clearInterval(checkInterval);");
            _js.AppendLine("                        resolve({ state: 'error_alert' });");
            _js.AppendLine("                        return;");
            _js.AppendLine("                    }");
            _js.AppendLine("                    ");
            _js.AppendLine("                    // 状态5: 无法给对方发送消息");
            _js.AppendLine("                    const cannotSendSpan = document.querySelector('div[role=main] div[role=none][tabindex=\"-1\"] > div > div:nth-child(2) > span');");
            _js.AppendLine("                    if (cannotSendSpan) {");
            _js.AppendLine("                        clearTimeout(timeout);");
            _js.AppendLine("                        clearInterval(checkInterval);");
            _js.AppendLine("                        resolve({ state: 'cannot_send_to_user' });");
            _js.AppendLine("                        return;");
            _js.AppendLine("                    }");
            _js.AppendLine("                    ");
            _js.AppendLine("                    // 状态6: 发送上限提示");
            _js.AppendLine("                    const limitText = Array.from(document.querySelectorAll('div > div:nth-child(2) span>span'))");
            _js.AppendLine("                        .find(x => x.innerText.includes('limit') || x.innerText.includes('上限'));");
            _js.AppendLine("                    if (limitText) {");
            _js.AppendLine("                        clearTimeout(timeout);");
            _js.AppendLine("                        clearInterval(checkInterval);");
            _js.AppendLine("                        resolve({ state: 'rate_limit' });");
            _js.AppendLine("                        return;");
            _js.AppendLine("                    }");
            _js.AppendLine("                    ");
            _js.AppendLine("                    // 状态7: 编辑器出现（可以发送）");
            _js.AppendLine("                    const editor = document.querySelector('div[data-lexical-editor=true]');");
            _js.AppendLine("                    if (editor) {");
            _js.AppendLine("                        clearTimeout(timeout);");
            _js.AppendLine("                        clearInterval(checkInterval);");
            _js.AppendLine("                        resolve({ state: 'ready', element: editor });");
            _js.AppendLine("                        return;");
            _js.AppendLine("                    }");
            _js.AppendLine("                } catch (e) {");
            _js.AppendLine("                    console.warn('[私信] 状态检测异常:', e);");
            _js.AppendLine("                }");
            _js.AppendLine("            }, 500);");
            _js.AppendLine("        });");
            _js.AppendLine("        console.log('[私信发送] 页面状态:', pageState.state);");
            _js.AppendLine("");
        }
        
        private void HandlePageStates()
        {
            _js.AppendLine("        // 处理不同的页面状态");
            _js.AppendLine("        if (pageState.state === 'continue') {");
            _js.AppendLine("            pageState.element.click();");
            _js.AppendLine("            await randomDelay(1000, 2000);");
            _js.AppendLine("            await new Promise((resolve, reject) => {");
            _js.AppendLine("                const timeout = setTimeout(() => reject(new Error('等待编辑器超时')), 10000);");
            _js.AppendLine("                const checkInterval = setInterval(() => {");
            _js.AppendLine("                    if (document.querySelector('div[data-lexical-editor=true]')) {");
            _js.AppendLine("                        clearTimeout(timeout);");
            _js.AppendLine("                        clearInterval(checkInterval);");
            _js.AppendLine("                        resolve();");
            _js.AppendLine("                    }");
            _js.AppendLine("                }, 500);");
            _js.AppendLine("            });");
            _js.AppendLine("        } else if (pageState.state === 'user_not_found') {");
            _js.AppendLine("            throw new Error('未找到用户');");
            _js.AppendLine("        } else if (pageState.state === 'cannot_send') {");
            _js.AppendLine("            throw new Error('无法发送消息');");
            _js.AppendLine("        } else if (pageState.state === 'error_alert') {");
            _js.AppendLine("            throw new Error('页面错误');");
            _js.AppendLine("        } else if (pageState.state === 'cannot_send_to_user') {");
            _js.AppendLine("            throw new Error('无法给对方发送消息');");
            _js.AppendLine("        } else if (pageState.state === 'rate_limit') {");
            _js.AppendLine("            throw new Error('24小时内陌生人发送已达上限');");
            _js.AppendLine("        }");
            _js.AppendLine("");
        }
        
        private void TryPassHistoryPIN()
        {
            _js.AppendLine("        // 尝试通过历史PIN验证");
            _js.AppendLine("        const pinDialog = document.querySelector('div[role=dialog] div[role=button][aria-label*=\"pin\"]');");
            _js.AppendLine("        if (pinDialog) {");
            _js.AppendLine("            const lightModeDialog = document.querySelector('div.__fb-light-mode:has(div[role=dialog])');");
            _js.AppendLine("            if (lightModeDialog) {");
            _js.AppendLine("                lightModeDialog.remove();");
            _js.AppendLine("                console.log('[私信] 已关闭PIN验证弹窗');");
            _js.AppendLine("            }");
            _js.AppendLine("        }");
            _js.AppendLine("        ");
            _js.AppendLine("        const inputDialog = document.querySelector('div[role=dialog]:has(input)');");
            _js.AppendLine("        if (inputDialog) {");
            _js.AppendLine("            const dialogButton = inputDialog.querySelector('div[role=button]');");
            _js.AppendLine("            if (dialogButton) {");
            _js.AppendLine("                dialogButton.click();");
            _js.AppendLine("                await randomDelay(1000, 2000);");
            _js.AppendLine("                ");
            _js.AppendLine("                const restoreButton = document.querySelector('div[role=dialog] div[role=button][aria-label*=\"restore\"]:not([aria-disabled=true]), div[role=dialog] div[role=button][aria-label*=\"还原\"]:not([aria-disabled=true])');");
            _js.AppendLine("                if (restoreButton) {");
            _js.AppendLine("                    restoreButton.click();");
            _js.AppendLine("                    console.log('[私信] 已点击还原按钮');");
            _js.AppendLine("                    await randomDelay(1000, 2000);");
            _js.AppendLine("                }");
            _js.AppendLine("            }");
            _js.AppendLine("        }");
            _js.AppendLine("");
        }
        
        private void InputMessage()
        {
            _js.AppendLine("        // 查找私信编辑器");
            _js.AppendLine("        const editor = document.querySelector('div[data-lexical-editor=true]');");
            _js.AppendLine("        if (!editor) throw new Error('未找到私信编辑器');");
            _js.AppendLine("");
            _js.AppendLine("        // 使用人类打字行为输入消息");
            _js.AppendLine($"        const message = `{EscapeForJsTemplate(_messageText)}`;");
            _js.AppendLine("        await humanTypeText(editor, message);");
            _js.AppendLine("        console.log('[私信发送] 消息输入完成');");
            _js.AppendLine("");
        }
        
        private void ClickSendButton()
        {
            _js.AppendLine("        // 等待一下再发送");
            _js.AppendLine("        await randomDelay(500, 1000);");
            _js.AppendLine("");
            _js.AppendLine("        // 查找发送按钮（多种方式）");
            _js.AppendLine("        let sendButton = null;");
            _js.AppendLine("        ");
            _js.AppendLine("        const buttons = Array.from(document.querySelectorAll('div[role=button], button, span[role=button]'));");
            _js.AppendLine("        for (const btn of buttons) {");
            _js.AppendLine("            const ariaLabel = btn.getAttribute('aria-label') || '';");
            _js.AppendLine("            const text = btn.innerText || '';");
            _js.AppendLine("            if (ariaLabel.toLowerCase().includes('enter') || ");
            _js.AppendLine("                ariaLabel.toLowerCase().includes('send') ||");
            _js.AppendLine("                text.toLowerCase().includes('send') ||");
            _js.AppendLine("                text.toLowerCase().includes('kirim')) {");
            _js.AppendLine("                sendButton = btn;");
            _js.AppendLine("                break;");
            _js.AppendLine("            }");
            _js.AppendLine("        }");
            _js.AppendLine("        ");
            _js.AppendLine("        if (!sendButton) {");
            _js.AppendLine("            sendButton = document.querySelector('div[role=group] > span:nth-child(3) > div[role=button]');");
            _js.AppendLine("        }");
            _js.AppendLine("        ");
            _js.AppendLine("        if (!sendButton) throw new Error('未找到发送按钮');");
            _js.AppendLine("");
            _js.AppendLine("        console.log('[私信发送] 点击发送按钮');");
            _js.AppendLine("        await humanClick(sendButton);");
            _js.AppendLine("");
        }
        
        private void WaitForSendComplete()
        {
            _js.AppendLine("        // 等待发送完成（检查消息状态）");
            _js.AppendLine("        await new Promise((resolve, reject) => {");
            _js.AppendLine("            const timeout = setTimeout(() => reject(new Error('发送超时')), 15000);");
            _js.AppendLine("            ");
            _js.AppendLine("            const checkInterval = setInterval(async () => {");
            _js.AppendLine("                try {");
            _js.AppendLine("                    const messages = document.querySelectorAll('span[role=presentation] span.html-span');");
            _js.AppendLine("                    if (messages.length > 0) {");
            _js.AppendLine("                        const allStatus = Array.from(messages).map(x => ({");
            _js.AppendLine("                            color: window.getComputedStyle(x).color,");
            _js.AppendLine("                            status: x.innerText");
            _js.AppendLine("                        }));");
            _js.AppendLine("                        ");
            _js.AppendLine("                        allStatus.reverse();");
            _js.AppendLine("                        ");
            _js.AppendLine("                        // 检查是否达到发送上限");
            _js.AppendLine("                        const limitText = Array.from(document.querySelectorAll('div > div:nth-child(2) span>span'))");
            _js.AppendLine("                            .find(x => x.innerText.includes('limit') || x.innerText.includes('上限'));");
            _js.AppendLine("                        if (limitText) {");
            _js.AppendLine("                            clearTimeout(timeout);");
            _js.AppendLine("                            clearInterval(checkInterval);");
            _js.AppendLine("                            reject(new Error('24小时内陌生人发送已达上限'));");
            _js.AppendLine("                            return;");
            _js.AppendLine("                        }");
            _js.AppendLine("                        ");
            _js.AppendLine("                        // 检查是否有正在发送的消息");
            _js.AppendLine("                        const isSending = allStatus.some(x => ");
            _js.AppendLine("                            x.status.includes('正在发送') || x.status.includes('Sending')");
            _js.AppendLine("                        );");
            _js.AppendLine("                        if (isSending) return;");
            _js.AppendLine("                        ");
            _js.AppendLine("                        // 检查是否有发送失败的消息");
            _js.AppendLine("                        const isFailed = allStatus.some(x => ");
            _js.AppendLine("                            x.status.includes('无法发送') || ");
            _js.AppendLine("                            x.status.includes('Couldn\\'t') || ");
            _js.AppendLine("                            x.color === 'rgb(240, 40, 74)'");
            _js.AppendLine("                        );");
            _js.AppendLine("                        if (isFailed) {");
            _js.AppendLine("                            clearTimeout(timeout);");
            _js.AppendLine("                            clearInterval(checkInterval);");
            _js.AppendLine("                            reject(new Error('无法发送消息'));");
            _js.AppendLine("                            return;");
            _js.AppendLine("                        }");
            _js.AppendLine("                        ");
            _js.AppendLine("                        // 检查是否全部发送成功");
            _js.AppendLine("                        const isAllSent = allStatus.every(x => ");
            _js.AppendLine("                            x.status.includes('已发送') || x.status.includes('Sent')");
            _js.AppendLine("                        );");
            _js.AppendLine("                        if (isAllSent) {");
            _js.AppendLine("                            clearTimeout(timeout);");
            _js.AppendLine("                            clearInterval(checkInterval);");
            _js.AppendLine("                            resolve();");
            _js.AppendLine("                            return;");
            _js.AppendLine("                        }");
            _js.AppendLine("                    }");
            _js.AppendLine("                } catch (e) {");
            _js.AppendLine("                    console.warn('[私信] 状态检测异常:', e);");
            _js.AppendLine("                }");
            _js.AppendLine("            }, 500);");
            _js.AppendLine("        });");
            _js.AppendLine("        console.log('[私信发送] 发送成功');");
            _js.AppendLine("");
        }
    }
}
