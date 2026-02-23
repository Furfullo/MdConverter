using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Text;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media.Imaging;
using Hardcodet.Wpf.TaskbarNotification;
using Microsoft.Win32;

namespace MdConverter;

public partial class App : Application
{
    private TaskbarIcon  _trayIcon       = null!;
    private MenuItem     _startupMenuItem = null!;
    private MainWindow?  _mainWindow;

    private const string AppName = "MdConverter";
    private const string RunKey  = @"Software\Microsoft\Windows\CurrentVersion\Run";

    // -------------------------------------------------------------------------
    // Startup / Shutdown
    // -------------------------------------------------------------------------

    protected override void OnStartup(StartupEventArgs e)
    {
        // Pin a consistent AppUserModelID so the taskbar button appears regardless
        // of whether the app is launched from the exe directly or via a shortcut.
        SetCurrentProcessExplicitAppUserModelID("MdConverter.App");

        base.OnStartup(e);

        DispatcherUnhandledException += (_, ex) =>
        {
            MessageBox.Show(ex.Exception.ToString(), "MdConverter — Unhandled Error",
                MessageBoxButton.OK, MessageBoxImage.Error);
            ex.Handled = true;
        };

        try
        {
            _trayIcon = BuildTrayIcon();

            bool startMinimized = e.Args.Contains("--minimized");

            _mainWindow = new MainWindow
            {
                Icon = CreateWindowIcon()
            };

            if (startMinimized)
            {
                _trayIcon.ShowBalloonTip(
                    "MdConverter is running",
                    "Double-click the tray icon to open.",
                    BalloonIcon.Info);
            }
            else
            {
                _mainWindow.Show();
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show(ex.ToString(), "MdConverter — Startup Error",
                MessageBoxButton.OK, MessageBoxImage.Error);
            Shutdown(1);
        }
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _trayIcon.Dispose();
        base.OnExit(e);
    }

    // -------------------------------------------------------------------------
    // Tray icon construction
    // -------------------------------------------------------------------------

    private TaskbarIcon BuildTrayIcon()
    {
        var openItem = new MenuItem { Header = "Open MdConverter", FontWeight = FontWeights.Bold };
        openItem.Click += (_, _) => ShowMainWindow();

        _startupMenuItem = new MenuItem
        {
            Header      = "Start with Windows",
            IsCheckable = true,
            IsChecked   = IsStartupEnabled()
        };
        _startupMenuItem.Click += ToggleStartup;

        var exitItem = new MenuItem { Header = "Exit" };
        exitItem.Click += (_, _) => ExitApp();

        var menu = new ContextMenu();
        menu.Items.Add(openItem);
        menu.Items.Add(new Separator());
        menu.Items.Add(_startupMenuItem);
        menu.Items.Add(new Separator());
        menu.Items.Add(exitItem);

        var icon = new TaskbarIcon
        {
            Icon        = CreateTrayIcon(),
            ToolTipText = "Terminal → Markdown Converter",
            ContextMenu = menu
        };
        icon.TrayMouseDoubleClick += (_, _) => ShowMainWindow();

        return icon;
    }

    // -------------------------------------------------------------------------
    // Window management
    // -------------------------------------------------------------------------

    internal void ShowMainWindow()
    {
        if (_mainWindow == null) return;

        _mainWindow.Show();
        _mainWindow.WindowState = WindowState.Normal;
        _mainWindow.Activate();
        _mainWindow.Focus();
    }

    internal void HideMainWindow() => _mainWindow?.Hide();

    private void ExitApp()
    {
        _trayIcon.Dispose();
        Shutdown();
    }

    // -------------------------------------------------------------------------
    // Auto-startup (registry HKCU Run key)
    // -------------------------------------------------------------------------

    private static bool IsStartupEnabled()
    {
        using var key = Registry.CurrentUser.OpenSubKey(RunKey);
        return key?.GetValue(AppName) is not null;
    }

    private static void SetStartup(bool enable)
    {
        using var key = Registry.CurrentUser.OpenSubKey(RunKey, writable: true);
        if (key is null) return;

        if (enable)
        {
            var exe = Environment.ProcessPath ?? System.Reflection.Assembly.GetExecutingAssembly().Location;
            key.SetValue(AppName, $"\"{exe}\" --minimized");
        }
        else
        {
            key.DeleteValue(AppName, throwOnMissingValue: false);
        }
    }

    private void ToggleStartup(object? sender, RoutedEventArgs e)
    {
        bool newValue = !IsStartupEnabled();
        SetStartup(newValue);
        _startupMenuItem.IsChecked = newValue;
    }

    // -------------------------------------------------------------------------
    // Icon factory — shared drawing + typed wrappers
    // -------------------------------------------------------------------------

    /// <summary>Draws the app icon onto a 256×256 bitmap.</summary>
    private static Bitmap DrawIconBitmap()
    {
        const int size = 256;
        var bmp = new Bitmap(size, size, System.Drawing.Imaging.PixelFormat.Format32bppArgb);
        using var g = Graphics.FromImage(bmp);

        g.SmoothingMode     = SmoothingMode.AntiAlias;
        g.TextRenderingHint = TextRenderingHint.AntiAlias;
        g.InterpolationMode = InterpolationMode.HighQualityBicubic;

        // — Background: deep blue rounded rect —
        using var bgBrush = new LinearGradientBrush(
            new Rectangle(0, 0, size, size),
            Color.FromArgb(0, 90, 180),
            Color.FromArgb(0, 140, 230),
            45f);
        using var bgPath = RoundedRect(new Rectangle(0, 0, size - 1, size - 1), 44);
        g.FillPath(bgBrush, bgPath);

        // — Subtle inner glow ring —
        using var glowPen = new Pen(Color.FromArgb(40, 255, 255, 255), 6f);
        using var glowPath = RoundedRect(new Rectangle(5, 5, size - 11, size - 11), 40);
        g.DrawPath(glowPen, glowPath);

        // — "M" letter —
        using var fontM  = new Font("Segoe UI", 128, System.Drawing.FontStyle.Bold, GraphicsUnit.Pixel);
        using var white  = new SolidBrush(Color.White);
        var sfCenter     = new System.Drawing.StringFormat
        {
            Alignment     = StringAlignment.Center,
            LineAlignment = StringAlignment.Center
        };
        g.DrawString("M", fontM, white, new RectangleF(0, -22, size, size), sfCenter);

        // — Markdown down-arrow (▼ shape) at the bottom —
        int cx   = size / 2;
        int barY = size - 68;
        int tipY = size - 36;

        using var arrowBrush = new SolidBrush(Color.FromArgb(220, 255, 255, 255));

        // Horizontal bar
        var bar = new RectangleF(cx - 38, barY, 76, 14);
        g.FillRectangle(arrowBrush, bar);

        // Downward triangle
        var triangle = new PointF[]
        {
            new(cx - 32, barY + 14),
            new(cx + 32, barY + 14),
            new(cx,      tipY)
        };
        g.FillPolygon(arrowBrush, triangle);

        return bmp;
    }

    /// <summary>Tray icon — System.Drawing.Icon.</summary>
    private static System.Drawing.Icon CreateTrayIcon()
    {
        using var bmp = DrawIconBitmap();
        var handle = bmp.GetHicon();
        var icon   = (System.Drawing.Icon)System.Drawing.Icon.FromHandle(handle).Clone();
        DestroyIcon(handle);
        return icon;
    }

    /// <summary>Taskbar / title-bar icon — WPF BitmapSource.</summary>
    private static BitmapSource CreateWindowIcon()
    {
        using var bmp = DrawIconBitmap();
        var hBitmap   = bmp.GetHbitmap();
        try
        {
            return Imaging.CreateBitmapSourceFromHBitmap(
                hBitmap,
                IntPtr.Zero,
                Int32Rect.Empty,
                BitmapSizeOptions.FromEmptyOptions());
        }
        finally
        {
            DeleteObject(hBitmap);
        }
    }

    private static GraphicsPath RoundedRect(Rectangle b, int r)
    {
        var p = new GraphicsPath();
        p.AddArc(b.X,             b.Y,              r * 2, r * 2, 180, 90);
        p.AddArc(b.Right - r * 2, b.Y,              r * 2, r * 2, 270, 90);
        p.AddArc(b.Right - r * 2, b.Bottom - r * 2, r * 2, r * 2, 0,   90);
        p.AddArc(b.X,             b.Bottom - r * 2, r * 2, r * 2, 90,  90);
        p.CloseFigure();
        return p;
    }

    [DllImport("user32.dll")]   private static extern bool DestroyIcon(IntPtr hIcon);
    [DllImport("gdi32.dll")]    private static extern bool DeleteObject(IntPtr hObject);
    [DllImport("shell32.dll")]  private static extern void SetCurrentProcessExplicitAppUserModelID(
                                    [MarshalAs(UnmanagedType.LPWStr)] string appId);
}
