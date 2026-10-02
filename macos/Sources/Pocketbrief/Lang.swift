// 口袋句庫 Pocketbrief：介面語言（繁體中文、English、日本語、简体中文）
// Copyright (c) 2026 無名小律師（楊朝淵律師）. Licensed under the MIT License; see the LICENSE file.
//
// 程式中的介面文字一律以繁體中文撰寫，並以 L.t("…")／L.f("…", 參數) 取用：
//   ‧English、日本語：查 LangTable 的逐句翻譯；查不到時顯示繁體中文原文。
//   ‧简体中文：先以 cnTerms 換成大陸慣用詞，再用系統內建的繁簡轉換。
import Foundation

enum L {
    static let codes = ["zh-TW", "en", "ja", "zh-CN"]
    static let nativeNames = ["繁體中文", "English", "日本語", "简体中文"]

    private(set) static var code = "zh-TW"
    private static var table: [String: String] = [:]
    private static var cnCache: [String: String] = [:]

    // setting：設定檔裡的 language 值（auto 或語言代碼）
    static func initialize(_ setting: String) {
        code = resolve(setting)
        table = [:]
        cnCache = [:]
        if code == "en" || code == "ja" {
            let col = code == "en" ? 1 : 2
            let e = LangTable.entries
            var i = 0
            while i + 2 < e.count {
                if !e[i + col].isEmpty { table[e[i]] = e[i + col] }
                i += 3
            }
        }
    }

    static func resolve(_ setting: String) -> String {
        if let c = codes.first(where: { $0.caseInsensitiveCompare(setting) == .orderedSame }) { return c }
        // auto：依 macOS 的偏好語言
        let ui = Locale.preferredLanguages.first ?? "en"
        if ui.hasPrefix("zh") {
            let simplified = ui.contains("Hans") || ui.hasSuffix("CN") || ui.hasSuffix("SG")
            return simplified ? "zh-CN" : "zh-TW"
        }
        if ui.hasPrefix("ja") { return "ja" }
        return "en"
    }

    static func t(_ zh: String) -> String {
        switch code {
        case "zh-TW": return zh
        case "zh-CN": return toSimplified(zh)
        default: return table[zh] ?? zh
        }
    }

    // 佔位符寫法與 Windows 版相同：{0}、{1}…
    static func f(_ zh: String, _ args: Any...) -> String {
        var s = t(zh)
        for (i, a) in args.enumerated() { s = s.replacingOccurrences(of: "{\(i)}", with: "\(a)") }
        return s
    }

    static var appName: String { code == "zh-TW" ? "口袋句庫" : code == "zh-CN" ? "口袋句库" : "Pocketbrief" }

    static var fullName: String {
        switch code {
        case "zh-TW": return "口袋句庫 Pocketbrief"
        case "zh-CN": return "口袋句库 Pocketbrief"
        case "ja": return "Pocketbrief（口袋句庫）"
        default: return "Pocketbrief"
        }
    }

    static var copyright: String {
        switch code {
        case "en": return "Copyright © 2026 無名小律師 (Attorney 楊朝淵) · MIT License"
        case "ja": return "Copyright © 2026 無名小律師（楊朝淵弁護士）· MIT ライセンス"
        case "zh-CN": return "著作权所有 © 2026 无名小律师（杨朝渊律师）· MIT 许可"
        default: return "著作權所有 © 2026 無名小律師（楊朝淵律師）· MIT 授權"
        }
    }

    // 選單列圖示上的字
    static var iconGlyph: String { code == "en" ? "P" : "句" }

    // ── 繁→簡 ──
    // 台灣用語 → 大陸用語（仍以繁體寫，之後一起轉成簡體）；長的詞放前面
    private static let cnTerms = [
        "雲端硬碟", "雲端硬盤", "選單列", "菜單欄", "輔助使用", "輔助功能", "圖示", "圖標", "存檔", "保存",
        "範本檔", "模板文件", "本檔", "本文件",
        "標題列底色", "標題欄底色", "標題列文字", "標題欄文字", "標題列", "標題行", "選取列", "選中行", "資料夾", "文件夾",
        "剪貼簿", "剪貼板", "快速鍵", "快捷鍵", "檔名", "文件名", "設定檔", "配置文件", "檔案", "文件", "視窗", "窗口",
        "預設", "默認", "設定", "設置", "游標", "光標", "字型", "字體", "字級", "字號", "匯入", "導入", "匯出", "導出",
        "程式", "程序", "選單", "菜單", "螢幕", "屏幕", "網路", "網絡", "支援", "支持", "儲存", "保存",
        "清單", "列表", "硬碟", "硬盤", "滑鼠", "鼠標", "軟體", "軟件", "範本", "模板", "底色", "背景色",
        "訊息", "信息", "登入", "登錄", "搜尋", "搜索", "貼上", "粘貼", "全形", "全角", "半形", "半角",
        "使用者", "用戶", "介面", "界面", "反白", "選中", "列印", "打印", "資訊", "信息",
    ]

    static func toSimplified(_ s: String) -> String {
        if let r = cnCache[s] { return r }
        var t = s
        var i = 0
        while i + 1 < cnTerms.count {
            t = t.replacingOccurrences(of: cnTerms[i], with: cnTerms[i + 1])
            i += 2
        }
        let r = t.applyingTransform(StringTransform(rawValue: "Hant-Hans"), reverse: false) ?? t
        cnCache[s] = r
        return r
    }
}
