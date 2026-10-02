// 口袋句庫 Pocketbrief：範本清單的外觀（字型、顏色、大小）、配色主題與繪製
// Copyright (c) 2026 無名小律師（楊朝淵律師）. Licensed under the MIT License; see the LICENSE file.
import AppKit

extension NSColor {
    convenience init?(hex: String) {
        var s = hex.trimmingCharacters(in: .whitespacesAndNewlines)
        if s.hasPrefix("#") { s.removeFirst() }
        guard s.count == 6, let v = UInt32(s, radix: 16) else { return nil }
        self.init(srgbRed: CGFloat((v >> 16) & 0xFF) / 255, green: CGFloat((v >> 8) & 0xFF) / 255, blue: CGFloat(v & 0xFF) / 255, alpha: 1)
    }

    var hexString: String {
        guard let c = usingColorSpace(.sRGB) else { return "" }
        func b(_ x: CGFloat) -> Int { max(0, min(255, Int((x * 255).rounded()))) }
        return String(format: "#%02X%02X%02X", b(c.redComponent), b(c.greenComponent), b(c.blueComponent))
    }

    // 繪製時才呼叫：把會跟著淺色／深色變化的系統顏色，換成目前外觀下的實際顏色
    var resolved: NSColor { usingColorSpace(.sRGB) ?? self }

    func mixed(with other: NSColor, _ t: CGFloat) -> NSColor {
        resolved.blended(withFraction: t, of: other.resolved) ?? self
    }
}

// 範本清單（叫出的框框）的外觀；back／fore 為 nil 時代表跟隨系統配色
final class PopupStyle {
    static let defaultFont = "PingFang TC"
    static let classicBlue = NSColor(hex: "#2563EB")!

    var fontName = PopupStyle.defaultFont
    var fontSize: CGFloat = 14
    var back: NSColor? = nil
    var fore: NSColor? = nil
    var codeFore = PopupStyle.classicBlue
    var selBack = PopupStyle.classicBlue
    var selFore = NSColor.white
    var headerBack = PopupStyle.classicBlue
    var headerFore = NSColor.white
    var width = 640    // 點
    var rows = 10      // 一次顯示幾筆

    func copy() -> PopupStyle {
        let s = PopupStyle()
        s.fontName = fontName; s.fontSize = fontSize; s.back = back; s.fore = fore
        s.codeFore = codeFore; s.selBack = selBack; s.selFore = selFore
        s.headerBack = headerBack; s.headerFore = headerFore; s.width = width; s.rows = rows
        return s
    }

    var backR: NSColor { back ?? NSColor.textBackgroundColor }
    var foreR: NSColor { fore ?? NSColor.textColor }

    // 底色跟隨系統又是深色模式時，預設的藍色代碼在深灰底上太暗，改用亮一點的藍
    func codeForeR(dark: Bool) -> NSColor {
        if dark && back == nil && codeFore.hexString == PopupStyle.classicBlue.hexString { return NSColor(hex: "#6CA0FF")! }
        return codeFore
    }

    static func fontExists(_ name: String) -> Bool {
        NSFontManager.shared.availableFontFamilies.contains(name) || NSFont(name: name, size: 12) != nil
    }

    // 字型在這台電腦沒有安裝時（例如雲端同步到另一台電腦），改用預設字型
    func makeFont(bold: Bool) -> NSFont {
        let size = max(6, min(48, fontSize.isFinite ? fontSize : 14))
        let fm = NSFontManager.shared
        for name in [fontName, PopupStyle.defaultFont] {
            if let f = fm.font(withFamily: name, traits: bold ? .boldFontMask : [], weight: bold ? 9 : 5, size: size) { return f }
            if let f = NSFont(name: name, size: size) { return bold ? fm.convert(f, toHaveTrait: .boldFontMask) : f }
        }
        return bold ? NSFont.boldSystemFont(ofSize: size) : NSFont.systemFont(ofSize: size)
    }

    var key: String {
        [fontName, "\(fontSize)", back?.hexString ?? "", fore?.hexString ?? "", codeFore.hexString, selBack.hexString,
         selFore.hexString, headerBack.hexString, headerFore.hexString, "\(width)", "\(rows)"].joined(separator: "|")
    }
}

