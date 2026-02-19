using System.ComponentModel;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using Microsoft.Web.WebView2.Wpf;
using MdConverter.Converter;

namespace MdConverter;

public partial class MainWindow : Window
{
    // -------------------------------------------------------------------------
    // Per-tab state
    // -------------------------------------------------------------------------

    private class TabState
    {
        public required TextBox           InputBox       { get; init; }
        public required TextBox           OutputBox      { get; init; }
        public required WebView2          PreviewWebView { get; init; }
        public required ColumnDefinition  SourceCol      { get; init; }
        public required ColumnDefinition  SplitterCol1   { get; init; }
        public required ColumnDefinition  MdCol          { get; init; }
        public required ColumnDefinition  SplitterCol2   { get; init; }
        public required ColumnDefinition  PreviewCol     { get; init; }
        public required GridSplitter      Splitter1      { get; init; }
        public required GridSplitter      Splitter2      { get; init; }
        public          bool              WebViewReady   { get; set; }
        public          bool              IsViewMode     { get; set; }
    }

    private readonly Dictionary<TabItem, TabState> _tabStates = new();

    // -------------------------------------------------------------------------
    // Fields
    // -------------------------------------------------------------------------

    private readonly TerminalToMarkdownConverter _converter = new();
    private bool     _suppressTabChange  = false;
    private bool     _suppressOutputSync = false;
    private TabItem? _addTabItem;
    private TabItem? _dockedTab;

    // Comparison-grid column accessors (indices match CompareGrid.ColumnDefinitions order)
    private ColumnDefinition LeftPaneCol    => CompareGrid.ColumnDefinitions[0];
    private ColumnDefinition SplitterLeft  => CompareGrid.ColumnDefinitions[1];
    private ColumnDefinition SplitterRight => CompareGrid.ColumnDefinitions[2];

    public MainWindow()
    {
        InitializeComponent();
    }

    // -------------------------------------------------------------------------
    // Window lifecycle — hide to tray instead of closing
    // -------------------------------------------------------------------------

    protected override void OnClosing(CancelEventArgs e)
    {
        e.Cancel = true;
        Hide();
    }

    protected override void OnStateChanged(EventArgs e)
    {
        if (WindowState == WindowState.Minimized) Hide();
        base.OnStateChanged(e);
    }

    // -------------------------------------------------------------------------
    // Loaded — initialise tabs (WebView2 init is now lazy per-tab)
    // -------------------------------------------------------------------------

    private void Window_Loaded(object sender, RoutedEventArgs e)
    {
        InitializeTabs();
    }

    // -------------------------------------------------------------------------
    // Keyboard shortcuts
    // -------------------------------------------------------------------------

