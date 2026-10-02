// 口袋句庫 Pocketbrief：全域快速鍵
// Copyright (c) 2026 無名小律師（楊朝淵律師）. Licensed under the MIT License; see the LICENSE file.
//
// 兩種快速鍵：
//   ‧修飾鍵組合（例如 ⌃`、⌃⇧0）：用系統的 RegisterEventHotKey 註冊，不需要額外權限
//   ‧前導鍵組合（`+1：按住 ` 再按 1）：系統沒有這種註冊方式，改用鍵盤事件攔截（需要「輸入監控」權限）
// 設定檔裡的寫法：Ctrl+Option+Shift+Cmd+鍵（Alt＝Option），或 `+鍵
import AppKit
import Carbon

// 按鍵名稱與 macOS 鍵碼（美式鍵盤位置）
enum KeyNames {
    static let table: [(String, UInt16)] = [
        ("A", 0), ("S", 1), ("D", 2), ("F", 3), ("H", 4), ("G", 5), ("Z", 6), ("X", 7), ("C", 8), ("V", 9),
        ("B", 11), ("Q", 12), ("W", 13), ("E", 14), ("R", 15), ("Y", 16), ("T", 17), ("1", 18), ("2", 19),
        ("3", 20), ("4", 21), ("6", 22), ("5", 23), ("=", 24), ("9", 25), ("7", 26), ("-", 27), ("8", 28),
        ("0", 29), ("]", 30), ("O", 31), ("U", 32), ("[", 33), ("I", 34), ("P", 35), ("L", 37), ("J", 38),
        ("'", 39), ("K", 40), (";", 41), ("\\", 42), (",", 43), ("/", 44), ("N", 45), ("M", 46), (".", 47),
        ("`", 50), ("Space", 49),
        ("F1", 122), ("F2", 120), ("F3", 99), ("F4", 118), ("F5", 96), ("F6", 97), ("F7", 98), ("F8", 100),
        ("F9", 101), ("F10", 109), ("F11", 103), ("F12", 111), ("F13", 105), ("F14", 107), ("F15", 113),
        ("F16", 106), ("F17", 64), ("F18", 79), ("F19", 80), ("F20", 90),
    ]

    static func code(_ name: String) -> UInt16? {
        let n = name.trimmingCharacters(in: .whitespaces)
        if n == "~" { return 50 }
        if n == "+" { return 24 }
        let up = n.count == 1 ? n.uppercased() : n
        for (k, c) in table where k.caseInsensitiveCompare(up) == .orderedSame { return c }
        return nil
    }

    static func name(_ code: UInt16) -> String? { table.first { $0.1 == code }?.0 }

    static func isFunctionKey(_ code: UInt16) -> Bool {
        guard let n = name(code) else { return false }
        return n.count >= 2 && n.hasPrefix("F") && Int(n.dropFirst()) != nil
    }
}

struct HotkeySpec {
    var ctrl = false, option = false, shift = false, cmd = false
    var keyCode: UInt16 = 0

    var carbonModifiers: UInt32 {
        var m: UInt32 = 0
        if ctrl { m |= UInt32(controlKey) }
        if option { m |= UInt32(optionKey) }
        if shift { m |= UInt32(shiftKey) }
        if cmd { m |= UInt32(cmdKey) }
        return m
    }
}

enum Hotkey {
    // 修飾鍵組合；看不懂回傳 nil
    static func parse(_ s: String) -> HotkeySpec? {
        let keyPart: String, modPart: String
        if s.hasSuffix("++") { keyPart = "+"; modPart = String(s.dropLast(2)) }
        else if let r = s.range(of: "+", options: .backwards) { keyPart = String(s[r.upperBound...]); modPart = String(s[..<r.lowerBound]) }
        else { keyPart = s; modPart = "" }
        var spec = HotkeySpec()
        for p0 in modPart.split(separator: "+") {
            switch p0.trimmingCharacters(in: .whitespaces).lowercased() {
            case "ctrl", "control": spec.ctrl = true
            case "alt", "option", "opt": spec.option = true
            case "shift": spec.shift = true
            case "cmd", "command", "win": spec.cmd = true
            default: return nil
            }
        }
        guard let code = KeyNames.code(keyPart) else { return nil }
        spec.keyCode = code
        return spec
    }

