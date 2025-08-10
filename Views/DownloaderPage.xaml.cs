using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using MonoTorrent;
using MonoTorrent.Client;
using Windows.ApplicationModel;
using Windows.ApplicationModel.DataTransfer;
using Windows.Storage;
using Windows.Storage.AccessCache;
using Windows.Storage.Pickers;
using Windows.System;

namespace REToolBox.Views
{
    public sealed partial class DownloaderPage : Page
    {
        public ObservableCollection<DownloadItem> DownloadItems { get; } = new ObservableCollection<DownloadItem>();
        private StorageFolder? _downloadFolder;
        private ClientEngine? _engine;
        private bool _engineInitialized = false;
        public DownloadSettings DownloadSettings { get; set; } = new DownloadSettings();

        public DownloaderPage()
        {
            this.InitializeComponent();
            LoadSettings();
            InitializeDownloadFolder();
            _ = InitializeBtEngineAsync();
        }

        private void LoadSettings()
        {
            var localSettings = ApplicationData.Current.LocalSettings;
            if (localSettings.Values.ContainsKey("ThreadCount"))
                DownloadSettings.ThreadCount = (int)localSettings.Values["ThreadCount"];
            if (localSettings.Values.ContainsKey("DiskCacheMB"))
                DownloadSettings.DiskCacheMB = (int)localSettings.Values["DiskCacheMB"];
            if (localSettings.Values.ContainsKey("ShowSpeed"))
                DownloadSettings.ShowSpeed = (bool)localSettings.Values["ShowSpeed"];
        }

        private void SaveSettings()
        {
            var localSettings = ApplicationData.Current.LocalSettings;
            localSettings.Values["ThreadCount"] = DownloadSettings.ThreadCount;
            localSettings.Values["DiskCacheMB"] = DownloadSettings.DiskCacheMB;
            localSettings.Values["ShowSpeed"] = DownloadSettings.ShowSpeed;
        }

        private async void InitializeDownloadFolder()
        {
            try
            {
                _downloadFolder = await StorageFolder.GetFolderFromPathAsync(System.Environment.GetFolderPath(System.Environment.SpecialFolder.UserProfile) + "\\Downloads");
            }
            catch
            {
                _downloadFolder = ApplicationData.Current.TemporaryFolder;
            }
            if (_downloadFolder != null)
            {
                DownloadPathTextBlock.Text = $"下载位置: {_downloadFolder.Path}";
            }
        }

        private async Task InitializeBtEngineAsync()
        {
            try
            {
                var localFolder = ApplicationData.Current.LocalFolder;
                var engineFolder = await localFolder.CreateFolderAsync("BTDownloads", CreationCollisionOption.OpenIfExists);
                var settingsBuilder = new EngineSettingsBuilder
                {
                    AllowPortForwarding = true,
                    ListenEndPoints = new System.Collections.Generic.Dictionary<string, System.Net.IPEndPoint>
                    {
                        { "ipv4", new System.Net.IPEndPoint(System.Net.IPAddress.Any, 55555) }
                    },
                    CacheDirectory = engineFolder.Path
                };
                settingsBuilder.MaximumConnections = DownloadSettings.ThreadCount * 10;
                var settings = settingsBuilder.ToSettings();
                _engine = new ClientEngine(settings);
                _engineInitialized = true;
            }
            catch (Exception)
            {
                try
                {
                    var settingsBuilder = new EngineSettingsBuilder();
                    settingsBuilder.MaximumConnections = DownloadSettings.ThreadCount * 10;
                    var settings = settingsBuilder.ToSettings();
                    _engine = new ClientEngine(settings);
                    _engineInitialized = true;
                }
                catch
                {
                    _engine = null;
                    _engineInitialized = false;
                }
            }
        }

        private async void AddDownloadButton_Click(object sender, RoutedEventArgs e)
        {
            string url = UrlTextBox.Text.Trim();
            if (string.IsNullOrEmpty(url))
            {
                ShowInfoBar("请输入下载链接", InfoBarSeverity.Error);
                return;
            }
            if (_downloadFolder == null)
            {
                ShowInfoBar("下载文件夹未初始化", InfoBarSeverity.Error);
                return;
            }
            try
            {
                var downloadItem = new DownloadItem
                {
                    Url = url,
                    FileName = GetFileNameFromUrl(url),
                    Progress = 0,
                    CancellationTokenSource = new CancellationTokenSource(),
                    DownloadFolder = _downloadFolder
                };
                DownloadItems.Add(downloadItem);
                await StartDownloadAsync(downloadItem);
            }
            catch (Exception ex)
            {
                ShowInfoBar($"添加下载失败: {ex.Message}", InfoBarSeverity.Error);
            }
        }

