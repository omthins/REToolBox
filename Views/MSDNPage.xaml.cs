using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml;
using Windows.System;

namespace REToolBox.Views
{
    public sealed partial class MSDNPage : Page
    {
        public MSDNPage()
        {
            this.InitializeComponent();
        }

        private async void OpenMSDN(object sender, RoutedEventArgs e)
        {
            await Launcher.LaunchUriAsync(new Uri(" https://next.itellyou.cn/Identity/Account/Login?ReturnUrl=%2FOriginal%2FIndex"));
        }
    }
}