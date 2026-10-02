// 口袋句庫 Pocketbrief：範本資料、CSV 讀寫、範本檔的文字編碼與安全寫檔
// Copyright (c) 2026 無名小律師（楊朝淵律師）. Licensed under the MIT License; see the LICENSE file.
//
// 範本檔格式與 Windows 版完全相同（UTF-8 含 BOM、CRLF 換行、第一列是欄位名稱），
// 兩邊可以透過雲端硬碟共用同一份「範本.csv」。
import Foundation

final class Template {
    var title: String
    var code: String
    var text: String   // 換行一律存成 \n

    init(title: String = "", code: String = "", text: String = "") {
        self.title = title
        self.code = code
        self.text = text
    }

    var key: String { code.lowercased() }

    var preview: String {
        let p = text.replacingOccurrences(of: "\n", with: " ⏎ ")
        return p.count > 80 ? String(p.prefix(80)) + "…" : p
    }

    var displayTitle: String {
        if !title.isEmpty { return title }
        let p = text.replacingOccurrences(of: "\n", with: " ")
        return p.count > 12 ? String(p.prefix(12)) + "…" : p
    }

    func sameAs(_ o: Template?) -> Bool {
        guard let o = o else { return false }
        return title == o.title && code == o.code && text == o.text
    }

    static func normalizeNewlines(_ s: String) -> String {
        s.replacingOccurrences(of: "\r\n", with: "\n").replacingOccurrences(of: "\r", with: "\n")
    }

    static func trimNewlines(_ s: String) -> String {
        s.trimmingCharacters(in: CharacterSet(charactersIn: "\n"))
    }

    static func isBlank(_ s: String) -> Bool {
        s.trimmingCharacters(in: .whitespacesAndNewlines).isEmpty
    }

    // 所有匯入來源一律用同一套規則整理，確保「記憶體中的內容」和「存檔後再讀回的內容」一致。
    // 回傳因格式不合而被清掉代碼的筆數；內容空白的會被移除。
    @discardableResult
    static func normalizeAll(_ list: inout [Template]) -> Int {
        var cleared = 0
        var i = list.count - 1
        while i >= 0 {
            let t = list[i]
            t.title = t.title.trimmingCharacters(in: .whitespacesAndNewlines)
            t.code = t.code.trimmingCharacters(in: .whitespacesAndNewlines)
            t.text = trimNewlines(normalizeNewlines(t.text))
            if !t.code.isEmpty && !Csv.isValidCode(t.code) { t.code = ""; cleared += 1 }
            if isBlank(t.text) { list.remove(at: i) }
            i -= 1
        }
        return cleared
    }
}

enum Csv {
    // 欄位名稱與 Windows 版一致（名稱／代碼／內容），附英文方便其他語言的使用者用 Excel 開啟
    static let header = ["名稱 Name", "代碼 Code", "內容 Content"]

    // 逐一處理 Unicode 純量（不能用 Character：Swift 會把 \r\n 當成一個字元）
    static func parse(_ s: String) -> [[String]] {
        let chars = Array(s.unicodeScalars)
        let delim = detectDelimiter(chars)
        var rows: [[String]] = []
        var row: [String] = []
        var field = String.UnicodeScalarView()
        var inQ = false, any = false
        var i = 0
        while i < chars.count {
            let c = chars[i]
            if inQ {
                if c == "\"" {
                    if i + 1 < chars.count && chars[i + 1] == "\"" { field.append("\""); i += 1 }
                    else { inQ = false }
                } else {
                    field.append(c)
                }
                i += 1
                continue
            }
            if c == "\"" && field.isEmpty { inQ = true; any = true }
            else if c == delim { row.append(String(field)); field = String.UnicodeScalarView(); any = true }
            else if c == "\r" { }
            else if c == "\n" { endRow(&rows, &row, &field, any); row = []; any = false }
            else { field.append(c); any = true }
            i += 1
        }
        endRow(&rows, &row, &field, any)
        return rows
    }