        private async Task StartDownloadAsync(DownloadItem item)
        {
            try
            {
                if (item.Url.StartsWith("magnet:") || item.Url.EndsWith(".torrent", StringComparison.OrdinalIgnoreCase))
                {
                    await StartTorrentDownloadAsync(item);
                }
                else if (item.Url.StartsWith("http://") || item.Url.StartsWith("https://") ||
                         item.Url.StartsWith("ftp://") || item.Url.StartsWith("sftp://") ||
                         item.Url.StartsWith("thunder://"))
                {
                    await StartHttpDownloadAsync(item);
                }
                else
                {
                    await StartHttpDownloadAsync(item);
                }
            }
            catch (Exception ex)
            {
                item.Status = $"下载失败: {ex.Message}";
                ShowInfoBar($"下载失败: {ex.Message}", InfoBarSeverity.Error);
            }
        }

        private async Task StartHttpDownloadAsync(DownloadItem item)
        {
            try
            {
                using var httpClient = new HttpClient();
                httpClient.DefaultRequestHeaders.Add("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36");
                var response = await httpClient.GetAsync(item.Url, HttpCompletionOption.ResponseHeadersRead);
                if (!response.IsSuccessStatusCode)
                {
                    item.Status = $"下载失败: {response.StatusCode} ({response.ReasonPhrase})";
                    ShowInfoBar($"下载失败: {response.StatusCode} ({response.ReasonPhrase})", InfoBarSeverity.Error);
                    return;
                }
                var totalBytes = response.Content.Headers.ContentLength ?? -1;
                var fileName = item.FileName;
                var file = await item.DownloadFolder.CreateFileAsync(fileName, CreationCollisionOption.GenerateUniqueName);
                item.DownloadedFile = file;
                using var fileStream = await file.OpenStreamForWriteAsync();
                using var contentStream = await response.Content.ReadAsStreamAsync();
                var buffer = new byte[8192];
                long totalBytesRead = 0;
                int bytesRead;
                var lastUpdate = DateTime.Now;
                while ((bytesRead = await contentStream.ReadAsync(buffer, 0, buffer.Length, item.CancellationTokenSource.Token)) > 0)
                {
                    await fileStream.WriteAsync(buffer, 0, bytesRead, item.CancellationTokenSource.Token);
                    totalBytesRead += bytesRead;
                    if (totalBytes > 0)
                    {
                        item.Progress = (int)((double)totalBytesRead / totalBytes * 100);
                    }
                    if (DownloadSettings.ShowSpeed && DateTime.Now.Subtract(lastUpdate).TotalSeconds >= 1)
                    {
                        var speed = BytesToString(bytesRead) + "/s";
                        item.Status = $"下载中... {item.Progress}% - {speed}";
                        lastUpdate = DateTime.Now;
                    }
                    else if (!DownloadSettings.ShowSpeed)
                    {
                        item.Status = $"下载中... {item.Progress}%";
                    }
                }
                item.Status = "下载完成";
                item.IsCompleted = true;
                ShowInfoBar($"下载完成: {fileName}", InfoBarSeverity.Success);
            }
            catch (OperationCanceledException)
            {
                item.Status = "已取消";
                ShowInfoBar($"下载已取消: {item.FileName}", InfoBarSeverity.Informational);
            }
            catch (Exception ex)
            {
                item.Status = $"下载失败: {ex.Message}";
                ShowInfoBar($"下载失败: {ex.Message}", InfoBarSeverity.Error);
            }
        }

