using System;
using UnityEngine;

namespace AppCat.Internal
{
    /// <summary>
    /// Picks the best available backend at runtime.
    ///
    /// Priority:
    ///   1. iOS native (device builds)
    ///   2. Android native (device builds)
    ///   3. Managed core (Editor, desktop, WebGL, or when a native bridge is missing)
    ///
    /// Never throws. Returns null only when the managed core DLL is also absent.
    /// </summary>
    internal static class BackendResolver
    {
        private static ICoreBackend _resolved;
        private static bool _resolvedOnce;

        public static ICoreBackend Resolve()
        {
            if (_resolvedOnce) return _resolved;
            _resolvedOnce = true;

            _resolved = TryNative() ?? TryManaged();
            return _resolved;
        }

        public static void Reset()
        {
            _resolved = null;
            _resolvedOnce = false;
        }

        private static ICoreBackend TryNative()
        {
            try
            {
#if UNITY_IOS && !UNITY_EDITOR
                return new IosCoreBackend(UnityMainThreadDispatcher.Instance);
#elif UNITY_ANDROID && !UNITY_EDITOR
                return new AndroidCoreBackend(UnityMainThreadDispatcher.Instance);
#else
                return null;
#endif
            }
            catch (Exception e)
            {
                Debug.LogWarning("[AppCat] native backend unavailable, falling back to managed core: " + e.Message);
                return null;
            }
        }

        private static ICoreBackend TryManaged()
        {
            try
            {
                Core.IHttpTransport transport = null;
#if UNITY_WEBGL
                transport = new UnityWebRequestTransport();
#endif
                return new ManagedCoreBackend(
                    new UnityDeviceContextProvider(),
                    transport,
                    LogSink);
            }
            catch (Exception e)
            {
                // Typically: AppCatCore.Unity.dll missing from Runtime/Plugins.
                Debug.LogWarning("[AppCat] managed core unavailable: " + e.Message);
                return null;
            }
        }

        private static void LogSink(int level, string message)
        {
            switch (level)
            {
                case 3: Debug.LogError(message); break;
                case 2: Debug.LogWarning(message); break;
                default: Debug.Log(message); break;
            }
        }
    }
}
