// 口袋句庫 Pocketbrief：設定視窗
// Copyright (c) 2026 無名小律師（楊朝淵律師）. Licensed under the MIT License; see the LICENSE file.
import AppKit
import ServiceManagement

// ───────────── 錄製快速鍵的格子 ─────────────
// 點一下格子，直接按下想用的組合鍵；按住 ` 再按一個鍵會錄成「`+鍵」
final class HotkeyField: NSView {
    var value: String { didSet { needsDisplay = true } }
    private let allowClear: Bool
    private var prefixHeld = false

    init(value: String, allowClear: Bool) {
        self.value = value
        self.allowClear = allowClear
        super.init(frame: NSRect(x: 0, y: 0, width: 220, height: 26))
        translatesAutoresizingMaskIntoConstraints = false
        widthAnchor.constraint(equalToConstant: 220).isActive = true
        heightAnchor.constraint(equalToConstant: 26).isActive = true
    }

    required init?(coder: NSCoder) { fatalError() }

    override var acceptsFirstResponder: Bool { true }
    override var canBecomeKeyView: Bool { true }
    override func becomeFirstResponder() -> Bool { needsDisplay = true; return true }
    override func resignFirstResponder() -> Bool { prefixHeld = false; needsDisplay = true; return true }
    override func mouseDown(with event: NSEvent) { window?.makeFirstResponder(self) }

    private var focused: Bool { window?.firstResponder === self }

    override func draw(_ dirtyRect: NSRect) {
        let r = bounds.insetBy(dx: 1, dy: 1)
        let path = NSBezierPath(roundedRect: r, xRadius: 5, yRadius: 5)
        NSColor.textBackgroundColor.setFill()
        path.fill()
        (focused ? NSColor.keyboardFocusIndicatorColor : NSColor.separatorColor).setStroke()
        path.lineWidth = focused ? 2 : 1
        path.stroke()
        let text: String
        let color: NSColor
        if focused && prefixHeld { text = L.t("` + …（請再按一個鍵）"); color = .secondaryLabelColor }
        else if value.isEmpty { text = focused ? L.t("請按下組合鍵") : L.t("（未設定）"); color = .secondaryLabelColor }
        else { text = Hotkey.display(value); color = .labelColor }
        PopupRenderer.drawLine(text, in: r.insetBy(dx: 8, dy: 0), font: NSFont.systemFont(ofSize: 13), color: color, center: true)
    }

    override func keyDown(with event: NSEvent) {
        let code = event.keyCode
        let flags = event.modifierFlags.intersection([.command, .control, .option, .shift])
        if code == 53 || code == 36 || code == 76 { super.keyDown(with: event); return }   // esc、return：保留關閉視窗的功能
        if code == 48 {   // tab：切換欄位
            if flags.contains(.shift) { window?.selectPreviousKeyView(self) } else { window?.selectNextKeyView(self) }
            return
        }
        if code == 50 && flags.isEmpty { prefixHeld = true; needsDisplay = true; return }
        var v: String?
        if prefixHeld { v = Hotkey.fromChordKey(code: code, flags: flags) }
        else if allowClear && (code == 51 || code == 117) && flags.isEmpty { value = ""; return }
        else { v = Hotkey.fromEvent(code: code, flags: flags) }
        if let v = v { value = v } else { NSSound.beep() }
    }

    override func keyUp(with event: NSEvent) {
        if event.keyCode == 50 { prefixHeld = false; needsDisplay = true }
    }

    // ⌘、⌃ 組合鍵會先被選單攔走：格子有焦點時先交給錄製
    override func performKeyEquivalent(with event: NSEvent) -> Bool {
        guard focused, event.type == .keyDown else { return super.performKeyEquivalent(with: event) }
        if [53, 36, 76, 48].contains(event.keyCode) { return super.performKeyEquivalent(with: event) }
        keyDown(with: event)
        return true
    }
}

// ───────────── 配色主題縮圖 ─────────────
final class SwatchView: NSView {
    let preset: ThemePreset
    var current = false { didSet { needsDisplay = true } }
    var onClick: (() -> Void)?
    private var hot = false

