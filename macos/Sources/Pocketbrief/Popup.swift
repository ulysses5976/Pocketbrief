// 口袋句庫 Pocketbrief：叫出的範本清單
// Copyright (c) 2026 無名小律師（楊朝淵律師）. Licensed under the MIT License; see the LICENSE file.
//
// 用「不啟用本程式」的浮動面板：清單收得到按鍵，原本的程式仍維持在前景，關閉後直接貼回去。
import AppKit

final class PopupPanel: NSPanel {
    override var canBecomeKey: Bool { true }
    override var canBecomeMain: Bool { false }
}

final class PopupController: NSObject, NSWindowDelegate {
    unowned let app: AppController
    let panel: PopupPanel
    let view: PopupView
    private(set) var buffer = ""
    private(set) var shown: [Template] = []
    private(set) var sel = 0
    private(set) var top = 0
    private var target: NSRunningApplication?
    private(set) var style = PopupStyle()
    private(set) var metrics: PopupMetrics
    private var appliedKey = ""

    init(app: AppController) {
        self.app = app
        metrics = PopupMetrics(style: PopupStyle())
        panel = PopupPanel(contentRect: NSRect(x: 0, y: 0, width: 640, height: 400),
                           styleMask: [.borderless, .nonactivatingPanel], backing: .buffered, defer: false)
        view = PopupView(frame: NSRect(x: 0, y: 0, width: 640, height: 400))
        super.init()
        panel.level = .popUpMenu
        panel.isFloatingPanel = true
        panel.hidesOnDeactivate = false
        panel.becomesKeyOnlyIfNeeded = false
        panel.collectionBehavior = [.canJoinAllSpaces, .fullScreenAuxiliary, .transient]
        panel.hasShadow = true
        panel.isOpaque = false
        panel.backgroundColor = .clear
        panel.title = L.appName
        view.controller = self
        panel.contentView = view
        panel.delegate = self
        applyStyle(app.settings.style)
    }

    var isVisible: Bool { panel.isVisible }

    // 套用外觀設定；設定沒變就不重做
    func applyStyle(_ st: PopupStyle) {
        let key = st.key
        if key == appliedKey { return }
        appliedKey = key
        style = st.copy()
        metrics = PopupMetrics(style: style)
        let size = NSSize(width: CGFloat(style.width), height: metrics.totalHeight(rows: style.rows))
        panel.setContentSize(size)
        view.frame = NSRect(origin: .zero, size: size)
        view.needsDisplay = true
    }

    func open(target: NSRunningApplication?) {
        self.target = target
        buffer = ""
        applyStyle(app.settings.style)
        refilter()
        place()
        panel.makeKeyAndOrderFront(nil)
        panel.makeFirstResponder(view)
        // 稍後確認：沒拿到鍵盤焦點的話，清單不能留在畫面上，否則打的代碼會跑進原本的程式
        DispatchQueue.main.asyncAfter(deadline: .now() + 0.25) { [weak self] in
            guard let self = self, self.panel.isVisible, !self.panel.isKeyWindow else { return }
            self.close(refocus: false)
        }
    }

    // 放在游標（插入點）下方；取不到游標位置就放在滑鼠旁邊
    private func place() {
        let size = panel.frame.size
        var anchorBottom: NSPoint, anchorTop: NSPoint
        if let r = Caret.rect() {
            anchorBottom = NSPoint(x: r.minX, y: r.minY)
            anchorTop = NSPoint(x: r.minX, y: r.maxY)
        } else {
            let m = NSEvent.mouseLocation
            anchorBottom = NSPoint(x: m.x, y: m.y - 8)
            anchorTop = NSPoint(x: m.x, y: m.y + 8)
        }
        let screen = NSScreen.screens.first(where: { NSMouseInRect(anchorBottom, $0.frame, false) }) ?? NSScreen.main ?? NSScreen.screens[0]
        let vf = screen.visibleFrame
        var x = anchorBottom.x
        var y = anchorBottom.y - 6 - size.height
        if y < vf.minY { y = anchorTop.y + 6 }   // 下方放不下就放到游標上方
        if x + size.width > vf.maxX { x = vf.maxX - size.width }
        if x < vf.minX { x = vf.minX }
        if y + size.height > vf.maxY { y = vf.maxY - size.height }
        if y < vf.minY { y = vf.minY }
        panel.setFrameOrigin(NSPoint(x: x, y: y))
    }

