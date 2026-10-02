// 口袋句庫 Pocketbrief：與系統打交道的部分（權限、游標位置、按鍵字元、貼上、提示）
// Copyright (c) 2026 無名小律師（楊朝淵律師）. Licensed under the MIT License; see the LICENSE file.
import AppKit
import ApplicationServices
import Carbon

// ───────────── 權限 ─────────────
// 「輔助使用」：送出 ⌘V／⌘C、讀取游標位置都需要。「輸入監控」：只有用 `+1 這類組合快速鍵時需要。
enum Permissions {
    static var accessibility: Bool { AXIsProcessTrusted() }

    static func requestAccessibilityPrompt() {
        let key = kAXTrustedCheckOptionPrompt.takeUnretainedValue() as String
        _ = AXIsProcessTrustedWithOptions([key: true] as CFDictionary)
    }

    static var inputMonitoring: Bool { CGPreflightListenEventAccess() }

    static func requestInputMonitoring() { _ = CGRequestListenEventAccess() }

    static func openAccessibilitySettings() {
        NSWorkspace.shared.open(URL(string: "x-apple.systempreferences:com.apple.preference.security?Privacy_Accessibility")!)
    }

    static func openInputMonitoringSettings() {
        NSWorkspace.shared.open(URL(string: "x-apple.systempreferences:com.apple.preference.security?Privacy_ListenEvent")!)
    }

    // 需要「輔助使用」時呼叫；沒有權限就說明怎麼開，回傳 false
    static func requireAccessibility() -> Bool {
        if accessibility { return true }
        explainAccessibility()
        return false
    }

    static func explainAccessibility() {
        let r = Alerts.show(L.t("需要「輔助使用」權限"),
                            info: L.t("口袋句庫要把範本貼到游標所在的位置，需要 macOS 的「輔助使用」權限。\n\n請到「系統設定」→「隱私權與安全性」→「輔助使用」，打開「Pocketbrief」。\n\n如果清單裡已經有 Pocketbrief 卻仍無法使用（例如剛更新過程式），請先用「－」把它移除，再重新加入。"),
                            buttons: [L.t("開啟系統設定"), L.t("稍後")])
        if r == 0 { requestAccessibilityPrompt(); openAccessibilitySettings() }
    }
}

// ───────────── 游標（插入點）位置 ─────────────
enum Caret {
    // 回傳插入點在螢幕上的位置（Cocoa 座標：原點在主螢幕左下角）；取不到時回傳 nil
    static func rect() -> NSRect? {
        guard Permissions.accessibility else { return nil }
        let sys = AXUIElementCreateSystemWide()
        AXUIElementSetMessagingTimeout(sys, 0.25)
        var focused: CFTypeRef?
        guard AXUIElementCopyAttributeValue(sys, kAXFocusedUIElementAttribute as CFString, &focused) == .success,
              let f = focused, CFGetTypeID(f) == AXUIElementGetTypeID() else { return nil }
        let el = f as! AXUIElement
        var range: CFTypeRef?
        guard AXUIElementCopyAttributeValue(el, kAXSelectedTextRangeAttribute as CFString, &range) == .success, let rg = range else { return nil }
        var bounds: CFTypeRef?
        guard AXUIElementCopyParameterizedAttributeValue(el, kAXBoundsForRangeParameterizedAttribute as CFString, rg, &bounds) == .success,
              let b = bounds, CFGetTypeID(b) == AXValueGetTypeID() else { return nil }
        var r = CGRect.zero
        guard AXValueGetValue(b as! AXValue, .cgRect, &r), r.height > 0 || r.width > 0 else { return nil }
        // 輔助使用的座標原點在主螢幕左上角，往下為正；換成 Cocoa 座標
        guard let primary = NSScreen.screens.first else { return nil }
        let rect = NSRect(x: r.minX, y: primary.frame.maxY - r.maxY, width: r.width, height: r.height)
        // 有些程式回報的位置不可靠（例如全是 0、在螢幕外、或整段選取範圍）：不合理就改放在滑鼠旁邊
        if rect.height > 200 || rect.width > 2000 { return nil }
        if !NSScreen.screens.contains(where: { $0.frame.contains(NSPoint(x: rect.minX, y: rect.midY)) }) { return nil }
        if r.minX == 0 && r.minY == 0 { return nil }
        return rect
    }
}

