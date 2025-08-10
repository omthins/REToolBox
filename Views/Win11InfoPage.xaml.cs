using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml;
using Windows.System;

namespace REToolBox.Views
{
    public sealed partial class Win11InfoPage : Page
    {
        public Win11InfoPage()
        {
            this.InitializeComponent();
        }

        private async void OpenOfficialWin11(object sender, RoutedEventArgs e)
        {
            await Launcher.LaunchUriAsync(new Uri("https://www.microsoft.com/zh-cn/software-download/windows11 "));
        }

        private async void OpenThirdPartyWin11(object sender, RoutedEventArgs e)
        {
            await Launcher.LaunchUriAsync(new Uri("https://www.123912.com/s/vi6Kjv-gNuBd "));
        }
    }
}