        private async Task StartTorrentDownloadAsync(DownloadItem item)
        {
            try
            {
                if (_engine == null || !_engineInitialized)
                {
                    await InitializeBtEngineAsync();
                    if (_engine == null || !_engineInitialized)
                    {
                        throw new Exception("BT引擎初始化失败，请确保已安装MonoTorrent库");
                    }
                }
                Torrent? torrent = null;
                TorrentManager? manager = null;
                if (item.Url.EndsWith(".torrent", StringComparison.OrdinalIgnoreCase))
                {
                    var file = await StorageFile.GetFileFromPathAsync(item.Url);
                    var buffer = await FileIO.ReadBufferAsync(file);
                    var bytes = buffer.ToArray();
                    torrent = Torrent.Load(bytes);
                    item.FileName = torrent.Name;
                }
                else if (item.Url.StartsWith("magnet:"))
                {
                    item.Status = "正在解析磁力链接...";
                    var magnetLink = MagnetLink.FromUri(new Uri(item.Url));
                    manager = await _engine.AddAsync(magnetLink, item.DownloadFolder.Path);
                    item.FileName = manager.Torrent?.Name ?? "BT下载";
                }
                if (torrent != null)
                {
                    manager = await _engine.AddAsync(torrent, item.DownloadFolder.Path);
                }
                if (manager == null)
                {
                    throw new Exception("无法创建BT下载任务");
                }
                item.TorrentManager = manager;
                item.Status = "正在连接BT网络...";
                await manager.StartAsync();
                await MonitorTorrentDownloadAsync(item, manager);
            }
            catch (Exception ex)
            {
                item.Status = $"BT下载失败: {ex.Message}";
                ShowInfoBar($"BT下载失败: {ex.Message}", InfoBarSeverity.Error);
            }
        }

        private async Task MonitorTorrentDownloadAsync(DownloadItem item, TorrentManager manager)
        {
            try
            {
                var lastUpdate = DateTime.Now;
                while (manager.State != TorrentState.Seeding &&
                       manager.State != TorrentState.Stopped &&
                       manager.State != TorrentState.Error &&
                       !item.CancellationTokenSource.Token.IsCancellationRequested)
                {
                    await Task.Delay(1000);
                    var progress = (int)(manager.Progress * 100);
                    item.Progress = progress;
                    if (DownloadSettings.ShowSpeed && DateTime.Now.Subtract(lastUpdate).TotalSeconds >= 1)
                    {
                        item.Status = $"下载中 {progress}% - ↑{BytesToString(manager.Monitor.UploadRate)}/s ↓{BytesToString(manager.Monitor.DownloadRate)}/s";
                        lastUpdate = DateTime.Now;
                    }
                    else if (!DownloadSettings.ShowSpeed)
                    {
                        item.Status = $"BT下载中... {progress}% ({manager.State})";
                    }
                    else
                    {
                        item.Status = $"BT下载中... {progress}% ({manager.State})";
                    }
                }
                if (item.CancellationTokenSource.Token.IsCancellationRequested)
                {
                    await manager.StopAsync();
                    item.Status = "BT下载已取消";
                    ShowInfoBar($"BT下载已取消: {item.FileName}", InfoBarSeverity.Informational);
                }
                else if (manager.State == TorrentState.Error)
                {
                    item.Status = $"BT下载错误: {manager.Error?.ToString() ?? "未知错误"}";
                    ShowInfoBar($"BT下载错误: {manager.Error?.ToString() ?? "未知错误"}", InfoBarSeverity.Error);
                }
                else
                {
                    item.Status = "BT下载完成";
                    item.IsCompleted = true;
                    item.DownloadedFile = await GetDownloadedFileAsync(item.FileName);
                    ShowInfoBar($"BT下载完成: {item.FileName}", InfoBarSeverity.Success);
                }
            }
            catch (Exception ex)
            {
                item.Status = $"监控BT下载失败: {ex.Message}";
                ShowInfoBar($"监控BT下载失败: {ex.Message}", InfoBarSeverity.Error);
            }
        }

        private async Task<StorageFile?> GetDownloadedFileAsync(string fileName)
        {
            try
            {
                if (_downloadFolder == null) return null;
                var files = await _downloadFolder.GetFilesAsync();
                foreach (var file in files)
                {
                    if (file.Name.Contains(fileName) || fileName.Contains(file.Name.Replace(".torrent", "")))
                    {
                        return file;
                    }
                }
                return null;
            }
            catch
            {
                return null;
            }
        }

        private string BytesToString(long byteCount)
        {
            string[] suf = { "B", "KB", "MB", "GB", "TB" };
            if (byteCount == 0)
                return "0" + suf[0];
            long bytes = Math.Abs(byteCount);
            int place = Convert.ToInt32(Math.Floor(Math.Log(bytes, 1024)));
            double num = Math.Round(bytes / Math.Pow(1024, place), 1);
            return (Math.Sign(byteCount) * num).ToString() + suf[place];
        }

