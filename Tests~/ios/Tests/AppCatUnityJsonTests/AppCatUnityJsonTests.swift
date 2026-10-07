import XCTest
@testable import AppCatUnityJson

final class AppCatUnityJsonTests: XCTestCase {

  // MARK: parseObject

  func testParseObjectReturnsDictionary() {
    let dict = AppCatUnityJson.parseObject(#"{"isDebug":true,"logLevel":0,"customerUserId":"u1"}"#)
    XCTAssertEqual(dict?["isDebug"] as? Bool, true)
    XCTAssertEqual(dict?["logLevel"] as? Int, 0)
    XCTAssertEqual(dict?["customerUserId"] as? String, "u1")
  }

  func testParseObjectRejectsNonObjectsAndGarbage() {
    XCTAssertNil(AppCatUnityJson.parseObject(nil))
    XCTAssertNil(AppCatUnityJson.parseObject(""))
    XCTAssertNil(AppCatUnityJson.parseObject("{"))
    XCTAssertNil(AppCatUnityJson.parseObject("[1,2]"))
    XCTAssertNil(AppCatUnityJson.parseObject("\"str\""))
  }

  // MARK: stringify

  func testStringifyRoundTrips() {
    let json = AppCatUnityJson.stringify(["a": 1, "b": "x", "c": [1, 2], "d": ["k": NSNull()]])
    XCTAssertNotNil(json)
    let back = AppCatUnityJson.parseObject(json)
    XCTAssertEqual(back?["a"] as? Int, 1)
    XCTAssertEqual(back?["b"] as? String, "x")
    XCTAssertEqual((back?["c"] as? [Int]), [1, 2])
    XCTAssertTrue((back?["d"] as? [String: Any])?["k"] is NSNull)
  }

  func testStringifyNilReturnsNil() {
    XCTAssertNil(AppCatUnityJson.stringify(nil))
  }

  func testStringifySanitizesNonJsonLeaves() {
    let date = Date(timeIntervalSince1970: 0)
    let json = AppCatUnityJson.stringify(["when": date, "url": URL(string: "https://appcat.ai")!])
    XCTAssertNotNil(json, "non-encodable leaves must be stringified, not fail")
    let back = AppCatUnityJson.parseObject(json)
    XCTAssertEqual(back?["url"] as? String, "https://appcat.ai")
    XCTAssertTrue(back?["when"] is String)
  }

  // MARK: initResponse

  func testInitResponseShapesMatch() {
    let env = AppCatUnityJson.initResponse(from: [
      "matched": true,
      "deepLinkParams": ["promo": "summer"],
      "geo": ["city": "NYC", "country": "US", "state": "NY"],
    ])
    XCTAssertEqual((env["deepLinkParams"] as? [String: Any])?["promo"] as? String, "summer")
    XCTAssertEqual((env["geo"] as? [String: Any])?["city"] as? String, "NYC")
  }

  func testInitResponseCollapsesEmptyParamsToNull() {
    let env = AppCatUnityJson.initResponse(from: ["deepLinkParams": [:]])
    XCTAssertTrue(env["deepLinkParams"] is NSNull)
    XCTAssertTrue(env["geo"] is NSNull)
  }

  func testInitResponseNilInput() {
    let env = AppCatUnityJson.initResponse(from: nil)
    XCTAssertTrue(env["deepLinkParams"] is NSNull)
    XCTAssertTrue(env["geo"] is NSNull)
    XCTAssertEqual(Set(env.keys), ["deepLinkParams", "geo"])
    XCTAssertNotNil(AppCatUnityJson.stringify(env), "envelope must always be serializable")
  }
}
