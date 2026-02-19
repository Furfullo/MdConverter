# Tab Comparison Mode Implementation Plan

> **For Claude:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task.

**Goal:** Let users pin any tab to the left half of the window and compare it side-by-side with the active tab on the right, both fully interactive.

**Architecture:** A new 3-column comparison `Grid` replaces the bare `TabControl` in Row 1 of the main window. The left column (normally `Width=0`) hosts the docked tab's `contentGrid`; the right column always holds the existing `TabControl`. A `GridSplitter` sits between them. State is tracked with a single `_dockedTab` field. Dock/undock is triggered by a right-click context menu on each tab header.

**Tech Stack:** C# 12, WPF (.NET 8), no unit-test framework (UI app — verification is build + manual run).

---

### Task 1: Wrap the TabControl in a comparison Grid (XAML only)

**Files:**
- Modify: `MainWindow.xaml` (lines 217–220 — the `<TabControl>` in Grid Row 1)

**Step 1: Replace the bare TabControl with a Grid wrapper**

Find this in `MainWindow.xaml`:
```xml
<!-- ── Document tabs (content built in code-behind) ── -->
<TabControl x:Name="DocumentTabs" Grid.Row="1"
            Style="{StaticResource DocTabControl}"
            SelectionChanged="DocumentTabs_SelectionChanged"/>
```

Replace it with:
```xml
<!-- ── Comparison layout (Row 1) ── -->
<Grid x:Name="CompareGrid" Grid.Row="1">
    <Grid.ColumnDefinitions>
        <ColumnDefinition x:Name="LeftPaneCol"    Width="0" MinWidth="0"/>
        <ColumnDefinition x:Name="SplitterLeft"   Width="0"/>
        <ColumnDefinition x:Name="SplitterRight"  Width="0"/>
        <ColumnDefinition                          Width="*"/>
    </Grid.ColumnDefinitions>

    <!-- Left (docked) pane -->
    <Border x:Name="CompareLeftPane"
            Grid.Column="0"
            ClipToBounds="True"
            BorderBrush="#D0D0D0"
            BorderThickness="0,0,0,1"/>

    <!-- Drag handle -->
    <GridSplitter x:Name="CompareSplitter"
                  Grid.Column="1"
                  Grid.ColumnSpan="2"
                  Width="5"
                  HorizontalAlignment="Stretch"
                  Background="#DDDDDD"
                  Cursor="SizeWE"
                  Visibility="Collapsed"/>

    <!-- Right pane: existing tab control -->
    <TabControl x:Name="DocumentTabs" Grid.Column="3"
                Style="{StaticResource DocTabControl}"
                SelectionChanged="DocumentTabs_SelectionChanged"/>
</Grid>
```

**Step 2: Build and verify it still looks identical to before**

```bash
dotnet build MdConverter.csproj
```
Expected: build succeeds, 0 errors. Run the app — it should look and behave exactly as before (left pane is Width=0, invisible).

**Step 3: Commit**

```bash
git add MainWindow.xaml
git commit -m "refactor: wrap TabControl in comparison Grid (hidden left pane)"
```

---

### Task 2: Add `_dockedTab` field and column-reference helpers

**Files:**
- Modify: `MainWindow.xaml.cs`

**Step 1: Add the field and column accessors after the existing fields block (around line 43)**

Add after `private TabItem? _addTabItem;`:
```csharp
private TabItem? _dockedTab;

// Convenience accessors for the comparison-Grid column definitions
private ColumnDefinition LeftPaneCol    => CompareGrid.ColumnDefinitions[0];
private ColumnDefinition SplitterLeft  => CompareGrid.ColumnDefinitions[1];
private ColumnDefinition SplitterRight => CompareGrid.ColumnDefinitions[2];
```

**Step 2: Build**

```bash
dotnet build MdConverter.csproj
```
Expected: 0 errors.

**Step 3: Commit**

```bash
git add MainWindow.xaml.cs
git commit -m "feat: add _dockedTab field and comparison-grid column accessors"
```

---

### Task 3: Implement `DockTab` and `UndockTab` methods

**Files:**
- Modify: `MainWindow.xaml.cs`

**Step 1: Add a helper that finds a tab's header TextBlock**

Add this private method near the existing `GetActiveTabName()` helper (around line 450):
```csharp
private static TextBlock? GetTabHeaderLabel(TabItem tab) =>
    (tab.Header as StackPanel)?.Children.OfType<TextBlock>().FirstOrDefault();
```

**Step 2: Add `UndockTab`**

Add after the helper above:
```csharp
private void UndockTab()
{
    if (_dockedTab is null) return;

    // Move contentGrid back into the TabItem
    if (CompareLeftPane.Child is Grid contentGrid)
    {
        CompareLeftPane.Child = null;
        _dockedTab.Content    = contentGrid;
    }

    // Remove pin prefix from header
    if (GetTabHeaderLabel(_dockedTab) is TextBlock lbl && lbl.Text.StartsWith("📌 "))
        lbl.Text = lbl.Text[3..];

    // Hide left pane and splitter
    LeftPaneCol.Width    = new GridLength(0);
    SplitterLeft.Width   = new GridLength(0);
    SplitterRight.Width  = new GridLength(0);
    CompareSplitter.Visibility = Visibility.Collapsed;

    _dockedTab = null;
}
```

