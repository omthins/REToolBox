using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml;
using Windows.System;

namespace REToolBox.Views
{
    public sealed partial class Win81InfoPage : Page
    {
        public Win81InfoPage()
        {
            this.InitializeComponent();
        }

        private async void OpenMSDN_Win8_1(object sender, RoutedEventArgs e)
        {
            await Launcher.LaunchUriAsync(new Uri("https://next.itellyou.cn/Original/#cbp=Product?ID=34b4ea5b-c24d-ea11-bd2e-b025aa28351d"));
        }
    }
}