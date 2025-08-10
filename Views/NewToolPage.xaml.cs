using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;

namespace REToolBox.Views
{
    public sealed partial class NewToolPage : Page
    {
        public NewToolPage()
        {
            this.InitializeComponent();
            NavigateToPage("KeyGenerator");
            if (NavigationViewControl.MenuItems.Count > 0)
            {
                NavigationViewControl.SelectedItem = NavigationViewControl.MenuItems[0];
            }
        }

        private void NavigationViewControl_ItemInvoked(NavigationView sender, NavigationViewItemInvokedEventArgs args)
        {
            if (args.InvokedItemContainer != null)
            {
                string tag = args.InvokedItemContainer.Tag?.ToString();
                NavigateToPage(tag);
            }
        }

        private void NavigateToPage(string tag)
        {
            Type pageType = tag switch
            {
                "KeyGenerator" => typeof(KeyGeneratorPage),
                "FileSplitter" => typeof(FileSplitterPage),
                "Downloader" => typeof(DownloaderPage),
                "Math"=> typeof(MathPage),
                "Vid" => typeof(VidPage),
                "Jsq" => typeof(JsqPage),
                _ => null
            };

            if (pageType != null && ContentFrame.CurrentSourcePageType != pageType)
            {
                ContentFrame.Navigate(pageType);
            }
        }
    }
}