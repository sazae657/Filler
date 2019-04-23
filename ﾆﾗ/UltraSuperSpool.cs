using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;


namespace ﾆﾗ
{
    public class UltraSuperSpool
    {
        protected HashSet<Task<bool>> Tasks {
            [MethodImpl(MethodImplOptions.Synchronized)]
            get;
            [MethodImpl(MethodImplOptions.Synchronized)]
            private set;
        } = new HashSet<Task<bool>>();

        public int TaskCount {
            [MethodImpl(MethodImplOptions.Synchronized)]
            get;
            [MethodImpl(MethodImplOptions.Synchronized)]
            private set;
        } = 0;

        public ClosableStream CreateClosableStream(Stream stream)
        {
            return new ClosableStream(this, stream);
        }
    }
}
