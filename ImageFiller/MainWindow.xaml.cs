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

namespace ImageFiller
{
    /// <summary>
    /// MainWindow.xaml の相互作用ロジック
    /// </summary>
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
            Guid = Guid.NewGuid();
        }


        bool running = false;
        private CancellationTokenSource tokenSource = null;

        DirectoryInfo destDir = null;
        DirectoryInfo tmpDir = null;
        Guid Guid;

        HashSet<Task<bool>> Tasks {
            [MethodImpl(MethodImplOptions.Synchronized)]
            get;
            [MethodImpl(MethodImplOptions.Synchronized)]
            set;
        } = new HashSet<Task<bool>>();

        List<FileInfo> SourceFiles = new List<FileInfo>();

        private void BtnStart_Click(object sender, RoutedEventArgs e)
        {
            progressBar.Value = 0;

            if (running) {
                tokenSource.Cancel();
                btnStart.IsEnabled = false;
                return;
            }
            var srcDir = new DirectoryInfo(txtSrc.Text.Trim());
            if (!srcDir.Exists) {
                MessageBox.Show($"{srcDir.FullName} が無い");
                return;
            }

            btnStart.Content = "Stop";
            btnStart.IsEnabled = false;
            Prepare();
            running = true;
            if (tokenSource == null) {
                tokenSource = new CancellationTokenSource();
            }
            var token = tokenSource.Token;

            Task.Factory.StartNew(() =>
            {
                Dispatcher.Invoke(() => btnStart.IsEnabled = true);

                SourceFiles.Clear();
                Dispatcher.Invoke(() => lblProgress.Content = "スキャン中 ...");
                foreach (var x in srcDir.GetFiles("*.png", SearchOption.AllDirectories)) {
                    if (token.IsCancellationRequested) {
                        return;
                    }
                    SourceFiles.Add(x);
                }
                Dispatcher.Invoke(() => lblProgress.Content = "スキャン完了");
                Dispatcher.Invoke(() =>
                {
                    progressBar.Minimum = 0;
                    progressBar.Maximum = SourceFiles.Count;
                });

                long n = 0;
                foreach (var x in SourceFiles) {
                    if (token.IsCancellationRequested) {
                        break;
                    }
                    Dispatcher.Invoke(() =>
                    {
                        lblProgress.Content = $"{x.FullName} {n+1}/{SourceFiles.Count}";
                        progressBar.Value = n;
                    });
                    ProcessFile(x);
                    n++;
                    Thread.Sleep(1);
                }
            }, token).ContinueWith(t =>
            {
                tokenSource.Dispose();
                tokenSource = null;
                running = false;

                foreach (var n in Tasks.ToArray()) {
                    var boo = n.Result;
                }
                if (tmpDir.Exists) {
                    tmpDir.Delete();
                }
                FixRootTimestamp();

                Dispatcher.Invoke(() =>
                {
                    lblProgress.Content = $"done";
                    btnStart.IsEnabled = true;
                    btnStart.Content = "Start";
                });
            });
        }

        private bool ProcessFile(FileInfo orig)
        {
            if (!orig.Exists) {
                return true;
            }

            var destFile = new FileInfo(destDir.FullName + $"\\{orig.Name}");
            if (destFile.Exists) {
                return true;
            }

            var tmp = tmpDir.FullName + $"\\{Guid.NewGuid().ToString()}...png";
            using (var src = orig.OpenRead()) {
                var bitmap = new BitmapImage();
                bitmap.BeginInit();
                bitmap.CacheOption = BitmapCacheOption.OnLoad;
                bitmap.CreateOptions = BitmapCreateOptions.None;
                bitmap.StreamSource = src;
                bitmap.DecodePixelWidth = 7680*2;
                bitmap.DecodePixelHeight = 4320*2;
                bitmap.EndInit();
                bitmap.Freeze();

                using (var dst = new FileStream(tmp, FileMode.OpenOrCreate, FileAccess.Write)) {
                    var encoder = new PngBitmapEncoder();
                    encoder.Frames.Add(BitmapFrame.Create(bitmap));
                    encoder.Save(dst);
                    dst.Flush();
                }
            }

            Task<bool> task = null;
            task = new Task<bool>(() =>
            {
                Tasks.Add(task);
                File.Move(tmp, destFile.FullName);
                FixTimestamp(destFile.FullName);
                return true;
            });
            task.ContinueWith(x =>
            {
                Tasks.Remove(task);
                return true;
            });
            task.Start();

            return true;
        }


        object obzekt = new object();
        static DateTime Timestamp = new DateTime(1999, 12, 31, 23, 59, 0);

        private void FixTimestamp(string path)
        {
            lock (obzekt) {
                var f = new FileInfo(path);
                if (f.Exists) {
                    f.CreationTime = Timestamp;
                    f.LastAccessTime = Timestamp;
                    f.LastWriteTime = Timestamp;
                    // f.Attributes |= FileAttributes.Hidden| FileAttributes.System;
                }
                try {
                    destDir.CreationTime = Timestamp;
                    destDir.LastAccessTime = Timestamp;
                    destDir.LastWriteTime = Timestamp;

                }
                catch {
                    Thread.Sleep(10);
                }

            }
        }

        private void FixRootTimestamp()
        {
            lock (obzekt) {
                for (int i = 0; i < 100; ++i) {
                    try {
                        destDir.CreationTime = Timestamp;
                        destDir.LastAccessTime = Timestamp;
                        destDir.LastWriteTime = Timestamp;
                        break;
                    }
                    catch {
                        Thread.Sleep(100);
                    }
                }
            }
        }

        private void Prepare()
        {
            destDir = new DirectoryInfo(txtDest.Text.Trim());
            if (!destDir.Exists) {
                destDir.Create();
                destDir.Refresh();
                destDir.CreationTime = Timestamp;
                destDir.LastAccessTime = Timestamp;
                destDir.LastWriteTime = Timestamp;
            }

            tmpDir = new DirectoryInfo(destDir.FullName.Substring(0, 3) + $"\\.wtmp..{Guid.NewGuid()}..");
            if (!tmpDir.Exists) {
                tmpDir.Create();
                tmpDir.Refresh();
                tmpDir.CreationTime = Timestamp;
                tmpDir.LastAccessTime = Timestamp;
                tmpDir.LastWriteTime = Timestamp;
                tmpDir.Attributes |= System.IO.FileAttributes.Hidden | FileAttributes.System;
            }
        }

    }

}
