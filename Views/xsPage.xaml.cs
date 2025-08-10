using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Windows.Storage;
using Windows.Storage.Pickers;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Linq;

namespace REToolBox.Views
{
    public sealed partial class xsPage : Page
    {
        private StorageFile inputFile;
        private StorageFolder outputFolder;
        private List<StorageFile> generatedFiles = new List<StorageFile>();
        private CancellationTokenSource cancellationTokenSource;

        public xsPage() => InitializeComponent();

        private async void SelectFileBtn_Click(object sender, RoutedEventArgs e)
        {
            var openPicker = new FileOpenPicker();
            var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(App.MainWindow);
            WinRT.Interop.InitializeWithWindow.Initialize(openPicker, hwnd);
            openPicker.FileTypeFilter.Add("*");
            inputFile = await openPicker.PickSingleFileAsync();
            FilePathText.Text = inputFile?.Path ?? "未选择文件";
        }

        private async void SelectOutputBtn_Click(object sender, RoutedEventArgs e)
        {
            var folderPicker = new FolderPicker();
            var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(App.MainWindow);
            WinRT.Interop.InitializeWithWindow.Initialize(folderPicker, hwnd);
            folderPicker.FileTypeFilter.Add("*");
            outputFolder = await folderPicker.PickSingleFolderAsync();
            OutputPathText.Text = outputFolder?.Path ?? "未选择输出位置";
        }

        private async void ExecuteBtn_Click(object sender, RoutedEventArgs e)
        {
            if (inputFile == null)
            {
                StatusText.Text = "请先选择文件";
                return;
            }
            if (outputFolder == null)
            {
                StatusText.Text = "请选择输出位置";
                return;
            }

            ProgressBar.Value = 0;
            ProgressBar.Visibility = Visibility.Visible;
            ExecuteBtn.IsEnabled = false;
            CancelBtn.IsEnabled = true;
            SelectFileBtn.IsEnabled = false;
            SelectOutputBtn.IsEnabled = false;
            generatedFiles.Clear();
            try
            {
                cancellationTokenSource = new CancellationTokenSource();
                var token = cancellationTokenSource.Token;

                StatusText.Text = "操作进行中...";
                ProgressText.Text = "正在准备...";

                ProgressText.Text = "正在检测文件编码...";
                var encoding = await DetectEncoding(inputFile);
                token.ThrowIfCancellationRequested();
                ProgressBar.Value = 20;

                ProgressText.Text = "正在读取文件内容...";
                var content = await ReadFileContent(inputFile, encoding);
                token.ThrowIfCancellationRequested();
                ProgressBar.Value = 40;

                ProgressText.Text = "正在分割文件内容...";
                var parts = SplitContent(content);
                token.ThrowIfCancellationRequested();
                ProgressBar.Value = 60;

                StatusText.Text = $"正在保存文件 (0/{parts.Length})";
                await SaveSplitFiles(parts, token, (current, total) =>
                {
                    ProgressBar.Value = 60 + (current * 40) / total;
                    ProgressText.Text = $"正在保存文件 ({current}/{total})";
                    StatusText.Text = $"正在保存文件 ({current}/{total})";
                });
                StatusText.Text = $"成功分割为 {parts.Length} 个文件";
                ProgressText.Text = "完成";
            }
            catch (OperationCanceledException)
            {
                StatusText.Text = "操作已取消";
                ProgressText.Text = "正在清理...";
                await CleanGeneratedFiles();
                ProgressText.Text = "已取消操作";
                StatusText.Text = $"已删除 {generatedFiles.Count} 个部分文件";
            }
            catch (Exception ex)
            {
                StatusText.Text = $"错误: {ex.Message}";
                await CleanGeneratedFiles();
            }
            finally
            {
                ExecuteBtn.IsEnabled = true;
                CancelBtn.IsEnabled = false;
                SelectFileBtn.IsEnabled = true;
                SelectOutputBtn.IsEnabled = true;
                cancellationTokenSource = null;
            }
        }

