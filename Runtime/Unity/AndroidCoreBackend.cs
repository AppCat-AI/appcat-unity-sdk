#if UNITY_ANDROID && !UNITY_EDITOR
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;

namespace AppCat.Internal
{
    /// <summary>
    /// Android native backend. Calls the static methods on
    /// ai.appcat.unity.AppCatUnityBridge (Plugins/Android/AppCatUnityBridge.kt),
    /// which delegate to com.appcat.core.AppCatCore in the vendored AAR.
    /// Payloads cross the boundary as JSON strings; async results come back
    /// through an AndroidJavaProxy keyed by request id.
    /// </summary>
    internal sealed class AndroidCoreBackend : ICoreBackend
    {
        private const string BridgeClass = "ai.appcat.unity.AppCatUnityBridge";
        private const string CallbackInterface = "ai.appcat.unity.AppCatUnityBridge$Callback";

        private readonly AndroidJavaClass _bridge;
        private readonly NativeCallbackRegistry _registry;

        public bool IsNative => true;

        public AndroidCoreBackend(IDispatcher dispatcher)
        {
            _bridge = new AndroidJavaClass(BridgeClass);
            _registry = new NativeCallbackRegistry(dispatcher);
        }

        /// <summary>Java-side callback; invoked on an arbitrary thread.</summary>
        private sealed class CallbackProxy : AndroidJavaProxy
        {
            private readonly NativeCallbackRegistry _registry;
            private readonly int _requestId;

            public CallbackProxy(NativeCallbackRegistry registry, int requestId) : base(CallbackInterface)
            {
                _registry = registry;
                _requestId = requestId;
            }

            // ReSharper disable UnusedMember.Local -- invoked via JNI
            public void onSuccess(string json) => _registry.Complete(_requestId, json, null, null);
            public void onError(string code, string message) => _registry.Complete(_requestId, null, code ?? "ERR_INTERNAL", message);
            // ReSharper restore UnusedMember.Local
        }

        public async Task<Dictionary<string, object>> Configure(string appId, string apiKey, Dictionary<string, object> options)
        {
            var id = _registry.Register(out var task);
            using (var activity = CurrentActivity())
            {
                _bridge.CallStatic("configure", activity, appId ?? string.Empty, apiKey, AppCatJson.Encode(options) ?? "{}", new CallbackProxy(_registry, id));
            }
            return AppCatJson.DecodeObject(await task);
        }

        public async Task<Dictionary<string, object>> Identify(Dictionary<string, object> data)
        {
            var id = _registry.Register(out var task);
            _bridge.CallStatic("identify", AppCatJson.Encode(data) ?? "{}", new CallbackProxy(_registry, id));
            return AppCatJson.DecodeObject(await task);
        }

        public void SendEvent(string eventName, Dictionary<string, object> parameters, Dictionary<string, object> options)
        {
            _bridge.CallStatic("sendEvent", eventName, AppCatJson.Encode(parameters), AppCatJson.Encode(options));
        }

        public Task<Dictionary<string, object>> GetAttribution()
        {
            return Task.FromResult(AppCatJson.DecodeObject(_bridge.CallStatic<string>("getAttribution")));
        }

        public Task<Dictionary<string, object>> GetDeviceContext()
        {
            return Task.FromResult(AppCatJson.DecodeObject(_bridge.CallStatic<string>("getDeviceContext")));
        }

        public Task<string> GetAppCatId()
        {
            return Task.FromResult(_bridge.CallStatic<string>("getAppCatId") ?? string.Empty);
        }

        public Task<bool> IsDisabled() => Task.FromResult(_bridge.CallStatic<bool>("isDisabled"));

        public void SetLogLevel(int level) => _bridge.CallStatic("setLogLevel", level);

        public async Task SetTrackingConsent(bool granted)
        {
            var id = _registry.Register(out var task);
            _bridge.CallStatic("setTrackingConsent", granted, new CallbackProxy(_registry, id));
            await task;
        }

        private static AndroidJavaObject CurrentActivity()
        {
            using (var player = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
            {
                return player.GetStatic<AndroidJavaObject>("currentActivity");
            }
        }
    }
}
#endif
