using System;
using System.IO;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Threading.Tasks;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Windows.Storage;
using Windows.Storage.Pickers;
using Windows.Storage.Streams;

namespace REToolBox.Views
{
    public sealed partial class VidPage : Page
    {
        private StorageFile selectedFile;
        public VidPage()
        {
            this.InitializeComponent();
        }

        private async void SelectFileButton_Click(object sender, RoutedEventArgs e)
        {
            var picker = new FileOpenPicker();
            var window = App.MainWindow;
            var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(window);
            WinRT.Interop.InitializeWithWindow.Initialize(picker, hwnd);
            picker.ViewMode = PickerViewMode.Thumbnail;
            picker.SuggestedStartLocation = PickerLocationId.VideosLibrary;
            picker.FileTypeFilter.Add(".mp4");
            picker.FileTypeFilter.Add(".avi");
            picker.FileTypeFilter.Add(".mkv");
            picker.FileTypeFilter.Add(".mov");
            picker.FileTypeFilter.Add(".wmv");
            picker.FileTypeFilter.Add(".flv");
            picker.FileTypeFilter.Add(".webm");
            selectedFile = await picker.PickSingleFileAsync();
            if (selectedFile != null)
            {
                SelectedFileTextBlock.Text = selectedFile.Name;
                ProcessButton.IsEnabled = true;
            }
            else
            {
                SelectedFileTextBlock.Text = "未选择文件";
                ProcessButton.IsEnabled = false;
            }
        }

        private async void ProcessButton_Click(object sender, RoutedEventArgs e)
        {
            if (selectedFile == null) return;
            ProcessButton.IsEnabled = false;
            ProgressBarControl.Visibility = Visibility.Visible;
            StatusTextBlock.Text = "正在处理...";
            try
            {
                await ProcessVideoFile();
                StatusTextBlock.Text = "处理完成！文件已保存到原文件目录";
            }
            catch (Exception ex)
            {
                StatusTextBlock.Text = $"处理失败: {ex.Message}";
            }
            finally
            {
                ProgressBarControl.Visibility = Visibility.Collapsed;
                ProcessButton.IsEnabled = true;
            }
        }

        private async Task ProcessVideoFile()
        {
            Random random = new Random();
            int intervalKB, bytesToCorrupt, probability;
            if (RandomCheckBox.IsChecked == true)
            {
                intervalKB = random.Next(50, 500);
                bytesToCorrupt = random.Next(1, 50);
                probability = random.Next(10, 90);
            }
            else
            {
                if (!int.TryParse(IntervalTextBox.Text, out intervalKB) || intervalKB <= 0)
                    intervalKB = 100;
                if (!int.TryParse(BytesTextBox.Text, out bytesToCorrupt) || bytesToCorrupt <= 0)
                    bytesToCorrupt = 10;
                if (!int.TryParse(ProbabilityTextBox.Text, out probability) || probability < 0 || probability > 100)
                    probability = 50;
            }
            var folder = await selectedFile.GetParentAsync();
            var fileNameWithoutExtension = Path.GetFileNameWithoutExtension(selectedFile.Name);
            var fileExtension = selectedFile.FileType;
            var newFileName = $"{fileNameWithoutExtension}_corrupted{fileExtension}";
            var newFile = await folder.CreateFileAsync(newFileName, CreationCollisionOption.GenerateUniqueName);
            using (var inputStream = await selectedFile.OpenReadAsync())
            using (var outputStream = await newFile.OpenAsync(FileAccessMode.ReadWrite))
            {
                using (var reader = new DataReader(inputStream))
                using (var writer = new DataWriter(outputStream)
                {
                    ByteOrder = ByteOrder.LittleEndian
                })
                {
                    ulong fileSize = inputStream.Size;
                    ulong processedBytes = 0;
                    ulong intervalBytes = (ulong)(intervalKB * 1024);
                    await reader.LoadAsync((uint)fileSize);
                    while (processedBytes < fileSize)
                    {
                        uint bytesToRead = (uint)Math.Min(4096, (long)(fileSize - processedBytes));
                        IBuffer buffer = reader.ReadBuffer(bytesToRead);
                        byte[] data = new byte[buffer.Length];
                        WindowsRuntimeBufferExtensions.CopyTo(buffer, data);
                        if (processedBytes % intervalBytes == 0 && random.Next(100) < probability)
                        {
                            for (int i = 0; i < Math.Min(bytesToCorrupt, data.Length); i++)
                            {
                                int index = random.Next(data.Length);
                                data[index] = (byte)random.Next(256);
                            }
                        }
                        writer.WriteBytes(data);
                        processedBytes += bytesToRead;
                    }
                    await writer.StoreAsync();
                }
            }
        }

        private void RandomCheckBox_Checked(object sender, RoutedEventArgs e)
        {
            SetSettingsEnabled(false);
        }

        private void RandomCheckBox_Unchecked(object sender, RoutedEventArgs e)
        {
            SetSettingsEnabled(true);
        }

        private void SetSettingsEnabled(bool isEnabled)
        {
            foreach (var child in SettingsGrid.Children)
            {
                if (child is Control control)
                {
                    control.IsEnabled = isEnabled;
                }
            }
        }
    }
}