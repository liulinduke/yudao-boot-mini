using CefSharp;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace SocialMatrix.WpfHost.Services
{
    /// <summary>
    /// 文件上传对话框处理器 - 自动提供文件路径
    /// </summary>
    public class FileUploadDialogHandler : IDialogHandler
    {
        private readonly List<string> _filePaths;
        public bool WasInvoked { get; private set; }

        public FileUploadDialogHandler(List<string> filePaths)
        {
            _filePaths = filePaths;
        }

        public bool OnFileDialog(
            IWebBrowser chromiumWebBrowser, 
            IBrowser browser, 
            CefFileDialogMode mode, 
            string title, 
            string defaultFilePath, 
            IReadOnlyCollection<string> acceptFilters,
            IReadOnlyCollection<string> acceptFilterDescriptions,
            IReadOnlyCollection<string> selectedAcceptFilters,
            IFileDialogCallback callback)
        {
            WasInvoked = true;
            // 自动提供文件路径，不显示对话框
            if (_filePaths != null && _filePaths.Count > 0)
            {
                try
                {
                    System.Diagnostics.Debug.WriteLine($"[文件上传] 接收文件对话框: mode={mode}, files={_filePaths.Count}, title={title}");
                    callback.Continue(new List<string>(_filePaths));
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"[文件上传] 提交文件失败: {ex}");
                    callback.Cancel();
                }
                return true; // 返回 true 表示已处理
            }

            return false; // 返回 false 使用默认行为
        }
    }
}