    private static func endRow(_ rows: inout [[String]], _ row: inout [String], _ field: inout String.UnicodeScalarView, _ any: Bool) {
        if !any && row.isEmpty { return }
        row.append(String(field))
        field = String.UnicodeScalarView()
        if row.contains(where: { !Template.isBlank($0) }) { rows.append(row) }
    }

    private static func detectDelimiter(_ chars: [Unicode.Scalar]) -> Unicode.Scalar {
        var commas = 0, tabs = 0
        for c in chars {
            if c == "\n" { break }
            if c == "," { commas += 1 } else if c == "\t" { tabs += 1 }
        }
        return tabs > commas ? "\t" : ","
    }

    private static func esc(_ f: String) -> String {
        // 以 Unicode 純量檢查：Swift 的 Character 會把 \r\n 視為一個字元，contains("\n") 會漏掉
        let needs = f.unicodeScalars.contains { $0 == "," || $0 == "\"" || $0 == "\n" || $0 == "\r" }
            || f != f.trimmingCharacters(in: .whitespacesAndNewlines)
        return needs ? "\"" + f.replacingOccurrences(of: "\"", with: "\"\"") + "\"" : f
    }

    static func write(_ items: [Template]) -> String {
        var s = header.joined(separator: ",") + "\r\n"
        for t in items {
            s += esc(t.title) + "," + esc(t.code) + "," + esc(t.text.replacingOccurrences(of: "\n", with: "\r\n")) + "\r\n"
        }
        return s
    }

    // 把 CSV 轉成範本；有標題列就照欄名對應，沒有就依內容猜哪一欄是代碼、標題、內容
    static func toTemplates(_ rows: [[String]]) -> [Template] {
        var result: [Template] = []
        if rows.isEmpty { return result }
        var iTitle = -1, iCode = -1, iText = -1
        let h = rows[0]
        for (i, raw) in h.enumerated() {
            let n = raw.trimmingCharacters(in: .whitespacesAndNewlines)
                .trimmingCharacters(in: CharacterSet(charactersIn: "\u{FEFF}")).lowercased()
            if iTitle < 0 && (n.contains("標題") || n.contains("title") || n.contains("名稱")) { iTitle = i }
            else if iCode < 0 && (n.contains("代碼") || n.contains("code") || n.contains("key") || n.contains("縮寫") || n.contains("輸入碼") || n.contains("代號")) { iCode = i }
            else if iText < 0 && (n.contains("內容") || n.contains("text") || n.contains("content") || n.contains("範本")) { iText = i }
        }
        var start: Int
        if iText >= 0 || iCode >= 0 { start = 1 }
        else {
            start = 0
            let ncol = rows.map { $0.count }.max() ?? 0
            if ncol == 1 { iText = 0 }
            else {
                let lim = min(ncol, 3)
                var bestScore = -1.0
                for c in 0..<lim {
                    let sc = codeLikeScore(rows, c)
                    if sc > bestScore { bestScore = sc; iCode = c }
                }
                if bestScore < 0.6 { iCode = -1 }
                let rest = (0..<lim).filter { $0 != iCode }
                if rest.count == 1 { iText = rest[0] }
                else if rest.count >= 2 {
                    let firstLonger = avgLen(rows, rest[0]) > avgLen(rows, rest[1])
                    iText = firstLonger ? rest[0] : rest[1]
                    iTitle = firstLonger ? rest[1] : rest[0]
                }
            }
        }
        if iText < 0 { return result }
        var r = start
        while r < rows.count {
            let row = rows[r]
            let t = Template()
            t.text = Template.trimNewlines(Template.normalizeNewlines(get(row, iText)))
            t.code = get(row, iCode).trimmingCharacters(in: .whitespacesAndNewlines)
            t.title = get(row, iTitle).trimmingCharacters(in: .whitespacesAndNewlines)
            if !Template.isBlank(t.text) { result.append(t) }
            r += 1
        }
        return result
    }

