# Dark Mode Design

**Date:** 2026-02-19
**Status:** Approved

## Overview

Add a dark mode option toggled by a toolbar button. The setting persists across restarts. Both the main UI and the WebView2 preview panel switch themes.

---

## Section 1: Theme Dictionaries

Two new XAML resource dictionaries in `Themes/`:

- `Themes/Light.xaml` — current colors extracted into named brush keys
- `Themes/Dark.xaml` — dark equivalents using the same key names

`App.xaml` merges `Themes/Light.xaml` as the default. On toggle, that entry is swapped to `Dark.xaml`.

### Color Keys

| Key | Light | Dark |
|---|---|---|
| `AppBackground` | `#F5F5F5` | `#1E1E2E` |
| `PanelBackground` | `#FFFFFF` | `#252535` |
| `PanelAltBackground` | `#FAFAFA` | `#2A2A3A` |
| `PanelHeaderBackground` | `#F5F5F5` | `#1A1A28` |
| `PanelHeaderForeground` | `#888888` | `#6A6A8A` |
| `TextForeground` | `#1E1E1E` | `#D4D4D4` |
| `TabBarBackground` | `#F2F2F2` | `#181825` |
| `TabBackground` | `#E0E0E0` | `#2A2A3A` |
| `TabSelectedBackground` | `#FFFFFF` | `#313145` |
| `StatusBarBackground` | `#EBEBEB` | `#181825` |
| `StatusBarForeground` | `#666666` | `#888888` |
| `SplitterBackground` | `#DDDDDD` | `#383850` |
| `BorderColor` | `#D0D0D0` | `#383850` |

---

## Section 2: XAML and Code-Behind Color Wiring

**XAML (`MainWindow.xaml`):** Every hardcoded color in `Window.Resources` styles is replaced with `{DynamicResource <Key>}`. WPF propagates the dictionary swap automatically to all XAML-owned elements.

**Code-behind (`MainWindow.xaml.cs`):** Controls built in `MakeDocumentTab()` (text boxes, splitters, panel label headers) use a new `ApplyThemeToTab(TabState state)` method that reads brushes from `Application.Current.Resources`. Called:
- At the end of `MakeDocumentTab()` for new tabs
- On theme toggle — once per open tab via a loop over `_tabStates`

The comparison mode's purple dock strip (`#6B46C1`) is theme-neutral and unchanged.

---

## Section 3: Toggle Button and Persistence

**Toolbar button:** `ToggleButton x:Name="ToggleTheme"` on the right side of the toolbar, styled with the existing `PanelToggle` style. Label shows `"☀ Light"` when dark is active, `"🌙 Dark"` when light is active (indicates what clicking will switch *to*).

**Persistence:** Theme saved to `%AppData%\MdConverter\theme.txt` (single line: `"dark"` or `"light"`). Loaded in `Window_Loaded` before the first tab is created.

**`ThemeManager` static class** (new file `ThemeManager.cs`):
- `Apply(string theme)` — swaps the merged dictionary in `App.xaml`
- `Load()` / `Save(string theme)` — reads/writes `theme.txt`
- `Current` property — returns `"light"` or `"dark"`

---

## Section 4: Preview Dark CSS

`MarkdownRenderer.ToHtml()` gains a `bool isDark = false` parameter. When `true`, CSS switches to a dark palette.

| Element | Light | Dark |
|---|---|---|
| `body` bg / text | `#ffffff` / `#1e1e1e` | `#1e1e2e` / `#cdd6f4` |
| `h1/h2/h3` | `#111` | `#cdd6f4` |
| `code` bg | `#f3f3f3` | `#313244` |
| `pre` bg / text | `#1e1e1e` / `#d4d4d4` | `#11111b` / `#cdd6f4` |
| `blockquote` bg | `#f0f7ff` | `#1e2030` |
| `th` bg | `#f0f4f8` | `#313244` |
| `td` even-row | `#f9fbfc` | `#252535` |
| `tr` hover | `#eef4fb` | `#2a2a3a` |
| borders | `#d0d7de` | `#45475a` |
| links | `#0078d4` | `#89b4fa` |

`RefreshPreview()` passes `ThemeManager.Current == "dark"` into `ToHtml()`. On theme toggle, `RefreshPreview()` is called to re-render immediately.
