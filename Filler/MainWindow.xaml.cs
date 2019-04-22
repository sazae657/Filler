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
                        Dispatcher.Invoke(() => lblProgress.Content = $"{f} Free:{GetTotalFreeSpace(rootDir.FullName)}");
                        if (File.Exists(f)) {
                            continue;
                        }
                        WriteFile(f);
                    }
                    else {
                        if (taskCount >= MaxTask) {
                            Thread.Sleep(100);
                            continue;
                        }
                        n++;
                        var f = System.IO.Path.Combine(rootDir.FullName, $"{n:D16}.{fileExt}");
                        Dispatcher.Invoke(() => lblProgress.Content = $"{f} Free:{GetTotalFreeSpace(rootDir.FullName)}");
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

                foreach (var n in Tasks.ToArray()) {
                    var boo = n.Result;
                }
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

        static DateTime Timestamp = new DateTime(1999, 12, 31, 23, 59, 0);

        byte[] writeBuffer = null;
        
        int taskCount {
            [MethodImpl(MethodImplOptions.Synchronized)]
            get;
            [MethodImpl(MethodImplOptions.Synchronized)]
            set; } =0;

        HashSet<Task<bool>> Tasks {
            [MethodImpl(MethodImplOptions.Synchronized)]
            get;
            [MethodImpl(MethodImplOptions.Synchronized)]
            set;
        } = new HashSet<Task<bool>>();
    
        private bool WriteFileSync(string path)
        {
            if (File.Exists(path)) {
                return true;
            }

            var free = GetTotalFreeSpace(rootDir.FullName);
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

            var free = GetTotalFreeSpace(rootDir.FullName);
            if (free < blockZise * blockCount) {
                WriteFile(path, free);
                return false;
            }
            Dispatcher.Invoke(() => lblFileStat.Content = $"{path} init");

            rand.NextBytes(writeBuffer);
            var tmp = tmpDir.FullName + $"\\{Guid.NewGuid().ToString()}...mp4";
            var fs = new FileStream(tmp, FileMode.OpenOrCreate);
            for (int i = 0; i < blockCount; ++i) {
                if ((i % 10) == 0) {
                    Dispatcher.Invoke(() => lblFileStat.Content = $"{path} {i}/{blockCount}");
                }
                fs.Write(writeBuffer, 0, writeBuffer.Length);
            }
            Task<bool> task = null;
            task = new Task<bool>(() =>
            {
                Tasks.Add(task);

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
                 Tasks.Remove(task);
                 return true;
             });
            task.Start();

            return true;
        }

        private Task<bool> WriteFileAsync(string path)
        {
            Task<bool> task = null;
            task = new Task<bool>(() =>
                {
                    Tasks.Add(task);
                    taskCount++;
                    Dispatcher.Invoke(() => lblFileStat.Content = $"Task: {taskCount}");

                    if (File.Exists(path)) {
                        return true;
                    }

                    var free = GetTotalFreeSpace(rootDir.FullName);
                    if (free < blockZise * blockCount) {
                        WriteFile(path, free);
                        return false;
                    }
                    rand.NextBytes(writeBuffer);
                    var tmp = tmpDir.FullName + $"\\{Guid.NewGuid().ToString()}...mp4";
                    using (var fs = new FileStream(tmp, FileMode.OpenOrCreate)) {
                        for (int i = 0; i < blockCount; ++i) {
                            fs.Write(writeBuffer, 0, writeBuffer.Length);
                        }
                        fs.Flush();
                    }
                    File.Move(tmp, path);
                    FixTimestamp(path);
                    return true;
                });
            task.ContinueWith((x) =>
            {
                taskCount--;
                Dispatcher.Invoke(() => lblFileStat.Content = $"Task: {taskCount}");

                Tasks.Remove(task);
                return true;
            });
            task.Start();
            return task;
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
            

            fileExt = txtFileExt.Text.Trim();
            writeBuffer = new byte[blockZise+1];
            MaxTask = int.Parse(txtTasks.Text.Trim());
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
