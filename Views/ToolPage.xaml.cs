using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Text;
using Windows.Storage;
using Windows.Storage.Pickers;
using Windows.Storage.Provider;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using WinRT.Interop;
using Microsoft.UI;
using System.Collections.ObjectModel;
using System.Diagnostics;
using Windows.UI;
using System.Security.Cryptography;
using Windows.ApplicationModel;
using System.IO;
using System.Threading;
using System.Net.Http;
using System.Net.Http.Headers;

namespace REToolBox.Views
{
    public sealed partial class ToolPage : Page
    {
        private const string SettingsKey_UpperCase = "UpperCaseSetting";
        private const string SettingsKey_LowerCase = "LowerCaseSetting";
        private const string SettingsKey_Numbers = "NumbersSetting";
        private const string SettingsKey_Symbols = "SymbolsSetting";
        private const string SettingsKey_Length = "LengthSetting";

        private bool _isLoading = true;
        private bool _isSaving = false;
        private const string HistoryFileName = "SaveKeyHistory.json";
        private IntPtr _windowHandle = IntPtr.Zero;
        private DateTime _lastFileSplitterClickTime = DateTime.MinValue;
        private bool _isFileSplitterCooldown = false;
        private StorageFile _saveFile;
        private CancellationTokenSource _downloadCts;
        private bool _isDownloadPaused;
        private HttpClient _httpClient;
        private Stopwatch _downloadStopwatch;
        private long _totalBytesDownloaded;
        private long _totalFileSize;

        public class KeyHistoryItem
        {
            public string Id { get; } = Guid.NewGuid().ToString();
            public DateTime Timestamp { get; set; } = DateTime.Now;
            public string FullKey { get; set; } = string.Empty;

            public string KeyDisplay => FullKey.Length > 16 ?
                $"{FullKey.Substring(0, 4)}...{FullKey.Substring(FullKey.Length - 4)}" :
                FullKey;

            public KeyHistoryItem(string key)
            {
                FullKey = key ?? string.Empty;
            }
        }

        private class DownloadPart
        {
            public long Start
            {
                get; set;
            }
            public long End
            {
                get; set;
            }
            public long Downloaded { get; set; } = 0;
        }

        public ObservableCollection<KeyHistoryItem> KeyHistory { get; } = new ObservableCollection<KeyHistoryItem>();

        public ToolPage()
        {
            this.InitializeComponent();
            this.Loaded += OnPageLoaded;
            this.Unloaded += OnPageUnloaded;
            _httpClient = new HttpClient();
            _httpClient.Timeout = TimeSpan.FromMinutes(30);
        }

        private async void Window_VisibilityChanged(object sender, WindowVisibilityChangedEventArgs e)
        {
            if (!e.Visible)
            {
                await SaveKeyHistoryAsync();
                SaveUserSettings();
            }
        }

        private void OnPageLoaded(object sender, RoutedEventArgs e)
        {
            if (Window.Current != null)
            {
                Window.Current.VisibilityChanged += Window_VisibilityChanged;
                _windowHandle = WindowNative.GetWindowHandle(Window.Current);
            }

            _isLoading = true;
            if (KeyHistoryList != null)
            {
                KeyHistoryList.ItemsSource = KeyHistory;
            }
            LoadUserSettings();
            LoadKeyHistory();
            _isLoading = false;
        }

        private async void OnPageUnloaded(object sender, RoutedEventArgs e)
        {
            _downloadCts?.Cancel();
            await SaveKeyHistoryAsync();
            SaveUserSettings();
            if (Window.Current != null)
            {
                Window.Current.VisibilityChanged -= Window_VisibilityChanged;
            }
        }

        private void LoadUserSettings()
        {
            var localSettings = ApplicationData.Current.LocalSettings;
            UpperCaseCheck.IsChecked = localSettings.Values.TryGetValue(SettingsKey_UpperCase, out object upperCase) ? upperCase as bool? ?? true : true;
            LowerCaseCheck.IsChecked = localSettings.Values.TryGetValue(SettingsKey_LowerCase, out object lowerCase) ? lowerCase as bool? ?? true : true;
            NumberCheck.IsChecked = localSettings.Values.TryGetValue(SettingsKey_Numbers, out object numbers) ? numbers as bool? ?? true : true;
            SymbolCheck.IsChecked = localSettings.Values.TryGetValue(SettingsKey_Symbols, out object symbols) ? symbols as bool? ?? false : false;

            if (localSettings.Values.TryGetValue(SettingsKey_Length, out object length))
            {
                if (length is double lenDouble) LengthNumberBox.Value = lenDouble;
                else if (length is int lenInt) LengthNumberBox.Value = lenInt;
                else LengthNumberBox.Value = 12;
            }
            else
            {
                LengthNumberBox.Value = 12;
            }
        }

