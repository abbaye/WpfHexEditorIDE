// ==========================================================
// Project: WpfHexEditor.Sample.Docking
// File: MainWindow.xaml.cs
// Author: Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.6
// Created: 2026-03-07
// Description:
//     Code-behind for the main window. Wires the DockControl ContentFactory,
//     builds the default layout (Explorer left / Welcome doc center /
//     Output+Properties bottom), saves/restores layout from disk, and handles
//     runtime theme switching via the View menu.
//
// Architecture Notes:
//     Patterns used: Factory (ContentFactory delegate), Strategy (theme switching)
//     Layout persistence: JSON via DockLayoutSerializer → %APPDATA%/WpfHexEditor/Samples/Docking/
//     Independence: ContentIds are prefixed "sample-" to avoid collision with any host application.
//     Validation: layout is accepted only when ALL four sample ContentIds are present.
//     Theme: DockWindowBackgroundBrush, DockMenuBackgroundBrush (dynamic, from Docking.Wpf themes)
// ==========================================================

using System.IO;
using System.Windows;
using System.Windows.Controls;
using WpfHexEditor.Docking.Core;
using WpfHexEditor.Docking.Core.Nodes;
using WpfHexEditor.Docking.Core.Serialization;
using WpfHexEditor.Sample.Docking.Panels;

namespace WpfHexEditor.Sample.Docking;

public partial class MainWindow : Window
{
    // ─── Constants ────────────────────────────────────────────────────────────

    // "sample-" prefix guarantees these IDs never collide with any host application layout.
    private const string IdExplorer   = "sample-explorer";
    private const string IdOutput     = "sample-output";
    private const string IdProperties = "sample-properties";
    private const string IdWelcome    = "sample-welcome";

    // Layout is valid only when ALL four panel IDs are present.
    private static readonly IReadOnlyList<string> AllSampleIds =
    [
        IdExplorer, IdOutput, IdProperties, IdWelcome
    ];

