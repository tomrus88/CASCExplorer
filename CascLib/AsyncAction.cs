using System;
using System.Threading;
using System.Threading.Tasks;

namespace CASCExplorer
{
    public class AsyncActionProgressChangedEventArgs : EventArgs
    {
        public int Progress { get; private set; }
        public object UserData { get; private set; }

        public AsyncActionProgressChangedEventArgs(int progress)
        {
            Progress = progress;
        }

        public AsyncActionProgressChangedEventArgs(int progress, object userData) : this(progress)
        {
            UserData = userData;
        }
    }

    delegate void AsyncActionProgressEventHandler(object sender, AsyncActionProgressChangedEventArgs e);

    public class AsyncActionProgress : Progress<AsyncActionProgressChangedEventArgs>
    {
        public AsyncActionProgress(Action<AsyncActionProgressChangedEventArgs> action) : base(action) { }
        public AsyncActionProgress() : base() { }

        public void Report(AsyncActionProgressChangedEventArgs value)
        {
            base.OnReport(value);
        }
    }

    public class AsyncAction
    {
        private AsyncActionProgress progress;
        private CancellationTokenSource cts;
        private Task task;
        private Action action;
        //private int lastPercent;

        public event EventHandler<AsyncActionProgressChangedEventArgs> ProgressChanged
        {
            add { progress.ProgressChanged += value; }
            remove { progress.ProgressChanged -= value; }
        }

        public bool IsCancellationRequested
        {
            get { return cts.IsCancellationRequested; }
        }

        public AsyncAction(Action action, Action<AsyncActionProgressChangedEventArgs> progressAction = null)
        {
            this.action = action;

            if (progressAction != null)
                progress = new AsyncActionProgress(progressAction);
            else
                progress = new AsyncActionProgress();
        }

        public async Task DoAction()
        {
            cts = new CancellationTokenSource();
            task = Task.Factory.StartNew(action, cts.Token);
            await task;
        }

        public void ReportProgress(int percent, object userData = null)
        {
            if (cts.IsCancellationRequested)
                return;

            //if (lastPercent != percent)
            //{
            //    lastPercent = percent;
            //if (percent == -2147483648)
            //    Console.WriteLine();

            progress.Report(new AsyncActionProgressChangedEventArgs(percent, userData));
            //}
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
