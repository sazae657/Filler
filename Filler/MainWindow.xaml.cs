using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
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
        }

        bool running = false;
        private CancellationTokenSource tokenSource = null;
        DirectoryInfo rootDir = null;

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
                Dispatcher.Invoke(() => btnStart.IsEnabled = false);
                long n = 0;
                while (true) {
                    if (token.IsCancellationRequested) {
                        break;
                    }
                    n++;
                    var f = System.IO.Path.Combine(rootDir.FullName, $"{n:D16}.xlsx");
                    Dispatcher.Invoke(() => lblProgress.Content = $"{f} Free:{GetTotalFreeSpace(rootDir.FullName)}");
                    if (!WriteFile(f)) {
                        break;
                    }
                    Thread.Sleep(1);
                }
            }, token).ContinueWith(t =>
            {
                tokenSource.Dispose();
                tokenSource = null;
                running = false;
                Dispatcher.Invoke(() =>
                {
                    btnStart.IsEnabled = true;
                    btnStart.Content = "Start";
                });
            });
        }

        Random rand = new Random();

        const int blockZise = 10240;
        const int blockCount = 10000;

        private bool WriteFile(string path)
        {
            var free = GetTotalFreeSpace(rootDir.FullName);
            if (free < blockZise * blockCount) {
                WriteFile(path, free);
                return false;
            }

            var bytes = new byte[blockZise];
            rand.NextBytes(bytes);
            using (var fs = new FileStream(path, FileMode.OpenOrCreate)) {
                for (int i = 0; i < blockCount; ++i) {
                    if ((i % 10) == 0) {
                        Dispatcher.Invoke(() => lblFileStat.Content = $"{path} {i}/10240");
                    }
                    fs.Write(bytes, 0, bytes.Length);
                }
            }
            return true;
        }
        private void WriteFile(string path, long zise)
        {
            var bytes = new byte[zise];
            rand.NextBytes(bytes);
            using (var fs = new FileStream(path, FileMode.OpenOrCreate)) {
                Dispatcher.Invoke(() => lblFileStat.Content = $"{path} {zise}(F)");
                fs.Write(bytes, 0, bytes.Length);
            }
        }


        private void Prepare()
        {
            rootDir = new DirectoryInfo(txtDest.Text);
            if (!rootDir.Exists) {
                rootDir.Create();
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
    }
}