    func close(refocus: Bool) {
        panel.orderOut(nil)
        if refocus, let t = target, t != NSWorkspace.shared.frontmostApplication { t.activate(options: []) }
    }

    func windowDidResignKey(_ notification: Notification) {
        if panel.isVisible { close(refocus: false) }
    }

    // ── 篩選與選取 ──
    func refilter() {
        let all = app.store.items
        if buffer.isEmpty { shown = all }
        else {
            shown = all.filter { !$0.code.isEmpty && $0.key.hasPrefix(buffer) }
            if shown.isEmpty {   // 沒有代碼符合時，改搜尋名稱與內容（例如打 pdf）
                shown = all.filter { $0.title.range(of: buffer, options: .caseInsensitive) != nil || $0.text.range(of: buffer, options: .caseInsensitive) != nil }
            }
        }
        sel = 0
        if !buffer.isEmpty, let exact = shown.firstIndex(where: { $0.key == buffer }) { sel = exact }
        top = 0
        ensureVisible()
        view.needsDisplay = true
    }

    var headerText: String {
        var title = L.appName + "　" + (buffer.isEmpty ? L.t("請打代碼") : L.f("代碼：{0}", buffer) + "▌")
        if let e = app.store.error { title = e.replacingOccurrences(of: "\n", with: " ") }
        return title + "　(\(shown.count)/\(app.store.items.count))"
    }

    var emptyText: String {
        app.store.items.isEmpty ? L.t("（還沒有範本，按 F2 開啟句庫管理新增）") : L.t("（沒有符合的範本）")
    }

    var selected: Template? { sel >= 0 && sel < shown.count ? shown[sel] : nil }

    private func ensureVisible() {
        let rows = style.rows
        if sel < top { top = sel }
        if sel >= top + rows { top = sel - rows + 1 }
        top = max(0, min(top, max(0, shown.count - rows)))
    }

    func moveSel(_ delta: Int) {
        if shown.isEmpty { return }
        sel = max(0, min(shown.count - 1, sel + delta))
        ensureVisible()
        view.needsDisplay = true
    }

    func scroll(_ delta: Int) {
        top = max(0, min(max(0, shown.count - style.rows), top + delta))
        view.needsDisplay = true
    }

    func select(row: Int) {
        let i = top + row
        if i >= 0 && i < shown.count { sel = i; view.needsDisplay = true }
    }

    func typeChar(_ c: Character) {
        if buffer.count >= 20 { return }
        buffer += String(c).lowercased()
        refilter()
        if !app.settings.autoCommit { return }
        var exact: Template?
        var longer = false
        for t in app.store.items {
            let k = t.key
            if k == buffer { if exact == nil { exact = t } else { longer = true } }   // 代碼重複時也停住讓使用者選
            else if k.count > buffer.count && k.hasPrefix(buffer) { longer = true }
        }
        if let e = exact, !longer { commit(e) }
    }

    func backspace() {
        if buffer.isEmpty { return }
        buffer.removeLast()
        refilter()
    }

    func commitSelected() { if let t = selected { commit(t) } }

    private func commit(_ t: Template) {
        let tgt = target
        close(refocus: false)
        Paster.paste(t.text, restore: app.settings.restoreClipboard, target: tgt)
    }

    func openManager() {
        close(refocus: false)
        app.showManager()
    }
}

final class PopupView: NSView {
    weak var controller: PopupController?

    override var isFlipped: Bool { true }
    override var acceptsFirstResponder: Bool { true }

    // 版面
    private struct Layout { var header: NSRect; var list: NSRect; var preview: NSRect; var hint: NSRect }

    private func layout(_ c: PopupController) -> Layout {
        let m = c.metrics
        let inner = bounds.insetBy(dx: 1, dy: 1)
        let header = NSRect(x: inner.minX, y: inner.minY, width: inner.width, height: m.headerH)
        let list = NSRect(x: inner.minX, y: header.maxY, width: inner.width, height: m.rowH * CGFloat(c.style.rows) + 2)
        let hint = NSRect(x: inner.minX, y: inner.maxY - m.hintH, width: inner.width, height: m.hintH)
        let preview = NSRect(x: inner.minX, y: list.maxY, width: inner.width, height: hint.minY - list.maxY)
        return Layout(header: header, list: list, preview: preview, hint: hint)
    }