    init(preset: ThemePreset) {
        self.preset = preset
        super.init(frame: NSRect(x: 0, y: 0, width: 96, height: 70))
        translatesAutoresizingMaskIntoConstraints = false
        widthAnchor.constraint(equalToConstant: 96).isActive = true
        heightAnchor.constraint(equalToConstant: 70).isActive = true
        if preset.eyeCare { toolTip = L.t("護眼配色：低對比、少藍光，適合長時間閱讀。") }
        addTrackingArea(NSTrackingArea(rect: .zero, options: [.mouseEnteredAndExited, .activeInActiveApp, .inVisibleRect], owner: self, userInfo: nil))
    }

    required init?(coder: NSCoder) { fatalError() }

    override var isFlipped: Bool { true }
    override func mouseEntered(with event: NSEvent) { hot = true; needsDisplay = true }
    override func mouseExited(with event: NSEvent) { hot = false; needsDisplay = true }
    override func mouseDown(with event: NSEvent) { onClick?() }

    override func draw(_ dirtyRect: NSRect) {
        let st = PopupStyle()
        preset.apply(to: st)
        let dark = PopupRenderer.isDark(self)
        let labelH: CGFloat = 18
        let box = NSRect(x: 4, y: 4, width: bounds.width - 8, height: bounds.height - labelH - 8)
        st.backR.resolved.setFill(); box.fill()
        let hh = max(3, (box.height / 4).rounded())
        st.headerBack.resolved.setFill(); NSRect(x: box.minX, y: box.minY, width: box.width, height: hh).fill()
        let rowH = max(3, ((box.height - hh) / 3).rounded(.down))
        st.selBack.resolved.setFill(); NSRect(x: box.minX, y: box.minY + hh, width: box.width, height: rowH).fill()
        for i in 0..<3 {   // 每列：代碼（短）＋內容（長）的示意線
            let y = box.minY + hh + CGFloat(i) * rowH + rowH / 2 - 1
            let code = i == 0 ? st.selFore : st.codeForeR(dark: dark)
            let text = i == 0 ? st.selFore : st.foreR
            code.resolved.setFill(); NSRect(x: box.minX + 5, y: y, width: 9, height: 2).fill()
            text.resolved.setFill(); NSRect(x: box.minX + 19, y: y, width: box.width - 27 - CGFloat(i) * 7, height: 2).fill()
        }
        st.headerFore.resolved.setFill(); NSRect(x: box.minX + 5, y: box.minY + hh / 2 - 1, width: box.width / 2 - 5, height: 2).fill()
        let border = NSBezierPath(rect: box.insetBy(dx: -1, dy: -1))
        border.lineWidth = current ? 3 : 1
        (current ? NSColor.controlAccentColor : hot ? NSColor.labelColor : NSColor.separatorColor).setStroke()
        border.stroke()
        PopupRenderer.drawLine(L.t(preset.name), in: NSRect(x: 0, y: bounds.height - labelH, width: bounds.width, height: labelH),
                               font: NSFont.systemFont(ofSize: 11), color: .labelColor, center: true)
    }
}

// ───────────── 清單外觀的即時預覽 ─────────────
final class PreviewView: NSView {
    var style = PopupStyle() { didSet { needsDisplay = true } }
    override var isFlipped: Bool { true }