    // 前導鍵組合「`+鍵」；回傳第二個鍵的鍵碼
    static func parseChord(_ s: String) -> UInt16? {
        guard let i = s.firstIndex(of: "+"), i != s.startIndex, s.index(after: i) != s.endIndex else { return nil }
        let p = s[..<i].trimmingCharacters(in: .whitespaces)
        if p != "`" && p != "~" { return nil }
        guard let spec = parse(String(s[s.index(after: i)...]).trimmingCharacters(in: .whitespaces)) else { return nil }
        if spec.ctrl || spec.option || spec.shift || spec.cmd || spec.keyCode == 50 { return nil }
        return spec.keyCode
    }

    static func isValid(_ s: String) -> Bool { parse(s) != nil || parseChord(s) != nil }

    static func string(_ spec: HotkeySpec) -> String? {
        guard let name = KeyNames.name(spec.keyCode) else { return nil }
        var parts: [String] = []
        if spec.ctrl { parts.append("Ctrl") }
        if spec.option { parts.append("Option") }
        if spec.shift { parts.append("Shift") }
        if spec.cmd { parts.append("Cmd") }
        parts.append(name)
        return parts.joined(separator: "+")
    }

    // 系統常用的組合：被攔截會讓電腦很難用，而且本程式自己會送出 ⌘C／⌘V
    private static let reserved: Set<String> = [
        "Cmd+C", "Cmd+V", "Cmd+X", "Cmd+Z", "Shift+Cmd+Z", "Cmd+A", "Cmd+S", "Cmd+Q", "Cmd+W", "Cmd+N", "Cmd+F",
        "Cmd+P", "Cmd+O", "Cmd+T", "Cmd+H", "Cmd+M", "Cmd+,", "Cmd+Space", "Ctrl+Space", "Cmd+`", "Shift+Cmd+`",
        "Option+Cmd+Space", "Ctrl+Cmd+Space", "Ctrl+Cmd+Q", "Ctrl+Cmd+F",
    ]

    // 設定視窗錄製快速鍵用；不合用時回傳 nil
    static func fromEvent(code: UInt16, flags: NSEvent.ModifierFlags) -> String? {
        var spec = HotkeySpec(keyCode: code)
        spec.ctrl = flags.contains(.control); spec.option = flags.contains(.option)
        spec.shift = flags.contains(.shift); spec.cmd = flags.contains(.command)
        let fkey = KeyNames.isFunctionKey(code)
        if !fkey && !(spec.ctrl || spec.option || spec.cmd) { return nil }   // 沒有 ⌃⌥⌘ 的一般按鍵會跟打字衝突
        guard let s = string(spec) else { return nil }
        if reserved.contains(s) { return nil }
        return s
    }

    // 按住 ` 時按下的鍵 → 「`+鍵」
    static func fromChordKey(code: UInt16, flags: NSEvent.ModifierFlags) -> String? {
        if !flags.intersection([.control, .option, .shift, .command]).isEmpty { return nil }
        guard let name = KeyNames.name(code) else { return nil }
        let s = "`+" + name
        return parseChord(s) != nil ? s : nil
    }

    // 顯示用：Ctrl+Shift+0 → ⌃⇧0；`+1 → ` + 1
    static func display(_ s: String) -> String {
        if s.isEmpty { return "" }
        if let code = parseChord(s), let n = KeyNames.name(code) { return "` + " + pretty(n) }
        guard let spec = parse(s), let n = KeyNames.name(spec.keyCode) else { return s }
        var r = ""
        if spec.ctrl { r += "⌃" }
        if spec.option { r += "⌥" }
        if spec.shift { r += "⇧" }
        if spec.cmd { r += "⌘" }
        return r + pretty(n)
    }

    private static func pretty(_ n: String) -> String { n == "Space" ? L.t("空白鍵") : n }
}

// ───────────── 修飾鍵組合：RegisterEventHotKey ─────────────
final class HotkeyCenter {
    static let shared = HotkeyCenter()
    var onPress: ((UInt32) -> Void)?
    private var refs: [UInt32: EventHotKeyRef] = [:]
    private var installed = false

    func install() {
        if installed { return }
        installed = true
        var spec = EventTypeSpec(eventClass: OSType(kEventClassKeyboard), eventKind: UInt32(kEventHotKeyPressed))
        InstallEventHandler(GetApplicationEventTarget(), { _, event, _ -> OSStatus in
            var hk = EventHotKeyID()
            let st = GetEventParameter(event, EventParamName(kEventParamDirectObject), EventParamType(typeEventHotKeyID),
                                       nil, MemoryLayout<EventHotKeyID>.size, nil, &hk)
            if st == noErr {
                let id = hk.id
                DispatchQueue.main.async { HotkeyCenter.shared.onPress?(id) }
            }
            return noErr
        }, 1, &spec, nil, nil)
    }

