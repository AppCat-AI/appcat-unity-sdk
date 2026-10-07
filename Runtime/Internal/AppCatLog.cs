using System;

namespace AppCat.Internal
{
    /// <summary>
    /// Wrapper-level logger. Unity installs a Debug.Log sink at startup;
    /// outside Unity (tests) the default sink is a no-op. Warnings are only
    /// emitted in development builds, matching the RN wrapper's __DEV__ guard.
    /// </summary>
    internal static class AppCatLog
    {
        private const string Prefix = "[AppCat] ";

        /// <summary>Set by the Unity bootstrap: true in Editor / Development builds.</summary>
        internal static bool IsDevelopment;

        internal static Action<string> WarnSink = _ => { };

        public static void Warn(string message)
        {
            if (!IsDevelopment) return;
            try { WarnSink(Prefix + message); } catch { /* never let logging crash the host */ }
        }
    }
}
