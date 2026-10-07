using System;
using System.Collections.Generic;
using System.Threading;
using UnityEngine;

namespace AppCat.Internal
{
    /// <summary>
    /// Hidden, persistent MonoBehaviour that drains a queue of actions on the
    /// Unity main thread each frame. Actions posted from the main thread while
    /// the player is running are still queued (never run inline) so callers
    /// observe consistent ordering.
    /// </summary>
    internal sealed class UnityMainThreadDispatcher : MonoBehaviour, IDispatcher
    {
        private static UnityMainThreadDispatcher _instance;
        private static readonly object InstanceGate = new object();
        private static int _mainThreadId = -1;

        private readonly Queue<Action> _queue = new Queue<Action>();
        private readonly List<Action> _drain = new List<Action>();

        public static IDispatcher Instance
        {
            get
            {
                lock (InstanceGate)
                {
                    if (_instance != null) return _instance;
                }

                // Only the main thread can create GameObjects. If we are off-thread
                // and no instance exists yet, fall back to inline execution.
                if (_mainThreadId != -1 && Thread.CurrentThread.ManagedThreadId != _mainThreadId)
                {
                    return new InlineDispatcher();
                }

                try
                {
                    var go = new GameObject("[AppCat]") { hideFlags = HideFlags.HideAndDontSave };
                    var d = go.AddComponent<UnityMainThreadDispatcher>();
                    if (Application.isPlaying) DontDestroyOnLoad(go);
                    lock (InstanceGate) _instance = d;
                    _mainThreadId = Thread.CurrentThread.ManagedThreadId;
                    return d;
                }
                catch
                {
                    // Edit mode without a scene, or shutting down.
                    return new InlineDispatcher();
                }
            }
        }

        public void Post(Action action)
        {
            if (action == null) return;
            lock (_queue) _queue.Enqueue(action);
        }

        private void Update()
        {
            lock (_queue)
            {
                if (_queue.Count == 0) return;
                _drain.AddRange(_queue);
                _queue.Clear();
            }
            foreach (var a in _drain)
            {
                try { a(); }
                catch (Exception e) { Debug.LogException(e); }
            }
            _drain.Clear();
        }

        private void OnDestroy()
        {
            lock (InstanceGate)
            {
                if (_instance == this) _instance = null;
            }
        }
    }
}