// ───────────── 按鍵 → 字元 ─────────────
// 叫出清單時不切換輸入法：直接用「英文鍵盤配置」把按鍵換成字元，注音、倉頡等輸入法開著也能打代碼
enum KeyChar {
    static func char(keyCode: UInt16, shift: Bool) -> Character? {
        guard let src = TISCopyCurrentASCIICapableKeyboardLayoutInputSource()?.takeRetainedValue(),
              let ptr = TISGetInputSourceProperty(src, kTISPropertyUnicodeKeyLayoutData) else { return nil }
        let data = Unmanaged<CFData>.fromOpaque(ptr).takeUnretainedValue() as Data
        return data.withUnsafeBytes { raw -> Character? in
            guard let base = raw.baseAddress else { return nil }
            let layout = base.assumingMemoryBound(to: UCKeyboardLayout.self)
            var dead: UInt32 = 0
            var chars = [UniChar](repeating: 0, count: 4)
            var len = 0
            let mods: UInt32 = shift ? UInt32((shiftKey >> 8) & 0xFF) : 0
            let st = UCKeyTranslate(layout, keyCode, UInt16(kUCKeyActionDown), mods, UInt32(LMGetKbdType()),
                                    OptionBits(kUCKeyTranslateNoDeadKeysMask), &dead, chars.count, &len, &chars)
            guard st == noErr, len > 0 else { return nil }
            return String(utf16CodeUnits: chars, count: len).first
        }
    }
}

// ───────────── 送出按鍵 ─────────────
enum Keys {
    static func modifiersDown() -> Bool {
        !CGEventSource.flagsState(.combinedSessionState).intersection([.maskCommand, .maskControl, .maskAlternate, .maskShift]).isEmpty
    }

    // 送出 ⌘+鍵（本程式送出的按鍵都打上記號，`+1 攔截時會放行）
    static func postCommand(_ code: CGKeyCode) {
        let src = CGEventSource(stateID: .hidSystemState)
        for down in [true, false] {
            guard let e = CGEvent(keyboardEventSource: src, virtualKey: code, keyDown: down) else { continue }
            e.flags = .maskCommand
            e.setIntegerValueField(.eventSourceUserData, value: pocketbriefEventMark)
            e.post(tap: .cghidEventTap)
        }
    }

    // 等使用者放開修飾鍵（最多 maxWait 秒）再執行；回傳參數表示是否真的放開了
    static func afterModifiersReleased(maxWait: Double, _ then: @escaping (Bool) -> Void) {
        let deadline = Date().addingTimeInterval(maxWait)
        func check() {
            if !modifiersDown() { then(true); return }
            if Date() >= deadline { then(false); return }
            DispatchQueue.main.asyncAfter(deadline: .now() + 0.03) { check() }
        }
        check()
    }
}

// ───────────── 輸出到目前的程式 ─────────────
enum Paster {
    // 剪貼簿的備份：每個項目各自保留常見格式的資料。nil＝無法備份（只有特殊格式），[]＝原本是空的
    typealias Snapshot = [[(NSPasteboard.PasteboardType, Data)]]

    // 只備份常見格式：逐一取出所有格式可能逼來源程式產生大量資料，而且有些格式無法還原
    private static let keepTypes: Set<NSPasteboard.PasteboardType> = [
        .string, .rtf, .rtfd, .html, .fileURL, .URL, .png, .tiff, .pdf, .tabularText,
        NSPasteboard.PasteboardType("public.utf16-external-plain-text"),
    ]

    // 尚未還原的剪貼簿備份。連續輸出兩次時要沿用「最早」那份，否則使用者原本的剪貼簿就永久不見了
    private static var pending: Snapshot?
    private static var pendingCount = 0
    private static var pendingWork: DispatchWorkItem?
    private static let restoreDelay = 1.2   // 給目標程式足夠時間讀取剪貼簿

    static func snapshot() -> Snapshot? {
        let pb = NSPasteboard.general
        guard let items = pb.pasteboardItems, !items.isEmpty else { return [] }
        var result: Snapshot = []
        for item in items {
            var pairs: [(NSPasteboard.PasteboardType, Data)] = []
            for t in item.types where keepTypes.contains(t) {
                if let d = item.data(forType: t) { pairs.append((t, d)) }
            }
            if !pairs.isEmpty { result.append(pairs) }
        }
        return result.isEmpty ? nil : result
    }

