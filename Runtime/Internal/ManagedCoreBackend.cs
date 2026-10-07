using System.Collections.Generic;
using System.Threading.Tasks;
using AppCat.Core;

namespace AppCat.Internal
{
    /// <summary>
    /// Backend over the vendored managed core (AppCatCore.Unity.dll). Used in
    /// the Editor, on desktop standalone and on WebGL. Same wire protocol as
    /// the native cores, reduced native signal fidelity.
    /// </summary>
    internal sealed class ManagedCoreBackend : ICoreBackend
    {
        public bool IsNative => false;

        public ManagedCoreBackend(IDeviceContextProvider deviceContextProvider, IHttpTransport transport, System.Action<int, string> logSink)
        {
            if (deviceContextProvider != null) AppCatCore.SetDeviceContextProvider(deviceContextProvider);
            if (transport != null) AppCatCore.SetHttpTransport(transport);
            if (logSink != null) AppCatCore.SetLogSink(logSink);
        }

        public async Task<Dictionary<string, object>> Configure(string appId, string apiKey, Dictionary<string, object> options)
        {
            try
            {
                var result = await AppCatCore.ConfigureAsync(appId, apiKey, options);
                return new Dictionary<string, object>
                {
                    ["deepLinkParams"] = ToObjectDict(result.DeepLinkParams),
                    ["geo"] = ToGeoDict(result.Geo),
                };
            }
            catch (AppCatCoreException e)
            {
                throw new AppCatException(AppCatException.CodeFromNative(e.Code), e.Message, e);
            }
        }

        public async Task<Dictionary<string, object>> Identify(Dictionary<string, object> data)
        {
            var result = await AppCatCore.IdentifyAsync(data);
            return new Dictionary<string, object>
            {
                ["geo"] = ToGeoDict(result.Geo),
                ["deepLinkParams"] = ToObjectDict(result.DeepLinkParams),
            };
        }

        public void SendEvent(string eventName, Dictionary<string, object> parameters, Dictionary<string, object> options)
        {
            AppCatCore.SendEvent(eventName, parameters, options);
        }

        public async Task<Dictionary<string, object>> GetAttribution()
        {
            var ro = await AppCatCore.GetAttributionAsync();
            return Copy(ro);
        }

        public async Task<Dictionary<string, object>> GetDeviceContext()
        {
            var ro = await AppCatCore.GetDeviceContextAsync();
            return Copy(ro);
        }

        public Task<string> GetAppCatId() => AppCatCore.GetAppCatIdAsync();

        public Task<bool> IsDisabled() => AppCatCore.IsDisabledAsync();

        public void SetLogLevel(int level) => AppCatCore.SetLogLevel(level);

        public Task SetTrackingConsent(bool granted) => AppCatCore.SetTrackingConsentAsync(granted);

        private static Dictionary<string, object> ToObjectDict(IReadOnlyDictionary<string, string> src)
        {
            if (src == null) return null;
            var d = new Dictionary<string, object>();
            foreach (var kv in src) d[kv.Key] = kv.Value;
            return d;
        }

        private static Dictionary<string, object> ToGeoDict(Geo geo)
        {
            if (geo == null) return null;
            return new Dictionary<string, object>
            {
                ["city"] = geo.City,
                ["country"] = geo.Country,
                ["state"] = geo.State,
            };
        }

        private static Dictionary<string, object> Copy(IReadOnlyDictionary<string, object> src)
        {
            if (src == null) return null;
            var d = new Dictionary<string, object>();
            foreach (var kv in src) d[kv.Key] = kv.Value;
            return d;
        }
    }
}
