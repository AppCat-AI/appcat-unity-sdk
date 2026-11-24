# Changelog

## 0.1.3

- iOS: updated `AppCatCoreKit.xcframework`. The minimum iOS version is now 15.0.
- Fix (iOS): `Identify` was rejected by the server, so user details were never attached.
- Fix (iOS): purchase values passed as whole numbers or decimals were dropped.
- Fix (iOS): a non-finite `eventTimeUnix` could crash the app.
- Fix (iOS): `Identify` no longer replaces the result of `GetAttribution()`.
- iOS: the advertising ID and tracking status are read at send time, so a later tracking prompt is respected.
- iOS: `MobileAppInstall` events are now forwarded to the ad platforms.

## 0.1.2

- Android: the bundled `appcat-core.aar` sends the Play Install Referrer on first open, so installs from Google App campaigns (`gclid` / `gbraid`) attribute to Google Ads. The post-processor now adds the `installreferrer` library, and a missing library no longer stops attribution.
- Android: advertising ID collection is opt-in. See "Advertising ID (optional)" in the README.
- Fix: `await AppCat.SetTrackingConsent()` and a failed `Identify` could hang on Android.
- Fix: tracking consent was not recorded when running on the managed fallback (Editor, desktop, WebGL).
- Fix (Android, managed fallback): `Identify` no longer replaces the result of `GetAttribution()`.
- Fix (Android): list parameters such as `content_ids` are sent as arrays.
- Android and managed fallback: `MobileAppInstall` events are now forwarded to the ad platforms.
- `eventTimeUnix` can be passed with an event to set its time.
- The bundled iOS framework is unchanged in this release.

## 0.1.1

- Fix: the `.unitypackage` release asset now includes the `AppCatCoreKit.xcframework` payload. UPM installs were unaffected.

## 0.1.0

- Initial release.
- `AppCat.Init`, `Identify`, `SendEvent`, `GetAttribution`, `GetDeviceContext`, `GetAppCatId`, `IsDisabled`, `SetTrackingConsent`.
- Native backends for iOS (AppCatCoreKit.xcframework) and Android (appcat-core.aar); managed fallback (AppCatCore.Unity.dll) for Editor, desktop and WebGL.
- UPM git install; `.unitypackage` published on GitHub Releases.
