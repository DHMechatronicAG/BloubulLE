using System;
using System.Threading;
using System.Threading.Tasks;

namespace DH.BloubulLE.Utils
{
    public static class TaskBuilder
    {
        public static Task<TReturn> FromEvent<TReturn, TEventHandler>(
            Action execute,
            Func<Action<TReturn>, Action<Exception>, TEventHandler> getCompleteHandler,
            Action<TEventHandler> subscribeComplete,
            Action<TEventHandler> unsubscribeComplete,
            CancellationToken token = default(CancellationToken))
        {
            return FromEvent<TReturn, TEventHandler, Object>(
                execute, getCompleteHandler, subscribeComplete, unsubscribeComplete,
                reject => null,
                handler => { },
                handler => { },
                token);
        }

        public static async Task<TReturn> FromEvent<TReturn, TEventHandler, TRejectHandler>(
            Action execute,
            Func<Action<TReturn>, Action<Exception>, TEventHandler> getCompleteHandler,
            Action<TEventHandler> subscribeComplete,
            Action<TEventHandler> unsubscribeComplete,
            Func<Action<Exception>, TRejectHandler> getRejectHandler,
            Action<TRejectHandler> subscribeReject,
            Action<TRejectHandler> unsubscribeReject,
            CancellationToken token = default(CancellationToken))
        {
            TaskCompletionSource<TReturn> tcs = new TaskCompletionSource<TReturn>();
            Action<TReturn> complete = args => tcs.TrySetResult(args);
            Action<Exception> completeException = ex => tcs.TrySetException(ex);
            Action<Exception> reject = ex => tcs.TrySetException(ex);

            TEventHandler handler = getCompleteHandler(complete, completeException);
            TRejectHandler rejectHandler = getRejectHandler(reject);

            try
            {
                subscribeComplete(handler);
                subscribeReject(rejectHandler);
                using (token.Register(() => tcs.TrySetCanceled(), false))
                {
                    execute();
                    return await tcs.Task;
                }
            }
            finally
            {
                unsubscribeReject(rejectHandler);
                unsubscribeComplete(handler);
            }
        }
    }
}