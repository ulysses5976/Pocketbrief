// 口袋句庫 Pocketbrief：介面語言（繁體中文、English、日本語、简体中文）
// Copyright (c) 2026 無名小律師（楊朝淵律師）. Licensed under the MIT License; see the LICENSE file.
//
// 程式中的介面文字一律以繁體中文撰寫，並以 L.T("…")／L.F("…", 參數) 取用：
//   ‧English、日本語：查下方 LangTable 的逐句翻譯；查不到時顯示繁體中文原文。
//   ‧简体中文：先以 CnTerms 換成大陸慣用詞，再用 Windows 內建的繁簡轉換（LCMapStringEx）。
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Text;

namespace Pocketbrief
{
    static partial class L
    {
        public static readonly string[] Codes = { "zh-TW", "en", "ja", "zh-CN" };
        public static readonly string[] NativeNames = { "繁體中文", "English", "日本語", "简体中文" };

        public static string Code = "zh-TW";
        static Dictionary<string, string> table;          // en／ja 的翻譯
        static readonly Dictionary<string, string> cnCache = new Dictionary<string, string>();

        // setting：設定檔裡的 language 值（auto 或語言代碼）
        public static void Init(string setting)
        {
            Code = Resolve(setting);
            table = null;
            if (Code == "en" || Code == "ja")
            {
                int col = Code == "en" ? 1 : 2;
                table = new Dictionary<string, string>();
                for (int i = 0; i + 2 < LangTable.Length; i += 3)
                    if (!string.IsNullOrEmpty(LangTable[i + col])) table[LangTable[i]] = LangTable[i + col];
            }
        }

        public static string Resolve(string setting)
        {
            if (!string.IsNullOrEmpty(setting))
                foreach (string c in Codes) if (string.Equals(c, setting, StringComparison.OrdinalIgnoreCase)) return c;
            // auto：依 Windows 的顯示語言
            string ui = CultureInfo.CurrentUICulture.Name;
            if (ui.StartsWith("zh", StringComparison.OrdinalIgnoreCase))
            {
                bool simplified = ui.EndsWith("CN", StringComparison.OrdinalIgnoreCase) || ui.EndsWith("SG", StringComparison.OrdinalIgnoreCase)
                                  || ui.IndexOf("Hans", StringComparison.OrdinalIgnoreCase) >= 0;
                return simplified ? "zh-CN" : "zh-TW";
            }
            if (ui.StartsWith("ja", StringComparison.OrdinalIgnoreCase)) return "ja";
            return "en";
        }

        public static string T(string zh)
        {
            if (Code == "zh-TW" || zh == null) return zh;
            if (Code == "zh-CN") return ToSimplified(zh);
            string v;
            return table != null && table.TryGetValue(zh, out v) ? v : zh;
        }

        public static string F(string zh, params object[] args)
        {
            try { return string.Format(T(zh), args); }
            catch (FormatException) { return string.Format(zh, args); }   // 翻譯的佔位符寫錯時退回原文
        }

        public static string AppName
        {
            get { return Code == "zh-TW" ? "口袋句庫" : Code == "zh-CN" ? "口袋句库" : "Pocketbrief"; }
        }

        public static string FullName
        {
            get { return Code == "zh-TW" ? "口袋句庫 Pocketbrief" : Code == "zh-CN" ? "口袋句库 Pocketbrief" : Code == "ja" ? "Pocketbrief（口袋句庫）" : "Pocketbrief"; }
        }

        public static string Copyright
        {
            get
            {
                switch (Code)
                {
                    case "en": return "Copyright © 2026 無名小律師 (Attorney 楊朝淵) · MIT License";
                    case "ja": return "Copyright © 2026 無名小律師（楊朝淵弁護士）· MIT ライセンス";
                    case "zh-CN": return "著作权所有 © 2026 无名小律师（杨朝渊律师）· MIT 许可";
                    default: return "著作權所有 © 2026 無名小律師（楊朝淵律師）· MIT 授權";
                }
            }
        }

        // 系統匣圖示上的字
        public static string IconGlyph { get { return Code == "en" ? "P" : "句"; } }

