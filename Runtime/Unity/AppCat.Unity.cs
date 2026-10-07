using AppCat.Internal;
using UnityEngine;

namespace AppCat
{
    /// <summary>
    /// Unity half of the <see cref="AppCat"/> partial class. Wires the real
    /// backend resolver, main-thread dispatcher and Debug.Log sink. The pure
    /// half (AppCat.cs) compiles without UnityEngine so its logic is testable
    /// with plain dotnet.
    /// </summary>
    public static partial class AppCat
    {
        private static bool _platformConfigured;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Bootstrap()
        {
            ConfigurePlatformDefaults();
        }

        static partial void ConfigurePlatformDefaults()
        {
            if (_platformConfigured) return;
            _platformConfigured = true;

            AppCatLog.IsDevelopment = Debug.isDebugBuild || Application.isEditor;
            AppCatLog.WarnSink = Debug.LogWarning;

            if (Dispatcher == null) Dispatcher = UnityMainThreadDispatcher.Instance;
            if (BackendFactory == null) BackendFactory = BackendResolver.Resolve;
        }

        static partial void ResetPlatform()
        {
            BackendResolver.Reset();
        }
    }
}