        private async void CancelBtn_Click(object sender, RoutedEventArgs e)
        {
            if (cancellationTokenSource != null && !cancellationTokenSource.IsCancellationRequested)
            {
                CancelBtn.Content = "正在取消...";
                CancelBtn.IsEnabled = false;
                cancellationTokenSource.Cancel();
            }
        }

        private async Task CleanGeneratedFiles()
        {
            if (generatedFiles.Count == 0) return;
            foreach (var file in generatedFiles)
            {
                try
                {
                    if (file != null && !IsSourceFile(file))
                        await file.DeleteAsync();
                }
                catch { }
            }
            generatedFiles.Clear();
        }

        private bool IsSourceFile(StorageFile file)
        {
            return file?.Path == inputFile?.Path;
        }

        private async Task<Encoding> DetectEncoding(StorageFile file)
        {
            if (EncodingComboBox.SelectedItem != null)
            {
                return EncodingComboBox.SelectedItem.ToString() switch
                {
                    "UTF-8" => Encoding.UTF8,
                    "UTF-16" => Encoding.Unicode,
                    "GB2312" => Encoding.GetEncoding("GB2312"),
                    "Big5" => Encoding.GetEncoding("Big5"),
                    _ => Encoding.UTF8
                };
            }
            using var stream = await file.OpenStreamForReadAsync();
            using var reader = new StreamReader(stream, true);
            await reader.ReadToEndAsync();
            return reader.CurrentEncoding;
        }

        private async Task<string> ReadFileContent(StorageFile file, Encoding encoding)
        {
            var buffer = await FileIO.ReadBufferAsync(file);
            return encoding.GetString(buffer.ToArray());
        }

        private string[] SplitContent(string content)
        {
            var method = SplitMethodCombo.SelectedIndex;
            var param = ParameterInput.Text.Trim();
            if (string.IsNullOrWhiteSpace(param))
                throw new ArgumentException("请提供分割参数");
            return method switch
            {
                0 => SplitByCharacters(content, int.Parse(param)),
                1 => SplitByLines(content, int.Parse(param)),
                2 => SplitByParts(content, int.Parse(param)),
                3 => Regex.Split(content, param).Where(s => !string.IsNullOrEmpty(s)).ToArray(),
                _ => throw new InvalidOperationException("无效的分割方法")
            };
        }

        private static string[] SplitByCharacters(string content, int charCount)
        {
            if (charCount <= 0) throw new ArgumentException("字符数必须大于0");
            var parts = new string[(int)Math.Ceiling(content.Length / (double)charCount)];
            for (var i = 0; i < parts.Length; i++)
                parts[i] = content.Substring(i * charCount, Math.Min(charCount, content.Length - i * charCount));
            return parts;
        }

        private static string[] SplitByLines(string content, int linesPerPart)
        {
            if (linesPerPart <= 0) throw new ArgumentException("行数必须大于0");
            var lines = content.Split('\n');
            var parts = new string[(int)Math.Ceiling(lines.Length / (double)linesPerPart)];
            for (var i = 0; i < parts.Length; i++)
            {
                var start = i * linesPerPart;
                var count = Math.Min(linesPerPart, lines.Length - start);
                parts[i] = string.Join("\n", lines, start, count);
            }
            return parts;
        }

        private static string[] SplitByParts(string content, int partCount)
        {
            if (partCount <= 0) throw new ArgumentException("份数必须大于0");
            var charCount = (int)Math.Ceiling(content.Length / (double)partCount);
            return SplitByCharacters(content, charCount);
        }

        private async Task SaveSplitFiles(string[] parts, CancellationToken token, Action<int, int> progressCallback)
        {
            var baseName = Path.GetFileNameWithoutExtension(inputFile.Name);
            var ext = Path.GetExtension(inputFile.Name);
            for (var i = 0; i < parts.Length; i++)
            {
                token.ThrowIfCancellationRequested();
                var file = await outputFolder.CreateFileAsync($"{baseName}_part{i + 1}{ext}",
                    CreationCollisionOption.GenerateUniqueName);
                await FileIO.WriteTextAsync(file, parts[i]);
                generatedFiles.Add(file);
                progressCallback?.Invoke(i + 1, parts.Length);
            }
        }
    }
}