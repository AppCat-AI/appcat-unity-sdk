using System;
using System.Collections.Generic;
using AppCat.Core;
using UnityEngine;

namespace AppCat.Internal
{
    /// <summary>
    /// Supplies engine-level device signals to the managed core using Unity
    /// APIs. Every read is best-effort; a failing API simply omits its key.
    /// </summary>
    internal sealed class UnityDeviceContextProvider : IDeviceContextProvider
    {
        public IReadOnlyDictionary<string, object> Collect()
        {
            var d = new Dictionary<string, object>();

            Try(() => d["platform"] = PlatformName(Application.platform));
            Try(() => d["bundle_id"] = Application.identifier);
            Try(() => d["app_version"] = Application.version);
            Try(() => d["os_version"] = SystemInfo.operatingSystem);
            Try(() => d["device_model"] = SystemInfo.deviceModel);
            Try(() =>
            {
                var id = SystemInfo.deviceUniqueIdentifier;
                if (!string.IsNullOrEmpty(id) && id != SystemInfo.unsupportedIdentifier) d["vendor_id"] = id;
            });
            Try(() =>
            {
                if (Screen.width > 0) d["screen_width"] = Screen.width;
                if (Screen.height > 0) d["screen_height"] = Screen.height;
                if (Screen.dpi > 0) d["screen_density"] = Math.Round(Screen.dpi / 160.0, 2);
            });
            Try(() =>
            {
                if (SystemInfo.processorCount > 0) d["cpu_cores"] = SystemInfo.processorCount;
            });
            Try(() => d["locale"] = System.Globalization.CultureInfo.CurrentCulture.Name);

            return d;
        }

        /// <summary>Maps Unity's RuntimePlatform to the AppCat platform token.</summary>
        internal static string PlatformName(RuntimePlatform platform)
        {
            switch (platform)
            {
                case RuntimePlatform.IPhonePlayer:
                case RuntimePlatform.tvOS:
                    return "ios";
                case RuntimePlatform.Android:
                    return "android";
                case RuntimePlatform.WebGLPlayer:
                    return "web";
                case RuntimePlatform.OSXEditor:
                case RuntimePlatform.WindowsEditor:
                case RuntimePlatform.LinuxEditor:
                    return "editor";
                default:
                    return platform.ToString().ToLowerInvariant();
            }
        }

        private static void Try(Action a)
        {
            try { a(); } catch { /* best effort */ }
        }
    }
}
