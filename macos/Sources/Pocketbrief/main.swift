// 口袋句庫 Pocketbrief（macOS 版）：按快速鍵叫出常用句子清單，打代碼即可輸出到游標所在位置。
// Copyright (c) 2026 無名小律師（楊朝淵律師）. Licensed under the MIT License; see the LICENSE file.
import AppKit

let application = NSApplication.shared
let controller = AppController()
application.delegate = controller
application.setActivationPolicy(.accessory)   // 只顯示選單列圖示，不出現在 Dock
application.run()
