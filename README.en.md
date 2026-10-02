<div align="center">

# Pocketbrief（口袋句庫）

**Press a hotkey to pop up your saved phrases, type a short code, and the whole phrase is typed at your cursor.**

[繁體中文](README.md) ｜ English

![Typing a code in the pop-up list](docs/screenshots/popup-en.png)

</div>

---

## What is it?

Pocketbrief is a small Windows tray utility for the long, fixed phrases you type over and over.

Save each phrase as a template with a short **code**. Then, anywhere you can type (Word, your browser, a chat app, an AI prompt box), press `Ctrl` + `` ` `` to pop up your phrase list and type the code. The whole phrase is inserted right where your cursor is.

For example:

| You type | Pocketbrief inserts |
|---|---|
| `a` | Please turn on Track Changes so I can see what you edited. |
| `z` | Best regards,<br>[Your Name] |
| `p` | name@example.com |

It was built by a litigation lawyer for party names and contract titles in court filings, email sign-offs, canned replies, and the prompts you give AI tools every day. It works for anyone who types the same things again and again.

## Features

- ⚡ **Type a code, get the phrase**: when a code matches exactly one template, it is inserted immediately, with no Enter needed. You can also type a keyword to search names and content.
- ✂️ **Quick add**: select any text, anywhere, and press `Ctrl` + `Shift` + `0` to save it as a new template.
- ♾️ **No limits**: store as many templates as you like and use them on as many computers as you like.
- ☁️ **Sync across computers**: keep the folder in Google Drive, OneDrive or Dropbox. Edit once and every computer gets the change.
- 🔒 **Your data stays yours**: templates live in a CSV file next to the program. Pocketbrief never connects to the internet.
- ⌨️ **Configurable hotkeys**: the default `Ctrl` + `` ` `` won't fire by accident while you type. You can also use "hold `` ` `` and press a key" (e.g. `` ` `` + `1`) for an even smaller left-hand move.
- 🎨 **Customizable look**: choose the font, size, colors, width and number of rows of the pop-up list, or pick one of 10 built-in color themes, including two eye-friendly ones.
- 🌙 **Dark mode**: follow Windows or choose light or dark yourself.
- 🌐 **Four UI languages**: English, 日本語, 繁體中文, 简体中文.
- 📥 **CSV import/export**: bring templates over from other tools or back them up anywhere.
- 🛡️ **Safe saving**: every save goes through a temporary file, so you never end up with a half-written file. A backup is also made automatically before the first change of each day, and the last 14 days are kept.

## Screenshots

**Phrase Library** lists your templates on the left; edit on the right and press `Ctrl` + `S` to save.

![Phrase Library](docs/screenshots/manager-en.png)

**List Appearance** lets you apply one of 10 color themes with a single click, fine-tune any color, and watch a live preview.

![List Appearance settings](docs/screenshots/settings-appearance-en.png)

---

## Download & install

### Requirements

- Windows 10 or Windows 11
- .NET Framework 4.8 (already built into Windows 10/11)
- macOS and Linux are **not** supported

### Steps

1. Download the latest zip from [Releases](../../releases/latest).
2. Unzip it into any folder. **To sync across computers, unzip it into your cloud-sync folder.**
3. Double-click `Pocketbrief.exe`. No installation is needed.
4. A tray icon appears at the bottom right, which means Pocketbrief is running.

