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
        object obzekt = new object();
        HashSet<Task<bool>> tasks;
        protected HashSet<Task<bool>> Tasks {
            [MethodImpl(MethodImplOptions.Synchronized)]
            get => tasks;
        }


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

        private void AddTask(Task<bool> task)
        {
            lock (obzekt) {
                tasks.Add(task);
                TaskCount++;
            }
        }

        private void RemveTask(Task<bool> task)
        {
            lock (obzekt) {
                tasks.Remove(task);
                TaskCount--;
            }
        }

        public void Omit()
        {
            tasks = new HashSet<Task<bool>>();
            TaskCount = 0;
            cancellationTokenSource?.Dispose();
            cancellationTokenSource = new CancellationTokenSource();
            StopRequested = false;
        }

        public IEnumerable<bool> Wait()
        {           
            var boo = new List<bool>();
            foreach (var n in Tasks.ToArray()) {
                if (null != n) {
                    boo.Add(n.Result);
                }
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
                AddTask(task);
                delegatey?.Invoke(token);
                return true;
            }, token);
            task.ContinueWith(x =>
            {
                RemveTask(task);
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
