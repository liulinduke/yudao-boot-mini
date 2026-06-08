using Newtonsoft.Json.Linq;
using SocialMatrix.WpfHost.Services;

namespace SocialMatrix.WpfHost.Services
{
    /// <summary>
    /// 发群帖脚本生成器
    /// </summary>
    public class GroupPublishScriptBuilder : FacebookScriptBuilder
    {
        private readonly string _actionConfigJson;
        
        public GroupPublishScriptBuilder(string actionConfigJson)
        {
            _actionConfigJson = actionConfigJson;
        }
        
        /// <summary>
        /// 生成完整的发群帖脚本（已废弃，现在使用C#控制循环）
        /// </summary>
        [System.Obsolete("请使用C#层面的ExecuteGroupPublish方法")]
        public override string Build()
        {
            var config = JObject.Parse(_actionConfigJson);
            var postContent = config["postContent"]?.ToString() ?? "";
            var mediaUrls = config["mediaUrls"]?.ToObject<string[]>() ?? new string[0];
            var anonymouslyPost = config["anonymouslyPost"]?.Value<bool>() ?? false;
            var groupType = config["groupType"]?.Value<int>() ?? 1;
            var selectedGroups = config["selectedGroups"]?.ToObject<JArray>() ?? new JArray();
            var selectedUnjoinedGroups = config["selectedUnjoinedGroups"]?.ToObject<JArray>() ?? new JArray();
            var minIntervalSeconds = config["minIntervalSeconds"]?.Value<int>() ?? 10;
            var maxIntervalSeconds = config["maxIntervalSeconds"]?.Value<int>() ?? 20;
            
            BeginScript();
            
            // 确定要发布的群组列表
            BuildTargetGroups(groupType, selectedGroups, selectedUnjoinedGroups);
            
            // 遍历群组进行发布
            BuildGroupLoop(postContent, mediaUrls, anonymouslyPost, minIntervalSeconds, maxIntervalSeconds);
            
            return EndScript();
        }
        
        private void BuildTargetGroups(int groupType, JArray selectedGroups, JArray selectedUnjoinedGroups)
        {
            if (groupType == 1 && selectedGroups.Count > 0)
            {
                _js.AppendLine($"        // 使用已选择的 {selectedGroups.Count} 个已加入群组");
                _js.AppendLine("        const targetGroups = [");
                
                foreach (var group in selectedGroups)
                {
                    var groupId = group["groupId"]?.ToString();
                    var groupName = group["groupName"]?.ToString();
                    var groupUrl = group["groupUrl"]?.ToString();
                    
                    if (!string.IsNullOrEmpty(groupUrl))
                    {
                        _js.AppendLine($"            {{ groupId: '{groupId}', groupName: '{groupName}', groupUrl: '{groupUrl}' }},");
                    }
                }
                
                _js.AppendLine("        ];");
                _js.AppendLine("");
            }
            else if (groupType == 2 && selectedUnjoinedGroups.Count > 0)
            {
                _js.AppendLine($"        // 使用已选择的 {selectedUnjoinedGroups.Count} 个未加入群组");
                _js.AppendLine("        const targetGroups = [");
                
                foreach (var group in selectedUnjoinedGroups)
                {
                    var groupId = group["groupId"]?.ToString();
                    var groupName = group["groupName"]?.ToString();
                    var groupUrl = group["groupUrl"]?.ToString();
                    
                    if (!string.IsNullOrEmpty(groupUrl))
                    {
                        _js.AppendLine($"            {{ groupId: '{groupId}', groupName: '{groupName}', groupUrl: '{groupUrl}' }},");
                    }
                }
                
                _js.AppendLine("        ];");
                _js.AppendLine("");
            }
            else
            {
                _js.AppendLine("        console.error('[发群帖] 未选择任何群组');");
                _js.AppendLine("        return JSON.stringify({ success: false, message: '请至少选择一个群组' });");
                _js.AppendLine("        const targetGroups = [];");
                _js.AppendLine("");
            }
        }
        
