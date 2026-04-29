using System;
using System.Threading.Tasks;
using CefSharp;
using CefSharp.Wpf;
using SocialMatrix.WpfHost.Services;

namespace SocialMatrix.WpfHost.Windows
{
    /// <summary>
    /// BrowserMatrixWindow 的私信发送功能扩展
    /// </summary>
    public partial class BrowserMatrixWindow
    {
        /// <summary>
        /// 生成私信发送脚本（委托给ScriptBuilder）
        /// </summary>
        private string GenerateDmSendScript(string fbUserId, string messageText)
        {
            var builder = new DmScriptBuilder(fbUserId, messageText);
            return builder.Build();
        }
        /// <summary>
        /// 执行私信发送
        /// </summary>
        public async Task SendDirectMessage(string accountId, string fbUserId, string messageText)
        {
            if (!_browsers.TryGetValue(accountId, out var browser))
            {
                System.Diagnostics.Debug.WriteLine($"❌ 账号 {accountId} 的浏览器不存在");
                OnCollectionError?.Invoke(accountId, "浏览器不存在");
                return;
            }

            try
            {
                System.Diagnostics.Debug.WriteLine($"📨 开始发送私信: 账号={accountId}, 目标={fbUserId}");
                
                // 1. C#层面等待页面加载完成（使用IsLoading智能检测）
                System.Diagnostics.Debug.WriteLine($"📌 等待私信页面加载...");
                await WaitForPageLoad(browser, timeoutMs: 15000);
                System.Diagnostics.Debug.WriteLine($"✅ 私信页面加载完成");
                
                // 2. 生成并执行JS脚本（只处理业务逻辑，不再等待页面加载）
                var script = GenerateDmSendScript(fbUserId, messageText);
                var result = await browser.EvaluateScriptAsync(script);
                
                if (result.Success && result.Result != null)
                {
                    var resultStr = result.Result.ToString();
                    System.Diagnostics.Debug.WriteLine($"✅ 私信发送结果: {resultStr}");
                    
                    // 解析结果
                    var resultObj = Newtonsoft.Json.JsonConvert.DeserializeObject<dynamic>(resultStr);
                    if (resultObj != null && resultObj.success == true)
                    {
                        System.Diagnostics.Debug.WriteLine($"✅ 私信发送成功");
                    }
                    else
                    {
                        var errorMsg = resultObj?.message?.ToString() ?? "未知错误";
                        System.Diagnostics.Debug.WriteLine($"❌ 私信发送失败: {errorMsg}");
                        OnCollectionError?.Invoke(accountId, $"私信发送失败: {errorMsg}");
                    }
                }
                else
                {
                    System.Diagnostics.Debug.WriteLine($"❌ JS执行失败");
                    OnCollectionError?.Invoke(accountId, "JS执行失败");
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"❌ 私信发送异常: {ex.Message}");
                OnCollectionError?.Invoke(accountId, $"私信发送异常: {ex.Message}");
            }
        }
    }
}
