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
    /// BrowserMatrixWindow 的发个人帖功能扩展
    /// </summary>
    public partial class BrowserMatrixWindow
    {
        /// <summary>
        /// 生成发个人帖脚本（委托给ScriptBuilder）
        /// </summary>
        private string GeneratePublishPostScript(string actionConfigJson)
        {
            var builder = new PublishPostScriptBuilder(actionConfigJson);
            return builder.Build();
        }
        
        /// <summary>
        /// 生成继续执行脚本（文件上传后的操作）
        /// </summary>
        private string GenerateContinueScript()
        {
            var builder = new PublishPostScriptBuilder("{}");
            return builder.BuildContinueScript();
        }
        
        /// <summary>
        /// 执行发个人帖
        /// </summary>
        public async Task ExecutePublishPost(string accountId, string actionConfigJson)
        {
            try
            {
                var browser = GetBrowser(accountId);
                if (browser == null)
                {
                    throw new InvalidOperationException($"未找到账号 {accountId} 的浏览器");
                }

                // 解析配置获取媒体文件路径
                JObject config = JObject.Parse(actionConfigJson);
                var mediaUrls = config["mediaUrls"]?.ToObject<string[]>() ?? new string[0];
                
                System.Diagnostics.Debug.WriteLine("[发个人帖] 开始执行脚本...");
                
                // 生成并执行脚本（导航、输入内容等）
                var script = GeneratePublishPostScript(actionConfigJson);
                var result = await browser.EvaluateScriptAsync(script);
                
                // 检查脚本是否成功
                if (result.Success && result.Result != null)
                {
                    var resultObj = JsonConvert.DeserializeObject<dynamic>(result.Result.ToString());
                    bool success = resultObj.success;
                    string message = resultObj.message?.ToString() ?? "";
                    
                    if (!success)
                    {
                        System.Diagnostics.Debug.WriteLine($"[发个人帖] 脚本执行失败: {message}");
                        throw new Exception(message);
                    }
                }
                
                // 如果有媒体文件，设置 DialogHandler 并触发文件选择
                if (mediaUrls.Length > 0)
                {
                    System.Diagnostics.Debug.WriteLine($"[发个人帖] 准备上传 {mediaUrls.Length} 个文件");
                    
                    // 设置文件上传处理器
                    var fileHandler = new Services.FileUploadDialogHandler(new List<string>(mediaUrls));
                    browser.DialogHandler = fileHandler;
                    
                    // 等待一下让 handler 生效
                    await Task.Delay(500);
                    
                    // 通过 JavaScript 触发文件输入框的点击事件
                    var triggerFileInputScript = @"
                        (function() {
                            const fileInput = document.querySelector('div[role=dialog] form input[type=file]');
                            if (fileInput) {
                                fileInput.click();
                                return true;
                            }
                            return false;
                        })();
                    ";
                    
                    var triggerResult = await browser.EvaluateScriptAsync(triggerFileInputScript);
                    if (triggerResult.Success && triggerResult.Result != null && Convert.ToBoolean(triggerResult.Result))
                    {
                        System.Diagnostics.Debug.WriteLine("[发个人帖] 已触发文件选择对话框");
                    }
                    else
                    {
                        System.Diagnostics.Debug.WriteLine("[发个人帖] ⚠️ 未找到文件输入框");
                    }
                    
                    // 等待文件上传处理
                    await Task.Delay(2000);
                    
                    System.Diagnostics.Debug.WriteLine("[发个人帖] 所有文件设置完成");
                }
                
                // 继续执行后续操作（点击发布按钮等）
                var continueScript = GenerateContinueScript();
                var continueResult = await browser.EvaluateScriptAsync(continueScript);
                
                if (continueResult.Success && continueResult.Result != null)
                {
                    var resultObj = JsonConvert.DeserializeObject<dynamic>(continueResult.Result.ToString());
                    bool success = resultObj.success;
                    string message = resultObj.message?.ToString() ?? "";
                    
                    if (success)
                    {
                        System.Diagnostics.Debug.WriteLine($"[发个人帖] 执行成功: {message}");
                    }
                    else
                    {
                        System.Diagnostics.Debug.WriteLine($"[发个人帖] 执行失败: {message}");
                        throw new Exception(message);
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[发个人帖] 异常: {ex.Message}");
                throw;
            }
        }
    }
}
