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
            DataContext = this;
            spool = new UltraSuperSpool();
            spool.Omit();
        }
        public RejectablePrperty<string> Dest { get; set; }
        public ObservableCollection<string> History { get; set; }
        UltraSuperSpool spool;


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

        private void Window_Drop(object sender, DragEventArgs e)
        {
            if (string.IsNullOrEmpty(Dest.Value)) {
                MessageBox.Show(this, $"フォルダーが設定されていないよう");
                return;
            }
            var dest = Dest.Value;
            foreach (var n in
                from k in e.Data.GetData(DataFormats.FileDrop) as string[]
                where File.Exists(k) select new FileInfo(k)) {
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
                spool.Schedule(t =>
                {
                    File.Move(n.FullName, d.FullName);
                }, null);
            }

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