    static func restore(_ s: Snapshot) {
        let pb = NSPasteboard.general
        pb.clearContents()
        if s.isEmpty { return }
        let items: [NSPasteboardItem] = s.map { pairs in
            let it = NSPasteboardItem()
            for (t, d) in pairs { it.setData(d, forType: t) }
            return it
        }
        pb.writeObjects(items)
    }

    static func paste(_ text: String, restore: Bool, target: NSRunningApplication?) {
        let backup = restore ? takePendingOrBackup() : nil
        let pb = NSPasteboard.general
        pb.clearContents()
        pb.setString(text, forType: .string)
        let count = pb.changeCount
        if !Permissions.accessibility {
            // 沒有權限送不出 ⌘V：內容留在剪貼簿，請使用者自己貼
            Toast.show(L.t("已複製到剪貼簿"), L.t("請按 ⌘V 貼上。開啟「輔助使用」權限後就能自動貼上。"))
            return
        }
        if let t = target, t != NSWorkspace.shared.frontmostApplication { t.activate(options: []) }
        DispatchQueue.main.asyncAfter(deadline: .now() + 0.08) {
            Keys.afterModifiersReleased(maxWait: 0.6) { _ in
                Keys.postCommand(9)   // V
                if let b = backup { scheduleRestore(b, count) }
            }
        }
    }

    private static func takePendingOrBackup() -> Snapshot? {
        if let w = pendingWork {
            w.cancel(); pendingWork = nil
            let b = pending; pending = nil
            // 還原前使用者若自己又複製了別的東西，就以那個為準
            if let b = b, NSPasteboard.general.changeCount == pendingCount { return b }
        }
        return snapshot()
    }

    private static func scheduleRestore(_ b: Snapshot, _ count: Int) {
        pending = b
        pendingCount = count
        let w = DispatchWorkItem { flushPending() }
        pendingWork = w
        DispatchQueue.main.asyncAfter(deadline: .now() + restoreDelay, execute: w)
    }

    // 立刻完成尚未執行的剪貼簿還原
    static func flushPending() {
        guard let w = pendingWork else { return }
        w.cancel(); pendingWork = nil
        let b = pending; pending = nil
        if let b = b, NSPasteboard.general.changeCount == pendingCount { restore(b) }   // 使用者又複製了別的東西就不要蓋掉
    }

    // 複製目前選取的文字（快速新增用）；取完後把剪貼簿還原
    static func copySelection(_ done: @escaping (String) -> Void) {
        flushPending()   // 先完成上次輸出的還原，才不會把範本誤當成原本的剪貼簿
        let pb = NSPasteboard.general
        let backup = snapshot()
        let before = pb.changeCount
        Keys.postCommand(8)   // C
        var tries = 0
        func poll() {
            // 最多等 0.75 秒讓對方程式把選取內容放進剪貼簿
            if pb.changeCount != before || tries >= 25 {
                var text = ""
                if pb.changeCount != before {
                    text = pb.string(forType: .string) ?? ""
                    if let b = backup { restore(b) }   // backup 為 nil 表示備份失敗：保留現狀，不要清空
                }
                done(Template.trimNewlines(Template.normalizeNewlines(text)))
                return
            }
            tries += 1
            DispatchQueue.main.asyncAfter(deadline: .now() + 0.03) { poll() }
        }
        DispatchQueue.main.asyncAfter(deadline: .now() + 0.05) { poll() }
    }
}

// ───────────── 短暫的提示（取代 Windows 版的系統匣氣球提示） ─────────────
enum Toast {
    private static var panel: NSPanel?
    private static var hideWork: DispatchWorkItem?