> [!NOTE]
> **Windows may show "Windows protected your PC".**
> Pocketbrief is not code-signed, so Windows SmartScreen may warn you the first time you run it. Click **More info → Run anyway**.
> The full source code is public, so you can also [build it yourself](#build-from-source).

### Start with Windows

Open **Settings → Hotkeys & Behavior** and check **Start automatically when Windows starts**. This is set separately on each computer.

### Language

Pocketbrief follows your Windows display language. To change it, open **Settings → Hotkeys & Behavior → Language**. The program restarts automatically.

---

## How to use

### Basics

| To… | Do this |
|---|---|
| Show the phrase list | Press `Ctrl` + `` ` `` (the key below `Esc`) wherever you're typing |
| Insert a phrase | Type its code. It's inserted as soon as only one template matches; otherwise pick one with `↑` `↓` and press `Enter` |
| Search | If what you type doesn't match any code, names and content are searched instead (e.g. type `pdf`) |
| Cancel | `Esc` |
| Quick-add a template | Select text, then press `Ctrl` + `Shift` + `0` |
| Open the Phrase Library | Click the tray icon, or press `F2` in the list |
| Open Settings | **Settings…** at the top right of the Phrase Library, or right-click the tray icon |

### Phrase Library

- **Add**: click **+ New template** (`Ctrl` + `N`), fill in the name, code and content on the right, then click **Save** (`Ctrl` + `S`).
- **Edit**: click a template on the left and edit it on the right. If you switch away with unsaved changes, Pocketbrief asks first.
- **Delete**: select templates and click **Delete** (`Delete` key). Hold `Ctrl` or `Shift` to select several.
- **Sort**: click a column header to sort by code or name.
- **Text size**: the **Text size** box on the toolbar enlarges only the list and editor text, not the window or buttons.

### Tips for choosing codes

- Give your most-used templates codes on the **left side** of the keyboard (`a`, `s`, `z`, `x`…) so your left hand can reach them right after the hotkey.
- Let related templates share a prefix, e.g. `a`, `aa`, `as`, `ass`.
- When codes share a prefix (e.g. `a` and `aa`), typing `a` pauses the list. Press `Enter` to insert `a`, or keep typing.
- Codes may contain letters, digits and half-width symbols, up to 20 characters, and are case-insensitive. Spaces, CJK characters and `` ` `` are not allowed.

### Using `` ` `` as a hotkey prefix

Besides `Ctrl`/`Alt` combinations, you can set a hotkey as "hold `` ` `` and press a key", e.g. `` ` `` + `1`, so your left hand doesn't have to reach for `Ctrl`. To set it, click the hotkey box in Settings, hold `` ` `` and press `1`.

With a combination like this:

- Tapping `` ` `` on its own still types `` ` ``; it just appears when you release the key, and holding it down doesn't repeat.
- If you press another key while holding `` ` `` (e.g. `a`), you get "`` ` ``a" in order, so typing isn't affected.
- `Shift` + `` ` `` (~), `Ctrl` + `` ` `` and similar combinations are unaffected.
- If you type fast and hit `1` before releasing `` ` `` (e.g. typing `` `1` `` in Markdown), it counts as the hotkey.

### First run

Try the sample library: in the Phrase Library, click **Import CSV…** and choose [`examples/sample-phrases-en.csv`](examples/sample-phrases-en.csv).

### Settings

| Tab | Options |
|---|---|
| Hotkeys & Behavior | Hotkey for the list, quick-add hotkey, window font size, phrase text size, insert immediately on a unique match, restore the clipboard after inserting, start with Windows, language, appearance (light / dark / follow Windows) |
| List Appearance | Color theme, font, font size, list width, number of rows, individual colors |

---

## Syncing across computers

Put the whole folder in a cloud-sync folder and run `Pocketbrief.exe` from there. Pocketbrief keeps these files in the same folder:

| File | Contents |
|---|---|
| `範本.csv` | All your templates |
| `設定.ini` | Hotkeys, appearance, language and other settings |
| `備份\` | Daily automatic backups (last 14 days) |

(The file names are in Chinese; this is expected.)

On your other computers, wait for the sync to finish and run the same `Pocketbrief.exe`. A change made on one computer shows up on the others the next time you open the list.

> [!TIP]
> If two computers edit at almost the same moment, your cloud service may create a conflicted copy (e.g. a file name ending in "(1)") that you'll need to merge by hand. Pocketbrief itself never treats a half-synced or empty file as real data, and never writes an empty file over your synced templates.

## Template file format

`範本.csv` is a UTF-8 CSV file. The first row holds the column names, and each following row is one template:

```csv
名稱 Name,代碼 Code,內容 Content
Track changes,a,Please turn on Track Changes so I can see what you edited.
Email sign-off,z,"Best regards,
[Your Name]"
```

- Content can span multiple lines; just wrap it in double quotes.
- When importing other CSV files, Pocketbrief detects the column order, whether there is a header row, and UTF-8 or Big5 encoding.

> [!WARNING]
> It's fine to **view** the file in Excel, but avoid **editing and re-saving** it there. Excel drops leading zeros from codes (`01` becomes `1`), turns `1-2` into a date, and treats content starting with `=` or `+` as a formula. Edit templates in the Phrase Library instead.

---

## Privacy & security

- Pocketbrief **never connects to the internet**. There is no telemetry, advertising or reporting of any kind.
- Your templates stay in the folder you choose.
- To paste a phrase into the current window, Pocketbrief briefly borrows the clipboard and restores your original clipboard about one second later.
- Because Pocketbrief registers global hotkeys and simulates `Ctrl` + `V` and `Ctrl` + `C`, a few antivirus products may flag it. The source code is fully public, so you can inspect or build it yourself.
- If you set a hotkey like "`` ` `` + a key", Pocketbrief watches the keyboard so it can tell which key follows `` ` ``. It only checks for that combination; nothing you type is recorded or sent anywhere. With `Ctrl`/`Alt` combinations it doesn't watch the keyboard at all.

## Known limitations

- Windows does not let ordinary programs send keystrokes to windows running **as administrator**, so Pocketbrief cannot paste into them.
- In dark mode, built-in Windows dialogs (confirmations, file pickers, color picker) remain light.
- Windows only.

---

## FAQ

<details>
<summary><b>Does it work alongside Chinese, Japanese or other input methods?</b></summary>

Yes. When the list pops up, Pocketbrief switches the input method to plain letters, so your codes aren't turned into other characters.
</details>

<details>
<summary><b>The hotkey conflicts with another program. What can I do?</b></summary>

Open **Settings → Hotkeys & Behavior**, click the hotkey box and press the combination you want. A "`` ` `` + a key" combination almost never clashes with other programs. Common system shortcuts such as `Ctrl` + `C` and `Ctrl` + `V` can't be used.
</details>

<details>
<summary><b>Will I lose what was on my clipboard?</b></summary>

No. About one second after inserting a phrase, Pocketbrief restores your clipboard. If you copy something else in that moment, your new copy wins. You can turn this off in Settings.
</details>

<details>
<summary><b>Can I move my phrases over from another tool?</b></summary>

Yes. Export them as CSV (with name, code and content columns) and use **Import CSV…**. If a code already exists with different content, Pocketbrief asks whether to replace it or keep both.
</details>

<details>
<summary><b>I broke or deleted something by mistake.</b></summary>

Open the `備份\` (backup) folder next to the program, find `範本_<date>.csv` from before the change, and bring it back with **Import CSV…**.
</details>

---

## Build from source

You don't need Visual Studio. The C# compiler ships with the .NET Framework built into Windows. In the project folder, run:

```bat
build.cmd
```

This produces `dist\Pocketbrief.exe`.

### Source layout

| File | Contents |
|---|---|
| `src/Pocketbrief.cs` | Main program: hotkeys, pop-up list, Phrase Library, settings, saving |
| `src/Lang.cs` | UI language switching and Traditional→Simplified Chinese conversion |
| `src/LangTable.cs` | English and Japanese translations |
| `src/Theme.cs` | Dark mode |
| `examples/` | Sample libraries (Traditional Chinese, English) |

### Contributing

- The code is C# 5 and uses only what ships with .NET Framework 4.x. There are no third-party dependencies.
- UI strings are written in Traditional Chinese inside `L.T("…")`. Add the English and Japanese translations to `LangTable.cs`; Simplified Chinese is converted automatically.
- Bug reports and suggestions are welcome in Issues.

---

## License

Pocketbrief is open source under the [MIT License](LICENSE). You're free to use, modify and distribute it.

© 2026 無名小律師 (Attorney 楊朝淵)

Pocketbrief was built by "vibe coding" with the help of AI (Claude Code).
