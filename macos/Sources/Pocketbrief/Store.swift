// 口袋句庫 Pocketbrief：範本檔的讀取、驗證、存檔與每日備份
// Copyright (c) 2026 無名小律師（楊朝淵律師）. Licensed under the MIT License; see the LICENSE file.
//
// 與 Windows 版相同的資料安全規則：
//   ‧讀到空檔、不完整（不以換行結尾）、缺少欄位名稱列的檔案，一律拒收並沿用上次讀到的內容
//   ‧讀過之後檔案不見了（多半是雲端硬碟換檔的空窗），絕不自動建空檔
//   ‧存檔先寫暫存檔再一次替換；每天第一次修改前備份到「備份」資料夾，保留 14 份
import AppKit

final class Store {
    private(set) var items: [Template] = []
    private(set) var version = 0   // items 每次被換新（重讀或存檔）就加一
    private(set) var error: String?
    private var loadedPath: String?
    private var loadedTime: Date?
    private var loadedLen: Int64 = -1
    private var everLoaded = false   // 這次執行是否成功讀過範本檔
    // 只有使用者剛選好句庫資料夾時，才可以在裡面建立新的範本檔。
    // 平常找不到範本檔，多半是雲端硬碟還在同步（例如剛開機）——這時建空檔，會把雲端上真正的句庫蓋掉。
    var allowCreate = false

    // 換了句庫資料夾：當作第一次讀取
    func reset() {
        items = []
        version += 1
        error = nil
        loadedPath = nil
        loadedTime = nil
        loadedLen = -1
        everLoaded = false
    }

    static func fileInfo(_ path: String) -> (exists: Bool, mtime: Date?, size: Int64) {
        guard let a = try? FileManager.default.attributesOfItem(atPath: path) else { return (false, nil, -1) }
        return (true, a[.modificationDate] as? Date, (a[.size] as? NSNumber)?.int64Value ?? -1)
    }

    // 回傳 true 表示畫面需要重新整理（內容重新讀過，或錯誤狀態改變）。
    // 讀取有問題時一律「沿用上次讀到的內容＋設定 error」，絕不自動寫檔，以免把雲端上的資料蓋掉。
    @discardableResult
    func ensureLoaded(_ path: String) -> Bool {
        let prevError = error
        do {
            if !FileManager.default.fileExists(atPath: path) {
                // 讀過卻不見了，多半是雲端硬碟換檔的空窗；資料夾不存在則可能是路徑設錯。兩種都不能建空檔。
                let dir = (path as NSString).deletingLastPathComponent
                var isDir: ObjCBool = false
                if everLoaded || !allowCreate || dir.isEmpty || !FileManager.default.fileExists(atPath: dir, isDirectory: &isDir) || !isDir.boolValue {
                    error = L.t("找不到範本檔（可能正在同步），暫時沿用上次讀到的內容。") + "\n" + path
                    return error != prevError
                }
                try TextFile.write(path, Csv.write([]))
            }
            allowCreate = false
            for _ in 0..<3 {
                let before = Store.fileInfo(path)
                if path == loadedPath && before.mtime == loadedTime && before.size == loadedLen && error == nil { return false }
                let text = try TextFile.read(path)
                let after = Store.fileInfo(path)
                if after.mtime != before.mtime || after.size != before.size {   // 讀的時候檔案被換掉了
                    Thread.sleep(forTimeInterval: 0.2)
                    continue
                }
                if let problem = Store.validate(text) {
                    error = problem
                    return error != prevError
                }
                items = Csv.toTemplates(Csv.parse(text))
                version += 1
                loadedPath = path; loadedTime = before.mtime; loadedLen = before.size
                everLoaded = true
                error = nil
                return true
            }
            error = L.t("範本檔一直在變動（可能正在同步），暫時沿用上次讀到的內容。")
            return error != prevError
        } catch {
            self.error = L.t("讀不到範本檔：") + error.localizedDescription
            return self.error != prevError
        }
    }

