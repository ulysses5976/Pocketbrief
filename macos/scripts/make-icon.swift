// 產生 App 圖示（與 Windows 版相同：藍色圓角方塊裡一個白色的「句」字）
// 用法：swift scripts/make-icon.swift <輸出的 .iconset 資料夾>
import AppKit

let out = CommandLine.arguments.count > 1 ? CommandLine.arguments[1] : "AppIcon.iconset"
try? FileManager.default.createDirectory(atPath: out, withIntermediateDirectories: true)

func render(_ px: Int) -> Data? {
    guard let rep = NSBitmapImageRep(bitmapDataPlanes: nil, pixelsWide: px, pixelsHigh: px, bitsPerSample: 8, samplesPerPixel: 4,
                                     hasAlpha: true, isPlanar: false, colorSpaceName: .deviceRGB, bytesPerRow: 0, bitsPerPixel: 0),
          let ctx = NSGraphicsContext(bitmapImageRep: rep) else { return nil }
    NSGraphicsContext.saveGraphicsState()
    NSGraphicsContext.current = ctx
    let s = CGFloat(px)
    // macOS 圖示慣例：主體約佔畫布 80%，四周留白
    let box = NSRect(x: s * 0.1, y: s * 0.1, width: s * 0.8, height: s * 0.8)
    let path = NSBezierPath(roundedRect: box, xRadius: s * 0.18, yRadius: s * 0.18)
    NSColor(srgbRed: 37 / 255, green: 99 / 255, blue: 235 / 255, alpha: 1).setFill()
    path.fill()
    let font = NSFont(name: "PingFangTC-Semibold", size: s * 0.5) ?? NSFont.boldSystemFont(ofSize: s * 0.5)
    let attrs: [NSAttributedString.Key: Any] = [.font: font, .foregroundColor: NSColor.white]
    let glyph = "句" as NSString
    let size = glyph.size(withAttributes: attrs)
    glyph.draw(at: NSPoint(x: box.midX - size.width / 2, y: box.midY - size.height / 2), withAttributes: attrs)
    NSGraphicsContext.restoreGraphicsState()
    return rep.representation(using: .png, properties: [:])
}

for base in [16, 32, 128, 256, 512] {
    for scale in [1, 2] {
        let name = scale == 1 ? "icon_\(base)x\(base).png" : "icon_\(base)x\(base)@2x.png"
        if let d = render(base * scale) { try d.write(to: URL(fileURLWithPath: out + "/" + name)) }
    }
}
print("icon written to \(out)")