**Step 3: Add `DockTab`**

Add after `UndockTab`:
```csharp
private void DockTab(TabItem tab)
{
    // Undock any previously docked tab first
    UndockTab();

    // Move contentGrid from the TabItem into the left pane
    if (tab.Content is Grid contentGrid)
    {
        tab.Content           = null;
        CompareLeftPane.Child = contentGrid;
    }

    // Add pin prefix to header
    if (GetTabHeaderLabel(tab) is TextBlock lbl && !lbl.Text.StartsWith("📌 "))
        lbl.Text = "📌 " + lbl.Text;

    // Show left pane and splitter (equal split initially)
    LeftPaneCol.Width    = new GridLength(1, GridUnitType.Star);
    SplitterLeft.Width   = new GridLength(3);
    SplitterRight.Width  = new GridLength(3);
    CompareSplitter.Visibility = Visibility.Visible;

    _dockedTab = tab;

    // If the docked tab was selected in the right pane, move to another tab
    if (DocumentTabs.SelectedItem == tab)
    {
        var next = DocumentTabs.Items
            .OfType<TabItem>()
            .FirstOrDefault(t => t != tab && t != _addTabItem);
        if (next is not null)
            DocumentTabs.SelectedItem = next;
    }
}
```

**Step 4: Build**

```bash
dotnet build MdConverter.csproj
```
Expected: 0 errors.

**Step 5: Commit**

```bash
git add MainWindow.xaml.cs
git commit -m "feat: implement DockTab and UndockTab mechanics"
```

---

### Task 4: Add right-click context menu to each tab header

**Files:**
- Modify: `MainWindow.xaml.cs` — inside `MakeDocumentTab()`

**Step 1: Add context menu wiring at the end of `MakeDocumentTab`, just before `return tab`**

The context menu must be rebuilt each time it opens (items are dynamic). Add this block after the `closeBtn.Click` line (around line 360):

```csharp
// ── Right-click context menu ─────────────────────────────────────────────

var contextMenu = new ContextMenu();
tab.Header = header; // header already set below — move assignment before this block if needed

contextMenu.Opened += (_, _) =>
{
    contextMenu.Items.Clear();

    int docCount = DocumentTabs.Items.Cast<TabItem>().Count(t => t != _addTabItem);

    if (_dockedTab == tab)
    {
        // This tab is currently docked — offer unpin only
        var unpin = new MenuItem { Header = "Unpin from left" };
        unpin.Click += (_, _) => UndockTab();
        contextMenu.Items.Add(unpin);
    }
    else
    {
        // Offer to dock this tab
        var dock = new MenuItem
        {
            Header    = "📌  Dock to left",
            IsEnabled = docCount > 1
        };
        dock.Click += (_, _) => DockTab(tab);
        contextMenu.Items.Add(dock);

        // If another tab is currently docked, also offer to unpin it
        if (_dockedTab is not null)
        {
            var otherName = GetTabHeaderLabel(_dockedTab)?.Text?.TrimStart('📌', ' ') ?? "tab";
            var unpin = new MenuItem { Header = $"Unpin \"{otherName}\" from left" };
            unpin.Click += (_, _) => UndockTab();
            contextMenu.Items.Add(unpin);
        }
    }
};

header.ContextMenu = contextMenu;
```