        private string BytesToString(int byteCount)
        {
            return BytesToString((long)byteCount);
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            var button = sender as Button;
            var item = button?.Tag as DownloadItem;
            if (item != null && item.CancellationTokenSource != null)
            {
                item.CancellationTokenSource.Cancel();
                item.Status = "已取消";
                if (item.TorrentManager != null)
                {
                    _ = item.TorrentManager.StopAsync();
                }
            }
        }

        private async void SelectFolderButton_Click(object sender, RoutedEventArgs e)
        {
            var folderPicker = new FolderPicker();
            var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(App.MainWindow);
            WinRT.Interop.InitializeWithWindow.Initialize(folderPicker, hwnd);
            folderPicker.SuggestedStartLocation = PickerLocationId.Downloads;
            folderPicker.FileTypeFilter.Add("*");
            var folder = await folderPicker.PickSingleFolderAsync();
            if (folder != null)
            {
                _downloadFolder = folder;
                DownloadPathTextBlock.Text = $"下载位置: {folder.Path}";
                ShowInfoBar($"已选择下载位置: {folder.Path}", InfoBarSeverity.Success);
            }
        }

        private async void OpenFolderButton_Click(object sender, RoutedEventArgs e)
        {
            if (_downloadFolder == null) return;
            try
            {
                await Launcher.LaunchFolderAsync(_downloadFolder);
            }
            catch (Exception ex)
            {
                ShowInfoBar($"打开文件夹失败: {ex.Message}", InfoBarSeverity.Error);
            }
        }

        private async void OpenFileButton_Click(object sender, RoutedEventArgs e)
        {
            var button = sender as Button;
            var item = button?.Tag as DownloadItem;
            if (item != null && item.DownloadedFile != null)
            {
                try
                {
                    await Launcher.LaunchFileAsync(item.DownloadedFile);
                }
                catch (Exception ex)
                {
                    ShowInfoBar($"打开文件失败: {ex.Message}", InfoBarSeverity.Error);
                }
            }
        }

        private async void SettingsButton_Click(object sender, RoutedEventArgs e)
        {
            await SettingsDialog.ShowAsync();
        }

        private void SettingsDialog_PrimaryButtonClick(ContentDialog sender, ContentDialogButtonClickEventArgs args)
        {
            SaveSettings();
            ShowInfoBar("设置已保存", InfoBarSeverity.Success);
        }

        private string GetFileNameFromUrl(string url)
        {
            try
            {
                if (url.StartsWith("magnet:"))
                {
                    var magnetLink = MagnetLink.FromUri(new Uri(url));
                    return !string.IsNullOrEmpty(magnetLink.Name) ? magnetLink.Name : "magnet_download";
                }
                var uri = new Uri(url);
                var fileName = Path.GetFileName(uri.LocalPath);
                return string.IsNullOrEmpty(fileName) ? "download" : fileName;
            }
            catch
            {
                return "download";
            }
        }

        private void ShowInfoBar(string message, InfoBarSeverity severity)
        {
            DownloadInfoBar.Message = message;
            DownloadInfoBar.Severity = severity;
            DownloadInfoBar.IsOpen = true;
            var timer = new DispatcherTimer();
            timer.Interval = TimeSpan.FromSeconds(3);
            timer.Tick += (s, e) => {
                DownloadInfoBar.IsOpen = false;
                timer.Stop();
            };
            timer.Start();
        }

        private void Page_DragOver(object sender, DragEventArgs e)
        {
            e.AcceptedOperation = DataPackageOperation.Copy;
        }

        private async void Page_Drop(object sender, DragEventArgs e)
        {
            if (e.DataView.Contains(StandardDataFormats.StorageItems))
            {
                var items = await e.DataView.GetStorageItemsAsync();
                foreach (var storageItem in items)
                {
                    if (storageItem is StorageFile file)
                    {
                        if (file.FileType.Equals(".torrent", StringComparison.OrdinalIgnoreCase))
                        {
                            await ProcessTorrentFileAsync(file);
                        }
                    }
                }
            }
        }

