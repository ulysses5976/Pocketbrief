// 口袋句庫 Pocketbrief：深色模式
// Copyright (c) 2026 無名小律師（楊朝淵律師）. Licensed under the MIT License; see the LICENSE file.
//
// WinForms 沒有內建深色模式，這裡在視窗建好之後逐一替控制項換色；
// 淺色模式什麼都不做，維持 Windows 原生外觀。
using System;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using Microsoft.Win32;

namespace Pocketbrief
{
    static class Theme
    {
        public static bool Dark;
        public static Color Back = SystemColors.Control;        // 視窗、面板
        public static Color Surface = SystemColors.Control;     // 工具列、狀態列、按鈕
        public static Color InputBack = SystemColors.Window;    // 輸入框、清單
        public static Color Fore = SystemColors.ControlText;
        public static Color SubFore = Color.Gray;               // 說明文字
        public static Color Border = SystemColors.ControlDark;
        public static Color Hover = SystemColors.ControlLight;
        public static Color SelBack = SystemColors.Highlight;
        public static Color SelFore = SystemColors.HighlightText;

        // mode：light／dark／auto（跟隨 Windows 的應用程式模式）
        public static void Init(string mode)
        {
            Dark = mode == "dark" || (mode != "light" && WindowsPrefersDark());
            if (!Dark) return;
            Back = Color.FromArgb(0x20, 0x20, 0x20);
            Surface = Color.FromArgb(0x2B, 0x2B, 0x2B);
            InputBack = Color.FromArgb(0x2A, 0x2A, 0x2A);
            Fore = Color.FromArgb(0xE4, 0xE4, 0xE4);
            SubFore = Color.FromArgb(0x9A, 0x9A, 0x9A);
            Border = Color.FromArgb(0x48, 0x48, 0x48);
            Hover = Color.FromArgb(0x3A, 0x3A, 0x3A);
            SelBack = Color.FromArgb(0x26, 0x4F, 0x78);
            SelFore = Color.White;
        }

        public static bool WindowsPrefersDark()
        {
            try
            {
                using (RegistryKey k = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize"))
                {
                    object v = k == null ? null : k.GetValue("AppsUseLightTheme");
                    return v is int && (int)v == 0;
                }
            }
            catch { return false; }
        }

        [DllImport("dwmapi.dll")] static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int value, int size);
        [DllImport("uxtheme.dll", CharSet = CharSet.Unicode)] static extern int SetWindowTheme(IntPtr hwnd, string app, string idList);

        // 視窗標題列（Windows 10 20H1 以後、Windows 11）
        static void DarkTitleBar(Form f)
        {
            Action apply = () =>
            {
                int on = 1;
                if (DwmSetWindowAttribute(f.Handle, 20, ref on, 4) != 0) DwmSetWindowAttribute(f.Handle, 19, ref on, 4);
            };
            if (f.IsHandleCreated) apply(); else f.HandleCreated += delegate { apply(); };
        }

        // 捲軸改成深色
        static void DarkScrollBars(Control c) { SetTheme(c, "DarkMode_Explorer"); }

        static void SetTheme(Control c, string name)
        {
            Action apply = () => { try { SetWindowTheme(c.Handle, name, null); } catch { } };
            if (c.IsHandleCreated) apply(); else c.HandleCreated += delegate { apply(); };
        }

        [DllImport("user32.dll")] public static extern IntPtr SendMessage(IntPtr h, int msg, IntPtr w, IntPtr l);

        // 視窗建好之後呼叫一次
        public static void Apply(Control root)
        {
            if (!Dark) return;
            var f = root as Form;
            if (f != null) DarkTitleBar(f);
            Walk(root);
        }

        public static void ApplyMenu(ToolStrip menu)
        {
            if (!Dark) return;
            StyleStrip(menu);
        }

