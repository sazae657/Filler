using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Text;
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

namespace NCopy
{
    /// <summary>
    /// MainWindow.xaml の相互作用ロジック
    /// </summary>
    public partial class MainWindow : Window
    {
        ObservableCollection<FileInfo> fileList = new ObservableCollection<FileInfo>();
        DirectoryInfo destDir = null;
        bool running = false;

        public MainWindow()
        {
            InitializeComponent();
            listVox.DataContext = fileList;
            var cl = System.Environment.GetCommandLineArgs();
            if (cl.Length > 1) {
                var d = new DirectoryInfo(cl[1]);
                if (d.Exists) {
                    SetDestDir(d);
                }
            }
            
        }

        private void BtnStart_Click(object sender, RoutedEventArgs e)
        {
            if (null == destDir) {
                MessageBox.Show($"コピー先がないよう");
                return;
            }
            if (running) {
                return;
            }
            btnStart.IsEnabled = false;
            progressBar.Minimum = 0;
            progressBar.Maximum = fileList.Count;
            running = true;
            Task.Factory.StartNew(() =>
            {
                int i = 0;
                foreach (var n in fileList) {
                    var dest = System.IO.Path.Combine(destDir.FullName, n.Name);
                    Dispatcher.Invoke(() =>
                    {
                        lblProgress.Content = dest;
                        progressBar.Value = i;
                        listVox.SelectedIndex = i;
                    });
                    i++;
                    if (File.Exists(dest)) {
                        continue;
                    }
                    File.Copy(n.FullName, dest);
                }
            }).ContinueWith(x =>
            {
                Dispatcher.Invoke(()=>{
                    btnStart.IsEnabled = true;
                    fileList.Clear();
                    progressBar.Value = 0;
                    lblProgress.Content = "完了";
                });
            });
        }

        private void TxtDest_Drop(object sender, DragEventArgs e)
        {
            destDir = null;
            var dir = new DirectoryInfo((e.Data.GetData(DataFormats.FileDrop) as string[]).First());
            if (!dir.Exists) {
                MessageBox.Show($"フォルダーがないよう {dir}");
                return;
            }
            SetDestDir(dir);
        }

        private void SetDestDir(DirectoryInfo dir)
        {
            destDir = dir;
            diskInfo.Drive = dir.FullName;
            txtDest.Text = dir.FullName;
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

        private void ListVox_PreviewDragOver(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent(DataFormats.FileDrop, true)) {
                e.Effects = DragDropEffects.Copy;
            }
            else {
                e.Effects = DragDropEffects.None;
            }
            e.Handled = true;
        }

        private void ListVox_Drop(object sender, DragEventArgs e)
        {
            foreach (var n in 
                from x in e.Data.GetData(DataFormats.FileDrop) as string[] select new FileInfo(x)) {
                if (n.Exists) {
                    fileList.Add(n);
                }
            }
        }

        private void ListVox_KeyUp(object sender, KeyEventArgs e)
        {
            if (listVox.SelectedIndex < 0) {
                return;
            }

            fileList.RemoveAt(listVox.SelectedIndex);
        }
    }
}