        private void SaveUserSettings()
        {
            if (_isLoading) return;
            var localSettings = ApplicationData.Current.LocalSettings;
            localSettings.Values[SettingsKey_UpperCase] = UpperCaseCheck.IsChecked ?? false;
            localSettings.Values[SettingsKey_LowerCase] = LowerCaseCheck.IsChecked ?? false;
            localSettings.Values[SettingsKey_Numbers] = NumberCheck.IsChecked ?? false;
            localSettings.Values[SettingsKey_Symbols] = SymbolCheck.IsChecked ?? false;
            localSettings.Values[SettingsKey_Length] = LengthNumberBox.Value;
        }

        private async void LoadKeyHistory()
        {
            try
            {
                KeyHistory.Clear();
                var localFolder = ApplicationData.Current.LocalFolder;
                if (!await FileExistsAsync(localFolder, HistoryFileName))
                {
                    await localFolder.CreateFileAsync(HistoryFileName, CreationCollisionOption.ReplaceExisting);
                }
                var historyFile = await localFolder.GetFileAsync(HistoryFileName);
                var json = await FileIO.ReadTextAsync(historyFile);
                if (!string.IsNullOrWhiteSpace(json))
                {
                    var items = System.Text.Json.JsonSerializer.Deserialize<List<KeyHistoryItem>>(json);
                    if (items != null)
                    {
                        foreach (var item in items.OrderByDescending(i => i.Timestamp))
                        {
                            KeyHistory.Add(item);
                        }
                    }
                }
            }
            catch { }
        }

        private async Task<bool> FileExistsAsync(StorageFolder folder, string fileName)
        {
            try
            {
                await folder.GetFileAsync(fileName);
                return true;
            }
            catch
            {
                return false;
            }
        }

        public async Task SaveKeyHistoryAsync()
        {
            if (_isSaving) return;
            if (KeyHistory == null) return;
            _isSaving = true;
            try
            {
                var localFolder = ApplicationData.Current.LocalFolder;
                var historyFile = await localFolder.CreateFileAsync(HistoryFileName, CreationCollisionOption.ReplaceExisting);
                if (historyFile != null)
                {
                    var sortedHistory = KeyHistory.OrderByDescending(i => i.Timestamp).ToList();
                    var json = System.Text.Json.JsonSerializer.Serialize(sortedHistory);
                    await FileIO.WriteTextAsync(historyFile, json);
                }
            }
            catch { }
            finally
            {
                _isSaving = false;
            }
        }

