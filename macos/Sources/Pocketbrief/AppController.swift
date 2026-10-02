// 口袋句庫 Pocketbrief：常駐程式本體（選單列圖示、快速鍵、各視窗）
// Copyright (c) 2026 無名小律師（楊朝淵律師）. Licensed under the MIT License; see the LICENSE file.
import AppKit
import ServiceManagement

let appVersion = (Bundle.main.object(forInfoDictionaryKey: "CFBundleShortVersionString") as? String) ?? "4.3.0"

final class AppController: NSObject, NSApplicationDelegate {
    var settings = Settings()
    let store = Store()
    private var popup: PopupController!
    private var statusItem: NSStatusItem?
    private var manager: ManagerWindowController?
    private var settingsWC: SettingsWindowController?
    private var quickAddBusy = false   // 從按下快速鍵到對話框關閉為止都是 true，防止重複觸發
    private var iniTime: Date?
    private var chordTap: ChordTap?
    private var hotkeyOk = false
    private var pendingFontSize: CGFloat?
    private var fontSaveWork: DispatchWorkItem?
    private static let hotkeyId: UInt32 = 1, quickAddId: UInt32 = 2

    // ── 啟動 ──
    func applicationDidFinishLaunching(_ notification: Notification) {
        settings = Settings.load() ?? Settings()
        L.initialize(settings.language)
        guard ensureSingleInstance() else { NSApp.terminate(nil); return }
        applyAppearance()
        buildMainMenu()
        guard ensureDataFolder() else { NSApp.terminate(nil); return }
        reload()
        popup = PopupController(app: self)
        buildStatusItem()
        HotkeyCenter.shared.onPress = { [weak self] id in self?.onHotkey(id) }
        HotkeyCenter.shared.install()
        registerHotkeys()
        if hotkeyOk {
            Toast.show(L.f("{0} 已啟動", L.fullName),
                       L.f("按 {0} 叫出範本清單", Hotkey.display(settings.hotkey))
                       + (settings.quickAddHotkey.isEmpty ? "" : L.f("；反白文字後按 {0} 快速新增", Hotkey.display(settings.quickAddHotkey)))
                       + L.t("。\n點選單列的圖示可管理範本。"))
        }
        if !Permissions.accessibility {
            DispatchQueue.main.asyncAfter(deadline: .now() + 0.6) { Permissions.explainAccessibility() }
        }
        if CommandLine.arguments.contains("--manage") { showManager() }
    }

    // 同時只執行一份；重新啟動時（--restart）等前一份結束
    private func ensureSingleInstance() -> Bool {
        guard let bid = Bundle.main.bundleIdentifier else { return true }
        let me = ProcessInfo.processInfo.processIdentifier
        func others() -> [NSRunningApplication] {
            NSRunningApplication.runningApplications(withBundleIdentifier: bid).filter { $0.processIdentifier != me && !$0.isTerminated }
        }
        if CommandLine.arguments.contains("--restart") {
            let deadline = Date().addingTimeInterval(15)
            while !others().isEmpty && Date() < deadline { RunLoop.current.run(until: Date().addingTimeInterval(0.2)) }
        }
        if let o = others().first {
            Alerts.show(L.f("{0}已經在執行了（請看選單列的圖示）。", L.appName))
            o.activate(options: [])
            return false
        }
        return true
    }

    // 第一次使用：選擇句庫資料夾。之後若資料夾暫時不在（例如雲端硬碟還沒掛載），不再詢問，由範本清單顯示狀況
    private func ensureDataFolder() -> Bool {
        if Settings.dataFolder != nil { return true }
        guard let f = DataFolderChooser.choose() else { return false }
        Settings.dataFolder = f
        settings = Settings.load() ?? Settings()
        L.initialize(settings.language)
        applyAppearance()
        buildMainMenu()
        return true
    }

    private func reload() {
        if let p = Settings.iniPath { iniTime = Store.fileInfo(p).mtime }
        store.ensureLoaded(settings.dataPath)
    }

