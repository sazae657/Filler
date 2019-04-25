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
using ﾆﾗ;
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
        UltraSuperSpool spool = new UltraSuperSpool();

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
            spool.Omit();

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
                    if (spool.TaskCount >= MaxTask) {
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

                spool.Stop();
                spool.Wait();

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
                        n.Attributes &= ~System.IO.FileAttributes.Hidden;
                        n.Attributes &= ~System.IO.FileAttributes.ReadOnly;
                        n.Attributes &= ~System.IO.FileAttributes.System;
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

        Random rand = new Random();
        const int BufferZise = 10485760;
        byte[] templateBuffer;

        private void WriteFileAsync(FileInfo path)
        {
            spool.Schedule(token =>
            {
                try {
                    if (!path.Exists) {
                        return;
                    }
                    try {
                        path.Attributes &= ~System.IO.FileAttributes.Hidden;
                        path.Attributes &= ~System.IO.FileAttributes.ReadOnly;
                        path.Attributes &= ~System.IO.FileAttributes.System;
                    }
                    catch (Exception e) {
                        Errors.Add(new Error { Path = path.FullName, Message = e.Message });
                    }

                    long fsize = path.Length;
                    if (fsize < 128) {
                        fsize = 128;
                    }
                    var bs = Math.Min(BufferZise, fsize);
                    long bc = 1;
                    long amt = 0;
                    if (fsize < BufferZise) {
                        bc = 0;
                        amt = fsize;
                    }
                    else {
                        bc = fsize / BufferZise;
                        amt = fsize - (BufferZise * bc);
                    }
                    byte[] writeBuffer = new byte[bs];
                    rand.NextBytes(writeBuffer);

                    using (var fs = new FileStream(path.FullName, FileMode.Open, FileAccess.Write)) {
                        fs.Seek(0, SeekOrigin.Begin);
                        for (int i = 0; i < bc; ++i) {
                            fs.Write(writeBuffer, 0, writeBuffer.Length);
                            if (token.IsCancellationRequested) {
                                break;
                            }
                        }
                        fs.Write(writeBuffer, 0, (int)amt);
                        fs.Flush();
                    }
                    using (var fs = new FileStream(path.FullName, FileMode.Open, FileAccess.Write)) {
                        fs.Seek(0, SeekOrigin.Begin);
                        for (int i = 0; i < bc; ++i) {
                            fs.Write(templateBuffer, 0, templateBuffer.Length);
                            if (token.IsCancellationRequested) {
                                break;
                            }
                        }
                        fs.Write(templateBuffer, 0, (int)amt);
                        fs.Flush();
                    }
                    Nira.FixTimestamp(path, Timestamp);
                    Nira.FixTimestamp(path.Directory, Timestamp);
                }
                catch (Exception e) {
                    Errors.Add(new Error { Path = path.FullName, Message = e.Message });
                }
            }, null);
        }

        object obzekt = new object();
        static DateTime Timestamp = new DateTime(1999, 12, 31, 23, 59, 0);

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

        private void TxtSrc_PreviewDragOver(object sender, DragEventArgs e)
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
