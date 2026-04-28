using CefSharp;
using CefSharp.Wpf;
using System;
using System.IO;
using System.Windows;

namespace SocialMatrix.WpfHost
{
    /// <summary>
    /// Application 入口，初始化 CefSharp
    /// </summary>
    public partial class App : Application
    {
        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            // 初始化 CefSharp - 启用持久化会话支持
            var settings = new CefSettings();
            
            // 设置全局缓存根目录（每个账号会有子目录）
            string cacheRoot = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "BrowserCache");
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