        static void Walk(Control c)
        {
            if (c is TextBoxBase)
            {
                c.BackColor = InputBack; c.ForeColor = Fore;
                var tb = c as TextBox;
                if (tb != null && tb.BorderStyle == BorderStyle.Fixed3D) tb.BorderStyle = BorderStyle.FixedSingle;
                if (tb != null && tb.Multiline) DarkScrollBars(tb);
            }
            else if (c is ListView) StyleListView((ListView)c);
            else if (c is NumericUpDown) { c.BackColor = InputBack; c.ForeColor = Fore; ((NumericUpDown)c).BorderStyle = BorderStyle.FixedSingle; }
            else if (c is ComboBox) { var cb = (ComboBox)c; cb.BackColor = InputBack; cb.ForeColor = Fore; SetTheme(cb, "DarkMode_CFD"); }
            else if (c is Button)
            {
                var b = (Button)c;
                if (!"swatch".Equals(b.Tag))   // 顏色色塊按鈕保留自己的顏色
                {
                    b.FlatStyle = FlatStyle.Flat; b.BackColor = Surface; b.ForeColor = Fore;
                    b.FlatAppearance.BorderColor = Border; b.FlatAppearance.MouseOverBackColor = Hover;
                }
            }
            else if (c is ToolStrip) StyleStrip((ToolStrip)c);
            else if (c is LinkLabel) { var l = (LinkLabel)c; l.LinkColor = SubFore; l.ActiveLinkColor = Fore; }
            else if (c is SplitContainer) { c.BackColor = Border; }
            else if (c is TabPage) { ((TabPage)c).UseVisualStyleBackColor = false; c.BackColor = Back; c.ForeColor = Fore; }
            else if (c is Form || c is Panel || c is GroupBox || c is TabControl || c is SplitterPanel) { c.BackColor = Back; c.ForeColor = Fore; }
            foreach (Control child in c.Controls) Walk(child);
        }

        static void StyleStrip(ToolStrip ts)
        {
            ts.Renderer = new ToolStripProfessionalRenderer(new DarkColors()) { RoundedEdges = false };
            ts.BackColor = Surface; ts.ForeColor = Fore;
            foreach (ToolStripItem it in ts.Items) StyleItem(it);
        }

        static void StyleItem(ToolStripItem it)
        {
            it.ForeColor = Fore;
            var tb = it as ToolStripTextBox;
            if (tb != null) { tb.BackColor = InputBack; tb.ForeColor = Fore; }
            var host = it as ToolStripControlHost;
            if (host != null && tb == null && host.Control != null) Walk(host.Control);
            var dd = it as ToolStripDropDownItem;
            if (dd != null) foreach (ToolStripItem sub in dd.DropDownItems) StyleItem(sub);
        }

        // 清單：欄位標題與每一列自己畫（系統畫的標題列在深色底上會是一塊白）
        static void StyleListView(ListView lv)
        {
            lv.BackColor = InputBack; lv.ForeColor = Fore;
            DarkScrollBars(lv);
            Action header = () => { try { IntPtr h = SendMessage(lv.Handle, 0x101F /* LVM_GETHEADER */, IntPtr.Zero, IntPtr.Zero); if (h != IntPtr.Zero) SetWindowTheme(h, "DarkMode_ItemsView", null); } catch { } };
            if (lv.IsHandleCreated) header(); else lv.HandleCreated += delegate { header(); };
            if (lv.OwnerDraw) return;   // 已經自己畫的（例如範本清單）不動
            lv.OwnerDraw = true;
            lv.DrawColumnHeader += (s, e) =>
            {
                using (var b = new SolidBrush(Surface)) e.Graphics.FillRectangle(b, e.Bounds);
                using (var p = new Pen(Border)) { e.Graphics.DrawLine(p, e.Bounds.Right - 1, e.Bounds.Top + 4, e.Bounds.Right - 1, e.Bounds.Bottom - 4); e.Graphics.DrawLine(p, e.Bounds.Left, e.Bounds.Bottom - 1, e.Bounds.Right, e.Bounds.Bottom - 1); }
                var r = new Rectangle(e.Bounds.X + 6, e.Bounds.Y, e.Bounds.Width - 8, e.Bounds.Height);
                TextRenderer.DrawText(e.Graphics, e.Header.Text, lv.Font, r, Fore, TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix);
            };
            lv.DrawItem += (s, e) => { };
            lv.DrawSubItem += (s, e) =>
            {
                bool sel = e.Item.Selected;
                using (var b = new SolidBrush(sel ? SelBack : InputBack)) e.Graphics.FillRectangle(b, e.Bounds);
                Color fc = sel ? SelFore : (e.Item.ForeColor == lv.ForeColor || e.Item.ForeColor == SystemColors.WindowText ? Fore : e.Item.ForeColor);
                var r = new Rectangle(e.Bounds.X + 4, e.Bounds.Y, e.Bounds.Width - 6, e.Bounds.Height);
                TextRenderer.DrawText(e.Graphics, e.SubItem.Text, e.Item.Font ?? lv.Font, r, fc, TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix | TextFormatFlags.SingleLine);
            };
        }