    override func draw(_ dirtyRect: NSRect) {
        NSColor.windowBackgroundColor.setFill()
        bounds.fill()
        let m = PopupMetrics(style: style)
        let dark = PopupRenderer.isDark(self)
        let w = min(bounds.width, CGFloat(style.width))
        let outer = NSRect(x: 0, y: 0, width: w, height: bounds.height)
        style.headerBack.resolved.setFill(); outer.fill()
        let inner = outer.insetBy(dx: 1, dy: 1)
        let hr = NSRect(x: inner.minX, y: inner.minY, width: inner.width, height: m.headerH)
        PopupRenderer.drawLine(L.appName + "　" + L.f("代碼：{0}", "1") + "▌　(3/12)", in: hr.insetBy(dx: 10, dy: 0), font: m.headerFont, color: style.headerFore.resolved)
        let body = NSRect(x: inner.minX, y: hr.maxY, width: inner.width, height: inner.maxY - hr.maxY)
        style.backR.resolved.setFill(); body.fill()
        let rows = [("1", L.t("法院公鑒"), L.t("此致 ⏎ 臺灣臺北地方法院　公鑒")),
                    ("12", L.t("審閱檢查"), L.t("幫我用 /legal-reviewer審閱、檢查一下")),
                    ("111", L.t("聯絡資訊"), L.t("電子郵件：name@example.com"))]
        var y = body.minY
        for (i, r) in rows.enumerated() where y + m.rowH <= body.maxY {
            PopupRenderer.drawRow(NSRect(x: body.minX, y: y, width: body.width, height: m.rowH), code: r.0, title: r.1, text: r.2,
                                  selected: i == 0, dim: false, style: style, metrics: m, dark: dark)
            y += m.rowH
        }
        if y + m.rowH <= body.maxY {
            let pr = NSRect(x: body.minX, y: y + 6, width: body.width, height: body.maxY - y - 6)
            style.backR.mixed(with: style.foreR, 0.06).setFill(); pr.fill()
            PopupRenderer.drawWrapped(L.t("此致\n臺灣臺北地方法院　公鑒"), in: pr.insetBy(dx: 10, dy: 6), font: m.rowFont, color: style.foreR.resolved)
        }
    }
}

// ───────────── 數值調整（數字＋上下鈕） ─────────────
final class NumberStepper: NSStackView {
    private let field = UI.label("")
    private let stepper = NSStepper()
    var onChange: ((Double) -> Void)?
    private let suffix: String

    init(min: Double, max: Double, step: Double, value: Double, suffix: String = "") {
        self.suffix = suffix
        super.init(frame: .zero)
        orientation = .horizontal
        spacing = 4
        stepper.minValue = min
        stepper.maxValue = max
        stepper.increment = step
        stepper.valueWraps = false
        stepper.doubleValue = value
        stepper.target = self
        stepper.action = #selector(changed)
        field.alignment = .right
        field.widthAnchor.constraint(greaterThanOrEqualToConstant: 34).isActive = true
        addArrangedSubview(field)
        addArrangedSubview(stepper)
        if !suffix.isEmpty { addArrangedSubview(UI.label(suffix)) }
        show()
    }

    required init?(coder: NSCoder) { fatalError() }

    var value: Double {
        get { stepper.doubleValue }
        set { stepper.doubleValue = newValue; show() }
    }

    private func show() { field.stringValue = String(format: "%g", stepper.doubleValue) }

    @objc private func changed() { show(); onChange?(stepper.doubleValue) }
}

struct SettingsResult {
    let settings: Settings
    let launchAtLogin: Bool
    let folder: String
}

// ───────────── 設定視窗 ─────────────
final class SettingsWindowController: NSWindowController, NSWindowDelegate {
    private let original: Settings
    private var work: PopupStyle
    private let onClose: (SettingsResult?) -> Void
    private var closedWith: SettingsResult?
    private var finished = false

    private let hotkey: HotkeyField
    private let quickAdd: HotkeyField
    private let contentSize: NumberStepper
    private let autoCommit: NSButton
    private let restoreClip: NSButton
    private let launchAtLogin: NSButton
    private let langBox = NSPopUpButton()
    private let themeBox = NSPopUpButton()
    private let folderLabel = UI.label("")
    private var folder: String
    private let axStatus = UI.label("")
    private let imStatus = UI.label("")

    private let fontBox = NSPopUpButton()
    private var fontSize: NumberStepper!
    private var widthBox: NumberStepper!
    private var rowsBox: NumberStepper!
    private var wells: [(NSColorWell, (PopupStyle) -> NSColor, (PopupStyle, NSColor) -> Void)] = []
    private let followBack = UI.checkbox("", on: false)
    private let followFore = UI.checkbox("", on: false)
    private var swatches: [SwatchView] = []
    private let preview = PreviewView()
    private var loading = false

