using System;
using System.Collections.Generic;
using System.Diagnostics;
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
using ﾆﾗ;

namespace Filler
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
            txtDest.Text = 
                System.Environment.GetFolderPath(Environment.SpecialFolder.Personal);
        }

        bool running = false;
        private CancellationTokenSource tokenSource = null;
        UltraSuperSpool spool = new UltraSuperSpool();

        DirectoryInfo rootDir = null;
        DirectoryInfo tmpDir = null;
        Guid Guid;
        string fileExt = "xlsx";

        private void BtnStart_Click(object sender, RoutedEventArgs e)
        {
            if (running) {
                tokenSource.Cancel();
                btnStart.IsEnabled = false;
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
            spool.Omit();
            Task.Factory.StartNew(() =>
            {
                Dispatcher.Invoke(() => btnStart.IsEnabled = true);
                long n = 0;
                while (true) {
                    if (token.IsCancellationRequested) {
                        break;
                    }
                    if (MaxTask <= 0) {
                        n++;
                        var f = System.IO.Path.Combine(rootDir.FullName, $"{n:D16}.{fileExt}");
                        Dispatcher.Invoke(() => lblProgress.Content = $"{f} Free:{Nira.GetDriveFreeSpace(rootDir.FullName)}");
                        if (File.Exists(f)) {
                            continue;
                        }
                        WriteFile(f);
                    }
                    else {
                        if (spool.TaskCount >= MaxTask) {
                            Thread.Sleep(100);
                            continue;
                        }
                        var dfs = Nira.GetDriveFreeSpace(rootDir.FullName);
                        if (dfs < 10) {
                            break;
                        }

                        n++;
                        var f = System.IO.Path.Combine(rootDir.FullName, $"{n:D16}.{fileExt}");
                        Dispatcher.Invoke(() => lblProgress.Content = $"{f} Free:{dfs}");
                        if (File.Exists(f)) {
                            continue;
                        }
                        WriteFileAsync(f);
                    }
                    Thread.Sleep(1);
                }
            }, token).ContinueWith(t =>
            {
                tokenSource.Dispose();
                tokenSource = null;
                running = false;
                spool.Stop();
                spool.Wait();

                if (tmpDir.Exists) {
                    tmpDir.Delete();
                }

                Dispatcher.Invoke(() =>
                {
                    btnStart.IsEnabled = true;
                    btnStart.Content = "Start";
                });
            });
        }

        Random rand = new Random();

        const long blockZise = 1048576;
        const long blockCount = 1024;
        int MaxTask = 2;

        byte[] writeBuffer = null;
            
        private bool WriteFileSync(string path)
        {
            if (File.Exists(path)) {
                return true;
            }

            var free = Nira.GetDriveFreeSpace(rootDir.FullName);
            if (free < blockZise * blockCount) {
                WriteFile(path, free);
                return false;
            }
            Dispatcher.Invoke(() => lblFileStat.Content = $"{path} init");

            rand.NextBytes(writeBuffer);
            using (var fs = new FileStream(path, FileMode.OpenOrCreate)) {
                for (int i = 0; i < blockCount; ++i) {
                    if ((i % 10) == 0) {
                        Dispatcher.Invoke(() => lblFileStat.Content = $"{path} {i}/{blockCount}");
                    }
                    fs.Write(writeBuffer, 0, writeBuffer.Length);
                }
                fs.Flush();
            }
            FixTimestamp(path);

            return true;
        }

        private bool WriteFile(string path)
        {
            if (File.Exists(path)) {
                return true;
            }

            var free = Nira.GetDriveFreeSpace(rootDir.FullName);
            if (free < blockZise * blockCount) {
                WriteFile(path, free);
                return false;
            }
            Dispatcher.Invoke(() => lblFileStat.Content = $"{path} init");

            rand.NextBytes(writeBuffer);
            var tmp = tmpDir.FullName + $"\\{Guid.NewGuid().ToString()}...mp4";
            using (var fs = spool.CreateClosableStream(new FileStream(tmp, FileMode.OpenOrCreate))) {
                fs.Closed += (e, v) =>
                {
                    File.Move(tmp, path);
                    FixTimestamp(path);
                };

                for (int i = 0; i < blockCount; ++i) {
                    if ((i % 10) == 0) {
                        Dispatcher.Invoke(() => lblFileStat.Content = $"{path} {i}/{blockCount}");
                    }
                    fs.Write(writeBuffer, 0, writeBuffer.Length);
                }
            }
            return true;
        }

        private void WriteFileAsync(string path)
        {
            spool.Schedule(token =>
                {
                    bool cancel = false;
                    Dispatcher.Invoke(() => lblFileStat.Content = $"Task: {spool.TaskCount}");

                    if (File.Exists(path)) {
                        return;
                    }

                    var free = Nira.GetDriveFreeSpace(rootDir.FullName);
                    if (free < blockZise * blockCount) {
                        WriteFile(path, free);
                        return;
                    }
                    rand.NextBytes(writeBuffer);
                    var tmp = tmpDir.FullName + $"\\{Guid.NewGuid().ToString()}...mp4";
                    using (var fs = new FileStream(tmp, FileMode.OpenOrCreate)) {
                        for (int i = 0; i < blockCount; ++i) {
                            if (token.IsCancellationRequested) {
                                cancel = true;
                                Debug.WriteLine($"CancellationRequested");
                                break;
                            }
                            fs.Write(writeBuffer, 0, writeBuffer.Length);
                        }
                        fs.Flush();
                    }
                    File.Move(tmp, path);
                    FixTimestamp(path);
                    return;
                }, 
                token => {
                    Dispatcher.Invoke(() => lblFileStat.Content = $"Task: {spool.TaskCount}");
            });
        }

        private void WriteFile(string path, long zise)
        {
            var bytes = new byte[zise];
            rand.NextBytes(bytes);
            using (var fs = new FileStream(path, FileMode.OpenOrCreate)) {
                Dispatcher.Invoke(() => lblFileStat.Content = $"{path} {zise}(F)");
                fs.Write(bytes, 0, bytes.Length);
                fs.Flush();
            }
            FixTimestamp(path);
        }

        object obzekt = new object();

        private void FixTimestamp(string path)
        {
            lock (obzekt) {
                Nira.FixTimestamp(new FileInfo(path));
                Nira.FixTimestamp(rootDir);
            }
        }

        private void Prepare()
        {
            rootDir = new DirectoryInfo(txtDest.Text.Trim());
            if (!rootDir.Exists) {
                rootDir.Create();
                rootDir.Refresh();
                Nira.FixTimestamp(rootDir);
            }

            tmpDir = new DirectoryInfo(rootDir.FullName.Substring(0, 3) + $"\\.wtmp..{Guid.NewGuid()}..");
            if (!tmpDir.Exists) {
                tmpDir.Create();
                tmpDir.Refresh();
                Nira.FixTimestamp(tmpDir);
                #if !DEBUG
                tmpDir.Attributes |= System.IO.FileAttributes.Hidden | FileAttributes.System;
                #endif
            }
            
            fileExt = txtFileExt.Text.Trim();
            writeBuffer = new byte[blockZise+1];
            MaxTask = int.Parse(txtTasks.Text.Trim());
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

        private void TxtDest_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (null != diskInfo) {
                diskInfo.Drive = txtDest.Text;
            }
        }
    }
}
