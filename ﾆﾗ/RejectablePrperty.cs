using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Text;

namespace ﾆﾗ
{
    public class RejectablePrperty<Tv> : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;
        private void RaisePropertyChanged([CallerMemberName]string propertyName = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));

        public RejectablePrperty()
        {
        }

        public RejectablePrperty(PropertyChangedEventHandler handler)
        {
            if (null != handler) {
                PropertyChanged += handler;
            }
        }


        private Tv value;
        public Tv Value {
            get => value;
            set {
                this.value = value;
                RaisePropertyChanged();
            }
        }
    }
}