    override func draw(_ dirtyRect: NSRect) {
        guard let c = controller else { return }
        let st = c.style, m = c.metrics
        let dark = PopupRenderer.isDark(self)
        let l = layout(c)
        st.headerBack.resolved.setFill()
        bounds.fill()   // 外框
        PopupRenderer.drawLine(c.headerText, in: l.header.insetBy(dx: 10, dy: 0), font: m.headerFont, color: st.headerFore.resolved)
        st.backR.resolved.setFill()
        l.list.fill()
        if c.shown.isEmpty {
            PopupRenderer.drawRow(NSRect(x: l.list.minX, y: l.list.minY, width: l.list.width, height: m.rowH), code: "", title: c.emptyText,
                                  text: "", selected: false, dim: true, style: st, metrics: m, dark: dark)
        } else {
            var y = l.list.minY
            var i = c.top
            while i < c.shown.count && i < c.top + st.rows {
                let t = c.shown[i]
                PopupRenderer.drawRow(NSRect(x: l.list.minX, y: y, width: l.list.width, height: m.rowH),
                                      code: t.code.isEmpty ? "·" : t.code, title: t.title, text: t.preview,
                                      selected: i == c.sel, dim: false, style: st, metrics: m, dark: dark)
                y += m.rowH
                i += 1
            }
        }
        let pane = st.backR.mixed(with: st.foreR, 0.06)
        pane.setFill()
        l.preview.fill()
        l.hint.fill()
        if let t = c.selected {
            PopupRenderer.drawWrapped(t.text, in: l.preview.insetBy(dx: 10, dy: 6), font: m.rowFont, color: st.foreR.resolved)
        }
        PopupRenderer.drawLine(L.t("打代碼跳選　↑↓ 移動　↩ 輸出　⌫ 刪代碼　esc 取消　F2 句庫管理"), in: l.hint.insetBy(dx: 10, dy: 0),
                               font: m.hintFont, color: st.backR.mixed(with: st.foreR, 0.55))
    }

    override func keyDown(with event: NSEvent) {
        guard let c = controller else { return }
        let flags = event.modifierFlags.intersection([.command, .control, .option, .shift])
        switch event.keyCode {
        case 53: c.close(refocus: true)                        // esc
        case 36, 76, 49: c.commitSelected()                     // return、enter、空白鍵
        case 126: c.moveSel(-1)                                 // ↑
        case 125: c.moveSel(1)                                  // ↓
        case 116: c.moveSel(-8)                                 // page up
        case 121: c.moveSel(8)                                  // page down
        case 115: c.moveSel(-100000)                            // home
        case 119: c.moveSel(100000)                             // end
        case 51: c.backspace()                                  // delete（⌫）
        case 120: c.openManager()                               // F2
        case 50 where !flags.contains(.control): c.close(refocus: true)   // 再按一下 ` 也是取消
        default:
            if !flags.intersection([.command, .control, .option]).isEmpty { return }
            if let ch = KeyChar.char(keyCode: event.keyCode, shift: flags.contains(.shift)),
               let v = ch.unicodeScalars.first?.value, v > 32 && v <= 126 && ch != "`" && ch != "~" {
                c.typeChar(ch)
            }
        }
    }

    override func performKeyEquivalent(with event: NSEvent) -> Bool {
        // 清單開著時不讓 ⌘ 組合鍵觸發本程式的選單
        window?.isKeyWindow == true && event.modifierFlags.contains(.command)
    }

    override func mouseDown(with event: NSEvent) {
        guard let c = controller else { return }
        let p = convert(event.locationInWindow, from: nil)
        let l = layout(c)
        guard l.list.contains(p) else { return }
        let row = Int((p.y - l.list.minY) / c.metrics.rowH)
        c.select(row: row)
        if event.clickCount >= 2 { c.commitSelected() }
    }

    // 觸控板會送出一連串細小的捲動量：累積到一列的高度才捲一列；滑鼠滾輪每格捲一列
    private var scrollAccum: CGFloat = 0

    override func scrollWheel(with event: NSEvent) {
        guard let c = controller else { return }
        if !event.hasPreciseScrollingDeltas {
            if abs(event.scrollingDeltaY) >= 0.5 { c.scroll(event.scrollingDeltaY > 0 ? -1 : 1) }
            return
        }
        if event.phase == .began { scrollAccum = 0 }
        scrollAccum += event.scrollingDeltaY
        let step = c.metrics.rowH
        while abs(scrollAccum) >= step {
            c.scroll(scrollAccum > 0 ? -1 : 1)
            scrollAccum -= scrollAccum > 0 ? step : -step
        }
    }
}
