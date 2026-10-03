// 口袋句庫 Pocketbrief：按快速鍵叫出常用句子清單，打代碼即可輸出到游標所在位置。
// Copyright (c) 2026 無名小律師（楊朝淵律師）. Licensed under the MIT License; see the LICENSE file.
// 編譯：執行同資料夾的「編譯.cmd」（只用 Windows 內建的 .NET Framework 4 編譯器，不必安裝任何東西）。
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Windows.Forms;
using Microsoft.Win32;

[assembly: System.Reflection.AssemblyTitle("口袋句庫 Pocketbrief")]
[assembly: System.Reflection.AssemblyProduct("口袋句庫 Pocketbrief")]
[assembly: System.Reflection.AssemblyDescription("常用句子範本：按快速鍵叫出清單，打代碼即可輸出")]
[assembly: System.Reflection.AssemblyCompany("無名小律師（楊朝淵律師）")]
[assembly: System.Reflection.AssemblyCopyright("Copyright © 2026 無名小律師（楊朝淵律師） · MIT License")]
[assembly: System.Reflection.AssemblyVersion("4.3.1.0")]
[assembly: System.Reflection.AssemblyFileVersion("4.3.1.0")]

namespace Pocketbrief
{
    class Template
    {
        public string Title = "";
        public string Code = "";
        public string Text = "";   // 換行一律存成 \n

        public string Key { get { return Code.ToLowerInvariant(); } }

        public string Preview
        {
            get
            {
                string p = Text.Replace("\n", " ⏎ ");
                return p.Length > 80 ? p.Substring(0, 80) + "…" : p;
            }
        }

        public string DisplayTitle
        {
            get
            {
                if (Title.Length > 0) return Title;
                string p = Text.Replace("\n", " ");
                return p.Length > 12 ? p.Substring(0, 12) + "…" : p;
            }
        }

        public bool SameAs(Template o)
        {
            return o != null && Title == o.Title && Code == o.Code && Text == o.Text;
        }

        public static string NormalizeNewlines(string s)
        {
            return (s ?? "").Replace("\r\n", "\n").Replace("\r", "\n");
        }

        // 所有匯入來源一律用同一套規則整理，確保「記憶體中的內容」和「存檔後再讀回的內容」一致。
        // 回傳因格式不合而被清掉代碼的筆數；內容空白的會被移除。
        public static int NormalizeAll(List<Template> list)
        {
            int cleared = 0;
            for (int i = list.Count - 1; i >= 0; i--)
            {
                Template t = list[i];
                t.Title = (t.Title ?? "").Trim();
                t.Code = (t.Code ?? "").Trim();
                t.Text = NormalizeNewlines(t.Text).Trim('\n');
                if (t.Code.Length > 0 && !Csv.IsValidCode(t.Code)) { t.Code = ""; cleared++; }
                if (t.Text.Trim().Length == 0) list.RemoveAt(i);
            }
            return cleared;
        }
    }

    static class Ui
    {
        // 明確的靜態建構式：讓下面的字型物件在「第一次用到 Ui」時才建立，
        // 不會在 Main 開頭、還沒宣告支援高 DPI 之前就先初始化 GDI+（否則縮放比例會被記成 100%，原生元件的字變小）
        static Ui() { }

        public static float S = 1f;   // DPI 縮放倍率
        public static int P(float px) { return (int)Math.Round(px * S); }
        // 依「視窗字體大小」等比例放大的尺寸（只放大不縮小）
        public static int PS(float px) { return P(px * Math.Max(1f, UiSize / DefaultUiSize)); }
        public const float DefaultUiSize = 10.5f;
        public static float UiSize = DefaultUiSize;   // 管理視窗、編輯視窗、設定視窗的字體大小
        public static Font Base = new Font("Microsoft JhengHei UI", DefaultUiSize);
        public static Font Small = new Font("Microsoft JhengHei UI", 9f);
        public const string Copyright = "著作權所有 © 2026 無名小律師（楊朝淵律師）· MIT 授權";

        public static float ContentSize = 12f;   // 句庫管理裡範本文字的字體大小（與視窗字體脫勾）
        public static Font Content = new Font("Microsoft JhengHei UI", 12f);
        public static void SetContentSize(float pt)
        {
            pt = Math.Max(8f, Math.Min(28f, pt));
            if (pt == ContentSize) return;
            ContentSize = pt;
            Content = new Font("Microsoft JhengHei UI", pt);
        }

        // 視窗大小不超過目前螢幕的可用範圍（留一點邊給標題列與工作列）
        public static Size FitScreen(int w, int h)
        {
            Rectangle wa = Screen.FromPoint(Cursor.Position).WorkingArea;
            return new Size(Math.Min(w, wa.Width - P(24)), Math.Min(h, wa.Height - P(56)));
        }

        public static void SetUiSize(float pt)
        {
            pt = Math.Max(8f, Math.Min(24f, pt));
            if (pt == UiSize) return;
            UiSize = pt;
            Base = new Font("Microsoft JhengHei UI", pt);
            Small = new Font("Microsoft JhengHei UI", Math.Max(8f, pt * 0.86f));
        }
        public static readonly Color Accent = Color.FromArgb(37, 99, 235);
        public const string AppName = "口袋句庫";
        public const string EnglishName = "Pocketbrief";
        public const string FullName = "口袋句庫 Pocketbrief";
    }

    static class Native
    {
        public const int WM_HOTKEY = 0x0312;
        public const uint MOD_ALT = 1, MOD_CONTROL = 2, MOD_SHIFT = 4, MOD_WIN = 8, MOD_NOREPEAT = 0x4000;

        [DllImport("user32.dll")] public static extern bool RegisterHotKey(IntPtr hWnd, int id, uint mods, uint vk);
        [DllImport("user32.dll")] public static extern bool UnregisterHotKey(IntPtr hWnd, int id);
        [DllImport("user32.dll")] public static extern IntPtr GetForegroundWindow();
        [DllImport("user32.dll")] public static extern bool SetForegroundWindow(IntPtr hWnd);
        [DllImport("user32.dll")] public static extern bool IsWindow(IntPtr hWnd);
        [DllImport("user32.dll")] public static extern uint GetWindowThreadProcessId(IntPtr hWnd, IntPtr pid);
        [DllImport("user32.dll")] public static extern bool GetGUIThreadInfo(uint tid, ref GUITHREADINFO info);
        [DllImport("user32.dll")] public static extern bool ClientToScreen(IntPtr hWnd, ref POINT p);
        [DllImport("user32.dll")] public static extern void keybd_event(byte vk, byte scan, uint flags, UIntPtr extra);
        [DllImport("user32.dll")] public static extern uint GetClipboardSequenceNumber();
        [DllImport("user32.dll")] public static extern bool SetProcessDPIAware();
        [DllImport("user32.dll")] public static extern bool DestroyIcon(IntPtr h);
        [DllImport("imm32.dll")] public static extern uint ImmGetVirtualKey(IntPtr hWnd);
        [DllImport("user32.dll")] public static extern short GetAsyncKeyState(int vk);

        // 低階鍵盤攔截（前導鍵組合，例如 `+1）
        public const int WH_KEYBOARD_LL = 13, WM_KEYDOWN = 0x100, WM_KEYUP = 0x101, WM_SYSKEYDOWN = 0x104, WM_SYSKEYUP = 0x105, WM_QUIT = 0x12;
        public const uint LLKHF_EXTENDED = 0x01, LLKHF_INJECTED = 0x10, KEYEVENTF_EXTENDEDKEY = 0x1, KEYEVENTF_KEYUP = 0x2;
        public delegate IntPtr LowLevelKeyboardProc(int nCode, IntPtr wParam, IntPtr lParam);
        [DllImport("user32.dll", SetLastError = true)] public static extern IntPtr SetWindowsHookEx(int idHook, LowLevelKeyboardProc fn, IntPtr hMod, uint threadId);
        [DllImport("user32.dll")] public static extern bool UnhookWindowsHookEx(IntPtr hook);
        [DllImport("user32.dll")] public static extern IntPtr CallNextHookEx(IntPtr hook, int nCode, IntPtr wParam, IntPtr lParam);
        [DllImport("kernel32.dll", CharSet = CharSet.Unicode)] public static extern IntPtr GetModuleHandle(string name);
        [DllImport("kernel32.dll")] public static extern uint GetCurrentThreadId();
        [DllImport("user32.dll")] public static extern int GetMessage(out MSG msg, IntPtr hWnd, uint min, uint max);
        [DllImport("user32.dll")] public static extern bool PeekMessage(out MSG msg, IntPtr hWnd, uint min, uint max, uint remove);
        [DllImport("user32.dll")] public static extern bool AttachThreadInput(uint idAttach, uint idAttachTo, bool attach);
        [DllImport("user32.dll")] public static extern bool BringWindowToTop(IntPtr hWnd);
        [DllImport("user32.dll")] public static extern bool PostThreadMessage(uint threadId, uint msg, IntPtr wParam, IntPtr lParam);
        [DllImport("user32.dll")] public static extern bool PostMessage(IntPtr hWnd, int msg, IntPtr wParam, IntPtr lParam);
        [StructLayout(LayoutKind.Sequential)] public struct KBDLLHOOKSTRUCT { public uint vkCode, scanCode, flags, time; public IntPtr dwExtraInfo; }
        [StructLayout(LayoutKind.Sequential)] public struct MSG { public IntPtr hwnd; public uint message; public IntPtr wParam, lParam; public uint time; public POINT pt; }

        public static bool ModifiersDown()
        {
            foreach (int vk in new[] { 0x10, 0x11, 0x12, 0x5B, 0x5C })   // Shift Ctrl Alt LWin RWin
                if ((GetAsyncKeyState(vk) & 0x8000) != 0) return true;
            return false;
        }

        [StructLayout(LayoutKind.Sequential)] public struct POINT { public int X, Y; }
        [StructLayout(LayoutKind.Sequential)] public struct RECT { public int Left, Top, Right, Bottom; }
        [StructLayout(LayoutKind.Sequential)]
        public struct GUITHREADINFO
        {
            public int cbSize, flags;
            public IntPtr hwndActive, hwndFocus, hwndCapture, hwndMenuOwner, hwndMoveSize, hwndCaret;
            public RECT rcCaret;
        }
    }

    // ───────────── 文字檔讀寫與 CSV ─────────────
    static class TextFile
    {
        // 自動判斷編碼：BOM → UTF-8 → Big5（舊版記事本或部分程式匯出的 CSV）
        public static string Read(string path)
        {
            byte[] b = null;
            for (int i = 0; ; i++)
            {
                try { b = File.ReadAllBytes(path); break; }
                catch (IOException) { if (i >= 5) throw; Thread.Sleep(150); } // 雲端硬碟同步中可能暫時鎖檔
            }
            if (b.Length >= 3 && b[0] == 0xEF && b[1] == 0xBB && b[2] == 0xBF) return Encoding.UTF8.GetString(b, 3, b.Length - 3);
            if (b.Length >= 2 && b[0] == 0xFF && b[1] == 0xFE) return Encoding.Unicode.GetString(b, 2, b.Length - 2);
            if (b.Length >= 2 && b[0] == 0xFE && b[1] == 0xFF) return Encoding.BigEndianUnicode.GetString(b, 2, b.Length - 2);
            try { return new UTF8Encoding(false, true).GetString(b); }
            catch (DecoderFallbackException) { }
            try { return Encoding.GetEncoding(950).GetString(b); }
            catch { return Encoding.Default.GetString(b); }
        }

        // 先寫到暫存檔，完整寫好後才一次替換正式檔：當機、斷電或雲端硬碟同時讀取時，
        // 都不會出現「寫到一半」的檔案
        public static void Write(string path, string content)
        {
            string tmp = path + ".saving";
            for (int i = 0; ; i++)
            {
                try
                {
                    File.WriteAllText(tmp, content, new UTF8Encoding(true));
                    if (!File.Exists(path)) File.Move(tmp, path);
                    else
                    {
                        try { File.Replace(tmp, path, null, true); }
                        catch (PlatformNotSupportedException) { File.Copy(tmp, path, true); File.Delete(tmp); }
                    }
                    return;
                }
                catch (IOException)
                {
                    if (i >= 5) { try { File.Delete(tmp); } catch { } throw; }
                    Thread.Sleep(150);
                }
            }
        }
    }

    static class Csv
    {
        // 欄位名稱與介面一致（名稱／代碼／內容），附英文方便其他語言的使用者用 Excel 開啟
        public static readonly string[] Header = { "名稱 Name", "代碼 Code", "內容 Content" };

        public static List<string[]> Parse(string s)
        {
            char delim = DetectDelimiter(s);
            var rows = new List<string[]>();
            var row = new List<string>();
            var field = new StringBuilder();
            bool inQ = false, any = false;
            for (int i = 0; i < s.Length; i++)
            {
                char c = s[i];
                if (inQ)
                {
                    if (c == '"')
                    {
                        if (i + 1 < s.Length && s[i + 1] == '"') { field.Append('"'); i++; }
                        else inQ = false;
                    }
                    else field.Append(c);
                    continue;
                }
                if (c == '"' && field.Length == 0) { inQ = true; any = true; }
                else if (c == delim) { row.Add(field.ToString()); field.Length = 0; any = true; }
                else if (c == '\r') { }
                else if (c == '\n') { EndRow(rows, row, field, any); row = new List<string>(); any = false; }
                else { field.Append(c); any = true; }
            }
            EndRow(rows, row, field, any);
            return rows;
        }

        static void EndRow(List<string[]> rows, List<string> row, StringBuilder field, bool any)
        {
            if (!any && row.Count == 0) return;
            row.Add(field.ToString());
            field.Length = 0;
            bool allEmpty = true;
            foreach (string f in row) if (f.Trim().Length > 0) { allEmpty = false; break; }
            if (!allEmpty) rows.Add(row.ToArray());
        }

        static char DetectDelimiter(string s)
        {
            int nl = s.IndexOf('\n');
            string first = nl < 0 ? s : s.Substring(0, nl);
            int commas = 0, tabs = 0;
            foreach (char c in first) { if (c == ',') commas++; else if (c == '\t') tabs++; }
            return tabs > commas ? '\t' : ',';
        }

        static string Esc(string f)
        {
            if (f.IndexOfAny(new[] { ',', '"', '\n', '\r' }) >= 0 || f != f.Trim())
                return "\"" + f.Replace("\"", "\"\"") + "\"";
            return f;
        }

        public static string Write(IEnumerable<Template> items)
        {
            var sb = new StringBuilder();
            sb.Append(string.Join(",", Header)).Append("\r\n");
            foreach (Template t in items)
                sb.Append(Esc(t.Title)).Append(',').Append(Esc(t.Code)).Append(',')
                  .Append(Esc(t.Text.Replace("\n", "\r\n"))).Append("\r\n");
            return sb.ToString();
        }

        // 把 CSV 轉成範本；有標題列就照欄名對應，沒有就依內容猜哪一欄是代碼、標題、內容
        public static List<Template> ToTemplates(List<string[]> rows)
        {
            var result = new List<Template>();
            if (rows.Count == 0) return result;
            int iTitle = -1, iCode = -1, iText = -1;
            string[] h = rows[0];
            for (int i = 0; i < h.Length; i++)
            {
                string n = h[i].Trim().Trim('﻿').ToLowerInvariant();
                if (iTitle < 0 && (n.Contains("標題") || n.Contains("title") || n.Contains("名稱"))) iTitle = i;
                else if (iCode < 0 && (n.Contains("代碼") || n.Contains("code") || n.Contains("key") || n.Contains("縮寫") || n.Contains("輸入碼") || n.Contains("代號"))) iCode = i;
                else if (iText < 0 && (n.Contains("內容") || n.Contains("text") || n.Contains("content") || n.Contains("範本"))) iText = i;
            }
            int start;
            if (iText >= 0 || iCode >= 0) start = 1;
            else
            {
                start = 0;
                int ncol = 0;
                foreach (string[] r in rows) ncol = Math.Max(ncol, r.Length);
                if (ncol == 1) iText = 0;
                else
                {
                    int lim = Math.Min(ncol, 3);
                    double bestScore = -1;
                    for (int c = 0; c < lim; c++)
                    {
                        double sc = CodeLikeScore(rows, c);
                        if (sc > bestScore) { bestScore = sc; iCode = c; }
                    }
                    if (bestScore < 0.6) iCode = -1;
                    var rest = new List<int>();
                    for (int c = 0; c < lim; c++) if (c != iCode) rest.Add(c);
                    if (rest.Count == 1) iText = rest[0];
                    else if (rest.Count >= 2)
                    {
                        bool firstLonger = AvgLen(rows, rest[0]) > AvgLen(rows, rest[1]);
                        iText = firstLonger ? rest[0] : rest[1];
                        iTitle = firstLonger ? rest[1] : rest[0];
                    }
                }
            }
            if (iText < 0) return result;
            for (int r = start; r < rows.Count; r++)
            {
                string[] row = rows[r];
                var t = new Template();
                t.Text = Template.NormalizeNewlines(Get(row, iText)).Trim('\n');
                t.Code = Get(row, iCode).Trim();
                t.Title = Get(row, iTitle).Trim();
                if (t.Text.Trim().Length > 0) result.Add(t);
            }
            return result;
        }

        static string Get(string[] row, int i) { return i >= 0 && i < row.Length ? row[i] : ""; }

        static double CodeLikeScore(List<string[]> rows, int c)
        {
            int ok = 0, n = 0;
            foreach (string[] r in rows)
            {
                string v = Get(r, c).Trim();
                if (v.Length == 0) continue;
                n++;
                if (IsValidCode(v)) ok++;
            }
            return n == 0 ? 0 : (double)ok / n;
        }

        static double AvgLen(List<string[]> rows, int c)
        {
            double sum = 0;
            foreach (string[] r in rows) sum += Get(r, c).Length;
            return rows.Count == 0 ? 0 : sum / rows.Count;
        }

        public static bool IsValidCode(string v)
        {
            if (v.Length == 0 || v.Length > 20) return false;
            foreach (char ch in v) if (ch <= ' ' || ch > '~' || ch == '`') return false;
            return true;
        }
    }

    // ───────────── 設定（設定.ini，與程式同資料夾；由「設定」視窗維護） ─────────────
    // 範本清單（叫出的框框）的外觀；顏色為 Color.Empty 時代表跟隨 Windows 系統配色
    class PopupStyle
    {
        public const string DefaultFont = "Microsoft JhengHei UI";
        public string FontName = DefaultFont;
        public float FontSize = 10.5f;
        public Color Back = Color.Empty;
        public Color Fore = Color.Empty;
        public Color CodeFore = Color.FromArgb(37, 99, 235);
        public Color SelBack = Color.FromArgb(37, 99, 235);
        public Color SelFore = Color.White;
        public Color HeaderBack = Color.FromArgb(37, 99, 235);
        public Color HeaderFore = Color.White;
        public int Width = 640;   // 以 100% 縮放計算的寬度
        public int Rows = 10;     // 一次顯示幾筆

        public Color BackR { get { return Back.IsEmpty ? (Theme.Dark ? Theme.InputBack : SystemColors.Window) : Back; } }
        public Color ForeR { get { return Fore.IsEmpty ? (Theme.Dark ? Theme.Fore : SystemColors.WindowText) : Fore; } }
        // 底色跟隨系統又是深色模式時，預設的藍色代碼在深灰底上太暗，改用亮一點的藍
        public Color CodeForeR
        {
            get { return Theme.Dark && Back.IsEmpty && CodeFore.ToArgb() == Color.FromArgb(37, 99, 235).ToArgb() ? Color.FromArgb(0x6C, 0xA0, 0xFF) : CodeFore; }
        }

        public PopupStyle Clone() { return (PopupStyle)MemberwiseClone(); }

        // 中文 Windows 列出的字型名稱是中文（標楷體），英文 Windows 則是英文（DFKai-SB）；
        // 兩種名稱都能直接建立字型，所以用「建得起來」來判斷是否已安裝
        static readonly Dictionary<string, bool> installed = new Dictionary<string, bool>(StringComparer.OrdinalIgnoreCase);
        public static bool FontExists(string name)
        {
            bool ok;
            if (installed.TryGetValue(name, out ok)) return ok;
            try { using (new FontFamily(name)) ok = true; } catch { ok = false; }
            installed[name] = ok;
            return ok;
        }

        // 字型在這台電腦沒有安裝時（例如雲端同步到另一台電腦），改用預設字型
        public Font MakeFont(FontStyle fs)
        {
            string name = FontExists(FontName) ? FontName : DefaultFont;
            float size = float.IsNaN(FontSize) || float.IsInfinity(FontSize) ? 10.5f : Math.Max(6f, Math.Min(48f, FontSize));
            try { return new Font(name, size, fs); }
            catch
            {
                // 有些字型沒有粗體（或沒有一般）樣式，建不起來就改用一般樣式，再不行用預設字型
                try { return new Font(name, size, FontStyle.Regular); }
                catch { return new Font(DefaultFont, size, fs); }
            }
        }

        public static string ColorToString(Color c) { return c.IsEmpty ? "" : string.Format("#{0:X2}{1:X2}{2:X2}", c.R, c.G, c.B); }
        public static Color ParseColor(string s, Color fallback)
        {
            s = s.Trim();
            if (s.Length == 0) return Color.Empty;
            try { return ColorTranslator.FromHtml(s); } catch { return fallback; }
        }

