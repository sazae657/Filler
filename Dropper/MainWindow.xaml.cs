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
using System.Xml.Linq;
using ﾆﾗ;

namespace Dropper
{
    /// <summary>
    /// MainWindow.xaml の相互作用ロジック
    /// </summary>
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
            Dest = new RejectablePrperty<string>();
            History = new ObservableCollection<string>();
            Move = new RejectablePrperty<bool>();
            Move.Value = true;

            RectColor = new RejectablePrperty<Brush>();
            RectColor.Value = Brushes.White;

            TopMost = new RejectablePrperty<bool>();
            TopMost.Value = false;

            TotalFiles = new RejectablePrperty<int>();
            Progress = new RejectablePrperty<int>();

            DataContext = this;
            spool = new UltraSuperSpool();
            spool.Omit();
        }
        public RejectablePrperty<string> Dest { get; set; }
        public RejectablePrperty<bool> Move { get; set; }

        public RejectablePrperty<bool> TopMost { get; set; }

        public RejectablePrperty<int> TotalFiles { get; set; }
        public RejectablePrperty<int> Progress { get; set; }

        public ObservableCollection<string> History { get; set; }
        UltraSuperSpool spool;

        public RejectablePrperty<Brush> RectColor { get; set; }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            var s = 葱.ｸﾞﾛーﾊﾞﾙ設定ﾌｧｲﾙ作成("Dropper.xml");
            if (!File.Exists(s)) {
                return;
            }
            var d = XDocument.Load(s);
            if (null == d) {
                return;
            }
            foreach (var n in
               from x in d.Element("Dropper")?.Element("History")?.Elements("Path") select x.Value) {
                History.Add(n);
            }
        }

        private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            spool.Dispose();
            var d = new XDocument(
                new XElement("Dropper",
                    new XElement("History",
                        from x in History
                        select new XElement("Path", new XText(x)))
                ));
            d.Save(葱.ｸﾞﾛーﾊﾞﾙ設定ﾌｧｲﾙ作成("Dropper.xml"));
        }

        bool busy = false;

        private void Window_Drop(object sender, DragEventArgs e)
        {
            if (string.IsNullOrEmpty(Dest.Value)) {
                MessageBox.Show(this, $"フォルダーが設定されていないよう");
                return;
            }
            var dest = Dest.Value;
            var move = Move.Value;
            var targets = from k in e.Data.GetData(DataFormats.FileDrop) as string[]
                          where File.Exists(k)
                          select new FileInfo(k);
            if (targets.Count() == 0) {
                return;
            }
            TotalFiles.Value = targets.Count();
            Progress.Value = 0;
            spool.Schedule(t =>
            {
                busy = true;
                foreach (var n in targets) {
                    Dispatcher.Invoke(() => Progress.Value = Progress.Value + 1);
                    n.Refresh();
                    var d = new FileInfo(System.IO.Path.Combine(dest, n.Name));
                    if (!n.Exists || n.FullName.Equals(d.FullName, StringComparison.OrdinalIgnoreCase)) {
                        continue;
                    }
                    if (d.Exists) {
                        if (葱.比較(n, d)) {
                            continue;
                        }
                        if ((d = Rename(d.FullName)) == null) {
                            continue;
                        }
                    }

                    if (move) {
                        File.Move(n.FullName, d.FullName);
                    }
                    else {
                        File.Copy(n.FullName, d.FullName);
                    }
                }
            }, t =>
            {
                busy = false;
                Progress.Value = 0;
            });

        }

        FileInfo Rename(string name)
        {
            var src = new FileInfo(name);
            var ei = name.LastIndexOf('.');
            string ext = null;
            if (ei > 0) {
                ext = name.Substring(ei);
                name = name.Substring(0, ei);
            }
            long n = 0;
            while (true) {
                var t = new FileInfo($"{name}_{n:0000}{ext}");
                if (!t.Exists) {
                    return t;
                }
                if (葱.比較(src, t)) {
                    return null;
                }
                n++;
            }
        }

        private void TextBox_Drop(object sender, DragEventArgs e)
        {
            var dir = new DirectoryInfo((e.Data.GetData(DataFormats.FileDrop) as string[]).First());
            if (!dir.Exists) {
                MessageBox.Show(this, $"フォルダーがないよう {dir}");
                return;
            }
            if (!History.Contains(dir.FullName)) {
                History.Add(dir.FullName);
            }
            Dest.Value = dir.FullName;
        }


        private void Window_PreviewDragOver(object sender, DragEventArgs e)
        {
            if (busy) {
                e.Effects = DragDropEffects.None;
                e.Handled = true;
                return;
            }

            if (e.Data.GetDataPresent(DataFormats.FileDrop, true)) {
                e.Effects = DragDropEffects.Copy;
            }
            else {
                e.Effects = DragDropEffects.None;
            }
            e.Handled = true;
        }

        private void Window_DragEnter(object sender, DragEventArgs e)
        {
            RectColor.Value = Brushes.GhostWhite;
        }

        private void Window_DragLeave(object sender, DragEventArgs e)
        {
            RectColor.Value = Brushes.White;
        }
    }
}
