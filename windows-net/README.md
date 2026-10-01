<div align="center">

<img src="app/Assets/128x128.png" width="96" alt="Coucou icon">

# Coucou for Windows — .NET

**Mochi doesn't get a notch on a PC — so it lives at the top of your screen instead.**

The Windows build, rewritten on .NET: a C# host around the same island, the same
Mochi, the same sounds.

![Windows 10/11](https://img.shields.io/badge/Windows-10%2F11-0078D4?logo=windows)
![.NET 10](https://img.shields.io/badge/.NET-10-512BD4?logo=dotnet)
![WebView2](https://img.shields.io/badge/WebView2-evergreen-0078D4?logo=microsoftedge&logoColor=white)
![License: MIT](https://img.shields.io/badge/license-MIT-green)

**English** · [Italiano](README.it.md)

</div>

---

This folder is a rewrite of [`windows/`](../windows/README.md), the Tauri build (Rust +
TypeScript), and lives beside it until it has been used for a while;
everything a user sees and does is the same, so the screenshots and the usage
guide in [`windows/README.md`](../windows/README.md) apply here unchanged.

## What changed

| | Tauri build (`windows/`) | .NET build (`windows-net/`) |
|---|---|---|
| Host | Rust, Tauri 2, tokio | C# 14, .NET 10, async/await |
| Window, tray | tao / Tauri | WinForms shell (no controls) + Win32 |
| Web view | WebView2 through wry | WebView2 hosted directly (`CoreWebView2Controller`) |
| Page ⇄ host | Tauri IPC (`invoke` / `listen`) | `chrome.webview` messages |
| Dropped files | wry's OLE drop target | HTML5 drop + `CoreWebView2File` paths |
| Claude Code relay | `coucou-hook.exe` in Rust | `coucou-hook.exe` in C#, Native AOT (1.5 MB, ~20 ms) |
| Installer | NSIS `.exe` | per-user MSI (WiX), no admin prompt |
| Tests | `cargo test` | `dotnet test` (MSTest) |

The island itself — `web/` — is the Tauri build's front end, unchanged except
for `web/src/core/bridge.ts`, which now speaks to the C# host, and one fix: the
CSS loops now pause while the island is hidden, which takes a hidden island from
a few percent of a core down to nothing.

Nothing moves for the user: the same `%APPDATA%\Coucou\settings.json`, the same
Credential Manager entries (keys typed into the Tauri build keep working), the
same relay path and pipe protocol (hooks already in `~/.claude/settings.json`
keep working), the same log.

**Quit or uninstall the Tauri build before running this one** — two Coucous
would both want the relay pipe, and only the first gets it.

## Languages

English and Italian. **Settings… → General → Language** picks one, or
*Automatic* (the default), which follows Windows' display language. The island,
the settings window, the tray menu and the error messages all switch together.

The strings in the code are English and double as keys: `t("Allow")` in the
pages (`web/src/core/i18n.ts`), `Loc.T("…")` in the host (`app/Loc.cs`). A
missing translation falls back to English. Adding a language means adding a
table to each of those two files; `npm run build` refuses to build when a
string in the pages has no entry, and a test does the same for the host.

## Updating

Bump `<Version>` in `Directory.Build.props`, build the release (`npm run pack`)
and open the new MSI. It replaces the installed version in place: Coucou is
closed, updated and started again. Preferences, keys and the Claude Code hooks
are kept, and nothing needs uninstalling first. The version has to go up — an
MSI with the same version as the one installed does not replace it.

## Build it yourself

You need the [.NET 10 SDK](https://dot.net), [Node 20+](https://nodejs.org), and
the **MSVC build tools** (Visual Studio or its Build Tools, with "Desktop
development with C++") — the last only to compile the relay to a native exe.
WebView2 ships with Windows 10/11.

**In Visual Studio**, open `windows-net/Coucou.slnx` and press F5.

**From the command line**, in `windows-net/`:

| npm | dotnet | |
|---|---|---|
| `npm install` | | the pages' dependencies, in `web/` |
| `npm start` | `dotnet run --project app` | builds and runs Coucou |
| `npm test` | `dotnet test --project tests` | the tests — no Node or MSVC needed |
| `npm run build` | `dotnet build Coucou.slnx` | the app, the relay and the tests |
| `npm run pack` | `dotnet build installer -c Release` | the release, in `release/` |
| `npm run dev` | | Vite with hot reload, for the island's looks |

The npm scripts are shortcuts for the dotnet commands: Visual Studio, `dotnet`
and `npm` all run the same MSBuild build. Building the app runs `npm ci` /
`npm run build` in `web/` whenever a page source changed, and AOT-publishes
`coucou-hook.exe` next to `coucou.exe` whenever the relay changed.
`-p:SkipWeb=true` and `-p:SkipHook=true` turn either off.

To work on the island's looks with hot reload, run Coucou on Vite's pages:

```powershell
npm run dev                             # Vite on http://127.0.0.1:1420
dotnet run --project app -- --dev       # in a second terminal
```

`npm run dev` alone also serves the pages in an ordinary browser, and
`dev/upload-preview.html` replays the file-drop choreography on a loop.

### Release

`npm run pack` — that is, `dotnet build installer -c Release` — publishes the
app afresh, builds the MSI from it and leaves three files in
`windows-net/release/`:

```
Coucou-Windows-X.Y.Z-x64.msi      per-user installer, versioned
Coucou-Windows-x64.msi            the same file under the rolling name
Coucou-Windows-X.Y.Z-x64.zip      portable: unzip anywhere, run coucou.exe
```

By default the app is self-contained (~42 MB installer, nothing else needed).
Two properties change that:

```powershell
dotnet build installer -c Release -p:AppSelfContained=false   # ~3.5 MB, needs the .NET 10 Desktop Runtime
dotnet build installer -c Release -p:AppRuntime=win-arm64     # for ARM64
```

The [.NET 10 Desktop Runtime](https://dotnet.microsoft.com/download/dotnet/10.0)
is the one a framework-dependent build needs on the machine.

The MSI installs into `%LOCALAPPDATA%\Programs\Coucou`, adds a Start menu
shortcut and starts Coucou. Uninstalling closes Coucou and removes what it
created under `%LOCALAPPDATA%\Coucou` (the relay copy, the inbox, the log,
WebView2's cache); preferences and keys stay. Claude Code's `settings.json` is
never touched by the installer — see the comment at the top of
`installer/Package.wxs`.

The version lives in one place, `Directory.Build.props`.

The app icon and the tray icon are drawn in code, like Mochi itself:

```powershell
npm run icons --prefix web              # regenerates app\Assets from web\scripts\gen-icons.mjs
```

## Layout

```
windows-net/
  app/                 coucou.exe — the C# host
    Island/            the island window: placement, transparency, click-through, cursor poll
    Web/               WebView2 hosting and the page ⇄ host channel
    Hooks/             Claude Code: settings.json install (diff, backup, confirm) and the relay pipe
    Chat/              the Claude API client
    Integrations/      Stripe, GitHub, Vercel, n8n, Resend, Notion, Cal.com pollers
    Commands.cs        every command the pages can call
  hook/                coucou-hook.exe — the Claude Code relay, Native AOT
  shared/              code compiled into both exes (the pipe name)
  web/                 the island and the settings page (TypeScript, Canvas 2D, no framework)
  tests/               MSTest
  installer/           the MSI (WiX), and the release build
  package.json         npm shortcuts for the dotnet commands
```

### At rest, and when Claude asks something

There is no notch to hide in, so at rest the island is the bar macOS uses on
screens without one: 80×24 with Mochi alone (and a coloured dot when something
is waiting), widening to 240×24 with the other agents while the mouse is on it.
It sits over browser tabs and title bars, so at rest it takes no clicks beyond
its own shape.

Questions with their own answers (AskUserQuestion) are not permission requests,
whatever event Claude Code sends them with: Coucou hands them straight back, so
the choices appear where the session runs, and the island offers **Answer in
Claude**, which brings that window forward — Claude Desktop, a terminal, VS
Code. The relay finds the window by walking up its own parent processes, only
for the events that wait on the person and when a turn ends.

When a turn ends, the card shows Claude's whole last message — Claude Code
sends it as `last_assistant_message` with `Stop` — and **Open** brings the
session's window forward, or opens the project folder when no window was found.
Long text scrolls inside its card everywhere — the question, that message, an
error, and the command waiting for approval, which wraps rather than ending in
an ellipsis — while the header line and the buttons stay put.

### How the island window works

A borderless, always-on-top `WS_EX_NOACTIVATE | WS_EX_TOOLWINDOW` window with
DWM blur-behind on an empty region, so it honours alpha: WebView2 draws with a
transparent background and everything outside the island is see-through. A
cursor poll (16 ms, high-resolution timer) flips `WS_EX_TRANSPARENT |
WS_EX_LAYERED` so clicks pass through everywhere but the island — and the whole
panel takes the mouse while a button is held, so a file dragged from Explorer
can find the drop target. When the island hides, the window shrinks to a 6 px
wake strip and the poll parks: 0 % CPU.

### Log

`%LOCALAPPDATA%\Coucou\coucou.log` — hook events, permission decisions, poller
problems. It stays on your machine.
