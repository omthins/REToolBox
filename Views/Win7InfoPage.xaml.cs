using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml;
using Windows.System;

namespace REToolBox.Views
{
    public sealed partial class Win7InfoPage : Page
    {
        public Win7InfoPage()
        {
            this.InitializeComponent();
        }

        private async void OpenMSDN_Win7(object sender, RoutedEventArgs e)
        {
            await Launcher.LaunchUriAsync(new Uri(" https://next.itellyou.cn/Original/#cbp=Product?ID=6f677346-0a09-43fa-b60d-e878ed7625a0"));
        }
    }
}