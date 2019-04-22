using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace Stamper
{
    /// <summary>
    /// MainWindow.xaml の相互作用ロジック
    /// </summary>
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
            var dt = new DateTime(1999, 12, 31, 23, 59, 0);
            var dto = new DateTimeOffset(dt.Ticks, new TimeSpan(+09, 00, 00));
            txtTime.Text = dto.ToUnixTimeSeconds().ToString();
            UpdateTimePreview();
        }
        DirectoryInfo rootDir = null;
        bool running = false;
        CancellationTokenSource tokenSource = null;
        DateTime fileTime;
        public class Error
        {
            public string Path { get; set; }
            public string Message { get; set; }
        }
        
        List<Error> Errors {
            [MethodImpl(MethodImplOptions.Synchronized)]
            get;
            [MethodImpl(MethodImplOptions.Synchronized)]
            set;
        }

        private void BtnStart_Click(object sender, RoutedEventArgs e)
        {
            if (running) {
                tokenSource.Cancel();
                btnStart.IsEnabled = false;
                return;
            }
            rootDir = new DirectoryInfo(txtDest.Text.Trim());
            if (!rootDir.Exists) {
                MessageBox.Show($"ない:{txtDest.Text}");
                return;
            }
            var 字 = txtTime.Text.Trim();
            if (string.IsNullOrEmpty(字)) {
                MessageBox.Show($"タイムスタンプが空");
                return;
            }
            fileTime = DateTimeOffset.FromUnixTimeSeconds(long.Parse(字)).LocalDateTime;

            if (MessageBox.Show($"{rootDir} => {fileTime}: まじで？", "まじで？", MessageBoxButton.OKCancel) != MessageBoxResult.OK) {
                return;
            }
            Errors = new List<Error>();
            btnStart.Content = "Stop";
            btnStart.IsEnabled = false;
            if (tokenSource == null) {
                tokenSource = new CancellationTokenSource();
            }
            var token = tokenSource.Token;
            Task.Factory.StartNew(() => {
                running = true;
                Dispatcher.Invoke(() => {
                    btnStart.IsEnabled = true;
                });
                if (token.IsCancellationRequested) {
                    return;
                }
                ScanAndFix(rootDir, token);
            }, token).ContinueWith(t =>
            {
                tokenSource.Dispose();
                tokenSource = null;
                running = false;
                Dispatcher.Invoke(() =>
                {
                    lblProgress.Content = $"完了 エラー{Errors.Count}";
                    btnStart.IsEnabled = true;
                    btnStart.Content = "Start";
                });
            });
        }

        private void ScanAndFix(DirectoryInfo root, CancellationToken token)
        {
            if (token.IsCancellationRequested) {
                return;
            }
            Dispatcher.Invoke(() =>
                lblProgress.Content = $"{root.FullName}"
            );
            foreach (var n in root.GetDirectories()) {
                try {
                    try {
                        if (n.Attributes.HasFlag(FileAttributes.ReparsePoint)) {
                            continue;
                        }
                    }
                    catch (Exception e) {
                        Errors.Add(new Error { Path = n.FullName, Message = e.Message });
                        continue;
                    }
                    ScanAndFix(n, token);
                }
                catch (Exception e) {
                    Errors.Add(new Error { Path = n.FullName, Message = e.Message });
                }
            }
            try {
                foreach (var n in root.GetFiles()) {
                    try {
                        FixTimestamp(n);
                    }
                    catch (Exception e) {
                        Errors.Add(new Error { Path = n.FullName, Message = e.Message });
                    }
                }
            }
            catch (Exception e) {
                Errors.Add(new Error { Path = root.FullName, Message = e.Message });
            }
            FixTimestamp(root);
        }

        private void FixTimestamp(FileSystemInfo path)
        {
            try {
                path.CreationTime = fileTime;
                path.LastAccessTime = fileTime;
                path.LastWriteTime = fileTime;
            }
            catch (Exception e) {
                Errors.Add(new Error { Path = path.FullName, Message = e.Message });
            }
        }

        private void TxtTime_TextChanged(object sender, TextChangedEventArgs e)
        {
            UpdateTimePreview();
        }

        private void UpdateTimePreview()
        {
            var t = txtTime.Text.Trim();
            if (string.IsNullOrEmpty(t)) {
                return;
            }
            var ts = DateTimeOffset.FromUnixTimeSeconds(long.Parse(t)).LocalDateTime;
            if (null != lblTime) {
                lblTime.Content = ts.ToString();
            }
        }

        private void TxtDest_Drop(object sender, DragEventArgs e)
        {
            var files = e.Data.GetData(DataFormats.FileDrop) as string[];
            string path = null;
            if (files != null) {
                foreach (var s in files) {
                    path = s;
                    break;
                }
            }
            txtDest.Text = path;
        }

        private void TxtDest_DragOver(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent(DataFormats.FileDrop, true)) {
                e.Effects = DragDropEffects.Copy;
            }
            else {
                e.Effects = DragDropEffects.None;
            }
            e.Handled = true;
        }


    }
}