    // 回傳 OSStatus；noErr 表示成功，eventHotKeyExistsErr 表示被其他程式占用
    func register(id: UInt32, spec: HotkeySpec) -> OSStatus {
        unregister(id: id)
        var ref: EventHotKeyRef?
        let hkID = EventHotKeyID(signature: OSType(0x5042_4B54), id: id)   // 'PBKT'
        let st = RegisterEventHotKey(UInt32(spec.keyCode), spec.carbonModifiers, hkID, GetApplicationEventTarget(), 0, &ref)
        if st == noErr, let r = ref { refs[id] = r }
        return st
    }

    func unregister(id: UInt32) {
        if let r = refs.removeValue(forKey: id) { UnregisterEventHotKey(r) }
    }

    func unregisterAll() {
        for id in Array(refs.keys) { unregister(id: id) }
    }
}

// ───────────── 前導鍵組合（例如 `+1）的判斷邏輯 ─────────────
// 與 Windows 版完全相同：按下 ` 時先扣住不送出——
//   接著按到設定的鍵 → 觸發快速鍵（` 和那個鍵都不送出）
//   什麼都沒按就放開 → 補送一個 `，照常打字
//   按了其他鍵 → 先補送 `，再送出那個鍵，順序不變
// 按著 ⇧⌃⌥⌘ 時按 `（例如 ～）完全不攔。
final class ChordLogic {
    static let pass = 0, eat = 1, eatTapPrefix = 2, eatReplay = 3, fire = 4

    private let prefixes: [UInt16], keys: [UInt16], ids: [UInt32]
    private var held: UInt16 = 0xFFFF       // 目前按住（被扣住）的前導鍵；0xFFFF 表示沒有（鍵碼 0 是 A）
    private var consumed = false            // 這次按住期間已經觸發過或已補送過 `
    private var swallow: UInt16 = 0xFFFF    // 觸發鍵放開前的自動重複與放開都要吃掉
    private var lastPrefixTime: UInt32 = 0

    init(prefixes: [UInt16], keys: [UInt16], ids: [UInt32]) { self.prefixes = prefixes; self.keys = keys; self.ids = ids }

    var heldPrefix: UInt16? { held == 0xFFFF ? nil : held }

    // time：毫秒時間戳；modifiers：當下是否按著 ⇧⌃⌥⌘
    func onKey(_ code: UInt16, down: Bool, time: UInt32, modifiers: Bool, id: inout UInt32) -> Int {
        id = 0
        if swallow != 0xFFFF && code == swallow { if !down { swallow = 0xFFFF }; return ChordLogic.eat }
        if held != 0xFFFF && code != held && (time &- lastPrefixTime) > 1500 {
            held = 0xFFFF   // 按住的鍵一定會持續自動重複；很久沒消息表示放開的事件漏掉了，不要卡住
        }
        if held == 0xFFFF {
            if down && !modifiers && prefixes.contains(code) { held = code; consumed = false; lastPrefixTime = time; return ChordLogic.eat }
            return ChordLogic.pass
        }
        if code == held {
            lastPrefixTime = time
            if down { return ChordLogic.eat }   // 自動重複
            held = 0xFFFF
            return consumed ? ChordLogic.eat : ChordLogic.eatTapPrefix   // 單獨按一下 ` → 補送
        }
        if !down || consumed { return ChordLogic.pass }
        for i in 0..<keys.count where prefixes[i] == held && keys[i] == code && !modifiers {
            consumed = true; swallow = code; id = ids[i]
            return ChordLogic.fire
        }
        consumed = true
        return ChordLogic.eatReplay
    }
}

// 本程式自己送出的按鍵都打上這個記號，攔截時一律放行
let pocketbriefEventMark: Int64 = 0x5042_4B54

// ───────────── 前導鍵組合的鍵盤攔截（CGEventTap） ─────────────
// 在獨立的執行緒上跑：系統規定攔截要很快回應，否則會暫停攔截，所以不跟可能忙碌的主執行緒擠在一起。
final class ChordTap {
    private let logic: ChordLogic
    private let fire: (UInt32) -> Void
    private var tap: CFMachPort?
    private var runLoop: CFRunLoop?
    private var thread: Thread?