    private static func get(_ row: [String], _ i: Int) -> String { i >= 0 && i < row.count ? row[i] : "" }

    private static func codeLikeScore(_ rows: [[String]], _ c: Int) -> Double {
        var ok = 0, n = 0
        for r in rows {
            let v = get(r, c).trimmingCharacters(in: .whitespacesAndNewlines)
            if v.isEmpty { continue }
            n += 1
            if isValidCode(v) { ok += 1 }
        }
        return n == 0 ? 0 : Double(ok) / Double(n)
    }

    private static func avgLen(_ rows: [[String]], _ c: Int) -> Double {
        if rows.isEmpty { return 0 }
        return Double(rows.reduce(0) { $0 + get($1, c).count }) / Double(rows.count)
    }

    static func isValidCode(_ v: String) -> Bool {
        let s = v.unicodeScalars
        if s.isEmpty || s.count > 20 { return false }
        for ch in s where ch.value <= 32 || ch.value > 126 || ch == "`" { return false }
        return true
    }
}

enum TextFile {
    // 自動判斷編碼：BOM → UTF-8 → Big5（舊版記事本或部分程式匯出的 CSV）
    static func read(_ path: String) throws -> String {
        var lastError: Error?
        for _ in 0..<6 {
            do { return decode(try Data(contentsOf: URL(fileURLWithPath: path))) }
            catch { lastError = error; Thread.sleep(forTimeInterval: 0.15) }   // 雲端硬碟同步中可能暫時讀不到
        }
        throw lastError ?? CocoaError(.fileReadUnknown)
    }

    static func decode(_ data: Data) -> String {
        let b = [UInt8](data)
        if b.count >= 3 && b[0] == 0xEF && b[1] == 0xBB && b[2] == 0xBF { return String(decoding: b[3...], as: UTF8.self) }
        if b.count >= 2 && b[0] == 0xFF && b[1] == 0xFE { return String(data: Data(b[2...]), encoding: .utf16LittleEndian) ?? "" }
        if b.count >= 2 && b[0] == 0xFE && b[1] == 0xFF { return String(data: Data(b[2...]), encoding: .utf16BigEndian) ?? "" }
        if let s = String(data: data, encoding: .utf8) { return s }
        let big5 = String.Encoding(rawValue: CFStringConvertEncodingToNSStringEncoding(CFStringEncoding(CFStringEncodings.big5_HKSCS_1999.rawValue)))
        if let s = String(data: data, encoding: big5) { return s }
        return String(decoding: b, as: UTF8.self)
    }

    // 先寫到暫存檔，完整寫好後才一次替換正式檔：當機、斷電或雲端硬碟同時讀取時，
    // 都不會出現「寫到一半」的檔案。編碼與 Windows 版相同（UTF-8 含 BOM）。
    static func write(_ path: String, _ content: String) throws {
        let url = URL(fileURLWithPath: path)
        let tmp = URL(fileURLWithPath: path + ".saving")
        var data = Data([0xEF, 0xBB, 0xBF])
        data.append(Data(content.utf8))
        var lastError: Error?
        for _ in 0..<6 {
            do {
                try data.write(to: tmp)
                // rename() 會一次換掉正式檔（同一個資料夾內一定是整檔替換）；
                // 不用 replaceItemAt：雲端硬碟這類虛擬磁碟不一定支援
                if rename(tmp.path, url.path) != 0 {
                    throw NSError(domain: NSPOSIXErrorDomain, code: Int(errno))
                }
                return
            } catch {
                lastError = error
                Thread.sleep(forTimeInterval: 0.15)
            }
        }
        try? FileManager.default.removeItem(at: tmp)
        throw lastError ?? CocoaError(.fileWriteUnknown)
    }
}
