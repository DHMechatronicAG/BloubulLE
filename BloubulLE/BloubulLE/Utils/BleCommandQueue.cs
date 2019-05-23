using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace DH.BloubulLE.Utils
{
    public class BleCommandQueue
    {
        private readonly Object _lock = new Object();
        private IBleCommand _currentCommand;

        public Queue<IBleCommand> CommandQueue { get; set; }

        public Task<T> EnqueueAsync<T>(Func<Task<T>> bleCommand, Int32 timeOutInSeconds = 10)
        {
            BleCommand<T> command = new BleCommand<T>(bleCommand, timeOutInSeconds);
            lock (this._lock)
            {
                this.CommandQueue.Enqueue(command);
            }

            this.TryExecuteNext();
            return command.ExecutingTask;
        }

        public void CancelPending()
        {
            lock (this._lock)
            {
                foreach (IBleCommand command in this.CommandQueue) command.Cancel();
                this.CommandQueue.Clear();
            }
        }

        private async void TryExecuteNext()
        {
            lock (this._lock)
            {
                if (this._currentCommand != null || !this.CommandQueue.Any()) return;

                this._currentCommand = this.CommandQueue.Dequeue();
            }

            await this._currentCommand.ExecuteAsync();

            lock (this._lock)
            {
                this._currentCommand = null;
            }

            this.TryExecuteNext();
        }
    }


    public interface IBleCommand
    {
        Boolean IsExecuting { get; }
        Int32 TimeoutInMiliSeconds { get; }
        Task ExecuteAsync();
        void Cancel();
    }

    public class BleCommand<T> : IBleCommand
    {
        private readonly TaskCompletionSource<T> _taskCompletionSource;
        private readonly Func<Task<T>> _taskSource;

        public BleCommand(Func<Task<T>> taskSource, Int32 timeoutInSeconds)
        {
            this._taskSource = taskSource;
            this.TimeoutInMiliSeconds = timeoutInSeconds;
            this._taskCompletionSource = new TaskCompletionSource<T>();
        }

        public Task<T> ExecutingTask => this._taskCompletionSource.Task;

        public Int32 TimeoutInMiliSeconds { get; }

        public Boolean IsExecuting { get; private set; }

        public async Task ExecuteAsync()
        {
            try
            {
                this.IsExecuting = true;
                Task<T> source = this._taskSource();
                if (source != await Task.WhenAny(source, Task.Delay(this.TimeoutInMiliSeconds)))
                    throw new TimeoutException("Timed out while executing ble task.");

                this._taskCompletionSource.TrySetResult(await source);
            }
            catch (TaskCanceledException)
            {
                this._taskCompletionSource.TrySetCanceled();
            }
            catch (Exception ex)
            {
                this._taskCompletionSource.TrySetException(ex);
            }
            finally
            {
                this.IsExecuting = false;
            }
        }

        public void Cancel()
        {
            this._taskCompletionSource.TrySetCanceled();
        }
    }
}