        class DarkColors : ProfessionalColorTable
        {
            public override Color ToolStripGradientBegin { get { return Surface; } }
            public override Color ToolStripGradientMiddle { get { return Surface; } }
            public override Color ToolStripGradientEnd { get { return Surface; } }
            public override Color ToolStripBorder { get { return Surface; } }
            public override Color ToolStripDropDownBackground { get { return Surface; } }
            public override Color ToolStripContentPanelGradientBegin { get { return Surface; } }
            public override Color ToolStripContentPanelGradientEnd { get { return Surface; } }
            public override Color MenuStripGradientBegin { get { return Surface; } }
            public override Color MenuStripGradientEnd { get { return Surface; } }
            public override Color MenuBorder { get { return Border; } }
            public override Color MenuItemBorder { get { return Hover; } }
            public override Color MenuItemSelected { get { return Hover; } }
            public override Color MenuItemSelectedGradientBegin { get { return Hover; } }
            public override Color MenuItemSelectedGradientEnd { get { return Hover; } }
            public override Color MenuItemPressedGradientBegin { get { return Hover; } }
            public override Color MenuItemPressedGradientEnd { get { return Hover; } }
            public override Color ImageMarginGradientBegin { get { return Surface; } }
            public override Color ImageMarginGradientMiddle { get { return Surface; } }
            public override Color ImageMarginGradientEnd { get { return Surface; } }
            public override Color SeparatorDark { get { return Border; } }
            public override Color SeparatorLight { get { return Border; } }
            public override Color ButtonSelectedHighlight { get { return Hover; } }
            public override Color ButtonSelectedBorder { get { return Hover; } }
            public override Color ButtonSelectedGradientBegin { get { return Hover; } }
            public override Color ButtonSelectedGradientMiddle { get { return Hover; } }
            public override Color ButtonSelectedGradientEnd { get { return Hover; } }
            public override Color ButtonPressedGradientBegin { get { return Border; } }
            public override Color ButtonPressedGradientMiddle { get { return Border; } }
            public override Color ButtonPressedGradientEnd { get { return Border; } }
            public override Color ButtonPressedBorder { get { return Border; } }
            public override Color StatusStripGradientBegin { get { return Surface; } }
            public override Color StatusStripGradientEnd { get { return Surface; } }
            public override Color CheckBackground { get { return Hover; } }
            public override Color CheckSelectedBackground { get { return Hover; } }
            public override Color CheckPressedBackground { get { return Hover; } }
        }
    }

    // 分頁標籤：深色模式時自己畫（系統畫的分頁列在深色底上是一條白）
    class ThemedTabControl : TabControl
    {
        [DllImport("gdi32.dll")] static extern bool DeleteObject(IntPtr h);
        IntPtr hfont;

        void PushFont()
        {
            if (!Theme.Dark || !IsHandleCreated) return;
            IntPtr old = hfont;
            hfont = Font.ToHfont();
            Theme.SendMessage(Handle, 0x30 /* WM_SETFONT */, hfont, (IntPtr)1);
            if (old != IntPtr.Zero) DeleteObject(old);
        }

        protected override void OnHandleCreated(EventArgs e) { base.OnHandleCreated(e); PushFont(); }
        protected override void OnFontChanged(EventArgs e) { base.OnFontChanged(e); PushFont(); }
        protected override void Dispose(bool disposing)
        {
            base.Dispose(disposing);
            if (hfont != IntPtr.Zero) { DeleteObject(hfont); hfont = IntPtr.Zero; }
        }

        public ThemedTabControl()
        {
            if (!Theme.Dark) return;
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            if (!Theme.Dark) { base.OnPaint(e); return; }
            Graphics g = e.Graphics;
            g.Clear(Theme.Back);
            for (int i = 0; i < TabCount; i++)
            {
                Rectangle r = GetTabRect(i);
                bool sel = i == SelectedIndex;
                using (var b = new SolidBrush(sel ? Theme.Surface : Theme.Back)) g.FillRectangle(b, r);
                if (sel) using (var b = new SolidBrush(Color.FromArgb(0x3B, 0x82, 0xF6))) g.FillRectangle(b, r.X, r.Bottom - 3, r.Width, 3);
                TextRenderer.DrawText(g, TabPages[i].Text, Font, r, sel ? Theme.Fore : Theme.SubFore,
                                      TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPrefix);
            }
            if (SelectedTab != null)
            {
                Rectangle page = SelectedTab.Bounds;
                using (var p = new Pen(Theme.Border)) g.DrawRectangle(p, page.X - 1, page.Y - 1, page.Width + 1, page.Height + 1);
            }
        }
    }
}
