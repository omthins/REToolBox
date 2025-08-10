using Microsoft.UI.Xaml.Controls;
using REToolBox.Views;

namespace REToolBox.Views
{
    public sealed partial class DevPage : Page
    {
        public DevPage()
        {
            this.InitializeComponent();
        }

        private void NavigateToAiPage(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
        {
            Frame.Navigate(typeof(AiPage));
        }

        private void NavigateToDownloaderPage(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
        {
            Frame.Navigate(typeof(DownloaderPage));
        }

        private void NavigateToFileSplitterPage(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
        {
            Frame.Navigate(typeof(FileSplitterPage));
        }

        private void NavigateToHashPage(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
        {
            Frame.Navigate(typeof(HashPage));
        }

        private void NavigateToKeyGeneratorPage(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
        {
            Frame.Navigate(typeof(KeyGeneratorPage));
        }

        private void NavigateToMainPage(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
        {
            Frame.Navigate(typeof(MainPage));
        }

        private void NavigateToMSDNPage(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
        {
            Frame.Navigate(typeof(MSDNPage));
        }

        private void NavigateToNewToolPage(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
        {
            Frame.Navigate(typeof(NewToolPage));
        }

        private void NavigateToOSPage(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
        {
            Frame.Navigate(typeof(OSPage));
        }

        private void NavigateToSettingsPage(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
        {
            Frame.Navigate(typeof(SettingsPage));
        }

        private void NavigateToShellPage(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
        {
            Frame.Navigate(typeof(ShellPage));
        }

        private void NavigateToToolPage(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
        {
            Frame.Navigate(typeof(ToolPage));
        }

        private void NavigateToWin10InfoPage(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
        {
            Frame.Navigate(typeof(Win10InfoPage));
        }

        private void NavigateToWin11InfoPage(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
        {
            Frame.Navigate(typeof(Win11InfoPage));
        }

        private void NavigateToWin7InfoPage(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
        {
            Frame.Navigate(typeof(Win7InfoPage));
        }

        private void NavigateToWin81InfoPage(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
        {
            Frame.Navigate(typeof(Win81InfoPage));
        }
        private void NavigateToMathPage(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
        {
            Frame.Navigate(typeof(MathPage));
        }

        private void NavigateToXsPage(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
        {
            Frame.Navigate(typeof(xsPage));
        }
        private void NavigateToVidPage(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
        {
            Frame.Navigate(typeof(VidPage));
        }
        private void NavigateToJsqPage(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
        {
            Frame.Navigate(typeof(JsqPage));
        }


        private void Button_Click(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
        {

        }
    }
}