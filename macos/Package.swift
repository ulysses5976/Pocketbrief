// swift-tools-version:5.9
// 口袋句庫 Pocketbrief — macOS 版
// Copyright (c) 2026 無名小律師（楊朝淵律師）. Licensed under the MIT License; see the LICENSE file.
import PackageDescription

let package = Package(
    name: "Pocketbrief",
    platforms: [.macOS(.v13)],
    targets: [
        .executableTarget(
            name: "Pocketbrief",
            path: "Sources/Pocketbrief",
            linkerSettings: [
                .linkedFramework("AppKit"),
                .linkedFramework("Carbon"),
                .linkedFramework("ApplicationServices"),
                .linkedFramework("ServiceManagement"),
            ]
        )
    ]
)
