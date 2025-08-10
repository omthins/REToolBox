using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml;
using Windows.System;

namespace REToolBox.Views
{
    public sealed partial class Win10InfoPage : Page
    {
        public Win10InfoPage()
        {
            this.InitializeComponent();
        }

        private async void OpenOfficialWin10(object sender, RoutedEventArgs e)
        {
            await Launcher.LaunchUriAsync(new Uri("https://www.microsoft.com/zh-cn/software-download/windows10 "));
        }

        private async void OpenThirdPartyWin10(object sender, RoutedEventArgs e)
        {
            await Launcher.LaunchUriAsync(new Uri("https://www.123912.com/s/vi6Kjv-MNuBd "));
        }
    }
}