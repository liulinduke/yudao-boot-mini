using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using CefSharp;
using CefSharp.Wpf;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using SocialMatrix.WpfHost.Services;

namespace SocialMatrix.WpfHost.Windows
{
    /// <summary>
    /// BrowserMatrixWindow 的发群帖功能扩展
    /// </summary>
    public partial class BrowserMatrixWindow
    {
        /// <summary>
        /// 生成发群帖脚本（委托给ScriptBuilder）
        /// </summary>
        private string GenerateGroupPublishScript(string actionConfigJson)
        {
            var builder = new GroupPublishScriptBuilder(actionConfigJson);
            return builder.Build();
        }
        
        /// <summary>
        /// 执行发群帖（方案A：C#控制循环）
        /// </summary>
        public async Task ExecuteGroupPublish(string accountId, string actionConfigJson)
        {
            try
            {
                var browser = GetBrowser(accountId);
                if (browser == null)
                {
                    throw new InvalidOperationException($"未找到账号 {accountId} 的浏览器");
                }

                // 解析配置
                JObject config = JObject.Parse(actionConfigJson);
                var postContent = config["postContent"]?.ToString() ?? "";
                var mediaUrls = config["mediaUrls"]?.ToObject<string[]>() ?? new string[0];
                var anonymouslyPost = config["anonymouslyPost"]?.Value<bool>() ?? false;
                var groupType = config["groupType"]?.Value<int>() ?? 1;
                var selectedGroups = config["selectedGroups"]?.ToObject<JArray>() ?? new JArray();
                var selectedUnjoinedGroups = config["selectedUnjoinedGroups"]?.ToObject<JArray>() ?? new JArray();
                var minIntervalSeconds = config["minIntervalSeconds"]?.Value<int>() ?? 10;
                var maxIntervalSeconds = config["maxIntervalSeconds"]?.Value<int>() ?? 20;
                
                System.Diagnostics.Debug.WriteLine("[发群帖] 开始执行...");
                
                // 确定要发布的群组列表
                List<dynamic> targetGroups = new List<dynamic>();
                
                if (groupType == 1 && selectedGroups.Count > 0)
                {
                    foreach (var group in selectedGroups)
                    {
                        var groupId = group["groupId"]?.ToString();
                        var groupName = group["groupName"]?.ToString();
                        var groupUrl = group["groupUrl"]?.ToString();
                        
                        if (!string.IsNullOrEmpty(groupUrl))
                        {
                            targetGroups.Add(new { groupId, groupName, groupUrl });
                        }
                    }
                    System.Diagnostics.Debug.WriteLine($"[发群帖] 使用 {targetGroups.Count} 个已加入群组");
                }
                else if (groupType == 2 && selectedUnjoinedGroups.Count > 0)
                {
                    foreach (var group in selectedUnjoinedGroups)
                    {
                        var groupId = group["groupId"]?.ToString();
                        var groupName = group["groupName"]?.ToString();
                        var groupUrl = group["groupUrl"]?.ToString();
                        
                        if (!string.IsNullOrEmpty(groupUrl))
                        {
                            targetGroups.Add(new { groupId, groupName, groupUrl });
                        }
                    }
                    System.Diagnostics.Debug.WriteLine($"[发群帖] 使用 {targetGroups.Count} 个未加入群组");
                }
                else
                {
                    throw new Exception("请至少选择一个群组");
                }
                
                // 遍历群组进行发布
                for (int i = 0; i < targetGroups.Count; i++)
                {
                    var group = targetGroups[i];
                    string groupId = group.groupId;
                    string groupName = group.groupName;
                    string groupUrl = group.groupUrl;
                    
                    System.Diagnostics.Debug.WriteLine($"[发群帖] 正在发布到群组 {i + 1}/{targetGroups.Count}: {groupName}");
                    
                    try
                    {
                        // 1. 导航到群组页面
                        await NavigateToGroup(browser, groupUrl);
                        
                        // 2. 点击发帖框并输入内容
                        await InputPostContent(browser, postContent);
                        
                        // 3. 上传文件（如果有）
                        if (mediaUrls.Length > 0)
                        {
                            await UploadMediaFiles(browser, mediaUrls);
                        }
                        
                        // 4. 设置匿名发帖（如果启用）
                        if (anonymouslyPost)
                        {
                            await SetAnonymousPost(browser);
                        }
                        
                        // 5. 点击发布按钮
                        await ClickPublishButton(browser);
                        
                        // 6. 等待发布完成
                        await WaitForPublishComplete(browser);
                        
                        System.Diagnostics.Debug.WriteLine($"[发群帖] ✅ 发布成功: {groupName}");
                        
                        // 7. 随机间隔（防风控），最后一个群组不需要间隔
                        if (i < targetGroups.Count - 1)
                        {
                            var random = new Random();
                            var intervalMs = random.Next(minIntervalSeconds * 1000, maxIntervalSeconds * 1000);
                            System.Diagnostics.Debug.WriteLine($"[发群帖] 等待 {intervalMs / 1000} 秒后继续...");
                            await Task.Delay(intervalMs);
                        }
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"[发群帖] ❌ 发布到 {groupName} 失败: {ex.Message}");
                        // 继续下一个群组
                    }
                }
                
                System.Diagnostics.Debug.WriteLine("[发群帖] 所有操作完成");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[发群帖] 异常: {ex.Message}");
                throw;
            }
        }
        
        /// <summary>
        /// 导航到群组页面
        /// </summary>
        private async Task NavigateToGroup(ChromiumWebBrowser browser, string groupUrl)
        {
            var script = $@"
                (function() {{
                    window.location.href = '{groupUrl}';
                }})();
            ";
            await browser.EvaluateScriptAsync(script);
            await Task.Delay(2000); // 等待页面加载
            System.Diagnostics.Debug.WriteLine("[发群帖] 群组页面加载完成");
        }
        
