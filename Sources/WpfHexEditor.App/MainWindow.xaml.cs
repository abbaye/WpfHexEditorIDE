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
using System.Windows.Input;
using System.Windows.Interop;
using Microsoft.Win32;
using HexEditorControl = WpfHexEditor.HexEditor.HexEditor;
using WpfHexEditor.Docking.Core;
using WpfHexEditor.Docking.Core.Nodes;
using WpfHexEditor.Docking.Core.Serialization;
using WpfHexEditor.Docking.Wpf;
using WpfHexEditor.App.Controls;
using WpfHexEditor.App.Services;
using WpfHexEditor.Editor.Core;
using WpfHexEditor.Editor.TblEditor;
using WpfHexEditor.Editor.TblEditor.Models;
using WpfHexEditor.Editor.TblEditor.Services;
using WpfHexEditor.Editor.JsonEditor;
using WpfHexEditor.Editor.TextEditor;
using WpfHexEditor.Editor.ImageViewer;
using WpfHexEditor.Editor.EntropyViewer;
using WpfHexEditor.Editor.EntropyViewer.Controls;
using WpfHexEditor.Editor.DiffViewer;
using WpfHexEditor.Editor.DiffViewer.Controls;
using WpfHexEditor.Editor.DisassemblyViewer;
using WpfHexEditor.Editor.StructureEditor;
using WpfHexEditor.Editor.TileEditor;
using WpfHexEditor.Editor.AudioViewer;
using WpfHexEditor.Editor.ScriptEditor;
using WpfHexEditor.Panels.IDE;
using WpfHexEditor.Panels.IDE.Panels;
using WpfHexEditor.Panels.BinaryAnalysis;
using WpfHexEditor.Definitions;
using WpfHexEditor.ProjectSystem;
using WpfHexEditor.ProjectSystem.Dialogs;
using WpfHexEditor.ProjectSystem.Services;
using WpfHexEditor.ProjectSystem.Templates;
using System.Windows.Shell;

namespace WpfHexEditor.App;