    // 設定檔被另一台 Mac（經雲端同步）改過時，重新讀入；讀不到時沿用目前設定
    private func reloadSettingsIfChanged() {
        guard let p = Settings.iniPath else { return }
        let fi = Store.fileInfo(p)
        if !fi.exists || fi.mtime == iniTime { return }
        guard let loaded = Settings.load() else { return }
        let oldKeys = settings.hotkey + "|" + settings.quickAddHotkey
        settings = loaded
        iniTime = fi.mtime
        if oldKeys != settings.hotkey + "|" + settings.quickAddHotkey && settingsWC == nil { registerHotkeys() }
        applyAppearance()
        manager?.applyContentFont()
        manager?.updateTitle()
    }

    func applyAppearance() {
        switch settings.appTheme {
        case "light": NSApp.appearance = NSAppearance(named: .aqua)
        case "dark": NSApp.appearance = NSAppearance(named: .darkAqua)
        default: NSApp.appearance = nil
        }
    }

    // 有句庫管理或設定視窗時，在 Dock 和 ⌘Tab 裡顯示；都關掉就只留選單列圖示
    private func updateActivationPolicy() {
        let regular = manager != nil || settingsWC != nil
        NSApp.setActivationPolicy(regular ? .regular : .accessory)
    }

    // ── 快速鍵 ──
    private func unregisterHotkeys() {
        HotkeyCenter.shared.unregisterAll()
        chordTap?.stop()
        chordTap = nil
    }

    private func registerHotkeys() {
        unregisterHotkeys()
        hotkeyOk = false
        var quickOk = settings.quickAddHotkey.isEmpty
        var cp: [UInt16] = [], ck: [UInt16] = [], ci: [UInt32] = []
        var problem: (String, String)?

        if let k = Hotkey.parseChord(settings.hotkey) { cp.append(50); ck.append(k); ci.append(AppController.hotkeyId) }
        else if let spec = Hotkey.parse(settings.hotkey) {
            if HotkeyCenter.shared.register(id: AppController.hotkeyId, spec: spec) == noErr { hotkeyOk = true }
            else { problem = (L.t("快速鍵被占用"), L.f("{0} 已被其他程式使用，請在「設定」改用別的組合。", Hotkey.display(settings.hotkey))) }
        } else {
            problem = (L.t("快速鍵設定有誤"), L.t("請在「設定」重新指定叫出清單的快速鍵。"))
        }

        if !quickOk {
            if let k = Hotkey.parseChord(settings.quickAddHotkey) { cp.append(50); ck.append(k); ci.append(AppController.quickAddId); quickOk = true }
            else if let spec = Hotkey.parse(settings.quickAddHotkey) { quickOk = HotkeyCenter.shared.register(id: AppController.quickAddId, spec: spec) == noErr }
        }

        if !ck.isEmpty {
            let tap = ChordTap(logic: ChordLogic(prefixes: cp, keys: ck, ids: ci)) { [weak self] id in self?.onHotkey(id) }
            if tap.start() {
                chordTap = tap
                if ci.contains(AppController.hotkeyId) { hotkeyOk = true }
            } else {
                Permissions.requestInputMonitoring()
                if ci.contains(AppController.quickAddId) { quickOk = false }
                if ci.contains(AppController.hotkeyId) {
                    problem = (L.t("快速鍵無法啟用"),
                               L.f("{0} 需要「輸入監控」權限。請到「系統設定」→「隱私權與安全性」→「輸入監控」打開 Pocketbrief，再重新啟動程式。", Hotkey.display(settings.hotkey)))
                }
            }
        }
        if let p = problem { Toast.show(p.0, p.1, seconds: 8) }
        else if !quickOk {   // 叫出清單的快速鍵也壞掉時，已經顯示那則更重要的提示，不要蓋掉它
            Toast.show(L.t("快速新增的快速鍵無法使用"),
                       L.f("{0} 設定有誤或已被其他程式使用，請在「設定」改用別的組合。", Hotkey.display(settings.quickAddHotkey)), seconds: 8)
        }
        statusItem?.button?.toolTip = L.appName + " (" + Hotkey.display(settings.hotkey) + ")"
    }