    static func show(_ title: String, _ message: String = "", seconds: Double = 3.5) {
        hideWork?.cancel()
        panel?.orderOut(nil)
        let width: CGFloat = 340
        let titleLabel = NSTextField(labelWithString: title)
        titleLabel.font = NSFont.boldSystemFont(ofSize: 13)
        titleLabel.lineBreakMode = .byTruncatingTail
        let body = NSTextField(wrappingLabelWithString: message)
        body.font = NSFont.systemFont(ofSize: 12)
        body.preferredMaxLayoutWidth = width - 32
        let stack = NSStackView(views: message.isEmpty ? [titleLabel] : [titleLabel, body])
        stack.orientation = .vertical
        stack.alignment = .leading
        stack.spacing = 4
        stack.edgeInsets = NSEdgeInsets(top: 12, left: 16, bottom: 12, right: 16)
        let fx = NSVisualEffectView()
        fx.material = .hudWindow
        fx.blendingMode = .behindWindow
        fx.state = .active
        fx.wantsLayer = true
        fx.layer?.cornerRadius = 12
        fx.layer?.masksToBounds = true
        stack.translatesAutoresizingMaskIntoConstraints = false
        fx.addSubview(stack)
        NSLayoutConstraint.activate([
            stack.leadingAnchor.constraint(equalTo: fx.leadingAnchor), stack.trailingAnchor.constraint(equalTo: fx.trailingAnchor),
            stack.topAnchor.constraint(equalTo: fx.topAnchor), stack.bottomAnchor.constraint(equalTo: fx.bottomAnchor),
            fx.widthAnchor.constraint(equalToConstant: width),
        ])
        let p = NSPanel(contentRect: NSRect(x: 0, y: 0, width: width, height: 60), styleMask: [.borderless, .nonactivatingPanel], backing: .buffered, defer: false)
        p.isOpaque = false
        p.backgroundColor = .clear
        p.hasShadow = true
        p.level = .statusBar
        p.collectionBehavior = [.canJoinAllSpaces, .fullScreenAuxiliary]
        p.ignoresMouseEvents = true
        p.contentView = fx
        fx.layoutSubtreeIfNeeded()
        let size = fx.fittingSize
        if let screen = NSScreen.main {
            let vf = screen.visibleFrame
            p.setFrame(NSRect(x: vf.maxX - size.width - 16, y: vf.maxY - size.height - 12, width: size.width, height: size.height), display: true)
        }
        p.orderFrontRegardless()
        panel = p
        let w = DispatchWorkItem { panel?.orderOut(nil); panel = nil }
        hideWork = w
        DispatchQueue.main.asyncAfter(deadline: .now() + seconds, execute: w)
    }
}

// ───────────── 對話框 ─────────────
enum Alerts {
    // 回傳按下的按鈕順序（0 起算）
    @discardableResult
    static func show(_ message: String, info: String = "", buttons: [String] = [], style: NSAlert.Style = .informational) -> Int {
        NSApp.activate(ignoringOtherApps: true)
        let a = NSAlert()
        a.messageText = message
        a.informativeText = info
        a.alertStyle = style
        for b in (buttons.isEmpty ? [L.t("確定")] : buttons) {
            let btn = a.addButton(withTitle: b)
            // esc 對應到「取消」類的按鈕
            if b == L.t("取消") || b == L.t("稍後") || b == L.t("結束") { btn.keyEquivalent = "\u{1b}" }
        }
        let r = a.runModal()
        return r.rawValue - NSApplication.ModalResponse.alertFirstButtonReturn.rawValue
    }
}

// ───────────── 圖示 ─────────────
enum Icons {
    // 選單列圖示：圓角框裡一個「句」字（英文介面為 P），範本圖片會自動配合淺色／深色選單列
    static func menuBar() -> NSImage {
        let img = NSImage(size: NSSize(width: 18, height: 18), flipped: false) { rect in
            let box = NSBezierPath(roundedRect: rect.insetBy(dx: 1, dy: 1), xRadius: 4, yRadius: 4)
            NSColor.black.setFill()
            box.fill()
            let attrs: [NSAttributedString.Key: Any] = [.font: NSFont.boldSystemFont(ofSize: 12), .foregroundColor: NSColor.black]
            let s = L.iconGlyph as NSString
            let sz = s.size(withAttributes: attrs)
            NSGraphicsContext.current?.compositingOperation = .destinationOut
            s.draw(at: NSPoint(x: rect.midX - sz.width / 2, y: rect.midY - sz.height / 2), withAttributes: attrs)
            return true
        }
        img.isTemplate = true
        return img
    }
}
