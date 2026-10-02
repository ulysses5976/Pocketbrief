// 口袋句庫 Pocketbrief：設定（句庫資料夾裡的「設定-Mac.ini」）
// Copyright (c) 2026 無名小律師（楊朝淵律師）. Licensed under the MIT License; see the LICENSE file.
//
// 句庫資料夾的位置記在這台 Mac 的使用者偏好設定裡；其餘設定寫在資料夾裡的「設定-Mac.ini」，
// 可以跟著雲端硬碟同步到其他 Mac。Mac 與 Windows 的快速鍵寫法不同（Mac 有 ⌘），
// 所以不與 Windows 版的「設定.ini」共用，以免互相覆蓋。
import AppKit

final class Settings {
    var hotkey = "Ctrl+`"
    var quickAddHotkey = "Ctrl+Shift+0"   // 空字串＝停用
    var dataFile = "範本.csv"
    var restoreClipboard = true
    var autoCommit = true
    var language = "auto"          // auto／zh-TW／en／ja／zh-CN
    var appTheme = "auto"          // auto（跟隨系統）／light／dark
    var contentFontSize: CGFloat = 14   // 句庫管理裡範本文字（清單與編輯區）的字體大小
    var style = PopupStyle()

    static let iniName = "設定-Mac.ini"
    private static let folderKey = "DataFolder"

    func clone() -> Settings {
        let s = Settings()
        s.hotkey = hotkey; s.quickAddHotkey = quickAddHotkey; s.dataFile = dataFile
        s.restoreClipboard = restoreClipboard; s.autoCommit = autoCommit; s.language = language
        s.appTheme = appTheme; s.contentFontSize = contentFontSize; s.style = style.copy()
        return s
    }

    // 句庫資料夾（每台 Mac 各自記住）
    static var dataFolder: String? {
        get { UserDefaults.standard.string(forKey: folderKey) }
        set { UserDefaults.standard.set(newValue, forKey: folderKey) }
    }

    static var iniPath: String? { dataFolder.map { ($0 as NSString).appendingPathComponent(iniName) } }

    var dataPath: String {
        if dataFile.hasPrefix("/") { return dataFile }
        return ((Settings.dataFolder ?? NSHomeDirectory()) as NSString).appendingPathComponent(dataFile)
    }

    // 設定檔不存在：回傳預設值（不自動寫檔，以免雲端同步換檔的空窗把大家的設定蓋掉）。
    // 設定檔存在但讀不到或內容殘缺：回傳 nil，呼叫端應沿用目前的設定，也不可存檔。
    static func load() -> Settings? {
        let s = Settings()
        guard let path = iniPath, FileManager.default.fileExists(atPath: path) else { return s }
        guard let text = try? TextFile.read(path), text.range(of: "hotkey=", options: .caseInsensitive) != nil else { return nil }
        let st = s.style
        for raw in Template.normalizeNewlines(text).components(separatedBy: "\n") {
            let line = raw.trimmingCharacters(in: .whitespaces)
            if line.isEmpty || line.hasPrefix(";") || line.hasPrefix("#") { continue }
            guard let eq = line.firstIndex(of: "="), eq != line.startIndex else { continue }
            let k = line[..<eq].trimmingCharacters(in: .whitespaces).lowercased()
            let v = line[line.index(after: eq)...].trimmingCharacters(in: .whitespaces)
            let num = Double(v)
            switch k {
            case "hotkey": if !v.isEmpty { s.hotkey = v }
            case "quickadd_hotkey": s.quickAddHotkey = v
            case "datafile": if !v.isEmpty { s.dataFile = v }
            case "restore_clipboard": s.restoreClipboard = v != "0"
            case "auto_commit": s.autoCommit = v != "0"
            case "language": if !v.isEmpty { s.language = v }
            case "app_theme": if !v.isEmpty { s.appTheme = v }
            case "content_font_size": if let n = num, n.isFinite { s.contentFontSize = CGFloat(max(9, min(28, n))) }
            case "popup_font": if !v.isEmpty { st.fontName = v }
            case "popup_font_size": if let n = num, n.isFinite { st.fontSize = CGFloat(max(9, min(40, n))) }
            case "popup_back": st.back = v.isEmpty ? nil : (NSColor(hex: v) ?? st.back)
            case "popup_fore": st.fore = v.isEmpty ? nil : (NSColor(hex: v) ?? st.fore)
            case "popup_code_fore": if let c = NSColor(hex: v) { st.codeFore = c }
            case "popup_sel_back": if let c = NSColor(hex: v) { st.selBack = c }
            case "popup_sel_fore": if let c = NSColor(hex: v) { st.selFore = c }
            case "popup_header_back": if let c = NSColor(hex: v) { st.headerBack = c }
            case "popup_header_fore": if let c = NSColor(hex: v) { st.headerFore = c }
            case "popup_width": if let n = Int(v) { st.width = max(360, min(1600, n)) }
            case "popup_rows": if let n = Int(v) { st.rows = max(3, min(30, n)) }
            default: break
            }
        }
        return s
    }

    func save() throws {
        guard let path = Settings.iniPath else { throw CocoaError(.fileNoSuchFile) }
        let st = style
        func f(_ x: CGFloat) -> String { String(format: "%g", Double(x)) }
        let lines = [
            "; " + L.t("口袋句庫 Pocketbrief Mac 版設定檔（請在選單列圖示 →「設定…」修改，不必手動編輯本檔）"),
            "hotkey=" + hotkey,
            "quickadd_hotkey=" + quickAddHotkey,
            "datafile=" + dataFile,
            "auto_commit=" + (autoCommit ? "1" : "0"),
            "restore_clipboard=" + (restoreClipboard ? "1" : "0"),
            "language=" + language,
            "app_theme=" + appTheme,
            "content_font_size=" + f(contentFontSize),
            "",
            "; " + L.t("範本清單外觀（顏色空白＝跟隨系統配色）"),
            "popup_font=" + st.fontName,
            "popup_font_size=" + f(st.fontSize),
            "popup_back=" + (st.back?.hexString ?? ""),
            "popup_fore=" + (st.fore?.hexString ?? ""),
            "popup_code_fore=" + st.codeFore.hexString,
            "popup_sel_back=" + st.selBack.hexString,
            "popup_sel_fore=" + st.selFore.hexString,
            "popup_header_back=" + st.headerBack.hexString,
            "popup_header_fore=" + st.headerFore.hexString,
            "popup_width=" + String(st.width),
            "popup_rows=" + String(st.rows),
        ]
        try TextFile.write(path, lines.joined(separator: "\r\n") + "\r\n")
    }
}
