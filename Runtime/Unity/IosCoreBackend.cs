#if UNITY_IOS && !UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using AOT;

namespace AppCat.Internal
{
    /// <summary>
    /// iOS native backend. P/Invokes the @_cdecl entry points exported by
    /// Plugins/iOS/AppCatUnityBridge.swift, which delegate to AppCatCoreKit.
    /// Payloads cross the boundary as JSON strings; async results come back
    /// through a single static callback keyed by request id.
    /// </summary>
    internal sealed class IosCoreBackend : ICoreBackend
    {
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        private delegate void NativeCallback(int requestId, string json, string errorCode, string errorMessage);

        [DllImport("__Internal")] private static extern void appcat_unity_configure(string appId, string apiKey, string optionsJson, int requestId, NativeCallback callback);
        [DllImport("__Internal")] private static extern void appcat_unity_identify(string dataJson, int requestId, NativeCallback callback);
        [DllImport("__Internal")] private static extern void appcat_unity_send_event(string eventName, string paramsJson, string optionsJson);
        [DllImport("__Internal")] private static extern IntPtr appcat_unity_get_attribution();
        [DllImport("__Internal")] private static extern IntPtr appcat_unity_get_device_context();
        [DllImport("__Internal")] private static extern IntPtr appcat_unity_get_appcat_id();
        [DllImport("__Internal")] [return: MarshalAs(UnmanagedType.I1)] private static extern bool appcat_unity_is_disabled();
        [DllImport("__Internal")] private static extern void appcat_unity_set_log_level(int level);
        [DllImport("__Internal")] private static extern void appcat_unity_set_tracking_consent([MarshalAs(UnmanagedType.I1)] bool granted, int requestId, NativeCallback callback);
        [DllImport("__Internal")] private static extern void appcat_unity_free(IntPtr ptr);

        private static NativeCallbackRegistry _registry;
        private static readonly NativeCallback Callback = OnNativeResult;

        public bool IsNative => true;

        public IosCoreBackend(IDispatcher dispatcher)
        {
            _registry = new NativeCallbackRegistry(dispatcher);
        }

        [MonoPInvokeCallback(typeof(NativeCallback))]
        private static void OnNativeResult(int requestId, string json, string errorCode, string errorMessage)
        {
            _registry?.Complete(requestId, json, errorCode, errorMessage);
        }

        public async Task<Dictionary<string, object>> Configure(string appId, string apiKey, Dictionary<string, object> options)
        {
            var id = _registry.Register(out var task);
            appcat_unity_configure(appId ?? string.Empty, apiKey, AppCatJson.Encode(options) ?? "{}", id, Callback);
            return AppCatJson.DecodeObject(await task);
        }

        public async Task<Dictionary<string, object>> Identify(Dictionary<string, object> data)
        {
            var id = _registry.Register(out var task);
            appcat_unity_identify(AppCatJson.Encode(data) ?? "{}", id, Callback);
            return AppCatJson.DecodeObject(await task);
        }

        public void SendEvent(string eventName, Dictionary<string, object> parameters, Dictionary<string, object> options)
        {
            appcat_unity_send_event(eventName, AppCatJson.Encode(parameters), AppCatJson.Encode(options));
        }

        public Task<Dictionary<string, object>> GetAttribution()
        {
            return Task.FromResult(AppCatJson.DecodeObject(TakeString(appcat_unity_get_attribution())));
        }

        public Task<Dictionary<string, object>> GetDeviceContext()
        {
            return Task.FromResult(AppCatJson.DecodeObject(TakeString(appcat_unity_get_device_context())));
        }

        public Task<string> GetAppCatId()
        {
            return Task.FromResult(TakeString(appcat_unity_get_appcat_id()) ?? string.Empty);
        }

        public Task<bool> IsDisabled() => Task.FromResult(appcat_unity_is_disabled());

        public void SetLogLevel(int level) => appcat_unity_set_log_level(level);

        public async Task SetTrackingConsent(bool granted)
        {
            var id = _registry.Register(out var task);
            appcat_unity_set_tracking_consent(granted, id, Callback);
            await task;
        }

        /// <summary>Copies a strdup'd native string and frees it.</summary>
        private static string TakeString(IntPtr ptr)
        {
            if (ptr == IntPtr.Zero) return null;
            try { return Marshal.PtrToStringAnsi(ptr); }
            finally { appcat_unity_free(ptr); }
        }
    }
}
#endif
