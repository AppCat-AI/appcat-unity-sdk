import Foundation

/// JSON helpers for the Unity ↔ Swift boundary. Pure Foundation so they can be
/// unit-tested with `swift test` on macOS without the AppCatCoreKit framework.
///
/// Hardening: no forced unwraps, no `try!`. Every helper degrades to `nil` or
/// an empty value instead of trapping.
public enum AppCatUnityJson {

  /// Parses a JSON object string into a dictionary. Non-object JSON yields `nil`.
  public static func parseObject(_ json: String?) -> [String: Any]? {
    guard let json = json, let data = json.data(using: .utf8) else { return nil }
    guard let obj = try? JSONSerialization.jsonObject(with: data, options: [.fragmentsAllowed]) else { return nil }
    return obj as? [String: Any]
  }

  /// Serializes a dictionary to a compact JSON string. Non-encodable leaves are
  /// stringified so a stray NSDate/URL in a core result never breaks the bridge.
  public static func stringify(_ dict: [String: Any]?) -> String? {
    guard let dict = dict else { return nil }
    let sanitized = sanitize(dict)
    guard JSONSerialization.isValidJSONObject(sanitized),
          let data = try? JSONSerialization.data(withJSONObject: sanitized, options: []) else {
      return nil
    }
    return String(data: data, encoding: .utf8)
  }

  /// Shapes a core `resolve()` result into the `{ deepLinkParams, geo }` envelope
  /// shared by every SDK's `init()` response. Empty params collapse to `null`.
  public static func initResponse(from resolveResult: [String: Any]?) -> [String: Any] {
    let params = resolveResult?["deepLinkParams"] as? [String: Any]
    let geo = resolveResult?["geo"] as? [String: Any]
    return [
      "deepLinkParams": (params?.isEmpty == false ? params : nil) as Any? ?? NSNull(),
      "geo": geo as Any? ?? NSNull(),
    ]
  }

  // MARK: - Private

  private static func sanitize(_ value: Any) -> Any {
    switch value {
    case let dict as [String: Any]:
      var out: [String: Any] = [:]
      for (k, v) in dict { out[k] = sanitize(v) }
      return out
    case let arr as [Any]:
      return arr.map { sanitize($0) }
    case is NSNull, is String, is NSNumber, is Bool, is Int, is Double:
      return value
    default:
      return String(describing: value)
    }
  }
}
