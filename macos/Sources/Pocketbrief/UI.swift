// 口袋句庫 Pocketbrief：視窗共用的小元件
// Copyright (c) 2026 無名小律師（楊朝淵律師）. Licensed under the MIT License; see the LICENSE file.
import AppKit

enum UI {
    static func label(_ s: String, bold: Bool = false, size: CGFloat? = nil) -> NSTextField {
        let l = NSTextField(labelWithString: s)
        let sz = size ?? NSFont.systemFontSize
        l.font = bold ? NSFont.boldSystemFont(ofSize: sz) : NSFont.systemFont(ofSize: sz)
        l.setContentCompressionResistancePriority(.required, for: .horizontal)
        return l
    }

    // 灰色的小字說明，會自動換行
    static func hint(_ s: String) -> NSTextField {
        let l = NSTextField(wrappingLabelWithString: s)
        l.font = NSFont.systemFont(ofSize: NSFont.smallSystemFontSize)
        l.textColor = .secondaryLabelColor
        l.setContentCompressionResistancePriority(.defaultLow, for: .horizontal)
        return l
    }

    static func button(_ title: String, _ target: AnyObject?, _ action: Selector) -> NSButton {
        let b = NSButton(title: title, target: target, action: action)
        b.bezelStyle = .rounded
        b.setContentCompressionResistancePriority(.required, for: .horizontal)
        return b
    }

    static func checkbox(_ title: String, on: Bool) -> NSButton {
        let b = NSButton(checkboxWithTitle: title, target: nil, action: nil)
        b.state = on ? .on : .off
        return b
    }

    static func spacer() -> NSView {
        let v = NSView()
        v.setContentHuggingPriority(NSLayoutConstraint.Priority(1), for: .horizontal)
        v.setContentCompressionResistancePriority(NSLayoutConstraint.Priority(1), for: .horizontal)
        return v
    }

    static func hstack(_ views: [NSView], spacing: CGFloat = 8, align: NSLayoutConstraint.Attribute = .centerY) -> NSStackView {
        let s = NSStackView(views: views)
        s.orientation = .horizontal
        s.alignment = align
        s.spacing = spacing
        return s
    }

    static func vstack(_ views: [NSView], spacing: CGFloat = 8, insets: NSEdgeInsets = NSEdgeInsets()) -> NSStackView {
        let s = NSStackView(views: views)
        s.orientation = .vertical
        s.alignment = .leading
        s.spacing = spacing
        s.edgeInsets = insets
        return s
    }

    // 讓直排裡的每一列都撐滿整個寬度
    static func fillWidth(_ rows: [NSView], in stack: NSStackView) {
        let inset = stack.edgeInsets.left + stack.edgeInsets.right
        for r in rows { r.widthAnchor.constraint(equalTo: stack.widthAnchor, constant: -inset).isActive = true }
    }

    // 一組標籤設成相同寬度（以最寬的為準），表單才會對齊
    static func equalWidths(_ labels: [NSView]) {
        let w = labels.map { $0.fittingSize.width }.max() ?? 0
        for l in labels { l.widthAnchor.constraint(equalToConstant: ceil(w)).isActive = true }
    }

    // 純文字的多行編輯區：關掉智慧引號、自動更正等會偷改內容的功能
    static func textArea(font: NSFont) -> (NSScrollView, NSTextView) {
        let sv = NSTextView.scrollableTextView()
        let tv = sv.documentView as! NSTextView
        tv.font = font
        tv.isRichText = false
        tv.importsGraphics = false
        tv.allowsUndo = true
        tv.isAutomaticQuoteSubstitutionEnabled = false
        tv.isAutomaticDashSubstitutionEnabled = false
        tv.isAutomaticTextReplacementEnabled = false
        tv.isAutomaticSpellingCorrectionEnabled = false
        tv.isContinuousSpellCheckingEnabled = false
        tv.isAutomaticLinkDetectionEnabled = false
        tv.smartInsertDeleteEnabled = false
        tv.textContainerInset = NSSize(width: 4, height: 6)
        sv.borderType = .bezelBorder
        sv.hasVerticalScroller = true
        sv.setContentHuggingPriority(NSLayoutConstraint.Priority(1), for: .vertical)
        sv.setContentHuggingPriority(NSLayoutConstraint.Priority(1), for: .horizontal)
        sv.setContentCompressionResistancePriority(NSLayoutConstraint.Priority(1), for: .vertical)
        return (sv, tv)
    }

    static func textField(font: NSFont) -> NSTextField {
        let f = NSTextField(string: "")
        f.font = font
        f.isAutomaticTextCompletionEnabled = false
        f.lineBreakMode = .byTruncatingTail
        f.usesSingleLineMode = true
        f.setContentHuggingPriority(NSLayoutConstraint.Priority(1), for: .horizontal)
        return f
    }

    // 代碼欄：編輯時自動切到英數輸入
    static func codeField(font: NSFont) -> NSTextField {
        let f = textField(font: font)
        (f.cell as? NSTextFieldCell)?.allowedInputSourceLocales = [NSAllRomanInputSourcesLocaleIdentifier]
        return f
    }
}

enum EditField { case title, code, content }

// 新增／修改範本共用的檢查：內容不可空白、代碼格式、代碼重複提醒。
// 通過就回傳整理好的範本；不通過回傳 nil 與應該回去修改的欄位。
enum TemplateValidator {
    static func build(title: String, code: String, content: String, original: Template?, existing: [Template]) -> (Template?, EditField?) {
        let text = Template.trimNewlines(Template.normalizeNewlines(content))
        if Template.isBlank(text) { Alerts.show(L.t("內容不能是空白。")); return (nil, .content) }
        let c = code.trimmingCharacters(in: .whitespacesAndNewlines)
        if !c.isEmpty && !Csv.isValidCode(c) {
            Alerts.show(L.t("代碼只能用英文字母、數字或半形符號，\n不能有空白、中文或 ` 鍵。"))
            return (nil, .code)
        }
        if !c.isEmpty, let x = existing.first(where: { $0.key == c.lowercased() && !$0.sameAs(original) }) {
            let r = Alerts.show(L.f("代碼「{0}」已經被「{1}」使用了。\n\n代碼重複的話，打這組代碼時清單會停住，讓你用方向鍵和 Enter 選。\n仍要儲存嗎？", c, x.displayTitle),
                                buttons: [L.t("儲存"), L.t("取消")])
            if r != 0 { return (nil, .code) }
        }
        return (Template(title: title.trimmingCharacters(in: .whitespacesAndNewlines), code: c, text: text), nil)
    }
}
