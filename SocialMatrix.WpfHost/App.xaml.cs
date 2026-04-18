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

            // 初始化 CefSharp
            var settings = new CefSettings();
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
