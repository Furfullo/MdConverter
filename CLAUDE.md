# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Build and Run

```bash
dotnet build MdConverter.csproj
dotnet run --project MdConverter.csproj
```

There is no test suite — this is a WPF UI app. Build success (0 errors) is the verification step. The app must be closed before rebuilding, as the running process locks the output exe.

## Architecture

**Stack:** C# 12, WPF, .NET 8, Windows-only (`net8.0-windows`).

### App lifecycle (`App.xaml.cs`)

The app lives in the system tray. Closing the window hides it (`OnClosing` cancels and calls `Hide()`); the actual exit comes from the tray menu. The tray icon and its context menu are built programmatically in `BuildTrayIcon()`. The app icon is drawn at runtime via GDI+ in `DrawIconBitmap()` — there are no embedded icon resources. Auto-startup at login is toggled via `HKCU\Software\Microsoft\Windows\CurrentVersion\Run`.

### Tab system (`MainWindow.xaml.cs`)

All UI state is per-tab, stored in a `TabState` inner class:

```csharp
private class TabState {
    InputBox, OutputBox, PreviewWebView   // the three panels
    SourceCol, MdCol, PreviewCol          // ColumnDefinitions (width=0 hides a panel)
    SplitterCol1, SplitterCol2, Splitter1, Splitter2
    bool WebViewReady, IsViewMode
}
```

Tabs are keyed in `Dictionary<TabItem, TabState> _tabStates`. `MakeDocumentTab()` creates all controls and wiring for a new tab entirely in code-behind — nothing is in XAML templates. Panel visibility is controlled by setting `ColumnDefinition.Width` to `0` (hidden) or `new GridLength(1, GridUnitType.Star)` (visible).

**Re-entrancy guards:** `_suppressTabChange` (prevents `SelectionChanged` loops) and `_suppressOutputSync` (prevents `TextChanged` loops) are `bool` flags set around programmatic changes.

**`IsViewMode`** per tab: when a `.md` file is opened, the tab is put in view mode — the input box holds the raw markdown directly, conversion is skipped, and edits to the output box sync back to the input box.

**WebView2** is initialized lazily per tab (only when the Preview panel is first shown), via `EnsureWebViewReadyAsync`.

### Comparison mode (`DockTab` / `UndockTab`)

`_dockedTab` tracks which tab (if any) is pinned to the left pane. The main content area (`CompareGrid` in XAML) is a 4-column grid:

```
Col 0: left pane (Width=0 normally)
Col 1: splitter left gutter (Width=0 normally)
Col 2: splitter right gutter (Width=0 normally)
Col 3: TabControl (Width=*)
```

`DockTab` moves a tab's `contentGrid` from `TabItem.Content` into `CompareLeftPane` (a `Border`), wrapping it in a `DockPanel` with a purple header strip. `UndockTab` reverses this. The `PinPrefix` constant (`"📌 "`) is used consistently for all header label mutations — its `.Length` (3 UTF-16 code units) is used for slicing, not a magic number.

**Key close guard:** `CloseTab` prevents closing a tab if it would leave 0 non-docked tabs in the right pane (checked separately from the total tab count).

### Conversion pipeline (`Converter/`)

`TerminalToMarkdownConverter` is a stateless pure-function converter with no WPF dependencies. It processes input line-by-line, recognising:
- Box-drawing tables (`┌─┐│└┘`) → GFM tables
- Pure horizontal lines (`────`) → `---`
- Bullet points (`- `, `* `, `•`) → normalised `- `
- Headers: short lines surrounded by blank lines → `#` / `##`
- Sub-headers: short lines ending with `:` → `**bold**`

`MarkdownRenderer` wraps Markdig (with advanced extensions + emoji) to produce a self-contained HTML page for WebView2. All CSS is inlined in `WrapInPage()`.

### Key NuGet packages

| Package | Purpose |
|---|---|
| `Hardcodet.NotifyIcon.Wpf` | System tray `TaskbarIcon` |
| `Markdig` | Markdown → HTML for preview |
| `Microsoft.Web.WebView2` | Chromium-based preview panel |
| `System.Drawing.Common` | Runtime icon generation |

### Default save location

The app saves/opens `.md` files from `C:\temp\MdFiles` (created on first save).
