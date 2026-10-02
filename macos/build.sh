#!/bin/bash
# 編譯 Pocketbrief.app（同時支援 Apple 晶片與 Intel），並打包成 zip。
# 需要 Xcode 或 Xcode Command Line Tools（終端機執行 xcode-select --install 安裝）。
# 產出：dist/Pocketbrief.app、dist/Pocketbrief-v<版本>-mac.zip
set -euo pipefail
cd "$(dirname "$0")"

VERSION="$(tr -d '[:space:]' < VERSION)"
APP="dist/Pocketbrief.app"

swift build -c release --arch arm64 --arch x86_64
BIN_DIR="$(swift build -c release --arch arm64 --arch x86_64 --show-bin-path)"

rm -rf dist
mkdir -p "$APP/Contents/MacOS" "$APP/Contents/Resources"
cp "$BIN_DIR/Pocketbrief" "$APP/Contents/MacOS/Pocketbrief"
sed "s/__VERSION__/$VERSION/g" Info.plist > "$APP/Contents/Info.plist"

swift scripts/make-icon.swift dist/AppIcon.iconset
iconutil -c icns dist/AppIcon.iconset -o "$APP/Contents/Resources/AppIcon.icns"
rm -rf dist/AppIcon.iconset

# 沒有 Apple 開發者簽章：用「臨時簽章」，Apple 晶片的 Mac 才能執行
codesign --force --deep --sign - "$APP"

(cd dist && ditto -c -k --keepParent Pocketbrief.app "Pocketbrief-v$VERSION-mac.zip")
echo "Done: macos/dist/Pocketbrief-v$VERSION-mac.zip"
