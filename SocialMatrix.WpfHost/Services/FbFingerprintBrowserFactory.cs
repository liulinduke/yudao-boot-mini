using CefSharp;
using CefSharp.Wpf;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using SocialMatrix.WpfHost.Helpers;
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;

namespace SocialMatrix.WpfHost.Services
{
    /// <summary>
    /// Creates the same isolated, fingerprinted browser context used by FB collection/operation.
    /// </summary>
    public static class FbFingerprintBrowserFactory
    {
        public sealed class GlobalConfig
        {
            public bool DisableImages { get; init; } = false;
            public bool DisableVideos { get; init; } = true;
            public int MaxConcurrent { get; init; } = 19;
        }

        private static GlobalConfig _config = new();
        public static GlobalConfig Config => _config;
        public static int MaxConcurrentBrowsers => _config.MaxConcurrent;

        public static void UpdateGlobalConfig(bool disableImages, bool disableVideos, int maxConcurrent)
        {
            _config = new GlobalConfig
            {
                DisableImages = disableImages,
                DisableVideos = disableVideos,
                MaxConcurrent = Math.Min(Math.Max(maxConcurrent, 1), 50)
            };
        }

        public static ChromiumWebBrowser Create(string accountId, long? deviceId, out IRequestContext requestContext)
        {
            var cachePath = BrowserCachePaths.ForAccount(accountId);
            Directory.CreateDirectory(cachePath);
            requestContext = new RequestContext(new RequestContextSettings
            {
                CachePath = cachePath,
                PersistSessionCookies = true
            });

            var browser = new ChromiumWebBrowser("about:blank")
            {
                RequestContext = requestContext,
                Background = System.Windows.Media.Brushes.White,
                Tag = accountId
            };
            FingerprintInjector.ApplyResourceFilter(browser, _config.DisableImages, _config.DisableVideos);
            System.Diagnostics.Debug.WriteLine($"🔒 指纹浏览器创建: account={accountId}, cache={cachePath}, deviceId={deviceId}");
            return browser;
        }

        public static async Task InitializeAsync(ChromiumWebBrowser browser, string accountId, string? cookie,
            long? deviceId, string initialUrl)
        {
            try
            {
                FingerprintInjector.ApplyResourceFilter(browser, _config.DisableImages, _config.DisableVideos);
                if (!string.IsNullOrWhiteSpace(cookie))
                {
                    await InjectCookiesAsync(browser, accountId, cookie);
                }

                browser.FrameLoadEnd += async (_, args) =>
                {
                    if (args.Frame.IsMain)
                    {
                        await InjectFingerprintWhenReadyAsync(browser, deviceId);
                    }
                };
                browser.Load(initialUrl);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"❌ 账号 {accountId} 指纹浏览器初始化失败: {ex.Message}");
                throw;
            }
        }

        private static async Task InjectFingerprintWhenReadyAsync(ChromiumWebBrowser browser, long? deviceId)
        {
            for (var attempt = 0; attempt < 10; attempt++)
            {
                try
                {
                    if (browser.IsDisposed) return;
                    if (browser.CanExecuteJavascriptInMainFrame)
                    {
                        await FingerprintInjector.InjectScriptAsync(browser, new FingerprintConfig
                        {
                            Area = "",
                            Latitude = null,
                            Longitude = null,
                            DeviceId = deviceId,
                            DisableImages = _config.DisableImages,
                            DisableVideos = _config.DisableVideos
                        });
                        return;
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"⚠️ 指纹脚本等待V8上下文失败: {ex.Message}");
                }
                await Task.Delay(300);
            }
            System.Diagnostics.Debug.WriteLine("⚠️ 页面未建立V8上下文，跳过本次指纹脚本注入");
        }

        public static async Task<bool> InjectCookiesAsync(ChromiumWebBrowser browser, string accountId, string cookieJson)
        {
            try
            {
                var cookieList = JArray.Parse(cookieJson);
                if (cookieList == null) return false;
                var manager = browser.RequestContext.GetCookieManager(null);
                if (manager == null) return false;
                var success = 0;
                foreach (var item in cookieList)
                {
                    try
                    {
                        var cookie = new CefSharp.Cookie
                        {
                            Name = item["name"]?.ToString(),
                            Value = item["value"]?.ToString(),
                            Domain = item["domain"]?.ToString(),
                            Path = item["path"]?.ToString() ?? "/",
                            Secure = item["secure"]?.Value<bool>() ?? false,
                            HttpOnly = item["httpOnly"]?.Value<bool>() ?? false,
                            Expires = FacebookCookieExpirationHelper.Parse(item["expirationDate"]),
                            SameSite = CefSharp.Enums.CookieSameSite.NoRestriction
                        };
                        await manager.SetCookieAsync("https://www.facebook.com", cookie);
                        success++;
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"⚠️ Cookie注入失败: {ex.Message}");
                    }
                }
                System.Diagnostics.Debug.WriteLine($"✅ 账号 {accountId} Cookie注入: {success}/{cookieList.Count}");
                return success > 0;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"⚠️ 账号 {accountId} Cookie解析失败: {ex.Message}");
                return false;
            }
        }
    }
}
