using System;

namespace AppCat.Internal
{
    /// <summary>
    /// Marshals work onto the Unity main thread. Native bridges complete their
    /// callbacks on arbitrary threads; everything the host observes (task
    /// completion, OnError) goes through here first.
    /// </summary>
    internal interface IDispatcher
    {
        void Post(Action action);
    }

    /// <summary>Runs inline. Used in tests and on single-threaded targets (WebGL).</summary>
    internal sealed class InlineDispatcher : IDispatcher
    {
        public void Post(Action action)
        {
            action?.Invoke();
        }
    }
}
