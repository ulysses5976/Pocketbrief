// 口袋句庫 Pocketbrief：快速新增範本（反白文字後按快速鍵）
// Copyright (c) 2026 無名小律師（楊朝淵律師）. Licensed under the MIT License; see the LICENSE file.
import AppKit

final class QuickAddWindow: NSWindowController, NSWindowDelegate, NSTextFieldDelegate, NSTextViewDelegate {
    private let titleField: NSTextField
    private let codeField: NSTextField
    private let textView: NSTextView
    private let counter = UI.label("")
    private let existing: [Template]
    private var result: Template?

    // 以對話框方式開啟，按「確認」回傳整理好的範本，取消回傳 nil
    static func run(prefill: String, existing: [Template], contentFontSize: CGFloat) -> Template? {
        let w = QuickAddWindow(prefill: prefill, existing: existing, contentFontSize: contentFontSize)
        guard let win = w.window else { return nil }
        NSApp.activate(ignoringOtherApps: true)
        win.center()
        win.makeKeyAndOrderFront(nil)
        win.makeFirstResponder(w.titleField)
        NSApp.runModal(for: win)
        win.orderOut(nil)
        return w.result
    }

    private init(prefill: String, existing: [Template], contentFontSize: CGFloat) {
        self.existing = existing
        let font = NSFont.systemFont(ofSize: contentFontSize)
        titleField = UI.textField(font: font)
        codeField = UI.codeField(font: font)
        let (scroll, tv) = UI.textArea(font: font)
        textView = tv
        let win = NSWindow(contentRect: NSRect(x: 0, y: 0, width: 620, height: 460), styleMask: [.titled, .closable, .resizable],
                           backing: .buffered, defer: false)
        win.title = L.t("快速新增範本")
        win.minSize = NSSize(width: 460, height: 340)
        win.level = .floating
        super.init(window: win)
        win.delegate = self

        let lt = UI.label(L.t("名稱")), lc = UI.label(L.t("代碼")), lx = UI.label(L.t("內容")), lh = UI.label("")
        UI.equalWidths([lt, lc, lx, lh])
        codeField.delegate = self
        textView.delegate = self
        let ok = UI.button(L.t("確認"), self, #selector(confirm))
        ok.keyEquivalent = "\r"
        ok.keyEquivalentModifierMask = [.command]
        let cancel = UI.button(L.t("取消"), self, #selector(cancelAction))
        cancel.keyEquivalent = "\u{1b}"
        counter.textColor = .secondaryLabelColor

        let rows: [NSView] = [
            UI.hstack([lt, titleField]),
            UI.hstack([lc, codeField]),
            UI.hstack([lh, UI.hint(L.t("英文字母、數字或符號，最多 20 碼（不分大小寫）。叫出清單後打這組代碼就會直接輸出。"))], align: .top),
            UI.hstack([lx, scroll], align: .top),
            UI.hstack([counter, UI.spacer(), cancel, ok]),
        ]
        let stack = UI.vstack(rows, spacing: 10, insets: NSEdgeInsets(top: 16, left: 16, bottom: 16, right: 16))
        stack.translatesAutoresizingMaskIntoConstraints = false
        let content = NSView()
        content.addSubview(stack)
        NSLayoutConstraint.activate([
            stack.leadingAnchor.constraint(equalTo: content.leadingAnchor), stack.trailingAnchor.constraint(equalTo: content.trailingAnchor),
            stack.topAnchor.constraint(equalTo: content.topAnchor), stack.bottomAnchor.constraint(equalTo: content.bottomAnchor),
        ])
        UI.fillWidth(rows, in: stack)
        win.contentView = content

        textView.string = prefill
        updateCounter()
    }

    required init?(coder: NSCoder) { fatalError() }

    private func updateCounter() {
        counter.stringValue = L.f("字數：{0}", Template.normalizeNewlines(textView.string).count) + L.t("　（⌘↩ 確認）")
    }

    func textDidChange(_ notification: Notification) { updateCounter() }

    func controlTextDidChange(_ obj: Notification) {
        // 代碼最多 20 碼
        if codeField.stringValue.count > 20 { codeField.stringValue = String(codeField.stringValue.prefix(20)) }
    }

    @objc private func confirm() {
        let (r, bad) = TemplateValidator.build(title: titleField.stringValue, code: codeField.stringValue, content: textView.string,
                                              original: nil, existing: existing)
        guard let t = r else {
            switch bad {
            case .code?: window?.makeFirstResponder(codeField)
            case .content?: window?.makeFirstResponder(textView)
            default: break
            }
            return
        }
        result = t
        NSApp.stopModal()
    }

    @objc private func cancelAction() { result = nil; NSApp.stopModal() }

    func windowWillClose(_ notification: Notification) { NSApp.stopModal() }
}
