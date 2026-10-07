using System.Collections.Generic;
using UnityEngine;

namespace AppCat.Samples
{
    /// <summary>
    /// Drop this on any GameObject in your first scene. Fill in the API key in
    /// the Inspector, press Play, and watch the Console: it initializes AppCat,
    /// sends a ViewContent event and prints the resolved attribution.
    ///
    /// Mirrors the smoke apps used for the iOS / Android / React Native SDKs.
    /// </summary>
    public sealed class AppCatSmokeTest : MonoBehaviour
    {
        [Header("Credentials (SDK Guides → API Key Management)")]
        [SerializeField] private string apiKey = "";
        [SerializeField] private string appId = "";

        [Header("Options")]
        [SerializeField] private bool sendPurchase = true;

        private async void Start()
        {
            try
            {
                var res = await AppCat.Init(new AppCatConfig
                {
                    ApiKey = apiKey,
                    AppId = appId,
                    IsDebug = true,
                    LogLevel = AppCatLogLevel.Debug,
                    OnError = e => Debug.LogWarning("[AppCat smoke] non-fatal: " + e.Message),
                });

                Debug.Log($"[AppCat smoke] init ok. native={AppCat.IsNativeBackend} geo={res.Geo?.City},{res.Geo?.Country}");
                if (res.DeepLinkParams != null)
                {
                    foreach (var kv in res.DeepLinkParams) Debug.Log($"[AppCat smoke] deepLink {kv.Key}={kv.Value}");
                }
                else
                {
                    Debug.Log("[AppCat smoke] no deferred deep link");
                }

                AppCat.SendEvent(AppCatEvents.ViewContent, new Dictionary<string, object> { ["screen"] = "smoke" });
                if (sendPurchase)
                {
                    AppCat.SendEvent(AppCatEvents.Purchase, new Dictionary<string, object>
                    {
                        ["value"] = 9.99,
                        ["currency"] = "USD",
                        ["eventId"] = "smoke_" + System.Guid.NewGuid().ToString("N"),
                    });
                }

                Debug.Log("[AppCat smoke] appcatId=" + await AppCat.GetAppCatId());
            }
            catch (AppCatException e)
            {
                Debug.LogError($"[AppCat smoke] init failed: {e.Code} {e.Message}");
            }
        }
    }
}
