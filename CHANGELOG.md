# Changelog

## 0.1.1

- Fix: the `.unitypackage` release asset now includes the `AppCatCoreKit.xcframework` payload. UPM installs were unaffected.

## 0.1.0

- Initial release.
- `AppCat.Init`, `Identify`, `SendEvent`, `GetAttribution`, `GetDeviceContext`, `GetAppCatId`, `IsDisabled`, `SetTrackingConsent`.
- Native backends for iOS (AppCatCoreKit.xcframework) and Android (appcat-core.aar); managed fallback (AppCatCore.Unity.dll) for Editor, desktop and WebGL.
- UPM git install; `.unitypackage` published on GitHub Releases.
