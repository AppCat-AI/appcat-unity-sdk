using System;
using System.Collections.Generic;

namespace AppCat
{
    // -----------------------------------------------------------------------
    // Errors
    // -----------------------------------------------------------------------

    /// <summary>
    /// Stable error codes surfaced from the wrapper and every backend.
    /// Native bridges map their errors to these same codes so host code can
    /// branch on <see cref="AppCatException.Code"/> regardless of backend.
    /// </summary>
    public enum AppCatErrorCode
    {
        InvalidApiKey,
        InvalidConfig,
        NotConfigured,
        NoBackend,
        SerializeFailed,
        SetTrackingConsentFailed,
        ConfigureFailed,
        Internal,
    }

    /// <summary>
    /// Typed error thrown from <see cref="AppCat.Init"/>, the only method that
    /// throws. Every other public method swallows errors, reports them through
    /// <see cref="AppCatConfig.OnError"/>, and returns a typed fallback.
    /// </summary>
    public sealed class AppCatException : Exception
    {
        public AppCatErrorCode Code { get; }

        public AppCatException(AppCatErrorCode code, string message, Exception inner = null)
            : base(message, inner)
        {
            Code = code;
        }

        /// <summary>Maps a native bridge reject code (e.g. ERR_NOT_CONFIGURED) to a typed code.</summary>
        public static AppCatErrorCode CodeFromNative(string nativeCode)
        {
            switch (nativeCode)
            {
                case "ERR_INVALID_CONFIG": return AppCatErrorCode.InvalidConfig;
                case "INVALID_API_KEY": return AppCatErrorCode.InvalidApiKey;
                case "ERR_NOT_CONFIGURED":
                case "NOT_CONFIGURED": return AppCatErrorCode.NotConfigured;
                case "ERR_SERIALIZE": return AppCatErrorCode.SerializeFailed;
                case "ERR_SET_TRACKING_CONSENT": return AppCatErrorCode.SetTrackingConsentFailed;
                case "CONFIG_FETCH_FAILED": return AppCatErrorCode.ConfigureFailed;
                default: return AppCatErrorCode.Internal;
            }
        }
    }

    // -----------------------------------------------------------------------
    // Configuration
    // -----------------------------------------------------------------------

    /// <summary>Log verbosity level.</summary>
    public enum AppCatLogLevel
    {
        Debug = 0,
        Info = 1,
        Warn = 2,
        Error = 3,
    }

    /// <summary>Options passed to <see cref="AppCat.Init"/>.</summary>
    public sealed class AppCatConfig
    {
        /// <summary>API key for authenticating with the AppCat server. Required.</summary>
        public string ApiKey { get; set; }

        /// <summary>App ID. Resolved automatically from the API key when omitted.</summary>
        public string AppId { get; set; }

        /// <summary>Enable debug logging (default: false).</summary>
        public bool IsDebug { get; set; }

        /// <summary>Log level (default: Info).</summary>
        public AppCatLogLevel LogLevel { get; set; } = AppCatLogLevel.Info;

        /// <summary>Optional customer user ID to associate with this device/session.</summary>
        public string CustomerUserId { get; set; }

        /// <summary>
        /// Optional error callback. Invoked on non-fatal SDK errors instead of
        /// throwing. When omitted, errors are swallowed (best-effort). Always
        /// invoked on the Unity main thread.
        /// </summary>
        public Action<Exception> OnError { get; set; }
    }

    // -----------------------------------------------------------------------
    // Responses
    // -----------------------------------------------------------------------

    /// <summary>Geo data resolved by the server from the device IP.</summary>
    public sealed class AppCatGeo
    {
        public string City { get; }
        public string Country { get; }
        public string State { get; }

        public AppCatGeo(string city, string country, string state)
        {
            City = city;
            Country = country;
            State = state;
        }

        public override string ToString() => $"{City ?? "-"}, {State ?? "-"}, {Country ?? "-"}";
    }