    private func onHotkey(_ id: UInt32) {
        if settingsWC != nil { return }
        if id == AppController.quickAddId { quickAdd() } else { togglePopup() }
    }

    private func togglePopup() {
        if quickAddBusy { return }   // 快速新增正在取選取文字或開著對話框
        if popup.isVisible { popup.close(refocus: true); return }
        let fg = NSWorkspace.shared.frontmostApplication
        reloadSettingsIfChanged()
        store.ensureLoaded(settings.dataPath)   // 每次叫出都檢查檔案是否被改過（含雲端同步進來的修改）
        popup.open(target: fg)
    }

    // 快速新增：等使用者放開修飾鍵，再替他按 ⌘C 取得反白的文字
    private func quickAdd() {
        if quickAddBusy { return }
        guard Permissions.requireAccessibility() else { return }
        quickAddBusy = true
        let fg = NSWorkspace.shared.frontmostApplication
        Keys.afterModifiersReleased(maxWait: 1.5) { [weak self] released in
            guard let self = self else { return }
            if !released {
                // 一直按著修飾鍵不放時不要硬送 ⌘C（會變成別的組合鍵）
                self.quickAddBusy = false
                Toast.show(L.t("快速新增沒有執行"), L.f("請先放開 ⌘、⇧ 等按鍵，再按一次 {0}。", Hotkey.display(self.settings.quickAddHotkey)))
                return
            }
            Paster.copySelection { text in
                self.showQuickAdd(text)
                self.quickAddBusy = false
                if let f = fg, f != NSRunningApplication.current { f.activate(options: []) }
            }
        }
    }

    private func showQuickAdd(_ text: String) {
        store.ensureLoaded(settings.dataPath)
        guard let r = QuickAddWindow.run(prefill: text, existing: store.items, contentFontSize: settings.contentFontSize) else { return }
        if store.mutate(settings.dataPath, rescueText: r.text, { $0.append(r) }) {
            manager?.reloadFromStore()
            Toast.show(L.t("已新增範本"), (r.code.isEmpty ? "" : L.f("代碼 {0}：", r.code)) + r.displayTitle)
        }
    }

    // ── 視窗 ──
    func showManager() {
        if manager == nil { manager = ManagerWindowController(app: self) }
        updateActivationPolicy()
        NSApp.activate(ignoringOtherApps: true)
        manager?.present()
    }

    func managerDidClose() {
        manager = nil
        DispatchQueue.main.async { [weak self] in self?.updateActivationPolicy() }
    }

    func openSettings() {
        if let wc = settingsWC { wc.present(); return }
        reloadSettingsIfChanged()
        unregisterHotkeys()   // 錄製快速鍵時不要被自己攔截
        let origLang = settings.language
        let wc = SettingsWindowController(settings: settings, folder: Settings.dataFolder ?? "") { [weak self] result in
            self?.settingsClosed(result, origLang: origLang)
        }
        settingsWC = wc
        updateActivationPolicy()
        wc.present()
    }