    init(settings: Settings, folder: String, onClose: @escaping (SettingsResult?) -> Void) {
        original = settings
        work = settings.style.copy()
        self.folder = folder
        self.onClose = onClose
        hotkey = HotkeyField(value: settings.hotkey, allowClear: false)
        quickAdd = HotkeyField(value: settings.quickAddHotkey, allowClear: true)
        contentSize = NumberStepper(min: 9, max: 28, step: 1, value: Double(settings.contentFontSize))
        autoCommit = UI.checkbox(L.t("打的代碼只對應到一筆時，不必按 Enter 就直接輸出"), on: settings.autoCommit)
        restoreClip = UI.checkbox(L.t("輸出後把剪貼簿還原成原本的內容"), on: settings.restoreClipboard)
        launchAtLogin = UI.checkbox(L.t("登入時自動啟動（每台 Mac 各自設定）"), on: SMAppService.mainApp.status == .enabled)
        let win = NSWindow(contentRect: NSRect(x: 0, y: 0, width: 780, height: 700), styleMask: [.titled, .closable, .miniaturizable, .resizable],
                           backing: .buffered, defer: false)
        win.title = L.f("{0} 設定", L.appName)
        win.contentMinSize = NSSize(width: 740, height: 400)
        super.init(window: win)
        win.delegate = self
        win.isReleasedWhenClosed = false

        let tabs = NSTabView()
        let t1 = NSTabViewItem(identifier: "behavior")
        t1.label = L.t("快速鍵與行為")
        t1.view = behaviorPage()
        let t2 = NSTabViewItem(identifier: "style")
        t2.label = L.t("清單外觀")
        t2.view = stylePage()
        tabs.addTabViewItem(t1)
        tabs.addTabViewItem(t2)

        let ok = UI.button(L.t("確定"), self, #selector(okAction))
        ok.keyEquivalent = "\r"
        let cancel = UI.button(L.t("取消"), self, #selector(cancelAction))
        cancel.keyEquivalent = "\u{1b}"
        let bottom = UI.hstack([UI.spacer(), cancel, ok])
        // 分頁區隨視窗伸縮（內容放不下時在分頁裡捲動），「確定」「取消」固定在最下面
        tabs.setContentHuggingPriority(NSLayoutConstraint.Priority(1), for: .vertical)
        tabs.setContentCompressionResistancePriority(NSLayoutConstraint.Priority(1), for: .vertical)
        let main = UI.vstack([tabs, bottom], spacing: 10, insets: NSEdgeInsets(top: 12, left: 14, bottom: 14, right: 14))
        UI.fillWidth([tabs, bottom], in: main)
        main.translatesAutoresizingMaskIntoConstraints = false
        let content = NSView()
        content.addSubview(main)
        NSLayoutConstraint.activate([
            main.leadingAnchor.constraint(equalTo: content.leadingAnchor), main.trailingAnchor.constraint(equalTo: content.trailingAnchor),
            main.topAnchor.constraint(equalTo: content.topAnchor), main.bottomAnchor.constraint(equalTo: content.bottomAnchor),
        ])
        win.contentView = content
        loadStyleControls()
        styleChanged()
        refreshPermissions()
        NotificationCenter.default.addObserver(self, selector: #selector(refreshPermissions), name: NSWindow.didBecomeKeyNotification, object: win)
    }

    required init?(coder: NSCoder) { fatalError() }

    private var sized = false

    func present() {
        NSApp.activate(ignoringOtherApps: true)
        if !sized, let w = window {
            sized = true
            // 依螢幕可用範圍（扣掉選單列與 Dock）決定高度，並置中
            if let vf = (NSScreen.main ?? NSScreen.screens.first)?.visibleFrame {
                let frame = w.frameRect(forContentRect: NSRect(x: 0, y: 0, width: 780, height: min(820, vf.height - 60)))
                w.setFrame(NSRect(x: vf.midX - frame.width / 2, y: vf.minY + (vf.height - frame.height) / 2,
                                  width: frame.width, height: frame.height), display: false)
            }
            UI.fitOnScreen(w)
        }
        window?.makeKeyAndOrderFront(nil)
        window?.makeFirstResponder(hotkey)
    }

    // ── 版面工具 ──
    private func grid(_ rows: [[NSView]]) -> NSGridView {
        let g = NSGridView(views: rows)
        g.rowSpacing = 8
        g.columnSpacing = 12
        g.column(at: 0).xPlacement = .trailing
        g.rowAlignment = .firstBaseline
        return g
    }

    private func hintCell(_ s: String) -> NSTextField {
        let h = UI.hint(s)
        h.preferredMaxLayoutWidth = 500
        return h
    }

    // 分頁內容放在可上下捲動的區域裡：螢幕較矮時不會被截掉
    private func pageView(_ inner: NSView) -> NSView {
        let doc = FlippedView()
        doc.translatesAutoresizingMaskIntoConstraints = false
        inner.translatesAutoresizingMaskIntoConstraints = false
        doc.addSubview(inner)
        let scroll = NSScrollView()
        scroll.hasVerticalScroller = true
        scroll.autohidesScrollers = true
        scroll.drawsBackground = false
        scroll.borderType = .noBorder
        scroll.documentView = doc
        let clip = scroll.contentView
        NSLayoutConstraint.activate([
            doc.leadingAnchor.constraint(equalTo: clip.leadingAnchor),
            doc.topAnchor.constraint(equalTo: clip.topAnchor),
            doc.widthAnchor.constraint(equalTo: clip.widthAnchor),
            inner.leadingAnchor.constraint(equalTo: doc.leadingAnchor, constant: 18),
            inner.trailingAnchor.constraint(lessThanOrEqualTo: doc.trailingAnchor, constant: -18),
            inner.topAnchor.constraint(equalTo: doc.topAnchor, constant: 14),
            doc.bottomAnchor.constraint(equalTo: inner.bottomAnchor, constant: 14),
        ])
        return scroll
    }

    // ── 分頁一：快速鍵與行為 ──
    private func behaviorPage() -> NSView {
        let empty = { NSGridCell.emptyContentView }
        langBox.addItem(withTitle: L.t("自動（依系統語言）"))
        for n in L.nativeNames { langBox.addItem(withTitle: n) }
        if let i = L.codes.firstIndex(where: { $0.caseInsensitiveCompare(original.language) == .orderedSame }) { langBox.selectItem(at: i + 1) }
        else { langBox.selectItem(at: 0) }
        themeBox.addItems(withTitles: [L.t("跟隨系統"), L.t("淺色"), L.t("深色")])
        themeBox.selectItem(at: original.appTheme == "light" ? 1 : original.appTheme == "dark" ? 2 : 0)
        folderLabel.lineBreakMode = .byTruncatingMiddle
        folderLabel.stringValue = folder
        folderLabel.toolTip = folder
        folderLabel.widthAnchor.constraint(lessThanOrEqualToConstant: 380).isActive = true
        let folderRow = UI.hstack([folderLabel, UI.button(L.t("變更…"), self, #selector(chooseFolder))])
        let axRow = UI.hstack([axStatus, UI.button(L.t("開啟系統設定"), self, #selector(openAX))])
        let imRow = UI.hstack([imStatus, UI.button(L.t("開啟系統設定"), self, #selector(openIM))])
        let langTitle = L.code == "en" ? "Language" : L.code == "ja" ? "表示言語 / Language" : L.code == "zh-CN" ? "界面语言 / Language" : "介面語言 / Language"

        let g = grid([
            [UI.label(L.t("叫出範本清單")), hotkey],
            [empty(), hintCell(L.t("點一下格子，直接按下想用的組合鍵（例如 ⌃`、⌥`、F9），\n或按住 ` 再按一個鍵（例如 `+1）。單獨一個鍵（F1～F12 除外）不能當快速鍵，避免打字時誤觸。"))],
            [UI.label(L.t("快速新增範本")), quickAdd],
            [empty(), hintCell(L.t("在任何地方先反白一段文字，再按這組鍵，就會開啟「新增範本」並自動帶入反白的內容。\n點格子後按 ⌫ 可清除（停用此功能）。"))],
            [UI.label(L.t("句庫內容字體大小")), contentSize],
            [empty(), hintCell(L.t("句庫管理裡範本清單與編輯區的文字大小。叫出的範本清單則到「清單外觀」分頁調整。"))],
            [empty(), autoCommit],
            [empty(), restoreClip],
            [empty(), launchAtLogin],
            [UI.label(langTitle), langBox],
            [empty(), hintCell(L.t("更改語言後，程式會自動重新啟動。"))],
            [UI.label(L.t("外觀模式")), themeBox],
            [empty(), hintCell(L.t("深色模式會套用到句庫管理、設定等視窗。叫出的範本清單有自己的配色，請到「清單外觀」分頁選擇（例如「深色」主題）。"))],
            [UI.label(L.t("句庫資料夾")), folderRow],
            [empty(), hintCell(L.t("範本.csv 與設定都存在這個資料夾。想和 Windows 版或其他 Mac 共用句庫，請選雲端硬碟裡同一個資料夾。"))],
            [UI.label(L.t("輔助使用")), axRow],
            [empty(), hintCell(L.t("貼上範本、讀取游標位置、快速新增都需要這個權限。"))],
            [UI.label(L.t("輸入監控")), imRow],
            [empty(), hintCell(L.t("只有快速鍵設成「` + 某個鍵」時才需要。"))],
        ])
        return pageView(g)
    }

    @objc private func refreshPermissions() {
        axStatus.stringValue = Permissions.accessibility ? L.t("✅ 已開啟") : L.t("⚠️ 尚未開啟")
        imStatus.stringValue = Permissions.inputMonitoring ? L.t("✅ 已開啟") : L.t("⚠️ 尚未開啟")
    }

    @objc private func openAX() { Permissions.requestAccessibilityPrompt(); Permissions.openAccessibilitySettings() }
    @objc private func openIM() { Permissions.requestInputMonitoring(); Permissions.openInputMonitoringSettings() }

    @objc private func chooseFolder() {
        let p = NSOpenPanel()
        p.canChooseDirectories = true
        p.canChooseFiles = false
        p.canCreateDirectories = true
        p.allowsMultipleSelection = false
        p.prompt = L.t("選擇")
        p.message = L.t("選擇句庫資料夾（範本.csv 所在的資料夾）")
        p.directoryURL = URL(fileURLWithPath: folder)
        guard let w = window else { return }
        p.beginSheetModal(for: w) { [weak self] r in
            guard r == .OK, let url = p.url, let self = self else { return }
            self.folder = url.path
            self.folderLabel.stringValue = url.path
            self.folderLabel.toolTip = url.path
        }
    }

    // ── 分頁二：清單外觀 ──
    private func stylePage() -> NSView {
        // 配色主題：點一下套用一整組顏色（之後仍可在下方個別微調）
        var swatchRows: [NSView] = []
        var row: [NSView] = []
        for p in ThemePreset.all {
            let s = SwatchView(preset: p)
            s.onClick = { [weak self] in
                guard let self = self else { return }
                p.apply(to: self.work)
                self.loadStyleControls()
                self.styleChanged()
            }
            swatches.append(s)
            row.append(s)
            if row.count == 5 { swatchRows.append(UI.hstack(row, spacing: 6)); row = [] }
        }
        if !row.isEmpty { swatchRows.append(UI.hstack(row, spacing: 6)) }
        let themes = UI.vstack([UI.label(L.t("配色主題（點一下套用）"), bold: true)] + swatchRows, spacing: 6)

        // 字型（選單裡每個字型用它自己的字體顯示）
        buildFontMenu()
        fontBox.target = self
        fontBox.action = #selector(fontChosen)
        fontBox.widthAnchor.constraint(equalToConstant: 320).isActive = true
        fontSize = NumberStepper(min: 9, max: 40, step: 1, value: Double(work.fontSize))
        fontSize.onChange = { [weak self] v in self?.updateStyle { $0.fontSize = CGFloat(v) } }
        widthBox = NumberStepper(min: 360, max: 1600, step: 20, value: Double(work.width))
        widthBox.onChange = { [weak self] v in self?.updateStyle { $0.width = Int(v) } }
        rowsBox = NumberStepper(min: 3, max: 30, step: 1, value: Double(work.rows), suffix: L.t("筆"))
        rowsBox.onChange = { [weak self] v in self?.updateStyle { $0.rows = Int(v) } }

        followBack.title = L.t("跟隨系統")
        followFore.title = L.t("跟隨系統")
        followBack.target = self; followBack.action = #selector(followChanged)
        followFore.target = self; followFore.action = #selector(followChanged)
        let colorDefs: [(String, (PopupStyle) -> NSColor, (PopupStyle, NSColor) -> Void)] = [
            (L.t("底色"), { $0.backR.resolved }, { $0.back = $1 }),
            (L.t("文字顏色"), { $0.foreR.resolved }, { $0.fore = $1 }),
            (L.t("代碼顏色"), { $0.codeFore }, { $0.codeFore = $1 }),
            (L.t("選取列底色"), { $0.selBack }, { $0.selBack = $1 }),
            (L.t("選取列文字"), { $0.selFore }, { $0.selFore = $1 }),
            (L.t("標題列底色"), { $0.headerBack }, { $0.headerBack = $1 }),
            (L.t("標題列文字"), { $0.headerFore }, { $0.headerFore = $1 }),
        ]
        var colorCells: [NSView] = []
        for (i, d) in colorDefs.enumerated() {
            let w = NSColorWell()
            w.tag = i
            w.target = self
            w.action = #selector(colorChanged(_:))
            w.widthAnchor.constraint(equalToConstant: 44).isActive = true
            w.heightAnchor.constraint(equalToConstant: 24).isActive = true
            wells.append((w, d.1, d.2))
            var parts: [NSView] = [UI.label(d.0), w]
            if i == 0 { parts.append(followBack) }
            if i == 1 { parts.append(followFore) }
            colorCells.append(UI.hstack(parts))
        }
        let reset = UI.button(L.t("恢復預設外觀"), self, #selector(resetStyle))
        let colorGrid = NSGridView(views: [
            [colorCells[0], colorCells[1]],
            [colorCells[2], colorCells[3]],
            [colorCells[4], colorCells[5]],
            [colorCells[6], reset],
        ])
        colorGrid.rowSpacing = 8
        colorGrid.columnSpacing = 28

        let form = grid([
            [UI.label(L.t("字型")), fontBox],
            [UI.label(L.t("字體大小")), fontSize],
            [UI.label(L.t("清單寬度")), widthBox],
            [UI.label(L.t("一次顯示")), rowsBox],
        ])

        preview.translatesAutoresizingMaskIntoConstraints = false
        preview.heightAnchor.constraint(equalToConstant: 190).isActive = true
        preview.widthAnchor.constraint(equalToConstant: 640).isActive = true
        let page = UI.vstack([themes, form, colorGrid, UI.label(L.t("預覽"), bold: true), preview], spacing: 14)
        return pageView(page)
    }

    // 常用中文字型：排在清單最前面
    private static let favorites: [(String, String)] = [
        ("PingFang TC", "蘋方-繁（預設）"), ("Heiti TC", "黑體-繁"), ("Songti TC", "宋體-繁"), ("Kaiti TC", "楷體-繁"),
        ("BiauKai", "標楷體"), ("LiSong Pro", "儷宋 Pro"), ("LiHei Pro", "儷黑 Pro"),
        ("Noto Serif TC", "思源宋體（Noto Serif TC）"), ("Source Han Serif TC", "思源宋體 TC"),
        ("Noto Sans TC", "思源黑體（Noto Sans TC）"), ("Source Han Sans TC", "思源黑體 TC"),
        ("LXGW WenKai TC", "霞鶩文楷 TC"), ("TW-Kai", "全字庫正楷體"), ("TW-Sung", "全字庫正宋體"),
    ]

    private func buildFontMenu() {
        let fm = NSFontManager.shared
        let families = fm.availableFontFamilies
        var ordered: [String] = []
        for (n, _) in SettingsWindowController.favorites where families.contains(n) { ordered.append(n) }
        let rest = families.filter { !ordered.contains($0) && !$0.hasPrefix(".") }
            .sorted { (fm.localizedName(forFamily: $0, face: nil)).localizedStandardCompare(fm.localizedName(forFamily: $1, face: nil)) == .orderedAscending }
        ordered += rest
        if !ordered.contains(work.fontName) { ordered.insert(work.fontName, at: 0) }
        fontBox.removeAllItems()
        for n in ordered {
            var label = SettingsWindowController.favorites.first(where: { $0.0 == n })?.1 ?? fm.localizedName(forFamily: n, face: nil)
            if L.code == "zh-CN" { label = L.toSimplified(label) }
            if !families.contains(n) { label += L.t("（這台電腦沒有安裝）") }
            let item = NSMenuItem(title: label, action: nil, keyEquivalent: "")
            item.representedObject = n
            if let f = NSFont(name: n, size: 13) ?? fm.font(withFamily: n, traits: [], weight: 5, size: 13) {
                item.attributedTitle = NSAttributedString(string: label, attributes: [.font: f])
            }
            fontBox.menu?.addItem(item)
        }
    }

    private func updateStyle(_ change: (PopupStyle) -> Void) {
        if loading { return }
        change(work)
        styleChanged()
    }

    @objc private func fontChosen() {
        if let n = fontBox.selectedItem?.representedObject as? String { updateStyle { $0.fontName = n } }
    }

    @objc private func colorChanged(_ sender: NSColorWell) {
        if loading { return }
        let (_, _, setter) = wells[sender.tag]
        setter(work, sender.color)
        if sender.tag == 0 { followBack.state = .off }
        if sender.tag == 1 { followFore.state = .off }
        styleChanged()
    }

    @objc private func followChanged() {
        if loading { return }
        work.back = followBack.state == .on ? nil : (work.back ?? NSColor.textBackgroundColor.resolved)
        work.fore = followFore.state == .on ? nil : (work.fore ?? NSColor.textColor.resolved)
        loadStyleControls()
        styleChanged()
    }

    @objc private func resetStyle() {
        work = PopupStyle()
        loadStyleControls()
        styleChanged()
    }

    private func loadStyleControls() {
        loading = true
        if let i = fontBox.itemArray.firstIndex(where: { ($0.representedObject as? String) == work.fontName }) { fontBox.selectItem(at: i) }
        fontSize?.value = Double(work.fontSize)
        widthBox?.value = Double(work.width)
        rowsBox?.value = Double(work.rows)
        for (w, getter, _) in wells { w.color = getter(work) }
        followBack.state = work.back == nil ? .on : .off
        followFore.state = work.fore == nil ? .on : .off
        loading = false
    }

    private func styleChanged() {
        preview.style = work.copy()
        for s in swatches { s.current = s.preset.matches(work) }
    }

    // ── 確定／取消 ──
    @objc private func okAction() {
        if !Hotkey.isValid(hotkey.value) { Alerts.show(L.t("叫出清單的快速鍵看不懂，請重新設定。")); return }
        if !quickAdd.value.isEmpty {
            if !Hotkey.isValid(quickAdd.value) { Alerts.show(L.t("快速新增的快速鍵看不懂，請重新設定。")); return }
            if quickAdd.value.caseInsensitiveCompare(hotkey.value) == .orderedSame { Alerts.show(L.t("兩組快速鍵不能相同。")); return }
        }
        let r = original.clone()
        r.hotkey = hotkey.value
        r.quickAddHotkey = quickAdd.value
        r.autoCommit = autoCommit.state == .on
        r.restoreClipboard = restoreClip.state == .on
        r.contentFontSize = CGFloat(contentSize.value)
        r.language = langBox.indexOfSelectedItem <= 0 ? "auto" : L.codes[langBox.indexOfSelectedItem - 1]
        r.appTheme = themeBox.indexOfSelectedItem == 1 ? "light" : themeBox.indexOfSelectedItem == 2 ? "dark" : "auto"
        r.style = work.copy()
        closedWith = SettingsResult(settings: r, launchAtLogin: launchAtLogin.state == .on, folder: folder)
        window?.close()
    }

    @objc private func cancelAction() {
        closedWith = nil
        window?.close()
    }

    func windowWillClose(_ notification: Notification) {
        if finished { return }
        finished = true
        NSColorPanel.shared.orderOut(nil)
        NotificationCenter.default.removeObserver(self)
        onClose(closedWith)
    }
}