        // 兩色混合（預覽區底色用）
        public static Color Blend(Color a, Color b, float t)
        {
            return Color.FromArgb((int)(a.R + (b.R - a.R) * t), (int)(a.G + (b.G - a.G) * t), (int)(a.B + (b.B - a.B) * t));
        }
    }

    // 內建配色主題（只換顏色，不動字型與大小）
    class ThemePreset
    {
        public string Name;   // 以繁體中文命名，顯示時經 L.T 翻譯
        public bool EyeCare;  // 護眼配色（游標移上去會顯示說明）
        public Color Back, Fore, Code, SelBack, SelFore, HeadBack, HeadFore;

        static Color H(string s) { return s.Length == 0 ? Color.Empty : ColorTranslator.FromHtml(s); }
        static ThemePreset P(string name, string back, string fore, string code, string selBack, string selFore, string headBack, string headFore)
        {
            return new ThemePreset { Name = name, Back = H(back), Fore = H(fore), Code = H(code), SelBack = H(selBack), SelFore = H(selFore), HeadBack = H(headBack), HeadFore = H(headFore) };
        }

        public static readonly ThemePreset[] All =
        {
            P("經典藍",        "",        "",        "#2563EB", "#2563EB", "#FFFFFF", "#2563EB", "#FFFFFF"),
            P("深色",          "#1E1E1E", "#D4D4D4", "#4FC1FF", "#264F78", "#FFFFFF", "#333333", "#FFFFFF"),
            // 護眼系：低對比、少藍光，底色不用純白、文字不用純黑
            Eye(P("豆沙綠",    "#C7EDCC", "#2F3E30", "#3B7A45", "#5E8C61", "#FFFFFF", "#4E7351", "#F0FFF0")),
            Eye(P("暖米紙",    "#F4ECD8", "#5B4636", "#9C6B30", "#A67C52", "#FFFDF5", "#704F38", "#F9F1E1")),
            P("森林綠",        "#F3F8F3", "#1F2D1F", "#2E7D32", "#2E7D32", "#FFFFFF", "#1B5E20", "#E8F5E9"),
            P("摩卡棕",        "#FFF8E7", "#3B2F2F", "#B03A2E", "#8B5E3C", "#FFFFFF", "#5B3A29", "#FFE9C7"),
            P("朱紅",          "#FFFDF8", "#2B2B2B", "#C0392B", "#C0392B", "#FFFFFF", "#8E1F14", "#FFF3E0"),
            P("紫藤",          "#FAF7FD", "#2E2440", "#7E57C2", "#7E57C2", "#FFFFFF", "#4A3780", "#F3E5F5"),
            P("Solarized",      "#FDF6E3", "#586E75", "#268BD2", "#268BD2", "#FDF6E3", "#073642", "#EEE8D5"),
            P("高對比",        "#000000", "#FFFFFF", "#FFFF00", "#FFFF00", "#000000", "#1A1A1A", "#FFFF00"),
        };

        static ThemePreset Eye(ThemePreset p) { p.EyeCare = true; return p; }

        public void ApplyTo(PopupStyle st)
        {
            st.Back = Back; st.Fore = Fore; st.CodeFore = Code; st.SelBack = SelBack; st.SelFore = SelFore;
            st.HeaderBack = HeadBack; st.HeaderFore = HeadFore;
        }

        public bool Matches(PopupStyle st)
        {
            return st.Back.ToArgb() == Back.ToArgb() && st.Back.IsEmpty == Back.IsEmpty && st.Fore.ToArgb() == Fore.ToArgb() && st.Fore.IsEmpty == Fore.IsEmpty
                && st.CodeFore.ToArgb() == Code.ToArgb() && st.SelBack.ToArgb() == SelBack.ToArgb() && st.SelFore.ToArgb() == SelFore.ToArgb()
                && st.HeaderBack.ToArgb() == HeadBack.ToArgb() && st.HeaderFore.ToArgb() == HeadFore.ToArgb();
        }
    }

    // 設定視窗裡的配色主題縮圖（畫出迷你版的範本清單）
    class ThemeSwatch : Control
    {
        public readonly ThemePreset Preset;
        public bool Current;