> **Note:** `header` is the `StackPanel` defined earlier in `MakeDocumentTab`. The `tab.Header = header` assignment already exists a few lines later — make sure the context menu is attached to `header` before or after that line (both work since it's a reference).

**Step 2: Build and run**

```bash
dotnet build MdConverter.csproj
```

Open the app, right-click any tab header. You should see "📌 Dock to left" in the context menu (greyed out if only one tab, clickable if two or more exist).

**Step 3: Commit**

```bash
git add MainWindow.xaml.cs
git commit -m "feat: add right-click dock/undock context menu to tab headers"
```

---

### Task 5: Skip the docked tab in `DocumentTabs_SelectionChanged`

**Files:**
- Modify: `MainWindow.xaml.cs` — `DocumentTabs_SelectionChanged` (around line 471)

**Step 1: Add a guard at the top of `DocumentTabs_SelectionChanged`**

The existing method starts:
```csharp
private async void DocumentTabs_SelectionChanged(object sender, SelectionChangedEventArgs e)
{
    if (_suppressTabChange) return;

    if (DocumentTabs.SelectedItem == _addTabItem)
    {
        AddNewTab();
        return;
    }
    ...
```

Add one guard block after the `_addTabItem` check:
```csharp
    // If the selected tab is docked to the left pane, skip it and pick the next one
    if (DocumentTabs.SelectedItem is TabItem selected && selected == _dockedTab)
    {
        var next = DocumentTabs.Items
            .OfType<TabItem>()
            .FirstOrDefault(t => t != _dockedTab && t != _addTabItem);
        if (next is not null)
        {
            _suppressTabChange = true;
            DocumentTabs.SelectedItem = next;
            _suppressTabChange = false;
        }
        return;
    }
```

**Step 2: Build and test**

```bash
dotnet build MdConverter.csproj
```

Run the app:
1. Open two tabs
2. Right-click tab 1 → "Dock to left" — left pane appears with tab 1's content, right pane shows tab 2
3. Click tab 1 in the tab bar — it should stay on tab 2 (skipped)

**Step 3: Commit**

```bash
git add MainWindow.xaml.cs
git commit -m "feat: skip docked tab on tab-bar selection change"
```

---

### Task 6: Guard `CloseTab` against closing the docked tab directly

**Files:**
- Modify: `MainWindow.xaml.cs` — `CloseTab` (around line 423)

**Step 1: Add undock-before-close guard**

The existing `CloseTab` starts:
```csharp
private void CloseTab(TabItem tab)
{
    int docCount = DocumentTabs.Items.Cast<TabItem>().Count(t => t != _addTabItem);
    if (docCount <= 1) return;
    ...
```

Add after the `docCount` guard:
```csharp
    // If closing the docked tab, undock it first so its contentGrid is restored
    if (tab == _dockedTab)
        UndockTab();
```

**Step 2: Build and test**

```bash
dotnet build MdConverter.csproj
```

Run the app:
1. Open two tabs, dock tab 1 to the left
2. Click the × button on tab 1 in the tab bar — it should undock and then close without errors

**Step 3: Commit**

```bash
git add MainWindow.xaml.cs
git commit -m "feat: undock tab before closing if it is the docked tab"
```

---

### Task 7: Visual polish — label the left pane with the tab name

**Files:**
- Modify: `MainWindow.xaml.cs` — `DockTab` and `UndockTab`

**Step 1: Wrap the docked content in a `DockPanel` that adds a thin header strip**

Replace the `CompareLeftPane.Child = contentGrid` line in `DockTab` with:
```csharp
    var tabName  = GetTabHeaderLabel(tab)?.Text?.TrimStart('📌', ' ') ?? "Tab";
    var strip = new Border
    {
        Background = new SolidColorBrush(Color.FromRgb(0x6B, 0x46, 0xC1)),
        Padding    = new Thickness(8, 3, 8, 3)
    };
    strip.Child = new TextBlock
    {
        Text       = $"◀  {tabName}",
        Foreground = Brushes.White,
        FontSize   = 10,
        FontWeight = FontWeights.SemiBold
    };

    var wrapper = new DockPanel();
    DockPanel.SetDock(strip, Dock.Top);
    wrapper.Children.Add(strip);
    wrapper.Children.Add(contentGrid);

    CompareLeftPane.Child = wrapper;
```

And in `UndockTab`, extract the contentGrid from the wrapper before restoring:
```csharp
    // Move contentGrid back into the TabItem
    if (CompareLeftPane.Child is DockPanel wrapper)
    {
        var contentGrid = wrapper.Children.OfType<Grid>().FirstOrDefault();
        wrapper.Children.Clear();          // detach from DockPanel
        CompareLeftPane.Child = null;
        if (contentGrid is not null)
            _dockedTab.Content = contentGrid;
    }
```

**Step 2: Build and test**

```bash
dotnet build MdConverter.csproj
```

Run the app, dock a tab — the left pane should have a thin purple header bar showing "◀  TabName".

**Step 3: Commit**

```bash
git add MainWindow.xaml.cs
git commit -m "feat: add purple header strip to docked left pane"
```

---

### Task 8: Smoke-test the full feature end-to-end

No code changes — verification only.

**Checklist:**

1. **Normal mode:** open app with one tab → right-click → "Dock to left" is disabled (grey)
2. **Dock:** open two tabs, set different panel configs on each, right-click tab 1 → dock → left pane shows tab 1's panels, right pane shows tab 2
3. **Interaction:** type in left pane source box → markdown updates live; type in right pane → same
4. **Tab bar skip:** click the docked tab in the tab bar → right pane stays on current tab (no switch to docked)
5. **Switch right pane:** click tab 2, tab 3, etc. in the tab bar → right pane changes, left pane stays fixed
6. **Undock via docked tab:** right-click docked tab → "Unpin from left" → returns to single-pane view
7. **Undock via other tab:** right-click any other tab → "Unpin '[name]' from left" → same result
8. **Dock replacement:** dock tab 1, then right-click tab 2 → "Dock to left" → tab 1 undocks, tab 2 docks
9. **Close docked tab:** dock tab 1, click × on tab 1 → undocks then closes cleanly
10. **Splitter drag:** drag the splitter between panes to resize — both panes resize correctly

If any check fails, debug before proceeding.

**Final commit (if any fixes needed):**

```bash
git add -p
git commit -m "fix: <description of fix>"
```