    private static readonly string LayoutPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "WpfHexEditor", "Samples", "Docking", "layout.json");

    private static readonly Uri DarkThemeUri = new(
        "pack://application:,,,/WpfHexEditor.Docking.Wpf;component/Themes/DarkTheme.xaml");
    private static readonly Uri LightThemeUri = new(
        "pack://application:,,,/WpfHexEditor.Docking.Wpf;component/Themes/OfficeTheme.xaml");

    // ─── State ────────────────────────────────────────────────────────────────

    private ExplorerPanel?   _explorerPanel;
    private OutputPanel?     _outputPanel;
    private PropertiesPanel? _propertiesPanel;
    private WelcomePanel?    _welcomePanel;

    private bool _isDarkTheme = true;

    // ─── Init ─────────────────────────────────────────────────────────────────

    public MainWindow()
    {
        InitializeComponent();
    }

    // ─── Lifecycle ────────────────────────────────────────────────────────────

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        DockHost.ContentFactory = BuildContent;
        DockHost.Layout         = TryLoadLayout() ?? BuildDefaultLayout();
        RestoreWindowState(DockHost.Layout);

        _outputPanel?.Log("Sample application started.");
        _outputPanel?.Log("Use View > Theme to switch themes, View > Layout > Reset to restore panels.");
    }

    private void OnClosed(object sender, EventArgs e) => SaveLayout();

    // ─── ContentFactory ────────────────────────────────────────────────────────

    private object BuildContent(DockItem item) => item.ContentId switch
    {
        IdExplorer   => _explorerPanel   ??= new ExplorerPanel(),
        IdOutput     => _outputPanel     ??= new OutputPanel(),
        IdProperties => _propertiesPanel ??= new PropertiesPanel(),
        IdWelcome    => _welcomePanel    ??= new WelcomePanel(),
        _            => CreateFallback(item.ContentId)
    };

    // ─── Default layout ────────────────────────────────────────────────────────

    private static DockLayoutRoot BuildDefaultLayout()
    {
        var layout = new DockLayoutRoot();
        var engine = new DockEngine(layout);

        // Welcome document in the center
        var welcomeItem = new DockItem
        {
            Title     = "Welcome",
            ContentId = IdWelcome,
            CanClose  = false,
            CanFloat  = false
        };
        layout.MainDocumentHost.AddItem(welcomeItem);

        // Explorer panel — left of document host
        var explorerItem = new DockItem
        {
            Title     = "Explorer",
            ContentId = IdExplorer,
            CanClose  = false
        };
        engine.Dock(explorerItem, layout.MainDocumentHost, DockDirection.Left);

        // Output panel — bottom of document host
        var outputItem = new DockItem
        {
            Title     = "Output",
            ContentId = IdOutput,
            CanClose  = false
        };
        engine.Dock(outputItem, layout.MainDocumentHost, DockDirection.Bottom);

        // Properties panel — tabbed alongside Output in the same bottom group
        var propertiesItem = new DockItem
        {
            Title     = "Properties",
            ContentId = IdProperties,
            CanClose  = false
        };
        engine.Dock(propertiesItem, outputItem.Owner!, DockDirection.Center);

        return layout;
    }

    // ─── Layout persistence ────────────────────────────────────────────────────

    private static DockLayoutRoot? TryLoadLayout()
    {
        if (!File.Exists(LayoutPath))
            return null;

        try
        {
            var json   = File.ReadAllText(LayoutPath);
            var layout = DockLayoutSerializer.Deserialize(json);

            // Accept only when ALL sample ContentIds are present — strict check prevents
            // accidentally loading a layout saved by a different application.
            return AllSampleIds.All(id => layout.FindItemByContentId(id) is not null)
                ? layout
                : null;
        }
        catch
        {
            return null;
        }
    }

    private void SaveLayout()
    {
        if (DockHost.Layout is not { } layout) return;

        layout.WindowState  = (int)WindowState;
        layout.WindowLeft   = RestoreBounds.Left;
        layout.WindowTop    = RestoreBounds.Top;
        layout.WindowWidth  = RestoreBounds.Width;
        layout.WindowHeight = RestoreBounds.Height;

        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(LayoutPath)!);
            File.WriteAllText(LayoutPath, DockLayoutSerializer.Serialize(layout));
        }
        catch { /* non-critical */ }
    }

    private void RestoreWindowState(DockLayoutRoot layout)
    {
        if (layout.WindowWidth  is > 0 and var w &&
            layout.WindowHeight is > 0 and var h)
        {
            Width  = w;
            Height = h;
        }

        if (layout.WindowLeft is { } l && layout.WindowTop is { } t)
        {
            Left = l;
            Top  = t;
        }

        if (layout.WindowState == 2)
            WindowState = WindowState.Maximized;
    }

    // ─── Menu — Theme ─────────────────────────────────────────────────────────

    private void OnThemeDark(object sender, RoutedEventArgs e)
    {
        if (_isDarkTheme) return;
        SetTheme(dark: true);
    }

    private void OnThemeLight(object sender, RoutedEventArgs e)
    {
        if (!_isDarkTheme) return;
        SetTheme(dark: false);
    }

    private void SetTheme(bool dark)
    {
        _isDarkTheme = dark;
        App.SwitchTheme(dark ? DarkThemeUri : LightThemeUri);

        MenuDarkTheme.IsChecked  = dark;
        MenuLightTheme.IsChecked = !dark;
        StatusThemeLabel.Text    = dark ? "Theme: Dark" : "Theme: Light";
    }

    // ─── Menu — Layout ────────────────────────────────────────────────────────

    private void OnLayoutReset(object sender, RoutedEventArgs e)
    {
        // Dispose cached panel instances so they are re-created fresh
        _explorerPanel   = null;
        _outputPanel     = null;
        _propertiesPanel = null;
        _welcomePanel    = null;

        DockHost.Layout = BuildDefaultLayout();

        _outputPanel?.Log("Layout reset to default.");
    }

    // ─── Window chrome ────────────────────────────────────────────────────────

    private void OnMinimize(object sender, RoutedEventArgs e) =>
        WindowState = WindowState.Minimized;

    private void OnMaximizeRestore(object sender, RoutedEventArgs e)
    {
        WindowState = WindowState == WindowState.Maximized
            ? WindowState.Normal
            : WindowState.Maximized;

        MaxRestoreButton.Content = WindowState == WindowState.Maximized ? "\uE923" : "\uE739";
        MaxRestoreButton.ToolTip = WindowState == WindowState.Maximized ? "Restore" : "Maximize";
    }

    private void OnClose(object sender, RoutedEventArgs e) => Close();

    // ─── DockControl events ───────────────────────────────────────────────────

    private void OnTabCloseRequested(DockItem item) =>
        _outputPanel?.Log($"Closed: {item.Title}");

    private void OnActiveItemChanged(DockItem item) =>
        StatusActiveItemLabel.Text = item?.Title ?? "Ready";

    // ─── Helpers ──────────────────────────────────────────────────────────────

    private static UIElement CreateFallback(string contentId) =>
        new TextBlock
        {
            Text                = $"Content not found: {contentId}",
            VerticalAlignment   = VerticalAlignment.Center,
            HorizontalAlignment = HorizontalAlignment.Center,
            FontSize            = 13,
            Opacity             = 0.5
        };
}