    private func settingsClosed(_ result: SettingsResult?, origLang: String) {
        settingsWC = nil
        defer {
            registerHotkeys()   // 不論發生什麼事，都要把快速鍵註冊回來
            manager?.updateTitle()
            DispatchQueue.main.async { [weak self] in self?.updateActivationPolicy() }
        }
        guard let r = result else { return }

        // 登入時自動啟動（每台 Mac 各自設定）
        do {
            let svc = SMAppService.mainApp
            if r.launchAtLogin && svc.status != .enabled { try svc.register() }
            if !r.launchAtLogin && svc.status == .enabled { try svc.unregister() }
        } catch {
            Alerts.show(L.t("無法設定登入時自動啟動：") + error.localizedDescription, style: .warning)
        }

        // 句庫資料夾變更：改讀新資料夾的範本檔，設定也存到新資料夾
        if !r.folder.isEmpty && r.folder != Settings.dataFolder {
            Settings.dataFolder = r.folder
            store.reset()
            iniTime = nil
        }

        let s = r.settings
        if !saveSettingsSafely({ t in
            t.hotkey = s.hotkey; t.quickAddHotkey = s.quickAddHotkey; t.autoCommit = s.autoCommit
            t.restoreClipboard = s.restoreClipboard; t.contentFontSize = s.contentFontSize; t.style = s.style.copy()
            t.language = s.language; t.appTheme = s.appTheme
        }) {
            Alerts.show(L.t("設定檔暫時無法寫入（可能正在同步），新設定這次有效，但沒有存檔；請稍後再到「設定」按一次確定。"), style: .warning)
        }
        store.ensureLoaded(settings.dataPath)
        popup.applyStyle(settings.style)
        applyAppearance()
        manager?.applyContentFont()
        manager?.reloadFromStore()
        // 只有在這次對話框裡改了語言才重新啟動
        if s.language.caseInsensitiveCompare(origLang) != .orderedSame && L.resolve(s.language) != L.code {
            relaunch()
        }
    }

    // 先重讀最新的設定檔，再只改這次要改的欄位，以免蓋掉別台 Mac 剛改的其他設定。
    // 讀不到最新設定時不存檔（回傳 false），但這次仍照新設定運作。
    @discardableResult
    private func saveSettingsSafely(_ change: (Settings) -> Void) -> Bool {
        var target = settings.clone()
        if let p = Settings.iniPath, FileManager.default.fileExists(atPath: p) {
            guard let latest = Settings.load() else { change(settings); return false }
            target = latest
        }
        change(target)
        do {
            try target.save()
            settings = target
            if let p = Settings.iniPath { iniTime = Store.fileInfo(p).mtime }
            return true
        } catch {
            change(settings)
            return false
        }
    }

    // 句庫管理工具列的「內容字級」：立刻套用，稍後存檔
    func setContentFontSize(_ v: CGFloat) {
        settings.contentFontSize = v
        manager?.applyContentFont()
        pendingFontSize = v
        fontSaveWork?.cancel()
        let w = DispatchWorkItem { [weak self] in self?.saveFontSize() }
        fontSaveWork = w
        DispatchQueue.main.asyncAfter(deadline: .now() + 1, execute: w)
    }

    private func saveFontSize() {
        guard let v = pendingFontSize else { return }
        pendingFontSize = nil
        saveSettingsSafely { $0.contentFontSize = v }
    }

    func showAbout() {
        Alerts.show(L.fullName, info: L.f("版本 {0}", appVersion) + "\n\n" + L.copyright + "\n"
                    + L.t("本程式以 MIT 授權開源，可自由使用、修改與散布。") + "\n\nhttps://github.com/ulysses5976/Pocketbrief")
    }

    // 重新啟動：先排定一秒後開新的一份（帶 --restart，會等這一份結束），再結束自己
    private func relaunch() {
        let reopen = manager != nil
        if let m = manager, !m.confirmLeaveEditor() { return }
        let p = Process()
        p.executableURL = URL(fileURLWithPath: "/bin/sh")
        p.arguments = ["-c", "sleep 1; /usr/bin/open -n \"$0\" --args --restart" + (reopen ? " --manage" : ""), Bundle.main.bundlePath]
        do { try p.run() } catch { Alerts.show(error.localizedDescription); return }
        NSApp.terminate(nil)
    }