        // ── 繁→簡 ──
        [DllImport("kernel32.dll", CharSet = CharSet.Unicode)]
        static extern int LCMapStringEx(string locale, uint flags, string src, int srcLen, StringBuilder dest, int destLen, IntPtr ver, IntPtr res, IntPtr param);
        const uint LCMAP_SIMPLIFIED_CHINESE = 0x02000000;

        // 台灣用語 → 大陸用語（仍以繁體寫，之後一起轉成簡體）；長的詞放前面
        static readonly string[] CnTerms =
        {
            "雲端硬碟", "雲端硬盤", "系統匣", "系統托盤", "圖示", "圖標", "存檔", "保存", "範本檔", "模板文件", "本檔", "本文件",
            "標題列底色", "標題欄底色", "標題列文字", "標題欄文字", "標題列", "標題行", "選取列", "選中行", "資料夾", "文件夾", "剪貼簿", "剪貼板", "快速鍵", "快捷鍵",
            "檔名", "文件名", "範本檔", "模板文件", "設定檔", "配置文件", "檔案", "文件", "視窗", "窗口",
            "預設", "默認", "設定", "設置", "游標", "光標", "字型", "字體", "字級", "字號", "匯入", "導入", "匯出", "導出",
            "程式", "程序", "選單", "菜單", "螢幕", "屏幕", "網路", "網絡", "支援", "支持", "儲存", "保存",
            "清單", "列表", "硬碟", "硬盤", "滑鼠", "鼠標", "軟體", "軟件", "範本", "模板", "底色", "背景色",
            "訊息", "信息", "登入", "登錄", "搜尋", "搜索", "貼上", "粘貼", "全形", "全角", "半形", "半角",
            "使用者", "用戶", "介面", "界面", "反白", "選中", "列印", "打印", "資訊", "信息",
        };

        // 介面文字用到的繁體字 → 簡體字（由 OpenCC t2s 產生；新增文字時可重新產生，缺的字會退回 Windows 內建轉換）
        const string CnFrom = "並來個側儲內兩別刪則剛動匯區啟單嗎圖執夾審寫寬尋對帶幫庫後從復應擇敗數於時暫會標機檔檢欄權沒淺灣為無現當確碼稱筆範籤紅紙級組結絡統經綠編聯腦臺與蓋藍處號螢裝裡製複視覽觀觸訊設試話認語誤調請議護讀變讓貼資輯輸這過適選還郵鈕鍵鑒長開間閱關隨電響頁須預頭題顏顯體麼點";
        const string CnTo   = "并来个侧储内两别删则刚动汇区启单吗图执夹审写宽寻对带帮库后从复应择败数于时暂会标机档检栏权没浅湾为无现当确码称笔范签红纸级组结络统经绿编联脑台与盖蓝处号萤装里制复视览观触讯设试话认语误调请议护读变让贴资辑输这过适选还邮钮键鉴长开间阅关随电响页须预头题颜显体么点";

        public static string ToSimplified(string s)
        {
            string r;
            if (cnCache.TryGetValue(s, out r)) return r;
            string t = s;
            for (int i = 0; i + 1 < CnTerms.Length; i += 2) t = t.Replace(CnTerms[i], CnTerms[i + 1]);
            // 逐字轉換（以 OpenCC 產生的對照表為準；Windows 內建轉換對「後／后」等一對多的字會保留原樣）
            var chars = t.ToCharArray();
            for (int i = 0; i < chars.Length; i++) { int k = CnFrom.IndexOf(chars[i]); if (k >= 0) chars[i] = CnTo[k]; }
            t = new string(chars);
            try
            {
                var sb = new StringBuilder(t.Length * 2 + 16);
                int n = LCMapStringEx("zh-CN", LCMAP_SIMPLIFIED_CHINESE, t, t.Length, sb, sb.Capacity, IntPtr.Zero, IntPtr.Zero, IntPtr.Zero);
                r = n > 0 ? sb.ToString(0, n) : t;
            }
            catch { r = t; }
            cnCache[s] = r;
            return r;
        }
    }
}