    private void Window_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.T && Keyboard.Modifiers == ModifierKeys.Control)
        {
            AddNewTab();
            e.Handled = true;
        }
        else if (e.Key == Key.W && Keyboard.Modifiers == ModifierKeys.Control)
        {
            if (DocumentTabs.SelectedItem is TabItem tab && tab != _addTabItem)
                CloseTab(tab);
            e.Handled = true;
        }
        else if (e.Key == Key.O && Keyboard.Modifiers == ModifierKeys.Control)
        {
            OpenMdFile();
            e.Handled = true;
        }
    }

    // -------------------------------------------------------------------------
    // Panel visibility toggles
    // -------------------------------------------------------------------------

    private async void TogglePanel_Click(object sender, RoutedEventArgs e)
    {
        // Enforce at least one panel visible
        bool showSource  = ToggleSource.IsChecked  == true;
        bool showMd      = ToggleMd.IsChecked      == true;
        bool showPreview = TogglePreview.IsChecked  == true;

        if (!showSource && !showMd && !showPreview)
        {
            ToggleMd.IsChecked = true;
            showMd = true;
        }

        if (GetActiveTabState() is TabState state)
        {
            if (showPreview && !state.WebViewReady)
                await EnsureWebViewReadyAsync(state);

            ApplyPanelVisibilityToTab(state);
        }
    }

    private void ApplyPanelVisibilityToTab(TabState state)
    {
        bool showSource  = ToggleSource.IsChecked  == true;
        bool showMd      = ToggleMd.IsChecked      == true;
        bool showPreview = TogglePreview.IsChecked  == true;

        state.SourceCol.Width  = showSource  ? new GridLength(1, GridUnitType.Star) : new GridLength(0);
        state.MdCol.Width      = showMd      ? new GridLength(1, GridUnitType.Star) : new GridLength(0);
        state.PreviewCol.Width = showPreview ? new GridLength(1, GridUnitType.Star) : new GridLength(0);

        // Splitter 1: between Source and the rest
        bool sp1 = showSource && (showMd || showPreview);
        state.SplitterCol1.Width   = sp1 ? new GridLength(8) : new GridLength(0);
        state.Splitter1.Visibility = sp1 ? Visibility.Visible : Visibility.Collapsed;

        // Splitter 2: between MD and Preview
        bool sp2 = showMd && showPreview;
        state.SplitterCol2.Width   = sp2 ? new GridLength(8) : new GridLength(0);
        state.Splitter2.Visibility = sp2 ? Visibility.Visible : Visibility.Collapsed;

        if (showPreview) RefreshPreview();
    }

    private void ApplyPanelVisibility()
    {
        if (GetActiveTabState() is TabState state)
            ApplyPanelVisibilityToTab(state);
    }

    // -------------------------------------------------------------------------
    // Tab management
    // -------------------------------------------------------------------------

    private void InitializeTabs()
    {
        _addTabItem = MakeAddTabButton();
        DocumentTabs.Items.Add(_addTabItem);
        AddNewTab();
    }

    private void AddNewTab(string name = "")
    {
        if (string.IsNullOrEmpty(name))
            name = DateTime.Now.ToString("yyyyMMddHHmm");

        var tab = MakeDocumentTab(name);
        int pos  = DocumentTabs.Items.IndexOf(_addTabItem);
        DocumentTabs.Items.Insert(pos < 0 ? DocumentTabs.Items.Count : pos, tab);
        DocumentTabs.SelectedItem = tab;
    }

    private TabItem MakeDocumentTab(string name)
    {
        // ── Per-tab controls ─────────────────────────────────────────────────

        var inputBox = new TextBox
        {
            FontFamily                    = new FontFamily("Cascadia Code, Consolas, Courier New"),
            FontSize                      = 13,
            AcceptsReturn                 = true,
            AcceptsTab                    = true,
            TextWrapping                  = TextWrapping.NoWrap,
            VerticalScrollBarVisibility   = ScrollBarVisibility.Auto,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Auto,
            Padding                       = new Thickness(10),
            Background                    = Brushes.White,
            Foreground                    = new SolidColorBrush(Color.FromRgb(0x1E, 0x1E, 0x1E)),
            BorderThickness               = new Thickness(0)
        };

        var outputBox = new TextBox
        {
            FontFamily                    = new FontFamily("Cascadia Code, Consolas, Courier New"),
            FontSize                      = 13,
            AcceptsReturn                 = true,
            AcceptsTab                    = true,
            TextWrapping                  = TextWrapping.NoWrap,
            VerticalScrollBarVisibility   = ScrollBarVisibility.Auto,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Auto,
            Padding                       = new Thickness(10),
            Background                    = new SolidColorBrush(Color.FromRgb(0xFA, 0xFA, 0xFA)),
            Foreground                    = new SolidColorBrush(Color.FromRgb(0x1E, 0x1E, 0x1E)),
            BorderThickness               = new Thickness(0)
        };

        var previewWebView = new WebView2();

        // ── Column definitions ───────────────────────────────────────────────

        var sourceCol   = new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star), MinWidth = 0 };
        var splitterCol1 = new ColumnDefinition { Width = new GridLength(8) };
        var mdCol       = new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star), MinWidth = 0 };
        var splitterCol2 = new ColumnDefinition { Width = new GridLength(0) };
        var previewCol  = new ColumnDefinition { Width = new GridLength(0), MinWidth = 0 };

        // ── Splitters ────────────────────────────────────────────────────────

        var splitter1 = new GridSplitter
        {
            Width               = 4,
            HorizontalAlignment = HorizontalAlignment.Center,
            Background          = new SolidColorBrush(Color.FromRgb(0xDD, 0xDD, 0xDD)),
            Cursor              = Cursors.SizeWE
        };
        Grid.SetColumn(splitter1, 1);

        var splitter2 = new GridSplitter
        {
            Width               = 4,
            HorizontalAlignment = HorizontalAlignment.Center,
            Background          = new SolidColorBrush(Color.FromRgb(0xDD, 0xDD, 0xDD)),
            Cursor              = Cursors.SizeWE,
            Visibility          = Visibility.Collapsed
        };
        Grid.SetColumn(splitter2, 3);

        // ── 3-column content grid ────────────────────────────────────────────

        var contentGrid = new Grid();
        contentGrid.ColumnDefinitions.Add(sourceCol);
        contentGrid.ColumnDefinitions.Add(splitterCol1);
        contentGrid.ColumnDefinitions.Add(mdCol);
        contentGrid.ColumnDefinitions.Add(splitterCol2);
        contentGrid.ColumnDefinitions.Add(previewCol);

        contentGrid.Children.Add(WrapInPanel(inputBox,       "SOURCE",   0));
        contentGrid.Children.Add(splitter1);
        contentGrid.Children.Add(WrapInPanel(outputBox,      "MARKDOWN", 2));
        contentGrid.Children.Add(splitter2);
        contentGrid.Children.Add(WrapInPanel(previewWebView, "PREVIEW",  4));

        // ── Build TabState ───────────────────────────────────────────────────

        var state = new TabState
        {
            InputBox      = inputBox,
            OutputBox     = outputBox,
            PreviewWebView = previewWebView,
            SourceCol     = sourceCol,
            SplitterCol1  = splitterCol1,
            MdCol         = mdCol,
            SplitterCol2  = splitterCol2,
            PreviewCol    = previewCol,
            Splitter1     = splitter1,
            Splitter2     = splitter2
        };

        // ── Tab header: rename TextBlock ↔ TextBox + close button ────────────

        var nameLabel = new TextBlock
        {
            Text              = name,
            FontSize          = 12,
            FontWeight        = FontWeights.SemiBold,
            Foreground        = new SolidColorBrush(Color.FromRgb(0x22, 0x22, 0x22)),
            VerticalAlignment = VerticalAlignment.Center,
            MinWidth          = 60,
            MaxWidth          = 160,
            Cursor            = Cursors.Hand,
            ToolTip           = "Double-click to rename"
        };

        var nameEditor = new TextBox
        {
            FontSize          = 12,
            FontWeight        = FontWeights.SemiBold,
            Foreground        = new SolidColorBrush(Color.FromRgb(0x22, 0x22, 0x22)),
            BorderBrush       = new SolidColorBrush(Color.FromRgb(0x00, 0x78, 0xD4)),
            BorderThickness   = new Thickness(1),
            Background        = Brushes.White,
            Padding           = new Thickness(2, 0, 2, 0),
            MinWidth          = 60,
            MaxWidth          = 160,
            VerticalAlignment = VerticalAlignment.Center,
            Visibility        = Visibility.Collapsed
        };

        void BeginRename()
        {
            nameEditor.Text       = nameLabel.Text;
            nameLabel.Visibility  = Visibility.Collapsed;
            nameEditor.Visibility = Visibility.Visible;
            nameEditor.Focus();
            nameEditor.SelectAll();
        }

        void CommitRename(bool revert = false)
        {
            if (!revert && !string.IsNullOrWhiteSpace(nameEditor.Text))
                nameLabel.Text    = nameEditor.Text;
            nameEditor.Visibility = Visibility.Collapsed;
            nameLabel.Visibility  = Visibility.Visible;
        }

        nameLabel.MouseLeftButtonDown += (_, ev) =>
        {
            if (ev.ClickCount == 2) { BeginRename(); ev.Handled = true; }
        };

        nameEditor.KeyDown += (_, ev) =>
        {
            if (ev.Key == Key.Enter)  { CommitRename();             ev.Handled = true; }
            if (ev.Key == Key.Escape) { CommitRename(revert: true); ev.Handled = true; }
        };
        nameEditor.LostFocus += (_, _) => CommitRename();

        var closeBtn = new Button
        {
            Content           = "×",
            FontSize          = 13,
            FontWeight        = FontWeights.Bold,
            Padding           = new Thickness(4, 0, 4, 1),
            Margin            = new Thickness(5, 0, 0, 0),
            VerticalAlignment = VerticalAlignment.Center,
            Background        = Brushes.Transparent,
            BorderThickness   = new Thickness(0),
            Cursor            = Cursors.Hand,
            Foreground        = new SolidColorBrush(Color.FromRgb(0x99, 0x99, 0x99)),
            ToolTip           = "Close tab  (Ctrl+W)"
        };

        var header = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
        header.Children.Add(nameLabel);
        header.Children.Add(nameEditor);
        header.Children.Add(closeBtn);

        var tab = new TabItem
        {
            Header  = header,
            Content = contentGrid,
            Style   = (Style)FindResource("DocTabItem")
        };

        _tabStates[tab] = state;

        closeBtn.Click += (_, _) => CloseTab(tab);

        // ── Per-tab event wiring ─────────────────────────────────────────────

        inputBox.TextChanged += (s, _) =>
        {
            if (_suppressOutputSync) return;
            if (DocumentTabs.SelectedItem != tab) return;
            UpdateOutput(state, ((TextBox)s).Text);
        };

        outputBox.TextChanged += (_, _) =>
        {
            if (_suppressOutputSync) return;
            if (DocumentTabs.SelectedItem != tab) return;
            if (state.IsViewMode)
            {
                _suppressOutputSync = true;
                state.InputBox.Text = state.OutputBox.Text;
                _suppressOutputSync = false;
            }
            RefreshPreview();
        };

        // ── Right-click context menu ─────────────────────────────────────────────

        var contextMenu = new ContextMenu();

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
                    var otherName = GetTabHeaderLabel(_dockedTab)?.Text?.Replace("📌 ", "") ?? "tab";
                    var unpin     = new MenuItem { Header = $"Unpin \"{otherName}\" from left" };
                    unpin.Click  += (_, _) => UndockTab();
                    contextMenu.Items.Add(unpin);
                }
            }
        };

        header.ContextMenu = contextMenu;

        return tab;
    }

    private static Grid WrapInPanel(UIElement content, string label, int column)
    {
        var g = new Grid();
        g.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        g.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });

        var lbl = new TextBlock
        {
            Text       = label,
            FontSize   = 10,
            FontWeight = FontWeights.SemiBold,
            Foreground = new SolidColorBrush(Color.FromRgb(0x88, 0x88, 0x88)),
            Padding    = new Thickness(10, 4, 10, 4),
            Background = new SolidColorBrush(Color.FromRgb(0xF5, 0xF5, 0xF5))
        };

        Grid.SetRow(lbl, 0);
        Grid.SetRow((FrameworkElement)content, 1);
        g.Children.Add(lbl);
        g.Children.Add((UIElement)content);
        Grid.SetColumn(g, column);
        return g;
    }

    private TabItem MakeAddTabButton() => new()
    {
        Header          = "＋",
        FontSize        = 14,
        FontWeight      = FontWeights.Normal,
        Foreground      = new SolidColorBrush(Color.FromRgb(0x55, 0x55, 0x55)),
        Background      = Brushes.Transparent,
        BorderThickness = new Thickness(0),
        Style           = (Style)FindResource("DocTabItem"),
        ToolTip         = "New tab  (Ctrl+T)"
    };

    private void CloseTab(TabItem tab)
    {
        int docCount = DocumentTabs.Items.Cast<TabItem>().Count(t => t != _addTabItem);
        if (docCount <= 1) return; // always keep at least one

        int  index       = DocumentTabs.Items.IndexOf(tab);
        bool wasSelected = DocumentTabs.SelectedItem == tab;

        _suppressTabChange = true;
        DocumentTabs.Items.Remove(tab);
        _tabStates.Remove(tab);
        _suppressTabChange = false;

        int newIndex = Math.Max(0, Math.Min(index, DocumentTabs.Items.Count - 2));
        DocumentTabs.SelectedIndex = newIndex;

        if (wasSelected)
            UpdateOutput(GetActiveTabState());
    }

    // -------------------------------------------------------------------------
    // Active-tab helpers
    // -------------------------------------------------------------------------

    private TabState? GetActiveTabState() =>
        DocumentTabs.SelectedItem is TabItem ti && _tabStates.TryGetValue(ti, out var s) ? s : null;

    private string GetActiveTabName()
    {
        if (DocumentTabs.SelectedItem is TabItem { Header: StackPanel sp })
            return sp.Children.OfType<TextBlock>().FirstOrDefault()?.Text
                   ?? DateTime.Now.ToString("yyyyMMddHHmm");
        return DateTime.Now.ToString("yyyyMMddHHmm");
    }

    private void SetActiveTabName(string name)
    {
        if (DocumentTabs.SelectedItem is TabItem { Header: StackPanel sp })
        {
            var label = sp.Children.OfType<TextBlock>().FirstOrDefault();
            if (label is not null) label.Text = name;
        }
    }

    // -------------------------------------------------------------------------
    // Dock / undock helpers
    // -------------------------------------------------------------------------

    private static TextBlock? GetTabHeaderLabel(TabItem tab) =>
        (tab.Header as StackPanel)?.Children.OfType<TextBlock>().FirstOrDefault();

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

    // -------------------------------------------------------------------------
    // Tab switching
    // -------------------------------------------------------------------------

    private async void DocumentTabs_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_suppressTabChange) return;

        if (DocumentTabs.SelectedItem == _addTabItem)
        {
            AddNewTab();
            return;
        }

        if (GetActiveTabState() is TabState state)
        {
            ApplyPanelVisibilityToTab(state);
            UpdateOutput(state);

            if (TogglePreview.IsChecked == true && !state.WebViewReady)
                await EnsureWebViewReadyAsync(state);

            RefreshPreview();
        }
    }

    // -------------------------------------------------------------------------
    // Conversion
    // -------------------------------------------------------------------------

    private void SetOutputText(TabState state, string text)
    {
        _suppressOutputSync = true;
        state.OutputBox.Text = text;
        _suppressOutputSync = false;
    }

    private void UpdateOutput(TabState? state)
    {
        if (state is null) return;
        UpdateOutput(state, state.InputBox.Text);
    }

    private void UpdateOutput(TabState state, string input)
    {
        bool viewMode = state.IsViewMode;

        if (string.IsNullOrWhiteSpace(input))
        {
            SetOutputText(state, string.Empty);
            StatusText.Text = viewMode
                ? "Ready — edit markdown on the left"
                : "Ready — paste terminal text on the left";
            StatsText.Text  = string.Empty;
            RefreshPreview();
            return;
        }

        string result;
        if (viewMode)
        {
            result          = input;
            StatusText.Text = "Viewing";
            StatsText.Text  = $"{result.Split('\n').Length} lines";
        }
        else
        {
            result          = _converter.Convert(input);
            StatusText.Text = "Converted";
            StatsText.Text  = $"{input.Split('\n').Length} input lines  →  {result.Split('\n').Length} output lines";
        }

        SetOutputText(state, result);
        RefreshPreview();
    }

    // -------------------------------------------------------------------------
    // Preview
    // -------------------------------------------------------------------------

    private async Task EnsureWebViewReadyAsync(TabState state)
    {
        try
        {
            await state.PreviewWebView.EnsureCoreWebView2Async();
            state.WebViewReady = true;
        }
        catch
        {
            TogglePreview.IsEnabled = false;
            TogglePreview.IsChecked = false;
            TogglePreview.ToolTip   = "Preview unavailable — WebView2 runtime not found";
        }
    }

    private void RefreshPreview()
    {
        if (GetActiveTabState() is not TabState state) return;
        if (!state.WebViewReady) return;
        if (TogglePreview.IsChecked != true) return;
        state.PreviewWebView.CoreWebView2.NavigateToString(MarkdownRenderer.ToHtml(state.OutputBox.Text));
    }

    // -------------------------------------------------------------------------
    // Toolbar buttons
    // -------------------------------------------------------------------------

    private void NewTabButton_Click(object sender, RoutedEventArgs e) => AddNewTab();

    private void OpenButton_Click(object sender, RoutedEventArgs e) => OpenMdFile();

    private void OpenMdFile()
    {
        const string saveDir = @"C:\temp\MdFiles";

        var dialog = new Microsoft.Win32.OpenFileDialog
        {
            InitialDirectory = Directory.Exists(saveDir)
                               ? saveDir
                               : Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
            Filter = "Markdown files (*.md)|*.md|All files (*.*)|*.*",
            Title  = "Open Markdown file"
        };

        if (dialog.ShowDialog() != true) return;

        var content = File.ReadAllText(dialog.FileName, System.Text.Encoding.UTF8);
        var tabName = Path.GetFileNameWithoutExtension(dialog.FileName);

        AddNewTab(tabName);

        if (GetActiveTabState() is TabState state)
        {
            state.IsViewMode = true;
            _suppressOutputSync = true;
            state.InputBox.Text = content;
            _suppressOutputSync = false;
            UpdateOutput(state, content);
        }

        StatusText.Text = $"Opened  {Path.GetFileName(dialog.FileName)}";
    }

    private void ClearButton_Click(object sender, RoutedEventArgs e)
    {
        if (GetActiveTabState() is TabState state)
        {
            state.InputBox.Clear();
            StatusText.Text = "Ready — paste terminal text on the left";
            StatsText.Text  = string.Empty;
            RefreshPreview();
        }
    }

    private void SaveButton_Click(object sender, RoutedEventArgs e)
    {
        if (GetActiveTabState() is not TabState state) return;
        if (string.IsNullOrEmpty(state.OutputBox.Text)) return;

        const string saveDir = @"C:\temp\MdFiles";
        Directory.CreateDirectory(saveDir);

        var tabName = GetActiveTabName();

        var dialog = new Microsoft.Win32.SaveFileDialog
        {
            InitialDirectory = saveDir,
            FileName         = tabName.EndsWith(".md", StringComparison.OrdinalIgnoreCase)
                               ? tabName : tabName + ".md",
            Filter           = "Markdown files (*.md)|*.md|All files (*.*)|*.*",
            DefaultExt       = ".md",
            Title            = "Save Markdown file"
        };

        if (dialog.ShowDialog() != true) return;

        File.WriteAllText(dialog.FileName, state.OutputBox.Text, System.Text.Encoding.UTF8);

        SetActiveTabName(Path.GetFileNameWithoutExtension(dialog.FileName));
        StatusText.Text = $"Saved  {Path.GetFileName(dialog.FileName)}";

        SaveButton.Content = "Saved!";
        var timer = new System.Windows.Threading.DispatcherTimer { Interval = TimeSpan.FromSeconds(1.5) };
        timer.Tick += (_, _) => { SaveButton.Content = "Save .md"; timer.Stop(); };
        timer.Start();
    }

    private void CopyButton_Click(object sender, RoutedEventArgs e)
    {
        if (GetActiveTabState() is not TabState state) return;
        if (string.IsNullOrEmpty(state.OutputBox.Text)) return;

        Clipboard.SetText(state.OutputBox.Text);
        CopyButton.Content = "Copied!";

        var timer = new System.Windows.Threading.DispatcherTimer { Interval = TimeSpan.FromSeconds(1.5) };
        timer.Tick += (_, _) => { CopyButton.Content = "Copy"; timer.Stop(); };
        timer.Start();
    }
}
