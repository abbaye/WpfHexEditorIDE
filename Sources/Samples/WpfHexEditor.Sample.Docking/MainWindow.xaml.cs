//////////////////////////////////////////////
// Apache 2.0  - 2026
// Author : Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.5, Claude Sonnet 4.6
//////////////////////////////////////////////

using System.ComponentModel;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using Microsoft.Win32;
using WpfHexaEditor;
using WpfHexEditor.Docking.Core;
using WpfHexEditor.Docking.Core.Nodes;
using WpfHexEditor.Docking.Core.Serialization;
using WpfHexEditor.Docking.Wpf;

namespace WpfHexEditor.Sample.Docking;

public partial class MainWindow : Window
{
    private static readonly string LayoutFilePath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "WpfHexEditor", "Sample.Docking", "layout.json");

    private DockLayoutRoot _layout = null!;
    private DockEngine _engine = null!;
    private int _documentCounter;
    private bool _isLocked;

    public MainWindow()
    {
        InitializeComponent();
        Closing += OnWindowClosing;
        Loaded += OnLoaded;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        LoadSavedLayoutOrDefault();
    }

    private void OnWindowClosing(object? sender, CancelEventArgs e)
    {
        AutoSaveLayout();
    }

    // ─── Layout persistence ────────────────────────────────────────────

    private void LoadSavedLayoutOrDefault()
    {
        if (File.Exists(LayoutFilePath))
        {
            try
            {
                var layout = DockLayoutSerializer.Deserialize(File.ReadAllText(LayoutFilePath));
                ApplyLayout(layout);
                OutputLogger.Info($"Layout restored from: {LayoutFilePath}");
                StatusText.Text = "Layout restored from previous session.";
                return;
            }
            catch (Exception ex)
            {
                OutputLogger.Error($"Failed to restore layout: {ex.Message}");
            }
        }

        OutputLogger.Info("No saved layout found, using defaults.");
        SetupDefaultLayout();
    }

    private void AutoSaveLayout()
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(LayoutFilePath)!);
            File.WriteAllText(LayoutFilePath, DockLayoutSerializer.Serialize(_layout));
            OutputLogger.Info($"Layout auto-saved to: {LayoutFilePath}");
        }
        catch (Exception ex)
        {
            OutputLogger.Error($"Failed to auto-save layout: {ex.Message}");
        }
    }

    // ─── Layout helpers ────────────────────────────────────────────────

    private void SetupDefaultLayout()
    {
        var layout = new DockLayoutRoot();
        var engine = new DockEngine(layout);

        layout.MainDocumentHost.AddItem(new DockItem { Title = "Welcome", ContentId = "doc-welcome" });

        var solutionExplorer = new DockItem { Title = "Solution Explorer", ContentId = "panel-solution-explorer" };
        engine.Dock(solutionExplorer, layout.MainDocumentHost, DockDirection.Left);

        var output = new DockItem { Title = "Output", ContentId = "panel-output" };
        engine.Dock(output, layout.MainDocumentHost, DockDirection.Bottom);

        var properties = new DockItem { Title = "Properties", ContentId = "panel-properties" };
        engine.Dock(properties, layout.MainDocumentHost, DockDirection.Right);

        ApplyLayout(layout, engine);
    }

    /// <summary>
    /// Câble un layout au DockControl. Utilisé par SetupDefaultLayout, LoadSavedLayoutOrDefault
    /// et OnLoadLayout. Gère la souscription unique à TabCloseRequested et synchronise
    /// le compteur de documents.
    /// </summary>
    private void ApplyLayout(DockLayoutRoot layout, DockEngine? engine = null)
    {
        // Évite les doublons si ApplyLayout est appelé plusieurs fois (Reset, Load…)
        DockHost.TabCloseRequested -= OnTabCloseRequested;

        _layout = layout;
        _engine = engine ?? new DockEngine(_layout);
        _isLocked = false;
        LockMenuItem.IsChecked = false;

        DockHost.ContentFactory = CreateContentForItem;
        DockHost.TabCloseRequested += OnTabCloseRequested;
        DockHost.Layout = _layout;

        SyncDocumentCounter();
        UpdateStatusBar();
    }

    /// <summary>
    /// Synchronise <see cref="_documentCounter"/> avec les ContentId existants après
    /// un restore, pour éviter des collisions de ContentId lors de la création de nouveaux docs.
    /// </summary>
    private void SyncDocumentCounter()
    {
        var allItems = _layout.GetAllGroups()
            .SelectMany(g => g.Items)
            .Concat(_layout.FloatingItems)
            .Concat(_layout.AutoHideItems);

        var max = 0;
        foreach (var item in allItems)
        {
            var id = item.ContentId;
            if (id.StartsWith("doc-hex-") && int.TryParse(id["doc-hex-".Length..], out var n1))
                max = Math.Max(max, n1);
            else if (id.StartsWith("doc-") && int.TryParse(id["doc-".Length..], out var n2))
                max = Math.Max(max, n2);
        }

        _documentCounter = max;
    }

    // ─── Content factory ───────────────────────────────────────────────

    private object CreateContentForItem(DockItem item)
    {
        return item.ContentId switch
        {
            "panel-solution-explorer" => CreateSolutionExplorerContent(),
            "panel-properties" => CreatePropertiesContent(),
            "panel-output" => CreateOutputContent(),
            _ when item.ContentId.StartsWith("doc-hex-") => CreateHexEditorContent(
                item.Metadata.TryGetValue("FilePath", out var fp) ? fp : null),
            _ => CreateDocumentContent(item)
        };
    }

    private static UIElement CreateSolutionExplorerContent()
    {
        var treeView = new TreeView { Background = System.Windows.Media.Brushes.Transparent };
        var root = new TreeViewItem { Header = "Solution 'WpfHexEditor'" };
        root.Items.Add(new TreeViewItem { Header = "WpfHexEditor.Docking.Core" });
        root.Items.Add(new TreeViewItem { Header = "WpfHexEditor.Docking.Wpf" });
        root.Items.Add(new TreeViewItem { Header = "WpfHexEditor.Sample.Docking" });
        root.Items.Add(new TreeViewItem { Header = "WpfHexEditorCore" });
        root.IsExpanded = true;
        treeView.Items.Add(root);
        return treeView;
    }

    private static UIElement CreatePropertiesContent()
    {
        var panel = new StackPanel { Margin = new Thickness(8) };
        panel.Children.Add(new TextBlock { Text = "Properties", FontWeight = FontWeights.Bold, Margin = new Thickness(0, 0, 0, 8) });
        panel.Children.Add(new TextBlock { Text = "Name:" });
        panel.Children.Add(new TextBox { Text = "DockItem", Margin = new Thickness(0, 2, 0, 8) });
        panel.Children.Add(new TextBlock { Text = "Type:" });
        panel.Children.Add(new TextBox { Text = "Panel", Margin = new Thickness(0, 2, 0, 8) });
        panel.Children.Add(new TextBlock { Text = "CanClose:" });
        panel.Children.Add(new CheckBox { IsChecked = true, Margin = new Thickness(0, 2, 0, 8) });
        return panel;
    }

    private static UIElement CreateOutputContent()
    {
        var textBox = new TextBox
        {
            IsReadOnly = true,
            TextWrapping = TextWrapping.Wrap,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            Background = System.Windows.Media.Brushes.Transparent,
            Foreground = System.Windows.Media.Brushes.LightGray,
            FontFamily = new System.Windows.Media.FontFamily("Cascadia Mono, Consolas, monospace"),
            FontSize = 12,
            BorderThickness = new Thickness(0)
        };

        OutputLogger.Register(textBox);
        OutputLogger.Section("Output");
        OutputLogger.Info("Docking system initialized.");

        return textBox;
    }

    private static UIElement CreateHexEditorContent(string? filePath)
    {
        // No FilePath metadata → "New Hex Document" with random sample data
        if (filePath is null)
        {
            var hexEditor = new HexEditor();
            var data = new byte[1024];
            new Random().NextBytes(data);
            var tempFile = Path.Combine(Path.GetTempPath(), $"hexedit-sample-{Guid.NewGuid():N}.bin");
            File.WriteAllBytes(tempFile, data);
            hexEditor.OpenFile(tempFile);
            OutputLogger.Debug($"New hex document created (temp: {tempFile})");
            return hexEditor;
        }

        // FilePath exists → "Open File" or layout restore
        if (File.Exists(filePath))
        {
            var hexEditor = new HexEditor();
            hexEditor.OpenFile(filePath);
            OutputLogger.Info($"Opened: {filePath}");
            return hexEditor;
        }

        // File no longer exists → log error, show placeholder
        OutputLogger.Error($"File not found: {filePath}");
        return new TextBlock
        {
            Text = $"File not found:\n{filePath}",
            Foreground = System.Windows.Media.Brushes.Gray,
            VerticalAlignment = VerticalAlignment.Center,
            HorizontalAlignment = HorizontalAlignment.Center,
            TextAlignment = TextAlignment.Center,
            FontSize = 14
        };
    }

    private static UIElement CreateDocumentContent(DockItem item)
    {
        var textBox = new TextBox
        {
            TextWrapping = TextWrapping.Wrap,
            AcceptsReturn = true,
            AcceptsTab = true,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            Text = $"This is document: {item.Title}\n\nEdit this text...",
            Background = System.Windows.Media.Brushes.Transparent,
            Foreground = System.Windows.Media.Brushes.LightGray,
            FontFamily = new System.Windows.Media.FontFamily("Cascadia Mono, Consolas, monospace"),
            FontSize = 13,
            BorderThickness = new Thickness(0)
        };
        return textBox;
    }

    // ─── Tab / panel management ────────────────────────────────────────

    private void OnTabCloseRequested(DockItem item)
    {
        if (_isLocked) return;

        try
        {
            _engine.Close(item);
            DockHost.RebuildVisualTree();
            UpdateStatusBar();
            OutputLogger.Info($"Closed tab: {item.Title} ({item.ContentId})");
        }
        catch (InvalidOperationException ex)
        {
            OutputLogger.Warn($"Cannot close '{item.Title}': {ex.Message}");
            StatusText.Text = $"Cannot close: {ex.Message}";
        }
    }

    private void OnOpenFile(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFileDialog
        {
            Filter = "All Files (*.*)|*.*"
        };

        if (dialog.ShowDialog() == true)
        {
            _documentCounter++;
            var item = new DockItem
            {
                Title = System.IO.Path.GetFileName(dialog.FileName),
                ContentId = $"doc-hex-{_documentCounter}",
                Metadata = { ["FilePath"] = dialog.FileName }
            };
            _engine.Dock(item, _layout.MainDocumentHost, DockDirection.Center);
            DockHost.RebuildVisualTree();
            UpdateStatusBar();
            OutputLogger.Info($"Open file: {dialog.FileName}");
            StatusText.Text = $"Opened: {dialog.FileName}";
        }
    }

    private void OnNewDocument(object sender, RoutedEventArgs e)
    {
        _documentCounter++;
        var item = new DockItem
        {
            Title = $"Document {_documentCounter}",
            ContentId = $"doc-{_documentCounter}"
        };
        _engine.Dock(item, _layout.MainDocumentHost, DockDirection.Center);
        DockHost.RebuildVisualTree();
        UpdateStatusBar();
        StatusText.Text = $"Created: {item.Title}";
    }

    private void OnNewHexDocument(object sender, RoutedEventArgs e)
    {
        _documentCounter++;
        var item = new DockItem
        {
            Title = $"Hex Editor {_documentCounter}",
            ContentId = $"doc-hex-{_documentCounter}"
        };
        _engine.Dock(item, _layout.MainDocumentHost, DockDirection.Center);
        DockHost.RebuildVisualTree();
        UpdateStatusBar();
        StatusText.Text = $"Created: {item.Title} (HexEditor)";
    }

    private void OnShowProperties(object sender, RoutedEventArgs e)
    {
        ShowOrCreatePanel("Properties", "panel-properties", DockDirection.Right);
    }

    private void OnShowSolutionExplorer(object sender, RoutedEventArgs e)
    {
        ShowOrCreatePanel("Solution Explorer", "panel-solution-explorer", DockDirection.Left);
    }

    private void OnShowOutput(object sender, RoutedEventArgs e)
    {
        ShowOrCreatePanel("Output", "panel-output", DockDirection.Bottom);
    }

    private void ShowOrCreatePanel(string title, string contentId, DockDirection direction)
    {
        var existing = _layout.FindItemByContentId(contentId);
        if (existing is not null)
        {
            existing.Owner?.ActiveItem?.Equals(existing);
            StatusText.Text = $"Activated: {title}";
            return;
        }

        var item = new DockItem { Title = title, ContentId = contentId };
        _engine.Dock(item, _layout.MainDocumentHost, direction);
        DockHost.RebuildVisualTree();
        UpdateStatusBar();
        StatusText.Text = $"Opened: {title}";
    }

    // ─── Menu: Layout ──────────────────────────────────────────────────

    private void OnSaveLayout(object sender, RoutedEventArgs e)
    {
        var dialog = new SaveFileDialog
        {
            Filter = "JSON Files (*.json)|*.json",
            DefaultExt = ".json",
            FileName = "dock-layout.json"
        };

        if (dialog.ShowDialog() == true)
        {
            File.WriteAllText(dialog.FileName, DockLayoutSerializer.Serialize(_layout));
            OutputLogger.Info($"Layout saved to: {dialog.FileName}");
            StatusText.Text = $"Layout saved: {dialog.FileName}";
        }
    }

    private void OnLoadLayout(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFileDialog
        {
            Filter = "JSON Files (*.json)|*.json",
            DefaultExt = ".json"
        };

        if (dialog.ShowDialog() == true)
        {
            try
            {
                var layout = DockLayoutSerializer.Deserialize(File.ReadAllText(dialog.FileName));
                ApplyLayout(layout);
                OutputLogger.Info($"Layout loaded from: {dialog.FileName}");
                StatusText.Text = $"Layout loaded: {dialog.FileName}";
            }
            catch (Exception ex)
            {
                OutputLogger.Error($"Failed to load layout: {ex.Message}");
                MessageBox.Show($"Failed to load layout:\n{ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }

    private void OnResetLayout(object sender, RoutedEventArgs e)
    {
        SetupDefaultLayout();
        OutputLogger.Info("Layout reset to default.");
        StatusText.Text = "Layout reset to default";
    }

    // ─── Menu: other ───────────────────────────────────────────────────

    private void OnToggleLock(object sender, RoutedEventArgs e)
    {
        _isLocked = LockMenuItem.IsChecked;
        _layout.MainDocumentHost.LockMode = _isLocked ? DockLockMode.Full : DockLockMode.None;

        foreach (var group in _layout.GetAllGroups())
            group.LockMode = _isLocked ? DockLockMode.Full : DockLockMode.None;

        OutputLogger.Info(_isLocked ? "Layout locked." : "Layout unlocked.");
        StatusText.Text = _isLocked ? "Layout LOCKED" : "Layout UNLOCKED";
        UpdateStatusBar();
    }

    private void OnDarkTheme(object sender, RoutedEventArgs e)
    {
        Application.Current.Resources.MergedDictionaries.Clear();
        Application.Current.Resources.MergedDictionaries.Add(
            new ResourceDictionary { Source = new Uri("pack://application:,,,/WpfHexEditor.Docking.Wpf;component/Themes/DarkTheme.xaml") });
        SyncAllHexEditorThemes();
        OutputLogger.Info("Theme changed to Dark.");
        StatusText.Text = "Theme: Dark";
    }

    private void OnLightTheme(object sender, RoutedEventArgs e)
    {
        Application.Current.Resources.MergedDictionaries.Clear();
        Application.Current.Resources.MergedDictionaries.Add(
            new ResourceDictionary { Source = new Uri("pack://application:,,,/WpfHexEditor.Docking.Wpf;component/Themes/Generic.xaml") });
        SyncAllHexEditorThemes();
        OutputLogger.Info("Theme changed to Light.");
        StatusText.Text = "Theme: Light";
    }

    /// <summary>
    /// Re-applies theme colors to all HexEditor instances in the docking layout.
    /// </summary>
    private void SyncAllHexEditorThemes()
    {
        foreach (var editor in FindVisualChildren<HexEditor>(this))
            editor.ApplyThemeFromResources();
    }

    private static IEnumerable<T> FindVisualChildren<T>(DependencyObject parent) where T : DependencyObject
    {
        var count = System.Windows.Media.VisualTreeHelper.GetChildrenCount(parent);
        for (int i = 0; i < count; i++)
        {
            var child = System.Windows.Media.VisualTreeHelper.GetChild(parent, i);
            if (child is T t)
                yield return t;
            foreach (var descendant in FindVisualChildren<T>(child))
                yield return descendant;
        }
    }

    private void OnExit(object sender, RoutedEventArgs e)
    {
        Close();
    }

    // ─── Status bar ────────────────────────────────────────────────────

    private void UpdateStatusBar()
    {
        var panelCount = _layout.GetAllGroups().Sum(g => g.Items.Count);
        var lockState = _isLocked ? " [LOCKED]" : "";
        PanelCountText.Text = $"Panels: {panelCount}{lockState}";
    }
}
