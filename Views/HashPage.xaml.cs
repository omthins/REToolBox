using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using System;
using System.IO;
using System.Threading.Tasks;

namespace REToolBox.Views
{
    public sealed partial class HashPage : Page
    {
        // HTML 文件路径（根据实际位置修改）
        private const string HtmlFilePath = @"Assets\hash.html";

        public HashPage()
        {
            this.InitializeComponent();
            InitializeWebView();
        }

        private async void InitializeWebView()
        {
            try
            {
                // 初始化 WebView2 环境
                await HtmlWebView.EnsureCoreWebView2Async();

                // 设置导航完成事件
                HtmlWebView.CoreWebView2.NavigationCompleted += (s, e) =>
                {
                    // 加载完成后隐藏进度环
                    LoadingRing.Visibility = Visibility.Collapsed;
                };

                // 加载本地 HTML 文件
                await LoadLocalHtml();
            }
            catch (Exception ex)
            {
                // 错误处理
                LoadingRing.Visibility = Visibility.Collapsed;
                ContentDialog errorDialog = new ContentDialog
                {
                    Title = "加载错误",
                    Content = $"无法加载 HTML 文件: {ex.Message}",
                    CloseButtonText = "确定",
                    XamlRoot = this.Content.XamlRoot
                };
                await errorDialog.ShowAsync();
            }
        }

        private async Task LoadLocalHtml()
        {
            // 获取应用安装目录
            var appFolder = Windows.ApplicationModel.Package.Current.InstalledLocation.Path;
            var fullPath = Path.Combine(appFolder, HtmlFilePath);

            if (File.Exists(fullPath))
            {
                // 使用 file:// 协议加载本地文件
                var uri = new Uri($"file:///{fullPath.Replace('\\', '/')}");
                HtmlWebView.Source = uri;
            }
            else
            {
                // 文件不存在时显示错误
                LoadingRing.Visibility = Visibility.Collapsed;
                ContentDialog errorDialog = new ContentDialog
                {
                    Title = "文件未找到",
                    Content = $"HTML 文件不存在: {fullPath}",
                    CloseButtonText = "确定",
                    XamlRoot = this.Content.XamlRoot
                };
                await errorDialog.ShowAsync();
            }
        }

        protected override void OnNavigatedFrom(NavigationEventArgs e)
        {
            // 离开页面时清理资源
            HtmlWebView.Close();
            base.OnNavigatedFrom(e);
        }
    }
}