        private async Task ProcessTorrentFileAsync(StorageFile torrentFile)
        {
            if (_downloadFolder == null) return;
            try
            {
                var fileName = torrentFile.Name.Replace(".torrent", "");
                var downloadItem = new DownloadItem
                {
                    Url = torrentFile.Path,
                    FileName = fileName,
                    Progress = 0,
                    CancellationTokenSource = new CancellationTokenSource(),
                    DownloadFolder = _downloadFolder,
                    IsTorrentFile = true
                };
                DownloadItems.Add(downloadItem);
                ShowInfoBar($"已添加种子文件: {fileName}", InfoBarSeverity.Success);
                await ParseTorrentInfoAsync(downloadItem, torrentFile);
            }
            catch (Exception ex)
            {
                ShowInfoBar($"处理种子文件失败: {ex.Message}", InfoBarSeverity.Error);
            }
        }

        private async Task ParseTorrentInfoAsync(DownloadItem item, StorageFile torrentFile)
        {
            try
            {
                var buffer = await FileIO.ReadBufferAsync(torrentFile);
                var bytes = buffer.ToArray();
                var torrent = Torrent.Load(bytes);
                item.FileName = torrent.Name;
                item.Status = $"种子信息: {torrent.Files.Count}个文件, 总大小: {BytesToString(torrent.Size)}";
                item.Progress = 100;
                ShowInfoBar($"种子文件解析完成: {item.FileName}", InfoBarSeverity.Success);
            }
            catch (Exception ex)
            {
                item.Status = $"解析失败: {ex.Message}";
                ShowInfoBar($"种子文件解析失败: {ex.Message}", InfoBarSeverity.Error);
            }
        }

        protected override async void OnNavigatedFrom(NavigationEventArgs e)
        {
            foreach (var item in DownloadItems)
            {
                if (item.TorrentManager != null)
                {
                    try
                    {
                        await item.TorrentManager.StopAsync();
                    }
                    catch { }
                }
            }
            if (_engine != null && _engineInitialized)
            {
                try
                {
                    await _engine.StopAllAsync();
                }
                catch { }
            }
            base.OnNavigatedFrom(e);
        }
    }

    public class DownloadItem : INotifyPropertyChanged
    {
        private string _url = string.Empty;
        private string _fileName = string.Empty;
        private int _progress;
        private string _status = string.Empty;
        private CancellationTokenSource _cancellationTokenSource = new();
        private StorageFolder _downloadFolder;
        private bool _isTorrentFile;
        private StorageFile? _downloadedFile;
        private bool _isCompleted;
        private TorrentManager? _torrentManager;

        public string Url
        {
            get => _url;
            set
            {
                _url = value; OnPropertyChanged();
            }
        }

        public string FileName
        {
            get => _fileName;
            set
            {
                _fileName = value; OnPropertyChanged();
            }
        }

        public int Progress
        {
            get => _progress;
            set
            {
                _progress = value; OnPropertyChanged();
            }
        }

        public string Status
        {
            get => _status;
            set
            {
                _status = value; OnPropertyChanged();
            }
        }

        public CancellationTokenSource CancellationTokenSource
        {
            get => _cancellationTokenSource;
            set
            {
                _cancellationTokenSource = value; OnPropertyChanged();
            }
        }

        public StorageFolder DownloadFolder
        {
            get => _downloadFolder;
            set
            {
                _downloadFolder = value; OnPropertyChanged();
            }
        }

        public bool IsTorrentFile
        {
            get => _isTorrentFile;
            set
            {
                _isTorrentFile = value; OnPropertyChanged();
            }
        }

        public StorageFile? DownloadedFile
        {
            get => _downloadedFile;
            set
            {
                _downloadedFile = value; OnPropertyChanged();
            }
        }

        public bool IsCompleted
        {
            get => _isCompleted;
            set
            {
                _isCompleted = value; OnPropertyChanged();
            }
        }

        public TorrentManager? TorrentManager
        {
            get => _torrentManager;
            set
            {
                _torrentManager = value; OnPropertyChanged();
            }
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        protected void OnPropertyChanged([CallerMemberName] string? name = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }
    }

    public class DownloadSettings : INotifyPropertyChanged
    {
        private int _threadCount = 8;
        private int _diskCacheMB = 32;
        private bool _showSpeed = true;

        public int ThreadCount
        {
            get => _threadCount;
            set
            {
                _threadCount = value; OnPropertyChanged();
            }
        }

        public int DiskCacheMB
        {
            get => _diskCacheMB;
            set
            {
                _diskCacheMB = value; OnPropertyChanged();
            }
        }

        public bool ShowSpeed
        {
            get => _showSpeed;
            set
            {
                _showSpeed = value; OnPropertyChanged();
            }
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        protected void OnPropertyChanged([CallerMemberName] string? name = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }
    }
}