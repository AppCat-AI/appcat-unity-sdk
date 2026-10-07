using System.Collections.Generic;
using System.Threading.Tasks;

namespace AppCat.Internal
{
    /// <summary>
    /// Backend contract. Three implementations, resolved at runtime:
    /// iOS native (AppCatCoreKit.xcframework), Android native (appcat-core.aar),
    /// and the managed core (AppCatCore.Unity.dll) for Editor / desktop / WebGL.
    ///
    /// Every method returns loosely-typed dictionaries decoded from the core;
    /// <see cref="AppCat"/> maps them to typed responses. Implementations may
    /// throw <see cref="AppCatException"/>; the wrapper decides whether that
    /// is fatal (Init) or swallowed (everything else).
    /// </summary>
    internal interface ICoreBackend
    {
        bool IsNative { get; }

        /// <summary>Configure + auto-resolve. Returns { deepLinkParams, geo }.</summary>
        Task<Dictionary<string, object>> Configure(string appId, string apiKey, Dictionary<string, object> options);

        /// <summary>Returns { geo, deepLinkParams }.</summary>
        Task<Dictionary<string, object>> Identify(Dictionary<string, object> data);

        void SendEvent(string eventName, Dictionary<string, object> parameters, Dictionary<string, object> options);

        Task<Dictionary<string, object>> GetAttribution();

        Task<Dictionary<string, object>> GetDeviceContext();

        Task<string> GetAppCatId();

        Task<bool> IsDisabled();

        void SetLogLevel(int level);

        Task SetTrackingConsent(bool granted);
    }
}
