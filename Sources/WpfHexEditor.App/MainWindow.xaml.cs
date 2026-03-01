//////////////////////////////////////////////
// Apache 2.0  - 2026
// Author : Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.5, Claude Sonnet 4.6, Claude Opus 4.6
//////////////////////////////////////////////

using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using Microsoft.Win32;
using HexEditorControl = WpfHexEditor.HexEditor.HexEditor;
using WpfHexEditor.Docking.Core;
using WpfHexEditor.Docking.Core.Nodes;
using WpfHexEditor.Docking.Core.Serialization;
using WpfHexEditor.Docking.Wpf;
using WpfHexEditor.App.Controls;
using WpfHexEditor.Editor.Core;
using WpfHexEditor.WindowPanels.Panels;
using WpfHexEditor.ProjectSystem;
using WpfHexEditor.ProjectSystem.Dialogs;
using WpfHexEditor.ProjectSystem.Services;
using WpfHexEditor.ProjectSystem.Templates;

namespace WpfHexEditor.App;

public partial class MainWindow : Window, INotifyPropertyChanged
{
    // ─── INotifyPropertyChanged ────────────────────────────────────────
    public event PropertyChangedEventHandler? PropertyChanged;
    private void OnPropertyChanged([CallerMemberName] string? name = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));

    // ─── Constants ─────────────────────────────────────────────────────
    private static readonly string LayoutFilePath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "WpfHexEditor", "Sample.Docking", "layout.json");

    private const string ParsedFieldsPanelContentId   = "panel-parsed-fields";
    private const string SolutionExplorerContentId    = "panel-solution-explorer";

    // ─── Fields ────────────────────────────────────────────────────────
    private DockLayoutRoot _layout = null!;
    private DockEngine     _engine = null!;
    private int  _documentCounter;
    private bool _isLocked;

    // Content cache: ContentId → created UIElement
    private readonly Dictionary<string, UIElement> _contentCache = new();

    // Per-document format tracking: ContentId → last logged format name
    private readonly Dictionary<string, string> _loggedFormats = new();

    // ParsedFieldsPanel (persistent singleton)
    private ParsedFieldsPanel? _parsedFieldsPanel;
    private HexEditorControl?  _connectedHexEditor;

    // SolutionExplorer (persistent singleton)
    private SolutionExplorerPanel? _solutionExplorerPanel;

    // Properties panel (persistent singleton)
    private PropertiesPanel? _propertiesPanel;

    // SolutionManager
    private readonly ISolutionManager _solutionManager = SolutionManager.Instance;

    // ─── Bindable properties ────────────────────────────────────────────

    private IDocumentEditor? _activeDocumentEditor;
    public IDocumentEditor? ActiveDocumentEditor
    {
        get => _activeDocumentEditor;
        private set
        {
            if (_activeDocumentEditor != null)
            {
                _activeDocumentEditor.TitleChanged    -= OnEditorTitleChanged;
                _activeDocumentEditor.ModifiedChanged -= OnEditorModifiedChanged;
                _activeDocumentEditor.StatusMessage   -= OnEditorStatusMessage;
            }
            _activeDocumentEditor = value;
            if (_activeDocumentEditor != null)
            {
                _activeDocumentEditor.TitleChanged    += OnEditorTitleChanged;
                _activeDocumentEditor.ModifiedChanged += OnEditorModifiedChanged;
                _activeDocumentEditor.StatusMessage   += OnEditorStatusMessage;
            }
            OnPropertyChanged();
        }
    }

    private HexEditorControl? _activeHexEditor;
    public HexEditorControl? ActiveHexEditor
    {
        get => _activeHexEditor;
        private set { _activeHexEditor = value; OnPropertyChanged(); }
    }

    private IStatusBarContributor? _activeStatusBarContributor;
    public IStatusBarContributor? ActiveStatusBarContributor
    {
        get => _activeStatusBarContributor;
        private set { _activeStatusBarContributor = value; OnPropertyChanged(); }
    }

    private bool _hasSolution;
    public bool HasSolution
    {
        get => _hasSolution;
        private set { _hasSolution = value; OnPropertyChanged(); }
    }

    // ─── Constructor ───────────────────────────────────────────────────
    public MainWindow()
    {
        InitializeComponent();
        Closing      += OnWindowClosing;
        Loaded       += OnLoaded;
        StateChanged += OnStateChanged;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        // Wire SolutionManager events before restoring layout
        _solutionManager.SolutionChanged += OnSolutionChanged;
        _solutionManager.ProjectChanged  += OnProjectChanged;
        _solutionManager.ItemAdded       += OnProjectItemAdded;

        LoadSavedLayoutOrDefault();
        PopulateRecentMenus();
    }

    private void OnWindowClosing(object? sender, CancelEventArgs e)
    {
        AutoSaveLayout();
    }

    // ─── SolutionManager event handlers ────────────────────────────────

    private void OnSolutionChanged(object? sender, SolutionChangedEventArgs e)
    {
        HasSolution = _solutionManager.CurrentSolution != null;
        _solutionExplorerPanel?.SetSolution(_solutionManager.CurrentSolution);
        PopulateRecentMenus();

        // Update window title
        Title = _solutionManager.CurrentSolution is { } sol
            ? $"WpfHexEditor — {sol.Name}"
            : "WpfHexEditor";

        // Update status bar
        SolutionText.Text = _solutionManager.CurrentSolution is { } s
            ? $"Solution: {s.Name}"
            : "";
    }

    private void OnProjectChanged(object? sender, ProjectChangedEventArgs e)
    {
        _solutionExplorerPanel?.SetSolution(_solutionManager.CurrentSolution);
    }

    private void OnProjectItemAdded(object? sender, ProjectItemEventArgs e)
    {
        _solutionExplorerPanel?.SetSolution(_solutionManager.CurrentSolution);
    }

    // ─── Layout persistence ────────────────────────────────────────────

    private void LoadSavedLayoutOrDefault()
    {
        if (File.Exists(LayoutFilePath))
        {
            try
            {
                var layout = DockLayoutSerializer.Deserialize(File.ReadAllText(LayoutFilePath));
                RestoreWindowState(layout);
                // Panel must exist BEFORE ApplyLayout so the content factory can early-connect
                // it to the first HexEditor and format-detection events are not missed.
                _parsedFieldsPanel ??= new ParsedFieldsPanel();
                ApplyLayout(layout);
                EnsureParsedFieldsPanel();
                OutputLogger.Info($"Layout restored from: {LayoutFilePath}");
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

    private void RestoreWindowState(DockLayoutRoot layout)
    {
        if (layout.WindowWidth is > 0 && layout.WindowHeight is > 0)
        {
            Left   = layout.WindowLeft ?? Left;
            Top    = layout.WindowTop  ?? Top;
            Width  = layout.WindowWidth.Value;
            Height = layout.WindowHeight.Value;
        }

        if (layout.WindowState is 2)
            WindowState = WindowState.Maximized;
    }

    private void AutoSaveLayout()
    {
        try
        {
            DockHost.SyncLayoutSizes();

            _layout.WindowState = (int)WindowState;
            var rb = RestoreBounds;
            if (rb != Rect.Empty)
            {
                _layout.WindowLeft   = rb.Left;
                _layout.WindowTop    = rb.Top;
                _layout.WindowWidth  = rb.Width;
                _layout.WindowHeight = rb.Height;
            }
            else
            {
                _layout.WindowLeft   = Left;
                _layout.WindowTop    = Top;
                _layout.WindowWidth  = Width;
                _layout.WindowHeight = Height;
            }

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
        _parsedFieldsPanel    ??= new ParsedFieldsPanel();
        _solutionExplorerPanel ??= CreateSolutionExplorerPanel();

        var layout = new DockLayoutRoot();
        var engine = new DockEngine(layout);

        layout.MainDocumentHost.AddItem(new DockItem { Title = "Welcome", ContentId = "doc-welcome" });

        var seItem = new DockItem { Title = "Solution Explorer", ContentId = SolutionExplorerContentId };
        engine.Dock(seItem, layout.MainDocumentHost, DockDirection.Left);

        var output = new DockItem { Title = "Output", ContentId = "panel-output" };
        engine.Dock(output, layout.MainDocumentHost, DockDirection.Bottom);

        var parsedFields = new DockItem
        {
            Title      = "Parsed Fields",
            ContentId  = ParsedFieldsPanelContentId,
            CanClose   = false
        };
        engine.Dock(parsedFields, layout.MainDocumentHost, DockDirection.Right);

        ApplyLayout(layout, engine);
        OutputLogger.Info("Default layout applied.");
    }

    private void ApplyLayout(DockLayoutRoot layout, DockEngine? engine = null)
    {
        DockHost.TabCloseRequested  -= OnTabCloseRequested;
        DockHost.ActiveItemChanged  -= OnActiveDocumentChanged;

        _layout  = layout;
        _engine  = engine ?? new DockEngine(_layout);
        _isLocked = false;
        LockMenuItem.IsChecked = false;

        DockHost.ContentFactory    = CreateContentForItem;
        DockHost.TabCloseRequested += OnTabCloseRequested;
        DockHost.ActiveItemChanged += OnActiveDocumentChanged;
        DockHost.Layout = _layout;

        SyncDocumentCounter();
        UpdateStatusBar();
    }

    private void EnsureParsedFieldsPanel()
    {
        _parsedFieldsPanel ??= new ParsedFieldsPanel();

        if (_layout.FindItemByContentId(ParsedFieldsPanelContentId) == null)
        {
            var item = new DockItem
            {
                Title    = "Parsed Fields",
                ContentId = ParsedFieldsPanelContentId,
                CanClose  = false
            };
            _engine.Dock(item, _layout.MainDocumentHost, DockDirection.Right);
            DockHost.RebuildVisualTree();
            OutputLogger.Info("ParsedFields panel added to restored layout.");
        }
    }

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

    // ─── Content factory (with cache) ──────────────────────────────────

    private object CreateContentForItem(DockItem item)
    {
        if (_contentCache.TryGetValue(item.ContentId, out var cached))
            return cached;
        var content = BuildContentForItem(item);
        _contentCache[item.ContentId] = content;
        return content;
    }

    private UIElement BuildContentForItem(DockItem item) =>
        item.ContentId switch
        {
            SolutionExplorerContentId  => CreateSolutionExplorerContent(),
            "panel-output"             => CreateOutputContent(),
            ParsedFieldsPanelContentId => CreateParsedFieldsContent(),
            "panel-properties"         => CreatePropertiesContent(),
            _ when item.ContentId.StartsWith("doc-hex-") => CreateHexEditorContent(
                item.Metadata.TryGetValue("FilePath",    out var fp)   ? fp   : null,
                item.Metadata.TryGetValue("DisplayName", out var dn)   ? dn   : null,
                item.Metadata.TryGetValue("IsNewFile",   out var isNew) && isNew == "true"),
            _ => CreateDocumentContent(item)
        };

    // ─── Panel factories ───────────────────────────────────────────────

    private UIElement CreateSolutionExplorerContent()
    {
        _solutionExplorerPanel ??= CreateSolutionExplorerPanel();
        return _solutionExplorerPanel;
    }

    private SolutionExplorerPanel CreateSolutionExplorerPanel()
    {
        var panel = new SolutionExplorerPanel();
        panel.ItemActivated            += OnSolutionExplorerItemActivated;
        panel.ItemSelected             += OnSolutionExplorerItemSelected;
        panel.ItemRenameRequested      += OnSolutionExplorerItemRenameRequested;
        panel.ItemDeleteRequested      += OnSolutionExplorerItemDeleteRequested;
        panel.DefaultTblChangeRequested += OnDefaultTblChangeRequested;
        // Sync current solution if already loaded
        panel.SetSolution(_solutionManager.CurrentSolution);
        return panel;
    }

    private UIElement CreatePropertiesContent()
    {
        _propertiesPanel ??= new PropertiesPanel();
        return _propertiesPanel;
    }

    private static UIElement CreateOutputContent() => new OutputPanel();

    private UIElement CreateParsedFieldsContent()
    {
        _parsedFieldsPanel ??= new ParsedFieldsPanel();
        return _parsedFieldsPanel;
    }

    private UIElement CreateHexEditorContent(
        string? filePath,
        string? displayName = null,
        bool    isNewFile   = false)
    {
        var hexEditor = new HexEditorControl();
        hexEditor.ShowStatusBar = false;

        if (_parsedFieldsPanel != null && _connectedHexEditor == null)
        {
            _connectedHexEditor = hexEditor;
            hexEditor.ConnectParsedFieldsPanel(_parsedFieldsPanel);
            ActiveDocumentEditor       = hexEditor as IDocumentEditor;
            ActiveHexEditor            = hexEditor;
            ActiveStatusBarContributor = hexEditor as IStatusBarContributor;
        }

        // ── Phase 12: in-memory new document ──────────────────────────
        if (isNewFile)
        {
            hexEditor.OpenNew(displayName ?? "New1.bin");
            OutputLogger.Info($"New in-memory document: {displayName}");
            return hexEditor;
        }

        // ── File-backed document ───────────────────────────────────────
        if (filePath is null)
        {
            // Legacy fallback: unnamed temp document (e.g. Welcome tab)
            var data = new byte[1024];
            new Random().NextBytes(data);
            var tempFile = Path.Combine(Path.GetTempPath(), $"hexedit-sample-{Guid.NewGuid():N}.bin");
            File.WriteAllBytes(tempFile, data);
            hexEditor.OpenFile(tempFile);
            OutputLogger.Debug($"New hex document created (temp: {tempFile})");
            return hexEditor;
        }

        if (File.Exists(filePath))
        {
            hexEditor.OpenFile(filePath);
            OutputLogger.Info($"Opened: {filePath}");
            return hexEditor;
        }

        OutputLogger.Error($"File not found: {filePath}");
        return new TextBlock
        {
            Text = $"File not found:\n{filePath}",
            Foreground = System.Windows.Media.Brushes.Gray,
            VerticalAlignment   = VerticalAlignment.Center,
            HorizontalAlignment = HorizontalAlignment.Center,
            TextAlignment       = TextAlignment.Center,
            FontSize = 14
        };
    }

    private static UIElement CreateDocumentContent(DockItem item) =>
        new TextBox
        {
            TextWrapping           = TextWrapping.Wrap,
            AcceptsReturn          = true,
            AcceptsTab             = true,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            Text = $"This is document: {item.Title}\n\nEdit this text...",
            Background            = System.Windows.Media.Brushes.Transparent,
            Foreground            = System.Windows.Media.Brushes.LightGray,
            FontFamily            = new System.Windows.Media.FontFamily("Cascadia Mono, Consolas, monospace"),
            FontSize              = 13,
            BorderThickness       = new Thickness(0)
        };

    // ─── SolutionExplorer events ────────────────────────────────────────

    private void OnSolutionExplorerItemActivated(object? sender, ProjectItemActivatedEventArgs e)
    {
        OpenProjectItem(e.Item, e.Project);
    }

    private void OpenProjectItem(IProjectItem item, IProject project)
    {
        // Re-use an existing tab for this project item
        var existingContentId = $"doc-proj-{item.Id}";
        if (_layout.FindItemByContentId(existingContentId) is { } existing)
        {
            existing.Owner?.ActiveItem?.Equals(existing);
            return;
        }

        // Create a new HexEditor tab for binary items; generic for others
        _documentCounter++;
        var dockItem = new DockItem
        {
            Title     = item.Name,
            ContentId = existingContentId,
            Metadata  =
            {
                ["FilePath"]  = item.AbsolutePath,
                ["ProjectId"] = project.Id,
                ["ItemId"]    = item.Id
            }
        };

        _engine.Dock(dockItem, _layout.MainDocumentHost, DockDirection.Center);
        DockHost.RebuildVisualTree();
        UpdateStatusBar();
        _solutionManager.PushRecentFile(item.AbsolutePath);
        OutputLogger.Info($"Opened project item: {item.Name}");
    }

    private void OnDefaultTblChangeRequested(object? sender, DefaultTblChangeEventArgs e)
    {
        _solutionManager.SetDefaultTbl(e.Project, e.TblItem);
        OutputLogger.Info(e.TblItem is not null
            ? $"Default TBL set to '{e.TblItem.Name}' in project '{e.Project.Name}'"
            : $"Default TBL cleared in project '{e.Project.Name}'");
    }

    private void OnSolutionExplorerItemSelected(object? sender, ProjectItemEventArgs e)
    {
        _propertiesPanel?.SetProvider(new ProjectItemPropertyProvider(e.Item));
    }

    private void OnSolutionExplorerItemRenameRequested(object? sender, ProjectItemEventArgs e)
    {
        if (e.Project is null) return;

        // Simple programmatic rename dialog
        var win = new Window
        {
            Title                  = "Rename",
            Owner                  = this,
            Width                  = 380,
            Height                 = 120,
            WindowStartupLocation  = WindowStartupLocation.CenterOwner,
            ResizeMode             = ResizeMode.NoResize,
            ShowInTaskbar          = false,
        };

        var tb = new System.Windows.Controls.TextBox
        {
            Text              = e.Item.Name,
            Margin            = new Thickness(10, 12, 10, 0),
            VerticalAlignment = VerticalAlignment.Top,
        };
        tb.SelectAll();

        var okBtn = new System.Windows.Controls.Button
        {
            Content            = "OK",
            IsDefault          = true,
            Width              = 75,
            Margin             = new Thickness(0, 0, 10, 10),
            HorizontalAlignment = HorizontalAlignment.Right,
            VerticalAlignment  = VerticalAlignment.Bottom,
        };
        okBtn.Click += (_, _) => win.DialogResult = true;

        var grid = new System.Windows.Controls.Grid();
        grid.Children.Add(tb);
        grid.Children.Add(okBtn);
        win.Content = grid;

        if (win.ShowDialog() != true) return;
        var newName = tb.Text.Trim();
        if (newName.Length == 0 || newName == e.Item.Name) return;

        _ = _solutionManager.RenameItemAsync(e.Project, e.Item, newName);
        OutputLogger.Info($"Renamed '{e.Item.Name}' → '{newName}'");
    }

    private void OnSolutionExplorerItemDeleteRequested(object? sender, ProjectItemEventArgs e)
    {
        if (e.Project is null) return;

        var result = MessageBox.Show(
            $"Remove '{e.Item.Name}' from the project?\n(The file on disk will not be deleted.)",
            "Remove from project",
            MessageBoxButton.YesNo,
            MessageBoxImage.Question);

        if (result != MessageBoxResult.Yes) return;

        _ = _solutionManager.RemoveItemAsync(e.Project, e.Item, deleteFromDisk: false);
        OutputLogger.Info($"Removed '{e.Item.Name}' from project '{e.Project.Name}'");
    }

    // ─── Active document tracking ───────────────────────────────────────

    private void OnActiveDocumentChanged(DockItem item)
    {
        if (!_contentCache.TryGetValue(item.ContentId, out var content))
            return;

        if (item.ContentId.StartsWith("panel-"))
            return;

        var hex = content as HexEditorControl;

        if (hex != _connectedHexEditor)
        {
            _connectedHexEditor?.DisconnectParsedFieldsPanel();
            _connectedHexEditor = hex;
            if (_connectedHexEditor != null)
                _connectedHexEditor.ConnectParsedFieldsPanel(_parsedFieldsPanel);
            else
                _parsedFieldsPanel?.Clear();
        }

        ActiveDocumentEditor = hex as IDocumentEditor;
        ActiveHexEditor      = hex;
        ActiveStatusBarContributor = hex as IStatusBarContributor;

        if (ActiveDocumentEditor == null)
            RefreshText.Text = "";
        else
            hex?.RefreshDocumentStatus();

        // Sync Solution Explorer highlight
        if (hex?.FileName is { Length: > 0 } fn)
            _solutionExplorerPanel?.SyncWithFile(fn);

        // Sync Properties panel provider
        var providerSource = content as IPropertyProviderSource;
        _propertiesPanel?.SetProvider(providerSource?.GetPropertyProvider());
    }

    // ─── IDocumentEditor event handlers ────────────────────────────────

    private void OnEditorTitleChanged(object? sender, string newTitle)
    {
        var contentId = _contentCache
            .FirstOrDefault(kv => ReferenceEquals(kv.Value, sender as UIElement)).Key;
        if (contentId != null)
        {
            var dockItem = _layout.FindItemByContentId(contentId);
            if (dockItem != null) dockItem.Title = newTitle;
        }
    }

    private void OnEditorModifiedChanged(object? sender, EventArgs e) { }

    private void OnEditorStatusMessage(object? sender, string message)
    {
        var parts = message.Split(new[] { "  |  " }, StringSplitOptions.None);

        var refreshPart = parts.FirstOrDefault(
            p => p.TrimStart().StartsWith("Refresh:", StringComparison.OrdinalIgnoreCase));
        RefreshText.Text = refreshPart?.Trim() ?? "";

        if (!message.StartsWith("Format detected:", StringComparison.OrdinalIgnoreCase))
            return;

        var formatPart = parts[0].Trim();

        var contentId = _contentCache
            .FirstOrDefault(kv => ReferenceEquals(kv.Value, sender as UIElement)).Key;

        if (contentId != null &&
            _loggedFormats.TryGetValue(contentId, out var prev) &&
            prev == formatPart)
            return;

        if (contentId != null)
            _loggedFormats[contentId] = formatPart;

        var hex      = sender as HexEditorControl;
        var filename = hex?.FileName is { Length: > 0 } fn
            ? Path.GetFileName(fn)
            : ActiveDocumentEditor?.Title?.TrimEnd('*', ' ') ?? "Unknown";

        OutputLogger.Info($"File: {filename}");
        OutputLogger.Info(formatPart);
    }

    private void StatusBarItem_MouseLeftButtonDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        if (sender is Border border && border.ContextMenu != null)
        {
            border.ContextMenu.DataContext     = border.DataContext;
            border.ContextMenu.PlacementTarget = border;
            border.ContextMenu.Placement       = System.Windows.Controls.Primitives.PlacementMode.Top;
            border.ContextMenu.IsOpen          = true;
            e.Handled = true;
        }
    }

    // ─── Tab / panel management ────────────────────────────────────────

    private void OnTabCloseRequested(DockItem item)
    {
        if (_isLocked) return;

        if (_contentCache.TryGetValue(item.ContentId, out var ctrl))
        {
            if (ctrl is HexEditorControl hex)
            {
                if (ReferenceEquals(hex, _connectedHexEditor))
                {
                    hex.DisconnectParsedFieldsPanel();
                    _connectedHexEditor = null;
                    _parsedFieldsPanel?.Clear();
                }
                if (ReferenceEquals(hex, ActiveHexEditor))
                {
                    ActiveDocumentEditor = null;
                    ActiveHexEditor      = null;
                    RefreshText.Text     = "";
                }
            }
            _contentCache.Remove(item.ContentId);
        }

        _loggedFormats.Remove(item.ContentId);

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
        }
    }

    // ─── Menu: File — New ──────────────────────────────────────────────

    private void OnNewSolution(object sender, RoutedEventArgs e)
    {
        var dlg = new NewSolutionDialog { Owner = this };
        if (dlg.ShowDialog() != true) return;

        _ = CreateSolutionAsync(dlg.SolutionDirectory, dlg.SolutionName);
    }

    private async Task CreateSolutionAsync(string directory, string name)
    {
        try
        {
            await _solutionManager.CreateSolutionAsync(directory, name);
            OutputLogger.Info($"Solution created: {name}");
        }
        catch (Exception ex)
        {
            OutputLogger.Error($"Failed to create solution: {ex.Message}");
            MessageBox.Show($"Failed to create solution:\n{ex.Message}", "Error",
                MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void OnNewProject(object sender, RoutedEventArgs e)
    {
        if (_solutionManager.CurrentSolution is null)
        {
            MessageBox.Show("Please create or open a solution first.", "No Solution",
                MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        var suggestedDir = Path.GetDirectoryName(_solutionManager.CurrentSolution.FilePath) ?? "";
        var dlg = new NewProjectDialog(suggestedDir) { Owner = this };
        if (dlg.ShowDialog() != true) return;

        _ = CreateProjectAsync(_solutionManager.CurrentSolution, dlg.ProjectDirectory, dlg.ProjectName);
    }

    private async Task CreateProjectAsync(ISolution solution, string directory, string name)
    {
        try
        {
            await _solutionManager.CreateProjectAsync(solution, directory, name);
            OutputLogger.Info($"Project created: {name}");
        }
        catch (Exception ex)
        {
            OutputLogger.Error($"Failed to create project: {ex.Message}");
            MessageBox.Show($"Failed to create project:\n{ex.Message}", "Error",
                MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void OnNewFile(object sender, RoutedEventArgs e)
    {
        var availableProjects = _solutionManager.CurrentSolution?.Projects
            as IReadOnlyList<IProject>
            ?? [];

        var dlg = new NewFileDialog(
            defaultDirectory: _solutionManager.CurrentSolution is { } sol
                ? Path.GetDirectoryName(sol.FilePath)
                : null,
            availableProjects: availableProjects)
        {
            Owner = this
        };

        if (dlg.ShowDialog() != true) return;

        _documentCounter++;

        if (dlg.SaveLater)
        {
            // ── In-memory (Phase 12): file written on first Ctrl+S ─────
            var item = new DockItem
            {
                Title     = dlg.FileName,
                ContentId = $"doc-hex-{_documentCounter}",
                Metadata  =
                {
                    ["IsNewFile"]    = "true",
                    ["DisplayName"]  = dlg.FileName
                }
            };
            _engine.Dock(item, _layout.MainDocumentHost, DockDirection.Center);
            DockHost.RebuildVisualTree();
            UpdateStatusBar();
            OutputLogger.Info($"New in-memory document: {dlg.FileName}");
        }
        else
        {
            // ── Write to disk immediately ──────────────────────────────
            try
            {
                Directory.CreateDirectory(dlg.FileDirectory);
                File.WriteAllBytes(dlg.FullPath, dlg.SelectedTemplate!.CreateContent());

                if (dlg.TargetProject is { } project)
                    _ = _solutionManager.AddItemAsync(project, dlg.FullPath);

                var item = new DockItem
                {
                    Title     = dlg.FileName,
                    ContentId = $"doc-hex-{_documentCounter}",
                    Metadata  = { ["FilePath"] = dlg.FullPath }
                };
                _engine.Dock(item, _layout.MainDocumentHost, DockDirection.Center);
                DockHost.RebuildVisualTree();
                UpdateStatusBar();
                _solutionManager.PushRecentFile(dlg.FullPath);
                PopulateRecentMenus();
                OutputLogger.Info($"New file created: {dlg.FullPath}");
            }
            catch (Exception ex)
            {
                OutputLogger.Error($"Failed to create file: {ex.Message}");
                MessageBox.Show($"Failed to create file:\n{ex.Message}", "Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }

    private void OnCloseAllDocuments(object sender, RoutedEventArgs e)
    {
        var docs = _layout.GetAllGroups()
            .SelectMany(g => g.Items)
            .Where(i => !i.ContentId.StartsWith("panel-"))
            .ToList();

        // Collect dirty documents
        var dirty = docs
            .Where(i => _contentCache.TryGetValue(i.ContentId, out var c)
                        && c is IDocumentEditor { IsDirty: true })
            .ToList();

        if (dirty.Count > 0)
        {
            var names   = string.Join("\n  • ", dirty.Select(i => i.Title));
            var result  = MessageBox.Show(
                $"Save changes to the following files?\n\n  • {names}",
                "Save Changes",
                MessageBoxButton.YesNoCancel,
                MessageBoxImage.Question);

            if (result == MessageBoxResult.Cancel) return;

            if (result == MessageBoxResult.Yes)
            {
                foreach (var d in dirty)
                {
                    if (_contentCache.TryGetValue(d.ContentId, out var c)
                        && c is IDocumentEditor editor)
                        editor.SaveCommand?.Execute(null);
                }
            }
        }

        foreach (var doc in docs)
            OnTabCloseRequested(doc);
    }

    // ─── Menu: File — Open ─────────────────────────────────────────────

    private void OnOpenSolutionOrProject(object sender, RoutedEventArgs e)
    {
        var dlg = new OpenFileDialog
        {
            Filter      = "Solution/Project Files (*.whsln;*.whproj)|*.whsln;*.whproj|" +
                          "Solution Files (*.whsln)|*.whsln|" +
                          "Project Files (*.whproj)|*.whproj",
            DefaultExt  = ".whsln",
            Title       = "Open Solution or Project"
        };

        if (dlg.ShowDialog() != true) return;

        _ = OpenSolutionAsync(dlg.FileName);
    }

    private async Task OpenSolutionAsync(string filePath)
    {
        try
        {
            await _solutionManager.OpenSolutionAsync(filePath);
            OutputLogger.Info($"Solution opened: {filePath}");
            EnsureSolutionExplorerVisible();
        }
        catch (Exception ex)
        {
            OutputLogger.Error($"Failed to open solution: {ex.Message}");
            MessageBox.Show($"Failed to open solution:\n{ex.Message}", "Error",
                MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void OnOpenFile(object sender, RoutedEventArgs e)
    {
        var dlg = new OpenFileDialog
        {
            Filter = "All Files (*.*)|*.*|Binary Files (*.bin;*.rom;*.exe)|*.bin;*.rom;*.exe|" +
                     "TBL Files (*.tbl;*.tblx)|*.tbl;*.tblx"
        };

        if (dlg.ShowDialog() != true) return;

        _documentCounter++;
        var item = new DockItem
        {
            Title     = Path.GetFileName(dlg.FileName),
            ContentId = $"doc-hex-{_documentCounter}",
            Metadata  = { ["FilePath"] = dlg.FileName }
        };
        _engine.Dock(item, _layout.MainDocumentHost, DockDirection.Center);
        DockHost.RebuildVisualTree();
        UpdateStatusBar();
        _solutionManager.PushRecentFile(dlg.FileName);
        PopulateRecentMenus();
        OutputLogger.Info($"Open file: {dlg.FileName}");
    }

    // ─── Menu: File — Close ────────────────────────────────────────────

    private void OnCloseActiveDocument(object sender, RoutedEventArgs e)
    {
        var active = _layout.GetAllGroups()
            .SelectMany(g => g.Items)
            .FirstOrDefault(i => i == i.Owner?.ActiveItem && !i.ContentId.StartsWith("panel-"));

        if (active != null)
            OnTabCloseRequested(active);
    }

    private void OnCloseSolution(object sender, RoutedEventArgs e)
    {
        _ = CloseSolutionAsync();
    }

    private async Task CloseSolutionAsync()
    {
        try
        {
            await _solutionManager.CloseSolutionAsync();
            OutputLogger.Info("Solution closed.");
        }
        catch (Exception ex)
        {
            OutputLogger.Error($"Failed to close solution: {ex.Message}");
        }
    }

    // ─── Menu: File — Save ─────────────────────────────────────────────

    private void OnSaveAll(object sender, RoutedEventArgs e)
    {
        if (_solutionManager.CurrentSolution != null)
            _ = _solutionManager.SaveSolutionAsync(_solutionManager.CurrentSolution);

        // Also save the active document editor
        ActiveDocumentEditor?.SaveCommand?.Execute(null);
        OutputLogger.Info("Save All executed.");
    }

    // ─── Menu: Project ─────────────────────────────────────────────────

    private void OnProjectAddNewItem(object sender, RoutedEventArgs e)
    {
        if (_solutionManager.CurrentSolution?.Projects.FirstOrDefault() is not { } project) return;

        var dlg = new AddNewItemDialog(project) { Owner = this };
        if (dlg.ShowDialog() != true || dlg.SelectedTemplate is null) return;

        _ = _solutionManager.CreateItemAsync(
            project,
            dlg.FileName,
            ProjectItemTypeHelper.FromExtension(Path.GetExtension(dlg.FileName)),
            virtualFolderId: dlg.TargetFolderId,
            initialContent:  dlg.SelectedTemplate.CreateContent());
    }

    private void OnProjectAddExistingItem(object sender, RoutedEventArgs e)
    {
        if (_solutionManager.CurrentSolution?.Projects.FirstOrDefault() is not { } project) return;

        var dlg = new OpenFileDialog
        {
            Title  = "Add Existing Item",
            Filter = "All Files (*.*)|*.*|Binary Files (*.bin;*.rom)|*.bin;*.rom|TBL Files (*.tbl;*.tblx)|*.tbl;*.tblx"
        };
        if (dlg.ShowDialog() != true) return;

        _ = _solutionManager.AddItemAsync(project, dlg.FileName);
    }

    private void OnProjectProperties(object sender, RoutedEventArgs e)
    {
        var project = _solutionManager.CurrentSolution?.Projects.FirstOrDefault();
        if (project is null) return;

        var dlg = new ProjectPropertiesDialog(project) { Owner = this };
        dlg.ShowDialog();
    }

    // ─── MRU menus (Recent Solutions / Recent Files) ───────────────────

    private void PopulateRecentMenus()
    {
        PopulateMruMenu(RecentSolutionsMenuItem,
            _solutionManager.RecentSolutions,
            path => _ = OpenSolutionAsync(path));

        PopulateMruMenu(RecentFilesMenuItem,
            _solutionManager.RecentFiles,
            path =>
            {
                _documentCounter++;
                var item = new DockItem
                {
                    Title     = Path.GetFileName(path),
                    ContentId = $"doc-hex-{_documentCounter}",
                    Metadata  = { ["FilePath"] = path }
                };
                _engine.Dock(item, _layout.MainDocumentHost, DockDirection.Center);
                DockHost.RebuildVisualTree();
                UpdateStatusBar();
            });
    }

    private static void PopulateMruMenu(
        MenuItem parent,
        IReadOnlyList<string> paths,
        Action<string> onSelected)
    {
        parent.Items.Clear();
        if (paths.Count == 0)
        {
            parent.Items.Add(new MenuItem
            {
                Header    = "(none)",
                IsEnabled = false,
                Style     = Application.Current.FindResource("DockDarkMenuItemStyle") as Style
            });
            return;
        }

        for (int i = 0; i < paths.Count; i++)
        {
            var path = paths[i];
            var mi = new MenuItem
            {
                Header = $"_{i + 1}  {path}",
                ToolTip = path,
                Style = Application.Current.FindResource("DockDarkMenuItemStyle") as Style
            };
            mi.Click += (_, _) => onSelected(path);
            parent.Items.Add(mi);
        }
    }

    // ─── View panel helpers ────────────────────────────────────────────

    private void OnShowProperties(object sender, RoutedEventArgs e)
        => ShowOrCreatePanel("Properties", "panel-properties", DockDirection.Right);

    private void OnShowSolutionExplorer(object sender, RoutedEventArgs e)
    {
        ShowOrCreatePanel("Solution Explorer", SolutionExplorerContentId, DockDirection.Left);
        // If a singleton already exists but panel was closed, re-dock
        if (_solutionExplorerPanel != null)
            _solutionExplorerPanel.SetSolution(_solutionManager.CurrentSolution);
    }

    private void OnShowOutput(object sender, RoutedEventArgs e)
        => ShowOrCreatePanel("Output", "panel-output", DockDirection.Bottom);

    private void OnShowParsedFields(object sender, RoutedEventArgs e)
        => ShowOrCreatePanel("Parsed Fields", ParsedFieldsPanelContentId, DockDirection.Right);

    private void ShowOrCreatePanel(string title, string contentId, DockDirection direction)
    {
        var existing = _layout.FindItemByContentId(contentId);
        if (existing is not null) return;

        var item = new DockItem { Title = title, ContentId = contentId };
        _engine.Dock(item, _layout.MainDocumentHost, direction);
        DockHost.RebuildVisualTree();
        UpdateStatusBar();
    }

    private void EnsureSolutionExplorerVisible()
    {
        if (_layout.FindItemByContentId(SolutionExplorerContentId) == null)
            ShowOrCreatePanel("Solution Explorer", SolutionExplorerContentId, DockDirection.Left);
    }

    // ─── Menu: Layout ──────────────────────────────────────────────────

    private void OnSaveLayout(object sender, RoutedEventArgs e)
    {
        var dlg = new SaveFileDialog
        {
            Filter      = "JSON Files (*.json)|*.json",
            DefaultExt  = ".json",
            FileName    = "dock-layout.json"
        };

        if (dlg.ShowDialog() != true) return;

        DockHost.SyncLayoutSizes();
        _layout.WindowState = (int)WindowState;
        var rb = RestoreBounds;
        if (rb != Rect.Empty)
        {
            _layout.WindowLeft   = rb.Left;
            _layout.WindowTop    = rb.Top;
            _layout.WindowWidth  = rb.Width;
            _layout.WindowHeight = rb.Height;
        }

        File.WriteAllText(dlg.FileName, DockLayoutSerializer.Serialize(_layout));
        OutputLogger.Info($"Layout saved to: {dlg.FileName}");
    }

    private void OnLoadLayout(object sender, RoutedEventArgs e)
    {
        var dlg = new OpenFileDialog
        {
            Filter     = "JSON Files (*.json)|*.json",
            DefaultExt = ".json"
        };

        if (dlg.ShowDialog() != true) return;

        try
        {
            var layout = DockLayoutSerializer.Deserialize(File.ReadAllText(dlg.FileName));
            _contentCache.Clear();
            _parsedFieldsPanel ??= new ParsedFieldsPanel();
            ApplyLayout(layout);
            EnsureParsedFieldsPanel();
            OutputLogger.Info($"Layout loaded from: {dlg.FileName}");
        }
        catch (Exception ex)
        {
            OutputLogger.Error($"Failed to load layout: {ex.Message}");
            MessageBox.Show($"Failed to load layout:\n{ex.Message}", "Error",
                MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void OnResetLayout(object sender, RoutedEventArgs e)
    {
        _contentCache.Clear();
        SetupDefaultLayout();
        OutputLogger.Info("Layout reset to default.");
    }

    // ─── Menu: other ───────────────────────────────────────────────────

    private void OnToggleLock(object sender, RoutedEventArgs e)
    {
        _isLocked = LockMenuItem.IsChecked;
        _layout.MainDocumentHost.LockMode = _isLocked ? DockLockMode.Full : DockLockMode.None;
        foreach (var group in _layout.GetAllGroups())
            group.LockMode = _isLocked ? DockLockMode.Full : DockLockMode.None;
        OutputLogger.Info(_isLocked ? "Layout locked." : "Layout unlocked.");
        UpdateStatusBar();
    }

    private void ApplyTheme(string themeFile, string themeName)
    {
        Application.Current.Resources.MergedDictionaries.Clear();
        Application.Current.Resources.MergedDictionaries.Add(
            new ResourceDictionary
            {
                Source = new Uri($"pack://application:,,,/WpfHexEditor.Docking.Wpf;component/Themes/{themeFile}")
            });
        SyncAllHexEditorThemes();
        OutputLogger.Info($"Theme changed to {themeName}.");
    }

    private void OnDarkTheme(object sender, RoutedEventArgs e)         => ApplyTheme("DarkTheme.xaml",         "Dark");
    private void OnLightTheme(object sender, RoutedEventArgs e)        => ApplyTheme("Generic.xaml",           "Light");
    private void OnVS2022DarkTheme(object sender, RoutedEventArgs e)   => ApplyTheme("VS2022DarkTheme.xaml",   "VS2022 Dark");
    private void OnDarkGlassTheme(object sender, RoutedEventArgs e)    => ApplyTheme("DarkGlassTheme.xaml",    "Dark Glass");
    private void OnVisualStudioTheme(object sender, RoutedEventArgs e) => ApplyTheme("VisualStudioTheme.xaml", "Visual Studio");
    private void OnCyberpunkTheme(object sender, RoutedEventArgs e)    => ApplyTheme("CyberpunkTheme.xaml",    "Cyberpunk");
    private void OnMinimalTheme(object sender, RoutedEventArgs e)      => ApplyTheme("MinimalTheme.xaml",      "Minimal");
    private void OnOfficeTheme(object sender, RoutedEventArgs e)       => ApplyTheme("OfficeTheme.xaml",       "Office");

    private void SyncAllHexEditorThemes()
    {
        foreach (var editor in FindVisualChildren<HexEditorControl>(this))
            editor.ApplyThemeFromResources();
    }

    private static IEnumerable<T> FindVisualChildren<T>(DependencyObject parent) where T : DependencyObject
    {
        var count = System.Windows.Media.VisualTreeHelper.GetChildrenCount(parent);
        for (int i = 0; i < count; i++)
        {
            var child = System.Windows.Media.VisualTreeHelper.GetChild(parent, i);
            if (child is T t) yield return t;
            foreach (var d in FindVisualChildren<T>(child)) yield return d;
        }
    }

    private void OnExit(object sender, RoutedEventArgs e) => Close();

    // ─── Title bar ───────────────────────────────────────────────────

    private void OnMinimize(object sender, RoutedEventArgs e) =>
        WindowState = WindowState.Minimized;

    private void OnMaximizeRestore(object sender, RoutedEventArgs e) =>
        WindowState = WindowState == WindowState.Maximized
            ? WindowState.Normal
            : WindowState.Maximized;

    private void OnCloseWindow(object sender, RoutedEventArgs e) => Close();

    private void OnStateChanged(object? sender, EventArgs e)
    {
        MaxRestoreButton.Content = WindowState == WindowState.Maximized ? "\uE923" : "\uE739";
        RootGrid.Margin = new Thickness(0);
    }

    // ─── WM_GETMINMAXINFO — maximize respects taskbar ────────────────

    [StructLayout(LayoutKind.Sequential)] private struct POINT { public int x, y; }
    [StructLayout(LayoutKind.Sequential)] private struct MINMAXINFO
    {
        public POINT ptReserved, ptMaxSize, ptMaxPosition, ptMinTrackSize, ptMaxTrackSize;
    }

    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);
        (PresentationSource.FromVisual(this) as HwndSource)?.AddHook(HwndHook);
    }

    private IntPtr HwndHook(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        const int WM_GETMINMAXINFO = 0x0024;
        if (msg == WM_GETMINMAXINFO)
        {
            var source = PresentationSource.FromVisual(this);
            if (source?.CompositionTarget != null)
            {
                var mmi = Marshal.PtrToStructure<MINMAXINFO>(lParam);
                var m   = source.CompositionTarget.TransformToDevice;
                var wa  = SystemParameters.WorkArea;
                mmi.ptMaxPosition.x = (int)(wa.Left   * m.M11);
                mmi.ptMaxPosition.y = (int)(wa.Top    * m.M22);
                mmi.ptMaxSize.x     = (int)(wa.Width  * m.M11);
                mmi.ptMaxSize.y     = (int)(wa.Height * m.M22);
                Marshal.StructureToPtr(mmi, lParam, true);
            }
        }
        return IntPtr.Zero;
    }

    // ─── Status bar ────────────────────────────────────────────────────

    private void UpdateStatusBar()
    {
        var panelCount = _layout.GetAllGroups().Sum(g => g.Items.Count);
        var lockState  = _isLocked ? " [LOCKED]" : "";
        PanelCountText.Text = $"Panels: {panelCount}{lockState}";
    }
}
