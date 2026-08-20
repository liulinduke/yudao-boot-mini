using CefSharp;
using CefSharp.Wpf;
using System;
using System.IO;
using System.Windows;
using System.Windows.Threading;
using SocialMatrix.WpfHost.Helpers;
using SocialMatrix.WpfHost.Services;
using Velopack;

namespace SocialMatrix.WpfHost
{
    /// <summary>
    /// Application 入口，初始化 CefSharp
    /// </summary>
    public partial class App : Application
    {
        protected override void OnStartup(StartupEventArgs e)
        {
            VelopackApp.Build().Run();
            base.OnStartup(e);
            DispatcherUnhandledException += HandleDispatcherUnhandledException;

            // 初始化 CefSharp - 启用持久化会话支持
            BrowserCachePaths.MigrateLegacyCache();
            var settings = new CefSettings();
            
            // 开启远程调试端口（供 Puppeteer MCP 控制）
            settings.RemoteDebuggingPort = 9222;
            
            // 设置全局缓存根目录（每个账号会有子目录）
            string cacheRoot = BrowserCachePaths.Root;
            if (!Directory.Exists(cacheRoot))
            {
                Directory.CreateDirectory(cacheRoot);
            }
            settings.CachePath = cacheRoot;
            
            // 启用持久化会话
            settings.PersistSessionCookies = true;
            
            if (Cef.IsInitialized != true)
            {
                Cef.Initialize(settings);
            }
        }

        private static void HandleDispatcherUnhandledException(object sender,
            DispatcherUnhandledExceptionEventArgs e)
        {
            var stack = e.Exception.ToString();
            if (stack.Contains("CoreWebView2PrivateHostObjectHelper", StringComparison.Ordinal))
            {
                System.Diagnostics.Debug.WriteLine($"已忽略 WebView2 HostObject 异常: {e.Exception.Message}");
                e.Handled = true;
            }
        }

        protected override void OnExit(ExitEventArgs e)
        {
            if (Cef.IsInitialized == true)
            {
                Cef.Shutdown();
            }
            base.OnExit(e);
        }
    }
}
