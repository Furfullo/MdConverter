# MdConverter

A Windows tray app that converts terminal/console output to Markdown. Paste raw terminal text on the left, get clean Markdown on the right — with live preview.

## Features

- **Terminal → Markdown conversion**
  - Box-drawing tables (`┌─┐│`) → GFM tables
  - Horizontal rules (`────`) → `---`
  - Bullet points (`•`, `*`, `-`) → normalised `-`
  - Headers (short isolated lines) → `#` / `##`
  - Sub-headers (lines ending with `:`) → `**bold**`
- **Live HTML preview** via embedded Chromium (WebView2)
- **Multi-tab** workflow — each tab is independent
- **Side-by-side comparison** — right-click any tab to dock it to the left pane
- **View mode** — open an existing `.md` file to edit and preview it directly
- **System tray** — stays running in the background; minimising hides to tray
- **Optional auto-start** with Windows (toggle in tray menu)

## Requirements

- Windows 10/11
- [.NET 8 Runtime](https://dotnet.microsoft.com/download/dotnet/8.0)
- [WebView2 Runtime](https://developer.microsoft.com/microsoft-edge/webview2/) (for the Preview panel — usually already installed)

## Build

```bash
dotnet build MdConverter.csproj
dotnet run --project MdConverter.csproj
```

> Close the app before rebuilding — the running process locks the output `.exe`.

## Usage

### Basic conversion

1. Paste terminal output into the **SOURCE** panel
2. The **MARKDOWN** panel updates instantly
3. Toggle **Preview** in the toolbar to see the rendered HTML

### Panels

Use the **Source / Markdown / Preview** toggle buttons in the toolbar to show or hide panels. At least one panel must remain visible.

### Tabs

| Action | How |
|---|---|
| New tab | `Ctrl+T` or **＋ New** |
| Close tab | `Ctrl+W` or **×** on the tab |
| Rename tab | Double-click the tab name |
| Open `.md` file | `Ctrl+O` or **⊕ Open** |
| Save as `.md` | **Save .md** (saves to `C:\temp\MdFiles` by default) |
| Copy markdown | **Copy** |

### Side-by-side comparison

Right-click any tab header to access dock/undock options:

- **📌 Dock to left** — pins that tab to the left half; the active tab stays on the right
- **Unpin from left** — returns to single-pane view

Both panes are fully interactive. Each pane keeps its own panel configuration (Source/Markdown/Preview visibility).

## Project structure

```
App.xaml / App.xaml.cs          — Tray icon, window lifecycle, auto-startup
MainWindow.xaml / .xaml.cs      — UI, tab management, comparison mode
Converter/
  TerminalToMarkdownConverter.cs — Pure text converter (no WPF dependency)
  MarkdownRenderer.cs            — Markdown → HTML via Markdig (for preview)
docs/plans/                      — Design and implementation documents
```
