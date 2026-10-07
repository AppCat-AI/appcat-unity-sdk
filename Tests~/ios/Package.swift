// swift-tools-version:5.9
//
// Host-side unit tests for the pure-Foundation half of the iOS Unity bridge
// (AppCatUnityJson.swift). `Sources/AppCatUnityJson` symlinks to the file in
// Runtime/Plugins/iOS so the test always compiles the shipped source.
//
//   cd Tests~/ios && swift test
//
// The @_cdecl bridge itself (AppCatUnityBridge.swift) links AppCatCoreKit and
// is exercised by the Unity iOS smoke app.
import PackageDescription

let package = Package(
  name: "AppCatUnityBridgeTests",
  platforms: [.macOS(.v12)],
  targets: [
    .target(name: "AppCatUnityJson", path: "Sources/AppCatUnityJson"),
    .testTarget(
      name: "AppCatUnityJsonTests",
      dependencies: ["AppCatUnityJson"],
      path: "Tests/AppCatUnityJsonTests"
    ),
  ]
)
