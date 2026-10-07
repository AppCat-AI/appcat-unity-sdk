using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using AppCat.Internal;

namespace AppCat
{
    /// <summary>
    /// AppCat SDK for Unity.
    ///
    /// Deferred deep link resolution and attribution for Unity games.
    /// Supports three backends, resolved automatically:
    ///   1. iOS native (AppCatCoreKit.xcframework): full signal fidelity
    ///   2. Android native (appcat-core.aar): full signal fidelity
    ///   3. Managed core (AppCatCore.Unity.dll): Editor, desktop, WebGL
    ///
    /// <code>
    /// var res = await AppCat.Init(new AppCatConfig { AppId = "...", ApiKey = "..." });
    /// AppCat.SendEvent("Purchase", new Dictionary&lt;string, object&gt; { ["value"] = 9.99, ["currency"] = "USD" });
    /// var identity = await AppCat.Identify(new IdentifyData { UserId = "u1", Email = "a@b.com" });
    /// </code>
    ///
    /// Hardening contract:
    ///   - <see cref="Init"/> is the ONLY method that throws (an <see cref="AppCatException"/>).
    ///   - Every other method swallows errors, invokes <see cref="AppCatConfig.OnError"/>
    ///     when provided, and returns a typed fallback. The host game is never
    ///     taken down by an exception from this SDK.
    ///   - All callbacks and task completions are delivered on the Unity main thread.
    /// </summary>
    public static partial class AppCat
    {
        private static readonly object Gate = new object();

        private static bool _initialized;
        private static bool _isNativeBackend;
        private static Action<Exception> _onError;
        private static ICoreBackend _backend;

        /// <summary>Platform hook: supplies the real backend resolver, dispatcher and logger.</summary>
        static partial void ConfigurePlatformDefaults();

        // Injected by the Unity bootstrap (or tests). Null until then.
        internal static Func<ICoreBackend> BackendFactory;
        internal static IDispatcher Dispatcher;

        // -------------------------------------------------------------------
        // Public API
        // -------------------------------------------------------------------

        /// <summary>Whether <see cref="Init"/> has completed successfully.</summary>
        public static bool IsInitialized
        {
            get { lock (Gate) return _initialized; }
        }

        /// <summary>
        /// Whether the SDK is using a native backend (true) or the managed
        /// fallback (false). Only meaningful after <see cref="Init"/>.
        /// </summary>
        public static bool IsNativeBackend
        {
            get { lock (Gate) return _isNativeBackend; }
        }

        /// <summary>
        /// Initializes the SDK and resolves attribution plus deferred deep links.
        ///
        /// This is the one method that throws so the host can observe setup
        /// failures. Subsequent calls are idempotent and return an empty response.
        /// </summary>
        /// <exception cref="AppCatException">
        /// <see cref="AppCatErrorCode.InvalidApiKey"/>, <see cref="AppCatErrorCode.NoBackend"/>
        /// or <see cref="AppCatErrorCode.ConfigureFailed"/> (wrapping the cause).
        /// </exception>
        public static async Task<InitResponse> Init(AppCatConfig config)
        {
            lock (Gate)
            {
                if (_initialized) return InitResponse.Empty;
            }

            if (config == null || string.IsNullOrEmpty(config.ApiKey))
            {
                throw new AppCatException(AppCatErrorCode.InvalidApiKey, "[AppCat] ApiKey is required.");
            }

            if (config.OnError != null)
            {
                lock (Gate) _onError = config.OnError;
            }

            var backend = ResolveBackend();
            if (backend == null)
            {
                throw new AppCatException(
                    AppCatErrorCode.NoBackend,
                    "[AppCat] No backend available.\n\n" +
                    "Either:\n" +
                    "  - Build for iOS/Android with the vendored native cores (Runtime/Plugins/iOS, Runtime/Plugins/Android)\n" +
                    "  - Ensure Runtime/Plugins/AppCatCore.Unity.dll is present for Editor/desktop/WebGL\n");
            }

            Dictionary<string, object> raw;
            try
            {
                var options = new Dictionary<string, object>
                {
                    ["isDebug"] = config.IsDebug,
                    ["logLevel"] = (int)config.LogLevel,
                    ["customerUserId"] = config.CustomerUserId,
                };
                raw = await backend.Configure(config.AppId ?? string.Empty, config.ApiKey, options);
            }
            catch (AppCatException)
            {
                throw;
            }
            catch (Exception e)
            {
                throw new AppCatException(AppCatErrorCode.ConfigureFailed, Describe(e) ?? "configure() failed", e);
            }

            try { backend.SetLogLevel((int)config.LogLevel); }
            catch { /* older native builds may lack setLogLevel */ }

            lock (Gate)
            {
                _backend = backend;
                _isNativeBackend = backend.IsNative;
                _initialized = true;
            }

            return ResponseMapper.ToInitResponse(raw);
        }

