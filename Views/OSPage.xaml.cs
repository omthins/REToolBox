using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Markup;
using Microsoft.UI.Xaml.Navigation;
using System;
using Windows.System;

namespace REToolBox.Views
{
    public sealed partial class OSPage : Page
    {
        public OSPage()
        {
            this.InitializeComponent();
        }

        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            base.OnNavigatedTo(e);

            // 默认加载 Windows 11 页面
            if (ContentFrame.Content == null)
            {
                ContentFrame.Navigate(typeof(Win11InfoPage));
            }
        }

        private void NavView_SelectionChanged(NavigationView sender, NavigationViewSelectionChangedEventArgs args)
        {
            if (args.IsSettingsSelected)
            {
                // 设置页面（可选）
            }
            else if (args.SelectedItem is NavigationViewItem selectedItem && selectedItem.Tag is string pageName)
            {
                Type pageType = Type.GetType($"REToolBox.Views.{pageName}");
                if (pageType != null)
                {
                    ContentFrame.Navigate(pageType, null, args.RecommendedNavigationTransitionInfo);
                }
            }
        }

        private async void LaunchUrl(string url)
        {
            // 安全检查链接格式
            if (Uri.IsWellFormedUriString(url, UriKind.Absolute))
            {
                await Launcher.LaunchUriAsync(new Uri(url));
                // 隐藏占位符（如果链接有效则不显示）
                DefaultPlaceholder.Visibility = Visibility.Collapsed;
            }
            else
            {
                // 链接无效时显示错误提示
                var dialog = new ContentDialog
                {
                    Title = "错误",
                    Content = "无效的链接格式",
                    CloseButtonText = "确定",
                    XamlRoot = this.Content.XamlRoot
                };
                await dialog.ShowAsync();
                // 显示占位符
                DefaultPlaceholder.Visibility = Visibility.Visible;
            }
        }

        private void OpenOfficialWin11(object sender, RoutedEventArgs e) =>
            LaunchUrl("https://www.microsoft.com/zh-cn/software-download/windows11 ");

        private void OpenThirdPartyWin11(object sender, RoutedEventArgs e) =>
            LaunchUrl("https://www.123912.com/s/vi6Kjv-gNuBd ");

        private void OpenOfficialWin10(object sender, RoutedEventArgs e) =>
            LaunchUrl("https://www.microsoft.com/zh-cn/software-download/windows10 ");

        private void OpenThirdPartyWin10(object sender, RoutedEventArgs e) =>
            LaunchUrl("https://www.123912.com/s/vi6Kjv-MNuBd ");

        private void OpenMSDN_Win8_1(object sender, RoutedEventArgs e) =>
            LaunchUrl("https://next.itellyou.cn/Original/#cbp=Product?ID=34b4ea5b-c24d-ea11-bd2e-b025aa28351d");

        private void OpenMSDN_Win7(object sender, RoutedEventArgs e) =>
            LaunchUrl(" https://next.itellyou.cn/Original/#cbp=Product?ID=6f677346-0a09-43fa-b60d-e878ed7625a0");

        private void OpenMSDN(object sender, RoutedEventArgs e) =>
            LaunchUrl(" https://next.itellyou.cn/Identity/Account/Login?ReturnUrl=%2FOriginal%2FIndex");
    }
}