        private async void GenerateButton_Click(object sender, RoutedEventArgs e)
        {
            SaveUserSettings();
            try
            {
                int length = (int)LengthNumberBox.Value;
                if (length < 4 || length > 999)
                {
                    ShowError("无效长度", "密钥长度必须在4到999之间");
                    return;
                }
                var charSet = new StringBuilder();
                if (UpperCaseCheck.IsChecked ?? false) charSet.Append("ABCDEFGHIJKLMNOPQRSTUVWXYZ");
                if (LowerCaseCheck.IsChecked ?? false) charSet.Append("abcdefghijklmnopqrstuvwxyz");
                if (NumberCheck.IsChecked ?? false) charSet.Append("0123456789");
                if (SymbolCheck.IsChecked ?? false) charSet.Append("!@#$%^&*()_-+={}[]|\\:;\"'<>,.?/");
                if (charSet.Length == 0)
                {
                    ShowError("无效设置", "请至少选择一种字符类型");
                    return;
                }
                string chars = charSet.ToString();
                var key = new StringBuilder(length);
                using (var rng = RandomNumberGenerator.Create())
                {
                    byte[] randomBytes = new byte[length];
                    rng.GetBytes(randomBytes);
                    for (int i = 0; i < length; i++)
                    {
                        int index = randomBytes[i] % chars.Length;
                        key.Append(chars[index]);
                    }
                }
                await ShowKeyDialog(key.ToString());
                KeyHistory.Insert(0, new KeyHistoryItem(key.ToString()));
                await SaveKeyHistoryAsync();
            }
            catch (Exception ex)
            {
                ShowError("生成失败", $"生成密钥时发生错误: {ex.Message}");
            }
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

        private async Task ShowKeyDialog(string key)
        {
            if (string.IsNullOrEmpty(key))
            {
                ShowError("无效密钥", "生成的密钥为空");
                return;
            }
            var formattedKey = new StringBuilder();
            for (int i = 0; i < key.Length; i++)
            {
                formattedKey.Append(key[i]);
                if ((i + 1) % 4 == 0 && i != key.Length - 1) formattedKey.Append(" ");
            }
            var stackPanel = new StackPanel() { Spacing = 12 };
            var keyBorder = new Border()
            {
                Padding = new Thickness(12),
                Margin = new Thickness(0, 0, 0, 12),
                CornerRadius = new CornerRadius(4)
            };
            if (Application.Current.Resources.TryGetValue("CardBackgroundFillColorDefaultBrush", out object cardBrushObj) && cardBrushObj is Brush cardBrush)
            {
                keyBorder.Background = cardBrush;
            }
            var keyText = new TextBlock()
            {
                Text = formattedKey.ToString(),
                FontFamily = new FontFamily("Consolas"),
                FontSize = 24,
                TextAlignment = TextAlignment.Center
            };
            if (Application.Current.Resources.TryGetValue("TextFillColorPrimaryBrush", out object textBrushObj) && textBrushObj is Brush textBrush)
            {
                keyText.Foreground = textBrush;
            }
            keyBorder.Child = keyText;
            int upper = key.Count(c => char.IsUpper(c));
            int lower = key.Count(c => char.IsLower(c));
            int digits = key.Count(char.IsDigit);
            int symbols = key.Count(c => !char.IsLetterOrDigit(c));
            var statsPanel = new StackPanel()
            {
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Center,
                Spacing = 16
            };
            var statsTextStyle = new Style(typeof(TextBlock));
            if (Application.Current.Resources.TryGetValue("TextFillColorSecondaryBrush", out object secondaryBrushObj) && secondaryBrushObj is Brush secondaryBrush)
            {
                statsTextStyle.Setters.Add(new Setter(TextBlock.ForegroundProperty, secondaryBrush));
            }
            statsPanel.Children.Add(CreateStatsTextBlock($"长度: {key.Length}", statsTextStyle));
            statsPanel.Children.Add(CreateStatsTextBlock($"大写: {upper}", statsTextStyle));
            statsPanel.Children.Add(CreateStatsTextBlock($"小写: {lower}", statsTextStyle));
            statsPanel.Children.Add(CreateStatsTextBlock($"数字: {digits}", statsTextStyle));
            statsPanel.Children.Add(CreateStatsTextBlock($"符号: {symbols}", statsTextStyle));
            stackPanel.Children.Add(keyBorder);
            stackPanel.Children.Add(statsPanel);
            var dialog = new ContentDialog
            {
                Title = "生成的密钥",
                Content = stackPanel,
                PrimaryButtonText = "复制",
                CloseButtonText = "关闭",
                DefaultButton = ContentDialogButton.Primary,
                XamlRoot = this.Content.XamlRoot
            };
            var result = await dialog.ShowAsync();
            if (result == ContentDialogResult.Primary)
            {
                await CopyToClipboard(key);
            }
        }

        private TextBlock CreateStatsTextBlock(string text, Style style)
        {
            return new TextBlock
            {
                Text = text,
                Style = style,
                Margin = new Thickness(0, 0, 8, 0)
            };
        }

        private async void CopyHistoryItem_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button button && button.Tag is string key)
            {
                await CopyToClipboard(key);
            }
        }

