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

namespace Dupe
{
    /// <summary>
    /// MainWindow.xaml の相互作用ロジック
    /// </summary>
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
        }

        DirectoryInfo rootDir = null;
        DirectoryInfo tmpDir = null;
        FileInfo inputFile = null;
        bool running = false;
        private CancellationTokenSource tokenSource = null;

        Guid Guid;
        static DateTime Timestamp = new DateTime(1999, 12, 31, 23, 59, 0);

        int taskCount {
            [MethodImpl(MethodImplOptions.Synchronized)]
            get;
            [MethodImpl(MethodImplOptions.Synchronized)]
            set;
        } = 0;

        HashSet<Task<bool>> Tasks {
            [MethodImpl(MethodImplOptions.Synchronized)]
            get;
            [MethodImpl(MethodImplOptions.Synchronized)]
            set;
        } = new HashSet<Task<bool>>();

        const int MaxTask = 4;

        private void BtnStart_Click(object sender, RoutedEventArgs e)
        {
            if (running) {
                tokenSource.Cancel();
                btnStart.IsEnabled = false;
                return;
            }
            inputFile = new FileInfo(txtSrc.Text.Trim());
            if (!inputFile.Exists) {
                MessageBox.Show($"{txtSrc.Text}がないよう");
                return;
            }
            Prepare();
            progressBar.Maximum = inputFile.Length / blockZise;
            progressBar.Minimum = 0;
            btnStart.Content = "Stop";
            btnStart.IsEnabled = false;

            running = true;
            if (tokenSource == null) {
                tokenSource = new CancellationTokenSource();
            }
            var token = tokenSource.Token;
            Task.Factory.StartNew(() =>
            {
                Dispatcher.Invoke(() => btnStart.IsEnabled = true);
                long n = 0;
                while (true) {
                    if (token.IsCancellationRequested) {
                        break;
                    }

                    if (taskCount >= MaxTask) {
                        Thread.Sleep(100);
                        continue;
                    }
                    n++;
                    var f = System.IO.Path.Combine(rootDir.FullName, $"{n:D16}{inputFile.Extension}");
                    Dispatcher.Invoke(() => {
                        progressBar.Value = 0;
                        lblState.Content = $"{f} Free:{GetTotalFreeSpace(rootDir.FullName)}";
                    });
                    if (File.Exists(f)) {
                        continue;
                    }
                    WriteFile(f);
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

                Dispatcher.Invoke(() =>
                {
                    lblState.Content = "done";
                    btnStart.IsEnabled = true;
                    btnStart.Content = "Start";
                });
            });


        }
        const long blockZise = 8192 * 1024;
        byte[] readBuffer = new byte[blockZise];

        private bool WriteFile(string path)
        {
            if (File.Exists(path)) {
                return true;
            }

            var free = GetTotalFreeSpace(rootDir.FullName);
            if (free < readBuffer.Length) {
                return false;
            }

            var tmp = tmpDir.FullName + $"\\{Guid.NewGuid().ToString()}...tmp";
            var fs = new FileStream(tmp, FileMode.OpenOrCreate);
            long bc = 0;
            using (var src = new FileStream(inputFile.FullName, FileMode.Open, FileAccess.Read)) {
                while (true) {
                    Dispatcher.Invoke(() => progressBar.Value = ++bc);
                    var s = src.Read(readBuffer, 0, readBuffer.Length);
                    if (s <= 0) {
                        break;
                    }
                    fs.Write(readBuffer, 0, s);
                }
            }
            Task<bool> task = null;
            task = new Task<bool>(() =>
            {
                Tasks.Add(task);
                taskCount++;
                fs.Flush();
                fs.Close();
                fs.Dispose();
                fs = null;
                File.Move(tmp, path);
                FixTimestamp(path);

                return true;
            });
            task.ContinueWith(x =>
            {
                taskCount--;
                Tasks.Remove(task);
                return true;
            });
            task.Start();

            return true;
        }

        object obzekt = new object();

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
                    rootDir.CreationTime = Timestamp;
                    rootDir.LastAccessTime = Timestamp;
                    rootDir.LastWriteTime = Timestamp;
                }
                catch {
                }
            }
        }

        private void Prepare()
        {
            rootDir = new DirectoryInfo(txtDest.Text.Trim());
            if (!rootDir.Exists) {
                rootDir.Create();
                rootDir.Refresh();
                rootDir.CreationTime = Timestamp;
                rootDir.LastAccessTime = Timestamp;
                rootDir.LastWriteTime = Timestamp;
            }

            tmpDir = new DirectoryInfo(rootDir.FullName.Substring(0, 3) + $"\\.wtmp..{Guid.NewGuid()}..");
            if (!tmpDir.Exists) {
                tmpDir.Create();
                tmpDir.Refresh();
                tmpDir.CreationTime = Timestamp;
                tmpDir.LastAccessTime = Timestamp;
                tmpDir.LastWriteTime = Timestamp;
                tmpDir.Attributes |= System.IO.FileAttributes.Hidden | FileAttributes.System;
            }
        }

        private long GetTotalFreeSpace(string driveName)
        {
            foreach (DriveInfo drive in DriveInfo.GetDrives()) {
                if (drive.IsReady && driveName.StartsWith(drive.Name)) {
                    return drive.TotalFreeSpace;
                }
            }
            return -1;
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

        private void TxtSrc_Drop(object sender, DragEventArgs e)
        {
            var files = e.Data.GetData(DataFormats.FileDrop) as string[];
            string path = null;
            if (files != null) {
                foreach (var s in files) {
                    path = s;
                    break;
                }
            }
            txtSrc.Text = path;
        }

        private void TxtDest_PreviewDragOver(object sender, DragEventArgs e)
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