        private void BuildGroupLoop(string postContent, string[] mediaUrls, bool anonymouslyPost, int minIntervalSeconds, int maxIntervalSeconds)
        {
            _js.AppendLine("        // 遍历群组进行发布");
            _js.AppendLine("        for (let i = 0; i < targetGroups.length; i++) {");
            _js.AppendLine("            const group = targetGroups[i];");
            _js.AppendLine("            console.log(`[发群帖] 正在发布到群组 ${i + 1}/${targetGroups.length}: ${group.groupName}`);");
            _js.AppendLine("");
            
            // 1. 导航到群组页面
            _js.AppendLine("            try {");
            _js.AppendLine("                // 1. 导航到群组页面");
            _js.AppendLine("                window.location.href = group.groupUrl;");
            _js.AppendLine("                await randomDelay(2000, 3000);");
            _js.AppendLine("                console.log('[发群帖] 群组页面加载完成');");
            _js.AppendLine("");
            
            // 2. 点击发帖框
            _js.AppendLine("                // 2. 点击发帖框");
            _js.AppendLine("                const postBox = document.querySelector('span:has(>span a[href])+div[role=button]:has(>div+div[role=none][data-visualcompletion])');");
            _js.AppendLine("                if (!postBox) throw new Error('未找到发帖框');");
            _js.AppendLine("                await humanClick(postBox);");
            _js.AppendLine("                await randomDelay(1000, 2000);");
            _js.AppendLine("");
            
            // 3. 输入帖子内容
            if (!string.IsNullOrEmpty(postContent))
            {
                _js.AppendLine("                // 3. 输入帖子内容");
                _js.AppendLine("                const textbox = document.querySelector('div[role=dialog] form div[role=textbox]');");
                _js.AppendLine("                if (textbox) {");
                _js.AppendLine($"                    const content = `{EscapeForJsTemplate(postContent)}`;");
                _js.AppendLine("                    await humanTypeText(textbox, content);");
                _js.AppendLine("                    await randomDelay(500, 1000);");
                _js.AppendLine("                    ");
                _js.AppendLine("                    const dialog = document.querySelector('div[role=dialog] form div[role=dialog]');");
                _js.AppendLine("                    if (dialog) {");
                _js.AppendLine("                        await humanClick(dialog);");
                _js.AppendLine("                        await randomDelay(1000, 2000);");
                _js.AppendLine("                    }");
                _js.AppendLine("                }");
                _js.AppendLine("");
            }
            
            // 4. 上传图片/视频
            if (mediaUrls != null && mediaUrls.Length > 0)
            {
                _js.AppendLine("                // 4. 上传图片/视频（由C#层面处理）");
                _js.AppendLine("                const imageButton = document.querySelector('div[role=dialog] div[role=button][aria-label=\"Photo/video\"]:not([aria-disabled]), div[role=dialog] div[role=button][aria-label=\"照片/视频\"]:not([aria-disabled])');");
                _js.AppendLine("                if (imageButton) {");
                _js.AppendLine("                    await humanClick(imageButton);");
                _js.AppendLine("                    await randomDelay(1000, 2000);");
                _js.AppendLine("                }");
                _js.AppendLine("");
            }
            
            // 5. 匿名发帖
            if (anonymouslyPost)
            {
                _js.AppendLine("                // 5. 设置匿名发帖");
                _js.AppendLine("                const anonymousCheckbox = document.querySelector('div[role=dialog] input[type=checkbox]');");
                _js.AppendLine("                if (anonymousCheckbox) {");
                _js.AppendLine("                    anonymousCheckbox.click();");
                _js.AppendLine("                    await randomDelay(500, 1000);");
                _js.AppendLine("                    ");
                _js.AppendLine("                    const gotItButton = document.querySelector('div[role=dialog] div[role=button][aria-label=\"Got it\"], div[role=dialog] div[role=button][aria-label=\"知道了\"]');");
                _js.AppendLine("                    if (gotItButton) {");
                _js.AppendLine("                        await humanClick(gotItButton);");
                _js.AppendLine("                        await randomDelay(500, 1000);");
                _js.AppendLine("                    }");
                _js.AppendLine("                }");
                _js.AppendLine("");
            }
            
            // 6. 点击发布按钮
            _js.AppendLine("                // 6. 点击发布按钮");
            _js.AppendLine("                const publishButton = document.querySelector('div[role=dialog] div[role=button][aria-label=Post]:not([aria-disabled]), div[role=dialog] div[role=button][aria-label=发布]:not([aria-disabled]), div[role=dialog] div[role=button][aria-label=Submit]:not([aria-disabled]), div[role=dialog] div[role=button][aria-label=提交]:not([aria-disabled])');");
            _js.AppendLine("                if (!publishButton) throw new Error('未找到发布按钮');");
            _js.AppendLine("                await humanClick(publishButton);");
            _js.AppendLine("                console.log('[发群帖] 已点击发布按钮');");
            _js.AppendLine("");
            
            // 7. 等待发布完成
            WaitForDialogClose(30000);
            
            // 8. 随机间隔
            _js.AppendLine($"                // 8. 随机间隔 ({minIntervalSeconds}-{maxIntervalSeconds}秒)");
            _js.AppendLine($"                if (i < targetGroups.length - 1) {{");
            _js.AppendLine($"                    await randomDelay({minIntervalSeconds * 1000}, {maxIntervalSeconds * 1000});");
            _js.AppendLine("                }");
            _js.AppendLine("");
            
            // 异常处理
            _js.AppendLine("            } catch (e) {");
            _js.AppendLine("                console.error(`[发群帖] 发布到 ${group.groupName} 失败:`, e);");
            _js.AppendLine("            }");
            _js.AppendLine("        }");
            _js.AppendLine("");
        }
    }
}
