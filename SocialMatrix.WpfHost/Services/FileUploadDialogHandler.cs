using CefSharp;
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
            // 自动提供文件路径，不显示对话框
            if (_filePaths != null && _filePaths.Count > 0)
            {
                callback.Continue(new List<string>(_filePaths));
                return true; // 返回 true 表示已处理
            }

            return false; // 返回 false 使用默认行为
        }
    }
}
