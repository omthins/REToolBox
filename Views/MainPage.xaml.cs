using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Windows.Storage;
using Windows.System;
using Windows.UI;
using Windows.UI.ViewManagement;
using CommunityToolkit.WinUI.UI.Controls;

namespace REToolBox.Views;

public sealed partial class MainPage : Page
{
    public SolidColorBrush ClockColor => IsDarkTheme() ? new SolidColorBrush(Colors.White) : new SolidColorBrush(Colors.Black);
    public SolidColorBrush DateColor => IsDarkTheme() ? new SolidColorBrush(Color.FromArgb(255, 238, 238, 238)) : new SolidColorBrush(Colors.Black);
    private DispatcherTimer _timeUpdater = null!;
    private UISettings _themeMonitor = null!;
    private HttpClient _networkClient = null!;
    private DateTime _lastDateCheck = DateTime.MinValue;
    private string _dailyMessage = "加载中...";
    private static readonly string[] LocalMessages = {
        "看到我请马上去q群里说话",
        "原神，启动",
        "被我推回的每一分钟，倒回的每个存在，总有些秘密还深深藏在 你的脑海",
        "今天也为美好的世界  尽情地干杯",
        "美 好的世界在今天也如此安宁，面对席卷城市的 坏消息 我不知道 装出毫不知情的模样将视线给移开",
        "看着Visual Studio的报错，我心跳不止~（红温了）",
        "我一直都在看哟 看你独处时眼泪往下流 我知道哟",
        "队友，你快开😭😭😭监管者要来了，不！！！对面这是什么变态忍者啊 我再也不玩这个游戏了",
        "第五人格，启动",
        "我就是所谓的失败作吧 不管做什么 即便再怎么努力 也都是徒劳",
        "待って   こんなの聞いてないって   ダーリン？",
        "SNDML团队会一直陪着你 只要你不退出Q群的话",
    };
    private const string AnnouncementCache = "LastNotice";
    private const string NoticeSource = "https://raw.githubusercontent.com/omthins/SNDMLOTeam/main/gg.txt";
    private const string MessageApi = "https://api.codelife.cc/yiyan/random";
    private const string MessageDateKey = "LastMsgDate";
    private const string MessageContentKey = "DailyMsg";
    private bool _isCriticalUpdate = false;
    [DllImport("user32.dll")]
    private static extern int GetDpiForWindow(IntPtr hwnd);
    public MainPage()
    {
        InitializeComponent();
        InitializeUI();
        SetupEventHandlers();
        InitializeServices();
        PrepareDailyMessage();
    }
    private void InitializeUI()
    {
        NotificationDialog.SecondaryButtonText = null;
        NotificationDialog.KeyDown += HandleDialogKeyDown;
        NotificationDialog.Closing += OnDialogClosing;
    }
    private void SetupEventHandlers()
    {
        Loaded += OnPageLoaded;
        Unloaded += OnPageUnloaded;
    }
    private void InitializeServices()
    {
        _themeMonitor = new UISettings();
        _themeMonitor.ColorValuesChanged += HandleThemeChange;
        _networkClient = new HttpClient();
        _networkClient.DefaultRequestHeaders.Add("User-Agent", "REToolBox");
        _networkClient.Timeout = TimeSpan.FromSeconds(2);
    }
    private void PrepareDailyMessage()
    {
        var today = DateTime.Today;
        var settings = ApplicationData.Current.LocalSettings;
        var savedDate = settings.Values[MessageDateKey] as DateTime?;
        if (savedDate.HasValue && savedDate.Value.Date == today)
        {
            _dailyMessage = settings.Values[MessageContentKey] as string ?? LocalMessages[0];
        }
        else
        {
            _dailyMessage = GetRandomMessage();
        }
    }
    private string GetRandomMessage()
    {
        return LocalMessages[new Random().Next(LocalMessages.Length)];
    }
    private void OnPageLoaded(object sender, RoutedEventArgs e)
    {
        ApplyVisualStyle();
        StartTimeTracking();
        CheckForNotifications();
        RefreshDailyMessage();
    }
    private void ApplyVisualStyle()
    {
        try
        {
            if (IsModernWindows())
            {
                VisualRoot.Background = new SolidColorBrush(Colors.Transparent);
            }
            else
            {
                var themeColor = _themeMonitor.GetColorValue(UIColorType.Background);
                var bgColor = themeColor.R == 0 ?
                    Color.FromArgb(255, 30, 30, 30) :
                    Color.FromArgb(255, 230, 230, 230);
                VisualRoot.Background = new SolidColorBrush(bgColor);
                VisualRoot.Opacity = 0.85;
            }
        }
        catch
        {
            VisualRoot.Background = new SolidColorBrush(Color.FromArgb(255, 40, 40, 40));
        }
        UpdateClock();
    }
    private void StartTimeTracking()
    {
        _timeUpdater = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
        _timeUpdater.Tick += (s, args) => UpdateClock();
        _timeUpdater.Start();
        UpdateClock();
    }
    private async void CheckForNotifications()
    {
        try
        {
            string latestNotice = await FetchLatestNotice();
            var settings = ApplicationData.Current.LocalSettings;
            string cachedNotice = settings.Values[AnnouncementCache] as string ?? "";
            if (!string.IsNullOrEmpty(latestNotice) && latestNotice != cachedNotice)
            {
                settings.Values[AnnouncementCache] = latestNotice;
                NoticeMarkdownText.Text = latestNotice.Trim();
                _isCriticalUpdate = latestNotice.Contains("紧急更新");
                NotificationDialog.PrimaryButtonText = _isCriticalUpdate ? "更新" : "确定";
                NotificationDialog.SecondaryButtonText = _isCriticalUpdate ? "退出" : (latestNotice.Contains("新版本") ? "获取更新" : null);
                NotificationDialog.CloseButtonText = "";
                await NotificationDialog.ShowAsync();
            }
        }
        catch { }
    }
    private async Task<string> FetchLatestNotice()
    {
        try
        {
            return await _networkClient.GetStringAsync(NoticeSource);
        }
        catch
        {
            return string.Empty;
        }
    }
    private async void RefreshDailyMessage()
    {
        var today = DateTime.Today;
        if (today == _lastDateCheck) return;
        _lastDateCheck = today;
        var settings = ApplicationData.Current.LocalSettings;
        var savedDate = settings.Values[MessageDateKey] as DateTime?;
        if (savedDate == null || savedDate.Value.Date < today)
        {
            string newMessage = await ObtainDailyMessage();
            settings.Values[MessageDateKey] = today;
            settings.Values[MessageContentKey] = newMessage;
            _dailyMessage = newMessage;
        }
    }
    private async Task<string> ObtainDailyMessage()
    {
        try
        {
            var response = await _networkClient.GetAsync(MessageApi);
            if (response.IsSuccessStatusCode)
            {
                var json = await response.Content.ReadAsStringAsync();
                using var doc = JsonDocument.Parse(json);
                var root = doc.RootElement;
                if (root.TryGetProperty("data", out var data))
                {
                    if (data.TryGetProperty("hitokoto", out var text) &&
                        data.TryGetProperty("from", out var source))
                    {
                        return $"{text.GetString() ?? ""} -{source.GetString() ?? ""}-";
                    }
                }
            }
        }
        catch { }
        return GetRandomMessage();
    }
    private void UpdateClock()
    {
        var currentTime = DateTime.Now;
        ClockDisplay.Text = currentTime.ToString("HH:mm:ss");
        DateDisplay.Text = $"{currentTime:yyyy年MM月dd日 dddd}\n{_dailyMessage}";
    }
    private void HandleThemeChange(UISettings sender, object args)
    {
        DispatcherQueue.TryEnqueue(() =>
        {
            ApplyVisualStyle();
            UpdateClock();
        });
    }
    private bool IsDarkTheme()
    {
        return Application.Current.RequestedTheme == ApplicationTheme.Dark;
    }
    private bool IsModernWindows()
    {
        var osVersion = Environment.OSVersion.Version;
        return osVersion.Major >= 10 && osVersion.Build >= 22000;
    }
    private void OnPageUnloaded(object sender, RoutedEventArgs e)
    {
        _timeUpdater?.Stop();
        _themeMonitor.ColorValuesChanged -= HandleThemeChange;
        _networkClient?.Dispose();
    }
    private void HandleDialogKeyDown(object sender, KeyRoutedEventArgs e)
    {
        if (_isCriticalUpdate && e.Key == VirtualKey.Escape)
        {
            e.Handled = true;
        }
    }
    private void OnDialogClosing(ContentDialog sender, ContentDialogClosingEventArgs args)
    {
        if (_isCriticalUpdate && args.Result == ContentDialogResult.None)
        {
            args.Cancel = true;
        }
    }
    private async void OnPrimaryButtonClicked(ContentDialog sender, ContentDialogButtonClickEventArgs args)
    {
        if (_isCriticalUpdate)
        {
            await Launcher.LaunchUriAsync(new Uri("https://sndml.xyz/"));
        }
    }
    private async void OnSecondaryButtonClicked(ContentDialog sender, ContentDialogButtonClickEventArgs args)
    {
        if (_isCriticalUpdate)
        {
            Application.Current.Exit();
        }
        else if (NotificationDialog.SecondaryButtonText == "获取更新")
        {
            await Launcher.LaunchUriAsync(new Uri("https://sndml.xyz/"));
        }
    }
    private void DevPageButton_Click(object sender, RoutedEventArgs e)
    {
        this.Frame.Navigate(typeof(DevPage));
    }
    private async void NoticeMarkdownText_LinkClicked(object sender, LinkClickedEventArgs e)
    {
        if (Uri.TryCreate(e.Link, UriKind.Absolute, out Uri uri))
        {
            await Launcher.LaunchUriAsync(uri);
        }
    }
}