using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using AppCat.Internal;

namespace AppCat.Tests
{
    /// <summary>Scriptable backend double. Records calls; replays configured results.</summary>
    internal sealed class FakeCoreBackend : ICoreBackend
    {
        public bool IsNative { get; set; }

        public readonly List<string> Calls = new List<string>();
        public Dictionary<string, object> ConfigureResult = new Dictionary<string, object>
        {
            ["deepLinkParams"] = null,
            ["geo"] = null,
        };
        public Exception ConfigureThrows;
        public Dictionary<string, object> IdentifyResult;
        public Exception Throws;

        public string LastEventName;
        public Dictionary<string, object> LastEventParams;
        public Dictionary<string, object> LastEventOptions;
        public Dictionary<string, object> LastIdentifyData;
        public Dictionary<string, object> LastConfigureOptions;
        public string LastAppId;
        public string LastApiKey;
        public int? LastLogLevel;
        public bool? LastConsent;

        public Task<Dictionary<string, object>> Configure(string appId, string apiKey, Dictionary<string, object> options)
        {
            Calls.Add("configure");
            LastAppId = appId;
            LastApiKey = apiKey;
            LastConfigureOptions = options;
            if (ConfigureThrows != null) throw ConfigureThrows;
            return Task.FromResult(ConfigureResult);
        }

        public Task<Dictionary<string, object>> Identify(Dictionary<string, object> data)
        {
            Calls.Add("identify");
            LastIdentifyData = data;
            if (Throws != null) throw Throws;
            return Task.FromResult(IdentifyResult);
        }

        public void SendEvent(string eventName, Dictionary<string, object> parameters, Dictionary<string, object> options)
        {
            Calls.Add("sendEvent");
            LastEventName = eventName;
            LastEventParams = parameters;
            LastEventOptions = options;
            if (Throws != null) throw Throws;
        }

        public Task<Dictionary<string, object>> GetAttribution()
        {
            Calls.Add("getAttribution");
            if (Throws != null) throw Throws;
            return Task.FromResult(new Dictionary<string, object> { ["fbc"] = "fb.1.1" });
        }

        public Task<Dictionary<string, object>> GetDeviceContext()
        {
            Calls.Add("getDeviceContext");
            if (Throws != null) throw Throws;
            return Task.FromResult(new Dictionary<string, object> { ["platform"] = "ios" });
        }

        public Task<string> GetAppCatId()
        {
            Calls.Add("getAppCatId");
            if (Throws != null) throw Throws;
            return Task.FromResult("device-1");
        }

        public Task<bool> IsDisabled()
        {
            Calls.Add("isDisabled");
            if (Throws != null) throw Throws;
            return Task.FromResult(true);
        }

        public void SetLogLevel(int level)
        {
            Calls.Add("setLogLevel");
            LastLogLevel = level;
        }

        public Task SetTrackingConsent(bool granted)
        {
            Calls.Add("setTrackingConsent");
            LastConsent = granted;
            if (Throws != null) throw Throws;
            return Task.CompletedTask;
        }
    }
}
