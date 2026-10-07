using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace AppCat.Internal
{
    /// <summary>
    /// Correlates asynchronous native completions (Swift / Kotlin) with the
    /// C# Task that is awaiting them. Native code receives an integer request
    /// id and echoes it back; completion is marshalled to the main thread
    /// before the Task settles.
    ///
    /// Shared by both native backends and fully unit-testable without Unity.
    /// </summary>
    internal sealed class NativeCallbackRegistry
    {
        private readonly object _gate = new object();
        private readonly Dictionary<int, TaskCompletionSource<string>> _pending = new Dictionary<int, TaskCompletionSource<string>>();
        private readonly IDispatcher _dispatcher;
        private int _nextId;

        public NativeCallbackRegistry(IDispatcher dispatcher)
        {
            _dispatcher = dispatcher ?? new InlineDispatcher();
        }

        public int PendingCount
        {
            get { lock (_gate) return _pending.Count; }
        }

        /// <summary>Registers a new request and returns its id plus the task to await.</summary>
        public int Register(out Task<string> task)
        {
            var tcs = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);
            int id;
            lock (_gate)
            {
                id = Interlocked.Increment(ref _nextId);
                _pending[id] = tcs;
            }
            task = tcs.Task;
            return id;
        }

        /// <summary>Completes a request. Unknown ids are ignored (duplicate native callbacks).</summary>
        public void Complete(int id, string json, string errorCode, string errorMessage)
        {
            TaskCompletionSource<string> tcs;
            lock (_gate)
            {
                if (!_pending.TryGetValue(id, out tcs)) return;
                _pending.Remove(id);
            }

            _dispatcher.Post(() =>
            {
                if (!string.IsNullOrEmpty(errorCode))
                {
                    tcs.TrySetException(new AppCatException(
                        AppCatException.CodeFromNative(errorCode),
                        string.IsNullOrEmpty(errorMessage) ? errorCode : errorMessage));
                }
                else
                {
                    tcs.TrySetResult(json);
                }
            });
        }

        /// <summary>Fails every in-flight request (used on reset / shutdown).</summary>
        public void FailAll(string reason)
        {
            List<TaskCompletionSource<string>> all;
            lock (_gate)
            {
                all = new List<TaskCompletionSource<string>>(_pending.Values);
                _pending.Clear();
            }
            foreach (var tcs in all)
            {
                tcs.TrySetException(new AppCatException(AppCatErrorCode.Internal, reason));
            }
        }
    }
}