        /// <summary>Callback form of <see cref="Init(AppCatConfig)"/>. Never throws.</summary>
        public static void Init(AppCatConfig config, Action<InitResponse> onSuccess, Action<AppCatException> onError = null)
        {
            RunCallback(Init(config), onSuccess, onError);
        }

        /// <summary>
        /// Enriches the attribution profile with user identity. Best-effort:
        /// returns null before init or on any error.
        /// </summary>
        public static async Task<IdentifyResponse> Identify(IdentifyData data)
        {
            if (!AssertInitialized("Identify", out var backend)) return null;
            try
            {
                var raw = await backend.Identify(data?.ToDictionary() ?? new Dictionary<string, object>());
                return ResponseMapper.ToIdentifyResponse(raw);
            }
            catch (Exception e)
            {
                ReportError(e);
                return null;
            }
        }

        /// <summary>Callback form of <see cref="Identify(IdentifyData)"/>.</summary>
        public static void Identify(IdentifyData data, Action<IdentifyResponse> onComplete)
        {
            RunCallback(Identify(data), onComplete, null);
        }

        /// <summary>
        /// Tracks a conversion event. Fire-and-forget, never throws.
        ///
        /// Pass all event data in one flat dictionary. Reserved keys
        /// (<c>eventId</c>, <c>value</c>, <c>currency</c>, <c>testEventCode</c>)
        /// are forwarded to the ad platform payload; all other keys become
        /// <c>custom_data</c>.
        /// </summary>
        public static void SendEvent(string eventName, IDictionary<string, object> parameters = null)
        {
            if (!AssertInitialized("SendEvent", out var backend)) return;
            try
            {
                if (EventParamSplitter.IsMissingRevenueFields(eventName, parameters))
                {
                    AppCatLog.Warn($"'{eventName}' is missing value or currency. Meta and TikTok will silently drop this event without both fields.");
                }
                EventParamSplitter.Split(parameters, out var customData, out var options);
                backend.SendEvent(eventName, customData, options);
            }
            catch (Exception e)
            {
                ReportError(e);
            }
        }

        /// <summary>
        /// Cached attribution: the identify-cached profile when available,
        /// otherwise the resolve enrichment. Null if neither has run or on error.
        /// </summary>
        public static async Task<IReadOnlyDictionary<string, object>> GetAttribution()
        {
            if (!AssertInitialized("GetAttribution", out var backend)) return null;
            try { return await backend.GetAttribution(); }
            catch (Exception e) { ReportError(e); return null; }
        }

        /// <summary>Cached device context collected by the active backend.</summary>
        public static async Task<IReadOnlyDictionary<string, object>> GetDeviceContext()
        {
            if (!AssertInitialized("GetDeviceContext", out var backend)) return null;
            try { return await backend.GetDeviceContext(); }
            catch (Exception e) { ReportError(e); return null; }
        }

