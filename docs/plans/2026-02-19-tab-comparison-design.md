# Tab Comparison Mode — Design

**Date:** 2026-02-19
**Status:** Approved

## Overview

Allow users to pin any tab to the left half of the window and compare it side-by-side with the active tab in the right half. Both panes are fully interactive. The feature is triggered and dismissed via a right-click context menu on tab headers.

---

## Section 1: Layout

Main window Grid Row 1 (currently a single `TabControl`) is replaced with a 5-column `Grid`:

```
[ Left pane (0→*) | 5px col | GridSplitter | 5px col | TabControl (*) ]
```

- **Left pane:** a `Border` that hosts the docked tab's `contentGrid`
- **Splitter columns + GridSplitter:** start `Width=0` / `Collapsed`; shown when comparison mode is active
- **TabControl:** unchanged, always owns the right side
- In normal mode all left-side columns are `Width=0`, making the layout identical to today

---

## Section 2: State & Context Menu

A single new field:

```csharp
private TabItem? _dockedTab;  // null = normal mode
```

Each tab header gets a `ContextMenu` with context-sensitive items:

| Current state | Menu items shown |
|---|---|
| Nothing docked | "📌 Dock to left" |
| This tab is docked | "Unpin from left" |
| A different tab is docked | "📌 Dock to left" + "Unpin [other tab name]" |

- Context menu is not shown (or "Dock" is disabled) on the `+` add-tab button
- "Dock to left" is disabled when only one document tab exists

---

## Section 3: Docking / Undocking Mechanics

### Docking a tab
1. If another tab is already docked, undock it first (restore its `contentGrid` to `TabItem.Content`)
2. Remove the tab's `contentGrid` from `TabItem.Content` and place it in the left `Border`
3. Show the left pane column and `GridSplitter` (set widths to `*` and `8`)
4. Add `"📌 "` prefix to the tab header `TextBlock`
5. Set `_dockedTab = tab`
6. If the docked tab was selected in the right pane, auto-select the next available tab

### Undocking
1. Remove `contentGrid` from the left `Border`, restore it to `TabItem.Content`
2. Collapse left pane column and `GridSplitter` (widths back to `0`)
3. Remove `"📌 "` prefix from the header
4. Set `_dockedTab = null`

### Tab close guard
If the user closes the docked tab (Ctrl+W or close button), undock it first, then proceed with normal close logic.

---

## Section 4: Panel Visibility

- **Toolbar toggles** (Source / Markdown / Preview) apply only to the **right pane's active tab** — no change to existing behaviour
- **Left pane** retains whatever panel state the tab had when docked ("set panels up first, then dock")
- No "focused pane" concept — toolbar always targets the right pane
- **Tab bar skip:** when the docked tab is clicked in the tab bar, `DocumentTabs_SelectionChanged` detects `_dockedTab == selectedTab` and advances selection to the next available tab, preventing the same tab from showing in both panes simultaneously
