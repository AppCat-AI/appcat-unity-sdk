# AppCat Unity SDK

Track events and attribute installs across Meta, TikTok, Google Ads, and Apple Search Ads. Resolve deferred deep links to route players to the right content after install.

## Overview

- Track standard and custom events across ad platforms
- Track revenue events with currency and value
- Resolve deferred deep links after install to route players to the right scene
- Support Apple Search Ads attribution signals on iOS when available
- Retrieve the AppCat device ID and attribution data
- Native iOS/Android backends with full signal fidelity; managed C# fallback for the Editor, desktop and WebGL

## Get an API Key

You need an AppCat API key (and optionally an App ID) before the SDK can run.

1. Sign up at [appcat.ai](https://appcat.ai).
2. Click **+ New Product** in the top right and create your app. Select the platforms your game targets (iOS, Android, or both) and choose **Unity** as the framework.
3. In the sidebar, open **SDK Guides → API Key Management**.
4. Copy your **API Key**. The **App ID** is optional — it can be resolved automatically from the API key.

Pass these into `AppCat.Init(...)` below.

## Installation

### Unity Package Manager (recommended)

**Window → Package Manager → + → Add package from git URL…**

```
https://github.com/AppCat-AI/appcat-unity-sdk.git#v0.1.0
```

Or add it to `Packages/manifest.json`:

```json
{
  "dependencies": {
    "ai.appcat.sdk": "https://github.com/AppCat-AI/appcat-unity-sdk.git#v0.1.0"
  }
}
```

### .unitypackage

Download `AppCatSDK-vX.Y.Z.unitypackage` from [Releases](https://github.com/AppCat-AI/appcat-unity-sdk/releases) and import it via **Assets → Import Package → Custom Package…**.

### iOS — No Additional Configuration

`AppCatCoreKit.xcframework` ships under `Runtime/Plugins/iOS/`. An Editor post-processor embeds it in the exported Xcode project and enables Swift. Build and run from Xcode as usual.

### Android — No Additional Configuration

`appcat-core.aar` ships under `Runtime/Plugins/Android/`. An Editor post-processor adds `kotlin-stdlib` and the `INTERNET` permission to the exported Gradle project. Requires the default Unity Gradle template (Gradle 7+ / AGP 7+).

## Platform Requirements

| Platform | Minimum Version |
|----------|----------------|
| Unity | 2021.3 LTS+ |
| iOS | 13.0+ (Xcode 15+) |
| Android | 5.0+ (API 21), Java 17 |
| Scripting backend | Mono or IL2CPP |
| API compatibility | .NET Standard 2.1 |
| Editor / Standalone / WebGL | Managed fallback (`AppCatCore.Unity.dll`) |

## Quick Start

```csharp
using System.Collections.Generic;
using UnityEngine;
using AppCat;

public class Bootstrap : MonoBehaviour
{
    private async void Start()
    {
        try
        {
            var res = await AppCat.Init(new AppCatConfig
            {
                ApiKey = "your-api-key",
                AppId = "your-app-id", // optional
            });

            if (res.DeepLinkParams != null)
            {
                // route player based on params, e.g. res.DeepLinkParams["level"]
            }
        }
        catch (AppCatException e)
        {
            Debug.LogError($"AppCat init failed: {e.Code} {e.Message}");
        }
    }

    // Identify — call post-login when PII becomes available
    public void OnLogin(string userId, string email)
    {
        _ = AppCat.Identify(new IdentifyData { UserId = userId, Email = email });
    }

    // Track events after Init has completed
    public void OnPurchase()
    {
        AppCat.SendEvent(AppCatEvents.Purchase, new Dictionary<string, object>
        {
            ["item"] = "gem_pack_large",
            ["value"] = 9.99,
            ["currency"] = "USD",
        });
    }
}
```

Prefer callbacks over `async`? Every async method has an overload:

```csharp
AppCat.Init(config,
    onSuccess: res => Debug.Log("ready"),
    onError: e => Debug.LogError(e.Code));
```

## API

### `AppCat.Init(config)`

Initialize the SDK and create the attribution profile. Automatically resolves deferred deep links and returns any matched query params. Must be called before any other method. This is the only method that throws.

**Parameters (`AppCatConfig`):**

| Name | Type | Required | Description |
|------|------|----------|-------------|
| `ApiKey` | `string` | Yes | API key for your AppCat project |
| `AppId` | `string` | No | Your AppCat application ID. Resolved from `ApiKey` if omitted |
| `IsDebug` | `bool` | No | Enable debug logging (default: `false`) |
| `LogLevel` | `AppCatLogLevel` | No | `Debug`, `Info`, `Warn`, `Error` (default: `Info`) |
| `CustomerUserId` | `string` | No | User ID to associate with this session |
| `OnError` | `Action<Exception>` | No | Callback for non-fatal SDK errors (delivered on the main thread) |

**Returns:** `Task<InitResponse>`

| Field | Type | Description |
|-------|------|-------------|
| `DeepLinkParams` | `IReadOnlyDictionary<string, string>` or `null` | Query params from the matched ad click URL |
| `Geo` | `AppCatGeo { City, Country, State }` or `null` | Geo data, e.g. `San Francisco, US, CA` |

**Throws:** `AppCatException` with `Code` of `InvalidApiKey`, `NoBackend` or `ConfigureFailed`.

---

### `AppCat.Identify(data)`

Optional. Enrich the attribution profile with PII for stronger ad platform matching. Call after login, signup, or whenever user data becomes available. Returns `null` on error.

**Parameters (`IdentifyData`):**

| Name | Type | Description |
|------|------|-------------|
| `UserId` | `string` | Your internal user ID |
| `Email` | `string` | User email address |
| `Phone` | `string` | User phone number |
| `Name` | `string` | User display name |
| `RevenueCatIds` | `IEnumerable<string>` | RevenueCat app user IDs |
| `CustomAttributes` | `IDictionary<string, object>` | Any additional key-value pairs |

**Returns:** `Task<IdentifyResponse>` — `{ Geo, DeepLinkParams }`

---

### `AppCat.SendEvent(eventName, parameters)`

Track a conversion event. Fire-and-forget, never throws.

| Name | Type | Description |
|------|------|-------------|
| `eventName` | `string` | Event name (see `AppCatEvents`) |
| `parameters["eventId"]` | `string` | Unique event ID for deduplication |
| `parameters["value"]` | `double` | Monetary value of the event |
| `parameters["currency"]` | `string` | ISO 4217 currency code (e.g. `"USD"`) |
| `parameters["testEventCode"]` | `string` | Test event code for validation |
| other keys | `object` | Custom event parameters |

```csharp
AppCat.SendEvent(AppCatEvents.Subscribe, new Dictionary<string, object>
{
    ["plan"] = "annual", ["value"] = 99.99, ["currency"] = "USD", ["eventId"] = "order-abc-123",
});
```

---

### `AppCat.SetTrackingConsent(granted)`

Optional. Record the user's tracking-consent choice after ATT, GDPR, or an in-app privacy setting. When `granted` is `false`, AppCat stops forwarding certain PII fields to ad networks on your behalf. **Returns:** `Task`

### `AppCat.GetAttribution()`

Cached attribution data, or `null` before `Init`. **Returns:** `Task<IReadOnlyDictionary<string, object>>`

### `AppCat.GetDeviceContext()`

Cached device context. **Returns:** `Task<IReadOnlyDictionary<string, object>>`

### `AppCat.GetAppCatId()`

Stable AppCat device identifier (IDFV / Android ID / `SystemInfo.deviceUniqueIdentifier`). **Returns:** `Task<string>`

### `AppCat.IsDisabled()`

Whether the SDK has been remotely disabled. **Returns:** `Task<bool>`

### `AppCat.IsInitialized` / `AppCat.IsNativeBackend`

Properties. `IsNativeBackend` is `true` on iOS/Android device builds, `false` in the Editor and on the managed fallback.

### `AppCat.Reset()`

Reset SDK state for tests.

## Threading

All `Task` completions and callbacks are marshalled to the Unity main thread, so you can touch `GameObject`s from continuations.

## Privacy

Call `SetTrackingConsent(false)` when the user denies tracking consent. Avoid logging raw deep-link params, email, phone, or attribution payloads in production.

## Available Event Types

| Constant | Event Name | Description |
|----------|------------|-------------|
| `AppCatEvents.MobileAppInstall` | `MobileAppInstall` | App installed |
| `AppCatEvents.ViewContent` | `ViewContent` | User viewed content |
| `AppCatEvents.AddToCart` | `AddToCart` | Item added to cart |
| `AppCatEvents.InitiateCheckout` | `InitiateCheckout` | Checkout started |
| `AppCatEvents.StartTrial` | `StartTrial` | Free trial started |
| `AppCatEvents.Subscribe` | `Subscribe` | Subscription started |
| `AppCatEvents.Purchase` | `Purchase` | Purchase completed |
| `AppCatEvents.CompleteRegistration` | `CompleteRegistration` | Registration completed |
| `AppCatEvents.Search` | `Search` | Search performed |

Custom event names are also supported as any string value.

## Sample

**Package Manager → AppCat SDK → Samples → Smoke Test Scene → Import**, then drop `AppCatSmokeTest` on a GameObject and fill in the API key.

## Running the tests

| Suite | Command |
|-------|---------|
| C# wrapper (host) | `dotnet test Tests~/dotnet/AppCat.Unity.Tests.csproj` |
| C# wrapper (Unity) | Window → General → Test Runner → EditMode |
| Swift bridge | `cd Tests~/ios && swift test` |
| Kotlin bridge | `cd Tests~/android && ./gradlew test` |

## License

MIT -- see [LICENSE](./LICENSE) for details.
