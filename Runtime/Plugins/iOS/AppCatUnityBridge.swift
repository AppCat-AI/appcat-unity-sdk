import Foundation
import AppCatCoreKit

/// AppCatUnityBridge — iOS native bridge for the Unity SDK.
///
/// Exposes C-callable entry points (`@_cdecl`) that the C# `IosCoreBackend`
/// P/Invokes via `__Internal`. Delegates every call to AppCatCoreKit and ships
/// results back as JSON strings through a single C callback keyed by request id.
///
/// Hardening contract (same as the React Native bridge):
///   - No forced unwraps, `try!`, `as!`, or `fatalError`.
///   - Every async entry point invokes the callback exactly once.
///   - Native `AppCatError` cases map to stable string codes the C# wrapper
///     turns into `AppCatErrorCode`.
///   - Returned C strings are `strdup`'d; C# frees them via `appcat_unity_free`.

public typealias AppCatUnityCallback = @convention(c) (
  Int32,                    // requestId
  UnsafePointer<CChar>?,    // json (success)
  UnsafePointer<CChar>?,    // errorCode
  UnsafePointer<CChar>?     // errorMessage
) -> Void

private enum BridgeError {
  static let invalidConfig = "ERR_INVALID_CONFIG"
  static let notConfigured = "ERR_NOT_CONFIGURED"
  static let serialize = "ERR_SERIALIZE"
  static let resolve = "ERR_RESOLVE"
  static let identify = "ERR_IDENTIFY"
  static let setTrackingConsent = "ERR_SET_TRACKING_CONSENT"
  static let internalError = "ERR_INTERNAL"
}

private func string(_ ptr: UnsafePointer<CChar>?) -> String? {
  guard let ptr = ptr else { return nil }
  return String(cString: ptr)
}

private func succeed(_ cb: AppCatUnityCallback?, _ id: Int32, _ json: String) {
  json.withCString { cb?(id, $0, nil, nil) }
}

private func fail(_ cb: AppCatUnityCallback?, _ id: Int32, _ code: String, _ message: String) {
  code.withCString { c in
    message.withCString { m in cb?(id, nil, c, m) }
  }
}

private func code(for error: AppCatCore.AppCatError, fallback: String) -> (String, String) {
  switch error {
  case .invalidConfig(let msg): return (BridgeError.invalidConfig, msg)
  case .notConfigured: return (BridgeError.notConfigured, "Call configure() first")
  case .serializationFailed: return (BridgeError.serialize, "Serialization failed")
  }
}

// MARK: - configure

@_cdecl("appcat_unity_configure")
public func appcat_unity_configure(
  _ appId: UnsafePointer<CChar>?,
  _ apiKey: UnsafePointer<CChar>?,
  _ optionsJson: UnsafePointer<CChar>?,
  _ requestId: Int32,
  _ callback: AppCatUnityCallback?
) {
  let options = AppCatUnityJson.parseObject(string(optionsJson)) ?? [:]
  AppCatCore.shared.configure(
    appId: string(appId) ?? "",
    apiKey: string(apiKey) ?? "",
    options: options
  ) { result in
    switch result {
    case .success:
      AppCatCore.shared.resolve(ddlToken: nil) { resolveResult in
        switch resolveResult {
        case .success(let value):
          let envelope = AppCatUnityJson.initResponse(from: value)
          succeed(callback, requestId, AppCatUnityJson.stringify(envelope) ?? "{}")
        case .failure:
          // Resolve failure is non-fatal; configure still succeeded.
          succeed(callback, requestId, AppCatUnityJson.stringify(AppCatUnityJson.initResponse(from: nil)) ?? "{}")
        }
      }
    case .failure(let error):
      let (c, m) = code(for: error, fallback: BridgeError.internalError)
      fail(callback, requestId, c, m)
    }
  }
}

// MARK: - identify

@_cdecl("appcat_unity_identify")
public func appcat_unity_identify(
  _ dataJson: UnsafePointer<CChar>?,
  _ requestId: Int32,
  _ callback: AppCatUnityCallback?
) {
  let data = AppCatUnityJson.parseObject(string(dataJson)) ?? [:]
  AppCatCore.shared.identify(data: data) { result in
    switch result {
    case .success(let value):
      succeed(callback, requestId, AppCatUnityJson.stringify(value) ?? "null")
    case .failure(let error):
      let (c, m) = code(for: error, fallback: BridgeError.identify)
      fail(callback, requestId, c, m)
    }
  }
}

// MARK: - sendEvent (fire-and-forget)

@_cdecl("appcat_unity_send_event")
public func appcat_unity_send_event(
  _ eventName: UnsafePointer<CChar>?,
  _ paramsJson: UnsafePointer<CChar>?,
  _ optionsJson: UnsafePointer<CChar>?
) {
  guard let name = string(eventName), !name.isEmpty else { return }
  AppCatCore.shared.sendEvent(
    eventName: name,
    params: AppCatUnityJson.parseObject(string(paramsJson)),
    options: AppCatUnityJson.parseObject(string(optionsJson))
  )
}

// MARK: - synchronous getters (caller frees via appcat_unity_free)

@_cdecl("appcat_unity_get_attribution")
public func appcat_unity_get_attribution() -> UnsafeMutablePointer<CChar>? {
  guard let json = AppCatUnityJson.stringify(AppCatCore.shared.getAttribution()) else { return nil }
  return strdup(json)
}

@_cdecl("appcat_unity_get_device_context")
public func appcat_unity_get_device_context() -> UnsafeMutablePointer<CChar>? {
  guard let json = AppCatUnityJson.stringify(AppCatCore.shared.getDeviceContext()) else { return nil }
  return strdup(json)
}

@_cdecl("appcat_unity_get_appcat_id")
public func appcat_unity_get_appcat_id() -> UnsafeMutablePointer<CChar>? {
  return strdup(AppCatCore.shared.getAppCatId())
}

@_cdecl("appcat_unity_is_disabled")
public func appcat_unity_is_disabled() -> Bool {
  return AppCatCore.shared.isDisabled()
}

@_cdecl("appcat_unity_set_log_level")
public func appcat_unity_set_log_level(_ level: Int32) {
  AppCatCore.shared.setLogLevel(Int(level))
}

@_cdecl("appcat_unity_free")
public func appcat_unity_free(_ ptr: UnsafeMutablePointer<CChar>?) {
  free(ptr)
}

// MARK: - setTrackingConsent

@_cdecl("appcat_unity_set_tracking_consent")
public func appcat_unity_set_tracking_consent(
  _ granted: Bool,
  _ requestId: Int32,
  _ callback: AppCatUnityCallback?
) {
  AppCatCore.shared.setTrackingConsent(granted: granted) { result in
    switch result {
    case .success:
      succeed(callback, requestId, "null")
    case .failure(let error):
      fail(callback, requestId, BridgeError.setTrackingConsent, error.localizedDescription)
    }
  }
}