    // ── 選單列圖示與選單 ──
    private func buildStatusItem() {
        let item = NSStatusBar.system.statusItem(withLength: NSStatusItem.squareLength)
        item.button?.image = Icons.menuBar()
        item.button?.toolTip = L.appName + " (" + Hotkey.display(settings.hotkey) + ")"
        let menu = NSMenu()
        let m = NSMenuItem(title: L.t("句庫管理…"), action: #selector(menuManager), keyEquivalent: "")
        m.attributedTitle = NSAttributedString(string: m.title, attributes: [.font: NSFont.boldSystemFont(ofSize: NSFont.systemFontSize)])
        m.target = self
        menu.addItem(m)
        for (title, sel) in [(L.t("設定…"), #selector(menuSettings)), (L.t("開啟句庫資料夾"), #selector(menuFolder)), (L.t("關於…"), #selector(menuAbout))] {
            let it = NSMenuItem(title: title, action: sel, keyEquivalent: "")
            it.target = self
            menu.addItem(it)
        }
        menu.addItem(.separator())
        let quit = NSMenuItem(title: L.t("結束"), action: #selector(menuQuit), keyEquivalent: "")
        quit.target = self
        menu.addItem(quit)
        item.menu = menu
        statusItem = item
    }

    // 應用程式選單（句庫管理、設定視窗開著時才看得到）：也讓文字欄位的 ⌘C／⌘V 等能正常運作
    private func buildMainMenu() {
        let main = NSMenu()
        func sub(_ title: String) -> NSMenu {
            let item = NSMenuItem()
            let m = NSMenu(title: title)
            item.submenu = m
            main.addItem(item)
            return m
        }
        let appMenu = sub(L.appName)
        appMenu.addItem(withTitle: L.f("關於 {0}", L.appName), action: #selector(menuAbout), keyEquivalent: "").target = self
        appMenu.addItem(.separator())
        appMenu.addItem(withTitle: L.t("設定…"), action: #selector(menuSettings), keyEquivalent: ",").target = self
        appMenu.addItem(.separator())
        appMenu.addItem(withTitle: L.f("隱藏 {0}", L.appName), action: #selector(NSApplication.hide(_:)), keyEquivalent: "h")
        appMenu.addItem(withTitle: L.f("結束 {0}", L.appName), action: #selector(menuQuit), keyEquivalent: "q").target = self

        let file = sub(L.t("檔案"))
        file.addItem(withTitle: L.t("新增範本"), action: #selector(ManagerWindowController.newTemplate(_:)), keyEquivalent: "n")
        file.addItem(withTitle: L.t("儲存"), action: #selector(ManagerWindowController.saveTemplate(_:)), keyEquivalent: "s")
        file.addItem(.separator())
        file.addItem(withTitle: L.t("匯入 CSV…"), action: #selector(ManagerWindowController.importCSV(_:)), keyEquivalent: "")
        file.addItem(withTitle: L.t("匯出 CSV…"), action: #selector(ManagerWindowController.exportCSV(_:)), keyEquivalent: "")
        file.addItem(.separator())
        file.addItem(withTitle: L.t("關閉視窗"), action: #selector(NSWindow.performClose(_:)), keyEquivalent: "w")

        let edit = sub(L.t("編輯"))
        edit.addItem(withTitle: L.t("還原"), action: Selector(("undo:")), keyEquivalent: "z")
        edit.addItem(withTitle: L.t("重做"), action: Selector(("redo:")), keyEquivalent: "z").keyEquivalentModifierMask = [.command, .shift]
        edit.addItem(.separator())
        edit.addItem(withTitle: L.t("剪下"), action: #selector(NSText.cut(_:)), keyEquivalent: "x")
        edit.addItem(withTitle: L.t("拷貝"), action: #selector(NSText.copy(_:)), keyEquivalent: "c")
        edit.addItem(withTitle: L.t("貼上"), action: #selector(NSText.paste(_:)), keyEquivalent: "v")
        edit.addItem(withTitle: L.t("全選"), action: #selector(NSText.selectAll(_:)), keyEquivalent: "a")
        edit.addItem(.separator())
        edit.addItem(withTitle: L.t("搜尋"), action: #selector(ManagerWindowController.focusSearch(_:)), keyEquivalent: "f")

        let win = sub(L.t("視窗"))
        win.addItem(withTitle: L.t("縮到最小"), action: #selector(NSWindow.performMiniaturize(_:)), keyEquivalent: "m")
        NSApp.mainMenu = main
        NSApp.windowsMenu = win
    }

    @objc private func menuManager() { showManager() }
    @objc private func menuSettings() { openSettings() }
    @objc private func menuAbout() { showAbout() }
    @objc private func menuQuit() { NSApp.terminate(nil) }
    @objc private func menuFolder() {
        if let f = Settings.dataFolder { NSWorkspace.shared.open(URL(fileURLWithPath: f)) }
    }

    // ── 結束 ──
    func applicationShouldTerminate(_ sender: NSApplication) -> NSApplication.TerminateReply {
        if let m = manager, !m.confirmLeaveEditor() { return .terminateCancel }   // 有未儲存的修改時會先問
        return .terminateNow
    }

    func applicationWillTerminate(_ notification: Notification) {
        Paster.flushPending()   // 結束前把輸出時暫借的剪貼簿還原
        fontSaveWork?.cancel()
        saveFontSize()
        unregisterHotkeys()
    }

    // 程式已在執行時再從 Finder 打開：開啟句庫管理
    func applicationShouldHandleReopen(_ sender: NSApplication, hasVisibleWindows flag: Bool) -> Bool {
        showManager()
        return false
    }
}

// ───────────── 第一次使用：選擇句庫資料夾 ─────────────
enum DataFolderChooser {
    static let folderName = "口袋句庫 Pocketbrief"

    // 在 Google 雲端硬碟裡找 Windows 版已經在用的句庫資料夾
    static func detectGoogleDrive() -> String? {
        let cloud = (NSHomeDirectory() as NSString).appendingPathComponent("Library/CloudStorage")
        guard let entries = try? FileManager.default.contentsOfDirectory(atPath: cloud) else { return nil }
        for e in entries where e.hasPrefix("GoogleDrive") {
            for sub in ["我的雲端硬碟", "My Drive", "マイドライブ", "我的云端硬盘"] {
                let p = [cloud, e, sub, folderName].joined(separator: "/")
                if FileManager.default.fileExists(atPath: (p as NSString).appendingPathComponent("範本.csv")) { return p }
            }
        }
        return nil
    }

    static var defaultFolder: String {
        let docs = FileManager.default.urls(for: .documentDirectory, in: .userDomainMask).first?.path ?? NSHomeDirectory()
        return (docs as NSString).appendingPathComponent(folderName)
    }

    static func choose() -> String? {
        let found = detectGoogleDrive()
        var info = L.t("口袋句庫把範本（範本.csv）存在一個資料夾裡。把這個資料夾放在雲端硬碟，就能和其他電腦（包括 Windows 版）共用同一個句庫。")
        var buttons: [String] = []
        if let f = found {
            info += "\n\n" + L.f("在 Google 雲端硬碟找到現有的句庫：\n{0}", f)
            buttons.append(L.t("使用這個資料夾"))
        }
        buttons += [L.t("選擇資料夾…"), L.t("使用預設位置（文件）"), L.t("結束")]
        while true {
            var r = Alerts.show(L.t("選擇句庫資料夾"), info: info, buttons: buttons)
            if let f = found { if r == 0 { return f }; r -= 1 }
            switch r {
            case 0:
                let p = NSOpenPanel()
                p.canChooseDirectories = true
                p.canChooseFiles = false
                p.canCreateDirectories = true
                p.prompt = L.t("選擇")
                p.message = L.t("選擇句庫資料夾（範本.csv 所在的資料夾）")
                if p.runModal() == .OK, let url = p.url { return url.path }
            case 1:
                do {
                    try FileManager.default.createDirectory(atPath: defaultFolder, withIntermediateDirectories: true)
                    return defaultFolder
                } catch {
                    Alerts.show(error.localizedDescription, style: .warning)
                }
            default:
                return nil
            }
        }
    }
}
