using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Windows.Storage;
using Windows.Storage.Pickers;
using System.IO;
using System.Net;
using System.Threading.Tasks;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI;
using Windows.ApplicationModel.DataTransfer;
using System.Collections.ObjectModel;
using System.Collections.Generic;

namespace REToolBox.Views
{
    public class DeployItem
    {
        public string Name
        {
            get; set;
        }
        public string Path
        {
            get; set;
        }
        public string Url
        {
            get; set;
        }
        public HttpListener Listener
        {
            get; set;
        }
        public int Port
        {
            get; set;
        }
    }

    public sealed partial class JsqPage : Page
    {
        private ObservableCollection<DeployItem> _deployItems = new ObservableCollection<DeployItem>();
        private int _basePort = 8080;

        public JsqPage()
        {
            this.InitializeComponent();
            DeployList.ItemsSource = _deployItems;
        }

        private async void SelectFolderButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                FolderPicker folderPicker = new FolderPicker();

                var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(App.MainWindow);
                WinRT.Interop.InitializeWithWindow.Initialize(folderPicker, hwnd);

                folderPicker.SuggestedStartLocation = PickerLocationId.Desktop;
                folderPicker.FileTypeFilter.Add("*");

                StorageFolder folder = await folderPicker.PickSingleFolderAsync();
                if (folder != null)
                {
                    await AddDeployment(folder.Path, folder.Name);
                }
            }
            catch (System.Exception ex)
            {
                ShowErrorDialog($"选择文件夹时出错: {ex.Message}");
            }
        }

        private async void SelectHtmlButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                FileOpenPicker filePicker = new FileOpenPicker();

                var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(App.MainWindow);
                WinRT.Interop.InitializeWithWindow.Initialize(filePicker, hwnd);

                filePicker.SuggestedStartLocation = PickerLocationId.Desktop;
                filePicker.FileTypeFilter.Add(".html");
                filePicker.FileTypeFilter.Add(".htm");

                StorageFile file = await filePicker.PickSingleFileAsync();
                if (file != null)
                {
                    await AddDeployment(file.Path, file.Name);
                }
            }
            catch (System.Exception ex)
            {
                ShowErrorDialog($"选择文件时出错: {ex.Message}");
            }
        }

        private async Task AddDeployment(string path, string name)
        {
            try
            {
                int port = _basePort + _deployItems.Count;
                string url = $"http://localhost:{port}/";

                DeployItem item = new DeployItem
                {
                    Name = name,
                    Path = path,
                    Url = url,
                    Port = port
                };

                await StartHttpServer(item);
                _deployItems.Add(item);
                StopAllButton.IsEnabled = true;

                await ShowDeploySuccessDialog(url);
            }
            catch (System.Exception ex)
            {
                ShowErrorDialog($"添加部署失败: {ex.Message}");
            }
        }

        private async Task StartHttpServer(DeployItem item)
        {
            item.Listener = new HttpListener();
            item.Listener.Prefixes.Add($"http://localhost:{item.Port}/");
            item.Listener.Start();

            _ = Task.Run(async () =>
            {
                while (item.Listener.IsListening)
                {
                    try
                    {
                        HttpListenerContext context = await item.Listener.GetContextAsync();
                        await ProcessRequest(context, item.Path);
                    }
                    catch
                    {
                        break;
                    }
                }
            });
        }

        private void StopAllButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                foreach (var item in _deployItems)
                {
                    StopHttpServer(item);
                }
                _deployItems.Clear();
                StopAllButton.IsEnabled = false;
            }
            catch (System.Exception ex)
            {
                ShowErrorDialog($"停止所有服务时出错: {ex.Message}");
            }
        }

        private void StopItemButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                Button button = sender as Button;
                DeployItem item = button.Tag as DeployItem;

                StopHttpServer(item);
                _deployItems.Remove(item);

                if (_deployItems.Count == 0)
                {
                    StopAllButton.IsEnabled = false;
                }
            }
            catch (System.Exception ex)
            {
                ShowErrorDialog($"停止服务时出错: {ex.Message}");
            }
        }

        private void ViewItemButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                Button button = sender as Button;
                DeployItem item = button.Tag as DeployItem;
                _ = ShowDeploySuccessDialog(item.Url);
            }
            catch (System.Exception ex)
            {
                ShowErrorDialog($"查看服务信息时出错: {ex.Message}");
            }
        }

        private void StopHttpServer(DeployItem item)
        {
            if (item.Listener != null && item.Listener.IsListening)
            {
                item.Listener.Stop();
                item.Listener.Close();
            }
        }

        private async Task ProcessRequest(HttpListenerContext context, string basePath)
        {
            HttpListenerResponse response = context.Response;
            string filePath = "";

            try
            {
                if (File.Exists(basePath))
                {
                    filePath = basePath;
                }
                else if (Directory.Exists(basePath))
                {
                    string url = context.Request.Url.AbsolutePath.TrimStart('/');
                    if (string.IsNullOrEmpty(url))
                        url = "index.html";

                    filePath = Path.Combine(basePath, url);

                    if (!File.Exists(filePath))
                    {
                        filePath = Path.Combine(basePath, "index.html");
                    }
                }

                if (File.Exists(filePath))
                {
                    string extension = Path.GetExtension(filePath).ToLower();
                    string contentType = GetContentType(extension);

                    byte[] buffer = await File.ReadAllBytesAsync(filePath);
                    response.ContentType = contentType;
                    response.ContentLength64 = buffer.Length;
                    response.StatusCode = 200;

                    await response.OutputStream.WriteAsync(buffer, 0, buffer.Length);
                }
                else
                {
                    response.StatusCode = 404;
                    byte[] buffer = System.Text.Encoding.UTF8.GetBytes("<h1>404 - File Not Found</h1>");
                    response.ContentLength64 = buffer.Length;
                    await response.OutputStream.WriteAsync(buffer, 0, buffer.Length);
                }
            }
            catch
            {
                response.StatusCode = 500;
                byte[] buffer = System.Text.Encoding.UTF8.GetBytes("<h1>500 - Internal Server Error</h1>");
                response.ContentLength64 = buffer.Length;
                await response.OutputStream.WriteAsync(buffer, 0, buffer.Length);
            }
            finally
            {
                response.OutputStream.Close();
            }
        }

        private string GetContentType(string extension)
        {
            return extension switch
            {
                ".html" or ".htm" => "text/html",
                ".css" => "text/css",
                ".js" => "application/javascript",
                ".json" => "application/json",
                ".png" => "image/png",
                ".jpg" or ".jpeg" => "image/jpeg",
                ".gif" => "image/gif",
                ".svg" => "image/svg+xml",
                ".ico" => "image/x-icon",
                ".txt" => "text/plain",
                _ => "application/octet-stream"
            };
        }

        private async void ShowErrorDialog(string message)
        {
            ContentDialog dialog = new ContentDialog
            {
                Title = "错误",
                Content = message,
                CloseButtonText = "确定",
                XamlRoot = this.XamlRoot
            };
            await dialog.ShowAsync();
        }

        private async Task ShowDeploySuccessDialog(string url)
        {
            ContentDialog dialog = new ContentDialog
            {
                Title = "部署成功",
                Content = new ScrollViewer
                {
                    Content = new StackPanel
                    {
                        Children =
                        {
                            new TextBlock
                            {
                                Text = "网页已成功部署，访问地址：",
                                Margin = new Thickness(0, 0, 0, 10)
                            },
                            new TextBox
                            {
                                Text = url,
                                IsReadOnly = true,
                                Margin = new Thickness(0, 0, 0, 10)
                            }
                        }
                    }
                },
                PrimaryButtonText = "复制地址",
                CloseButtonText = "确定",
                XamlRoot = this.XamlRoot
            };

            ContentDialogResult result = await dialog.ShowAsync();
            if (result == ContentDialogResult.Primary)
            {
                CopyToClipboard(url);
            }
        }

        private void CopyToClipboard(string text)
        {
            DataPackage dataPackage = new DataPackage();
            dataPackage.SetText(text);
            Clipboard.SetContent(dataPackage);
        }

        protected override void OnNavigatedFrom(Microsoft.UI.Xaml.Navigation.NavigationEventArgs e)
        {
            foreach (var item in _deployItems)
            {
                StopHttpServer(item);
            }
            base.OnNavigatedFrom(e);
        }
    }
}