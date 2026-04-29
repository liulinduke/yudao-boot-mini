using System;
using Newtonsoft.Json.Linq;
using SocialMatrix.WpfHost.Services;

namespace SocialMatrix.WpfHost.Services
{
    /// <summary>
    /// 转帖脚本生成器
    /// </summary>
    public class RepostScriptBuilder : FacebookScriptBuilder
    {
        private readonly string _postUrl;
        private readonly string _actionConfigJson;
        private readonly string _commentScript;
        
        public RepostScriptBuilder(string postUrl, string actionConfigJson, string commentScript = "")
        {
            _postUrl = postUrl;
            _actionConfigJson = actionConfigJson;
            _commentScript = commentScript;
        }
        
        /// <summary>
        /// 生成完整的转帖脚本
        /// </summary>
        public string Build()
        {
            var config = JObject.Parse(_actionConfigJson);
            var actions = config["actions"]?.ToObject<int[]>() ?? new int[0];
            var shareToProfileCount = config["shareToProfileCount"]?.Value<int>() ?? 1;
            var selectedGroups = config["selectedGroups"]?.ToObject<JArray>() ?? new JArray();
            
            BeginScript();
            
            // 1. 导航到帖子页面
            NavigateToPost();
            
            // 2-7. 执行各种操作
            ExecuteActions(actions, shareToProfileCount, selectedGroups);
            
            return EndScript();
        }
        
        private void NavigateToPost()
        {
            _js.AppendLine($"        // 1. 导航到帖子页面");
            _js.AppendLine($"        window.location.href = '{_postUrl}';");
            _js.AppendLine("        await randomDelay(3000, 4000);");
            _js.AppendLine("        console.log('[转帖] 页面加载完成');");
            _js.AppendLine("");
        }
        
        private void ExecuteActions(int[] actions, int shareToProfileCount, JArray selectedGroups)
        {
            // 点赞 (actionType=1)
            if (Array.Exists(actions, a => a == 1))
            {
                _js.AppendLine("        // 2. 执行点赞操作");
                _js.AppendLine("        const likeButton = document.querySelector('div[role=button][aria-label=\"Like\"], div[role=button][aria-label=\"赞\"]');");
                _js.AppendLine("        if (likeButton) {");
                _js.AppendLine("            await humanClick(likeButton);");
                _js.AppendLine("            results.push({ actionType: 1, status: 1 });");
                _js.AppendLine("        } else {");
                _js.AppendLine("            results.push({ actionType: 1, status: 2, failReason: '未找到点赞按钮' });");
                _js.AppendLine("        }");
                _js.AppendLine("        await randomDelay(1000, 2000);");
                _js.AppendLine("");
            }
            
            // 转发到动态 (actionType=2)
            if (Array.Exists(actions, a => a == 2))
            {
                _js.AppendLine("        // 3. 执行转发到动态");
                _js.AppendLine("        if (await performShare('timeline')) {");
                _js.AppendLine("            results.push({ actionType: 2, status: 1 });");
                _js.AppendLine("        } else {");
                _js.AppendLine("            results.push({ actionType: 2, status: 2, failReason: '转发到动态失败' });");
                _js.AppendLine("        }");
                _js.AppendLine("        await randomDelay(1000, 2000);");
                _js.AppendLine("");
            }
            
            // 转帖到个人中心 (actionType=3)
            if (Array.Exists(actions, a => a == 3))
            {
                _js.AppendLine($"        // 4. 转帖到个人中心 (重复{shareToProfileCount}次)");
                _js.AppendLine($"        for (let i = 0; i < {shareToProfileCount}; i++) {{");
                _js.AppendLine("            if (await performShare('profile')) {");
                _js.AppendLine("                results.push({ actionType: 3, status: 1, targetName: '个人中心' });");
                _js.AppendLine("            } else {");
                _js.AppendLine("                results.push({ actionType: 3, status: 2, failReason: '转帖到个人中心失败' });");
                _js.AppendLine("            }");
                _js.AppendLine("            await randomDelay(2000, 3000);");
                _js.AppendLine("        }");
                _js.AppendLine("");
            }
            
            // 转贴到好友 (actionType=4)
            if (Array.Exists(actions, a => a == 4))
            {
                _js.AppendLine("        // 5. 转贴到好友（最多10个）");
                _js.AppendLine("        const friendCount = Math.min(await getFriendsCount(), 10);");
                _js.AppendLine("        for (let i = 0; i < friendCount; i++) {");
                _js.AppendLine("            if (await performShare('friend', i)) {");
                _js.AppendLine("                results.push({ actionType: 4, status: 1, targetType: 'friend' });");
                _js.AppendLine("            } else {");
                _js.AppendLine("                results.push({ actionType: 4, status: 2, failReason: '转贴到好友失败' });");
                _js.AppendLine("            }");
                _js.AppendLine("            await randomDelay(2000, 4000);");
                _js.AppendLine("        }");
                _js.AppendLine("");
            }
            
            // 转发到群组 (actionType=5)
            if (Array.Exists(actions, a => a == 5) && selectedGroups.Count > 0)
            {
                _js.AppendLine($"        // 6. 转发到群组 ({selectedGroups.Count}个)");
                
                foreach (var group in selectedGroups)
                {
                    var groupId = group["groupId"]?.ToString();
                    var groupName = group["groupName"]?.ToString();
                    
                    if (!string.IsNullOrEmpty(groupId))
                    {
                        _js.AppendLine($"        if (await performShare('group', '{groupId}')) {{");
                        _js.AppendLine($"            results.push({{ actionType: 5, status: 1, targetType: 'group', targetId: '{groupId}', targetName: '{groupName}' }});");
                        _js.AppendLine($"        }} else {{");
                        _js.AppendLine($"            results.push({{ actionType: 5, status: 2, targetType: 'group', targetId: '{groupId}', targetName: '{groupName}', failReason: '转发失败' }});");
                        _js.AppendLine($"        }}");
                        _js.AppendLine("        await randomDelay(2000, 4000);");
                        _js.AppendLine("");
                    }
                }
            }
            
            // 评论
            if (!string.IsNullOrEmpty(_commentScript))
            {
                _js.AppendLine("        // 7. 执行评论");
                _js.AppendLine($"        const commentText = `{EscapeForJsTemplate(_commentScript)}`;");
                _js.AppendLine("        const commentBox = document.querySelector('div[role=textbox]');");
                _js.AppendLine("        if (commentBox) {");
                _js.AppendLine("            await humanTypeText(commentBox, commentText);");
                _js.AppendLine("            await randomDelay(500, 1000);");
                _js.AppendLine("            ");
                _js.AppendLine("            const submitButton = document.querySelector('div[role=button][aria-label*=\"Comment\"], div[role=button][aria-label*=\"评论\"]');");
                _js.AppendLine("            if (submitButton) {");
                _js.AppendLine("                await humanClick(submitButton);");
                _js.AppendLine("                results.push({ actionType: 6, status: 1 });");
                _js.AppendLine("            } else {");
                _js.AppendLine("                results.push({ actionType: 6, status: 2, failReason: '未找到提交按钮' });");
                _js.AppendLine("            }");
                _js.AppendLine("        } else {");
                _js.AppendLine("            results.push({ actionType: 6, status: 2, failReason: '未找到评论框' });");
                _js.AppendLine("        }");
                _js.AppendLine("");
            }
            
            // 返回结果
            _js.AppendLine("        console.log('[转帖] 所有操作完成');");
            _js.AppendLine("        return JSON.stringify(results);");
        }
    }
}