    init(logic: ChordLogic, fire: @escaping (UInt32) -> Void) { self.logic = logic; self.fire = fire }

    // 沒有「輸入監控」權限時回傳 false
    func start() -> Bool {
        let mask = (1 << CGEventType.keyDown.rawValue) | (1 << CGEventType.keyUp.rawValue)
        guard let t = CGEvent.tapCreate(tap: .cgSessionEventTap, place: .headInsertEventTap, options: .defaultTap,
                                        eventsOfInterest: CGEventMask(mask), callback: chordTapCallback,
                                        userInfo: Unmanaged.passUnretained(self).toOpaque()) else { return false }
        tap = t
        let ready = DispatchSemaphore(value: 0)
        let th = Thread { [weak self] in
            guard let self = self, let t = self.tap else { ready.signal(); return }
            let src = CFMachPortCreateRunLoopSource(kCFAllocatorDefault, t, 0)
            self.runLoop = CFRunLoopGetCurrent()
            CFRunLoopAddSource(CFRunLoopGetCurrent(), src, .commonModes)
            CGEvent.tapEnable(tap: t, enable: true)
            ready.signal()
            CFRunLoopRun()
        }
        th.name = "ChordTap"
        th.start()
        thread = th
        _ = ready.wait(timeout: .now() + 3)
        return true
    }

    func stop() {
        if let t = tap { CGEvent.tapEnable(tap: t, enable: false); CFMachPortInvalidate(t) }
        if let rl = runLoop { CFRunLoopStop(rl) }
        tap = nil; runLoop = nil; thread = nil
    }

    fileprivate func handle(proxy: CGEventTapProxy, type: CGEventType, event: CGEvent) -> Unmanaged<CGEvent>? {
        if type == .tapDisabledByTimeout || type == .tapDisabledByUserInput {
            if let t = tap { CGEvent.tapEnable(tap: t, enable: true) }
            return Unmanaged.passUnretained(event)
        }
        guard type == .keyDown || type == .keyUp,
              event.getIntegerValueField(.eventSourceUserData) != pocketbriefEventMark else { return Unmanaged.passUnretained(event) }
        let code = UInt16(truncatingIfNeeded: event.getIntegerValueField(.keyboardEventKeycode))
        let down = type == .keyDown
        let mods = !event.flags.intersection([.maskShift, .maskControl, .maskAlternate, .maskCommand]).isEmpty
        let time = UInt32(truncatingIfNeeded: event.timestamp / 1_000_000)
        let prefix = logic.heldPrefix
        var id: UInt32 = 0
        let r = logic.onKey(code, down: down, time: time, modifiers: mods, id: &id)
        switch r {
        case ChordLogic.fire:
            let f = fire
            DispatchQueue.main.async { f(id) }
        case ChordLogic.eatTapPrefix:
            ChordTap.post(code: code, down: true, proxy: proxy)
            ChordTap.post(code: code, down: false, proxy: proxy)
        case ChordLogic.eatReplay:
            // 先補送 `，再重送這個鍵的按下（放開照常由系統送出），順序才會正確
            if let p = prefix {
                ChordTap.post(code: p, down: true, proxy: proxy)
                ChordTap.post(code: p, down: false, proxy: proxy)
            }
            if let copy = event.copy() {
                copy.setIntegerValueField(.eventSourceUserData, value: pocketbriefEventMark)
                copy.tapPostEvent(proxy)
            }
        default: break
        }
        return r == ChordLogic.pass ? Unmanaged.passUnretained(event) : nil
    }

    private static func post(code: UInt16, down: Bool, proxy: CGEventTapProxy) {
        guard let e = CGEvent(keyboardEventSource: CGEventSource(stateID: .hidSystemState), virtualKey: code, keyDown: down) else { return }
        e.flags = []
        e.setIntegerValueField(.eventSourceUserData, value: pocketbriefEventMark)
        e.tapPostEvent(proxy)
    }
}

private func chordTapCallback(proxy: CGEventTapProxy, type: CGEventType, event: CGEvent, refcon: UnsafeMutableRawPointer?) -> Unmanaged<CGEvent>? {
    guard let refcon = refcon else { return Unmanaged.passUnretained(event) }
    return Unmanaged<ChordTap>.fromOpaque(refcon).takeUnretainedValue().handle(proxy: proxy, type: type, event: event)
}
