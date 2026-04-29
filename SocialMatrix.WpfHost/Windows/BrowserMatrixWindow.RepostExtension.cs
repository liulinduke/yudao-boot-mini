using System;
using System.Threading.Tasks;
using CefSharp;
using CefSharp.Wpf;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using SocialMatrix.WpfHost.Services;

namespace SocialMatrix.WpfHost.Windows
{
    /// <summary>
    /// BrowserMatrixWindow 的转帖功能扩展
    /// </summary>
    public partial class BrowserMatrixWindow
    {
        /// <summary>
        /// 生成转帖脚本（委托给ScriptBuilder）
        /// </summary>
        private string GenerateRepostScript(string postUrl, string actionConfigJson, string commentScript)
        {
            var builder = new RepostScriptBuilder(postUrl, actionConfigJson, commentScript);
            return builder.Build();
        }
        
        /// <summary>
        /// 执行转帖
        /// </summary>
        public async Task ExecuteRepost(string accountId, string postUrl, string actionConfigJson, string commentScript)
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