// 內建配色主題（只換顏色，不動字型與大小）
struct ThemePreset {
    let name: String       // 以繁體中文命名，顯示時經 L.t 翻譯
    var eyeCare = false    // 護眼配色
    let back: String, fore: String, code: String, selBack: String, selFore: String, headBack: String, headFore: String

    static let all: [ThemePreset] = [
        ThemePreset(name: "經典藍", back: "", fore: "", code: "#2563EB", selBack: "#2563EB", selFore: "#FFFFFF", headBack: "#2563EB", headFore: "#FFFFFF"),
        ThemePreset(name: "深色", back: "#1E1E1E", fore: "#D4D4D4", code: "#4FC1FF", selBack: "#264F78", selFore: "#FFFFFF", headBack: "#333333", headFore: "#FFFFFF"),
        // 護眼系：低對比、少藍光，底色不用純白、文字不用純黑
        ThemePreset(name: "豆沙綠", eyeCare: true, back: "#C7EDCC", fore: "#2F3E30", code: "#3B7A45", selBack: "#5E8C61", selFore: "#FFFFFF", headBack: "#4E7351", headFore: "#F0FFF0"),
        ThemePreset(name: "暖米紙", eyeCare: true, back: "#F4ECD8", fore: "#5B4636", code: "#9C6B30", selBack: "#A67C52", selFore: "#FFFDF5", headBack: "#704F38", headFore: "#F9F1E1"),
        ThemePreset(name: "森林綠", back: "#F3F8F3", fore: "#1F2D1F", code: "#2E7D32", selBack: "#2E7D32", selFore: "#FFFFFF", headBack: "#1B5E20", headFore: "#E8F5E9"),
        ThemePreset(name: "摩卡棕", back: "#FFF8E7", fore: "#3B2F2F", code: "#B03A2E", selBack: "#8B5E3C", selFore: "#FFFFFF", headBack: "#5B3A29", headFore: "#FFE9C7"),
        ThemePreset(name: "朱紅", back: "#FFFDF8", fore: "#2B2B2B", code: "#C0392B", selBack: "#C0392B", selFore: "#FFFFFF", headBack: "#8E1F14", headFore: "#FFF3E0"),
        ThemePreset(name: "紫藤", back: "#FAF7FD", fore: "#2E2440", code: "#7E57C2", selBack: "#7E57C2", selFore: "#FFFFFF", headBack: "#4A3780", headFore: "#F3E5F5"),
        ThemePreset(name: "Solarized", back: "#FDF6E3", fore: "#586E75", code: "#268BD2", selBack: "#268BD2", selFore: "#FDF6E3", headBack: "#073642", headFore: "#EEE8D5"),
        ThemePreset(name: "高對比", back: "#000000", fore: "#FFFFFF", code: "#FFFF00", selBack: "#FFFF00", selFore: "#000000", headBack: "#1A1A1A", headFore: "#FFFF00"),
    ]

    func apply(to st: PopupStyle) {
        st.back = NSColor(hex: back); st.fore = NSColor(hex: fore)
        st.codeFore = NSColor(hex: code)!; st.selBack = NSColor(hex: selBack)!; st.selFore = NSColor(hex: selFore)!
        st.headerBack = NSColor(hex: headBack)!; st.headerFore = NSColor(hex: headFore)!
    }

    func matches(_ st: PopupStyle) -> Bool {
        (st.back?.hexString ?? "") == back && (st.fore?.hexString ?? "") == fore && st.codeFore.hexString == code
            && st.selBack.hexString == selBack && st.selFore.hexString == selFore
            && st.headerBack.hexString == headBack && st.headerFore.hexString == headFore
    }
}

// 範本清單的尺寸：實際的清單與設定視窗的預覽共用
struct PopupMetrics {
    let rowFont: NSFont, headerFont: NSFont, hintFont: NSFont
    let lineH: CGFloat, rowH: CGFloat, headerH: CGFloat, previewH: CGFloat, hintH: CGFloat

