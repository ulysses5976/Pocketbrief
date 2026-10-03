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

# 下載檔要自給自足：MIT 授權要求散布時附上授權聲明；README 叫使用者匯入的範例檔也要一起附
STAGE="dist/package"
mkdir -p "$STAGE"
cp -R "$APP" "$STAGE/"
for f in ../LICENSE ../README.md ../README.en.md; do
  if [ -f "$f" ]; then cp "$f" "$STAGE/"; fi
done
if [ -d ../examples ]; then cp -R ../examples "$STAGE/"; fi

# 用 ditto 壓縮才會保留 .app 的權限與簽章；不加 --keepParent，zip 根目錄就是 app 與說明檔
ditto -c -k "$STAGE" "dist/Pocketbrief-v$VERSION-mac.zip"
rm -rf "$STAGE"
echo "Done: macos/dist/Pocketbrief-v$VERSION-mac.zip"
