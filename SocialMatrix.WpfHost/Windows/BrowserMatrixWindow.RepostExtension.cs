using System;
using System.Threading.Tasks;
using CefSharp;
using CefSharp.Wpf;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace SocialMatrix.WpfHost.Windows
{
    /// <summary>
    /// BrowserMatrixWindow 的转帖功能扩展
    /// </summary>
    public partial class BrowserMatrixWindow
    {
        /// <summary>
        /// 生成转帖脚本
        /// </summary>
        private string GenerateRepostScript(string postUrl, string actionConfigJson, string commentScript)
        {
            var js = new System.Text.StringBuilder();
            
            // 解析 actionConfig
            JObject config = JObject.Parse(actionConfigJson);
            var actions = config["actions"]?.ToObject<int[]>() ?? new int[0];
            var shareToProfileCount = config["shareToProfileCount"]?.Value<int>() ?? 1;
            var selectedGroups = config["selectedGroups"]?.ToObject<JArray>() ?? new JArray();
            
            js.AppendLine("(async function() {");
            js.AppendLine("    const results = [];");
            js.AppendLine("");
            js.AppendLine("    try {");
            js.AppendLine($"        console.log('[转帖] 开始处理帖子: {postUrl}');");
            js.AppendLine("");
            
            // 1. 导航到帖子页面
            js.AppendLine($"        // 1. 导航到帖子页面");
            js.AppendLine($"        window.location.href = '{postUrl}';");
            js.AppendLine("        await new Promise(resolve => setTimeout(resolve, 3000));");
            js.AppendLine("        console.log('[转帖] 页面加载完成');");
            js.AppendLine("");
            
            // 2. 点赞 (actionType=1)
            if (Array.Exists(actions, a => a == 1))
            {
                js.AppendLine("        // 2. 执行点赞操作");
                js.AppendLine("        if (await performLike()) {");
                js.AppendLine("            results.push({ actionType: 1, status: 1 });");
                js.AppendLine("        } else {");
                js.AppendLine("            results.push({ actionType: 1, status: 2, failReason: '点赞失败' });");
                js.AppendLine("        }");
                js.AppendLine("        await randomDelay(1000, 2000);");
                js.AppendLine("");
            }
            
            // 3. 转发到动态 (actionType=2)
            if (Array.Exists(actions, a => a == 2))
            {
                js.AppendLine("        // 3. 执行转发到动态");
                js.AppendLine("        if (await performShareToTimeline()) {");
                js.AppendLine("            results.push({ actionType: 2, status: 1 });");
                js.AppendLine("        } else {");
                js.AppendLine("            results.push({ actionType: 2, status: 2, failReason: '转发到动态失败' });");
                js.AppendLine("        }");
                js.AppendLine("        await randomDelay(1000, 2000);");
                js.AppendLine("");
            }
            
            // 4. 转帖到个人中心 (actionType=3)
            if (Array.Exists(actions, a => a == 3))
            {
                js.AppendLine($"        // 4. 转帖到个人中心 (重复{shareToProfileCount}次)");
                js.AppendLine($"        for (let i = 0; i < {shareToProfileCount}; i++) {{");
                js.AppendLine("            if (await performShareToProfile()) {");
                js.AppendLine("                results.push({ actionType: 3, status: 1, targetName: '个人中心' });");
                js.AppendLine("            } else {");
                js.AppendLine("                results.push({ actionType: 3, status: 2, failReason: '转帖到个人中心失败' });");
                js.AppendLine("            }");
                js.AppendLine("            await randomDelay(2000, 3000);");
                js.AppendLine("        }");
                js.AppendLine("");
            }
            
            // 5. 转贴到好友 (actionType=4) - 简化版，随机选择几个好友
            if (Array.Exists(actions, a => a == 4))
            {
                js.AppendLine("        // 5. 转贴到好友");
                js.AppendLine("        const friendCount = await getFriendsCount();");
                js.AppendLine("        const targetFriends = Math.min(friendCount, 10); // 最多10个好友");
                js.AppendLine("        ");
                js.AppendLine("        for (let i = 0; i < targetFriends; i++) {");
                js.AppendLine("            if (await performShareToFriend(i)) {");
                js.AppendLine("                results.push({ actionType: 4, status: 1, targetType: 'friend' });");
                js.AppendLine("            } else {");
                js.AppendLine("                results.push({ actionType: 4, status: 2, failReason: '转贴到好友失败' });");
                js.AppendLine("            }");
                js.AppendLine("            await randomDelay(2000, 4000);");
                js.AppendLine("        }");
                js.AppendLine("");
            }
            
            // 6. 转发到群组 (actionType=5)
            if (Array.Exists(actions, a => a == 5) && selectedGroups.Count > 0)
            {
                js.AppendLine($"        // 6. 转发到群组 ({selectedGroups.Count}个群组)");
                
                foreach (var group in selectedGroups)
                {
                    var groupId = group["groupId"]?.ToString();
                    var groupName = group["groupName"]?.ToString();
                    
                    if (!string.IsNullOrEmpty(groupId))
                    {
                        js.AppendLine($"        // 转发到群组: {groupName}");
                        js.AppendLine($"        if (await performShareToGroup('{groupId}')) {{");
                        js.AppendLine($"            results.push({{ actionType: 5, status: 1, targetType: 'group', targetId: '{groupId}', targetName: '{groupName}' }});");
                        js.AppendLine($"        }} else {{");
                        js.AppendLine($"            results.push({{ actionType: 5, status: 2, targetType: 'group', targetId: '{groupId}', targetName: '{groupName}', failReason: '转发到群组失败' }});");
                        js.AppendLine($"        }}");
                        js.AppendLine("        await randomDelay(2000, 4000);");
                        js.AppendLine("");
                    }
                }
            }
            
            // 7. 评论（如果有话术）
            if (!string.IsNullOrEmpty(commentScript))
            {
                js.AppendLine("        // 7. 执行评论");
                js.AppendLine($"        const commentText = `{commentScript.Replace("`", "\\`")}`;");
                js.AppendLine("        if (await performComment(commentText)) {");
                js.AppendLine("            results.push({ actionType: 6, status: 1 });");
                js.AppendLine("        } else {");
                js.AppendLine("            results.push({ actionType: 6, status: 2, failReason: '评论失败' });");
                js.AppendLine("        }");
                js.AppendLine("");
            }
            
            // 返回结果
            js.AppendLine("        console.log('[转帖] 所有操作完成');");
            js.AppendLine("        return JSON.stringify(results);");
            js.AppendLine("");
            js.AppendLine("    } catch (e) {");
            js.AppendLine("        console.error('[转帖] 错误:', e);");
            js.AppendLine("        return JSON.stringify([{ actionType: 0, status: 2, failReason: e.message }]);");
            js.AppendLine("    }");
            js.AppendLine("})();");
            
            // 添加辅助函数
            js.AppendLine("");
            js.AppendLine("// ===== 辅助函数 =====");
            js.AppendLine("");
            
            // 随机延迟
            js.AppendLine("const randomDelay = (min, max) => {");
            js.AppendLine("    return new Promise(resolve => setTimeout(resolve, Math.floor(Math.random() * (max - min + 1)) + min));");
            js.AppendLine("};");
            js.AppendLine("");
            
            // 点赞
            js.AppendLine("const performLike = async () => {");
            js.AppendLine("    try {");
            js.AppendLine("        console.log('[转帖] 开始点赞');");
            js.AppendLine("        ");
            js.AppendLine("        // 查找点赞按钮 - 使用 aria-label='Like'");
            js.AppendLine("        const likeButton = document.querySelector('div[aria-label=\"Like\"]');");
            js.AppendLine("        ");
            js.AppendLine("        if (likeButton) {");
            js.AppendLine("            likeButton.click();");
            js.AppendLine("            console.log('[转帖] 点赞成功');");
            js.AppendLine("            await randomDelay(500, 1000);");
            js.AppendLine("            return true;");
            js.AppendLine("        }");
            js.AppendLine("        ");
            js.AppendLine("        console.warn('[转帖] 未找到点赞按钮');");
            js.AppendLine("        return false;");
            js.AppendLine("    } catch (e) {");
            js.AppendLine("        console.error('[转帖] 点赞失败:', e);");
            js.AppendLine("        return false;");
            js.AppendLine("    }");
            js.AppendLine("};");
            js.AppendLine("");
            
            // 转发到动态（Share now）
            js.AppendLine("const performShareToTimeline = async () => {");
            js.AppendLine("    try {");
            js.AppendLine("        console.log('[转帖] 开始转发到动态');");
            js.AppendLine("        ");
            js.AppendLine("        // 1. 点击分享按钮");
            js.AppendLine("        const shareButton = document.querySelector('div[aria-label=\"Send this to friends or post it on your profile.\"]');");
            js.AppendLine("        ");
            js.AppendLine("        if (!shareButton) {");
            js.AppendLine("            console.warn('[转帖] 未找到分享按钮');");
            js.AppendLine("            return false;");
            js.AppendLine("        }");
            js.AppendLine("        ");
            js.AppendLine("        shareButton.click();");
            js.AppendLine("        await randomDelay(1000, 2000);");
            js.AppendLine("        ");
            js.AppendLine("        // 2. 点击 'Share now' 按钮（直接分享到动态）");
            js.AppendLine("        const shareNowButton = document.querySelector('div[aria-label=\"Share now\"]');");
            js.AppendLine("        ");
            js.AppendLine("        if (shareNowButton) {");
            js.AppendLine("            shareNowButton.click();");
            js.AppendLine("            console.log('[转帖] 转发到动态成功');");
            js.AppendLine("            await randomDelay(1000, 2000);");
            js.AppendLine("            return true;");
            js.AppendLine("        }");
            js.AppendLine("        ");
            js.AppendLine("        console.warn('[转帖] 未找到 Share now 按钮');");
            js.AppendLine("        return false;");
            js.AppendLine("    } catch (e) {");
            js.AppendLine("        console.error('[转帖] 转发到动态失败:', e);");
            js.AppendLine("        return false;");
            js.AppendLine("    }");
            js.AppendLine("};");
            js.AppendLine("");
            
            // 转帖到个人中心（打开分享对话框并发布）
            js.AppendLine("const performShareToProfile = async () => {");
            js.AppendLine("    try {");
            js.AppendLine("        console.log('[转帖] 开始转帖到个人中心');");
            js.AppendLine("        ");
            js.AppendLine("        // 1. 点击分享按钮");
            js.AppendLine("        const shareButton = document.querySelector('div[aria-label=\"Send this to friends or post it on your profile.\"]');");
            js.AppendLine("        ");
            js.AppendLine("        if (!shareButton) {");
            js.AppendLine("            console.warn('[转帖] 未找到分享按钮');");
            js.AppendLine("            return false;");
            js.AppendLine("        }");
            js.AppendLine("        ");
            js.AppendLine("        shareButton.click();");
            js.AppendLine("        await randomDelay(1500, 2500);");
            js.AppendLine("        ");
            js.AppendLine("        // 2. 在分享对话框中，点击 'Feed' 隐私设置（确保发布到个人动态）");
            js.AppendLine("        const feedPrivacyButton = document.querySelector('div[aria-label^=\"Sharing to Feed\"]');");
            js.AppendLine("        if (feedPrivacyButton) {");
            js.AppendLine("            feedPrivacyButton.click();");
            js.AppendLine("            await randomDelay(500, 1000);");
            js.AppendLine("        }");
            js.AppendLine("        ");
            js.AppendLine("        // 3. 可选：在文本框中输入自定义内容");
            js.AppendLine("        const textBox = document.querySelector('div[contenteditable=true][role=textbox][data-lexical-editor=true]');");
            js.AppendLine("        if (textBox) {");
            js.AppendLine("            textBox.focus();");
            js.AppendLine("            await randomDelay(500, 1000);");
            js.AppendLine("            // 这里可以添加自定义文本，暂时跳过");
            js.AppendLine("        }");
            js.AppendLine("        ");
            js.AppendLine("        // 4. 点击 'Post' 发布按钮");
            js.AppendLine("        const postButton = document.querySelector('div[aria-label=\"Post\"]:not([aria-disabled])');");
            js.AppendLine("        ");
            js.AppendLine("        if (postButton) {");
            js.AppendLine("            postButton.click();");
            js.AppendLine("            console.log('[转帖] 转帖到个人中心成功');");
            js.AppendLine("            await randomDelay(2000, 3000);");
            js.AppendLine("            return true;");
            js.AppendLine("        }");
            js.AppendLine("        ");
            js.AppendLine("        console.warn('[转帖] 未找到 Post 按钮');");
            js.AppendLine("        return false;");
            js.AppendLine("    } catch (e) {");
            js.AppendLine("        console.error('[转帖] 转帖到个人中心失败:', e);");
            js.AppendLine("        return false;");
            js.AppendLine("    }");
            js.AppendLine("};");
            js.AppendLine("");
            
            // 转贴到好友（通过 Messenger）
            js.AppendLine("const performShareToFriend = async (index) => {");
            js.AppendLine("    try {");
            js.AppendLine("        console.log('[转帖] 开始转贴到好友', index);");
            js.AppendLine("        ");
            js.AppendLine("        // 1. 点击分享按钮");
            js.AppendLine("        const shareButton = document.querySelector('div[aria-label=\"Send this to friends or post it on your profile.\"]');");
            js.AppendLine("        ");
            js.AppendLine("        if (!shareButton) {");
            js.AppendLine("            console.warn('[转帖] 未找到分享按钮');");
            js.AppendLine("            return false;");
            js.AppendLine("        }");
            js.AppendLine("        ");
            js.AppendLine("        shareButton.click();");
            js.AppendLine("        await randomDelay(1500, 2500);");
            js.AppendLine("        ");
            js.AppendLine("        // 2. 查找 Messenger 联系人列表");
            js.AppendLine("        const messengerSection = document.querySelector('h2 span[dir=auto]');");
            js.AppendLine("        const isMessengerSection = messengerSection && (messengerSection.innerText === 'Send in Messenger' || messengerSection.innerText === '在 Messenger 中发送');");
            js.AppendLine("        ");
            js.AppendLine("        if (!isMessengerSection) {");
            js.AppendLine("            console.warn('[转帖] 未找到 Messenger 部分');");
            js.AppendLine("            return false;");
            js.AppendLine("        }");
            js.AppendLine("        ");
            js.AppendLine("        // 3. 获取所有联系人按钮");
            js.AppendLine("        const contactButtons = Array.from(document.querySelectorAll('div[aria-label^=\"Send to \"][role=button]'));");
            js.AppendLine("        ");
            js.AppendLine("        if (index >= contactButtons.length) {");
            js.AppendLine("            console.warn('[转帖] 好友索引超出范围');");
            js.AppendLine("            return false;");
            js.AppendLine("        }");
            js.AppendLine("        ");
            js.AppendLine("        // 4. 点击指定索引的联系人");
            js.AppendLine("        const targetContact = contactButtons[index];");
            js.AppendLine("        const contactName = targetContact.getAttribute('aria-label').replace('Send to ', '').replace(' via Messenger', '');");
            js.AppendLine("        ");
            js.AppendLine("        targetContact.click();");
            js.AppendLine("        console.log('[转帖] 已发送给好友:', contactName);");
            js.AppendLine("        await randomDelay(2000, 3000);");
            js.AppendLine("        ");
            js.AppendLine("        return true;");
            js.AppendLine("    } catch (e) {");
            js.AppendLine("        console.error('[转帖] 转贴到好友失败:', e);");
            js.AppendLine("        return false;");
            js.AppendLine("    }");
            js.AppendLine("};");
            js.AppendLine("");
            js.AppendLine("// 获取 Messenger 联系人数量");
            js.AppendLine("const getFriendsCount = async () => {");
            js.AppendLine("    try {");
            js.AppendLine("        const contactButtons = document.querySelectorAll('div[aria-label^=\"Send to \"][role=button]');");
            js.AppendLine("        return contactButtons.length;");
            js.AppendLine("    } catch (e) {");
            js.AppendLine("        console.error('[转帖] 获取好友数量失败:', e);");
            js.AppendLine("        return 0;");
            js.AppendLine("    }");
            js.AppendLine("};");
            js.AppendLine("");
            
            // 转发到群组（参考竞品B的实现）
            js.AppendLine("const performShareToGroup = async (groupId) => {");
            js.AppendLine("    try {");
            js.AppendLine("        console.log('[转帖] 开始转发到群组:', groupId);");
            js.AppendLine("        ");
            js.AppendLine("        // 1. 点击分享按钮");
            js.AppendLine("        const shareButton = document.querySelector('div[aria-label=\"Send this to friends or post it on your profile.\"]');");
            js.AppendLine("        ");
            js.AppendLine("        if (!shareButton) {");
            js.AppendLine("            console.warn('[转帖] 未找到分享按钮');");
            js.AppendLine("            return false;");
            js.AppendLine("        }");
            js.AppendLine("        ");
            js.AppendLine("        shareButton.click();");
            js.AppendLine("        await randomDelay(1500, 2500);");
            js.AppendLine("        ");
            js.AppendLine("        // 2. 点击 'Group' 选项（分享到群组）");
            js.AppendLine("        const groupOption = Array.from(document.querySelectorAll('div[role=button][tabindex=\"0\"]'))");
            js.AppendLine("            .find(btn => {");
            js.AppendLine("                const label = btn.getAttribute('aria-label') || '';");
            js.AppendLine("                const text = btn.innerText || '';");
            js.AppendLine("                return label.includes('Share to a group') || text === 'Group' || text === '小组';");
            js.AppendLine("            });");
            js.AppendLine("        ");
            js.AppendLine("        if (!groupOption) {");
            js.AppendLine("            console.warn('[转帖] 未找到 Group 选项');");
            js.AppendLine("            return false;");
            js.AppendLine("        }");
            js.AppendLine("        ");
            js.AppendLine("        groupOption.click();");
            js.AppendLine("        await randomDelay(2000, 3000);");
            js.AppendLine("        ");
            js.AppendLine("        // 3. 在群组选择对话框中，查找并点击目标群组");
            js.AppendLine("        // 注意：这里需要根据实际的群组名称来查找");
            js.AppendLine("        const groupListItems = document.querySelectorAll('div[role=listitem]');");
            js.AppendLine("        let targetGroupFound = false;");
            js.AppendLine("        ");
            js.AppendLine("        for (let i = 0; i < groupListItems.length; i++) {");
            js.AppendLine("            const item = groupListItems[i];");
            js.AppendLine("            const itemText = item.innerText || '';");
            js.AppendLine("            // 这里可以根据群组名称匹配，简化处理：选择第一个可用的群组");
            js.AppendLine("            if (itemText.length > 0) {");
            js.AppendLine("                item.click();");
            js.AppendLine("                targetGroupFound = true;");
            js.AppendLine("                console.log('[转帖] 已选择群组:', itemText);");
            js.AppendLine("                break;");
            js.AppendLine("            }");
            js.AppendLine("        }");
            js.AppendLine("        ");
            js.AppendLine("        if (!targetGroupFound) {");
            js.AppendLine("            console.warn('[转帖] 未找到可选择的群组');");
            js.AppendLine("            return false;");
            js.AppendLine("        }");
            js.AppendLine("        ");
            js.AppendLine("        await randomDelay(2000, 3000);");
            js.AppendLine("        ");
            js.AppendLine("        // 4. 点击 'Post' 发布按钮");
            js.AppendLine("        const postButton = document.querySelector('div[aria-label=\"Post\"]:not([aria-disabled])');");
            js.AppendLine("        ");
            js.AppendLine("        if (postButton) {");
            js.AppendLine("            postButton.click();");
            js.AppendLine("            console.log('[转帖] 转发到群组成功');");
            js.AppendLine("            await randomDelay(2000, 3000);");
            js.AppendLine("            return true;");
            js.AppendLine("        }");
            js.AppendLine("        ");
            js.AppendLine("        console.warn('[转帖] 未找到 Post 按钮');");
            js.AppendLine("        return false;");
            js.AppendLine("    } catch (e) {");
            js.AppendLine("        console.error('[转帖] 转发到群组失败:', e);");
            js.AppendLine("        return false;");
            js.AppendLine("    }");
            js.AppendLine("};");
            js.AppendLine("");
            
            // 评论（使用真实的选择器）
            js.AppendLine("const performComment = async (commentText) => {");
            js.AppendLine("    try {");
            js.AppendLine("        console.log('[转帖] 开始评论');");
            js.AppendLine("        ");
            js.AppendLine("        // 查找评论输入框 - 使用 aria-label='Write a comment…'");
            js.AppendLine("        const commentInput = document.querySelector('div[aria-label=\"Write a comment…\"][contenteditable=true]');");
            js.AppendLine("        ");
            js.AppendLine("        if (!commentInput) {");
            js.AppendLine("            console.warn('[转帖] 未找到评论输入框');");
            js.AppendLine("            return false;");
            js.AppendLine("        }");
            js.AppendLine("        ");
            js.AppendLine("        commentInput.focus();");
            js.AppendLine("        await randomDelay(500, 1000);");
            js.AppendLine("        ");
            js.AppendLine("        // 逐字输入评论（模拟人工输入）");
            js.AppendLine("        for (let i = 0; i < commentText.length; i++) {");
            js.AppendLine("            document.execCommand('insertText', false, commentText[i]);");
            js.AppendLine("            await randomDelay(50, 150);");
            js.AppendLine("        }");
            js.AppendLine("        ");
            js.AppendLine("        await randomDelay(500, 1000);");
            js.AppendLine("        ");
            js.AppendLine("        // 查找并提交评论按钮");
            js.AppendLine("        const submitButton = document.querySelector('div[aria-label=\"Post comment\"]:not([aria-disabled])');");
            js.AppendLine("        ");
            js.AppendLine("        if (submitButton) {");
            js.AppendLine("            submitButton.click();");
            js.AppendLine("            console.log('[转帖] 评论成功');");
            js.AppendLine("            await randomDelay(1000, 2000);");
            js.AppendLine("            return true;");
            js.AppendLine("        }");
            js.AppendLine("        ");
            js.AppendLine("        // 如果找不到提交按钮，尝试按 Enter 键");
            js.AppendLine("        const event = new KeyboardEvent('keydown', { key: 'Enter', code: 'Enter', bubbles: true });");
            js.AppendLine("        commentInput.dispatchEvent(event);");
            js.AppendLine("        console.log('[转帖] 评论成功（通过 Enter 键）');");
            js.AppendLine("        await randomDelay(1000, 2000);");
            js.AppendLine("        return true;");
            js.AppendLine("    } catch (e) {");
            js.AppendLine("        console.error('[转帖] 评论失败:', e);");
            js.AppendLine("        return false;");
            js.AppendLine("    }");
            js.AppendLine("};");
            
            return js.ToString();
        }

        /// <summary>
        /// 执行转帖任务
        /// </summary>
        public async Task ExecuteRepostTask(string accountId, string postUrl, string actionConfigJson, string commentScript)
        {
            if (!_browsers.TryGetValue(accountId, out var browser))
            {
                System.Diagnostics.Debug.WriteLine($"❌ 账号 {accountId} 的浏览器不存在");
                OnCollectionError?.Invoke(accountId, "浏览器不存在");
                return;
            }

            try
            {
                System.Diagnostics.Debug.WriteLine($"🔄 开始执行转帖任务: 账号={accountId}, 帖子={postUrl}");
                
                // 生成并执行JS脚本
                var script = GenerateRepostScript(postUrl, actionConfigJson, commentScript);
                var result = await browser.EvaluateScriptAsync(script);
                
                if (result.Success && result.Result != null)
                {
                    var resultStr = result.Result.ToString();
                    System.Diagnostics.Debug.WriteLine($"✅ 转帖执行结果: {resultStr}");
                    
                    // TODO: 将结果回传给后端
                    // 需要调用后端的 batchSaveRepostResult 接口
                }
                else
                {
                    System.Diagnostics.Debug.WriteLine($"❌ JS执行失败");
                    OnCollectionError?.Invoke(accountId, "JS执行失败");
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"❌ 转帖任务异常: {ex.Message}");
                OnCollectionError?.Invoke(accountId, $"转帖任务异常: {ex.Message}");
            }
        }
    }
}