    init(style: PopupStyle) {
        rowFont = style.makeFont(bold: false)
        headerFont = style.makeFont(bold: true)
        hintFont = NSFont.systemFont(ofSize: max(10, min(13, style.fontSize * 0.82)))
        lineH = PopupRenderer.lineHeight(rowFont)
        rowH = lineH + 8
        headerH = PopupRenderer.lineHeight(headerFont) + 12
        previewH = lineH * 3 + 12
        hintH = PopupRenderer.lineHeight(hintFont) + 8
    }

    var codeWidth: CGFloat { ("00000" as NSString).size(withAttributes: [.font: rowFont]).width + 18 }

    func totalHeight(rows: Int) -> CGFloat { 2 + headerH + rowH * CGFloat(rows) + 2 + previewH + hintH }
}

enum PopupRenderer {
    static func lineHeight(_ f: NSFont) -> CGFloat { ceil(NSLayoutManager().defaultLineHeight(for: f)) }

    // 單行、超出時以「…」截斷，垂直置中（在 isFlipped 的 view 裡使用）
    static func drawLine(_ s: String, in r: NSRect, font: NSFont, color: NSColor, center: Bool = false) {
        guard r.width > 2 else { return }
        let ps = NSMutableParagraphStyle()
        ps.lineBreakMode = .byTruncatingTail
        if center { ps.alignment = .center }
        let attrs: [NSAttributedString.Key: Any] = [.font: font, .foregroundColor: color, .paragraphStyle: ps]
        let lh = lineHeight(font)
        let rr = NSRect(x: r.minX, y: r.minY + (r.height - lh) / 2, width: r.width, height: lh)
        (s as NSString).draw(with: rr, options: [.usesLineFragmentOrigin, .truncatesLastVisibleLine], attributes: attrs)
    }

    // 多行（預覽區）：自動換行，放不下的部分截掉
    static func drawWrapped(_ s: String, in r: NSRect, font: NSFont, color: NSColor) {
        guard r.width > 2, r.height > 2 else { return }
        let ps = NSMutableParagraphStyle()
        ps.lineBreakMode = .byWordWrapping
        let attrs: [NSAttributedString.Key: Any] = [.font: font, .foregroundColor: color, .paragraphStyle: ps]
        (s as NSString).draw(with: r, options: [.usesLineFragmentOrigin, .truncatesLastVisibleLine], attributes: attrs)
    }

    static func drawRow(_ r: NSRect, code: String, title: String, text: String, selected: Bool, dim: Bool,
                        style st: PopupStyle, metrics m: PopupMetrics, dark: Bool) {
        var back = selected ? st.selBack : st.backR
        var fore = selected ? st.selFore : st.foreR
        var codeFore = selected ? st.selFore : st.codeForeR(dark: dark)
        back = back.resolved
        fore = fore.resolved
        codeFore = codeFore.resolved
        if dim { fore = st.backR.mixed(with: st.foreR, 0.5); codeFore = fore }
        back.setFill()
        r.fill()
        let pad: CGFloat = 10
        if dim {   // 提示訊息：橫跨整列
            drawLine(title, in: NSRect(x: r.minX + pad, y: r.minY, width: r.width - pad * 2, height: r.height), font: m.rowFont, color: fore)
            return
        }
        let cw = m.codeWidth
        var tw = (r.width * 0.26).rounded()
        if title.isEmpty { tw = 0 }   // 沒有標題時，內容直接從標題欄開始
        drawLine(code, in: NSRect(x: r.minX + pad, y: r.minY, width: cw - pad, height: r.height), font: m.rowFont, color: codeFore)
        if tw > 0 { drawLine(title, in: NSRect(x: r.minX + cw, y: r.minY, width: tw - 8, height: r.height), font: m.rowFont, color: fore) }
        drawLine(text, in: NSRect(x: r.minX + cw + tw, y: r.minY, width: r.width - cw - tw - pad, height: r.height), font: m.rowFont, color: fore)
    }

    static func isDark(_ view: NSView) -> Bool {
        view.effectiveAppearance.bestMatch(from: [.darkAqua, .aqua]) == .darkAqua
    }
}
