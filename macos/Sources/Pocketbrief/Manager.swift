// 口袋句庫 Pocketbrief：句庫管理視窗（左：範本清單；右：直接編輯）
// Copyright (c) 2026 無名小律師（楊朝淵律師）. Licensed under the MIT License; see the LICENSE file.
import AppKit
import UniformTypeIdentifiers

// 在清單上按 ⌫ 刪除
final class KeyTableView: NSTableView {
    var onDelete: (() -> Void)?
    override func keyDown(with event: NSEvent) {
        if (event.keyCode == 51 || event.keyCode == 117) && event.modifierFlags.intersection([.command, .control, .option]).isEmpty {
            onDelete?()
            return
        }
        super.keyDown(with: event)
    }
}

final class ManagerWindowController: NSWindowController, NSWindowDelegate, NSTableViewDataSource, NSTableViewDelegate,
                                     NSTextFieldDelegate, NSTextViewDelegate {
    private unowned let app: AppController
    private let table = KeyTableView()
    private let search = NSSearchField()
    private let sizeStepper = NSStepper()
    private let sizeValue = UI.label("")
    private let split = NSSplitView()
    private let edHeader = UI.label("", bold: true)
    private var edTitle: NSTextField!
    private var edCode: NSTextField!
    private var edText: NSTextView!
    private let edInfo = UI.label("")
    private var btnSave: NSButton!
    private var btnRevert: NSButton!
    private let stCount = UI.label("")
    private let stState = UI.label("")
    private var watch: Timer?
    private var didPlaceDivider = false

    private var view: [Template] = []
    private var seenVersion = -1

    // 右側編輯區的狀態
    private var editing: Template?      // 正在編輯的那一筆（新範本草稿時為 nil）
    private var isDraft = false         // 是否為尚未存檔的新範本
    private var loadingEditor = false   // 程式正在填入欄位（不算使用者修改）
    private var dirty = false           // 有未儲存的修改
    private var suppressSelect = false  // 程式自己在改選取，不要觸發切換
    private var selPending = false      // 已排定一次「選取改變」的處理
    private var confirming = false      // 「要儲存嗎？」對話框正開著

    private var dataPath: String { app.settings.dataPath }
    private var contentFont: NSFont { NSFont.systemFont(ofSize: app.settings.contentFontSize) }

    init(app: AppController) {
        self.app = app
        let win = NSWindow(contentRect: NSRect(x: 0, y: 0, width: 1060, height: 640),
                           styleMask: [.titled, .closable, .miniaturizable, .resizable], backing: .buffered, defer: false)
        win.minSize = NSSize(width: 760, height: 440)
        super.init(window: win)
        win.delegate = self
        win.isReleasedWhenClosed = false
        buildUI()
        win.center()
        win.setFrameAutosaveName("PocketbriefManager")
        updateTitle()
        app.store.ensureLoaded(dataPath)
        refreshList(select: nil)
        showEmptyEditor()
        watch = Timer.scheduledTimer(withTimeInterval: 3, repeats: true) { [weak self] _ in self?.checkStore() }
        NotificationCenter.default.addObserver(self, selector: #selector(windowBecameKey), name: NSWindow.didBecomeKeyNotification, object: win)
    }

    required init?(coder: NSCoder) { fatalError() }

    func present() {
        showWindow(nil)
        window?.makeKeyAndOrderFront(nil)
        if !didPlaceDivider {
            didPlaceDivider = true
            split.layoutSubtreeIfNeeded()
            split.setPosition((split.bounds.width * 0.40).rounded(), ofDividerAt: 0)
        }
        window?.makeFirstResponder(table)
    }

    func updateTitle() {
        window?.title = L.f("{0}－句庫管理（叫出清單的快速鍵：{1}）", L.appName, Hotkey.display(app.settings.hotkey))
    }

    @objc private func windowBecameKey() { checkStore() }

    // 雲端同步進來的變更：比對版本號（別的地方先讀到新檔時，ensureLoaded 在這裡會回傳 false，不能只看回傳值）
    private func checkStore() {
        let changed = app.store.ensureLoaded(dataPath)
        if changed || app.store.version != seenVersion { reloadFromStore() }
    }

    // ── 版面 ──
    private func separator() -> NSView {
        let b = NSBox()
        b.boxType = .separator
        b.translatesAutoresizingMaskIntoConstraints = false
        b.widthAnchor.constraint(equalToConstant: 1).isActive = true
        b.heightAnchor.constraint(equalToConstant: 20).isActive = true
        return b
    }

    private func buildUI() {
        // 工具列
        let bNew = UI.button(L.t("＋ 新增範本"), self, #selector(newTemplate(_:)))
        bNew.toolTip = L.t("新增一筆範本（⌘N）")
        let bDel = UI.button(L.t("刪除"), self, #selector(deleteSelected(_:)))
        bDel.toolTip = L.t("刪除選取的範本（⌫）")
        let bImp = UI.button(L.t("匯入 CSV…"), self, #selector(importCSV(_:)))
        let bExp = UI.button(L.t("匯出 CSV…"), self, #selector(exportCSV(_:)))
        search.placeholderString = L.t("名稱、代碼或內容")
        search.sendsSearchStringImmediately = true
        search.target = self
        search.action = #selector(searchChanged)
        search.widthAnchor.constraint(equalToConstant: 220).isActive = true
        let sizeTitle = UI.label(L.t("內容字級"))
        sizeTitle.toolTip = L.t("範本清單與編輯區的文字大小（不影響視窗與按鈕的大小）")
        sizeStepper.minValue = 9
        sizeStepper.maxValue = 28
        sizeStepper.increment = 1
        sizeStepper.valueWraps = false
        sizeStepper.doubleValue = Double(app.settings.contentFontSize)
        sizeStepper.target = self
        sizeStepper.action = #selector(sizeChanged)
        sizeValue.stringValue = String(format: "%g", Double(app.settings.contentFontSize))
        let bSet = UI.button(L.t("設定…"), self, #selector(openSettings))
        let toolbar = UI.hstack([bNew, bDel, separator(), bImp, bExp, separator(), search, UI.spacer(), sizeTitle, sizeValue, sizeStepper, bSet])

        // 左：範本清單
        let colCode = NSTableColumn(identifier: NSUserInterfaceItemIdentifier("code"))
        colCode.title = L.t("代碼")
        colCode.width = 110
        colCode.minWidth = 50
        colCode.sortDescriptorPrototype = NSSortDescriptor(key: "code", ascending: true)
        let colName = NSTableColumn(identifier: NSUserInterfaceItemIdentifier("name"))
        colName.title = L.t("名稱")
        colName.minWidth = 80
        colName.sortDescriptorPrototype = NSSortDescriptor(key: "name", ascending: true)
        table.addTableColumn(colCode)
        table.addTableColumn(colName)
        table.columnAutoresizingStyle = .lastColumnOnlyAutoresizingStyle
        table.allowsMultipleSelection = true
        table.usesAlternatingRowBackgroundColors = true
        table.style = .fullWidth
        table.dataSource = self
        table.delegate = self
        table.target = self
        table.doubleAction = #selector(tableDoubleClick)
        table.onDelete = { [weak self] in self?.deleteSelected(nil) }
        table.rowHeight = PopupRenderer.lineHeight(contentFont) + 6
        let tableScroll = NSScrollView()
        tableScroll.documentView = table
        tableScroll.hasVerticalScroller = true
        tableScroll.borderType = .noBorder

        // 右：編輯區
        let font = contentFont
        edTitle = UI.textField(font: font)
        edCode = UI.codeField(font: font)
        let (textScroll, tv) = UI.textArea(font: font)
        edText = tv
        edTitle.delegate = self
        edCode.delegate = self
        edText.delegate = self
        let lt = UI.label(L.t("名稱")), lc = UI.label(L.t("代碼")), lx = UI.label(L.t("內容")), lh = UI.label("")
        UI.equalWidths([lt, lc, lx, lh])
        edInfo.textColor = .secondaryLabelColor
        btnRevert = UI.button(L.t("復原"), self, #selector(revertEditor(_:)))
        btnSave = UI.button(L.t("儲存"), self, #selector(saveTemplate(_:)))
        let rows: [NSView] = [
            edHeader,
            UI.hstack([lt, edTitle]),
            UI.hstack([lc, edCode]),
            UI.hstack([lh, UI.hint(L.t("英文字母、數字或符號，最多 20 碼（不分大小寫）。叫出清單後打這組代碼就會直接輸出。"))], align: .top),
            UI.hstack([lx, textScroll], align: .top),
            UI.hstack([edInfo, UI.spacer(), btnRevert, btnSave]),
        ]
        let editor = UI.vstack(rows, spacing: 10, insets: NSEdgeInsets(top: 12, left: 16, bottom: 12, right: 16))
        UI.fillWidth(Array(rows.dropFirst()), in: editor)

        split.isVertical = true
        split.dividerStyle = .thin
        split.addArrangedSubview(tableScroll)
        split.addArrangedSubview(editor)
        split.setHoldingPriority(NSLayoutConstraint.Priority(260), forSubviewAt: 0)
        split.setContentHuggingPriority(NSLayoutConstraint.Priority(1), for: .vertical)
        tableScroll.widthAnchor.constraint(greaterThanOrEqualToConstant: 220).isActive = true
        editor.widthAnchor.constraint(greaterThanOrEqualToConstant: 380).isActive = true

        // 狀態列
        stCount.textColor = .secondaryLabelColor
        stState.lineBreakMode = .byTruncatingTail
        stState.setContentCompressionResistancePriority(NSLayoutConstraint.Priority(1), for: .horizontal)
        let copy = NSButton(title: L.copyright, target: self, action: #selector(showAbout))
        copy.isBordered = false
        copy.font = NSFont.systemFont(ofSize: NSFont.smallSystemFontSize)
        copy.contentTintColor = .secondaryLabelColor
        let status = UI.hstack([stCount, stState, UI.spacer(), copy], spacing: 12)

        let main = UI.vstack([toolbar, split, status], spacing: 6, insets: NSEdgeInsets(top: 8, left: 10, bottom: 6, right: 10))
        UI.fillWidth([toolbar, split, status], in: main)
        main.translatesAutoresizingMaskIntoConstraints = false
        let content = NSView()
        content.addSubview(main)
        NSLayoutConstraint.activate([
            main.leadingAnchor.constraint(equalTo: content.leadingAnchor), main.trailingAnchor.constraint(equalTo: content.trailingAnchor),
            main.topAnchor.constraint(equalTo: content.topAnchor), main.bottomAnchor.constraint(equalTo: content.bottomAnchor),
        ])
        window?.contentView = content
    }

    // ── 工具列動作（也接收選單的 ⌘N、⌘S 等） ──
    @objc func newTemplate(_ sender: Any?) {
        if !confirmLeaveEditor() { return }
        selectOnly(nil)
        editing = nil; isDraft = true; dirty = false
        fill("", "", "")
        edHeader.stringValue = L.t("新增範本")
        setEditorEnabled(true)
        updateEditorState()
        window?.makeFirstResponder(edTitle)
    }

    @objc func saveTemplate(_ sender: Any?) { _ = saveEditor() }

    @objc func focusSearch(_ sender: Any?) { window?.makeFirstResponder(search) }

    @objc private func searchChanged() { refreshList(select: editing) }

    @objc private func sizeChanged() {
        let v = CGFloat(sizeStepper.doubleValue)
        app.setContentFontSize(v)
    }

    @objc private func openSettings() { app.openSettings() }

    @objc private func showAbout() { app.showAbout() }

    @objc private func tableDoubleClick() {
        if !isDraft && editing != nil { window?.makeFirstResponder(edText); edText.setSelectedRange(NSRange(location: 0, length: 0)) }
    }

    // 範本文字字級變更：只換清單與編輯區的字型，視窗大小不變
    func applyContentFont() {
        let font = contentFont
        table.rowHeight = PopupRenderer.lineHeight(font) + 6
        table.reloadData()
        let was = loadingEditor
        loadingEditor = true   // 換字型不算修改
        edTitle.font = font
        edCode.font = font
        edText.font = font
        loadingEditor = was
        sizeStepper.doubleValue = Double(app.settings.contentFontSize)
        sizeValue.stringValue = String(format: "%g", Double(app.settings.contentFontSize))
    }

    // ── 清單 ──
    func reloadFromStore() {
        // 同步進來的新資料：編輯中的那筆若沒有修改，就換成新讀到的同一筆
        if let e = editing, !dirty { editing = app.store.items.first { $0.sameAs(e) } }
        refreshList(select: editing)
        if !dirty {
            if let e = editing { loadEditor(e) }
            else if !isDraft { showEmptyEditor() }
        }
        updateStatus()
    }

    private func refreshList(select: Template?) {
        let q = search.stringValue.trimmingCharacters(in: .whitespacesAndNewlines)
        var v = app.store.items.filter { t in
            q.isEmpty || t.title.range(of: q, options: .caseInsensitive) != nil || t.code.range(of: q, options: .caseInsensitive) != nil
                || t.text.range(of: q, options: .caseInsensitive) != nil
        }
        if let sd = table.sortDescriptors.first, let key = sd.key {
            let asc = sd.ascending
            let order = Dictionary(uniqueKeysWithValues: v.enumerated().map { (ObjectIdentifier($0.element), $0.offset) })
            v.sort { a, b in
                let x = key == "code" ? a.code : a.displayTitle
                let y = key == "code" ? b.code : b.displayTitle
                let r = ManagerWindowController.compareNatural(x, y)
                if r != 0 { return asc ? r < 0 : r > 0 }
                return order[ObjectIdentifier(a)]! < order[ObjectIdentifier(b)]!
            }
        }
        view = v
        seenVersion = app.store.version
        suppressSelect = true
        table.reloadData()
        var idx = -1
        if let s = select { idx = view.firstIndex(where: { $0 === s || $0.sameAs(s) }) ?? -1 }
        if idx >= 0 { table.selectRowIndexes(IndexSet(integer: idx), byExtendingSelection: false); table.scrollRowToVisible(idx) }
        else { table.deselectAll(nil) }
        suppressSelect = false
        updateStatus()
    }

    private func updateStatus() {
        let total = app.store.items.count
        stCount.stringValue = view.count == total ? L.f("共 {0} 筆", total) : L.f("符合 {0}／{1} 筆", view.count, total)
        if let e = app.store.error {
            stState.stringValue = "⚠ " + e.replacingOccurrences(of: "\n", with: " ")
            stState.textColor = .systemRed
        } else if stState.textColor == .systemRed {
            stState.stringValue = ""
            stState.textColor = .labelColor
        }
    }

    private static func compareNatural(_ a: String, _ b: String) -> Int {
        let x = Int64(a), y = Int64(b)
        if let x = x, let y = y { return x < y ? -1 : (x > y ? 1 : 0) }
        if (x != nil) != (y != nil) { return x != nil ? -1 : 1 }
        switch a.localizedStandardCompare(b) {
        case .orderedAscending: return -1
        case .orderedDescending: return 1
        default: return 0
        }
    }

    private func selectedTemplate() -> Template? {
        let rows = table.selectedRowIndexes
        guard rows.count == 1, let r = rows.first, r < view.count else { return nil }
        return view[r]
    }

    private func selectOnly(_ t: Template?) {
        suppressSelect = true
        if let t = t, let i = view.firstIndex(where: { $0 === t }) {
            table.selectRowIndexes(IndexSet(integer: i), byExtendingSelection: false)
            table.scrollRowToVisible(i)
        } else {
            table.deselectAll(nil)
        }
        suppressSelect = false
    }

    // ── NSTableView ──
    func numberOfRows(in tableView: NSTableView) -> Int { view.count }

    func tableView(_ tableView: NSTableView, viewFor tableColumn: NSTableColumn?, row: Int) -> NSView? {
        guard let col = tableColumn, row < view.count else { return nil }
        let id = NSUserInterfaceItemIdentifier("cell")
        let cell = (tableView.makeView(withIdentifier: id, owner: self) as? NSTableCellView) ?? makeCell(id)
        let t = view[row]
        let isCode = col.identifier.rawValue == "code"
        cell.textField?.stringValue = isCode ? (t.code.isEmpty ? "—" : t.code) : t.displayTitle
        cell.textField?.font = contentFont
        cell.textField?.textColor = isCode && t.code.isEmpty ? .secondaryLabelColor : .labelColor
        return cell
    }

    private func makeCell(_ id: NSUserInterfaceItemIdentifier) -> NSTableCellView {
        let c = NSTableCellView()
        c.identifier = id
        let tf = NSTextField(labelWithString: "")
        tf.lineBreakMode = .byTruncatingTail
        tf.translatesAutoresizingMaskIntoConstraints = false
        c.addSubview(tf)
        c.textField = tf
        NSLayoutConstraint.activate([
            tf.leadingAnchor.constraint(equalTo: c.leadingAnchor, constant: 4),
            tf.trailingAnchor.constraint(equalTo: c.trailingAnchor, constant: -4),
            tf.centerYAnchor.constraint(equalTo: c.centerYAnchor),
        ])
        return c
    }

    func tableView(_ tableView: NSTableView, sortDescriptorsDidChange oldDescriptors: [NSSortDescriptor]) {
        refreshList(select: editing)
    }

    func tableViewSelectionDidChange(_ notification: Notification) {
        if suppressSelect || selPending { return }
        // 點選時可能連續觸發好幾次：只排一次，延後一拍再判斷
        selPending = true
        DispatchQueue.main.async { [weak self] in self?.handleSelectionChange() }
    }

    private func handleSelectionChange() {
        selPending = false
        if suppressSelect || confirming { return }
        var now = selectedTemplate()
        let count = table.selectedRowIndexes.count
        if let n = now, n === editing, !isDraft { return }
        if !confirmLeaveEditor() {
            selectOnly(isDraft ? nil : editing)   // 使用者按取消：選回正在編輯的那筆
            return
        }
        // 剛才若存了檔，清單已重新整理（物件換新），用內容找回使用者點的那一筆
        if let n = now, !view.contains(where: { $0 === n }) { now = view.first { $0.sameAs(n) } }
        if let n = now { selectOnly(n); loadEditor(n) }
        else if count > 1 { showMultiSelected(count) }
        else { showEmptyEditor() }
    }

    // ── 右側編輯區 ──
    private func setEditorEnabled(_ on: Bool) {
        edTitle.isEnabled = on
        edCode.isEnabled = on
        edText.isEditable = on
        edText.isSelectable = on
        edText.textColor = on ? .textColor : .secondaryLabelColor
    }

    private func fill(_ title: String, _ code: String, _ text: String) {
        loadingEditor = true
        edTitle.stringValue = title
        edCode.stringValue = code
        edText.string = text
        edText.undoManager?.removeAllActions()
        loadingEditor = false
    }

    private func loadEditor(_ t: Template) {
        editing = t; isDraft = false; dirty = false
        fill(t.title, t.code, t.text)
        edHeader.stringValue = L.t("編輯範本")
        setEditorEnabled(true)
        updateEditorState()
    }

    private func showEmptyEditor() {
        editing = nil; isDraft = false; dirty = false
        fill("", "", "")
        edHeader.stringValue = L.t("在左側點選一筆範本即可編輯，或按「＋ 新增範本」。")
        setEditorEnabled(false)
        updateEditorState()
    }

    private func showMultiSelected(_ n: Int) {
        editing = nil; isDraft = false; dirty = false
        fill("", "", "")
        edHeader.stringValue = L.f("已選取 {0} 筆範本（可按「刪除」一次刪除）", n)
        setEditorEnabled(false)
        updateEditorState()
    }

    private func updateEditorState() {
        let active = isDraft || editing != nil
        btnSave.isEnabled = active && (dirty || isDraft)
        btnRevert.isEnabled = active && dirty
        if !active { edInfo.stringValue = ""; return }
        var info = L.f("字數：{0}", Template.normalizeNewlines(edText.string).count)
        if dirty { info += L.t("　（尚未儲存，⌘S 儲存）") }
        edInfo.stringValue = info
    }

    private func markDirty() {
        if loadingEditor { return }
        dirty = true
        updateEditorState()
    }

    func controlTextDidChange(_ obj: Notification) {
        if (obj.object as? NSTextField) === edCode, edCode.stringValue.count > 20 {
            edCode.stringValue = String(edCode.stringValue.prefix(20))   // 代碼最多 20 碼
        }
        markDirty()
    }

    func textDidChange(_ notification: Notification) { markDirty() }

    @objc private func revertEditor(_ sender: Any?) {
        if isDraft { fill("", "", ""); dirty = false; updateEditorState(); return }
        if let e = editing { loadEditor(e) }
    }

    // 回傳 true 表示已存檔（或沒有需要存的）
    @discardableResult
    private func saveEditor() -> Bool {
        if !isDraft && editing == nil { return true }
        if !dirty && !isDraft { return true }
        let (r, bad) = TemplateValidator.build(title: edTitle.stringValue, code: edCode.stringValue, content: edText.string,
                                              original: isDraft ? nil : editing, existing: app.store.items)
        guard let t = r else {
            switch bad {
            case .code?: window?.makeFirstResponder(edCode)
            case .content?: window?.makeFirstResponder(edText)
            case .title?: window?.makeFirstResponder(edTitle)
            case nil: break
            }
            return false
        }
        let original = editing
        let wasDraft = isDraft
        let ok = app.store.mutate(dataPath, rescueText: t.text) { list in
            var i: Int? = nil
            if !wasDraft { i = list.firstIndex(where: { $0.sameAs(original) }) }
            if let i = i { list[i] = t } else { list.append(t) }   // 原本那筆已在別台電腦被改掉時，當作新增
        }
        if !ok { return false }
        if wasDraft && !search.stringValue.isEmpty { search.stringValue = "" }
        let saved = app.store.items.first(where: { $0.sameAs(t) }) ?? t
        refreshList(select: saved)
        loadEditor(saved)
        stState.textColor = .labelColor
        stState.stringValue = L.t("已儲存。")
        return true
    }

    // 離開目前編輯的範本前，確認是否要儲存；回傳 false 表示使用者取消
    func confirmLeaveEditor() -> Bool {
        if !dirty { return true }
        if confirming { return false }   // 已經在問了，不要疊第二個對話框
        confirming = true
        defer { confirming = false }
        let name = isDraft ? L.t("新範本") : (editing?.displayTitle ?? "")
        let r = Alerts.show(L.f("「{0}」有尚未儲存的修改，要儲存嗎？", name), buttons: [L.t("儲存"), L.t("不儲存"), L.t("取消")])
        if r == 0 { return saveEditor() }
        if r == 1 { revertEditor(nil); return true }   // 放棄修改：編輯區恢復成原本的內容，免得之後又被一起存進去
        return false
    }

    // ── 刪除、匯入、匯出 ──
    @objc func deleteSelected(_ sender: Any?) {
        let targets = table.selectedRowIndexes.compactMap { $0 < view.count ? view[$0] : nil }
        if targets.isEmpty { Alerts.show(L.t("請先在左側點選要刪除的範本（按住 ⌘ 或 ⇧ 可以多選）。")); return }
        let editingDeleted = editing.map { e in targets.contains { $0 === e } } ?? false
        if dirty && !editingDeleted && !confirmLeaveEditor() { return }   // 另一筆有沒存的修改，先處理它
        let msg = targets.count == 1 ? L.f("確定要刪除「{0}」嗎？", targets[0].displayTitle) : L.f("確定要刪除選取的 {0} 筆範本嗎？", targets.count)
        if Alerts.show(msg, buttons: [L.t("刪除"), L.t("取消")], style: .warning) != 0 { return }
        let ok = app.store.mutate(dataPath) { list in
            for t in targets { if let i = list.firstIndex(where: { $0.sameAs(t) }) { list.remove(at: i) } }
        }
        if !ok { return }
        if editingDeleted || (editing == nil && !isDraft) {
            dirty = false
            refreshList(select: nil)
            showEmptyEditor()
        } else {
            // 刪的是別筆：編輯區保留（草稿或正在編輯的那筆）
            if let e = editing { editing = app.store.items.first { $0.sameAs(e) } }
            refreshList(select: editing)
        }
    }

    @objc func exportCSV(_ sender: Any?) {
        app.store.ensureLoaded(dataPath)
        let p = NSSavePanel()
        let df = DateFormatter()
        df.locale = Locale(identifier: "en_US_POSIX")
        df.dateFormat = "yyyyMMdd"
        p.nameFieldStringValue = L.appName + "_" + df.string(from: Date()) + ".csv"
        p.allowedContentTypes = [.commaSeparatedText]
        p.directoryURL = FileManager.default.urls(for: .desktopDirectory, in: .userDomainMask).first
        guard p.runModal() == .OK, let url = p.url else { return }
        do {
            try TextFile.write(url.path, Csv.write(app.store.items))
            Alerts.show(L.f("已匯出 {0} 筆範本：\n{1}", app.store.items.count, url.path),
                        info: L.t("提醒：這個檔案可以用 Excel 開來看，但不建議用 Excel 修改後另存——Excel 會把代碼開頭的 0 去掉（01 變成 1）、把 1-2 當成日期、把 = 或 + 開頭的內容當成公式。要修改範本，請在「句庫管理」裡改。"))
        } catch {
            Alerts.show(L.t("匯出失敗：") + error.localizedDescription, style: .warning)
        }
    }

    @objc func importCSV(_ sender: Any?) {
        if !confirmLeaveEditor() { return }
        let p = NSOpenPanel()
        p.canChooseFiles = true
        p.canChooseDirectories = false
        p.allowsMultipleSelection = false
        p.directoryURL = FileManager.default.urls(for: .desktopDirectory, in: .userDomainMask).first
        guard p.runModal() == .OK, let url = p.url else { return }
        let incoming: [Template]
        do { incoming = Csv.toTemplates(Csv.parse(try TextFile.read(url.path))) }
        catch { Alerts.show(L.t("讀取失敗：") + error.localizedDescription, style: .warning); return }
        merge(incoming, source: url.lastPathComponent)
    }

    private func merge(_ input: [Template], source: String) {
        var incoming = input
        let clearedCodes = Template.normalizeAll(&incoming)   // 與存檔後再讀回的內容一致，避免重複匯入被誤判成新範本
        app.store.ensureLoaded(dataPath)
        if let e = app.store.error { Alerts.show(e, info: L.t("為避免覆蓋掉資料，這次不匯入。"), style: .warning); return }
        let cur = app.store.items
        var fresh: [Template] = [], conflicts: [Template] = []
        var dup = 0
        for t in incoming {
            var same = false, conflict = false
            for c in cur {
                if c.key == t.key && c.text == t.text { same = true; break }
                if !t.code.isEmpty && c.key == t.key { conflict = true }
            }
            // 同一批匯入資料裡的重複（不論歸在新範本或代碼衝突）也略過
            if !same && (fresh + conflicts).contains(where: { $0.key == t.key && $0.text == t.text }) { same = true }
            if same { dup += 1 } else if conflict { conflicts.append(t) } else { fresh.append(t) }
        }
        if incoming.isEmpty { Alerts.show(L.f("在「{0}」裡沒有讀到任何範本。", source)); return }
        if fresh.isEmpty && conflicts.isEmpty {
            Alerts.show(L.f("從「{0}」讀到 {1} 筆，全部都已經在清單裡了，不需要匯入。", source, incoming.count))
            return
        }
        var s = L.f("‧新範本 {0} 筆", fresh.count) + "\n"
        if dup > 0 { s += L.f("‧已經存在、略過 {0} 筆", dup) + "\n" }
        if clearedCodes > 0 { s += L.f("‧有 {0} 筆的代碼含空白、中文或超過 20 碼，無法用來叫出，已改為無代碼（內容照常匯入）", clearedCodes) + "\n" }
        var replace = false
        if !conflicts.isEmpty {
            s += L.f("‧代碼跟現有範本相同、但內容不同 {0} 筆（例如代碼 {1}）", conflicts.count, conflicts[0].code) + "\n\n"
            s += L.t("代碼相同的要怎麼處理？「取代」會用匯入的內容取代現有那筆；「兩筆都保留」的話，打這組代碼時清單會停住讓你選。")
            let r = Alerts.show(L.f("從「{0}」讀到 {1} 筆：", source, incoming.count), info: s,
                                buttons: [L.t("取代"), L.t("兩筆都保留"), L.t("取消")])
            if r == 2 || r < 0 { return }
            replace = r == 0
        } else {
            if Alerts.show(L.f("從「{0}」讀到 {1} 筆：", source, incoming.count), info: s + "\n" + L.t("確定要匯入嗎？"),
                           buttons: [L.t("匯入"), L.t("取消")]) != 0 { return }
        }
        let ok = app.store.mutate(dataPath) { list in
            // 取代時每一筆「原有的」範本只會被取代一次：同一批裡有兩筆代碼相同時，第二筆改為新增
            let originalCount = list.count
            var replaced = Set<Int>()
            for t in conflicts {
                var at = -1
                if replace {
                    for i in 0..<originalCount where !replaced.contains(i) && list[i].key == t.key { at = i; break }
                }
                if at >= 0 { list[at] = t; replaced.insert(at) } else { list.append(t) }
            }
            list.append(contentsOf: fresh)
        }
        if ok {
            search.stringValue = ""
            refreshList(select: nil)
            showEmptyEditor()
            Alerts.show(L.f("匯入完成，目前共有 {0} 筆範本。", app.store.items.count))
        }
    }

    // ── 視窗 ──
    func windowShouldClose(_ sender: NSWindow) -> Bool { confirmLeaveEditor() }

    func windowWillClose(_ notification: Notification) {
        watch?.invalidate()
        watch = nil
        NotificationCenter.default.removeObserver(self)
        app.managerDidClose()
    }
}