        public ThemeSwatch(ThemePreset p)
        {
            Preset = p;
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.UserPaint | ControlStyles.ResizeRedraw
                     | ControlStyles.SupportsTransparentBackColor, true);
            BackColor = Color.Transparent;
            Size = new Size(Ui.P(92), Ui.P(52) + TextRenderer.MeasureText("Ag範", Ui.Small).Height + Ui.P(2));
            Margin = new Padding(Ui.P(3));
            Cursor = Cursors.Hand;
            MouseEnter += delegate { Invalidate(); };
            MouseLeave += delegate { Invalidate(); };
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;   // 背景由上層元件畫（透明背景），這裡只畫縮圖本身
            var st = new PopupStyle();
            Preset.ApplyTo(st);
            int labelH = TextRenderer.MeasureText("Ag範", Ui.Small).Height + Ui.P(2);
            var box = new Rectangle(Ui.P(3), Ui.P(3), Width - Ui.P(6), Height - labelH - Ui.P(6));
            using (var b = new SolidBrush(st.BackR)) g.FillRectangle(b, box);
            int hh = Math.Max(3, box.Height / 4);
            using (var b = new SolidBrush(st.HeaderBack)) g.FillRectangle(b, box.X, box.Y, box.Width, hh);
            int rowH = Math.Max(3, (box.Height - hh) / 3);
            using (var b = new SolidBrush(st.SelBack)) g.FillRectangle(b, box.X, box.Y + hh, box.Width, rowH);
            // 每列：代碼（短）＋內容（長）的示意線
            for (int i = 0; i < 3; i++)
            {
                int y = box.Y + hh + i * rowH + rowH / 2 - 1;
                Color code = i == 0 ? st.SelFore : st.CodeForeR;
                Color text = i == 0 ? st.SelFore : st.ForeR;
                using (var p = new Pen(code, 2)) g.DrawLine(p, box.X + Ui.P(4), y, box.X + Ui.P(12), y);
                using (var p = new Pen(text, 2)) g.DrawLine(p, box.X + Ui.P(18), y, box.Right - Ui.P(8) - i * Ui.P(6), y);
            }
            using (var p = new Pen(st.HeaderFore, 2)) g.DrawLine(p, box.X + Ui.P(4), box.Y + hh / 2, box.X + box.Width / 2, box.Y + hh / 2);
            bool hot = ClientRectangle.Contains(PointToClient(Cursor.Position));
            Color border = Current ? SystemColors.Highlight : hot ? (Theme.Dark ? Theme.Fore : SystemColors.ControlDarkDark) : (Theme.Dark ? Theme.Border : SystemColors.ControlDark);
            using (var p = new Pen(border, Current ? 3 : 1)) g.DrawRectangle(p, box.X - 1, box.Y - 1, box.Width + 1, box.Height + 1);
            TextRenderer.DrawText(g, L.T(Preset.Name), Ui.Small, new Rectangle(0, Height - labelH, Width, labelH), ForeColor,
                                  TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix);
        }
    }

    // 範本清單的繪製：實際的清單與設定視窗的預覽共用同一套畫法
    static class PopupRenderer
    {
        const TextFormatFlags Flags = TextFormatFlags.NoPrefix | TextFormatFlags.EndEllipsis | TextFormatFlags.SingleLine | TextFormatFlags.VerticalCenter;

        public static int CodeWidth(Font f) { return TextRenderer.MeasureText("00000", f).Width + Ui.P(10); }
        public static int TitleWidth(int total) { return (int)(total * 0.26); }
        public static int RowHeight(Font f) { return TextRenderer.MeasureText("範Ag", f).Height + Ui.P(8); }

        public static void DrawRow(Graphics g, Rectangle r, string code, string title, string text, bool selected, bool dim, PopupStyle st, Font f)
        {
            Color back = selected ? st.SelBack : st.BackR;
            Color fore = selected ? st.SelFore : st.ForeR;
            Color codeFore = selected ? st.SelFore : st.CodeForeR;
            if (dim) fore = codeFore = PopupStyle.Blend(st.BackR, st.ForeR, 0.5f);
            using (var b = new SolidBrush(back)) g.FillRectangle(b, r);
            int pad = Ui.P(8);
            if (dim)   // 提示訊息：橫跨整列
            {
                TextRenderer.DrawText(g, title, f, new Rectangle(r.X + pad, r.Y, r.Width - pad * 2, r.Height), fore, Flags);
                return;
            }
            int cw = CodeWidth(f), tw = TitleWidth(r.Width);
            if (string.IsNullOrEmpty(title)) tw = 0;   // 沒有標題時，內容直接從標題欄開始
            var rc = new Rectangle(r.X + pad, r.Y, cw - pad, r.Height);
            var rt = new Rectangle(r.X + cw, r.Y, tw - Ui.P(6), r.Height);
            var rx = new Rectangle(r.X + cw + tw, r.Y, r.Width - cw - tw - pad, r.Height);
            TextRenderer.DrawText(g, code, f, rc, codeFore, Flags);
            if (tw > 0) TextRenderer.DrawText(g, title, f, rt, fore, Flags);
            TextRenderer.DrawText(g, text, f, rx, fore, Flags);
        }
    }

    class Settings
    {
        public string Hotkey = "Ctrl+`";
        public string QuickAddHotkey = "Ctrl+Shift+0";   // 空字串＝停用
        public string DataFile = "範本.csv";
        public bool RestoreClipboard = true;
        public bool AutoCommit = true;
        public float UiFontSize = Ui.DefaultUiSize;   // 管理視窗等的字體大小
        public string Language = "auto";              // 介面語言：auto／zh-TW／en／ja／zh-CN
        public string AppTheme = "auto";              // 外觀模式：auto（跟隨 Windows）／light／dark
        public float ContentFontSize = 12f;           // 句庫管理裡範本文字（清單與編輯區）的字體大小
        public PopupStyle Style = new PopupStyle();

        public Settings Clone()
        {
            var s = (Settings)MemberwiseClone();
            s.Style = Style.Clone();
            return s;
        }

        public static string BaseDir { get { return AppDomain.CurrentDomain.BaseDirectory; } }
        public static string IniPath { get { return Path.Combine(BaseDir, "設定.ini"); } }
        public string DataPath { get { return Path.IsPathRooted(DataFile) ? DataFile : Path.Combine(BaseDir, DataFile); } }

        static bool TryFloat(string v, out float f)
        {
            return float.TryParse(v, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out f)
                   && !float.IsNaN(f) && !float.IsInfinity(f);
        }

        // 設定檔不存在：回傳預設值（不自動寫檔，以免雲端同步換檔的空窗把大家的設定蓋掉）。
        // 設定檔存在但讀不到或內容殘缺：回傳 null，呼叫端應沿用目前的設定，也不可存檔。
        public static Settings Load()
        {
            var s = new Settings();
            try
            {
                if (!File.Exists(IniPath)) return s;
                string text;
                try { text = TextFile.Read(IniPath); } catch { return null; }
                if (text.IndexOf("hotkey=", StringComparison.OrdinalIgnoreCase) < 0) return null;
                foreach (string raw in text.Replace("\r\n", "\n").Split('\n'))
                {
                    string line = raw.Trim();
                    if (line.Length == 0 || line[0] == ';' || line[0] == '#') continue;
                    int eq = line.IndexOf('=');
                    if (eq <= 0) continue;
                    string k = line.Substring(0, eq).Trim().ToLowerInvariant();
                    string v = line.Substring(eq + 1).Trim();
                    PopupStyle st = s.Style;
                    int n; float fl;
                    if (k == "hotkey" && v.Length > 0) s.Hotkey = v;
                    else if (k == "quickadd_hotkey") s.QuickAddHotkey = v;
                    else if (k == "datafile" && v.Length > 0) s.DataFile = v;
                    else if (k == "restore_clipboard") s.RestoreClipboard = v != "0";
                    else if (k == "auto_commit") s.AutoCommit = v != "0";
                    else if (k == "language" && v.Length > 0) s.Language = v;
                    else if (k == "app_theme" && v.Length > 0) s.AppTheme = v;
                    else if (k == "content_font_size" && TryFloat(v, out fl)) s.ContentFontSize = Math.Max(8f, Math.Min(28f, fl));
                    else if (k == "ui_font_size" && TryFloat(v, out fl)) s.UiFontSize = Math.Max(8f, Math.Min(24f, fl));
                    else if (k == "popup_font" && v.Length > 0) st.FontName = v;
                    else if (k == "popup_font_size" && TryFloat(v, out fl)) st.FontSize = Math.Max(7f, Math.Min(36f, fl));
                    else if (k == "popup_back") st.Back = PopupStyle.ParseColor(v, st.Back);
                    else if (k == "popup_fore") st.Fore = PopupStyle.ParseColor(v, st.Fore);
                    else if (k == "popup_code_fore") st.CodeFore = PopupStyle.ParseColor(v, st.CodeFore);
                    else if (k == "popup_sel_back") st.SelBack = PopupStyle.ParseColor(v, st.SelBack);
                    else if (k == "popup_sel_fore") st.SelFore = PopupStyle.ParseColor(v, st.SelFore);
                    else if (k == "popup_header_back") st.HeaderBack = PopupStyle.ParseColor(v, st.HeaderBack);
                    else if (k == "popup_header_fore") st.HeaderFore = PopupStyle.ParseColor(v, st.HeaderFore);
                    else if (k == "popup_width" && int.TryParse(v, out n)) st.Width = Math.Max(360, Math.Min(1600, n));
                    else if (k == "popup_rows" && int.TryParse(v, out n)) st.Rows = Math.Max(3, Math.Min(30, n));
                }
                // 標題列等顏色不允許「跟隨系統」（空值），補回預設
                var def = new PopupStyle();
                if (s.Style.CodeFore.IsEmpty) s.Style.CodeFore = def.CodeFore;
                if (s.Style.SelBack.IsEmpty) s.Style.SelBack = def.SelBack;
                if (s.Style.SelFore.IsEmpty) s.Style.SelFore = def.SelFore;
                if (s.Style.HeaderBack.IsEmpty) s.Style.HeaderBack = def.HeaderBack;
                if (s.Style.HeaderFore.IsEmpty) s.Style.HeaderFore = def.HeaderFore;
                // 1.0 版用的是 範本.txt，改用同名的 .csv（舊檔內容會自動轉入；下次存設定時一併更新）
                if (s.DataFile.EndsWith(".txt", StringComparison.OrdinalIgnoreCase))
                    s.DataFile = Path.ChangeExtension(s.DataFile, ".csv");
            }
            catch { return null; }
            return s;
        }

        public void Save()
        {
            PopupStyle st = Style;
            string ini =
                "; " + L.T("口袋句庫 Pocketbrief 設定檔（請在系統匣圖示按右鍵 →「設定」修改，不必手動編輯本檔）") + "\r\n" +
                "hotkey=" + Hotkey + "\r\n" +
                "quickadd_hotkey=" + QuickAddHotkey + "\r\n" +
                "datafile=" + DataFile + "\r\n" +
                "auto_commit=" + (AutoCommit ? "1" : "0") + "\r\n" +
                "restore_clipboard=" + (RestoreClipboard ? "1" : "0") + "\r\n" +
                "ui_font_size=" + UiFontSize.ToString(System.Globalization.CultureInfo.InvariantCulture) + "\r\n" +
                "language=" + Language + "\r\n" +
                "app_theme=" + AppTheme + "\r\n" +
                "content_font_size=" + ContentFontSize.ToString(System.Globalization.CultureInfo.InvariantCulture) + "\r\n" +
                "\r\n; " + L.T("範本清單外觀（顏色空白＝跟隨 Windows 系統配色）") + "\r\n" +
                "popup_font=" + st.FontName + "\r\n" +
                "popup_font_size=" + st.FontSize.ToString(System.Globalization.CultureInfo.InvariantCulture) + "\r\n" +
                "popup_back=" + PopupStyle.ColorToString(st.Back) + "\r\n" +
                "popup_fore=" + PopupStyle.ColorToString(st.Fore) + "\r\n" +
                "popup_code_fore=" + PopupStyle.ColorToString(st.CodeFore) + "\r\n" +
                "popup_sel_back=" + PopupStyle.ColorToString(st.SelBack) + "\r\n" +
                "popup_sel_fore=" + PopupStyle.ColorToString(st.SelFore) + "\r\n" +
                "popup_header_back=" + PopupStyle.ColorToString(st.HeaderBack) + "\r\n" +
                "popup_header_fore=" + PopupStyle.ColorToString(st.HeaderFore) + "\r\n" +
                "popup_width=" + st.Width + "\r\n" +
                "popup_rows=" + st.Rows + "\r\n";
            TextFile.Write(IniPath, ini);
        }

        public static bool ParseHotkey(string s, out uint mods, out uint vk)
        {
            mods = Native.MOD_NOREPEAT; vk = 0;
            string keyPart, modPart;
            if (s.EndsWith("++")) { keyPart = "+"; modPart = s.Substring(0, s.Length - 2); }
            else
            {
                int last = s.LastIndexOf('+');
                keyPart = last < 0 ? s : s.Substring(last + 1);
                modPart = last < 0 ? "" : s.Substring(0, last);
            }
            foreach (string p0 in modPart.Split(new[] { '+' }, StringSplitOptions.RemoveEmptyEntries))
            {
                string p = p0.Trim().ToLowerInvariant();
                if (p == "ctrl" || p == "control") mods |= Native.MOD_CONTROL;
                else if (p == "alt") mods |= Native.MOD_ALT;
                else if (p == "shift") mods |= Native.MOD_SHIFT;
                else if (p == "win" || p == "windows") mods |= Native.MOD_WIN;
                else return false;
            }
            string k = keyPart.Trim();
            string kl = k.ToLowerInvariant();
            if (k == "`" || k == "~") vk = 0xC0;
            else if (k == ";") vk = 0xBA;
            else if (k == "'") vk = 0xDE;
            else if (k == ",") vk = 0xBC;
            else if (k == ".") vk = 0xBE;
            else if (k == "/") vk = 0xBF;
            else if (k == "\\") vk = 0xDC;
            else if (k == "[") vk = 0xDB;
            else if (k == "]") vk = 0xDD;
            else if (k == "-") vk = 0xBD;
            else if (k == "=" || k == "+") vk = 0xBB;
            else if (k.Length == 1 && char.IsLetterOrDigit(k[0]) && k[0] < 128) vk = char.ToUpperInvariant(k[0]);
            else if (kl == "space") vk = 0x20;
            else if (kl.Length >= 2 && kl[0] == 'f')
            {
                int n;
                if (int.TryParse(kl.Substring(1), out n) && n >= 1 && n <= 24) vk = (uint)(0x70 + n - 1);
            }
            else
            {
                try { vk = (uint)(Keys)Enum.Parse(typeof(Keys), k, true); } catch { }
            }
            return vk != 0;
        }

        // 「前導鍵＋按鍵」組合（例如 `+1：按住 ` 再按 1）。RegisterHotKey 只接受 Ctrl/Alt/Shift/Win 當修飾鍵，
        // 這種組合改由 KeyChordHook 攔截鍵盤實作。目前只開放 ` 當前導鍵
        public static bool ParseChord(string s, out uint prefix, out uint vk)
        {
            prefix = 0; vk = 0;
            if (s == null) return false;
            int i = s.IndexOf('+');
            if (i <= 0 || i == s.Length - 1) return false;
            string p = s.Substring(0, i).Trim();
            if (p != "`" && p != "~") return false;
            uint mods;
            if (!ParseHotkey(s.Substring(i + 1).Trim(), out mods, out vk)) return false;
            // 第二個鍵不能再帶修飾鍵、不能是 ` 本身，也不能是 Backspace／Tab／Enter／Esc 這類操作鍵
            if (mods != Native.MOD_NOREPEAT || vk == 0xC0 || vk == 0x08 || vk == 0x09 || vk == 0x0D || vk == 0x1B) { vk = 0; return false; }
            prefix = 0xC0;
            return true;
        }

        public static bool IsValidHotkey(string s)
        {
            uint a, b;
            return ParseHotkey(s, out a, out b) || ParseChord(s, out a, out b);
        }

        // 按住 ` 時按下的鍵 → 「`+鍵」設定字串；不合用時回傳 null
        public static string FromChordKey(Keys keyData)
        {
            if ((keyData & Keys.Modifiers) != 0) return null;
            string name = KeyName(keyData & Keys.KeyCode);
            if (name == null) return null;
            string result = "`+" + name;
            uint p, v;
            return ParseChord(result, out p, out v) ? result : null;
        }

        static string KeyName(Keys k)
        {
            if (k == Keys.ControlKey || k == Keys.ShiftKey || k == Keys.Menu || k == Keys.LWin || k == Keys.RWin || k == Keys.None) return null;
            string name = null;
            if (k == Keys.Oemtilde) name = "`";
            else if (k == Keys.OemSemicolon) name = ";";
            else if (k == Keys.OemQuotes) name = "'";
            else if (k == Keys.Oemcomma) name = ",";
            else if (k == Keys.OemPeriod) name = ".";
            else if (k == Keys.OemQuestion) name = "/";
            else if (k == Keys.OemPipe || k == Keys.OemBackslash) name = "\\";
            else if (k == Keys.OemOpenBrackets) name = "[";
            else if (k == Keys.OemCloseBrackets) name = "]";
            else if (k == Keys.OemMinus) name = "-";
            else if (k == Keys.Oemplus) name = "=";
            else if (k >= Keys.D0 && k <= Keys.D9) name = ((char)('0' + (k - Keys.D0))).ToString();
            else if (k >= Keys.A && k <= Keys.Z) name = ((char)('A' + (k - Keys.A))).ToString();
            else if (k >= Keys.F1 && k <= Keys.F24) name = "F" + (1 + (k - Keys.F1));
            else if (k == Keys.Space) name = "Space";
            else name = k.ToString();
            return name;
        }

        // 把按下的組合鍵轉成設定字串（設定視窗錄製快速鍵用）；不合用時回傳 null
        public static string FromKeys(Keys keyData)
        {
            Keys k = keyData & Keys.KeyCode;
            string name = KeyName(k);
            if (name == null) return null;
            bool fkey = k >= Keys.F1 && k <= Keys.F24;
            var sb = new StringBuilder();
            if ((keyData & Keys.Control) != 0) sb.Append("Ctrl+");
            if ((keyData & Keys.Alt) != 0) sb.Append("Alt+");
            if ((keyData & Keys.Shift) != 0) sb.Append("Shift+");
            if (sb.Length == 0 && !fkey) return null;   // 沒有 Ctrl/Alt 的一般按鍵會跟打字衝突
            if (sb.ToString() == "Shift+" && !fkey) return null;
            string result = sb.Append(name).ToString();
            // 這些是全系統通用的操作（複製、貼上、復原、關閉視窗…），被攔截會讓電腦很難用，
            // 而且本程式自己會送出 Ctrl+C／Ctrl+V，設成這些會自我觸發
            foreach (string bad in new[] { "Ctrl+C", "Ctrl+V", "Ctrl+X", "Ctrl+Z", "Ctrl+Y", "Ctrl+A", "Ctrl+S", "Ctrl+F", "Ctrl+P", "Ctrl+N", "Ctrl+W", "Alt+F4", "Alt+Tab", "Ctrl+Alt+Delete" })
                if (string.Equals(result, bad, StringComparison.OrdinalIgnoreCase)) return null;
            return result;
        }
    }

    static class AutoStart
    {
        const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
        const string RunName = "口袋句庫 Pocketbrief";
        const string LegacyRunName = "文字範本";   // 改名前的開機啟動項目

        public static bool IsOn()
        {
            try
            {
                using (RegistryKey k = Registry.CurrentUser.OpenSubKey(RunKey))
                {
                    string v = k == null ? null : k.GetValue(RunName) as string;
                    return v != null && v.IndexOf(Application.ExecutablePath, StringComparison.OrdinalIgnoreCase) >= 0;
                }
            }
            catch { return false; }
        }

        public static void Set(bool on)
        {
            using (RegistryKey k = Registry.CurrentUser.CreateSubKey(RunKey))
            {
                if (on) k.SetValue(RunName, "\"" + Application.ExecutablePath + "\"");
                else k.DeleteValue(RunName, false);
            }
        }

        // 改名前若已設定開機啟動，換成新名稱、指向新程式
        public static void MigrateLegacy()
        {
            try
            {
                using (RegistryKey k = Registry.CurrentUser.OpenSubKey(RunKey, true))
                {
                    if (k == null || k.GetValue(LegacyRunName) == null) return;
                    k.DeleteValue(LegacyRunName, false);
                    k.SetValue(RunName, "\"" + Application.ExecutablePath + "\"");
                }
            }
            catch { }
        }
    }

    // ───────────── 範本資料（範本.csv） ─────────────
    class Store
    {
        public List<Template> Items = new List<Template>();
        public int Version;   // Items 每次被換新（重讀或存檔）就加一
        public string Error;
        string loadedPath;
        DateTime loadedTime;
        long loadedLen = -1;
        bool everLoaded;   // 這次執行是否成功讀過範本檔

        // 回傳 true 表示畫面需要重新整理（內容重新讀過，或錯誤狀態改變）。
        // 讀取有問題時一律「沿用上次讀到的內容＋設定 Error」，絕不自動寫檔，以免把雲端上的資料蓋掉。
        public bool EnsureLoaded(string path)
        {
            string prevError = Error;
            try
            {
                if (!File.Exists(path))
                {
                    // 讀過卻不見了，多半是雲端硬碟換檔的空窗；資料夾不存在則可能是路徑設錯。兩種都不能建空檔。
                    string dir = Path.GetDirectoryName(path);
                    if (everLoaded || string.IsNullOrEmpty(dir) || !Directory.Exists(dir))
                    {
                        Error = L.T("找不到範本檔（可能正在同步），暫時沿用上次讀到的內容。") + "\n" + path;
                        return Error != prevError;
                    }
                    CreateInitial(path);
                }
                for (int attempt = 0; attempt < 3; attempt++)
                {
                    var before = new FileInfo(path);
                    if (path == loadedPath && before.LastWriteTimeUtc == loadedTime && before.Length == loadedLen && Error == null) return false;
                    string text = TextFile.Read(path);
                    var after = new FileInfo(path);
                    if (after.LastWriteTimeUtc != before.LastWriteTimeUtc || after.Length != before.Length) { Thread.Sleep(200); continue; }  // 讀的時候檔案被換掉了
                    string problem = Validate(text);
                    if (problem != null)
                    {
                        Error = problem;
                        return Error != prevError;
                    }
                    Items = Csv.ToTemplates(Csv.Parse(text));
                    Version++;
                    loadedPath = path; loadedTime = before.LastWriteTimeUtc; loadedLen = before.Length;
                    everLoaded = true;
                    Error = null;
                    return true;
                }
                Error = L.T("範本檔一直在變動（可能正在同步），暫時沿用上次讀到的內容。");
                return Error != prevError;
            }
            catch (Exception ex)
            {
                Error = L.T("讀不到範本檔：") + ex.Message;
                return Error != prevError;
            }
        }

        // 本程式寫出的範本檔一定有標題列、以換行結尾；不符合就可能是空檔或只寫了一半
        static string Validate(string text)
        {
            if (text.Length == 0) return L.T("範本檔是空的（可能正在同步），暫時沿用上次讀到的內容。");
            if (!text.EndsWith("\n")) return L.T("範本檔不完整（可能正在同步），暫時沿用上次讀到的內容。");
            int nl = text.IndexOf('\n');
            string first = text.Substring(0, nl);
            // 認得現行的「代碼 Code」，也認得舊版的「輸入代碼」
            if (first.IndexOf("代碼") < 0 && first.IndexOf("code", StringComparison.OrdinalIgnoreCase) < 0) return L.T("範本檔格式不正確（缺少欄位名稱那一列），暫時沿用上次讀到的內容。");
            return null;
        }

        bool FileChangedSinceLoad(string path)
        {
            try
            {
                var fi = new FileInfo(path);
                return !fi.Exists || fi.LastWriteTimeUtc != loadedTime || fi.Length != loadedLen;
            }
            catch { return true; }
        }

        // 每次增刪改都「先讀最新檔 → 套用變更 → 立刻存檔」；存檔前若發現檔案又被別台電腦改了，就重讀後再套用一次。
        // rescueText：存檔失敗時先放進剪貼簿，避免使用者剛打的內容消失。
        public bool Mutate(string path, Action<List<Template>> change, string rescueText = null)
        {
            for (int attempt = 0; attempt < 3; attempt++)
            {
                EnsureLoaded(path);
                if (Error != null) { Fail(Error + "\n\n" + L.T("為避免覆蓋掉資料，這次的修改沒有存檔。"), rescueText); return false; }
                var list = new List<Template>(Items);
                change(list);
                if (FileChangedSinceLoad(path)) { Thread.Sleep(200); continue; }
                try
                {
                    DailyBackup(path);
                    TextFile.Write(path, Csv.Write(list));
                    Items = list;
                    Version++;
                    var fi = new FileInfo(path);
                    loadedPath = path; loadedTime = fi.LastWriteTimeUtc; loadedLen = fi.Length;
                    return true;
                }
                catch (Exception ex)
                {
                    Fail(L.T("存檔失敗：") + ex.Message, rescueText);
                    return false;
                }
            }
            Fail(L.T("範本檔一直在變動（可能正在同步），這次的修改沒有存檔，請稍後再試。"), rescueText);
            return false;
        }

        static void Fail(string msg, string rescueText)
        {
            if (!string.IsNullOrEmpty(rescueText))
            {
                try
                {
                    Clipboard.SetDataObject(rescueText.Replace("\n", "\r\n"), true, 5, 50);
                    msg += "\n\n" + L.T("你剛輸入的內容已先複製到剪貼簿，可以稍後再貼回去。");
                }
                catch { }
            }
            MessageBox.Show(msg, L.AppName, MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }

        // 每天第一次修改前，把當時的範本檔複製一份到「備份」資料夾，保留最近 14 份
        static void DailyBackup(string path)
        {
            try
            {
                var fi = new FileInfo(path);
                if (!fi.Exists || fi.Length == 0) return;
                string dir = Path.Combine(fi.DirectoryName, "備份");
                Directory.CreateDirectory(dir);
                string name = Path.GetFileNameWithoutExtension(path) + "_" + DateTime.Now.ToString("yyyyMMdd") + fi.Extension;
                string target = Path.Combine(dir, name);
                if (File.Exists(target)) return;
                File.Copy(path, target);
                var old = new List<string>(Directory.GetFiles(dir, Path.GetFileNameWithoutExtension(path) + "_*" + fi.Extension));
                old.Sort(StringComparer.Ordinal);
                for (int i = 0; i < old.Count - 14; i++) try { File.Delete(old[i]); } catch { }
            }
            catch { }   // 備份失敗不影響正常存檔
        }

        static void CreateInitial(string path)
        {
            var list = new List<Template>();
            string legacy = Path.ChangeExtension(path, ".txt");
            if (File.Exists(legacy))
            {
                list = ParseLegacyTxt(TextFile.Read(legacy));
                try { File.Move(legacy, legacy + ".已轉入csv.bak"); } catch { }
            }
            TextFile.Write(path, Csv.Write(list));
        }

        // 由左到右一次掃描，\\n 才會正確還原成「反斜線＋n」
        static string UnescapeLegacy(string s)
        {
            var sb = new StringBuilder(s.Length);
            for (int i = 0; i < s.Length; i++)
            {
                if (s[i] == '\\' && i + 1 < s.Length)
                {
                    char n = s[i + 1];
                    if (n == 'n') { sb.Append('\n'); i++; continue; }
                    if (n == 't') { sb.Append('\t'); i++; continue; }
                    if (n == '\\') { sb.Append('\\'); i++; continue; }
                }
                sb.Append(s[i]);
            }
            return sb.ToString();
        }

        // 1.0 版的 [代碼] 內容 格式
        static List<Template> ParseLegacyTxt(string s)
        {
            var rx = new Regex(@"^\s*[\[【]\s*([A-Za-z0-9]{1,10})\s*[\]】]\s*(.*)$");
            var list = new List<Template>();
            foreach (string raw in s.Replace("\r\n", "\n").Split('\n'))
            {
                string line = raw.TrimEnd();
                if (line.Trim().Length == 0 || line.TrimStart().StartsWith("#")) continue;
                var t = new Template();
                Match m = rx.Match(line);
                if (m.Success) { t.Code = m.Groups[1].Value; line = m.Groups[2].Value; }
                else line = line.Trim();
                t.Text = UnescapeLegacy(line);
                if (t.Text.Length > 0) list.Add(t);
            }
            return list;
        }
    }

    // ───────────── 輸出到目標視窗 ─────────────
    static class Paster
    {
        class ClipBackup { public DataObject Data; public bool WasEmpty; }

        // 尚未還原的剪貼簿備份。連續輸出兩次時要沿用「最早」那份，
        // 否則第二次備份到的會是第一次輸出的範本，使用者原本的剪貼簿就永久不見了。
        static ClipBackup pending;
        static uint pendingSeq;
        static System.Windows.Forms.Timer pendingTimer;
        const int RestoreDelayMs = 1200;   // 給目標程式（含遠端桌面等較慢的程式）足夠時間讀取剪貼簿

        // 只備份常見格式：逐一取出所有格式會逼來源程式產生大量資料（例如 Excel 大範圍），而且有些格式無法還原
        static readonly HashSet<string> KeepFormats = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            DataFormats.UnicodeText, DataFormats.Text, DataFormats.OemText, DataFormats.Locale, DataFormats.Rtf,
            DataFormats.Html, DataFormats.CommaSeparatedValue, DataFormats.FileDrop, DataFormats.Bitmap, DataFormats.Dib, "PNG"
        };

        const byte VK_SHIFT = 0x10, VK_CONTROL = 0x11, VK_MENU = 0x12, VK_LWIN = 0x5B, VK_RWIN = 0x5C;
        const uint KEYUP = 2;

        // 送出 Ctrl+V／Ctrl+C 前，等使用者放開 Shift、Alt、Win（否則目標程式收到的是 Ctrl+Shift+V 之類別的組合）。
        // 等不到就替他補送放開，讓組合鍵乾淨。
        static void ReleaseStrayModifiers(int maxWaitMs)
        {
            int[] keys = { VK_SHIFT, VK_MENU, VK_LWIN, VK_RWIN };
            for (int waited = 0; waited < maxWaitMs; waited += 15)
            {
                bool any = false;
                foreach (int k in keys) if ((Native.GetAsyncKeyState(k) & 0x8000) != 0) any = true;
                if (!any) return;
                Thread.Sleep(15);
            }
            foreach (int k in keys)
                if ((Native.GetAsyncKeyState(k) & 0x8000) != 0) Native.keybd_event((byte)k, 0, KEYUP, UIntPtr.Zero);
        }

        static void SendCtrl(byte key)
        {
            Native.keybd_event(VK_CONTROL, 0, 0, UIntPtr.Zero);
            Native.keybd_event(key, 0, 0, UIntPtr.Zero);
            Native.keybd_event(key, 0, KEYUP, UIntPtr.Zero);
            Native.keybd_event(VK_CONTROL, 0, KEYUP, UIntPtr.Zero);
        }

        public static void Paste(IntPtr target, string text, bool restore)
        {
            ClipBackup backup = restore ? TakePendingOrBackup() : null;
            try { Clipboard.SetDataObject(text.Replace("\n", "\r\n"), true, 10, 50); }
            catch
            {
                if (backup != null) Restore(backup);
                MessageBox.Show(L.T("剪貼簿被其他程式占用，請再試一次。"), L.AppName);
                return;
            }
            uint seq = Native.GetClipboardSequenceNumber();

            if (target != IntPtr.Zero && Native.IsWindow(target)) Native.SetForegroundWindow(target);
            Thread.Sleep(80);
            ReleaseStrayModifiers(600);
            SendCtrl(0x56);   // V

            if (backup != null) ScheduleRestore(backup, seq);
        }

        static ClipBackup TakePendingOrBackup()
        {
            if (pendingTimer != null)
            {
                pendingTimer.Stop(); pendingTimer.Dispose(); pendingTimer = null;
                ClipBackup b = pending; pending = null;
                // 還原前使用者若自己又複製了別的東西，就以那個為準
                if (b != null && Native.GetClipboardSequenceNumber() == pendingSeq) return b;
            }
            return Backup();
        }

        static void ScheduleRestore(ClipBackup b, uint seq)
        {
            pending = b; pendingSeq = seq;
            pendingTimer = new System.Windows.Forms.Timer { Interval = RestoreDelayMs };
            pendingTimer.Tick += delegate { FlushPending(); };
            pendingTimer.Start();
        }

        // 立刻完成尚未執行的剪貼簿還原
        public static void FlushPending()
        {
            if (pendingTimer == null) return;
            pendingTimer.Stop(); pendingTimer.Dispose(); pendingTimer = null;
            ClipBackup b = pending; pending = null;
            if (b != null && Native.GetClipboardSequenceNumber() == pendingSeq) Restore(b);  // 使用者又複製了別的東西就不要蓋掉
        }

        static void Restore(ClipBackup b)
        {
            try
            {
                if (b.WasEmpty) Clipboard.Clear();
                else if (b.Data != null) Clipboard.SetDataObject(b.Data, true, 5, 50);
            }
            catch { }
        }

        // 複製目前選取的文字（快速新增用）；取完後把剪貼簿還原。呼叫前使用者應已放開修飾鍵。
        public static string CopySelection(IntPtr target)
        {
            FlushPending();   // 先完成上次輸出的還原，才不會把範本誤當成原本的剪貼簿
            ClipBackup backup = Backup();
            uint seq = Native.GetClipboardSequenceNumber();
            if (target != IntPtr.Zero && Native.IsWindow(target)) Native.SetForegroundWindow(target);
            Thread.Sleep(50);
            ReleaseStrayModifiers(300);
            SendCtrl(0x43);   // C
            string text = "";
            // 最多等 0.75 秒讓對方程式把選取內容放進剪貼簿。這裡刻意不呼叫 DoEvents，以免等待中又處理到快速鍵而重入。
            for (int i = 0; i < 25 && Native.GetClipboardSequenceNumber() == seq; i++) Thread.Sleep(30);
            if (Native.GetClipboardSequenceNumber() != seq)
            {
                Thread.Sleep(30);
                try { if (Clipboard.ContainsText()) text = Clipboard.GetText(); } catch { }
                if (backup != null) Restore(backup);   // backup 為 null 表示備份失敗：保留現狀，不要清空
            }
            return Template.NormalizeNewlines(text).Trim('\n');
        }

        // 回傳 null 表示無法備份（剪貼簿被鎖住，或只有無法還原的特殊格式）
        static ClipBackup Backup()
        {
            try
            {
                IDataObject src = Clipboard.GetDataObject();
                if (src == null) return new ClipBackup { WasEmpty = true };
                string[] formats = src.GetFormats(false);
                if (formats.Length == 0) return new ClipBackup { WasEmpty = true };
                var dst = new DataObject();
                int n = 0;
                foreach (string f in formats)
                {
                    if (!KeepFormats.Contains(f)) continue;
                    try { object d = src.GetData(f, false); if (d != null) { dst.SetData(f, false, d); n++; } } catch { }
                }
                return n == 0 ? null : new ClipBackup { Data = dst };
            }
            catch { return null; }
        }
    }

    // ───────────── 叫出的範本清單 ─────────────
    class PopupForm : Form
    {
        readonly AppContext app;
        readonly Label header = new Label();
        readonly ListView list = new ListView();
        readonly Label preview = new Label();
        readonly Label hint = new Label();
        string buffer = "";
        List<Template> shown = new List<Template>();
        public IntPtr Target;
        PopupStyle style;
        string appliedKey;
        Font rowFont, headerFont;
        readonly ImageList rowSizer = new ImageList();   // 用來撐高清單的列高

        public PopupForm(AppContext app)
        {
            this.app = app;
            Text = L.AppName;
            FormBorderStyle = FormBorderStyle.None;
            ShowInTaskbar = false;
            TopMost = true;
            StartPosition = FormStartPosition.Manual;
            KeyPreview = true;
            ImeMode = ImeMode.Disable;
            Padding = new Padding(Math.Max(1, Ui.P(1)));   // 露出的 BackColor 當作外框
            Font = Ui.Base;

            header.Dock = DockStyle.Top;
            header.TextAlign = ContentAlignment.MiddleLeft;
            header.Padding = new Padding(Ui.P(8), 0, 0, 0);
            header.AutoEllipsis = true;

            list.Dock = DockStyle.Fill;
            list.View = View.Details;
            list.FullRowSelect = true;
            list.HideSelection = false;
            list.MultiSelect = false;
            list.HeaderStyle = ColumnHeaderStyle.None;
            list.BorderStyle = BorderStyle.None;
            list.ImeMode = ImeMode.Disable;
            list.OwnerDraw = true;
            list.Columns.Add("", Ui.P(600));
            list.SmallImageList = rowSizer;
            list.DrawItem += (s, e) =>
            {
                var t = e.Item.Tag as Template;
                if (t != null)
                    PopupRenderer.DrawRow(e.Graphics, e.Bounds, t.Code.Length > 0 ? t.Code : "·", t.Title, t.Preview, e.Item.Selected, false, style, rowFont);
                else
                    PopupRenderer.DrawRow(e.Graphics, e.Bounds, "", e.Item.Text, "", false, true, style, rowFont);
            };
            list.DrawSubItem += (s, e) => { };
            list.Resize += delegate { if (list.Columns.Count > 0) list.Columns[0].Width = list.ClientSize.Width; };
            list.SelectedIndexChanged += delegate { UpdatePreview(); };
            list.MouseDoubleClick += delegate { CommitSelected(); };

            preview.Dock = DockStyle.Bottom;
            preview.Padding = new Padding(Ui.P(8), Ui.P(4), Ui.P(8), 0);
            preview.AutoEllipsis = true;

            hint.Dock = DockStyle.Bottom;
            hint.Height = TextRenderer.MeasureText("Ag範", Ui.Small).Height + Ui.P(8);
            hint.Font = Ui.Small;
            hint.Padding = new Padding(Ui.P(8), 0, 0, 0);
            hint.TextAlign = ContentAlignment.MiddleLeft;
            hint.Text = L.T("打代碼跳選　↑↓ 移動　Enter 輸出　Backspace 刪代碼　Esc 取消　F2 句庫管理");

            Controls.Add(list);
            Controls.Add(preview);
            Controls.Add(hint);
            Controls.Add(header);

            ApplyStyle(app.Settings.Style);
            Deactivate += delegate { if (Visible) Close0(false); };
        }

        // 套用外觀設定（字型、顏色、大小）；設定沒變就不重做
        public void ApplyStyle(PopupStyle st)
        {
            string key = st.FontName + "|" + st.FontSize + "|" + st.BackR + st.ForeR + st.CodeFore + st.SelBack + st.SelFore + st.HeaderBack + st.HeaderFore + "|" + st.Width + "|" + st.Rows;
            if (key == appliedKey) return;
            appliedKey = key;
            style = st.Clone();
            if (rowFont != null) rowFont.Dispose();
            if (headerFont != null) headerFont.Dispose();
            rowFont = st.MakeFont(FontStyle.Regular);
            headerFont = st.MakeFont(FontStyle.Bold);

            int rowH = PopupRenderer.RowHeight(rowFont);
            rowSizer.ImageSize = new Size(1, Math.Min(256, rowH));
            list.Font = rowFont;
            list.BackColor = st.BackR;
            list.ForeColor = st.ForeR;

            BackColor = st.HeaderBack;
            header.BackColor = st.HeaderBack;
            header.ForeColor = st.HeaderFore;
            header.Font = headerFont;
            header.Height = TextRenderer.MeasureText("範", headerFont).Height + Ui.P(12);

            Color paneBack = PopupStyle.Blend(st.BackR, st.ForeR, 0.06f);
            preview.BackColor = hint.BackColor = paneBack;
            preview.ForeColor = st.ForeR;
            hint.ForeColor = PopupStyle.Blend(st.BackR, st.ForeR, 0.55f);
            preview.Font = rowFont;
            int lineH = TextRenderer.MeasureText("範", rowFont).Height;
            preview.Height = lineH * 3 + Ui.P(10);

            // 實際量一列的高度（清單會在圖示高度外再加幾個像素），避免最後一筆被切掉
            list.BeginUpdate();
            var saved = new ListViewItem[list.Items.Count];
            list.Items.CopyTo(saved, 0);
            list.Items.Clear();
            list.Items.Add("量測");
            int realRowH = Math.Max(rowH, list.GetItemRect(0).Height);
            list.Items.Clear();
            list.Items.AddRange(saved);
            list.EndUpdate();

            int w = Ui.P(st.Width);
            int h = Padding.Vertical + header.Height + realRowH * st.Rows + Ui.P(2) + preview.Height + hint.Height;
            Size = new Size(w, h);
            list.Columns[0].Width = list.ClientSize.Width;
        }

        public void Open(IntPtr target)
        {
            Target = target;
            buffer = "";
            ApplyStyle(app.Settings.Style);
            Refilter();
            PlaceNearCaret(target);
            Show();
            Activate();
            Native.SetForegroundWindow(Handle);
            // 用 `+1 這類組合叫出時，Windows 不一定准許搶焦點（一般快速鍵有系統給的許可，攔截來的沒有）；
            // 搶不到就借用前景視窗的輸入狀態再試一次
            if (Native.GetForegroundWindow() != Handle) ForceForeground();
            list.Focus();
            // 稍後再確認一次：還是沒搶到焦點的話，清單不能留在畫面上，否則打的代碼會跑進原本的視窗
            var check = new System.Windows.Forms.Timer { Interval = 250 };
            check.Tick += delegate
            {
                check.Stop(); check.Dispose();
                if (Visible && Native.GetForegroundWindow() != Handle) Close0(false);
            };
            check.Start();
        }

        void ForceForeground()
        {
            // 不用「送出 Alt」的作法：萬一沒搶到，原視窗（例如 Word）會收到單按 Alt 而進入功能區快捷字母模式，
            // 接著打的代碼會變成執行功能區指令
            IntPtr fg = Native.GetForegroundWindow();
            uint fgThread = fg == IntPtr.Zero ? 0 : Native.GetWindowThreadProcessId(fg, IntPtr.Zero);
            uint me = Native.GetCurrentThreadId();
            if (fgThread == 0 || fgThread == me || !Native.AttachThreadInput(me, fgThread, true)) return;
            try
            {
                Native.BringWindowToTop(Handle);
                Native.SetForegroundWindow(Handle);
                Activate();
            }
            finally { Native.AttachThreadInput(me, fgThread, false); }
        }

        void PlaceNearCaret(IntPtr target)
        {
            Point p = Cursor.Position;
            int caretH = 0;
            try
            {
                var gti = new Native.GUITHREADINFO();
                gti.cbSize = Marshal.SizeOf(gti);
                uint tid = Native.GetWindowThreadProcessId(target, IntPtr.Zero);
                if (Native.GetGUIThreadInfo(tid, ref gti) && gti.hwndCaret != IntPtr.Zero)
                {
                    var pt = new Native.POINT { X = gti.rcCaret.Left, Y = gti.rcCaret.Bottom };
                    if (Native.ClientToScreen(gti.hwndCaret, ref pt)) { p = new Point(pt.X, pt.Y); caretH = gti.rcCaret.Bottom - gti.rcCaret.Top; }
                }
            }
            catch { }
            Rectangle wa = Screen.FromPoint(p).WorkingArea;
            int x = p.X, y = p.Y + 6;
            if (y + Height > wa.Bottom) y = p.Y - caretH - Height - 6;
            if (x + Width > wa.Right) x = wa.Right - Width;
            if (x < wa.Left) x = wa.Left;
            if (y < wa.Top) y = wa.Top;
            Location = new Point(x, y);
        }

        void Refilter()
        {
            List<Template> all = app.Store.Items;
            shown = new List<Template>();
            if (buffer.Length == 0) shown.AddRange(all);
            else
            {
                foreach (Template t in all) if (t.Code.Length > 0 && t.Key.StartsWith(buffer, StringComparison.Ordinal)) shown.Add(t);
                if (shown.Count == 0)   // 沒有代碼符合時，改搜尋標題與內容（例如打 legal）
                    foreach (Template t in all)
                        if (t.Title.IndexOf(buffer, StringComparison.OrdinalIgnoreCase) >= 0 || t.Text.IndexOf(buffer, StringComparison.OrdinalIgnoreCase) >= 0) shown.Add(t);
            }

            list.BeginUpdate();
            list.Items.Clear();
            int exact = 0;
            for (int i = 0; i < shown.Count; i++)
            {
                list.Items.Add(new ListViewItem(shown[i].Code) { Tag = shown[i] });
                if (buffer.Length > 0 && shown[i].Key == buffer) exact = i;
            }
            if (shown.Count == 0)
                list.Items.Add(new ListViewItem(app.Store.Items.Count == 0 ? L.T("（還沒有範本，按 F2 開啟句庫管理新增）") : L.T("（沒有符合的範本）")));
            list.EndUpdate();
            list.Columns[0].Width = list.ClientSize.Width;   // 筆數多到出現捲軸時，欄寬要扣掉捲軸，否則會冒出水平捲軸蓋住最後一列
            if (shown.Count > 0) { list.Items[exact].Selected = true; list.Items[exact].Focused = true; list.EnsureVisible(exact); }

            string title = L.AppName + "　" + (buffer.Length > 0 ? L.F("代碼：{0}", buffer) + "▌" : L.T("請打代碼"));
            if (app.Store.Error != null) title = app.Store.Error;
            header.Text = title + "　(" + shown.Count + "/" + app.Store.Items.Count + ")";
            UpdatePreview();
        }

        void UpdatePreview()
        {
            Template t = Selected();
            preview.Text = t == null ? "" : t.Text.Replace("\n", "\r\n");
        }

        Template Selected()
        {
            if (list.SelectedIndices.Count == 0) return null;
            int i = list.SelectedIndices[0];
            return i < shown.Count ? shown[i] : null;
        }

        void MoveSel(int delta)
        {
            if (shown.Count == 0) return;
            int i = list.SelectedIndices.Count > 0 ? list.SelectedIndices[0] : 0;
            i = Math.Max(0, Math.Min(shown.Count - 1, i + delta));
            list.Items[i].Selected = true; list.Items[i].Focused = true; list.EnsureVisible(i);
        }

        void TypeChar(char c)
        {
            if (buffer.Length >= 20) return;
            buffer += char.ToLowerInvariant(c);
            Refilter();
            if (!app.Settings.AutoCommit) return;
            Template exact = null;
            bool longer = false;
            foreach (Template t in app.Store.Items)
            {
                string k = t.Key;
                if (k == buffer) { if (exact == null) exact = t; else longer = true; }  // 代碼重複時也停住讓使用者選
                else if (k.Length > buffer.Length && k.StartsWith(buffer, StringComparison.Ordinal)) longer = true;
            }
            if (exact != null && !longer) Commit(exact);
        }

        void CommitSelected()
        {
            Template t = Selected();
            if (t != null) Commit(t);
        }

        void Commit(Template t)
        {
            Close0(false);
            Paster.Paste(Target, t.Text, app.Settings.RestoreClipboard);
        }

        public void Close0(bool refocusTarget)
        {
            Hide();
            if (refocusTarget && Target != IntPtr.Zero && Native.IsWindow(Target)) Native.SetForegroundWindow(Target);
        }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            Keys k = e.KeyCode;
            if (k == Keys.ProcessKey)   // 輸入法沒關掉時，取回真正的按鍵
            {
                try { k = (Keys)Native.ImmGetVirtualKey(list.Handle); } catch { }
            }
            bool handled = true;
            if (k == Keys.Escape) Close0(true);
            else if (k == Keys.Enter || k == Keys.Space) CommitSelected();
            else if (k == Keys.Up) MoveSel(-1);
            else if (k == Keys.Down) MoveSel(1);
            else if (k == Keys.PageUp) MoveSel(-8);
            else if (k == Keys.PageDown) MoveSel(8);
            else if (k == Keys.Home) MoveSel(-100000);
            else if (k == Keys.End) MoveSel(100000);
            else if (k == Keys.Back) { if (buffer.Length > 0) { buffer = buffer.Substring(0, buffer.Length - 1); Refilter(); } }
            else if (k == Keys.F2) { Close0(false); app.ShowManager(); }
            else if (k == Keys.Oemtilde && !e.Control) Close0(true);
            else if (!e.Alt && !e.Shift && k >= Keys.D0 && k <= Keys.D9) TypeChar((char)('0' + (k - Keys.D0)));
            else if (!e.Alt && k >= Keys.NumPad0 && k <= Keys.NumPad9) TypeChar((char)('0' + (k - Keys.NumPad0)));
            else if (!e.Alt && !e.Control && !e.Shift && k >= Keys.A && k <= Keys.Z) TypeChar((char)('a' + (k - Keys.A)));
            else handled = false;
            if (handled) { e.Handled = true; e.SuppressKeyPress = true; }
            base.OnKeyDown(e);
        }

        // 其他可當代碼的符號（例如匯入的代碼含有 , . / ; 等）
        protected override void OnKeyPress(KeyPressEventArgs e)
        {
            char c = e.KeyChar;
            if (c > ' ' && c <= '~' && c != '`' && c != '~') { TypeChar(c); e.Handled = true; }
            base.OnKeyPress(e);
        }
    }

    // ───────────── 新增／修改範本 ─────────────
    class EditForm : Form
    {
        readonly TextBox title = new TextBox();
        readonly TextBox code = new TextBox();
        readonly TextBox content = new TextBox();
        readonly Label counter = new Label();
        readonly Template original;
        readonly List<Template> existing;
        public Template Result;

        // isNew=true 時 t 只是預先填入的內容（快速新增），不是要修改的那一筆
        public EditForm(string caption, Template t, List<Template> existing, bool isNew = false)
        {
            original = isNew ? null : t;
            this.existing = existing;
            Text = caption;
            Font = Ui.Base;
            StartPosition = FormStartPosition.CenterParent;
            FormBorderStyle = FormBorderStyle.Sizable;
            MinimizeBox = false;
            ShowIcon = false;
            ShowInTaskbar = false;
            ClientSize = new Size(Ui.PS(620), Ui.PS(470));
            MinimumSize = new Size(Ui.P(460), Ui.P(360));
            KeyPreview = true;

            var tl = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 5, Padding = new Padding(Ui.P(12)) };
            tl.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            tl.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            tl.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            tl.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            tl.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            tl.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            tl.RowStyles.Add(new RowStyle(SizeType.AutoSize));

            title.Dock = code.Dock = DockStyle.Fill;
            code.ImeMode = ImeMode.Disable;
            code.MaxLength = 20;
            content.Dock = DockStyle.Fill;
            content.Multiline = true;
            content.AcceptsReturn = true;
            content.AcceptsTab = true;
            content.ScrollBars = ScrollBars.Vertical;
            content.WordWrap = true;

            tl.Controls.Add(Lbl(L.T("名稱")), 0, 0); tl.Controls.Add(title, 1, 0);
            tl.Controls.Add(Lbl(L.T("代碼")), 0, 1); tl.Controls.Add(code, 1, 1);
            var codeHint = new Label
            {
                Text = L.T("英文字母、數字或符號，最多 20 碼（不分大小寫）。叫出清單後打這組代碼就會直接輸出。"),
                AutoSize = true, ForeColor = Theme.SubFore, Font = Ui.Small, Margin = new Padding(Ui.P(3), 0, 0, Ui.P(8))
            };
            tl.Controls.Add(codeHint, 1, 2);
            var lc = Lbl(L.T("內容")); lc.Anchor = AnchorStyles.Top | AnchorStyles.Left; lc.Margin = new Padding(Ui.P(3), Ui.P(6), Ui.P(3), 0);
            tl.Controls.Add(lc, 0, 3); tl.Controls.Add(content, 1, 3);

            var bottom = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 3, AutoSize = true, Margin = new Padding(0, Ui.P(8), 0, 0) };
            bottom.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            bottom.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            bottom.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            counter.AutoSize = true; counter.Anchor = AnchorStyles.Left; counter.ForeColor = Theme.SubFore;
            var ok = Btn(L.T("確認")); var cancel = Btn(L.T("取消"));
            ok.Click += delegate { TryOk(); };
            cancel.DialogResult = DialogResult.Cancel;
            CancelButton = cancel;
            bottom.Controls.Add(counter, 0, 0); bottom.Controls.Add(ok, 1, 0); bottom.Controls.Add(cancel, 2, 0);
            tl.Controls.Add(bottom, 0, 4); tl.SetColumnSpan(bottom, 2);
            Controls.Add(tl);

            content.TextChanged += delegate { counter.Text = L.F("字數：{0}", Template.NormalizeNewlines(content.Text).Length) + L.T("　（Ctrl+Enter 確認）"); };
            if (t != null)
            {
                title.Text = t.Title;
                code.Text = t.Code;
                content.Text = t.Text.Replace("\n", "\r\n");
            }
            counter.Text = L.F("字數：{0}", Template.NormalizeNewlines(content.Text).Length) + L.T("　（Ctrl+Enter 確認）");
            Shown += delegate { if (t == null || isNew) title.Focus(); else content.Focus(); };
            content.Font = Ui.Content;
            Theme.Apply(this);
        }

        static Label Lbl(string s)
        {
            return new Label { Text = s, AutoSize = true, Anchor = AnchorStyles.Left, Margin = new Padding(Ui.P(3), Ui.P(6), Ui.P(10), Ui.P(6)) };
        }

        static Button Btn(string s)
        {
            return new Button { Text = s, AutoSize = true, MinimumSize = new Size(Ui.P(96), Ui.P(34)), Margin = new Padding(Ui.P(8), 0, 0, 0) };
        }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            if (e.Control && e.KeyCode == Keys.Enter) { e.SuppressKeyPress = true; TryOk(); }
            base.OnKeyDown(e);
        }

        void TryOk()
        {
            Control bad;
            Template r = BuildValidated(this, Text, title.Text, code.Text, content.Text, original, existing, title, code, content, out bad);
            if (r == null) { if (bad != null) bad.Focus(); return; }
            Result = r;
            DialogResult = DialogResult.OK;
        }

        // 新增／修改範本共用的檢查：內容不可空白、代碼格式、代碼重複提醒。
        // 通過就回傳整理好的範本；不通過回傳 null，bad 為應該回去修改的欄位。
        public static Template BuildValidated(IWin32Window owner, string caption, string title, string code, string content,
                                              Template original, List<Template> existing,
                                              Control titleBox, Control codeBox, Control contentBox, out Control bad)
        {
            bad = null;
            string text = Template.NormalizeNewlines(content).Trim('\n');
            if (text.Trim().Length == 0) { MessageBox.Show(owner, L.T("內容不能是空白。"), caption); bad = contentBox; return null; }
            string c = code.Trim();
            if (c.Length > 0 && !Csv.IsValidCode(c))
            {
                MessageBox.Show(owner, L.T("代碼只能用英文字母、數字或半形符號，\n不能有空白、中文或 ` 鍵。"), caption);
                bad = codeBox; return null;
            }
            if (c.Length > 0)
            {
                foreach (Template x in existing)
                {
                    if (x.Key == c.ToLowerInvariant() && !x.SameAs(original))
                    {
                        var r = MessageBox.Show(owner, L.F("代碼「{0}」已經被「{1}」使用了。\n\n代碼重複的話，打這組代碼時清單會停住，讓你用方向鍵和 Enter 選。\n仍要儲存嗎？", c, x.DisplayTitle),
                                                caption, MessageBoxButtons.YesNo, MessageBoxIcon.Question);
                        if (r != DialogResult.Yes) { bad = codeBox; return null; }
                        break;
                    }
                }
            }
            return new Template { Title = title.Trim(), Code = c, Text = text };
        }
    }

    // ───────────── 設定視窗 ─────────────
    class SettingsForm : Form
    {
        readonly TextBox hotkey = new TextBox();
        readonly TextBox quickAdd = new TextBox();
        readonly CheckBox autoCommit = new CheckBox();
        readonly CheckBox restoreClip = new CheckBox();
        readonly CheckBox autoStart = new CheckBox();
        readonly ComboBox fontBox = new ComboBox();
        readonly NumericUpDown fontSize = new NumericUpDown();
        readonly NumericUpDown widthBox = new NumericUpDown();
        readonly NumericUpDown rowsBox = new NumericUpDown();
        readonly NumericUpDown uiSizeBox = new NumericUpDown();
        readonly ComboBox langBox = new ComboBox();
        readonly ComboBox appThemeBox = new ComboBox();
        readonly NumericUpDown contentSizeBox = new NumericUpDown();
        readonly Panel previewPanel = new Panel();
        readonly List<Action> refreshSwatches = new List<Action>();
        readonly List<ThemeSwatch> themeSwatches = new List<ThemeSwatch>();
        readonly Dictionary<string, Font> fontCache = new Dictionary<string, Font>();
        PopupStyle work;
        bool loading;
        public Settings Result;
        public bool AutoStartOn;

        public SettingsForm(Settings s)
        {
            work = s.Style.Clone();
            Text = L.F("{0} 設定", L.AppName);
            Font = Ui.Base;
            StartPosition = FormStartPosition.CenterScreen;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = MinimizeBox = false;
            ShowInTaskbar = true;
            ClientSize = Ui.FitScreen(Ui.PS(760), Ui.PS(790));

            var tabs = new ThemedTabControl { Dock = DockStyle.Fill, Padding = new Point(Ui.P(14), Ui.P(5)) };
            tabs.TabPages.Add(BuildBehaviorPage(s));
            tabs.TabPages.Add(BuildStylePage());

            var bottom = new FlowLayoutPanel { FlowDirection = FlowDirection.RightToLeft, Dock = DockStyle.Bottom, AutoSize = true, Padding = new Padding(Ui.P(8)) };
            var cancel = new Button { Text = L.T("取消"), AutoSize = true, MinimumSize = new Size(Ui.P(96), Ui.P(34)), DialogResult = DialogResult.Cancel };
            var ok = new Button { Text = L.T("確定"), AutoSize = true, MinimumSize = new Size(Ui.P(96), Ui.P(34)) };
            ok.Click += delegate
            {
                if (!Settings.IsValidHotkey(hotkey.Text)) { MessageBox.Show(this, L.T("叫出清單的快速鍵看不懂，請重新設定。"), Text); return; }
                if (quickAdd.Text.Length > 0)
                {
                    if (!Settings.IsValidHotkey(quickAdd.Text)) { MessageBox.Show(this, L.T("快速新增的快速鍵看不懂，請重新設定。"), Text); return; }
                    if (string.Equals(quickAdd.Text, hotkey.Text, StringComparison.OrdinalIgnoreCase)) { MessageBox.Show(this, L.T("兩組快速鍵不能相同。"), Text); return; }
                }
                Result = s.Clone();
                Result.Hotkey = hotkey.Text;
                Result.QuickAddHotkey = quickAdd.Text;
                Result.AutoCommit = autoCommit.Checked;
                Result.RestoreClipboard = restoreClip.Checked;
                Result.UiFontSize = (float)uiSizeBox.Value;
                Result.Language = langBox.SelectedIndex <= 0 ? "auto" : L.Codes[langBox.SelectedIndex - 1];
                Result.AppTheme = appThemeBox.SelectedIndex == 1 ? "light" : appThemeBox.SelectedIndex == 2 ? "dark" : "auto";
                Result.ContentFontSize = (float)contentSizeBox.Value;
                Result.Style = work.Clone();
                AutoStartOn = autoStart.Checked;
                DialogResult = DialogResult.OK;
            };
            bottom.Controls.Add(cancel); bottom.Controls.Add(ok);
            AcceptButton = ok;
            CancelButton = cancel;
            Controls.Add(tabs);
            Controls.Add(bottom);
            FormClosed += delegate { foreach (Font f in fontCache.Values) f.Dispose(); };
            Theme.Apply(this);
        }

        // ── 分頁一：快速鍵與行為 ──
        TabPage BuildBehaviorPage(Settings s)
        {
            var page = new TabPage(L.T("快速鍵與行為")) { UseVisualStyleBackColor = true };
            var tl = new TableLayoutPanel { ColumnCount = 2, Dock = DockStyle.Fill, Padding = new Padding(Ui.P(14)), AutoScroll = true };
            for (int i = 0; i < 15; i++) tl.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            tl.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            tl.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));

            SetupHotkeyBox(hotkey, s.Hotkey, false);
            SetupHotkeyBox(quickAdd, s.QuickAddHotkey, true);
            int r = 0;
            tl.Controls.Add(Lbl(L.T("叫出範本清單")), 0, r); tl.Controls.Add(hotkey, 1, r++);
            tl.Controls.Add(Hint(L.T("點一下格子，直接按下想用的組合鍵（例如 Ctrl+`、Alt+`、F9），\n或按住 ` 再按一個鍵（例如 `+1）。單獨一個鍵（F1～F12 除外）不能當快速鍵，避免打字時誤觸。")), 1, r++);
            tl.Controls.Add(Lbl(L.T("快速新增範本")), 0, r); tl.Controls.Add(quickAdd, 1, r++);
            tl.Controls.Add(Hint(L.T("在任何地方先反白一段文字，再按這組鍵，就會開啟「新增範本」並自動帶入反白的內容。\n在格子裡按 Backspace 可清除（停用此功能）。")), 1, r++);

            uiSizeBox.DecimalPlaces = 1; uiSizeBox.Increment = 0.5m; uiSizeBox.Minimum = 8; uiSizeBox.Maximum = 24;
            uiSizeBox.Value = (decimal)Math.Max(8f, Math.Min(24f, s.UiFontSize));
            uiSizeBox.Width = Ui.P(80); uiSizeBox.Anchor = AnchorStyles.Left;
            tl.Controls.Add(Lbl(L.T("視窗字體大小")), 0, r); tl.Controls.Add(uiSizeBox, 1, r++);
            tl.Controls.Add(Hint(L.T("各視窗的按鈕、選單、標籤等文字大小（預設 10.5）。放太大可能讓視窗超出螢幕。")), 1, r++);

            contentSizeBox.DecimalPlaces = 1; contentSizeBox.Increment = 0.5m; contentSizeBox.Minimum = 8; contentSizeBox.Maximum = 28;
            contentSizeBox.Value = (decimal)Math.Max(8f, Math.Min(28f, s.ContentFontSize));
            contentSizeBox.Width = Ui.P(80); contentSizeBox.Anchor = AnchorStyles.Left;
            tl.Controls.Add(Lbl(L.T("句庫內容字體大小")), 0, r); tl.Controls.Add(contentSizeBox, 1, r++);
            tl.Controls.Add(Hint(L.T("句庫管理裡範本清單與編輯區的文字大小，和上面的視窗字體分開調整，放大也不會讓視窗變大。\n在句庫管理的工具列上也可以直接調。叫出的範本清單則到「清單外觀」分頁調整。")), 1, r++);

            autoCommit.Text = L.T("打的代碼只對應到一筆時，不必按 Enter 就直接輸出");
            restoreClip.Text = L.T("輸出後把剪貼簿還原成原本的內容");
            autoStart.Text = L.T("開機時自動啟動（每台電腦各自設定）");
            autoCommit.AutoSize = restoreClip.AutoSize = autoStart.AutoSize = true;
            autoCommit.Margin = restoreClip.Margin = autoStart.Margin = new Padding(Ui.P(3), Ui.P(6), 0, Ui.P(6));
            autoCommit.Checked = s.AutoCommit;
            restoreClip.Checked = s.RestoreClipboard;
            autoStart.Checked = AutoStart.IsOn();
            foreach (Control c in new Control[] { autoCommit, restoreClip, autoStart }) { tl.Controls.Add(c, 0, r++); tl.SetColumnSpan(c, 2); }

            // 介面語言（改了會自動重新啟動程式）
            langBox.DropDownStyle = ComboBoxStyle.DropDownList;
            langBox.Width = Ui.P(240);
            langBox.Anchor = AnchorStyles.Left;
            langBox.Items.Add(L.T("自動（依 Windows 語言）"));
            foreach (string n in L.NativeNames) langBox.Items.Add(n);
            int li = -1;
            for (int i = 0; i < L.Codes.Length; i++) if (string.Equals(L.Codes[i], s.Language, StringComparison.OrdinalIgnoreCase)) li = i;
            langBox.SelectedIndex = li < 0 ? 0 : li + 1;
            var langLbl = Lbl(L.Code == "en" ? "Language" : L.Code == "ja" ? "表示言語 / Language" : L.Code == "zh-CN" ? "界面语言 / Language" : "介面語言 / Language"); langLbl.Margin = new Padding(Ui.P(3), Ui.P(18), Ui.P(10), Ui.P(3));
            langBox.Margin = new Padding(Ui.P(3), Ui.P(14), Ui.P(3), Ui.P(3));
            tl.Controls.Add(langLbl, 0, r); tl.Controls.Add(langBox, 1, r++);
            tl.Controls.Add(Hint(L.T("更改語言後，程式會自動重新啟動。")), 1, r++);

            // 外觀模式（改了會自動重新啟動程式）
            appThemeBox.DropDownStyle = ComboBoxStyle.DropDownList;
            appThemeBox.Width = Ui.P(240);
            appThemeBox.Anchor = AnchorStyles.Left;
            appThemeBox.Items.Add(L.T("跟隨 Windows"));
            appThemeBox.Items.Add(L.T("淺色"));
            appThemeBox.Items.Add(L.T("深色"));
            appThemeBox.SelectedIndex = s.AppTheme == "light" ? 1 : s.AppTheme == "dark" ? 2 : 0;
            tl.Controls.Add(Lbl(L.T("外觀模式")), 0, r); tl.Controls.Add(appThemeBox, 1, r++);
            tl.Controls.Add(Hint(L.T("深色模式會套用到句庫管理、設定等視窗。更改後，程式會自動重新啟動。\n叫出的範本清單有自己的配色，請到「清單外觀」分頁選擇（例如「深色」主題）。")), 1, r++);
            page.Controls.Add(tl);
            return page;
        }

        void SetupHotkeyBox(TextBox box, string value, bool allowClear)
        {
            box.ReadOnly = true;
            box.BackColor = SystemColors.Window;
            box.Width = Ui.P(240);
            box.Anchor = AnchorStyles.Left;
            box.ImeMode = ImeMode.Disable;
            box.ShortcutsEnabled = false;
            box.Text = value;
            bool prefixHeld = false;   // 正按住 `：下一個鍵錄成「`+鍵」
            box.KeyDown += (o, e) =>
            {
                if (e.KeyData == Keys.Escape || e.KeyData == Keys.Tab || e.KeyData == Keys.Enter) return;  // 保留關閉視窗、切換欄位的功能
                e.SuppressKeyPress = true; e.Handled = true;
                if (e.KeyData == Keys.Oemtilde) { prefixHeld = true; return; }
                string v;
                if (prefixHeld) v = Settings.FromChordKey(e.KeyData);
                else if (allowClear && (e.KeyData == Keys.Back || e.KeyData == Keys.Delete)) { box.Text = ""; return; }
                else v = Settings.FromKeys(e.KeyData);
                if (v != null) box.Text = v;
            };
            box.KeyUp += (o, e) => { if ((e.KeyData & Keys.KeyCode) == Keys.Oemtilde) prefixHeld = false; };
            box.Leave += delegate { prefixHeld = false; };
            box.PreviewKeyDown += (o, e) => { e.IsInputKey = e.KeyData != Keys.Escape && e.KeyData != Keys.Tab; };
        }

        // ── 分頁二：清單外觀 ──
        TabPage BuildStylePage()
        {
            // 整頁可捲動：螢幕較小時預覽不會被擠扁
            var page = new TabPage(L.T("清單外觀")) { UseVisualStyleBackColor = true, AutoScroll = true };
            var tl = new TableLayoutPanel { ColumnCount = 4, Dock = DockStyle.Top, AutoSize = true, Padding = new Padding(Ui.P(14), Ui.P(10), Ui.P(14), Ui.P(6)) };
            tl.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            tl.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
            tl.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            tl.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
            for (int i = 0; i < 7; i++) tl.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            tl.RowStyles.Add(new RowStyle(SizeType.Absolute, Ui.P(200)));   // 預覽固定高度

            // 字型（下拉選單裡每個字型用它自己的字體顯示）
            fontBox.DropDownStyle = ComboBoxStyle.DropDownList;
            fontBox.DrawMode = DrawMode.OwnerDrawFixed;
            fontBox.ItemHeight = Ui.P(24);
            fontBox.MaxDropDownItems = 16;
            fontBox.Dock = DockStyle.Fill;
            var names = new List<string>();
            foreach (FontFamily f in FontFamily.Families) if (f.IsStyleAvailable(FontStyle.Regular)) names.Add(f.Name);
            names.Sort(StringComparer.CurrentCulture);
            // 常用中文字型排在最前面（Windows 多半用英文名稱列出，例如標楷體是 DFKai-SB）
            var ordered = new List<string>();
            foreach (string[] fav in FontAliases) if (names.Contains(fav[0])) ordered.Add(fav[0]);
            foreach (string n in names) if (!ordered.Contains(n)) ordered.Add(n);
            if (!ordered.Contains(work.FontName)) ordered.Insert(0, work.FontName);
            foreach (string n in ordered) fontBox.Items.Add(n);
            fontBox.DrawItem += (o, e) =>
            {
                e.DrawBackground();
                if (e.Index < 0) return;
                string n = (string)fontBox.Items[e.Index];
                Font f = SampleFont(n);
                string label = FontLabel(n);
                if (L.Code == "zh-CN") label = L.ToSimplified(label);
                if (!PopupStyle.FontExists(n)) label += L.T("（這台電腦沒有安裝）");
                TextRenderer.DrawText(e.Graphics, label, f, e.Bounds, e.ForeColor, TextFormatFlags.VerticalCenter | TextFormatFlags.NoPrefix | TextFormatFlags.EndEllipsis);
            };
            fontBox.SelectedIndexChanged += delegate { if (!loading && fontBox.SelectedItem != null) { work.FontName = (string)fontBox.SelectedItem; Changed(); } };

            fontSize.DecimalPlaces = 1; fontSize.Increment = 0.5m; fontSize.Minimum = 7; fontSize.Maximum = 36;
            fontSize.Width = Ui.P(90); fontSize.Anchor = AnchorStyles.Left;
            fontSize.ValueChanged += delegate { if (!loading) { work.FontSize = (float)fontSize.Value; Changed(); } };
            widthBox.Minimum = 360; widthBox.Maximum = 1600; widthBox.Increment = 20; widthBox.Width = Ui.P(90); widthBox.Anchor = AnchorStyles.Left;
            widthBox.ValueChanged += delegate { if (!loading) { work.Width = (int)widthBox.Value; Changed(); } };
            rowsBox.Minimum = 3; rowsBox.Maximum = 30; rowsBox.Width = Ui.P(90); rowsBox.Anchor = AnchorStyles.Left;
            rowsBox.ValueChanged += delegate { if (!loading) { work.Rows = (int)rowsBox.Value; Changed(); } };

            tl.Controls.Add(Lbl(L.T("字型")), 0, 0); tl.Controls.Add(fontBox, 1, 0); tl.SetColumnSpan(fontBox, 3);
            tl.Controls.Add(Lbl(L.T("字體大小")), 0, 1); tl.Controls.Add(fontSize, 1, 1);
            tl.Controls.Add(Lbl(L.T("清單寬度")), 2, 1); tl.Controls.Add(widthBox, 3, 1);
            tl.Controls.Add(Lbl(L.T("一次顯示")), 0, 2); tl.Controls.Add(Suffix(rowsBox, L.T("筆")), 1, 2);

            tl.Controls.Add(Swatch(L.T("底色"), () => work.BackR, c => work.Back = c, () => work.Back.IsEmpty), 0, 3); tl.SetColumnSpan(tl.Controls[tl.Controls.Count - 1], 2);
            tl.Controls.Add(Swatch(L.T("文字顏色"), () => work.ForeR, c => work.Fore = c, () => work.Fore.IsEmpty), 2, 3); tl.SetColumnSpan(tl.Controls[tl.Controls.Count - 1], 2);
            tl.Controls.Add(Swatch(L.T("代碼顏色"), () => work.CodeFore, c => work.CodeFore = c), 0, 4); tl.SetColumnSpan(tl.Controls[tl.Controls.Count - 1], 2);
            tl.Controls.Add(Swatch(L.T("選取列底色"), () => work.SelBack, c => work.SelBack = c), 2, 4); tl.SetColumnSpan(tl.Controls[tl.Controls.Count - 1], 2);
            tl.Controls.Add(Swatch(L.T("選取列文字"), () => work.SelFore, c => work.SelFore = c), 0, 5); tl.SetColumnSpan(tl.Controls[tl.Controls.Count - 1], 2);
            tl.Controls.Add(Swatch(L.T("標題列底色"), () => work.HeaderBack, c => work.HeaderBack = c), 2, 5); tl.SetColumnSpan(tl.Controls[tl.Controls.Count - 1], 2);
            tl.Controls.Add(Swatch(L.T("標題列文字"), () => work.HeaderFore, c => work.HeaderFore = c), 0, 6); tl.SetColumnSpan(tl.Controls[tl.Controls.Count - 1], 2);
            var reset = new Button { Text = L.T("恢復預設外觀"), AutoSize = true, Anchor = AnchorStyles.Left };
            reset.Click += delegate { work = new PopupStyle(); LoadControls(); Changed(); };
            tl.Controls.Add(reset, 2, 6); tl.SetColumnSpan(reset, 2);

            // 即時預覽
            var box = new GroupBox { Text = L.T("預覽"), Dock = DockStyle.Fill, Padding = new Padding(Ui.P(10)) };
            previewPanel.Dock = DockStyle.Fill;
            previewPanel.Paint += (o, e) => DrawPreview(e.Graphics, previewPanel.ClientRectangle);
            previewPanel.Resize += delegate { previewPanel.Invalidate(); };
            typeof(Panel).GetProperty("DoubleBuffered", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).SetValue(previewPanel, true, null);
            box.Controls.Add(previewPanel);
            tl.Controls.Add(box, 0, 7); tl.SetColumnSpan(box, 4);

            page.Controls.Add(tl);

            // 配色主題：點一下套用一整組顏色（之後仍可在下方個別微調）
            var themeBox = new GroupBox { Text = L.T("配色主題（點一下套用）"), Dock = DockStyle.Top, AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink, Padding = new Padding(Ui.P(8), Ui.P(4), Ui.P(8), Ui.P(6)) };
            var themeTip = new ToolTip();
            // 不用 Dock：給定最大寬度讓它自己換行、自己算高度，外框才會跟著長高
            var themeFlow = new FlowLayoutPanel { AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink, WrapContents = true,
                                                  Location = new Point(Ui.P(8), Ui.P(22)),
                                                  MaximumSize = new Size(ClientSize.Width - Ui.P(80), 0) };
            foreach (ThemePreset preset in ThemePreset.All)
            {
                var sw = new ThemeSwatch(preset);
                if (preset.EyeCare) themeTip.SetToolTip(sw, L.T("護眼配色：低對比、少藍光，適合長時間閱讀。"));
                ThemePreset chosen = preset;
                sw.Click += delegate { chosen.ApplyTo(work); LoadControls(); Changed(); };
                themeSwatches.Add(sw);
                themeFlow.Controls.Add(sw);
            }
            themeBox.Controls.Add(themeFlow);
            var themeHolder = new Panel { Dock = DockStyle.Top, AutoSize = true, Padding = new Padding(Ui.P(14), Ui.P(10), Ui.P(14), 0) };
            themeHolder.Controls.Add(themeBox);
            page.Controls.Add(themeHolder);
            LoadControls();
            Changed();   // 標出目前套用中的配色主題
            return page;
        }

        // 常用中文字型：排在清單最前面；英文名稱的另外標上中文
        static readonly string[][] FontAliases =
        {
            new[] { "Microsoft JhengHei UI", "微軟正黑體 UI（預設）" },
            new[] { "微軟正黑體", "微軟正黑體" },
            new[] { "思源宋體", "思源宋體" },
            new[] { "思源宋體 Medium", "思源宋體 Medium" },
            new[] { "思源黑體", "思源黑體" },
            new[] { "標楷體", "標楷體" },
            new[] { "新細明體", "新細明體" },
            new[] { "細明體", "細明體" },
            new[] { "Microsoft JhengHei", "微軟正黑體" },
            new[] { "Source Han Serif TC", "思源宋體 TC" },
            new[] { "Source Han Serif TC Medium", "思源宋體 TC Medium" },
            new[] { "Noto Serif TC", "思源宋體（Noto Serif TC）" },
            new[] { "Noto Serif TC Medium", "思源宋體 Medium（Noto Serif TC）" },
            new[] { "Noto Serif TC SemiBold", "思源宋體 SemiBold（Noto Serif TC）" },
            new[] { "Source Han Sans TC", "思源黑體 TC" },
            new[] { "Noto Sans TC", "思源黑體（Noto Sans TC）" },
            new[] { "DFKai-SB", "標楷體" },
            new[] { "BiauKai", "標楷體" },
            new[] { "PMingLiU", "新細明體" },
            new[] { "MingLiU", "細明體" },
            new[] { "TW-Kai", "全字庫正楷體" },
            new[] { "TW-Sung", "全字庫正宋體" },
            new[] { "LXGW WenKai TC", "霞鶩文楷 TC" },
            new[] { "jf-openhuninn-2.0", "jf open 粉圓" },
        };

        static string FontLabel(string name)
        {
            foreach (string[] a in FontAliases) if (a[0] == name) return a[1] == name ? name : a[1] + "　" + name;
            return name;
        }

        void LoadControls()
        {
            loading = true;
            int idx = fontBox.Items.IndexOf(work.FontName);
            if (idx < 0) { fontBox.Items.Insert(0, work.FontName); idx = 0; }
            fontBox.SelectedIndex = idx;
            fontSize.Value = (decimal)Math.Max(7f, Math.Min(36f, work.FontSize));
            widthBox.Value = Math.Max(360, Math.Min(1600, work.Width));
            rowsBox.Value = Math.Max(3, Math.Min(30, work.Rows));
            foreach (Action a in refreshSwatches) a();
            loading = false;
        }

        void Changed()
        {
            previewPanel.Invalidate();
            foreach (ThemeSwatch sw in themeSwatches)
            {
                bool cur = sw.Preset.Matches(work);
                if (cur != sw.Current) { sw.Current = cur; sw.Invalidate(); }
            }
        }

        Font SampleFont(string name)
        {
            Font f;
            if (!fontCache.TryGetValue(name, out f))
            {
                try { f = new Font(PopupStyle.FontExists(name) ? name : PopupStyle.DefaultFont, 11f); }
                catch { f = new Font(PopupStyle.DefaultFont, 11f); }
                fontCache[name] = f;
            }
            return f;
        }

        // 一個「顏色名稱＋色塊按鈕」；按下去開調色盤
        Control Swatch(string label, Func<Color> get, Action<Color> set, Func<bool> followsSystem = null)
        {
            var flow = new FlowLayoutPanel { AutoSize = true, WrapContents = false, Margin = new Padding(0, Ui.P(2), 0, Ui.P(2)) };
            var l = Lbl(label); l.MinimumSize = new Size(Ui.P(112), Ui.P(26)); l.TextAlign = ContentAlignment.MiddleLeft;
            var btn = new Button { Width = Ui.P(130), Height = Ui.P(30), FlatStyle = FlatStyle.Flat, Font = Ui.Small };
            btn.FlatAppearance.BorderColor = Color.Gray;
            btn.Tag = "swatch";
            Action refresh = () =>
            {
                Color c = get();
                btn.BackColor = c;
                btn.ForeColor = (c.R * 299 + c.G * 587 + c.B * 114) / 1000 > 140 ? Color.Black : Color.White;
                btn.Text = followsSystem != null && followsSystem() ? L.T("跟隨系統") : PopupStyle.ColorToString(c);
            };
            refreshSwatches.Add(refresh);
            btn.Click += delegate
            {
                using (var dlg = new ColorDialog { Color = get(), FullOpen = true, AnyColor = true })
                {
                    if (dlg.ShowDialog(this) != DialogResult.OK) return;
                    set(dlg.Color);
                    refresh();
                    Changed();
                }
            };
            refresh();
            flow.Controls.Add(l); flow.Controls.Add(btn);
            return flow;
        }

        void DrawPreview(Graphics g, Rectangle area)
        {
            g.Clear(Theme.Back);
            using (Font rowFont = work.MakeFont(FontStyle.Regular))
            using (Font headerFont = work.MakeFont(FontStyle.Bold))
            {
                int w = Math.Min(area.Width, Ui.P(work.Width));
                var outer = new Rectangle(area.X, area.Y, w, area.Height);
                int headerH = TextRenderer.MeasureText("範", headerFont).Height + Ui.P(12);
                int rowH = PopupRenderer.RowHeight(rowFont);
                using (var b = new SolidBrush(work.HeaderBack)) g.FillRectangle(b, outer);
                var inner = Rectangle.Inflate(outer, -1, -1);
                var hr = new Rectangle(inner.X, inner.Y, inner.Width, headerH);
                TextRenderer.DrawText(g, L.AppName + "　" + L.F("代碼：{0}", "1") + "▌　(3/12)", headerFont, new Rectangle(hr.X + Ui.P(8), hr.Y, hr.Width - Ui.P(8), hr.Height), work.HeaderFore,
                    TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix);
                var body = new Rectangle(inner.X, hr.Bottom, inner.Width, inner.Bottom - hr.Bottom);
                using (var b = new SolidBrush(work.BackR)) g.FillRectangle(b, body);
                string[,] rows = {
                    { "1", L.T("法院公鑒"), L.T("此致 ⏎ 臺灣臺北地方法院　公鑒") },
                    { "12", L.T("審閱檢查"), L.T("幫我用 /legal-reviewer審閱、檢查一下") },
                    { "111", L.T("聯絡資訊"), L.T("電子郵件：name@example.com") },
                };
                int y = body.Y;
                for (int i = 0; i < rows.GetLength(0) && y + rowH <= body.Bottom; i++, y += rowH)
                    PopupRenderer.DrawRow(g, new Rectangle(body.X, y, body.Width, rowH), rows[i, 0], rows[i, 1], rows[i, 2], i == 0, false, work, rowFont);
                if (y + rowH <= body.Bottom)
                {
                    var pr = new Rectangle(body.X, y + Ui.P(6), body.Width, body.Bottom - y - Ui.P(6));
                    using (var b = new SolidBrush(PopupStyle.Blend(work.BackR, work.ForeR, 0.06f))) g.FillRectangle(b, pr);
                    TextRenderer.DrawText(g, L.T("此致\n臺灣臺北地方法院　公鑒"), rowFont, Rectangle.Inflate(pr, -Ui.P(8), -Ui.P(4)), work.ForeR, TextFormatFlags.NoPrefix | TextFormatFlags.WordBreak);
                }
            }
        }

        static Label Lbl(string s)
        {
            return new Label { Text = s, AutoSize = true, Anchor = AnchorStyles.Left, Margin = new Padding(Ui.P(3), Ui.P(8), Ui.P(10), Ui.P(6)) };
        }

        static Label Hint(string s)
        {
            return new Label { Text = s, AutoSize = true, ForeColor = Theme.SubFore, Font = Ui.Small, Margin = new Padding(Ui.P(3), Ui.P(2), Ui.P(3), Ui.P(14)) };
        }

        static Control Suffix(Control c, string s)
        {
            var flow = new FlowLayoutPanel { AutoSize = true, WrapContents = false, Margin = Padding.Empty };
            flow.Controls.Add(c);
            flow.Controls.Add(new Label { Text = s, AutoSize = true, Margin = new Padding(Ui.P(4), Ui.P(8), 0, 0) });
            return flow;
        }
    }

    // ───────────── 範本管理視窗（左：範本清單；右：直接編輯） ─────────────
    class ManagerForm : Form
    {
        readonly AppContext app;
        readonly ToolStrip bar = new ToolStrip();
        readonly ToolStripTextBox search = new ToolStripTextBox();
        readonly NumericUpDown uiSize = new NumericUpDown();
        readonly SplitContainer split = new SplitContainer();
        readonly ListView lv = new ListView();
        readonly Label edHeader = new Label();
        readonly TextBox edTitle = new TextBox();
        readonly TextBox edCode = new TextBox();
        readonly TextBox edText = new TextBox();
        readonly Label edInfo = new Label();
        readonly Button btnSave = new Button();
        readonly Button btnRevert = new Button();
        readonly TableLayoutPanel editor = new TableLayoutPanel();
        readonly StatusStrip status = new StatusStrip();
        readonly ToolStripStatusLabel stCount = new ToolStripStatusLabel();
        readonly ToolStripStatusLabel stState = new ToolStripStatusLabel();
        readonly ToolStripStatusLabel stCopy = new ToolStripStatusLabel();
        readonly System.Windows.Forms.Timer watch = new System.Windows.Forms.Timer { Interval = 3000 };
        List<Template> view = new List<Template>();
        int sortCol = -1;
        bool sortAsc = true;

        // 右側編輯區的狀態
        Template editing;      // 正在編輯的那一筆（新範本草稿時為 null）
        bool isDraft;          // 是否為尚未存檔的新範本
        bool loadingEditor;    // 程式正在填入欄位（不算使用者修改）
        bool dirty;            // 有未儲存的修改
        bool suppressSelect;   // 程式自己在改選取，不要觸發切換
        Template lastSelected; // 上一次選取的範本（切換被取消時要選回去）

        public ManagerForm(AppContext app)
        {
            this.app = app;
            Font = Ui.Base;
            Icon = app.AppIcon;
            StartPosition = FormStartPosition.CenterScreen;
            ClientSize = Ui.FitScreen(Ui.PS(1120), Ui.PS(640));
            MinimumSize = new Size(Ui.P(720), Ui.P(420));
            KeyPreview = true;
            UpdateTitle();

            BuildToolbar();
            BuildList();
            BuildEditor();
            BuildStatus();

            split.Dock = DockStyle.Fill;
            split.Orientation = Orientation.Vertical;
            split.Panel1.Controls.Add(lv);
            split.Panel2.Controls.Add(editor);
            split.Panel2.Padding = new Padding(Ui.P(12), Ui.P(8), Ui.P(12), Ui.P(8));
            Controls.Add(split);
            Controls.Add(status);
            Controls.Add(bar);
            Load += delegate { split.SplitterDistance = (int)(split.Width * 0.40); };

            // 雲端同步進來的變更：比對版本號（別的地方先讀到新檔時，EnsureLoaded 在這裡會回傳 false，不能只看回傳值）
            watch.Tick += delegate { CheckStore(); };
            Activated += delegate { CheckStore(); };
            FormClosing += (o, e) => { if (!ConfirmLeaveEditor()) e.Cancel = true; };
            FormClosed += delegate { watch.Stop(); watch.Dispose(); };
            Shown += delegate { lv.Focus(); };
            watch.Start();
            app.Store.EnsureLoaded(DataPath);
            RefreshList(null);
            ShowEmptyEditor();
            Theme.Apply(this);
        }

        string DataPath { get { return app.Settings.DataPath; } }
        int seenVersion = -1;

        void CheckStore()
        {
            bool changed = app.Store.EnsureLoaded(DataPath);
            if (changed || app.Store.Version != seenVersion) ReloadFromStore();
        }

        public void UpdateTitle() { Text = L.F("{0}－句庫管理（叫出清單的快速鍵：{1}）", L.AppName, app.Settings.Hotkey); }

        // ── 版面 ──
        void BuildToolbar()
        {
            bar.GripStyle = ToolStripGripStyle.Hidden;
            bar.Padding = new Padding(Ui.P(6), Ui.P(4), Ui.P(6), Ui.P(4));
            bar.Font = Ui.Base;
            bar.Items.Add(ToolBtn(L.T("＋ 新增範本"), delegate { NewDraft(); }, L.T("新增一筆範本（Ctrl+N）")));
            bar.Items.Add(ToolBtn(L.T("刪除"), delegate { DeleteSelected(); }, L.T("刪除選取的範本（Delete）")));
            bar.Items.Add(new ToolStripSeparator());
            bar.Items.Add(ToolBtn(L.T("匯入 CSV…"), delegate { ImportCsv(); }, null));
            bar.Items.Add(ToolBtn(L.T("匯出 CSV…"), delegate { ExportCsv(); }, null));
            bar.Items.Add(new ToolStripSeparator());
            bar.Items.Add(new ToolStripLabel(L.T("搜尋")));
            search.AutoSize = false;
            search.Width = Ui.P(240);
            search.BorderStyle = BorderStyle.FixedSingle;
            search.TextChanged += delegate { RefreshList(editing); };
            SetCue(search.TextBox, L.T("名稱、代碼或內容"));
            bar.Items.Add(search);

            var settingsBtn = ToolBtn(L.T("設定…"), delegate { app.OpenSettings(this); UpdateTitle(); }, null);
            settingsBtn.Alignment = ToolStripItemAlignment.Right;
            bar.Items.Add(settingsBtn);
            // 字級：調了立刻套用到本視窗，並存進設定
            uiSize.DecimalPlaces = 1; uiSize.Increment = 0.5m; uiSize.Minimum = 8; uiSize.Maximum = 28;
            uiSize.Value = (decimal)Ui.ContentSize;
            uiSize.Width = Ui.P(64);
            uiSize.ValueChanged += delegate { app.SetContentFontSize((float)uiSize.Value); };
            var host = new ToolStripControlHost(uiSize) { Alignment = ToolStripItemAlignment.Right, Margin = new Padding(0, Ui.P(2), Ui.P(10), Ui.P(2)) };
            bar.Items.Add(host);
            bar.Items.Add(new ToolStripLabel(L.T("內容字級")) { Alignment = ToolStripItemAlignment.Right, ToolTipText = L.T("範本清單與編輯區的文字大小（不影響視窗與按鈕的大小）") });
        }

        static ToolStripButton ToolBtn(string text, EventHandler h, string tip)
        {
            var b = new ToolStripButton(text) { DisplayStyle = ToolStripItemDisplayStyle.Text, Padding = new Padding(Ui.P(6), 0, Ui.P(6), 0) };
            if (tip != null) b.ToolTipText = tip;
            b.Click += h;
            return b;
        }

        void BuildList()
        {
            lv.Dock = DockStyle.Fill;
            lv.View = View.Details;
            lv.FullRowSelect = true;
            lv.HideSelection = false;
            lv.MultiSelect = true;
            lv.BorderStyle = BorderStyle.None;
            lv.Font = Ui.Content;
            float k = Ui.ContentSize / Ui.DefaultUiSize;
            lv.Columns.Add(L.T("代碼"), (int)(Ui.P(110) * k));
            lv.Columns.Add(L.T("名稱"), (int)(Ui.P(300) * k));
            lv.ClientSizeChanged += delegate { FitNameColumn(); };
            lv.SelectedIndexChanged += delegate { OnSelectionChanged(); };
            lv.DoubleClick += delegate { if (!isDraft && editing != null) { edText.Focus(); edText.SelectionStart = 0; } };
            lv.ColumnClick += (o, e) =>
            {
                if (sortCol == e.Column) sortAsc = !sortAsc; else { sortCol = e.Column; sortAsc = true; }
                RefreshList(editing);
            };
        }

        // 「名稱」欄自動填滿清單剩下的寬度
        void FitNameColumn()
        {
            if (lv.Columns.Count < 2) return;
            int w = lv.ClientSize.Width - lv.Columns[0].Width - 2;
            if (w > Ui.P(80)) lv.Columns[1].Width = w;
        }

        void BuildEditor()
        {
            editor.Dock = DockStyle.Fill;
            editor.ColumnCount = 2;
            editor.RowCount = 6;
            editor.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            editor.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            for (int i = 0; i < 4; i++) editor.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            editor.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            editor.RowStyles.Add(new RowStyle(SizeType.AutoSize));

            edHeader.AutoSize = true;
            edHeader.Font = new Font(Ui.Base, FontStyle.Bold);
            edHeader.Margin = new Padding(Ui.P(2), Ui.P(4), 0, Ui.P(10));
            editor.Controls.Add(edHeader, 0, 0); editor.SetColumnSpan(edHeader, 2);

            edTitle.Dock = edCode.Dock = DockStyle.Fill;
            edTitle.Font = edCode.Font = edText.Font = Ui.Content;
            edCode.ImeMode = ImeMode.Disable;
            edCode.MaxLength = 20;
            editor.Controls.Add(Lbl(L.T("名稱")), 0, 1); editor.Controls.Add(edTitle, 1, 1);
            editor.Controls.Add(Lbl(L.T("代碼")), 0, 2); editor.Controls.Add(edCode, 1, 2);
            var hint = new Label
            {
                Text = L.T("英文字母、數字或符號，最多 20 碼（不分大小寫）。叫出清單後打這組代碼就會直接輸出。"),
                AutoSize = true, ForeColor = Theme.SubFore, Font = Ui.Small, Margin = new Padding(Ui.P(3), 0, 0, Ui.P(8))
            };
            editor.Controls.Add(hint, 1, 3);

            edText.Dock = DockStyle.Fill;
            edText.Multiline = true;
            edText.AcceptsReturn = true;
            edText.AcceptsTab = true;
            edText.ScrollBars = ScrollBars.Vertical;
            edText.WordWrap = true;
            var lc = Lbl(L.T("內容")); lc.Anchor = AnchorStyles.Top | AnchorStyles.Left; lc.Margin = new Padding(Ui.P(3), Ui.P(6), Ui.P(10), 0);
            editor.Controls.Add(lc, 0, 4); editor.Controls.Add(edText, 1, 4);

            var bottom = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 3, AutoSize = true, Margin = new Padding(0, Ui.P(8), 0, 0) };
            bottom.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            bottom.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            bottom.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            edInfo.AutoSize = true; edInfo.Anchor = AnchorStyles.Left; edInfo.ForeColor = Theme.SubFore;
            btnRevert.Text = L.T("復原"); btnSave.Text = L.T("儲存");
            foreach (Button b in new[] { btnRevert, btnSave })
            {
                b.AutoSize = true; b.MinimumSize = new Size(Ui.P(96), Ui.P(34)); b.Margin = new Padding(Ui.P(8), 0, 0, 0);
            }
            btnSave.Click += delegate { SaveEditor(); };
            btnRevert.Click += delegate { RevertEditor(); };
            bottom.Controls.Add(edInfo, 0, 0); bottom.Controls.Add(btnRevert, 1, 0); bottom.Controls.Add(btnSave, 2, 0);
            editor.Controls.Add(bottom, 0, 5); editor.SetColumnSpan(bottom, 2);

            EventHandler changed = delegate { if (!loadingEditor) { dirty = true; UpdateEditorState(); } };
            edTitle.TextChanged += changed; edCode.TextChanged += changed; edText.TextChanged += changed;
        }

        void BuildStatus()
        {
            status.SizingGrip = true;
            stState.Spring = true;
            stState.TextAlign = ContentAlignment.MiddleLeft;
            stCopy.IsLink = true;
            stCopy.LinkColor = Theme.SubFore;
            stCopy.ActiveLinkColor = Theme.SubFore;
            stCopy.LinkBehavior = LinkBehavior.HoverUnderline;
            stCopy.Text = L.Copyright;
            stCopy.Click += delegate { app.ShowAbout(this); };
            status.Items.Add(stCount);
            status.Items.Add(stState);
            status.Items.Add(stCopy);
        }

        static Label Lbl(string s)
        {
            return new Label { Text = s, AutoSize = true, Anchor = AnchorStyles.Left, Margin = new Padding(Ui.P(3), Ui.P(6), Ui.P(10), Ui.P(6)) };
        }

        [DllImport("user32.dll", CharSet = CharSet.Unicode)] static extern IntPtr SendMessage(IntPtr h, int msg, IntPtr w, string l);
        static void SetCue(TextBox tb, string cue)
        {
            if (tb.IsHandleCreated) SendMessage(tb.Handle, 0x1501 /* EM_SETCUEBANNER */, (IntPtr)1, cue);
            else tb.HandleCreated += delegate { SendMessage(tb.Handle, 0x1501, (IntPtr)1, cue); };
        }

        // 字級變更：整個視窗換字型，欄寬等比例放大縮小
        // 範本文字字級變更：只換清單與編輯區的字型，視窗大小不變
        public void ApplyContentFont()
        {
            float ratio = Ui.Content.Size / lv.Font.Size;
            if (Math.Abs(ratio - 1f) < 0.001f) return;
            lv.BeginUpdate();
            lv.Font = Ui.Content;
            lv.Columns[0].Width = (int)(Ui.P(110) * Ui.ContentSize / Ui.DefaultUiSize);
            lv.EndUpdate();
            FitNameColumn();
            bool wasLoading = loadingEditor; loadingEditor = true;   // 換字型不算修改
            edTitle.Font = edCode.Font = edText.Font = Ui.Content;
            loadingEditor = wasLoading;
            if (uiSize.Value != (decimal)Ui.ContentSize) uiSize.Value = (decimal)Ui.ContentSize;
        }

        public void ApplyUiFont()
        {
            float ratio = Ui.Base.Size / Font.Size;
            if (Math.Abs(ratio - 1f) < 0.001f) return;
            SuspendLayout();
            Font = Ui.Base;
            bar.Font = Ui.Base;
            status.Font = Ui.Base;
            Font oldHeader = edHeader.Font;
            edHeader.Font = new Font(Ui.Base, FontStyle.Bold);
            oldHeader.Dispose();
            if (WindowState == FormWindowState.Normal)
            {
                Rectangle wa = Screen.FromControl(this).WorkingArea;
                int w = Math.Min(wa.Width, Math.Max(MinimumSize.Width, (int)(Width * ratio)));
                int h = Math.Min(wa.Height, Math.Max(MinimumSize.Height, (int)(Height * ratio)));
                Size = new Size(w, h);
                if (Right > wa.Right) Left = Math.Max(wa.Left, wa.Right - Width);
                if (Bottom > wa.Bottom) Top = Math.Max(wa.Top, wa.Bottom - Height);
            }
            ResumeLayout(true);
        }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            if (e.Control && e.KeyCode == Keys.N) { NewDraft(); e.Handled = e.SuppressKeyPress = true; }
            else if (e.Control && e.KeyCode == Keys.S) { SaveEditor(); e.Handled = e.SuppressKeyPress = true; }
            else if (e.Control && e.KeyCode == Keys.F) { search.Focus(); e.Handled = e.SuppressKeyPress = true; }
            else if (lv.Focused && e.KeyCode == Keys.Delete) { DeleteSelected(); e.Handled = true; }   // 只在焦點位於清單時
            base.OnKeyDown(e);
        }

        // ── 清單 ──
        public void ReloadFromStore()
        {
            // 同步進來的新資料：編輯中的那筆若沒有修改，就換成新讀到的同一筆
            if (editing != null && !dirty)
            {
                Template fresh = null;
                foreach (Template t in app.Store.Items) if (t.SameAs(editing)) { fresh = t; break; }
                editing = fresh;
            }
            RefreshList(editing);
            if (!dirty)
            {
                if (editing != null) LoadEditor(editing, false);
                else if (!isDraft) ShowEmptyEditor();
            }
            UpdateStatus();
        }

        void RefreshList(Template select)
        {
            string q = search.Text.Trim();
            view = new List<Template>();
            foreach (Template t in app.Store.Items)
            {
                if (q.Length == 0 || t.Title.IndexOf(q, StringComparison.OrdinalIgnoreCase) >= 0
                    || t.Code.IndexOf(q, StringComparison.OrdinalIgnoreCase) >= 0
                    || t.Text.IndexOf(q, StringComparison.OrdinalIgnoreCase) >= 0) view.Add(t);
            }
            if (sortCol >= 0)
            {
                int col = sortCol; int dir = sortAsc ? 1 : -1;
                var idx = new Dictionary<Template, int>();
                for (int i = 0; i < view.Count; i++) idx[view[i]] = i;
                view.Sort((a, b) =>
                {
                    string x = col == 0 ? a.Code : a.DisplayTitle;
                    string y = col == 0 ? b.Code : b.DisplayTitle;
                    int r = CompareNatural(x, y);
                    return r != 0 ? r * dir : idx[a] - idx[b];
                });
            }

            seenVersion = app.Store.Version;
            suppressSelect = true;
            lv.BeginUpdate();
            lv.Items.Clear();
            bool selectedOne = false;
            foreach (Template t in view)
            {
                var it = new ListViewItem(t.Code.Length > 0 ? t.Code : "—");
                it.SubItems.Add(t.DisplayTitle);
                it.Tag = t;
                if (t.Code.Length == 0) it.ForeColor = Theme.SubFore;
                if (!selectedOne && select != null && (select == t || select.SameAs(t))) { it.Selected = true; it.Focused = true; selectedOne = true; }
                lv.Items.Add(it);
            }
            lv.EndUpdate();
            FitNameColumn();   // 筆數變多出現捲軸時，重新配欄寬
            suppressSelect = false;
            if (lv.SelectedItems.Count > 0) lv.SelectedItems[0].EnsureVisible();
            lastSelected = selectedOne ? SelectedTemplate() : null;
            UpdateStatus();
        }

        void UpdateStatus()
        {
            int total = app.Store.Items.Count;
            stCount.Text = view.Count == total ? L.F("共 {0} 筆", total) : L.F("符合 {0}／{1} 筆", view.Count, total);
            if (app.Store.Error != null) { stState.Text = "⚠ " + app.Store.Error.Replace("\n", " "); stState.ForeColor = Theme.Dark ? Color.FromArgb(0xFF, 0x7B, 0x72) : Color.Firebrick; }
            else { stState.Text = ""; stState.ForeColor = Theme.Fore; }
        }

        static int CompareNatural(string a, string b)
        {
            long x, y;
            bool nx = long.TryParse(a, out x), ny = long.TryParse(b, out y);
            if (nx && ny) return x.CompareTo(y);
            if (nx != ny) return nx ? -1 : 1;
            return string.Compare(a, b, StringComparison.CurrentCulture);
        }

        Template SelectedTemplate()
        {
            return lv.SelectedItems.Count == 1 ? (Template)lv.SelectedItems[0].Tag : null;
        }

        static int IndexOfSame(List<Template> list, Template t)
        {
            for (int i = 0; i < list.Count; i++) if (list[i].SameAs(t)) return i;
            return -1;
        }

        bool selPending;   // 已排定一次「選取改變」的處理
        bool confirming;   // 「要儲存嗎？」對話框正開著

        void OnSelectionChanged()
        {
            if (suppressSelect || selPending) return;
            // SelectedIndexChanged 在點選時會先觸發「取消選取」再觸發「選取」：只排一次，延後一拍再判斷
            selPending = true;
            BeginInvoke((MethodInvoker)delegate
            {
                selPending = false;
                if (suppressSelect || IsDisposed || confirming) return;
                Template now = SelectedTemplate();
                int count = lv.SelectedItems.Count;
                if (now != null && now == editing && !isDraft) return;
                if (!ConfirmLeaveEditor())
                {
                    SelectOnly(isDraft ? null : editing);   // 使用者按取消：選回正在編輯的那筆
                    return;
                }
                // 剛才若存了檔，清單已重新整理（物件換新），用內容找回使用者點的那一筆
                if (now != null && !InList(now)) now = FindInList(now);
                lastSelected = now;
                if (now != null) { SelectOnly(now); LoadEditor(now, false); }
                else if (count > 1) ShowMultiSelected(count);
                else ShowEmptyEditor();
            });
        }

        bool InList(Template t)
        {
            foreach (ListViewItem it in lv.Items) if (it.Tag == t) return true;
            return false;
        }

        Template FindInList(Template t)
        {
            foreach (ListViewItem it in lv.Items) if (((Template)it.Tag).SameAs(t)) return (Template)it.Tag;
            return null;
        }

        void SelectOnly(Template t)
        {
            suppressSelect = true;
            foreach (ListViewItem it in lv.Items)
            {
                bool on = t != null && it.Tag == t;
                if (it.Selected != on) it.Selected = on;
                if (on) { it.Focused = true; it.EnsureVisible(); }
            }
            suppressSelect = false;
        }

        // ── 右側編輯區 ──
        void SetEditorEnabled(bool on)
        {
            edTitle.Enabled = edCode.Enabled = edText.Enabled = on;
        }

        void Fill(string title, string code, string text)
        {
            loadingEditor = true;
            edTitle.Text = title; edCode.Text = code; edText.Text = text.Replace("\n", "\r\n");
            loadingEditor = false;
        }

        void LoadEditor(Template t, bool focus)
        {
            editing = t; isDraft = false; dirty = false;
            Fill(t.Title, t.Code, t.Text);
            edHeader.Text = L.T("編輯範本");
            SetEditorEnabled(true);
            UpdateEditorState();
            if (focus) edText.Focus();
        }

        void ShowEmptyEditor()
        {
            editing = null; isDraft = false; dirty = false;
            Fill("", "", "");
            edHeader.Text = L.T("在左側點選一筆範本即可編輯，或按「＋ 新增範本」。");
            SetEditorEnabled(false);
            UpdateEditorState();
        }

        void ShowMultiSelected(int n)
        {
            editing = null; isDraft = false; dirty = false;
            Fill("", "", "");
            edHeader.Text = L.F("已選取 {0} 筆範本（可按「刪除」一次刪除）", n);
            SetEditorEnabled(false);
            UpdateEditorState();
        }

        void NewDraft()
        {
            if (!ConfirmLeaveEditor()) return;
            SelectOnly(null);
            lastSelected = null;
            editing = null; isDraft = true; dirty = false;
            Fill("", "", "");
            edHeader.Text = L.T("新增範本");
            SetEditorEnabled(true);
            UpdateEditorState();
            edTitle.Focus();
        }

        void UpdateEditorState()
        {
            bool active = isDraft || editing != null;
            btnSave.Enabled = active && (dirty || isDraft);
            btnRevert.Enabled = active && dirty;
            if (!active) { edInfo.Text = ""; return; }
            string info = L.F("字數：{0}", Template.NormalizeNewlines(edText.Text).Length);
            if (dirty) info += L.T("　（尚未儲存，Ctrl+S 儲存）");
            edInfo.Text = info;
        }

        Template FindSame(Template t)
        {
            foreach (Template x in app.Store.Items) if (x.SameAs(t)) return x;
            return null;
        }

        void RevertEditor()
        {
            if (isDraft) { Fill("", "", ""); dirty = false; UpdateEditorState(); return; }
            if (editing != null) LoadEditor(editing, false);
        }

        // 回傳 true 表示已存檔（或沒有需要存的）
        bool SaveEditor()
        {
            if (!isDraft && editing == null) return true;
            if (!dirty && !isDraft) return true;
            Control bad;
            Template r = EditForm.BuildValidated(this, Text, edTitle.Text, edCode.Text, edText.Text,
                                                 isDraft ? null : editing, app.Store.Items, edTitle, edCode, edText, out bad);
            if (r == null) { if (bad != null) bad.Focus(); return false; }
            Template original = editing;
            bool wasDraft = isDraft;
            bool ok = app.Store.Mutate(DataPath, list =>
            {
                int i = wasDraft ? -1 : IndexOfSame(list, original);
                if (i >= 0) list[i] = r; else list.Add(r);   // 原本那筆已在別台電腦被改掉時，當作新增
            }, r.Text);
            if (!ok) return false;
            if (wasDraft && search.Text.Length > 0) search.Text = "";
            Template saved = null;
            foreach (Template t in app.Store.Items) if (t.SameAs(r)) { saved = t; break; }
            RefreshList(saved ?? r);
            LoadEditor(saved ?? r, false);
            stState.Text = L.T("已儲存。");
            return true;
        }

        // 離開目前編輯的範本前，確認是否要儲存；回傳 false 表示使用者取消
        bool ConfirmLeaveEditor()
        {
            if (!dirty) return true;
            if (confirming) return false;   // 已經在問了，不要疊第二個對話框
            confirming = true;
            try
            {
                string name = isDraft ? L.T("新範本") : (editing != null ? editing.DisplayTitle : "");
                var r = MessageBox.Show(this, L.F("「{0}」有尚未儲存的修改，要儲存嗎？", name), L.AppName,
                                        MessageBoxButtons.YesNoCancel, MessageBoxIcon.Question);
                if (r == DialogResult.Cancel) return false;
                if (r == DialogResult.Yes) return SaveEditor();
                RevertEditor();   // 放棄修改：編輯區恢復成原本的內容，免得之後又被一起存進去
                return true;
            }
            finally { confirming = false; }
        }

        // ── 刪除、匯入、匯出 ──
        void DeleteSelected()
        {
            var targets = new List<Template>();
            foreach (ListViewItem it in lv.SelectedItems) targets.Add((Template)it.Tag);
            if (targets.Count == 0) { MessageBox.Show(this, L.T("請先在左側點選要刪除的範本（按住 Ctrl 或 Shift 可以多選）。"), L.AppName); return; }
            bool editingDeleted = editing != null && targets.Contains(editing);
            if (dirty && !editingDeleted && !ConfirmLeaveEditor()) return;   // 另一筆有沒存的修改，先處理它
            string msg = targets.Count == 1
                ? L.F("確定要刪除「{0}」嗎？", targets[0].DisplayTitle)
                : L.F("確定要刪除選取的 {0} 筆範本嗎？", targets.Count);
            if (MessageBox.Show(this, msg, L.AppName, MessageBoxButtons.YesNo, MessageBoxIcon.Warning, MessageBoxDefaultButton.Button2) != DialogResult.Yes) return;
            if (app.Store.Mutate(DataPath, list => { foreach (Template t in targets) { int i = IndexOfSame(list, t); if (i >= 0) list.RemoveAt(i); } }))
            {
                if (editingDeleted || (editing == null && !isDraft))
                {
                    dirty = false;
                    RefreshList(null);
                    ShowEmptyEditor();
                }
                else
                {
                    // 刪的是別筆：編輯區保留（草稿或正在編輯的那筆）
                    Template keep = editing == null ? null : FindSame(editing);
                    if (editing != null) editing = keep;
                    RefreshList(keep);
                }
            }
        }

        void ExportCsv()
        {
            app.Store.EnsureLoaded(DataPath);
            using (var d = new SaveFileDialog())
            {
                d.Filter = L.T("CSV 檔案 (*.csv)|*.csv");
                d.FileName = L.AppName + "_" + DateTime.Now.ToString("yyyyMMdd") + ".csv";
                d.InitialDirectory = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);
                if (d.ShowDialog(this) != DialogResult.OK) return;
                try
                {
                    TextFile.Write(d.FileName, Csv.Write(app.Store.Items));
                    MessageBox.Show(this, L.F("已匯出 {0} 筆範本：\n{1}", app.Store.Items.Count, d.FileName) + "\n\n" +
                        L.T("提醒：這個檔案可以用 Excel 開來看，但不建議用 Excel 修改後另存——Excel 會把代碼開頭的 0 去掉（01 變成 1）、把 1-2 當成日期、把 = 或 + 開頭的內容當成公式。要修改範本，請在「句庫管理」裡改。"), L.AppName);
                }
                catch (Exception ex) { MessageBox.Show(this, L.T("匯出失敗：") + ex.Message, L.AppName); }
            }
        }

        void ImportCsv()
        {
            if (!ConfirmLeaveEditor()) return;
            using (var d = new OpenFileDialog())
            {
                d.Filter = L.T("CSV 檔案 (*.csv)|*.csv|文字檔 (*.txt)|*.txt|所有檔案 (*.*)|*.*");
                d.InitialDirectory = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);
                if (d.ShowDialog(this) != DialogResult.OK) return;
                List<Template> incoming;
                try { incoming = Csv.ToTemplates(Csv.Parse(TextFile.Read(d.FileName))); }
                catch (Exception ex) { MessageBox.Show(this, L.T("讀取失敗：") + ex.Message, L.AppName); return; }
                Merge(incoming, Path.GetFileName(d.FileName));
            }
        }

        void Merge(List<Template> incoming, string source)
        {
            int clearedCodes = Template.NormalizeAll(incoming);   // 與存檔後再讀回的內容一致，避免重複匯入被誤判成新範本
            app.Store.EnsureLoaded(DataPath);
            if (app.Store.Error != null) { MessageBox.Show(this, app.Store.Error + "\n\n" + L.T("為避免覆蓋掉資料，這次不匯入。"), L.AppName); return; }
            List<Template> cur = app.Store.Items;
            var fresh = new List<Template>();
            var conflicts = new List<Template>();
            int dup = 0;
            foreach (Template t in incoming)
            {
                bool same = false, conflict = false;
                foreach (Template c in cur)
                {
                    if (c.Key == t.Key && c.Text == t.Text) { same = true; break; }
                    if (t.Code.Length > 0 && c.Key == t.Key) conflict = true;
                }
                // 同一批匯入資料裡的重複（不論歸在新範本或代碼衝突）也略過
                if (!same) foreach (Template f in fresh) if (f.Key == t.Key && f.Text == t.Text) { same = true; break; }
                if (!same) foreach (Template f in conflicts) if (f.Key == t.Key && f.Text == t.Text) { same = true; break; }
                if (same) dup++;
                else if (conflict) conflicts.Add(t);
                else fresh.Add(t);
            }
            if (incoming.Count == 0) { MessageBox.Show(this, L.F("在「{0}」裡沒有讀到任何範本。", source), L.AppName); return; }
            if (fresh.Count == 0 && conflicts.Count == 0)
            {
                MessageBox.Show(this, L.F("從「{0}」讀到 {1} 筆，全部都已經在清單裡了，不需要匯入。", source, incoming.Count), L.AppName);
                return;
            }
            var sb = new StringBuilder();
            sb.Append(L.F("從「{0}」讀到 {1} 筆：", source, incoming.Count)).Append("\n\n");
            sb.Append(L.F("‧新範本 {0} 筆", fresh.Count)).Append("\n");
            if (dup > 0) sb.Append(L.F("‧已經存在、略過 {0} 筆", dup)).Append("\n");
            if (clearedCodes > 0) sb.Append(L.F("‧有 {0} 筆的代碼含空白、中文或超過 20 碼，無法用來叫出，已改為無代碼（內容照常匯入）", clearedCodes)).Append("\n");
            bool replace = false;
            if (conflicts.Count > 0)
            {
                sb.Append(L.F("‧代碼跟現有範本相同、但內容不同 {0} 筆（例如代碼 {1}）", conflicts.Count, conflicts[0].Code)).Append("\n\n");
                sb.Append(L.T("代碼相同的要怎麼處理？")).Append("\n\n");
                sb.Append(L.T("「是」＝用匯入的內容取代現有那筆\n「否」＝兩筆都保留（打這組代碼時清單會停住讓你選）\n「取消」＝這次不匯入"));
                var r = MessageBox.Show(this, sb.ToString(), L.T("匯入範本"), MessageBoxButtons.YesNoCancel, MessageBoxIcon.Question);
                if (r == DialogResult.Cancel) return;
                replace = r == DialogResult.Yes;
            }
            else
            {
                sb.Append("\n").Append(L.T("確定要匯入嗎？"));
                if (MessageBox.Show(this, sb.ToString(), L.T("匯入範本"), MessageBoxButtons.OKCancel, MessageBoxIcon.Question) != DialogResult.OK) return;
            }
            bool ok = app.Store.Mutate(DataPath, list =>
            {
                // 取代時每一筆「原有的」範本只會被取代一次：同一批裡有兩筆代碼相同時，第二筆改為新增，
                // 不會把剛放進去的第一筆又蓋掉
                int originalCount = list.Count;
                var replaced = new HashSet<int>();
                foreach (Template t in conflicts)
                {
                    int at = -1;
                    if (replace)
                        for (int i = 0; i < originalCount; i++)
                            if (!replaced.Contains(i) && list[i].Key == t.Key) { at = i; break; }
                    if (at >= 0) { list[at] = t; replaced.Add(at); }
                    else list.Add(t);
                }
                list.AddRange(fresh);
            });
            if (ok)
            {
                search.Text = "";
                RefreshList(null);
                ShowEmptyEditor();
                MessageBox.Show(this, L.F("匯入完成，目前共有 {0} 筆範本。", app.Store.Items.Count), L.AppName);
            }
        }
    }

    // ───────────── 接收全域快速鍵的隱形視窗 ─────────────
    class HotkeyWindow : NativeWindow, IDisposable
    {
        public event Action<int> Pressed;   // 參數為快速鍵編號
        public HotkeyWindow() { CreateHandle(new CreateParams { Parent = new IntPtr(-3) }); } // HWND_MESSAGE
        protected override void WndProc(ref Message m)
        {
            if (m.Msg == Native.WM_HOTKEY && Pressed != null) Pressed(m.WParam.ToInt32());
            base.WndProc(ref m);
        }
        public void Dispose() { DestroyHandle(); }
    }

    // ───────────── 前導鍵組合（例如 `+1）的判斷邏輯 ─────────────
    // 按下 ` 時先扣住不送出：
    //   接著按到設定的鍵 → 觸發快速鍵（` 和那個鍵都不送出）
    //   什麼都沒按就放開 → 補送一個 `，照常打字
    //   按了其他鍵 → 先補送 `，再送出那個鍵，順序不變
    // 按著 Shift／Ctrl／Alt／Win 時按 `（例如 ～、Ctrl+`）完全不攔。
    // 這裡只做判斷、不碰系統，方便單獨測試；實際攔截與送鍵在 KeyChordHook。
    sealed class ChordLogic
    {
        public const int Pass = 0, Eat = 1, EatTapPrefix = 2, EatReplay = 3, Fire = 4;

        readonly uint[] prefixes, keys;
        readonly int[] ids;
        uint held;           // 目前按住（被扣住）的前導鍵，0 表示沒有
        bool consumed;       // 這次按住期間已經觸發過或已補送過 `
        uint swallowUp;      // 觸發鍵放開前的自動重複與放開都要吃掉，否則按久一點就會把 1 打進清單
        uint lastPrefixTime;

        public ChordLogic(uint[] prefixes, uint[] keys, int[] ids) { this.prefixes = prefixes; this.keys = keys; this.ids = ids; }

        public uint HeldPrefix { get { return held; } }

        // time：按鍵事件的時間戳（毫秒）；modifiers：當下是否按著 Shift/Ctrl/Alt/Win
        public int OnKey(uint vk, bool down, uint time, bool modifiers, out int id)
        {
            id = 0;
            if (swallowUp != 0 && vk == swallowUp) { if (!down) swallowUp = 0; return Eat; }
            if (held != 0 && vk != held && unchecked(time - lastPrefixTime) > 1500)
                held = 0;   // 按住的鍵一定會持續送出自動重複；很久沒消息表示放開的事件漏掉了（例如跳出 UAC 視窗），不要卡住
            if (held == 0)
            {
                if (down && !modifiers && Array.IndexOf(prefixes, vk) >= 0) { held = vk; consumed = false; lastPrefixTime = time; return Eat; }
                return Pass;
            }
            if (vk == held)
            {
                lastPrefixTime = time;
                if (down) return Eat;                     // 自動重複
                held = 0;
                return consumed ? Eat : EatTapPrefix;     // 單獨按一下 ` → 補送
            }
            if (!down || consumed) return Pass;
            for (int i = 0; i < keys.Length; i++)
                if (prefixes[i] == held && keys[i] == vk && !modifiers)
                {
                    consumed = true; swallowUp = vk; id = ids[i];
                    return Fire;
                }
            consumed = true;
            return EatReplay;
        }
    }

    // ───────────── 前導鍵組合的鍵盤攔截 ─────────────
    // 只有設定了 `+1 這類組合時才會啟用。攔截在獨立的執行緒上跑：Windows 規定攔截程序要很快回應，
    // 否則會默默把它移除，所以不能跟可能忙碌的主視窗執行緒擠在一起。觸發時送 WM_HOTKEY 給主程式，
    // 走跟一般快速鍵相同的路徑。本程式自己送出的按鍵（貼上、補送的 `）一律放行。
    sealed class KeyChordHook : IDisposable
    {
        readonly ChordLogic logic;
        readonly IntPtr target;
        Native.LowLevelKeyboardProc proc;   // 要留著參考，避免被記憶體回收
        IntPtr hook;
        volatile uint threadId;
        volatile bool disposed;
        uint prefixScan;   // 被扣住的 ` 的掃描碼，補送時沿用（有些輸入法看掃描碼）
        Thread thread;

        KeyChordHook(ChordLogic logic, IntPtr target) { this.logic = logic; this.target = target; }

        // 安裝失敗回傳 null
        public static KeyChordHook Start(ChordLogic logic, IntPtr target)
        {
            var h = new KeyChordHook(logic, target);
            var ready = new ManualResetEvent(false);
            h.thread = new Thread(() =>
            {
                Native.MSG msg;
                Native.PeekMessage(out msg, IntPtr.Zero, 0, 0, 0);   // 先建立訊息佇列，之後 Dispose 送的結束訊息才收得到
                h.threadId = Native.GetCurrentThreadId();
                h.proc = h.HookProc;
                h.hook = Native.SetWindowsHookEx(Native.WH_KEYBOARD_LL, h.proc, Native.GetModuleHandle(null), 0);
                ready.Set();
                if (h.hook == IntPtr.Zero) return;
                // 主程式等太久已放棄（Dispose 過）時，不要留下沒人管的攔截
                if (!h.disposed)
                    while (Native.GetMessage(out msg, IntPtr.Zero, 0, 0) > 0) { }
                Native.UnhookWindowsHookEx(h.hook);
            });
            h.thread.IsBackground = true;
            h.thread.Name = "KeyChordHook";
            h.thread.Start();
            ready.WaitOne(3000);
            if (h.hook == IntPtr.Zero) { h.Dispose(); return null; }
            return h;
        }

        IntPtr HookProc(int nCode, IntPtr wParam, IntPtr lParam)
        {
            if (nCode >= 0)
            {
                var k = (Native.KBDLLHOOKSTRUCT)Marshal.PtrToStructure(lParam, typeof(Native.KBDLLHOOKSTRUCT));
                int msg = wParam.ToInt32();
                bool down = msg == Native.WM_KEYDOWN || msg == Native.WM_SYSKEYDOWN;
                bool up = msg == Native.WM_KEYUP || msg == Native.WM_SYSKEYUP;
                if ((k.flags & Native.LLKHF_INJECTED) == 0 && (down || up))
                {
                    uint prefix = logic.HeldPrefix;
                    int id;
                    int r = logic.OnKey(k.vkCode, down, k.time, Native.ModifiersDown(), out id);
                    if (down && prefix == 0 && logic.HeldPrefix == k.vkCode) prefixScan = k.scanCode;
                    if (r == ChordLogic.Fire) Native.PostMessage(target, Native.WM_HOTKEY, (IntPtr)id, IntPtr.Zero);
                    else if (r == ChordLogic.EatTapPrefix) Send(k.vkCode, k.scanCode, false, true);
                    else if (r == ChordLogic.EatReplay)
                    {
                        // 先補送 `，再重送這個鍵的按下（放開照常由系統送出），順序才會正確
                        Send(prefix, prefixScan, false, true);
                        Send(k.vkCode, k.scanCode, (k.flags & Native.LLKHF_EXTENDED) != 0, false);
                    }
                    if (r != ChordLogic.Pass) return (IntPtr)1;
                }
            }
            return Native.CallNextHookEx(hook, nCode, wParam, lParam);
        }

        // 送出按下（withUp 時再送放開）
        static void Send(uint vk, uint scan, bool extended, bool withUp)
        {
            uint ext = extended ? Native.KEYEVENTF_EXTENDEDKEY : 0;
            Native.keybd_event((byte)vk, (byte)scan, ext, UIntPtr.Zero);
            if (withUp) Native.keybd_event((byte)vk, (byte)scan, ext | Native.KEYEVENTF_KEYUP, UIntPtr.Zero);
        }

        public void Dispose()
        {
            disposed = true;
            if (threadId != 0) Native.PostThreadMessage(threadId, Native.WM_QUIT, IntPtr.Zero, IntPtr.Zero);
            if (thread != null) thread.Join(1000);
            thread = null; threadId = 0;
        }
    }

    // ───────────── 常駐程式本體（系統匣圖示） ─────────────
    class AppContext : ApplicationContext
    {
        public Settings Settings;
        public readonly Store Store = new Store();
        public readonly Icon AppIcon = MakeIcon();
        readonly NotifyIcon tray = new NotifyIcon();
        readonly HotkeyWindow hotkeyWnd = new HotkeyWindow();
        readonly PopupForm popup;
        ManagerForm manager;
        EditForm quickAddForm;
        bool hotkeyOk;
        const int HotkeyId = 1, QuickAddId = 2;

        public AppContext()
        {
            Settings = Settings.Load() ?? new Settings();
            Ui.SetUiSize(Settings.UiFontSize);
            Ui.SetContentSize(Settings.ContentFontSize);
            AutoStart.MigrateLegacy();
            popup = new PopupForm(this);
            var dummy = popup.Handle;   // 先建立視窗，第一次叫出時比較快
            hotkeyWnd.Pressed += id => { if (id == QuickAddId) OnQuickAdd(); else OnHotkey(); };

            var menu = new ContextMenuStrip();
            var mgr = new ToolStripMenuItem(L.T("句庫管理…"), null, delegate { ShowManager(); });
            mgr.Font = new Font(mgr.Font, FontStyle.Bold);
            menu.Items.Add(mgr);
            menu.Items.Add(L.T("設定（快速鍵等）…"), null, delegate { OpenSettings(null); });
            menu.Items.Add(L.T("開啟程式所在資料夾"), null, delegate { Process.Start("explorer.exe", "\"" + Settings.BaseDir.TrimEnd('\\') + "\""); });
            menu.Items.Add(L.T("關於…"), null, delegate { ShowAbout(null); });
            menu.Items.Add(new ToolStripSeparator());
            menu.Items.Add(L.T("結束"), null, delegate { RequestExit(); });

            tray.Icon = AppIcon;
            tray.ContextMenuStrip = menu;
            Theme.ApplyMenu(menu);
            tray.Visible = true;
            tray.MouseClick += (s, e) => { if (e.Button == MouseButtons.Left) ShowManager(); };

            Reload();
            if (hotkeyOk)
                tray.ShowBalloonTip(3000, L.F("{0} 已啟動", L.FullName), L.F("按 {0} 叫出範本清單", Settings.Hotkey) +
                    (Settings.QuickAddHotkey.Length > 0 ? L.F("；反白文字後按 {0} 快速新增", Settings.QuickAddHotkey) : "") +
                    L.T("。\n點右下角的系統匣圖示可管理範本。"), ToolTipIcon.Info);
        }

        DateTime iniTime;

        public void ShowAbout(IWin32Window owner)
        {
            Version v = System.Reflection.Assembly.GetExecutingAssembly().GetName().Version;
            string msg = L.FullName + "　" + L.F("版本 {0}", v.Major + "." + v.Minor) + "\n\n" +
                         L.T("按快速鍵叫出常用句子清單，打代碼即可輸出到游標所在位置。") + "\n\n" +
                         L.Copyright + "\n" + L.T("本程式以 MIT 授權開源，可自由使用、修改與散布。");
            MessageBox.Show(owner, msg, L.F("關於 {0}", L.FullName), MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        // 管理視窗等的字體大小：立即套用；停止調整約 1 秒後才存檔，避免連續調整時雲端硬碟一直同步
        System.Windows.Forms.Timer saveTimer;
        public void SetUiFontSize(float pt)
        {
            Ui.SetUiSize(pt);
            if (manager != null && !manager.IsDisposed) manager.ApplyUiFont();
            if (Settings.UiFontSize == Ui.UiSize) return;
            Settings.UiFontSize = Ui.UiSize;
            if (saveTimer == null)
            {
                saveTimer = new System.Windows.Forms.Timer { Interval = 1000 };
                saveTimer.Tick += delegate
                {
                    saveTimer.Stop();
                    SaveFontSizes();
                };
            }
            saveTimer.Stop();
            saveTimer.Start();
        }

        void SaveFontSizes()
        {
            float ui = Settings.UiFontSize, content = Settings.ContentFontSize;
            SaveSettingsSafely(s => { s.UiFontSize = ui; s.ContentFontSize = content; });
        }

        // 句庫管理裡範本文字的字級：立即套用，延遲存檔
        public void SetContentFontSize(float pt)
        {
            Ui.SetContentSize(pt);
            if (manager != null && !manager.IsDisposed) manager.ApplyContentFont();
            if (Settings.ContentFontSize == Ui.ContentSize) return;
            Settings.ContentFontSize = Ui.ContentSize;
            ScheduleSave();
        }

        void ScheduleSave()
        {
            if (saveTimer == null)
            {
                saveTimer = new System.Windows.Forms.Timer { Interval = 1000 };
                saveTimer.Tick += delegate { saveTimer.Stop(); SaveFontSizes(); };
            }
            saveTimer.Stop();
            saveTimer.Start();
        }

        // 存設定前先讀一次最新的設定檔，只改動要改的欄位，以免蓋掉別台電腦剛做的修改；
        // 設定檔讀不到（例如同步中被鎖住）時不存，免得把預設值寫回去
        bool SaveSettingsSafely(Action<Settings> change)
        {
            Settings latest = File.Exists(Settings.IniPath) ? Settings.Load() : Settings.Clone();
            if (latest == null) return false;
            change(latest);
            try
            {
                latest.Save();
                Settings = latest;
                iniTime = File.GetLastWriteTimeUtc(Settings.IniPath);
                return true;
            }
            catch { return false; }
        }

        void Reload()
        {
            Settings loaded = Settings.Load();
            if (loaded != null)
            {
                Settings = loaded;
                try { iniTime = File.GetLastWriteTimeUtc(Settings.IniPath); } catch { }
            }
            else if (Settings == null) Settings = new Settings();   // 讀不到就先用預設值，但不記錄時間，稍後會再重讀
            Store.EnsureLoaded(Settings.DataPath);
            RegisterHotkey();
        }

        // 設定檔被另一台電腦（經雲端同步）改過時，重新讀入；讀不到時沿用目前設定，下次再試
        void ReloadSettingsIfChanged()
        {
            try
            {
                if (!File.Exists(Settings.IniPath)) return;
                DateTime t = File.GetLastWriteTimeUtc(Settings.IniPath);
                if (t == iniTime) return;
                Settings loaded = Settings.Load();
                if (loaded == null) return;
                string oldKeys = Settings.Hotkey + "|" + Settings.QuickAddHotkey;
                Settings = loaded;
                iniTime = t;
                if (oldKeys != Settings.Hotkey + "|" + Settings.QuickAddHotkey) RegisterHotkey();
                Ui.SetUiSize(Settings.UiFontSize);
                Ui.SetContentSize(Settings.ContentFontSize);
                if (manager != null && !manager.IsDisposed) { manager.ApplyUiFont(); manager.ApplyContentFont(); manager.UpdateTitle(); }
            }
            catch { }
        }

        KeyChordHook chordHook;   // 只有設定了 `+1 這類組合時才會有

        void UnregisterHotkeys()
        {
            Native.UnregisterHotKey(hotkeyWnd.Handle, HotkeyId);
            Native.UnregisterHotKey(hotkeyWnd.Handle, QuickAddId);
            if (chordHook != null) { chordHook.Dispose(); chordHook = null; }
        }

        void RegisterHotkey()
        {
            UnregisterHotkeys();
            uint mods, vk, prefix;
            hotkeyOk = false;
            bool quickOk = Settings.QuickAddHotkey.Length == 0;
            var chordPrefixes = new List<uint>(); var chordKeys = new List<uint>(); var chordIds = new List<int>();

            if (Settings.ParseChord(Settings.Hotkey, out prefix, out vk)) { chordPrefixes.Add(prefix); chordKeys.Add(vk); chordIds.Add(HotkeyId); }
            else if (!Settings.ParseHotkey(Settings.Hotkey, out mods, out vk))
                tray.ShowBalloonTip(6000, L.T("快速鍵設定有誤"), L.T("請在「設定」重新指定叫出清單的快速鍵。"), ToolTipIcon.Error);
            else if (!Native.RegisterHotKey(hotkeyWnd.Handle, HotkeyId, mods, vk))
                tray.ShowBalloonTip(6000, L.T("快速鍵被占用"), L.F("{0} 已被其他程式使用，請在「設定」改用別的組合。", Settings.Hotkey), ToolTipIcon.Error);
            else hotkeyOk = true;

            if (!quickOk)
            {
                if (Settings.ParseChord(Settings.QuickAddHotkey, out prefix, out vk)) { chordPrefixes.Add(prefix); chordKeys.Add(vk); chordIds.Add(QuickAddId); quickOk = true; }
                else quickOk = Settings.ParseHotkey(Settings.QuickAddHotkey, out mods, out vk) && Native.RegisterHotKey(hotkeyWnd.Handle, QuickAddId, mods, vk);
            }

            if (chordKeys.Count > 0)
            {
                chordHook = KeyChordHook.Start(new ChordLogic(chordPrefixes.ToArray(), chordKeys.ToArray(), chordIds.ToArray()), hotkeyWnd.Handle);
                if (chordHook != null) { if (chordIds.Contains(HotkeyId)) hotkeyOk = true; }
                else
                {
                    if (chordIds.Contains(QuickAddId)) quickOk = false;
                    if (chordIds.Contains(HotkeyId))
                        tray.ShowBalloonTip(6000, L.T("快速鍵無法啟用"), L.F("{0} 無法啟用，請在「設定」改用別的組合。", Settings.Hotkey), ToolTipIcon.Error);
                }
            }
            if (!quickOk && hotkeyOk)   // 叫出清單的快速鍵也壞掉時，已經跳過那則更重要的提示，不要蓋掉它
                tray.ShowBalloonTip(6000, L.T("快速新增的快速鍵無法使用"), L.F("{0} 設定有誤或已被其他程式使用，請在「設定」改用別的組合。", Settings.QuickAddHotkey), ToolTipIcon.Warning);
            tray.Text = L.AppName + " (" + Settings.Hotkey + ")";
        }

        void OnHotkey()
        {
            if (quickAddBusy) return;   // 快速新增正在取選取文字或開著對話框
            if (popup.Visible) { popup.Close0(true); return; }
            IntPtr fg = Native.GetForegroundWindow();
            ReloadSettingsIfChanged();
            Store.EnsureLoaded(Settings.DataPath);   // 每次叫出都檢查檔案是否被改過（含雲端同步進來的修改）
            popup.Open(fg);
        }

        // 快速新增：等使用者放開 Ctrl/Shift，再替他按 Ctrl+C 取得反白的文字
        bool quickAddBusy;   // 從按下快速鍵到對話框關閉為止都是 true，防止重複觸發

        void OnQuickAdd()
        {
            if (quickAddBusy)
            {
                if (quickAddForm != null && !quickAddForm.IsDisposed) quickAddForm.Activate();
                return;
            }
            quickAddBusy = true;
            IntPtr fg = Native.GetForegroundWindow();
            var timer = new System.Windows.Forms.Timer { Interval = 30 };
            int waited = 0;
            timer.Tick += delegate
            {
                if (Native.ModifiersDown() && waited < 1500) { waited += 30; return; }
                timer.Stop(); timer.Dispose();
                if (Native.ModifiersDown())
                {
                    // 一直按著 Ctrl/Shift 不放時不要硬送 Ctrl+C（會變成 Ctrl+Shift+C，在瀏覽器會開開發者工具）
                    quickAddBusy = false;
                    tray.ShowBalloonTip(3000, L.T("快速新增沒有執行"), L.F("請先放開 Ctrl、Shift 等按鍵，再按一次 {0}。", Settings.QuickAddHotkey), ToolTipIcon.Info);
                    return;
                }
                try { ShowQuickAdd(Paster.CopySelection(fg)); }
                finally { quickAddBusy = false; }
            };
            timer.Start();
        }

        void ShowQuickAdd(string text)
        {
            Store.EnsureLoaded(Settings.DataPath);
            try
            {
                using (quickAddForm = new EditForm(L.T("快速新增範本"), new Template { Text = text }, Store.Items, true))
                {
                    quickAddForm.StartPosition = FormStartPosition.CenterScreen;
                    quickAddForm.TopMost = true;
                    quickAddForm.ShowInTaskbar = true;
                    quickAddForm.Icon = AppIcon;
                    quickAddForm.ShowIcon = true;
                    EditForm f = quickAddForm;
                    f.Shown += delegate { f.Activate(); Native.SetForegroundWindow(f.Handle); };
                    if (f.ShowDialog() == DialogResult.OK)
                    {
                        Template r = f.Result;
                        if (Store.Mutate(Settings.DataPath, list => list.Add(r), r.Text))
                        {
                            if (manager != null && !manager.IsDisposed) manager.ReloadFromStore();
                            tray.ShowBalloonTip(2500, L.T("已新增範本"), (r.Code.Length > 0 ? L.F("代碼 {0}：", r.Code) : "") + r.DisplayTitle, ToolTipIcon.Info);
                        }
                    }
                }
            }
            finally { quickAddForm = null; }
        }

        public void ShowManager()
        {
            if (manager == null || manager.IsDisposed) manager = new ManagerForm(this);
            if (manager.WindowState == FormWindowState.Minimized) manager.WindowState = FormWindowState.Normal;
            manager.Show();
            manager.Activate();
        }

        void RequestExit()
        {
            if (quickAddForm != null && !quickAddForm.IsDisposed) { quickAddForm.Activate(); return; }
            if (settingsDialog != null && !settingsDialog.IsDisposed) { settingsDialog.Activate(); return; }
            if (manager != null && !manager.IsDisposed)
            {
                manager.Close();                                            // 有未儲存的修改時會先問
                if (!manager.IsDisposed && manager.Visible) return;         // 使用者取消
            }
            ExitThread();
        }

        SettingsForm settingsDialog;   // 同時只開一個設定視窗
        bool restartRequested;
        bool saveFailed;

        // 重新啟動：先開新的一份（帶 /restart，會等這一份結束才開始運作），再結束自己
        void BeginRestart()
        {
            if (manager != null && !manager.IsDisposed && !manager.IsHandleCreated) manager = null;
            bool reopenManager = manager != null && !manager.IsDisposed && manager.Visible;
            if (manager != null && !manager.IsDisposed)
            {
                manager.Close();                              // 有未儲存的修改時會先問
                if (!manager.IsDisposed && manager.Visible) { restartRequested = false; return; }   // 使用者取消
            }
            try { Process.Start(Application.ExecutablePath, reopenManager ? "/restart /manage" : "/restart"); }
            catch (Exception ex) { restartRequested = false; MessageBox.Show(ex.Message, L.AppName); return; }
            ExitThread();
        }

        public void OpenSettings(IWin32Window owner)
        {
            if (settingsDialog != null && !settingsDialog.IsDisposed) { settingsDialog.Activate(); return; }
            ReloadSettingsIfChanged();   // 以最新的設定（含別台電腦剛改的）為基礎
            UnregisterHotkeys();         // 錄製快速鍵時不要被自己攔截
            saveFailed = false;
            try
            {
                string origLanguage = Settings.Language, origTheme = Settings.AppTheme;
                using (settingsDialog = new SettingsForm(Settings))
                {
                    settingsDialog.Icon = AppIcon;
                    if (settingsDialog.ShowDialog(owner) == DialogResult.OK)
                    {
                        Settings result = settingsDialog.Result;
                        bool autoStartOn = settingsDialog.AutoStartOn;
                        try { AutoStart.Set(autoStartOn); } catch { }
                        if (!SaveSettingsSafely(s =>
                            {
                                s.Hotkey = result.Hotkey; s.QuickAddHotkey = result.QuickAddHotkey;
                                s.AutoCommit = result.AutoCommit; s.RestoreClipboard = result.RestoreClipboard;
                                s.UiFontSize = result.UiFontSize; s.Style = result.Style.Clone(); s.Language = result.Language;
                                s.AppTheme = result.AppTheme; s.ContentFontSize = result.ContentFontSize;
                            }))
                        {
                            Settings = result;   // 這次先照新設定運作
                            saveFailed = true;
                            MessageBox.Show(L.T("設定檔暫時無法寫入（可能正在同步），新設定這次有效，但沒有存檔；請稍後再到「設定」按一次確定。"), L.AppName);
                        }
                        popup.ApplyStyle(Settings.Style);
                        Ui.SetUiSize(Settings.UiFontSize);
                        if (manager != null && !manager.IsDisposed) manager.ApplyUiFont();
                        // 只有在這次對話框裡改了語言或外觀模式才重新啟動（別台電腦改的、Windows 自己切深淺色，都不算）；
                        // 存檔失敗時也不重啟，否則新程式會讀回舊設定
                        bool langChanged = !string.Equals(result.Language, origLanguage, StringComparison.OrdinalIgnoreCase) && L.Resolve(result.Language) != L.Code;
                        bool wantDark = result.AppTheme == "dark" || (result.AppTheme != "light" && Theme.WindowsPrefersDark());
                        bool themeChanged = !string.Equals(result.AppTheme, origTheme, StringComparison.OrdinalIgnoreCase) && wantDark != Theme.Dark;
                        if (!saveFailed && (langChanged || themeChanged)) restartRequested = true;
                        Ui.SetContentSize(Settings.ContentFontSize);
                        if (manager != null && !manager.IsDisposed) manager.ApplyContentFont();
                    }
                }
            }
            finally
            {
                settingsDialog = null;
                RegisterHotkey();   // 不論發生什麼事，都要把快速鍵註冊回來
                if (restartRequested) BeginRestart();
                if (manager != null && !manager.IsDisposed) manager.UpdateTitle();
            }
        }

        static Icon MakeIcon()
        {
            using (var bmp = new Bitmap(32, 32))
            {
                using (Graphics g = Graphics.FromImage(bmp))
                {
                    g.SmoothingMode = SmoothingMode.AntiAlias;
                    g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.AntiAliasGridFit;
                    using (var path = new GraphicsPath())
                    {
                        path.AddArc(0, 0, 12, 12, 180, 90); path.AddArc(19, 0, 12, 12, 270, 90);
                        path.AddArc(19, 19, 12, 12, 0, 90); path.AddArc(0, 19, 12, 12, 90, 90);
                        path.CloseFigure();
                        using (var b = new SolidBrush(Ui.Accent)) g.FillPath(b, path);
                    }
                    using (var f = new Font("Microsoft JhengHei UI", 17f, FontStyle.Bold, GraphicsUnit.Pixel))
                    using (var sf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center })
                        g.DrawString(L.IconGlyph, f, Brushes.White, new RectangleF(0, 1, 32, 32), sf);
                }
                IntPtr h = bmp.GetHicon();
                Icon ico = (Icon)Icon.FromHandle(h).Clone();
                Native.DestroyIcon(h);
                return ico;
            }
        }

        protected override void ExitThreadCore()
        {
            Paster.FlushPending();   // 結束前把輸出時暫借的剪貼簿還原
            if (saveTimer != null && saveTimer.Enabled) { saveTimer.Stop(); SaveFontSizes(); }
            UnregisterHotkeys();
            tray.Visible = false;
            tray.Dispose();
            hotkeyWnd.Dispose();
            base.ExitThreadCore();
        }
    }

    static class Program
    {
        [STAThread]
        static void Main(string[] args)
        {
            try { Native.SetProcessDPIAware(); } catch { }   // 必須在建立任何字型、視窗之前
            Settings early = Settings.Load();
            L.Init(early != null ? early.Language : "auto");
            Theme.Init(early != null ? early.AppTheme : "auto");
            bool restart = Array.IndexOf(args, "/restart") >= 0;
            using (var mutex = new Mutex(false, @"Local\Pocketbrief_SingleInstance"))
            {
                bool owned;
                // 重新啟動時，等前一份程式結束（最多 15 秒）
                try { owned = mutex.WaitOne(restart ? 15000 : 0); }
                catch (AbandonedMutexException) { owned = true; }   // 前一份被強制結束
                if (!owned)
                {
                    MessageBox.Show(L.F("{0}已經在執行了（請看右下角的系統匣圖示）。", L.AppName), L.AppName);
                    return;
                }
                try { RunApp(args); }
                finally { mutex.ReleaseMutex(); }
            }
        }

        static void RunApp(string[] args)
        {
            {
                using (Graphics g = Graphics.FromHwnd(IntPtr.Zero)) Ui.S = g.DpiX / 96f;
                Application.EnableVisualStyles();
                Application.SetCompatibleTextRenderingDefault(false);
                var ctx = new AppContext();
                if (Array.IndexOf(args, "/manage") >= 0) ctx.ShowManager();
                if (Array.IndexOf(args, "/settings") >= 0)
                {
                    var tm = new System.Windows.Forms.Timer { Interval = 200 };
                    tm.Tick += delegate { tm.Stop(); tm.Dispose(); ctx.OpenSettings(null); };
                    tm.Start();
                }
                Application.Run(ctx);
            }
        }
    }
}
