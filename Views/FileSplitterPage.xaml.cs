using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;

namespace REToolBox.Views
{
    public sealed partial class FileSplitterPage : Page
    {
        private DateTime _lastFileSplitterClickTime = DateTime.MinValue;
        private bool _isFileSplitterCooldown = false;

        public FileSplitterPage()
        {
            this.InitializeComponent();
        }

        private async void FileSplitterButton_Click(object sender, RoutedEventArgs e)
        {
            if (_isFileSplitterCooldown)
            {
                var timeLeft = (int)(10 - (DateTime.Now - _lastFileSplitterClickTime).TotalSeconds);
                ShowError("操作过于频繁", $"请等待{timeLeft}秒后再尝试启动文件分割工具");
                return;
            }
            _isFileSplitterCooldown = true;
            _lastFileSplitterClickTime = DateTime.Now;
            FileSplitterButton.IsEnabled = false;
            try
            {
                string exePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Assets", "FileSplitter.exe");
                if (File.Exists(exePath))
                {
                    Process.Start(new ProcessStartInfo
                    {
                        FileName = exePath,
                        UseShellExecute = true
                    });
                }
                else
                {
                    ShowError("文件未找到", "找不到文件分割工具: FileSplitter.exe");
                }
            }
            catch (Exception ex)
            {
                ShowError("启动失败", $"无法启动文件分割工具: {ex.Message}");
            }
            _ = DispatcherQueue.TryEnqueue(async () =>
            {
                await Task.Delay(10000);
                FileSplitterButton.IsEnabled = true;
                _isFileSplitterCooldown = false;
            });
        }

        private async void ShowError(string title, string message)
        {
            if (this.Content == null) return;
            ContentDialog errorDialog = new ContentDialog
            {
                Title = title,
                Content = message,
                CloseButtonText = "确定",
                DefaultButton = ContentDialogButton.Close,
                XamlRoot = this.Content.XamlRoot
            };
            await errorDialog.ShowAsync();
        }
    }
}