    /// <summary>Structured response from <see cref="AppCat.Init"/>.</summary>
    public sealed class InitResponse
    {
        /// <summary>Query params from the matched ad click URL, or null if no match.</summary>
        public IReadOnlyDictionary<string, string> DeepLinkParams { get; }

        /// <summary>Geo resolved from the device IP during attribution, or null.</summary>
        public AppCatGeo Geo { get; }

        public static readonly InitResponse Empty = new InitResponse(null, null);

        public InitResponse(IReadOnlyDictionary<string, string> deepLinkParams, AppCatGeo geo)
        {
            DeepLinkParams = deepLinkParams;
            Geo = geo;
        }
    }

    /// <summary>Structured response from <see cref="AppCat.Identify"/>.</summary>
    public sealed class IdentifyResponse
    {
        public AppCatGeo Geo { get; }
        public IReadOnlyDictionary<string, string> DeepLinkParams { get; }

        public static readonly IdentifyResponse Empty = new IdentifyResponse(null, null);

        public IdentifyResponse(AppCatGeo geo, IReadOnlyDictionary<string, string> deepLinkParams)
        {
            Geo = geo;
            DeepLinkParams = deepLinkParams;
        }
    }

    // -----------------------------------------------------------------------
    // Identify
    // -----------------------------------------------------------------------

    /// <summary>User identity fields linked to the attribution profile.</summary>
    public sealed class IdentifyData
    {
        public string UserId { get; set; }
        public string Email { get; set; }
        public string Phone { get; set; }
        public string Name { get; set; }

        /// <summary>RevenueCat subscriber IDs for cross-referencing server-side events.</summary>
        public IReadOnlyList<string> RevenueCatIds { get; set; }

        /// <summary>Free-form attributes stored on the profile.</summary>
        public IReadOnlyDictionary<string, object> CustomAttributes { get; set; }

        /// <summary>Flattens to the wire shape shared by every SDK.</summary>
        public Dictionary<string, object> ToDictionary()
        {
            var d = new Dictionary<string, object>();
            if (!string.IsNullOrEmpty(UserId)) d["userId"] = UserId;
            if (!string.IsNullOrEmpty(Email)) d["email"] = Email;
            if (!string.IsNullOrEmpty(Phone)) d["phone"] = Phone;
            if (!string.IsNullOrEmpty(Name)) d["name"] = Name;
            if (RevenueCatIds != null && RevenueCatIds.Count > 0) d["revenueCatIds"] = new List<object>(RevenueCatIds);
            if (CustomAttributes != null && CustomAttributes.Count > 0)
            {
                var attrs = new Dictionary<string, object>();
                foreach (var kv in CustomAttributes) attrs[kv.Key] = kv.Value;
                d["customAttributes"] = attrs;
            }
            return d;
        }
    }

    // -----------------------------------------------------------------------
    // Events
    // -----------------------------------------------------------------------

    /// <summary>Standard event names. Custom names are also accepted by <see cref="AppCat.SendEvent"/>.</summary>
    public static class AppCatEvents
    {
        public const string MobileAppInstall = "MobileAppInstall";
        public const string ViewContent = "ViewContent";
        public const string AddToCart = "AddToCart";
        public const string InitiateCheckout = "InitiateCheckout";
        public const string StartTrial = "StartTrial";
        public const string Subscribe = "Subscribe";
        public const string Purchase = "Purchase";
        public const string CompleteRegistration = "CompleteRegistration";
        public const string Search = "Search";
    }

    /// <summary>
    /// Reserved keys in the flat <c>sendEvent</c> params object. They are routed
    /// to the ad platform payload; all other keys become <c>custom_data</c>.
    /// </summary>
    public static class AppCatEventKeys
    {
        public const string EventId = "eventId";
        public const string Value = "value";
        public const string Currency = "currency";
        public const string TestEventCode = "testEventCode";
    }
}
