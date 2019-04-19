using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
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

namespace DirectoryFiller
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
        public class Error {
            public string Path { get; set; }
            public string Message { get; set; }
        }

        bool running = false;
        private CancellationTokenSource tokenSource = null;

        DirectoryInfo rootDir = null;
        Stack<FileInfo> files = null;
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

            if (!Prepare()) {
                return;
            }
            if (MessageBox.Show($"{rootDir}: まじで？", "まじで？", MessageBoxButton.OKCancel) != MessageBoxResult.OK) {
                return;
            }
            Errors = new List<Error>();
            btnStart.Content = "Stop";
            btnStart.IsEnabled = false;
            running = true;
            if (tokenSource == null) {
                tokenSource = new CancellationTokenSource();
            }
            files = new Stack<FileInfo>();
            var token = tokenSource.Token;
            Task.Factory.StartNew(() =>
            {
                Dispatcher.Invoke(() => {
                    btnStart.IsEnabled = true;
                });
                SearchFiles(rootDir, token);
                long n = 0;
                long max = files.Count;
                Dispatcher.Invoke(() => {
                    progressBar.Minimum = 0;
                    progressBar.Maximum = files.Count;
                });
                while (true) {
                    if (token.IsCancellationRequested) {
                        break;
                    }
                    if (files.Count == 0) {
                        break;
                    }
                    if (taskCount >= MaxTask) {
                        Thread.Sleep(10);
                        continue;
                    }
                    n++;
                    var f = files.Pop();
                    if (!f.Exists) {
                        continue;
                    }
                    Dispatcher.Invoke(() =>
                    {
                        lblProgress.Content = $"{n}/{progressBar.Maximum}: {f.FullName}";
                        progressBar.Value = n;
                    });
                    WriteFileAsync(f);
                }
             }, token).ContinueWith(t =>
            {
                tokenSource.Dispose();
                tokenSource = null;
                running = false;
                Dispatcher.Invoke(() =>
                {
                    lblProgress.Content = $"完了待ち";
                });
                foreach (var n in Tasks.ToArray()) {
                    var boo = n?.Result;
                }
                SaveLog();
                Dispatcher.Invoke(() =>
                {
                    lblProgress.Content = $"Complete! ｴﾗー:{Errors.Count}";
                    btnStart.IsEnabled = true;
                    btnStart.Content = "Start";
                });
            });
        }

        private void SaveLog()
        {
            if (Errors.Count == 0) {
                return;
            }

            using (var sw = new StreamWriter(
                (new FileInfo(Assembly.GetEntryAssembly().Location)).DirectoryName + $"\\{DateTime.Now.ToString("yyyy-MM-dd-HH-mm-ss")}.txt", false, Encoding.Unicode)) {
                sw.WriteLine($"# {DateTime.Now.ToString()}");
                sw.WriteLine($"# {Environment.UserName}");
                sw.WriteLine($"# {Environment.MachineName}");
                foreach (var x in Errors) {
                    sw.WriteLine($"{x.Path} : {x.Message}");
                }
            }
        }

        private void SearchFiles(DirectoryInfo root, CancellationToken token)
        {
            if (token.IsCancellationRequested) {
                return;
            }
            Dispatcher.Invoke(() =>
                lblProgress.Content = $"検索中:{files.Count} {root.FullName}"
            );
            try {
                foreach (var n in root.GetFiles()) {
                    try {
                        if (n.Attributes.HasFlag(FileAttributes.ReparsePoint)) {
                            continue;
                        }
                    }
                    catch (Exception e) {
                        Errors.Add(new Error { Path = n.FullName, Message = e.Message });
                        continue;
                    }

                    files.Push(n);
                }
            }
            catch (Exception e) {
                Errors.Add(new Error { Path = root.FullName, Message = e.Message });
            }

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
                    SearchFiles(n, token);
                }
                catch (Exception e) {
                    Errors.Add(new Error { Path = n.FullName, Message = e.Message });
                    continue;
                }
            }
        }

        int MaxTask = 4;

        HashSet<Task<bool>> Tasks {
            [MethodImpl(MethodImplOptions.Synchronized)]
            get;
            [MethodImpl(MethodImplOptions.Synchronized)]
            set;
        } = new HashSet<Task<bool>>();

        int taskCount {
            [MethodImpl(MethodImplOptions.Synchronized)]
            get;
            [MethodImpl(MethodImplOptions.Synchronized)]
            set;
        } = 0;
        Random rand = new Random();
        const int BufferZise = 10485760;
        byte[] templateBuffer;

        private Task<bool> WriteFileAsync(FileInfo path)
        {
            Task<bool> task = null;
            task = new Task<bool>(() =>
            {
                Tasks.Add(task);
                taskCount++;
                try {
                    if (!path.Exists) {
                        return true;
                    }
                    var bs = Math.Min(BufferZise, path.Length);
                    long bc = 1;
                    if (path.Length < BufferZise) {
                        bc = 1;
                    }
                    else {
                        bc = path.Length / BufferZise;
                    }
                    byte[] writeBuffer = new byte[bs];
                    rand.NextBytes(writeBuffer);

                    using (var fs = new FileStream(path.FullName, FileMode.Open, FileAccess.Write)) {
                        fs.Seek(0, SeekOrigin.Begin);
                        for (int i = 0; i < bc; ++i) {
                            fs.Write(writeBuffer, 0, writeBuffer.Length);
                        }
                        fs.Write(writeBuffer, 0, writeBuffer.Length);
                        fs.Flush();
                    }
                    using (var fs = new FileStream(path.FullName, FileMode.Open, FileAccess.Write)) {
                        fs.Seek(0, SeekOrigin.Begin);
                        for (int i = 0; i < bc; ++i) {
                            fs.Write(templateBuffer, 0, templateBuffer.Length);
                        }
                        fs.Write(templateBuffer, 0, templateBuffer.Length);
                        fs.Flush();
                    }
                    FixTimestamp(path);
                }
                catch {
                    return false;
                }
                return true;
            });
            task.ContinueWith((x) =>
            {
                taskCount--;
                Tasks.Remove(task);
                return true;
            });
            task.Start();
            return task;
        }

        object obzekt = new object();
        static DateTime Timestamp = new DateTime(1999, 12, 31, 23, 59, 0);

        private void FixTimestamp(FileInfo path)
        {
            if (path.Exists) {
                path.CreationTime = Timestamp;
                path.LastAccessTime = Timestamp;
                path.LastWriteTime = Timestamp;
            }
        }

        private bool Prepare()
        {
            rootDir = new DirectoryInfo(txtSrc.Text.Trim());
            if (!rootDir.Exists) {
                MessageBox.Show($"{rootDir}: 無い");
                return false;
            }
            var u = (new UTF8Encoding(false)).GetBytes("ｵﾗ");
            using (var ms = new MemoryStream()) {
                for (int i = 0; i < BufferZise / u.Length; ++i) {
                    ms.Write(u, 0, u.Length);
                }
                ms.Close();
                templateBuffer = ms.ToArray();
            }
            return true;
        }
    }


}