        /// <summary>
        /// Stable AppCat device identifier: IDFV on iOS, Android ID on Android,
        /// <c>SystemInfo.deviceUniqueIdentifier</c> elsewhere. Empty string on failure.
        /// </summary>
        public static async Task<string> GetAppCatId()
        {
            if (!AssertInitialized("GetAppCatId", out var backend)) return string.Empty;
            try { return await backend.GetAppCatId() ?? string.Empty; }
            catch (Exception e) { ReportError(e); return string.Empty; }
        }

        /// <summary>
        /// Whether the SDK has been remotely disabled (invalid key, kill switch,
        /// compliance hold). False on any failure.
        /// </summary>
        public static async Task<bool> IsDisabled()
        {
            if (!AssertInitialized("IsDisabled", out var backend)) return false;
            try { return await backend.IsDisabled(); }
            catch (Exception e) { ReportError(e); return false; }
        }

        /// <summary>
        /// Records the user's tracking-consent choice (ATT prompt, GDPR banner,
        /// settings toggle). When false, AppCat stops forwarding PII such as
        /// email and phone to ad networks. Best-effort, never throws.
        /// </summary>
        public static async Task SetTrackingConsent(bool granted)
        {
            if (!AssertInitialized("SetTrackingConsent", out var backend)) return;
            try { await backend.SetTrackingConsent(granted); }
            catch (Exception e) { ReportError(e); }
        }

        /// <summary>Resets all SDK state. Primarily for tests.</summary>
        public static void Reset()
        {
            lock (Gate)
            {
                _initialized = false;
                _isNativeBackend = false;
                _onError = null;
                _backend = null;
            }
            ResetPlatform();
        }

        /// <summary>Platform hook: clears cached backend state.</summary>
        static partial void ResetPlatform();

        // -------------------------------------------------------------------
        // Internal
        // -------------------------------------------------------------------

        private static ICoreBackend ResolveBackend()
        {
            ConfigurePlatformDefaults();
            try
            {
                return BackendFactory?.Invoke();
            }
            catch (Exception e)
            {
                ReportError(e);
                return null;
            }
        }

        private static bool AssertInitialized(string caller, out ICoreBackend backend)
        {
            lock (Gate)
            {
                backend = _backend;
                if (_initialized && backend != null) return true;
            }
            var msg = $"{caller}() called before Init(). Call AppCat.Init() first.";
            AppCatLog.Warn(msg);
            ReportError(new AppCatException(AppCatErrorCode.NotConfigured, "[AppCat] " + msg));
            return false;
        }

        private static void ReportError(Exception e)
        {
            Action<Exception> handler;
            lock (Gate) handler = _onError;
            if (handler == null) return;
            var dispatcher = Dispatcher ?? new InlineDispatcher();
            try
            {
                dispatcher.Post(() =>
                {
                    try { handler(e); } catch { /* host callback threw; never crash */ }
                });
            }
            catch { /* dispatcher unavailable (shutting down) */ }
        }

        private static void RunCallback<T>(Task<T> task, Action<T> onSuccess, Action<AppCatException> onError)
        {
            task.ContinueWith(t =>
            {
                var dispatcher = Dispatcher ?? new InlineDispatcher();
                dispatcher.Post(() =>
                {
                    try
                    {
                        if (t.IsFaulted)
                        {
                            var inner = t.Exception?.GetBaseException();
                            var typed = inner as AppCatException
                                ?? new AppCatException(AppCatErrorCode.Internal, Describe(inner) ?? "unknown error", inner);
                            onError?.Invoke(typed);
                        }
                        else
                        {
                            onSuccess?.Invoke(t.Result);
                        }
                    }
                    catch { /* host callback threw; never crash */ }
                });
            }, TaskScheduler.Default);
        }

        private static string Describe(Exception e)
        {
            if (e == null) return null;
            try { return e.Message; } catch { return "unknown error"; }
        }
    }
}
