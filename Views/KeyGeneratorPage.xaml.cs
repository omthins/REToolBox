using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Text;
using Windows.Storage;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Security.Cryptography;
using Windows.ApplicationModel.DataTransfer;

namespace REToolBox.Views
{
    public sealed partial class KeyGeneratorPage : Page
    {
        private const string SettingsKey_UpperCase = "UpperCaseSetting";
        private const string SettingsKey_LowerCase = "LowerCaseSetting";
        private const string SettingsKey_Numbers = "NumbersSetting";
        private const string SettingsKey_Symbols = "SymbolsSetting";
        private const string SettingsKey_Length = "LengthSetting";
        private bool _isLoading = true;
        private bool _isSaving = false;
        private const string HistoryFileName = "SaveKeyHistory.json";

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

        public ObservableCollection<KeyHistoryItem> KeyHistory { get; } = new ObservableCollection<KeyHistoryItem>();

        public KeyGeneratorPage()
        {
            this.InitializeComponent();
            this.Loaded += OnPageLoaded;
            this.Unloaded += OnPageUnloaded;
        }

        private void OnPageLoaded(object sender, RoutedEventArgs e)
        {
            _isLoading = true;
            KeyHistoryList.ItemsSource = KeyHistory;
            LoadUserSettings();
            LoadKeyHistory();
            _isLoading = false;
        }

        private async void OnPageUnloaded(object sender, RoutedEventArgs e)
        {
            await SaveKeyHistoryAsync();
            SaveUserSettings();
        }

        private void LoadUserSettings()
        {
            var localSettings = ApplicationData.Current.LocalSettings;
            UpperCaseCheck.IsChecked = localSettings.Values.TryGetValue(SettingsKey_UpperCase, out object upperCase) ? (bool?)upperCase : true;
            LowerCaseCheck.IsChecked = localSettings.Values.TryGetValue(SettingsKey_LowerCase, out object lowerCase) ? (bool?)lowerCase : true;
            NumberCheck.IsChecked = localSettings.Values.TryGetValue(SettingsKey_Numbers, out object numbers) ? (bool?)numbers : true;
            SymbolCheck.IsChecked = localSettings.Values.TryGetValue(SettingsKey_Symbols, out object symbols) ? (bool?)symbols : false;

            if (localSettings.Values.TryGetValue(SettingsKey_Length, out object length))
            {
                if (length is double lenDouble)
                {
                    LengthNumberBox.Value = lenDouble;
                }
                else if (length is int lenInt)
                {
                    LengthNumberBox.Value = lenInt;
                }
                else
                {
                    LengthNumberBox.Value = 12;
                }
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
                    var items = System.Text.Json.JsonSerializer.Deserialize<List<KeyHistoryItem>?>(json);
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
                if (UpperCaseCheck.IsChecked == true) charSet.Append("ABCDEFGHIJKLMNOPQRSTUVWXYZ");
                if (LowerCaseCheck.IsChecked == true) charSet.Append("abcdefghijklmnopqrstuvwxyz");
                if (NumberCheck.IsChecked == true) charSet.Append("0123456789");
                if (SymbolCheck.IsChecked == true) charSet.Append("!@#$%^&*()_-+={}[]|\\:;\"'<>,.?/");

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

                string generatedKey = key.ToString();
                await ShowKeyDialog(generatedKey);
                KeyHistory.Insert(0, new KeyHistoryItem(generatedKey));
                await SaveKeyHistoryAsync();
            }
            catch (Exception ex)
            {
                ShowError("生成失败", $"生成密钥时发生错误: {ex.Message}");
            }
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

            if (Application.Current.Resources.TryGetValue("CardBackgroundFillColorDefaultBrush", out object? cardBrushObj) && cardBrushObj is Brush cardBrush)
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

            if (Application.Current.Resources.TryGetValue("TextFillColorPrimaryBrush", out object? textBrushObj) && textBrushObj is Brush textBrush)
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
            if (Application.Current.Resources.TryGetValue("TextFillColorSecondaryBrush", out object? secondaryBrushObj) && secondaryBrushObj is Brush secondaryBrush)
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
                XamlRoot = this.Content?.XamlRoot
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
                XamlRoot = this.Content?.XamlRoot
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
                var package = new DataPackage();
                package.SetText(text);
                Clipboard.SetContent(package);

                var messageDialog = new ContentDialog
                {
                    Title = "复制成功",
                    Content = "密钥已复制到剪贴板",
                    CloseButtonText = "确定",
                    DefaultButton = ContentDialogButton.Close,
                    XamlRoot = this.Content?.XamlRoot
                };

                await messageDialog.ShowAsync();
            }
            catch (Exception ex)
            {
                ShowError("复制失败", $"复制过程中出错: {ex.Message}");
            }
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