    // 本程式寫出的範本檔一定有標題列、以換行結尾；不符合就可能是空檔或只寫了一半
    private static func validate(_ text: String) -> String? {
        if text.isEmpty { return L.t("範本檔是空的（可能正在同步），暫時沿用上次讀到的內容。") }
        if text.unicodeScalars.last != "\n" { return L.t("範本檔不完整（可能正在同步），暫時沿用上次讀到的內容。") }
        let firstLine: Substring
        if let r = text.range(of: "\n", options: .literal) { firstLine = text[..<r.lowerBound] } else { firstLine = Substring(text) }
        if firstLine.range(of: "代碼", options: .literal) == nil && firstLine.range(of: "code", options: .caseInsensitive) == nil {
            return L.t("範本檔格式不正確（缺少欄位名稱那一列），暫時沿用上次讀到的內容。")
        }
        return nil
    }

    private func fileChangedSinceLoad(_ path: String) -> Bool {
        let fi = Store.fileInfo(path)
        return !fi.exists || fi.mtime != loadedTime || fi.size != loadedLen
    }

    // 每次增刪改都「先讀最新檔 → 套用變更 → 立刻存檔」；存檔前若發現檔案又被別台電腦改了，就重讀後再套用一次。
    // rescueText：存檔失敗時先放進剪貼簿，避免使用者剛打的內容消失。
    @discardableResult
    func mutate(_ path: String, rescueText: String? = nil, _ change: (inout [Template]) -> Void) -> Bool {
        for _ in 0..<3 {
            ensureLoaded(path)
            if let e = error {
                Store.fail(e + "\n\n" + L.t("為避免覆蓋掉資料，這次的修改沒有存檔。"), rescueText)
                return false
            }
            var list = items
            change(&list)
            if fileChangedSinceLoad(path) { Thread.sleep(forTimeInterval: 0.2); continue }
            do {
                Store.dailyBackup(path)
                try TextFile.write(path, Csv.write(list))
                items = list
                version += 1
                let fi = Store.fileInfo(path)
                loadedPath = path; loadedTime = fi.mtime; loadedLen = fi.size
                return true
            } catch {
                Store.fail(L.t("存檔失敗：") + error.localizedDescription, rescueText)
                return false
            }
        }
        Store.fail(L.t("範本檔一直在變動（可能正在同步），這次的修改沒有存檔，請稍後再試。"), rescueText)
        return false
    }

    private static func fail(_ message: String, _ rescueText: String?) {
        var msg = message
        if let r = rescueText, !r.isEmpty {
            let pb = NSPasteboard.general
            pb.clearContents()
            if pb.setString(r, forType: .string) {
                msg += "\n\n" + L.t("你剛輸入的內容已先複製到剪貼簿，可以稍後再貼回去。")
            }
        }
        Alerts.show(msg, style: .warning)
    }

    // 每天第一次修改前，把當時的範本檔複製一份到「備份」資料夾，保留最近 14 份
    private static func dailyBackup(_ path: String) {
        let fm = FileManager.default
        let fi = fileInfo(path)
        if !fi.exists || fi.size <= 0 { return }
        let dir = ((path as NSString).deletingLastPathComponent as NSString).appendingPathComponent("備份")
        let base = ((path as NSString).lastPathComponent as NSString).deletingPathExtension
        let ext = (path as NSString).pathExtension
        let df = DateFormatter()
        df.locale = Locale(identifier: "en_US_POSIX")
        df.dateFormat = "yyyyMMdd"
        let name = base + "_" + df.string(from: Date()) + (ext.isEmpty ? "" : "." + ext)
        let target = (dir as NSString).appendingPathComponent(name)
        do {
            try fm.createDirectory(atPath: dir, withIntermediateDirectories: true)
            if fm.fileExists(atPath: target) { return }
            try fm.copyItem(atPath: path, toPath: target)
            let prefix = base + "_"
            var old = try fm.contentsOfDirectory(atPath: dir).filter { $0.hasPrefix(prefix) && (ext.isEmpty || $0.hasSuffix("." + ext)) }
            old.sort()
            if old.count > 14 {
                for n in old.prefix(old.count - 14) { try? fm.removeItem(atPath: (dir as NSString).appendingPathComponent(n)) }
            }
        } catch { }   // 備份失敗不影響正常存檔
    }
}
