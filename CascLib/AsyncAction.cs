using System;
using System.Threading;
using System.Threading.Tasks;

namespace CASCExplorer
{
    public class AsyncAction
    {
        private Progress<int> progress;
        private CancellationTokenSource cts;
        private Task task;
        private Action action;

        public event EventHandler<int> ProgressChanged
        {
            add { progress.ProgressChanged += value; }
            remove { progress.ProgressChanged -= value; }
        }

        public bool IsCancellationRequested
        {
            get { return cts.IsCancellationRequested; }
        }

        public AsyncAction(Action action, Action<int> progressAction = null)
        {
            this.action = action;

            if (progressAction != null)
                progress = new Progress<int>(progressAction);
            else
                progress = new Progress<int>();

            cts = new CancellationTokenSource();
        }

        public async Task DoAction()
        {
            task = Task.Factory.StartNew(action, cts.Token);
            await task;
        }

        public void ReportProgress(int percent)
        {
            if (cts.IsCancellationRequested)
                return;
            (progress as IProgress<int>).Report(percent);
        }

        public void ThrowOnCancel()
        {
            cts.Token.ThrowIfCancellationRequested();
        }

        public void Cancel()
        {
            if (!task.IsCompleted)
                cts.Cancel();
        }
    }
}
