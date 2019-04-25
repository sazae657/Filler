using System;
using System.Collections.Generic;
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
using System.Windows.Threading;
using ﾆﾗ;

namespace NiraControl
{
    /// <summary>
    /// UserControl1.xaml の相互作用ロジック
    /// </summary>
    public partial class NiraDisk : UserControl
    {
        DispatcherTimer dispatcherTimer;
        public NiraDisk()
        {
            InitializeComponent();
            dispatcherTimer = new DispatcherTimer(DispatcherPriority.Normal);
            dispatcherTimer.Interval = new TimeSpan(0, 0, 0, 1);
            dispatcherTimer.Tick += DispatcherTimer_Tick;
            dispatcherTimer.Start();
        }

        private void DispatcherTimer_Tick(object sender, EventArgs e)
        {
            UpdateDisplay();
        }

        void UpdateDisplay()
        {
            long total = 0;
            var df = Nira.GetDriveFreeSpace(CurrentDrive, out total);
            var n = (double)df / total;
            lubel.Content = $"{CurrentDrive}: {df}/{total} ({100 - (n * 100):F2})";
            mrogressBar.Value = (int)(100 - (n * 100));
        }

        protected override void OnInitialized(EventArgs e)
        {
            base.OnInitialized(e);
            ChangeDrive(Drive);
            UpdateDisplay();
        }

        protected string CurrentDrive { get; set; }

        public static DependencyProperty DriveProperty =
             DependencyProperty.Register(
                 "Drive",
                 typeof(string),
                 typeof(NiraDisk),
                 new PropertyMetadata("C"));

        public string Drive {
            get => (string)GetValue(DriveProperty);
            set {
                SetValue(DriveProperty, value);
                ChangeDrive(value);
            }
        }

        void ChangeDrive(string path)
        {
            CurrentDrive = path;
            lubel.Content = CurrentDrive;
        }


    }
}
