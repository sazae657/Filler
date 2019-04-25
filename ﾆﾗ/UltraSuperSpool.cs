using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;


namespace ﾆﾗ
{
    public delegate void UltraSuperDelegatey(CancellationToken token);

    public class UltraSuperSpool : IDisposable
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

        public bool StopRequested {
            [MethodImpl(MethodImplOptions.Synchronized)]
            get;
            [MethodImpl(MethodImplOptions.Synchronized)]
            private set;
        } = false;

        CancellationTokenSource cancellationTokenSource = null;

        public void Stop()
        {
            if (StopRequested) {
                return;
            }
            if (cancellationTokenSource == null) {
                return;
            }
            StopRequested = true;
            if (!cancellationTokenSource.IsCancellationRequested) {
                cancellationTokenSource.Cancel();
            }
        }

        public void Omit()
        {
            Tasks = new HashSet<Task<bool>>();
            TaskCount = 0;
            cancellationTokenSource?.Dispose();
            cancellationTokenSource = new CancellationTokenSource();
            StopRequested = false;
        }

        public IEnumerable<bool> Wait()
        {           
            var boo = new List<bool>();
            foreach (var n in Tasks.ToArray()) {
                boo.Add(n.Result);
            }
            return boo;
        }

        public ClosableStream CreateClosableStream(Stream stream)
        {
            return new ClosableStream(this, stream);
        }

        public void Schedule(UltraSuperDelegatey delegatey, UltraSuperDelegatey completed)
        {
            if (StopRequested) {
                return;
            }
            var token = cancellationTokenSource.Token;

            Task<bool> task = null;
            task = new Task<bool>(() =>
            {
                Tasks.Add(task);
                TaskCount++;
                delegatey?.Invoke(token);
                return true;
            }, token);
            task.ContinueWith(x =>
            {
                Tasks.Remove(task);
                TaskCount--;
                completed?.Invoke(token);
                return true;
            });
            task.Start();
        }

        #region IDisposable Support
        private bool disposedValue = false; // 重複する呼び出しを検出するには

        protected virtual void Dispose(bool disposing)
        {
            if (!disposedValue) {
                if (disposing) {
                    Wait();
                    cancellationTokenSource?.Dispose();
                }
                disposedValue = true;
            }
        }

        // このコードは、破棄可能なパターンを正しく実装できるように追加されました。
        public void Dispose()
        {
            Dispose(true);
        }
        #endregion
    }
}