public partial class MainWindow : Window, INotifyPropertyChanged
{
    // ─── INotifyPropertyChanged ────────────────────────────────────────
    public event PropertyChangedEventHandler? PropertyChanged;
    private void OnPropertyChanged([CallerMemberName] string? name = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));

    // ─── Static RoutedCommands (Find Next / Previous — F3/Shift+F3) ────
    public static readonly RoutedCommand FindNextCommand = new RoutedCommand(
        "FindNext", typeof(MainWindow),
        new InputGestureCollection { new KeyGesture(Key.F3) });

    public static readonly RoutedCommand FindPreviousCommand = new RoutedCommand(
        "FindPrevious", typeof(MainWindow),
        new InputGestureCollection { new KeyGesture(Key.F3, ModifierKeys.Shift) });

    // ─── Constants ─────────────────────────────────────────────────────
    private static readonly string LayoutFilePath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "WpfHexEditor", "Sample.Docking", "layout.json");

    private static readonly string SessionFilePath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "WpfHexEditor", "session.json");

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

    // M5: PropertyProvider cache — avoids recreating the provider on every tab switch
    private readonly Dictionary<UIElement, IPropertyProvider?> _propertyProviderCache = new();

    // ParsedFieldsPanel (persistent singleton)
    private ParsedFieldsPanel? _parsedFieldsPanel;
    private HexEditorControl?  _connectedHexEditor;

    // SolutionExplorer (persistent singleton)
    private SolutionExplorerPanel? _solutionExplorerPanel;

    // Properties panel (persistent singleton)
    private PropertiesPanel? _propertiesPanel;

    // Custom Parser Template panel (persistent singleton)
    private WpfHexEditor.Panels.BinaryAnalysis.CustomParserTemplatePanel? _customParserPanel;

    private const string CustomParserPanelContentId = "panel-custom-parser";

    // Error Panel (persistent singleton)
    private ErrorPanel? _errorPanel;
    private const string ErrorPanelContentId = "panel-errors";

    // Background file monitor (watches project files for external changes)
    private readonly FileMonitorDiagnosticSource _fileMonitorSource = new();
    private FileMonitorService? _fileMonitorService;

    // Output Panel (persistent singleton — pre-created so OutputLogger works from startup)
    private OutputPanel? _outputPanel;

    // SolutionManager
    private readonly ISolutionManager _solutionManager = SolutionManager.Instance;

    // Editor registry for doc-proj-* dispatcher
    private readonly IEditorRegistry _editorRegistry = new EditorRegistry();

    // ─── Bindable properties ────────────────────────────────────────────

    private IDocumentEditor? _activeDocumentEditor;
    public IDocumentEditor? ActiveDocumentEditor
    {
        get => _activeDocumentEditor;
        private set
        {
            if (_activeDocumentEditor != null)
            {
                _activeDocumentEditor.TitleChanged       -= OnEditorTitleChanged;
                _activeDocumentEditor.ModifiedChanged    -= OnEditorModifiedChanged;
                _activeDocumentEditor.StatusMessage      -= OnEditorStatusMessage;
                _activeDocumentEditor.OutputMessage      -= OnEditorOutputMessage;
                _activeDocumentEditor.OperationStarted   -= OnDocumentOperationStarted;
                _activeDocumentEditor.OperationProgress  -= OnDocumentOperationProgress;
                _activeDocumentEditor.OperationCompleted -= OnDocumentOperationCompleted;
            }
            _activeDocumentEditor = value;
            if (_activeDocumentEditor != null)
            {
                _activeDocumentEditor.TitleChanged       += OnEditorTitleChanged;
                _activeDocumentEditor.ModifiedChanged    += OnEditorModifiedChanged;
                _activeDocumentEditor.StatusMessage      += OnEditorStatusMessage;
                _activeDocumentEditor.OutputMessage      += OnEditorOutputMessage;
                _activeDocumentEditor.OperationStarted   += OnDocumentOperationStarted;
                _activeDocumentEditor.OperationProgress  += OnDocumentOperationProgress;
                _activeDocumentEditor.OperationCompleted += OnDocumentOperationCompleted;
            }
            // Sync progress bar immediately to reflect the new active document's state
            SyncProgressBarToActiveEditor(value);
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
        private set { _hasSolution = value; OnPropertyChanged(); OnPropertyChanged(nameof(IsProjectMenuEnabled)); }
    }

    // ── Long-running operation state (per active document) ───────────────
    private bool _isDocumentBusy;
    private bool _isClosingForced;
    /// <summary>
    /// True while the currently active document is performing a long-running operation.
    /// Switches instantly when the active tab changes.
    /// </summary>
    public bool IsDocumentBusy
    {
        get => _isDocumentBusy;
        private set
        {
            _isDocumentBusy = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(IsMenuEnabled));
            OnPropertyChanged(nameof(IsProjectMenuEnabled));
        }
    }

    /// <summary>
    /// False while the active document is busy — gates File and Edit menus.
    /// </summary>
    public bool IsMenuEnabled => !_isDocumentBusy;

    /// <summary>
    /// True only when a solution is loaded AND the active document is not busy.
    /// </summary>
    public bool IsProjectMenuEnabled => _hasSolution && !_isDocumentBusy;

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
        _solutionManager.SolutionChanged      += OnSolutionChanged;
        _solutionManager.ProjectChanged       += OnProjectChanged;
        _solutionManager.ItemAdded            += OnProjectItemAdded;
        _solutionManager.FormatUpgradeRequired += OnFormatUpgradeRequired;

        // Register editor factories (doc-proj-* dispatcher)
        _editorRegistry.Register(new TblEditorFactory());
        _editorRegistry.Register(new JsonEditorFactory());
        _editorRegistry.Register(new TextEditorFactory());
        _editorRegistry.Register(new ImageViewerFactory());
        _editorRegistry.Register(new EntropyViewerFactory());
        _editorRegistry.Register(new DiffViewerFactory());
        _editorRegistry.Register(new DisassemblyViewerFactory());
        _editorRegistry.Register(new StructureEditorFactory());
        _editorRegistry.Register(new TileEditorFactory());
        _editorRegistry.Register(new AudioViewerFactory());
        _editorRegistry.Register(new ScriptEditorFactory());

        // Register VS-style project templates
        ProjectTemplateRegistry.RegisterDefaults();

        // Pre-create OutputPanel so OutputLogger.Register is called before any Info/Error calls
        _outputPanel = new OutputPanel();

        LoadSavedLayoutOrDefault();
        PopulateRecentMenus();
        TryRestoreSession();
        HandleStartupFile();
    }

    private void OnWindowClosing(object? sender, CancelEventArgs e)
    {
        if (_isClosingForced)
        {
            _fileMonitorService?.Dispose();
            AutoSaveLayout();
            return;
        }

        // Collect all dirty items: solution/project files + open document editors
        var allDirty = CollectDirtySolutionItems();

        var dirtyDocs = _layout.GetAllGroups()
            .SelectMany(g => g.Items)
            .Where(i => !i.ContentId.StartsWith("panel-") &&
                        _contentCache.TryGetValue(i.ContentId, out var c) &&
                        c is IDocumentEditor { IsDirty: true })
            .ToList();

        allDirty.AddRange(dirtyDocs.Select(i => (i.ContentId, i.Title.TrimEnd('*', ' '))));

        if (allDirty.Count == 0)
        {
            AutoSaveLayout();
            return;
        }

        e.Cancel = true;
        _ = ConfirmAndCloseAsync(allDirty, dirtyDocs);
    }

    private async Task ConfirmAndCloseAsync(
        List<(string ContentId, string Title)> allDirty,
        List<DockItem> dirtyDocs)
    {
        var dlg = new Dialogs.SaveChangesDialog
        {
            DirtyItems = allDirty,
            Owner      = this
        };
        if (dlg.ShowDialog() != true || dlg.Choice == Dialogs.SaveChangesChoice.Cancel) return;

        if (dlg.Choice == Dialogs.SaveChangesChoice.Save)
        {
            var toSave = new HashSet<string>(dlg.SelectedContentIds);
            await SaveSelectedSolutionItemsAsync(dlg.SelectedContentIds);
            foreach (var doc in dirtyDocs)
            {
                if (toSave.Contains(doc.ContentId) &&
                    _contentCache.TryGetValue(doc.ContentId, out var c) &&
                    c is IDocumentEditor editor)
                    editor.SaveCommand?.Execute(null);
            }
        }

        _isClosingForced = true;
        AutoSaveLayout();
        Close();
    }

    // ─── SolutionManager event handlers ────────────────────────────────

    private void OnSolutionChanged(object? sender, SolutionChangedEventArgs e)
    {
        HasSolution = _solutionManager.CurrentSolution != null;
        _solutionExplorerPanel?.SetSolution(_solutionManager.CurrentSolution);
        PopulateRecentMenus();

        // Start or stop the background file monitor
        if (_solutionManager.CurrentSolution is { } sol)
        {
            _fileMonitorService ??= new FileMonitorService(_editorRegistry, _fileMonitorSource);
            var dirs = sol.Projects
                .Select(p => Path.GetDirectoryName(p.ProjectFilePath))
                .Where(d => d is not null)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Cast<string>();
            _fileMonitorService.StartWatching(dirs);
        }
        else
        {
            _fileMonitorService?.StopWatching();
        }

        // Update window title
        Title = _solutionManager.CurrentSolution is { } titleSol
            ? $"WpfHexEditor — {titleSol.Name}"
            : "WpfHexEditor";

        // Update status bar
        SolutionText.Text = _solutionManager.CurrentSolution is { } s
            ? $"Solution: {s.Name}"
            : "";
    }

    private void OnFormatUpgradeRequired(object? sender, FormatUpgradeRequiredEventArgs e)
    {
        // Dispatch to UI thread — event may arrive from an async context.
        Dispatcher.InvokeAsync(async () =>
        {
            var fileList = string.Join("\n  • ", e.AffectedFiles.Select(System.IO.Path.GetFileName));
            var msg =
                $"The following files use an older format (v{e.FromVersion}) " +
                $"that will be upgraded to v{e.ToVersion}:\n\n  • {fileList}\n\n" +
                $"A backup (.v{e.FromVersion}.bak) will be created for each file.\n\n" +
                "Upgrade now?";

            var result = MessageBox.Show(this, msg, "Format Upgrade Required",
                MessageBoxButton.YesNo, MessageBoxImage.Information,
                MessageBoxResult.Yes);

            if (result == MessageBoxResult.Yes)
            {
                try
                {
                    await _solutionManager.UpgradeFormatAsync(e.Solution);
                    OutputLogger.Info($"Solution '{e.Solution.Name}' upgraded from v{e.FromVersion} to v{e.ToVersion}.");
                }
                catch (Exception ex)
                {
                    MessageBox.Show(this, $"Upgrade failed: {ex.Message}", "Format Upgrade",
                        MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
            else
            {
                _solutionManager.SetReadOnlyFormat(e.Solution, readOnly: true);
                OutputLogger.Info($"Solution '{e.Solution.Name}' opened in read-only mode (format v{e.FromVersion}).");
                // Reflect read-only in the title bar
                Title = $"WpfHexEditor — {e.Solution.Name} [read-only format v{e.FromVersion}]";
            }
        });
    }

    private void OnProjectChanged(object? sender, ProjectChangedEventArgs e)
    {
        _solutionExplorerPanel?.SetSolution(_solutionManager.CurrentSolution);
    }

    private void OnProjectItemAdded(object? sender, ProjectItemEventArgs e)
    {
        _solutionExplorerPanel?.SetSolution(_solutionManager.CurrentSolution);

        // When a FormatDefinition is added, inject it into all open HexEditors
        // that belong to the same project so ParsedFields auto-detects the new format.
        if (e.Item.ItemType == ProjectItemType.FormatDefinition)
            InjectFormatDefinitionToOpenEditors(e.Project, e.Item.AbsolutePath);
    }

    /// <summary>
    /// Loads <paramref name="whfmtPath"/> into every HexEditor tab that belongs to
    /// <paramref name="project"/> and triggers a re-detection of the current file.
    /// </summary>
    private void InjectFormatDefinitionToOpenEditors(IProject project, string whfmtPath)
    {
        foreach (var (contentId, uiElement) in _contentCache)
        {
            if (uiElement is not HexEditorControl hex) continue;

            // Check that this tab belongs to the same project
            var dockItem = _engine.Layout.FindItemByContentId(contentId);
            if (dockItem is null) continue;
            if (!dockItem.Metadata.TryGetValue("ProjectId", out var projId) || projId != project.Id)
                continue;

            hex.LoadFormatDefinition(whfmtPath);

            // Re-run auto-detection if the editor already has a file open
            if (hex.FileName is { Length: > 0 })
                hex.AutoDetectAndApplyFormat(hex.FileName);
        }
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
                EnsureErrorPanel();
                OutputLogger.Debug($"Layout restored from: {LayoutFilePath}");
                return;
            }
            catch (Exception ex)
            {
                OutputLogger.Error($"Failed to restore layout: {ex.Message}");
            }
        }

        OutputLogger.Debug("No saved layout found, using defaults.");
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
            OutputLogger.Debug($"Layout auto-saved to: {LayoutFilePath}");
            SaveSession();
        }
        catch (Exception ex)
        {
            OutputLogger.Error($"Failed to auto-save layout: {ex.Message}");
        }
    }

    private void SaveSession()
    {
        try
        {
            var solutionPath = _solutionManager.CurrentSolution?.FilePath;
            var json = System.Text.Json.JsonSerializer.Serialize(
                new { lastSolutionPath = solutionPath },
                new System.Text.Json.JsonSerializerOptions { WriteIndented = true });
            Directory.CreateDirectory(Path.GetDirectoryName(SessionFilePath)!);
            File.WriteAllText(SessionFilePath, json);
        }
        catch (Exception ex)
        {
            OutputLogger.Error($"Failed to save session: {ex.Message}");
        }
    }

    private void TryRestoreSession()
    {
        // Command-line arg takes priority over session restore
        if (!string.IsNullOrEmpty(App.StartupFilePath)) return;
        try
        {
            if (!File.Exists(SessionFilePath)) return;
            using var doc = System.Text.Json.JsonDocument.Parse(File.ReadAllText(SessionFilePath));
            if (!doc.RootElement.TryGetProperty("lastSolutionPath", out var prop)) return;
            if (prop.ValueKind != System.Text.Json.JsonValueKind.String) return;
            var path = prop.GetString();
            if (string.IsNullOrEmpty(path) || !File.Exists(path)) return;
            _ = OpenSolutionAsync(path);
        }
        catch (Exception ex)
        {
            OutputLogger.Error($"Failed to restore session: {ex.Message}");
        }
    }

    private void HandleStartupFile()
    {
        var path = App.StartupFilePath;
        if (string.IsNullOrEmpty(path)) return;

        var ext = Path.GetExtension(path).ToLowerInvariant();
        if (ext is ".whsln" or ".whproj")
        {
            if (File.Exists(path)) _ = OpenSolutionAsync(path);
            else OutputLogger.Error($"Startup solution not found: {path}");
            return;
        }
        if (!File.Exists(path)) { OutputLogger.Error($"Startup file not found: {path}"); return; }

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
        _solutionManager.PushRecentFile(path);
        PopulateRecentMenus();
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

        // Error panel docked first so Output (added last) becomes the active tab
        var errorsItem = new DockItem { Title = "Error List", ContentId = ErrorPanelContentId };
        engine.Dock(errorsItem, layout.MainDocumentHost, DockDirection.Bottom);

        var output = new DockItem { Title = "Output", ContentId = "panel-output" };
        engine.Dock(output, errorsItem.Owner!, DockDirection.Center);

        var parsedFields = new DockItem
        {
            Title      = "Parsed Fields",
            ContentId  = ParsedFieldsPanelContentId,
            CanClose   = false
        };
        engine.Dock(parsedFields, layout.MainDocumentHost, DockDirection.Right);

        ApplyLayout(layout, engine);
        OutputLogger.Debug("Default layout applied.");
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
            OutputLogger.Debug("ParsedFields panel added to restored layout.");
        }
    }

    private void EnsureErrorPanel()
    {
        if (_layout.FindItemByContentId(ErrorPanelContentId) != null) return;

        var item = new DockItem { Title = "Error List", ContentId = ErrorPanelContentId };

        // Prefer to dock alongside the Output panel if it exists
        var outputItem = _layout.FindItemByContentId("panel-output");
        if (outputItem?.Owner is { } outputGroup)
            _engine.Dock(item, outputGroup, DockDirection.Center);
        else
            _engine.Dock(item, _layout.MainDocumentHost, DockDirection.Bottom);

        DockHost.RebuildVisualTree();
        OutputLogger.Debug("Error panel added to restored layout.");
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
            CustomParserPanelContentId => CreateCustomParserContent(),
            ErrorPanelContentId        => CreateErrorPanelContent(),
            _ when item.ContentId.StartsWith("doc-hex-") => CreateHexEditorContent(
                item.Metadata.TryGetValue("FilePath",    out var fp)   ? fp   : null,
                item.Metadata.TryGetValue("DisplayName", out var dn)   ? dn   : null,
                item.Metadata.TryGetValue("IsNewFile",   out var isNew) && isNew == "true"),
            _ when item.ContentId.StartsWith("doc-proj-") => CreateProjectItemContent(item),
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
        panel.ItemActivated             += OnSolutionExplorerItemActivated;
        panel.ItemSelected              += OnSolutionExplorerItemSelected;
        panel.ItemRenameRequested       += OnSolutionExplorerItemRenameRequested;
        panel.ItemDeleteRequested       += OnSolutionExplorerItemDeleteRequested;
        panel.ItemMoveRequested         += OnSolutionExplorerItemMoveRequested;
        panel.DefaultTblChangeRequested += OnDefaultTblChangeRequested;
        panel.AddNewItemRequested              += OnSEAddNewItem;
        panel.AddExistingItemRequested         += OnSEAddExistingItem;
        panel.ImportFormatDefinitionRequested  += OnSEImportFormatDefinition;
        panel.ConvertTblRequested              += OnSEConvertTbl;
        panel.FolderCreateRequested            += OnSEFolderCreateRequested;
        panel.FolderRenameRequested            += OnSEFolderRenameRequested;
        panel.FolderDeleteRequested            += OnSEFolderDeleteRequested;
        panel.FolderFromDiskRequested          += OnSEFolderFromDiskRequested;
        panel.ProjectRenameRequested           += OnSEProjectRenameRequested;
        panel.CloseSolutionRequested           += OnSECloseSolutionRequested;
        _solutionManager.ItemRenamed           += OnProjectItemRenamed;
        // Sync current solution if already loaded
        panel.SetSolution(_solutionManager.CurrentSolution);
        return panel;
    }

    private UIElement CreatePropertiesContent()
    {
        _propertiesPanel ??= new PropertiesPanel();
        return _propertiesPanel;
    }

    private UIElement CreateCustomParserContent()
    {
        if (_customParserPanel is null)
        {
            _customParserPanel = new WpfHexEditor.Panels.BinaryAnalysis.CustomParserTemplatePanel();
            _customParserPanel.TemplateApplyRequested += OnTemplateApplyRequested;
        }
        return _customParserPanel;
    }

    private UIElement CreateOutputContent()
    {
        _outputPanel ??= new OutputPanel();
        return _outputPanel;
    }

    private UIElement CreateParsedFieldsContent()
    {
        _parsedFieldsPanel ??= new ParsedFieldsPanel();
        return _parsedFieldsPanel;
    }

    private UIElement CreateErrorPanelContent()
    {
        if (_errorPanel is null)
        {
            _errorPanel = new ErrorPanel();
            _errorPanel.EntryNavigationRequested  += OnErrorEntryNavigation;
            _errorPanel.OpenInTextEditorRequested += OnOpenInTextEditorRequested;

            // Register solution manager as a permanent diagnostic source if it implements IDiagnosticSource
            if (_solutionManager is IDiagnosticSource sm)
                _errorPanel.AddSource(sm);

            // Background file monitor source (always registered — stays empty when no solution)
            _errorPanel.AddSource(_fileMonitorSource);
        }
        return _errorPanel;
    }

    private void OnErrorEntryNavigation(object? sender, DiagnosticEntry e)
    {
        if (e.FilePath is null) return;

        // Find an already-open tab for this file
        var (contentId, content) = _contentCache.FirstOrDefault(kv =>
        {
            if (kv.Value is HexEditorControl hex)
                return string.Equals(hex.FileName, e.FilePath, System.StringComparison.OrdinalIgnoreCase);
            if (kv.Value is WpfHexEditor.Editor.TblEditor.Controls.TblEditor tbl)
                return string.Equals(tbl.Title.TrimEnd('*', ' '),
                    Path.GetFileName(e.FilePath), System.StringComparison.OrdinalIgnoreCase);
            return false;
        });

        // Activate existing tab or open the file
        if (contentId != null)
        {
            var dockItem = _layout.FindItemByContentId(contentId);
            if (dockItem?.Owner != null) dockItem.Owner.ActiveItem = dockItem;
        }
        else
        {
            OpenFileDirectly(e.FilePath);
        }

        // Navigate after layout completes
        Dispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.Loaded, () =>
        {
            // HexEditor — byte offset
            if (e.Offset.HasValue && _connectedHexEditor != null)
                _connectedHexEditor.SetPosition(e.Offset.Value);

            // TblEditor — jump to hex entry (key stored in Tag by repair service)
            if (e.Tag is string hexKey && content is WpfHexEditor.Editor.TblEditor.Controls.TblEditor tblEd)
                tblEd.GoToEntry(hexKey);
        });
    }

    private void OnOpenInTextEditorRequested(object? sender, DiagnosticEntry e)
    {
        if (e.FilePath is null || !File.Exists(e.FilePath)) return;

        // Look for an already-open TextEditor tab for this file
        var existing = _contentCache.FirstOrDefault(kv =>
            kv.Value is WpfHexEditor.Editor.TextEditor.Controls.TextEditor tc &&
            string.Equals(tc.Title.TrimEnd('*', ' '),
                Path.GetFileName(e.FilePath), System.StringComparison.OrdinalIgnoreCase));

        if (existing.Key != null)
        {
            var existingDockItem = _layout.FindItemByContentId(existing.Key);
            if (existingDockItem?.Owner != null) existingDockItem.Owner.ActiveItem = existingDockItem;
        }
        else
        {
            // Create a new TextEditor tab, bypassing the registry (force TextEditorFactory)
            _documentCounter++;
            var newContentId = $"doc-text-err-{_documentCounter}";
            var textFactory  = new WpfHexEditor.Editor.TextEditor.TextEditorFactory();
            var editor       = textFactory.Create() as WpfHexEditor.Editor.TextEditor.Controls.TextEditor;
            if (editor == null) return;

            editor.OutputMessage += OnEditorOutputMessage;
            _ = editor.OpenAsync(e.FilePath);

            _contentCache[newContentId] = editor;
            var newDockItem = new DockItem
            {
                ContentId = newContentId,
                Title     = Path.GetFileName(e.FilePath)
            };
            _engine.Dock(newDockItem, _layout.MainDocumentHost, DockDirection.Center);
            ActiveDocumentEditor       = editor;
            ActiveStatusBarContributor = null;
        }

        // Navigate to the target line after the file is loaded
        if (e.Line.HasValue)
        {
            var targetLine = e.Line.Value;
            var targetCol  = e.Column ?? 1;
            Dispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.Loaded, () =>
            {
                if (_contentCache.Values
                    .OfType<WpfHexEditor.Editor.TextEditor.Controls.TextEditor>()
                    .FirstOrDefault(t => string.Equals(
                        t.Title.TrimEnd('*', ' '), Path.GetFileName(e.FilePath),
                        System.StringComparison.OrdinalIgnoreCase)) is { } tc)
                {
                    tc.GoToLine(targetLine, targetCol);
                }
            });
        }
    }

    private UIElement CreateHexEditorContent(
        string?   filePath,
        string?   displayName = null,
        bool      isNewFile   = false,
        IProject? project     = null)
    {
        var hexEditor = new HexEditorControl();
        hexEditor.ShowStatusBar       = false;
        hexEditor.ShowProgressOverlay = false;  // App handles progress in its own status bar

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
            // Inject project-local FormatDefinitions BEFORE opening the file so that
            // auto-detection can use project overrides instead of embedded ones.
            if (project is not null)
            {
                foreach (var fmtItem in project.Items
                    .Where(i => i.ItemType == ProjectItemType.FormatDefinition
                             && File.Exists(i.AbsolutePath)))
                    hexEditor.LoadFormatDefinition(fmtItem.AbsolutePath);
            }

            try
            {
                hexEditor.OpenFile(filePath);
                OutputLogger.Info($"Opened: {filePath}");
                return hexEditor;
            }
            catch (IOException ex)
            {
                var msg = $"Cannot open '{Path.GetFileName(filePath)}': {ex.Message}";
                OutputLogger.Error(msg);
                return MakeErrorBlock(msg);
            }
            catch (UnauthorizedAccessException ex)
            {
                var msg = $"Access denied '{Path.GetFileName(filePath)}': {ex.Message}";
                OutputLogger.Error(msg);
                return MakeErrorBlock(msg);
            }
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

    private static TextBlock MakeErrorBlock(string message) => new()
    {
        Text                = message,
        Foreground          = System.Windows.Media.Brushes.OrangeRed,
        Margin              = new Thickness(8),
        TextWrapping        = TextWrapping.Wrap,
        VerticalAlignment   = VerticalAlignment.Top,
        FontSize            = 13
    };

    private UIElement CreateProjectItemContent(DockItem item)
    {
        item.Metadata.TryGetValue("FilePath",   out var filePath);
        item.Metadata.TryGetValue("ItemId",     out var itemId);
        item.Metadata.TryGetValue("ProjectId",  out var projectId);
        item.Metadata.TryGetValue("EditorConfigJson", out var configJson);

        if (filePath is null) return CreateDocumentContent(item);

        // Try registry-based editor (TBL, JSON, …)
        var factory = _editorRegistry.FindFactory(filePath);
        if (factory != null)
        {
            var editor = factory.Create();
            if (editor is System.Windows.FrameworkElement fe)
            {
                if (editor is IOpenableDocument openable)
                    _ = openable.OpenAsync(filePath);

                // Restore EditorConfig + bookmarks if available
                if (editor is IEditorPersistable p)
                {
                    if (configJson != null)
                    {
                        try
                        {
                            var cfg = System.Text.Json.JsonSerializer.Deserialize<EditorConfigDto>(configJson);
                            if (cfg != null) p.ApplyEditorConfig(cfg);
                        }
                        catch { /* ignore malformed config */ }
                    }

                    if (itemId is not null && projectId is not null)
                    {
                        var owningProj = _solutionManager.CurrentSolution?.Projects.FirstOrDefault(pr => pr.Id == projectId);
                        var bkms       = owningProj?.FindItem(itemId)?.Bookmarks;
                        if (bkms is { Count: > 0 }) p.ApplyBookmarks(bkms);
                    }
                }

                // Wire OutputMessage → Output panel
                editor.OutputMessage += OnEditorOutputMessage;

                // Register as diagnostic source (e.g. TblEditor with IDiagnosticSource)
                if (editor is IDiagnosticSource diagSrc && _errorPanel is not null)
                    _errorPanel.AddSource(diagSrc);

                ActiveDocumentEditor       = editor;
                ActiveStatusBarContributor = editor as IStatusBarContributor;
                return fe;
            }
        }

        // Fallback: HexEditor for any file.
        // Resolve the owning project so we can inject project-local FormatDefinitions
        // BEFORE opening the file (format auto-detection reads them at open time).
        var project = projectId is not null
            ? _solutionManager.CurrentSolution?.Projects.FirstOrDefault(p => p.Id == projectId)
            : null;

        var hexContent = CreateHexEditorContent(filePath, project: project);

        if (hexContent is HexEditorControl hexEditorCtrl && hexEditorCtrl is IEditorPersistable hexPers)
        {
            // Restore editor state (cursor, bytes/line, encoding …)
            if (configJson != null)
            {
                try
                {
                    var cfg = System.Text.Json.JsonSerializer.Deserialize<EditorConfigDto>(configJson);
                    if (cfg != null) hexPers.ApplyEditorConfig(cfg);
                }
                catch { /* ignore malformed config */ }
            }

            // Restore unsaved in-memory modifications (IPS patch stored in .whproj) + bookmarks
            if (itemId is not null && project is not null)
            {
                var projItem = project.FindItem(itemId);
                if (projItem is not null)
                {
                    var mods = _solutionManager.GetItemModifications(project, projItem);
                    if (mods is { Length: > 0 })
                        hexPers.ApplyUnsavedModifications(mods);

                    var bkms = projItem.Bookmarks;
                    if (bkms is { Count: > 0 }) hexPers.ApplyBookmarks(bkms);
                }
            }
        }

        return hexContent;
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
            if (existing.Owner is { } owner)
                owner.ActiveItem = existing;
            DockHost.RebuildVisualTree();
            return;
        }

        // Create a new editor tab via registry (TBL, JSON) or HexEditor fallback
        _documentCounter++;

        // Embed the EditorConfig in metadata so CreateProjectItemContent can restore it
        var editorConfigJson = item.EditorConfig is not null
            ? System.Text.Json.JsonSerializer.Serialize(item.EditorConfig)
            : null;

        var dockItem = new DockItem
        {
            Title     = item.Name,
            ContentId = existingContentId,
            Metadata  =
            {
                ["FilePath"]  = item.AbsolutePath,
                ["ProjectId"] = project.Id,
                ["ItemId"]    = item.Id,
                ["ItemType"]  = item.ItemType.ToString()
            }
        };
        if (editorConfigJson != null)
            dockItem.Metadata["EditorConfigJson"] = editorConfigJson;

        _engine.Dock(dockItem, _layout.MainDocumentHost, DockDirection.Center);
        DockHost.RebuildVisualTree();
        UpdateStatusBar();
        _solutionManager.PushRecentFile(item.AbsolutePath);
        OutputLogger.Debug($"Opened project item: {item.Name}");
    }

    private void OnDefaultTblChangeRequested(object? sender, DefaultTblChangeEventArgs e)
    {
        _solutionManager.SetDefaultTbl(e.Project, e.TblItem);
        OutputLogger.Info(e.TblItem is not null
            ? $"Default TBL set to '{e.TblItem.Name}' in project '{e.Project.Name}'"
            : $"Default TBL cleared in project '{e.Project.Name}'");
    }

    private void OnSEAddNewItem(object? sender, AddItemRequestedEventArgs e)
    {
        var dlg = new AddNewItemDialog(e.Project) { Owner = this };
        if (dlg.ShowDialog() != true || dlg.SelectedTemplate is null) return;

        _ = _solutionManager.CreateItemAsync(
            e.Project,
            dlg.FileName,
            ProjectItemTypeHelper.FromExtension(Path.GetExtension(dlg.FileName)),
            virtualFolderId: dlg.TargetFolderId ?? e.TargetFolderId,
            initialContent:  dlg.SelectedTemplate.CreateContent());
    }

    private async void OnSEAddExistingItem(object? sender, AddItemRequestedEventArgs e)
    {
        var dlg = new AddExistingItemDialog(e.Project) { Owner = this };
        if (dlg.ShowDialog() != true) return;

        var projDir = Path.GetDirectoryName(e.Project.ProjectFilePath) ?? "";

        foreach (var srcPath in dlg.SelectedFilePaths)
        {
            var itemType  = ProjectItemTypeHelper.FromExtension(Path.GetExtension(srcPath));
            var finalPath = srcPath;
            var folderId  = dlg.SelectedVirtualFolderId ?? e.TargetFolderId;

            // ── 1. Physical copy ──────────────────────────────────────────────
            if (dlg.CopyToProject)
            {
                var destDir = projDir;
                if (dlg.UseTypeSubfolder)
                    destDir = Path.Combine(destDir, AddExistingItemDialog.TypeSubfolderName(itemType));
                Directory.CreateDirectory(destDir);
                finalPath = Path.Combine(destDir, Path.GetFileName(srcPath));
                if (!string.Equals(finalPath, srcPath, StringComparison.OrdinalIgnoreCase))
                    File.Copy(srcPath, finalPath, overwrite: false);
            }

            // ── 2. Virtual folder ─────────────────────────────────────────────
            if (dlg.CreateVirtualFolder && dlg.UseTypeSubfolder && folderId is null)
            {
                var folderName = AddExistingItemDialog.TypeSubfolderName(itemType);
                var existing   = e.Project.RootFolders
                    .FirstOrDefault(f => string.Equals(f.Name, folderName,
                                        StringComparison.OrdinalIgnoreCase));
                folderId = existing?.Id
                    ?? (await _solutionManager.CreateFolderAsync(e.Project, folderName)).Id;
            }

            // ── 3. Register in project ────────────────────────────────────────
            await _solutionManager.AddItemAsync(e.Project, finalPath, folderId);
        }
    }

    private void OnSEImportFormatDefinition(object? sender, AddItemRequestedEventArgs e)
    {
        var catalog = EmbeddedFormatCatalog.Instance;
        var dlg     = new ImportEmbeddedFormatDialog(catalog, e.Project) { Owner = this };
        if (dlg.ShowDialog() != true || dlg.SelectedEntries.Count == 0) return;

        var projDir = Path.GetDirectoryName(e.Project.ProjectFilePath)!;
        foreach (var entry in dlg.SelectedEntries)
        {
            var destDir  = Path.Combine(projDir, entry.Category);
            Directory.CreateDirectory(destDir);
            var safeName = string.Concat(entry.Name.Split(Path.GetInvalidFileNameChars()))
                                 .Replace(' ', '_');
            var destPath = Path.Combine(destDir, safeName + ".whfmt");
            var json     = catalog.GetJson(entry.ResourceKey);
            File.WriteAllText(destPath, json, System.Text.Encoding.UTF8);

            _ = _solutionManager.AddItemAsync(
                e.Project, destPath,
                virtualFolderId: dlg.TargetFolderId ?? e.TargetFolderId);
        }

        OutputLogger.Info(
            $"Imported {dlg.SelectedEntries.Count} format definition(s) into project '{e.Project.Name}'.");
    }

    private async void OnSEConvertTbl(object? sender, ProjectItemEventArgs e)
    {
        var dlg = new ConvertTblDialog(e.Item.AbsolutePath) { Owner = this };
        if (dlg.ShowDialog() != true) return;

        // 1. Load source .tbl (supports Thingy and Atlas formats automatically)
        var importSvc = new TblImportService();
        var importResult = importSvc.ImportFromFile(dlg.SourcePath);
        if (!importResult.Success)
        {
            MessageBox.Show(
                $"Failed to load TBL file:\n{string.Join('\n', importResult.Errors)}",
                "Convert TBL", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        // 2. Build TblxMetadata from dialog properties
        var metadata = new TblxMetadata
        {
            Name         = Path.GetFileNameWithoutExtension(dlg.SourcePath),
            CreatedDate  = DateTime.UtcNow,
            ModifiedDate = DateTime.UtcNow,
        };
        if (!string.IsNullOrWhiteSpace(dlg.GameTitle) ||
            !string.IsNullOrWhiteSpace(dlg.Platform)  ||
            !string.IsNullOrWhiteSpace(dlg.Region)    ||
            dlg.ReleaseYear.HasValue)
        {
            metadata.Game = new GameInfo
            {
                Title       = dlg.GameTitle,
                Platform    = dlg.Platform,
                Region      = dlg.Region,
                ReleaseYear = dlg.ReleaseYear,
            };
        }
        if (!string.IsNullOrWhiteSpace(dlg.Author))
            metadata.Author = dlg.Author;

        // 3. Export to .tblx
        try
        {
            var exportSvc = new TblExportService();
            exportSvc.ExportToFile(importResult.Entries, dlg.TargetPath, tblxMetadata: metadata);
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Failed to write TBLX file:\n{ex.Message}",
                "Convert TBL", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        // 4. Optionally add to project
        IProjectItem? newItem = null;
        if (dlg.AddToProject && e.Project is not null)
            newItem = await _solutionManager.AddItemAsync(e.Project, dlg.TargetPath);

        OutputLogger.Info($"Converted '{Path.GetFileName(dlg.SourcePath)}' → '{Path.GetFileName(dlg.TargetPath)}'" +
                          $" ({importResult.ImportedCount} entries, format: {importResult.DetectedFormat})");

        // 5. Optionally open the converted file
        if (dlg.OpenAfterConversion)
        {
            if (newItem is not null && e.Project is not null)
                OpenProjectItem(newItem, e.Project);
            else
                OpenFileDirectly(dlg.TargetPath);
        }
    }

    /// <summary>
    /// Opens a file in the appropriate editor without associating it with a project item.
    /// </summary>
    private void OpenFileDirectly(string filePath)
    {
        _documentCounter++;
        var item = new DockItem
        {
            Title     = Path.GetFileName(filePath),
            ContentId = $"doc-file-{_documentCounter}",
            Metadata  = { ["FilePath"] = filePath }
        };
        _engine.Dock(item, _layout.MainDocumentHost, DockDirection.Center);
        DockHost.RebuildVisualTree();
        UpdateStatusBar();
        _solutionManager.PushRecentFile(filePath);
    }

    private void OnSolutionExplorerItemSelected(object? sender, ProjectItemEventArgs e)
    {
        _propertiesPanel?.SetProvider(new ProjectItemPropertyProvider(e.Item));
    }

    private void OnSolutionExplorerItemRenameRequested(object? sender, ProjectItemEventArgs e)
    {
        if (e.Project is null) return;

        string newName;

        if (e.NewName is not null)
        {
            // Inline rename path — name was already validated by the panel
            newName = e.NewName;
        }
        else
        {
            // Fallback: programmatic dialog (context menu without inline editing)
            var win = new Window
            {
                Title                 = "Rename",
                Owner                 = this,
                Width                 = 380,
                Height                = 120,
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                ResizeMode            = ResizeMode.NoResize,
                ShowInTaskbar         = false,
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
                Content             = "OK",
                IsDefault           = true,
                Width               = 75,
                Margin              = new Thickness(0, 0, 10, 10),
                HorizontalAlignment = HorizontalAlignment.Right,
                VerticalAlignment   = VerticalAlignment.Bottom,
            };
            okBtn.Click += (_, _) => win.DialogResult = true;

            var grid = new System.Windows.Controls.Grid();
            grid.Children.Add(tb);
            grid.Children.Add(okBtn);
            win.Content = grid;

            if (win.ShowDialog() != true) return;
            newName = tb.Text.Trim();
        }

        if (newName.Length == 0 || newName == e.Item.Name) return;

        _ = _solutionManager.RenameItemAsync(e.Project, e.Item, newName);
        // Log is emitted by OnProjectItemRenamed via the ItemRenamed event
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

    private void OnSolutionExplorerItemMoveRequested(object? sender, ItemMoveRequestedEventArgs e)
    {
        _ = _solutionManager.MoveItemToFolderAsync(e.Project, e.Item, e.TargetFolderId);

        var destination = e.TargetFolderId is null
            ? $"project root of '{e.Project.Name}'"
            : $"folder '{e.TargetFolderId}' in '{e.Project.Name}'";
        OutputLogger.Info($"Moved '{e.Item.Name}' → {destination}");
    }

    // ─── Folder management ──────────────────────────────────────────────

    private void OnSEFolderCreateRequested(object? sender, FolderCreateRequestedEventArgs e)
        => _ = CreateFolderAndBeginRenameAsync(e.Project, e.ParentFolderId, e.CreatePhysical);

    private async Task CreateFolderAndBeginRenameAsync(IProject project, string? parentId, bool physical)
    {
        var baseName = physical ? "New Physical Folder" : "New Folder";
        var folder   = await _solutionManager.CreateFolderAsync(project, baseName, parentId, physical);
        _solutionExplorerPanel?.SetSolution(_solutionManager.CurrentSolution);
        _solutionExplorerPanel?.BeginFolderRename(folder);
        OutputLogger.Info($"Folder created in '{project.Name}'{(physical ? " (physical)" : "")}");
    }

    private void OnSEFolderRenameRequested(object? sender, FolderRenameEventArgs e)
    {
        _ = _solutionManager.RenameFolderAsync(e.Project, e.Folder, e.NewName);
        OutputLogger.Info($"Folder renamed to '{e.NewName}'");
    }

    private void OnProjectItemRenamed(object? sender, ProjectItemRenamedEventArgs e)
    {
        var contentId = $"doc-proj-{e.Item.Id}";
        var dockItem  = _layout.FindItemByContentId(contentId);
        if (dockItem is null) return;

        dockItem.Title = e.Item.Name;
        dockItem.Metadata["FilePath"] = e.Item.AbsolutePath;
        OutputLogger.Info($"Renamed '{e.OldName}' → '{e.Item.Name}'");
    }

    private void OnSEFolderDeleteRequested(object? sender, FolderDeleteEventArgs e)
    {
        var result = MessageBox.Show(
            $"Remove folder '{e.Folder.Name}' from the project?\n" +
            "Items inside will be kept at the project root. The physical directory will not be deleted.",
            "Remove folder",
            MessageBoxButton.YesNo,
            MessageBoxImage.Question);

        if (result != MessageBoxResult.Yes) return;

        _ = _solutionManager.DeleteFolderAsync(e.Project, e.Folder);
        _solutionExplorerPanel?.SetSolution(_solutionManager.CurrentSolution);
        OutputLogger.Info($"Folder '{e.Folder.Name}' removed from '{e.Project.Name}'");
    }

    private void OnSEFolderFromDiskRequested(object? sender, FolderFromDiskRequestedEventArgs e)
    {
        var dlg = new Microsoft.Win32.OpenFolderDialog
        {
            Title = "Select the folder to import",
        };
        if (dlg.ShowDialog() != true) return;
        _ = ImportFolderFromDiskAsync(e.Project, dlg.FolderName, e.ParentFolderId);
    }

    private async Task ImportFolderFromDiskAsync(IProject project, string physicalPath, string? parentId)
    {
        await _solutionManager.AddFolderFromDiskAsync(project, physicalPath, parentId);
        _solutionExplorerPanel?.SetSolution(_solutionManager.CurrentSolution);
        OutputLogger.Info($"Imported folder '{Path.GetFileName(physicalPath)}' into '{project.Name}'");
    }

    private void OnSEProjectRenameRequested(object? sender, ProjectRenameRequestedEventArgs e)
        => _ = _solutionManager.RenameProjectAsync(e.Project, e.NewName);

    private void OnSECloseSolutionRequested(object? sender, EventArgs e)
        => _ = CloseSolutionAsync();

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
            // Disconnect previous hex editor from panels
            if (_connectedHexEditor is IDiagnosticSource prevDiag)
                _errorPanel?.RemoveSource(prevDiag);
            _connectedHexEditor?.DisconnectParsedFieldsPanel();

            _connectedHexEditor = hex;

            // Connect new hex editor to panels
            if (_connectedHexEditor != null)
            {
                _connectedHexEditor.ConnectParsedFieldsPanel(_parsedFieldsPanel);
                if (_connectedHexEditor is IDiagnosticSource newDiag)
                    _errorPanel?.AddSource(newDiag);
            }
            else
            {
                _parsedFieldsPanel?.Clear();
            }
        }

        ActiveDocumentEditor       = content as IDocumentEditor;
        ActiveHexEditor            = hex;
        ActiveStatusBarContributor = content as IStatusBarContributor;

        if (ActiveDocumentEditor == null)
            RefreshText.Text = "";
        else
            hex?.RefreshDocumentStatus();

        // Sync Solution Explorer highlight
        if (hex?.FileName is { Length: > 0 } fn)
            _solutionExplorerPanel?.SyncWithFile(fn);

        // Sync Properties panel provider (M5: cached per editor instance)
        if (content is IPropertyProviderSource providerSource)
        {
            if (!_propertyProviderCache.TryGetValue(content, out var cachedProvider))
            {
                cachedProvider = providerSource.GetPropertyProvider();
                _propertyProviderCache[content] = cachedProvider;
            }
            _propertiesPanel?.SetProvider(cachedProvider);
        }
        else
        {
            _propertiesPanel?.SetProvider(null);
        }
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

    private void OnEditorModifiedChanged(object? sender, EventArgs e)
        => CommandManager.InvalidateRequerySuggested();

    // ─── Find / Replace handlers ────────────────────────────────────────

    private void OnShowAdvancedSearch(object sender, RoutedEventArgs e)
    {
        if (ActiveHexEditor is { } hex)
            hex.ShowAdvancedSearchDialog(this);
        else if (ActiveDocumentEditor is WpfHexEditor.Editor.JsonEditor.Controls.JsonEditor jsonEd)
            jsonEd.ShowFindBar();
    }

    private void OnFindNext(object sender, RoutedEventArgs e)
    {
        if (ActiveHexEditor is { } hex)
            hex.ShowQuickSearchBar();   // F3: show/focus inline bar
        else if (ActiveDocumentEditor is WpfHexEditor.Editor.JsonEditor.Controls.JsonEditor jsonEd)
            jsonEd.FindNext();
    }

    private void OnFindPrevious(object sender, RoutedEventArgs e)
    {
        if (ActiveHexEditor is { } hex)
            hex.ShowQuickSearchBar();   // Shift+F3: show/focus inline bar
        else if (ActiveDocumentEditor is WpfHexEditor.Editor.JsonEditor.Controls.JsonEditor jsonEd)
            jsonEd.FindPrevious();
    }

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

        OutputLogger.Debug($"File: {filename}");
        OutputLogger.Debug(formatPart);
    }

    private void OnEditorOutputMessage(object? sender, string message)
        => OutputLogger.Info(message);

    // ─── Long-running operation handlers (per active document) ──────────

    /// <summary>
    /// Syncs the progress bar zone to the given editor's busy state.
    /// Called on every active-document change so switching tabs feels instant.
    /// </summary>
    private void SyncProgressBarToActiveEditor(IDocumentEditor? editor)
    {
        if (editor?.IsBusy == true)
        {
            // New active doc is already mid-operation — show bar in indeterminate mode
            // until the next OperationProgress event provides exact values
            IsDocumentBusy = true;
            ProgressStatusItem.Visibility  = Visibility.Visible;
            AppProgressBar.IsIndeterminate = true;
            ProgressTitleText.Text         = "";
            ProgressMessageText.Text       = "";
            ProgressPercentText.Text       = "";
            ProgressCancelButton.Visibility = Visibility.Collapsed;
        }
        else
        {
            IsDocumentBusy = false;
            ProgressStatusItem.Visibility = Visibility.Collapsed;
        }
    }

    private void OnDocumentOperationStarted(object? sender, DocumentOperationEventArgs e)
    {
        Dispatcher.BeginInvoke(() =>
        {
            IsDocumentBusy = true;
            ProgressStatusItem.Visibility   = Visibility.Visible;
            ProgressTitleText.Text          = e.Title;
            AppProgressBar.IsIndeterminate  = e.IsIndeterminate;
            AppProgressBar.Value            = e.Percentage;
            ProgressPercentText.Text        = e.IsIndeterminate ? "" : $"{e.Percentage}%";
            ProgressMessageText.Text        = e.Message;
            ProgressCancelButton.Visibility = e.CanCancel ? Visibility.Visible : Visibility.Collapsed;
        });
    }

    private void OnDocumentOperationProgress(object? sender, DocumentOperationEventArgs e)
    {
        Dispatcher.BeginInvoke(() =>
        {
            AppProgressBar.IsIndeterminate = e.IsIndeterminate;
            AppProgressBar.Value           = e.Percentage;
            ProgressPercentText.Text       = e.IsIndeterminate ? "" : $"{e.Percentage}%";
            ProgressMessageText.Text       = e.Message;
        });
    }

    private void OnDocumentOperationCompleted(object? sender, DocumentOperationCompletedEventArgs e)
    {
        Dispatcher.BeginInvoke(() =>
        {
            IsDocumentBusy = false;
            ProgressStatusItem.Visibility = Visibility.Collapsed;

            // Brief status feedback in the existing RefreshText slot
            if (e.WasCancelled)
                RefreshText.Text = "Operation cancelled";
            else if (!e.Success && !string.IsNullOrEmpty(e.ErrorMessage))
                RefreshText.Text = $"Error: {e.ErrorMessage}";
        });
    }

    private void OnProgressCancel(object sender, RoutedEventArgs e)
        => ActiveDocumentEditor?.CancelOperation();

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

    private void OnTabCloseRequested(DockItem item) => CloseTab(item, promptIfDirty: true);

    /// <summary>Core close logic. Set <paramref name="promptIfDirty"/> to false when
    /// the dirty-check has already been handled by a batch dialog.</summary>
    private void CloseTab(DockItem item, bool promptIfDirty)
    {
        if (_isLocked) return;

        // Dirty-close prompt — only for document tabs backed by IDocumentEditor
        if (promptIfDirty &&
            _contentCache.TryGetValue(item.ContentId, out var dirtyCtrl) &&
            dirtyCtrl is IDocumentEditor dirtyEditor && dirtyEditor.IsDirty)
        {
            var cleanTitle = item.Title.TrimEnd('*', ' ');
            var dlg = new Dialogs.SaveChangesDialog
            {
                DirtyItems = [(item.ContentId, cleanTitle)],
                Owner      = this
            };
            if (dlg.ShowDialog() != true || dlg.Choice == Dialogs.SaveChangesChoice.Cancel) return;
            if (dlg.Choice == Dialogs.SaveChangesChoice.Save &&
                dirtyEditor.SaveCommand?.CanExecute(null) == true)
            {
                dirtyEditor.SaveCommand.Execute(null);
            }
        }

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
                hex.Close();   // Release the underlying FileStream immediately
            }

            // Save EditorConfig + unsaved modifications for project-item tabs
            if (item.ContentId.StartsWith("doc-proj-") &&
                item.Metadata.TryGetValue("ItemId",    out var itemId) &&
                item.Metadata.TryGetValue("ProjectId", out var projectId) &&
                ctrl is IEditorPersistable persistable)
            {
                SaveProjectItemState(itemId, projectId, persistable);
            }

            // Disconnect IDiagnosticSource for non-HexEditor registry-based editors (e.g. TblEditor)
            if (ctrl is not HexEditorControl && ctrl is IDiagnosticSource closingDiag)
                _errorPanel?.RemoveSource(closingDiag);

            _propertyProviderCache.Remove(ctrl);  // M5: evict cached provider
            _contentCache.Remove(item.ContentId);
        }

        _loggedFormats.Remove(item.ContentId);

        try
        {
            _engine.Close(item);
            DockHost.RebuildVisualTree();
            UpdateStatusBar();
            OutputLogger.Debug($"Closed tab: {item.Title} ({item.ContentId})");
        }
        catch (InvalidOperationException ex)
        {
            OutputLogger.Warn($"Cannot close '{item.Title}': {ex.Message}");
        }
    }

    private void SaveProjectItemState(string itemId, string projectId, IEditorPersistable persistable)
    {
        if (_solutionManager.CurrentSolution is null) return;
        var project = _solutionManager.CurrentSolution.Projects.FirstOrDefault(p => p.Id == projectId);
        var item    = project?.FindItem(itemId);
        if (item is null) return;

        // EditorConfig (cursor position, bytes/line, encoding …)
        item.EditorConfig = persistable.GetEditorConfig();

        // Bookmarks
        item.Bookmarks = persistable.GetBookmarks();

        // Unsaved in-memory modifications (IPS patch) — captured only when not saved to disk
        var mods = persistable.GetUnsavedModifications();
        _ = _solutionManager.PersistItemModificationsAsync(project!, item, mods);
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
            // VS-like behaviour: no solution open → show the project dialog and auto-create
            // a solution with the same name in the same directory.
            var dlg = new NewProjectDialog(null) { Owner = this };
            if (dlg.ShowDialog() != true) return;

            _ = CreateSolutionAndProjectAsync(dlg.ProjectDirectory, dlg.ProjectName, dlg.SelectedTemplate);
            return;
        }

        var suggestedDir = Path.GetDirectoryName(_solutionManager.CurrentSolution.FilePath) ?? "";
        var dlg2 = new NewProjectDialog(suggestedDir) { Owner = this };
        if (dlg2.ShowDialog() != true) return;

        _ = CreateProjectAsync(_solutionManager.CurrentSolution, dlg2.ProjectDirectory, dlg2.ProjectName, dlg2.SelectedTemplate);
    }

    private async Task CreateSolutionAndProjectAsync(string directory, string name, IProjectTemplate? template = null)
    {
        try
        {
            await _solutionManager.CreateSolutionAsync(directory, name);
            OutputLogger.Info($"Solution created: {name}");

            var project = await _solutionManager.CreateProjectAsync(_solutionManager.CurrentSolution!, directory, name);
            OutputLogger.Info($"Project created: {name}");

            await ApplyProjectTemplateAsync(project, template);
        }
        catch (Exception ex)
        {
            OutputLogger.Error($"Failed to create solution/project: {ex.Message}");
            MessageBox.Show($"Failed to create solution/project:\n{ex.Message}", "Error",
                MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private async Task CreateProjectAsync(ISolution solution, string directory, string name, IProjectTemplate? template = null)
    {
        try
        {
            var project = await _solutionManager.CreateProjectAsync(solution, directory, name);
            OutputLogger.Info($"Project created: {name}");

            await ApplyProjectTemplateAsync(project, template);
        }
        catch (Exception ex)
        {
            OutputLogger.Error($"Failed to create project: {ex.Message}");
            MessageBox.Show($"Failed to create project:\n{ex.Message}", "Error",
                MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private async Task ApplyProjectTemplateAsync(IProject project, IProjectTemplate? template)
    {
        if (template is null) return;

        try
        {
            var projDir  = Path.GetDirectoryName(project.ProjectFilePath) ?? "";
            var scaffold = await template.ScaffoldAsync(projDir, project.Name);

            // Apply project type
            if (scaffold.ProjectType is not null)
                project.ProjectType = scaffold.ProjectType;

            // Create virtual folders
            foreach (var folderName in scaffold.VirtualFolders)
                await _solutionManager.CreateFolderAsync(project, folderName, createPhysical: true);

            // Create and register files
            foreach (var file in scaffold.Files)
            {
                var absPath = Path.GetFullPath(
                    Path.Combine(projDir, file.RelativePath.Replace('/', Path.DirectorySeparatorChar)));
                Directory.CreateDirectory(Path.GetDirectoryName(absPath)!);
                await File.WriteAllBytesAsync(absPath, file.Content);
                await _solutionManager.AddItemAsync(project, absPath);

                if (file.OpenOnCreate)
                    OpenFileDirectly(absPath);
            }

            await _solutionManager.SaveProjectAsync(project);
            OutputLogger.Info($"Template '{template.DisplayName}' applied to project '{project.Name}'.");
        }
        catch (Exception ex)
        {
            OutputLogger.Info($"Template scaffolding failed: {ex.Message}");
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

        // Collect dirty documents and prompt once with VS-style grouped dialog
        var dirty = docs
            .Where(i => _contentCache.TryGetValue(i.ContentId, out var c)
                        && c is IDocumentEditor { IsDirty: true })
            .ToList();

        if (dirty.Count > 0)
        {
            var dlg = new Dialogs.SaveChangesDialog
            {
                DirtyItems = dirty.Select(i => (i.ContentId, i.Title.TrimEnd('*', ' '))).ToList(),
                Owner      = this
            };
            if (dlg.ShowDialog() != true || dlg.Choice == Dialogs.SaveChangesChoice.Cancel) return;

            if (dlg.Choice == Dialogs.SaveChangesChoice.Save)
            {
                // Save only the files the user kept checked
                var toSave = new HashSet<string>(dlg.SelectedContentIds);
                foreach (var d in dirty)
                {
                    if (toSave.Contains(d.ContentId) &&
                        _contentCache.TryGetValue(d.ContentId, out var c) &&
                        c is IDocumentEditor editor)
                        editor.SaveCommand?.Execute(null);
                }
            }
        }

        // Close all documents — dirty check already resolved above
        foreach (var doc in docs)
            CloseTab(doc, promptIfDirty: false);
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
        // Prompt to save modified solution/project files before replacing the current solution.
        if (_solutionManager.CurrentSolution != null)
        {
            var solDirty = CollectDirtySolutionItems();
            if (solDirty.Count > 0)
            {
                var dlg = new Dialogs.SaveChangesDialog
                {
                    DirtyItems = solDirty,
                    Owner      = this
                };
                if (dlg.ShowDialog() != true || dlg.Choice == Dialogs.SaveChangesChoice.Cancel)
                    return;

                if (dlg.Choice == Dialogs.SaveChangesChoice.Save)
                    await SaveSelectedSolutionItemsAsync(dlg.SelectedContentIds);
            }
        }

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

    // ─── Toolbar dropdown helper ───────────────────────────────────────
    // Generic Click handler for DockToolBarPodDropDownButtonStyle buttons.
    // Opens the ContextMenu attached to the button below its bottom edge.
    private void OnToolBarDropDownClick(object sender, RoutedEventArgs e)
    {
        if (sender is not Button btn || btn.ContextMenu is null) return;
        btn.ContextMenu.PlacementTarget = btn;
        btn.ContextMenu.Placement       = System.Windows.Controls.Primitives.PlacementMode.Bottom;
        btn.ContextMenu.IsOpen          = true;
    }

    // Alias: Open Project… → reuses the solution/project open dialog
    private void OnOpenProject(object sender, RoutedEventArgs e) =>
        OnOpenSolutionOrProject(sender, e);

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

    private async Task<bool> CloseSolutionAsync(bool promptUser = true)
    {
        if (promptUser)
        {
            var solDirty = CollectDirtySolutionItems();
            if (solDirty.Count > 0)
            {
                var dlg = new Dialogs.SaveChangesDialog
                {
                    DirtyItems = solDirty,
                    Owner      = this
                };
                if (dlg.ShowDialog() != true || dlg.Choice == Dialogs.SaveChangesChoice.Cancel)
                    return false;

                if (dlg.Choice == Dialogs.SaveChangesChoice.Save)
                    await SaveSelectedSolutionItemsAsync(dlg.SelectedContentIds);
            }
        }

        try
        {
            await _solutionManager.CloseSolutionAsync();
            OutputLogger.Info("Solution closed.");
        }
        catch (Exception ex)
        {
            OutputLogger.Error($"Failed to close solution: {ex.Message}");
        }
        return true;
    }

    // ─── Solution dirty-save helpers ───────────────────────────────────

    // Synthetic content IDs used in the SaveChangesDialog for solution/project files.
    private const string SolutionDirtyId = "__solution__";
    private static string ProjectDirtyId(string id) => $"__proj__{id}";

    /// <summary>
    /// Returns one entry per modified solution/project file for the SaveChangesDialog.
    /// </summary>
    private List<(string ContentId, string Title)> CollectDirtySolutionItems()
    {
        var result = new List<(string, string)>();
        var sol = _solutionManager.CurrentSolution;
        if (sol is null) return result;

        if (sol.IsModified)
            result.Add((SolutionDirtyId, $"{sol.Name}.whsln"));

        foreach (var proj in sol.Projects.Where(p => p.IsModified))
            result.Add((ProjectDirtyId(proj.Id), $"{proj.Name}.whproj"));

        return result;
    }

    /// <summary>
    /// Saves whichever solution/project items the user selected in the dialog.
    /// If the solution file is in the set, <see cref="ISolutionManager.SaveSolutionAsync"/>
    /// is used (which also writes all project files).  Otherwise only the selected
    /// projects are saved individually.
    /// </summary>
    private async Task SaveSelectedSolutionItemsAsync(IReadOnlyList<string> selectedIds)
    {
        var sol = _solutionManager.CurrentSolution;
        if (sol is null) return;

        if (selectedIds.Contains(SolutionDirtyId))
        {
            await _solutionManager.SaveSolutionAsync(sol);
            return;
        }

        foreach (var proj in sol.Projects)
        {
            if (selectedIds.Contains(ProjectDirtyId(proj.Id)))
                await _solutionManager.SaveProjectAsync(proj);
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

        RebuildJumpList();
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

    private void RebuildJumpList()
    {
        try
        {
            var appPath = Environment.ProcessPath ?? string.Empty;
            if (string.IsNullOrEmpty(appPath)) return;

            var jl = new JumpList();
            JumpList.SetJumpList(Application.Current, jl);

            foreach (var path in _solutionManager.RecentSolutions)
                jl.JumpItems.Add(new JumpTask
                {
                    Title           = Path.GetFileNameWithoutExtension(path),
                    Description     = path,
                    CustomCategory  = "Recent Projects",
                    ApplicationPath = appPath,
                    Arguments       = $"--open \"{path}\""
                });

            foreach (var path in _solutionManager.RecentFiles)
                jl.JumpItems.Add(new JumpTask
                {
                    Title           = Path.GetFileName(path),
                    Description     = path,
                    CustomCategory  = "Recent Files",
                    ApplicationPath = appPath,
                    Arguments       = $"--open \"{path}\""
                });

            jl.Apply();
        }
        catch (Exception ex)
        {
            OutputLogger.Error($"Failed to rebuild JumpList: {ex.Message}");
        }
    }

    // ─── View panel helpers ────────────────────────────────────────────

    private void OnShowProperties(object sender, RoutedEventArgs e)
        => ShowOrCreatePanel("Properties", "panel-properties", DockDirection.Right);

    private void OnShowCustomParserTemplate(object sender, RoutedEventArgs e)
        => ShowOrCreatePanel("Custom Parser Template", CustomParserPanelContentId, DockDirection.Right);

    private void OnCompareFiles(object sender, RoutedEventArgs e)
    {
        var dlgLeft = new OpenFileDialog { Title = "Select LEFT file for comparison", Multiselect = false };
        if (dlgLeft.ShowDialog() != true) return;

        var dlgRight = new OpenFileDialog { Title = "Select RIGHT file for comparison", Multiselect = false };
        if (dlgRight.ShowDialog() != true) return;

        _documentCounter++;
        var contentId = $"doc-diff-{_documentCounter}";
        var viewer    = new WpfHexEditor.Editor.DiffViewer.Controls.DiffViewer();
        var title     = $"{Path.GetFileName(dlgLeft.FileName)} ↔ {Path.GetFileName(dlgRight.FileName)}";

        _contentCache[contentId] = viewer;
        var item = new DockItem { Title = title, ContentId = contentId };
        _engine.Dock(item, _layout.MainDocumentHost, DockDirection.Center);
        DockHost.RebuildVisualTree();
        _ = viewer.CompareAsync(dlgLeft.FileName, dlgRight.FileName);
    }

    private void OnEntropyAnalysis(object sender, RoutedEventArgs e)
    {
        // Prefer the active hex editor's file; fall back to a file-open dialog
        var filePath = ActiveHexEditor?.FileName;
        if (string.IsNullOrEmpty(filePath))
        {
            var dlg = new OpenFileDialog { Title = "Select file for entropy analysis" };
            if (dlg.ShowDialog() != true) return;
            filePath = dlg.FileName;
        }

        _documentCounter++;
        var contentId = $"doc-entropy-{_documentCounter}";
        var viewer    = new WpfHexEditor.Editor.EntropyViewer.Controls.EntropyViewer();
        var title     = $"Entropy: {Path.GetFileName(filePath)}";

        _contentCache[contentId] = viewer;
        var item = new DockItem { Title = title, ContentId = contentId };
        _engine.Dock(item, _layout.MainDocumentHost, DockDirection.Center);
        DockHost.RebuildVisualTree();
        _ = viewer.OpenAsync(filePath);
    }

    private void OnTemplateApplyRequested(object? sender,
        WpfHexEditor.Panels.BinaryAnalysis.TemplateApplyEventArgs e)
    {
        if (ActiveHexEditor is not { IsFileOrStreamLoaded: true } hex)
        {
            System.Windows.MessageBox.Show(
                "Please open a file in the HexEditor before applying a template.",
                "Apply Template", System.Windows.MessageBoxButton.OK,
                System.Windows.MessageBoxImage.Warning);
            return;
        }

        var bytes    = hex.GetAllBytes();
        var template = e.Template;

        // Ensure ParsedFieldsPanel is visible and initialised
        ShowOrCreatePanel("Parsed Fields", ParsedFieldsPanelContentId, DockDirection.Right);
        if (_parsedFieldsPanel is null) return;

        _parsedFieldsPanel.Clear();
        _parsedFieldsPanel.TotalFileSize = bytes.LongLength;
        _parsedFieldsPanel.FormatInfo = new WpfHexEditor.Core.Interfaces.FormatInfo
        {
            IsDetected  = true,
            Name        = template.Name,
            Description = template.Description ?? string.Empty,
            Category    = "Custom Template",
        };

        foreach (var block in template.Blocks)
        {
            if (block.Offset < 0 || block.Offset + block.Length > bytes.Length)
                continue;

            var data = new byte[block.Length];
            Array.Copy(bytes, block.Offset, data, 0, block.Length);
            var (rawVal, fmtVal) = InterpretTemplateBytes(data, block.ValueType);

            _parsedFieldsPanel.ParsedFields.Add(
                new WpfHexEditor.Core.ViewModels.ParsedFieldViewModel
                {
                    Name           = block.Name ?? $"Field@0x{block.Offset:X}",
                    Offset         = block.Offset,
                    Length         = block.Length,
                    RawValue       = rawVal,
                    FormattedValue = fmtVal,
                    ValueType      = block.ValueType,
                    Description    = block.Description,
                    Color          = block.Color,
                    IsValid        = true,
                });
        }

        _parsedFieldsPanel.RefreshView();
        RefreshText.Text = $"Template '{template.Name}' applied — {template.Blocks.Count} fields.";
    }

    private static (object rawVal, string fmtVal) InterpretTemplateBytes(byte[] data, string? type)
    {
        if (data is not { Length: > 0 }) return (data!, "(empty)");
        try
        {
            return type?.ToLowerInvariant() switch
            {
                "uint8"   => ((object)(byte)data[0],                                    $"{data[0]}"),
                "int8"    => ((object)(sbyte)data[0],                                   $"{(sbyte)data[0]}"),
                "uint16"  when data.Length >= 2 => ((object)BitConverter.ToUInt16(data, 0), $"{BitConverter.ToUInt16(data, 0)}"),
                "int16"   when data.Length >= 2 => ((object)BitConverter.ToInt16 (data, 0), $"{BitConverter.ToInt16 (data, 0)}"),
                "uint32"  when data.Length >= 4 => ((object)BitConverter.ToUInt32(data, 0), $"{BitConverter.ToUInt32(data, 0)}"),
                "int32"   when data.Length >= 4 => ((object)BitConverter.ToInt32 (data, 0), $"{BitConverter.ToInt32 (data, 0)}"),
                "uint64"  when data.Length >= 8 => ((object)BitConverter.ToUInt64(data, 0), $"{BitConverter.ToUInt64(data, 0)}"),
                "int64"   when data.Length >= 8 => ((object)BitConverter.ToInt64 (data, 0), $"{BitConverter.ToInt64 (data, 0)}"),
                "float"   when data.Length >= 4 => ((object)BitConverter.ToSingle(data, 0), $"{BitConverter.ToSingle(data, 0):F4}"),
                "double"  when data.Length >= 8 => ((object)BitConverter.ToDouble(data, 0), $"{BitConverter.ToDouble(data, 0):F6}"),
                "string"  or "ascii"            => ((object)System.Text.Encoding.ASCII  .GetString(data).TrimEnd('\0'), System.Text.Encoding.ASCII  .GetString(data).TrimEnd('\0')),
                "utf8"                          => ((object)System.Text.Encoding.UTF8   .GetString(data).TrimEnd('\0'), System.Text.Encoding.UTF8   .GetString(data).TrimEnd('\0')),
                "utf16"                         => ((object)System.Text.Encoding.Unicode.GetString(data).TrimEnd('\0'), System.Text.Encoding.Unicode.GetString(data).TrimEnd('\0')),
                "boolean"                       => ((object)(data[0] != 0), data[0] != 0 ? "True" : "False"),
                "binary"                        => ((object)data, string.Join(" ", data.Select(b => Convert.ToString(b, 2).PadLeft(8, '0')))),
                _                               => ((object)data, BitConverter.ToString(data).Replace("-", " ")),
            };
        }
        catch { return (data, BitConverter.ToString(data).Replace("-", " ")); }
    }

    private void OnShowSolutionExplorer(object sender, RoutedEventArgs e)
    {
        ShowOrCreatePanel("Solution Explorer", SolutionExplorerContentId, DockDirection.Left);
        // If a singleton already exists but panel was closed, re-dock
        if (_solutionExplorerPanel != null)
            _solutionExplorerPanel.SetSolution(_solutionManager.CurrentSolution);
    }

    private void OnShowOutput(object sender, RoutedEventArgs e)
        => ShowOrCreatePanel("Output", "panel-output", DockDirection.Bottom);

    private void OnShowErrorPanel(object sender, RoutedEventArgs e)
        => ShowOrCreatePanel("Error List", ErrorPanelContentId, DockDirection.Bottom);

    private void OnShowParsedFields(object sender, RoutedEventArgs e)
        => ShowOrCreatePanel("Parsed Fields", ParsedFieldsPanelContentId, DockDirection.Right);

    private void ShowOrCreatePanel(string title, string contentId, DockDirection direction)
    {
        var existing = _layout.FindItemByContentId(contentId);
        if (existing is not null)
        {
            // M7: if the item is floating, activate its window and select the tab
            var floatingWindow = Application.Current.Windows
                .OfType<FloatingWindow>()
                .FirstOrDefault(w =>
                    w.Item?.ContentId == contentId ||
                    (w.Node?.Items.Any(i => i.ContentId == contentId) == true));

            if (floatingWindow is not null)
            {
                if (floatingWindow.Node is { } node)
                {
                    var tab = node.Items.FirstOrDefault(i => i.ContentId == contentId);
                    if (tab is not null) node.ActiveItem = tab;
                }
                floatingWindow.Activate();
                return;
            }

            // Docked: make active in its tab group
            if (existing.Owner is { } owner)
                owner.ActiveItem = existing;
            DockHost.RebuildVisualTree();
            return;
        }

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
            OutputLogger.Debug($"Layout loaded from: {dlg.FileName}");
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
        OutputLogger.Debug("Layout reset to default.");
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

    // ─── Logo click — native system menu ─────────────────────────────

    [DllImport("user32.dll")] private static extern IntPtr GetSystemMenu(IntPtr hWnd, bool bRevert);
    [DllImport("user32.dll")] private static extern int    TrackPopupMenuEx(IntPtr hmenu, uint fuFlags, int x, int y, IntPtr hwnd, IntPtr lptpm);
    [DllImport("user32.dll")] private static extern bool   PostMessage(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam);

    private void OnLogoMouseDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ChangedButton != MouseButton.Left) return;

        const uint TPM_RETURNCMD = 0x0100;
        const uint WM_SYSCOMMAND = 0x0112;

        var hwnd = new WindowInteropHelper(this).Handle;
        var menu = GetSystemMenu(hwnd, false);
        var img  = (UIElement)sender;
        var pt   = img.PointToScreen(new Point(0, img.RenderSize.Height));

        int cmd = TrackPopupMenuEx(menu, TPM_RETURNCMD, (int)pt.X, (int)pt.Y, hwnd, IntPtr.Zero);
        if (cmd != 0)
            PostMessage(hwnd, WM_SYSCOMMAND, new IntPtr(cmd), IntPtr.Zero);

        e.Handled = true;
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

    private void UpdateStatusBar() { }
}