        private async void ViewHistoryItem_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button button && button.Tag is KeyHistoryItem item)
            {
                await ShowKeyDialog(item.FullKey);
            }
        }

        private async void ClearHistoryButton_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new ContentDialog
            {
                Title = "确认清空",
                Content = "确定要清空所有密钥历史记录吗？此操作不可恢复。",
                PrimaryButtonText = "确定",
                CloseButtonText = "取消",
                DefaultButton = ContentDialogButton.Primary,
                XamlRoot = this.Content.XamlRoot
            };
            var result = await dialog.ShowAsync();
            if (result == ContentDialogResult.Primary)
            {
                KeyHistory.Clear();
                await SaveKeyHistoryAsync();
            }
        }

        private async Task CopyToClipboard(string text)
        {
            if (string.IsNullOrEmpty(text))
            {
                ShowError("复制失败", "没有内容可复制");
                return;
            }
            try
            {
                var package = new Windows.ApplicationModel.DataTransfer.DataPackage();
                package.SetText(text);
                Windows.ApplicationModel.DataTransfer.Clipboard.SetContent(package);
                var messageDialog = new ContentDialog
                {
                    Title = "复制成功",
                    Content = "密钥已复制到剪贴板",
                    CloseButtonText = "确定",
                    DefaultButton = ContentDialogButton.Close,
                    XamlRoot = this.Content.XamlRoot
                };
                await messageDialog.ShowAsync();
            }
            catch (Exception ex)
            {
                ShowError("复制失败", $"复制过程中出错: {ex.Message}");
            }
        }

        private async void SelectSaveLocationButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var savePicker = new FileSavePicker();
                InitializeWithWindow.Initialize(savePicker, _windowHandle);
                savePicker.SuggestedStartLocation = PickerLocationId.Downloads;
                savePicker.FileTypeChoices.Add("所有文件", new List<string>() { ".*" });

                _saveFile = await savePicker.PickSaveFileAsync();
                if (_saveFile != null)
                {
                    SaveLocationText.Text = _saveFile.Path;
                }
            }
            catch (Exception ex)
            {
                ShowError("保存位置错误", $"选择保存位置失败: {ex.Message}");
            }
        }

        private async void StartDownloadButton_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(DownloadUrlTextBox.Text))
            {
                ShowError("输入错误", "请输入下载URL");
                return;
            }

            if (_saveFile == null)
            {
                ShowError("保存位置错误", "请先选择文件保存位置");
                return;
            }

            int threadCount = (int)ThreadCountBox.Value;
            if (threadCount < 1 || threadCount > 32)
            {
                ShowError("参数错误", "线程数必须在1-32之间");
                return;
            }

            _downloadCts = new CancellationTokenSource();
            _isDownloadPaused = false;
            _downloadStopwatch = Stopwatch.StartNew();
            _totalBytesDownloaded = 0;

            StartDownloadButton.IsEnabled = false;
            PauseDownloadButton.IsEnabled = true;
            CancelDownloadButton.IsEnabled = true;
            DownloadProgressBar.Value = 0;
            ProgressText.Text = "正在初始化下载...";
            DownloadSpeedText.Text = "";

            try
            {
                using (var response = await _httpClient.GetAsync(DownloadUrlTextBox.Text, HttpCompletionOption.ResponseHeadersRead))
                {
                    if (!response.IsSuccessStatusCode)
                    {
                        ShowError("下载错误", $"服务器返回错误: {response.StatusCode}");
                        ResetDownloadState();
                        return;
                    }

                    if (!response.Content.Headers.ContentLength.HasValue)
                    {
                        ShowError("下载错误", "无法获取文件大小");
                        ResetDownloadState();
                        return;
                    }

                    _totalFileSize = response.Content.Headers.ContentLength.Value;
                    ProgressText.Text = $"准备下载 ({FormatFileSize(_totalFileSize)})";
                }

                var tempFolder = await ApplicationData.Current.TemporaryFolder.CreateFolderAsync("Downloads", CreationCollisionOption.OpenIfExists);
                var downloadTasks = new List<Task>();
                var partSize = _totalFileSize / threadCount;
                var downloadParts = new List<DownloadPart>();

                for (int i = 0; i < threadCount; i++)
                {
                    long start = i * partSize;
                    long end = (i == threadCount - 1) ? _totalFileSize - 1 : (i + 1) * partSize - 1;
                    var part = new DownloadPart { Start = start, End = end };
                    downloadParts.Add(part);
                    downloadTasks.Add(DownloadFilePart(DownloadUrlTextBox.Text, tempFolder, i, part, _downloadCts.Token));
                }

                await Task.WhenAll(downloadTasks);

                if (!_downloadCts.Token.IsCancellationRequested && !_isDownloadPaused)
                {
                    ProgressText.Text = "正在合并文件...";
                    await MergeFiles(tempFolder, _saveFile, threadCount);
                    ProgressText.Text = "下载完成!";
                    ShowNotification("下载成功", $"文件已保存到: {_saveFile.Path}");
                }
            }
            catch (OperationCanceledException)
            {
                ProgressText.Text = "下载已取消";
            }
            catch (Exception ex)
            {
                ShowError("下载错误", $"下载失败: {ex.Message}");
            }
            finally
            {
                ResetDownloadState();
            }
        }

        private async Task DownloadFilePart(string url, StorageFolder tempFolder, int partIndex, DownloadPart part, CancellationToken token)
        {
            try
            {
                var request = new HttpRequestMessage(HttpMethod.Get, url);
                request.Headers.Range = new RangeHeaderValue(part.Start, part.End);

                using (var response = await _httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, token))
                {
                    response.EnsureSuccessStatusCode();

                    using (var stream = await response.Content.ReadAsStreamAsync())
                    using (var fileStream = await (await tempFolder.CreateFileAsync($"part_{partIndex}.tmp", CreationCollisionOption.ReplaceExisting)).OpenStreamForWriteAsync())
                    {
                        var buffer = new byte[8192];
                        int bytesRead;

                        while ((bytesRead = await stream.ReadAsync(buffer, 0, buffer.Length, token)) > 0)
                        {
                            token.ThrowIfCancellationRequested();

                            while (_isDownloadPaused)
                            {
                                await Task.Delay(500, token);
                                token.ThrowIfCancellationRequested();
                            }

                            await fileStream.WriteAsync(buffer, 0, bytesRead, token);

                            Interlocked.Add(ref _totalBytesDownloaded, bytesRead);
                            part.Downloaded += bytesRead;

                            if (_downloadStopwatch.ElapsedMilliseconds > 200)
                            {
                                await Dispatcher.RunAsync(Windows.UI.Core.CoreDispatcherPriority.Normal, () =>
                                {
                                    double progress = (_totalBytesDownloaded / (double)_totalFileSize) * 100;
                                    DownloadProgressBar.Value = progress;

                                    double elapsedSeconds = _downloadStopwatch.Elapsed.TotalSeconds;
                                    double speed = elapsedSeconds > 0 ? _totalBytesDownloaded / elapsedSeconds : 0;
                                    DownloadSpeedText.Text = $"{FormatFileSize((long)speed)}/s";

                                    ProgressText.Text = $"{progress:F1}% ({FormatFileSize(_totalBytesDownloaded)} / {FormatFileSize(_totalFileSize)})";
                                });
                                _downloadStopwatch.Restart();
                            }
                        }
                    }
                }
            }
            catch
            {
                throw;
            }
        }

        private async Task MergeFiles(StorageFolder tempFolder, StorageFile outputFile, int partsCount)
        {
            using (var outputStream = await outputFile.OpenStreamForWriteAsync())
            {
                for (int i = 0; i < partsCount; i++)
                {
                    var partFile = await tempFolder.GetFileAsync($"part_{i}.tmp");
                    using (var partStream = await partFile.OpenStreamForReadAsync())
                    {
                        await partStream.CopyToAsync(outputStream);
                    }
                    await partFile.DeleteAsync();
                }
            }
        }

        private void ResetDownloadState()
        {
            StartDownloadButton.IsEnabled = true;
            PauseDownloadButton.IsEnabled = false;
            CancelDownloadButton.IsEnabled = false;
            PauseDownloadButton.Content = "暂停";
        }

        private string FormatFileSize(long bytes)
        {
            string[] sizes = { "B", "KB", "MB", "GB", "TB" };
            int order = 0;
            double len = bytes;

            while (len >= 1024 && order < sizes.Length - 1)
            {
                order++;
                len /= 1024;
            }

            return $"{len:0.##} {sizes[order]}";
        }

        private async void PauseDownloadButton_Click(object sender, RoutedEventArgs e)
        {
            _isDownloadPaused = !_isDownloadPaused;
            PauseDownloadButton.Content = _isDownloadPaused ? "继续" : "暂停";
            ProgressText.Text = _isDownloadPaused ? "已暂停" : "正在下载...";
        }

        private void CancelDownloadButton_Click(object sender, RoutedEventArgs e)
        {
            _downloadCts?.Cancel();
            ProgressText.Text = "正在取消...";
        }

        private async void ShowNotification(string title, string message)
        {
            ContentDialog notificationDialog = new ContentDialog
            {
                Title = title,
                Content = message,
                CloseButtonText = "确定",
                DefaultButton = ContentDialogButton.Close,
                XamlRoot = this.Content.XamlRoot
            };
            await notificationDialog.ShowAsync();
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