        /// <summary>
        /// 输入帖子内容
        /// </summary>
        private async Task InputPostContent(ChromiumWebBrowser browser, string postContent)
        {
            if (string.IsNullOrEmpty(postContent))
            {
                return;
            }
            
            var script = $@"
                (async function() {{
                    const postBox = document.querySelector('span:has(>span a[href])+div[role=button]:has(>div+div[role=none][data-visualcompletion])');
                    if (!postBox) {{
                        throw new Error('未找到发帖框');
                    }}
                    postBox.click();
                    await new Promise(resolve => setTimeout(resolve, 1500));
                    
                    const textbox = document.querySelector('div[role=dialog] form div[role=textbox]');
                    if (textbox) {{
                        textbox.focus();
                        document.execCommand('insertText', false, `{postContent.Replace("`", "``")}`);
                        await new Promise(resolve => setTimeout(resolve, 1000));
                        
                        const dialog = document.querySelector('div[role=dialog] form div[role=dialog]');
                        if (dialog) {{
                            dialog.click();
                            await new Promise(resolve => setTimeout(resolve, 1500));
                        }}
                    }}
                }})();
            ";
            
            var result = await browser.EvaluateScriptAsync(script);
            if (!result.Success)
            {
                throw new Exception($"输入帖子内容失败: {result.Message}");
            }
        }
        
        /// <summary>
        /// 上传媒体文件
        /// </summary>
        private async Task UploadMediaFiles(ChromiumWebBrowser browser, string[] mediaUrls)
        {
            System.Diagnostics.Debug.WriteLine($"[发群帖] 准备上传 {mediaUrls.Length} 个文件");
            
            // 设置文件上传处理器
            var fileHandler = new Services.FileUploadDialogHandler(new List<string>(mediaUrls));
            browser.DialogHandler = fileHandler;
            
            // 等待一下让 handler 生效
            await Task.Delay(500);
            
            // 通过 JavaScript 触发文件选择
            var triggerScript = @"
                (function() {
                    const imageButton = document.querySelector('div[role=dialog] div[role=button][aria-label=""Photo/video""]:not([aria-disabled]), div[role=dialog] div[role=button][aria-label=""照片/视频""]:not([aria-disabled])');
                    if (imageButton) {
                        imageButton.click();
                        return true;
                    }
                    return false;
                })();
            ";
            
            var triggerResult = await browser.EvaluateScriptAsync(triggerScript);
            if (triggerResult.Success && triggerResult.Result != null && Convert.ToBoolean(triggerResult.Result))
            {
                System.Diagnostics.Debug.WriteLine("[发群帖] 已触发文件选择对话框");
            }
            else
            {
                System.Diagnostics.Debug.WriteLine("[发群帖] ⚠️ 未找到图片/视频按钮");
            }
            
            // 等待文件上传处理
            await Task.Delay(2000);
            
            System.Diagnostics.Debug.WriteLine("[发群帖] 文件上传完成");
        }
        
        /// <summary>
        /// 设置匿名发帖
        /// </summary>
        private async Task SetAnonymousPost(ChromiumWebBrowser browser)
        {
            var script = @"
                (async function() {
                    const anonymousCheckbox = document.querySelector('div[role=dialog] input[type=checkbox]');
                    if (anonymousCheckbox) {
                        anonymousCheckbox.click();
                        await new Promise(resolve => setTimeout(resolve, 800));
                        
                        const gotItButton = document.querySelector('div[role=dialog] div[role=button][aria-label=""Got it""], div[role=dialog] div[role=button][aria-label=""知道了""]');
                        if (gotItButton) {
                            gotItButton.click();
                            await new Promise(resolve => setTimeout(resolve, 800));
                        }
                    }
                })();
            ";
            
            await browser.EvaluateScriptAsync(script);
            System.Diagnostics.Debug.WriteLine("[发群帖] 已设置匿名发帖");
        }
        
        /// <summary>
        /// 点击发布按钮
        /// </summary>
        private async Task ClickPublishButton(ChromiumWebBrowser browser)
        {
            var script = @"
                (function() {
                    const publishButton = document.querySelector('div[role=dialog] div[role=button][aria-label=Post]:not([aria-disabled]), div[role=dialog] div[role=button][aria-label=发布]:not([aria-disabled]), div[role=dialog] div[role=button][aria-label=Submit]:not([aria-disabled]), div[role=dialog] div[role=button][aria-label=提交]:not([aria-disabled])');
                    if (!publishButton) {
                        throw new Error('未找到发布按钮');
                    }
                    publishButton.click();
                    return true;
                })();
            ";
            
            var result = await browser.EvaluateScriptAsync(script);
            if (!result.Success)
            {
                throw new Exception($"点击发布按钮失败: {result.Message}");
            }
            
            System.Diagnostics.Debug.WriteLine("[发群帖] 已点击发布按钮");
        }
        
        /// <summary>
        /// 等待发布完成
        /// </summary>
        private async Task WaitForPublishComplete(ChromiumWebBrowser browser)
        {
            var script = @"
                (function() {
                    return new Promise((resolve, reject) => {
                        const timeout = setTimeout(() => reject(new Error('发布超时')), 30000);
                        const checkInterval = setInterval(() => {
                            const dialog = document.querySelector('div[role=dialog] form');
                            if (!dialog) {
                                clearTimeout(timeout);
                                clearInterval(checkInterval);
                                resolve();
                            }
                        }, 500);
                    });
                })();
            ";
            
            await browser.EvaluateScriptAsync(script);
            System.Diagnostics.Debug.WriteLine("[发群帖] 发布完成，对话框已关闭");
        }
    }
}
