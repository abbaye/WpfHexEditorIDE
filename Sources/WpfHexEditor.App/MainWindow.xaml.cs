//////////////////////////////////////////////
// GNU Affero General Public License v3.0 - 2026
// Author : Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.5, Claude Sonnet 4.6, Claude Opus 4.6
//////////////////////////////////////////////

using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
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
using WpfHexEditor.App.Dialogs;
using WpfHexEditor.App.Models;
using WpfHexEditor.App.Services;
using WpfHexEditor.Editor.Core;
using WpfHexEditor.Core;
using WpfHexEditor.Core.CharacterTable;
using WpfHexEditor.Editor.TblEditor;
using WpfHexEditor.Editor.TblEditor.Models;
using WpfHexEditor.Editor.TblEditor.Services;
using WpfHexEditor.Editor.CodeEditor;
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
using WpfHexEditor.Editor.ChangesetEditor;
using WpfHexEditor.Editor.XamlDesigner;
using WpfHexEditor.Panels.IDE;
using WpfHexEditor.Panels.IDE.Panels;
using WpfHexEditor.Panels.IDE.Services;
using WpfHexEditor.Definitions;
using WpfHexEditor.ProjectSystem;
using WpfHexEditor.ProjectSystem.Dialogs;
using WpfHexEditor.ProjectSystem.Serialization;
using WpfHexEditor.ProjectSystem.Services;
using WpfHexEditor.ProjectSystem.Templates;
using WpfHexEditor.ProjectSystem.Languages;
using System.Windows.Shell;
using System.Windows.Threading;
using WpfHexEditor.Options;
using WpfHexEditor.Editor.Core.Views;
using CodeEditorControl = WpfHexEditor.Editor.CodeEditor.Controls.CodeEditor;
using TblEditorControl  = WpfHexEditor.Editor.TblEditor.Controls.TblEditor;

namespace WpfHexEditor.App;

public partial class MainWindow : Window, INotifyPropertyChanged
{
    // --- INotifyPropertyChanged ----------------------------------------
    public event PropertyChangedEventHandler? PropertyChanged;
    private void OnPropertyChanged([CallerMemberName] string? name = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));

    // --- Static RoutedCommands (Find / Find Next / Previous — F3/Shift+F3) ----
    public static readonly RoutedCommand FindNextCommand = new RoutedCommand(
        "FindNext", typeof(MainWindow),
        new InputGestureCollection { new KeyGesture(Key.F3) });

    public static readonly RoutedCommand WriteToDiskCommand = new RoutedCommand(
        "WriteToDisk", typeof(MainWindow),
        new InputGestureCollection { new KeyGesture(Key.W, ModifierKeys.Control | ModifierKeys.Shift) });

    public static readonly RoutedCommand FindPreviousCommand = new RoutedCommand(
        "FindPrevious", typeof(MainWindow),
        new InputGestureCollection { new KeyGesture(Key.F3, ModifierKeys.Shift) });

    /// <summary>Ctrl+Shift+F — opens the full 5-mode Advanced Search dialog (HexEditor only).</summary>
    public static readonly RoutedCommand AdvancedSearchCommand = new RoutedCommand(
        "AdvancedSearch", typeof(MainWindow),
        new InputGestureCollection { new KeyGesture(Key.F, ModifierKeys.Control | ModifierKeys.Shift) });

    /// <summary>Ctrl+G — opens the Go To Offset dialog.</summary>
    public static readonly RoutedCommand GoToOffsetCommand = new RoutedCommand(
        "GoToOffset", typeof(MainWindow),
        new InputGestureCollection { new KeyGesture(Key.G, ModifierKeys.Control) });

    /// <summary>Ctrl+Shift+P — opens the Plugin Manager panel.</summary>
    public static readonly RoutedCommand OpenPluginManagerCommand = new RoutedCommand(
        "OpenPluginManager", typeof(MainWindow),
        new InputGestureCollection { new KeyGesture(Key.P, ModifierKeys.Control | ModifierKeys.Shift) });

    // --- Constants -----------------------------------------------------
    private static readonly string LayoutFilePath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "WpfHexEditor", "Sample.Docking", "layout.json");

    private static readonly string SessionFilePath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "WpfHexEditor", "session.json");

    private const string SolutionExplorerContentId    = "panel-solution-explorer";

    // --- Fields --------------------------------------------------------
    private DockLayoutRoot _layout = null!;
    private DockEngine     _engine = null!;
    private int  _documentCounter;
    private bool _isLocked;

    // Content cache: ContentId → actual editor UIElement (unwrapped; used for editor logic)
    private readonly Dictionary<string, UIElement> _contentCache = new();

    // Project properties map: doc-projprops-{id} → IProject (for deferred content creation)
    private readonly Dictionary<string, IProject> _projectPropertiesMap = new();

    // NuGet manager map: doc-nuget-{name} → IProject (for deferred content creation)
    private readonly Dictionary<string, IProject> _nugetManagerMap = new();

    // ContentIds of "doc-projprops-*" tabs that received the placeholder at layout-restore time
    // because the solution was not yet loaded. Evicted and rebuilt in OnSolutionChanged().
    private readonly HashSet<string> _pendingProjectPropertiesContentIds = new();

    // Display cache: ContentId → visual UIElement shown in the docking layout
    // When an InfoBar is present this is a Grid wrapper; otherwise == _contentCache entry.
    private readonly Dictionary<string, UIElement> _displayContent = new();

    // Per-document format tracking: ContentId → last logged format name
    private readonly Dictionary<string, string> _loggedFormats = new();

    // QuickSearchBar instances for CodeEditor documents (CodeEditor has no embedded Canvas)
    private readonly Dictionary<CodeEditorControl, QuickSearchBar> _codeEditorBars = new();

    // M5: PropertyProvider cache — avoids recreating the provider on every tab switch
    private readonly Dictionary<UIElement, IPropertyProvider?> _propertyProviderCache = new();

    private HexEditorControl?  _connectedHexEditor;

    // SolutionExplorer (persistent singleton)
    private SolutionExplorerPanel? _solutionExplorerPanel;

    // Properties panel (persistent singleton)
    private PropertiesPanel? _propertiesPanel;

    // Error Panel (persistent singleton)
    private ErrorPanel? _errorPanel;
    private const string ErrorPanelContentId    = "panel-errors";
    private const string OptionsContentId       = "panel-options";
    private const string FileComparisonPanelContentId    = "panel-file-comparison";
    private const string ArchivePanelContentId           = "panel-archive";
    private string _lastAppliedTheme = string.Empty;

    // TBL dropdown — tracks which project TBL item is applied per hex editor
    private readonly Dictionary<HexEditorControl, IProjectItem?> _editorProjectTbl = new();

    // Background file monitor (watches project files for external changes)
    private readonly FileMonitorDiagnosticSource _fileMonitorSource = new();
    private FileMonitorService? _fileMonitorService;

    // Output Panel (persistent singleton — pre-created so OutputLogger works from startup)
    private OutputPanel? _outputPanel;

    // FileOps panels (persistent singletons)
    private WpfHexEditor.Panels.FileOps.ArchiveStructurePanel? _archivePanel;

    // SolutionManager
    private readonly ISolutionManager _solutionManager = SolutionManager.Instance;

    // Editor registry for doc-proj-* dispatcher
    private readonly IEditorRegistry _editorRegistry = new EditorRegistry();

    // Auto-serialize timer (Tracked mode)
    private DispatcherTimer? _autoSerializeTimer;

    // Default (empty) status bar contributor — used when no active editor provides one.
    // Ensures the status bar shows a clean state instead of stale items from a closed editor.
    private static readonly IStatusBarContributor _defaultStatusBarContributor =
        new EmptyStatusBarContributor();

    private sealed class EmptyStatusBarContributor : IStatusBarContributor
    {
        public ObservableCollection<StatusBarItem> StatusBarItems { get; } = [];
        public void RefreshStatusBarItems() { }
    }

    // --- Bindable properties --------------------------------------------

    private IDocumentEditor? _activeDocumentEditor;
    public IDocumentEditor? ActiveDocumentEditor
    {
        get => _activeDocumentEditor;
        private set
        {
            if (_activeDocumentEditor != null)
            {
                // TitleChanged and ModifiedChanged are now handled by DocumentManager
                // (wired via AttachEditor — covers all open editors, not just the active one).
                _activeDocumentEditor.StatusMessage      -= OnEditorStatusMessage;
                _activeDocumentEditor.OutputMessage      -= OnEditorOutputMessage;
                _activeDocumentEditor.OperationStarted   -= OnDocumentOperationStarted;
                _activeDocumentEditor.OperationProgress  -= OnDocumentOperationProgress;
                _activeDocumentEditor.OperationCompleted -= OnDocumentOperationCompleted;
            }
            _activeDocumentEditor = value;
            if (_activeDocumentEditor != null)
            {
                // TitleChanged and ModifiedChanged are handled by DocumentManager.
                _activeDocumentEditor.StatusMessage      += OnEditorStatusMessage;
                _activeDocumentEditor.OutputMessage      += OnEditorOutputMessage;
                _activeDocumentEditor.OperationStarted   += OnDocumentOperationStarted;
                _activeDocumentEditor.OperationProgress  += OnDocumentOperationProgress;
                _activeDocumentEditor.OperationCompleted += OnDocumentOperationCompleted;
            }
            // Notify plugin system only when a hex editor becomes active.
            // Non-hex editors (JSON, TBL…) leave panels connected to the last hex editor
            // (same VS-like behaviour enforced in OnActiveDocumentChanged).
            if (value is HexEditorControl hexEditor)
                _hexEditorService?.SetActiveEditor(hexEditor);

            // Sync progress bar immediately to reflect the new active document's state
            SyncProgressBarToActiveEditor(value);
            OnPropertyChanged();
            OnPropertyChanged(nameof(IsImageViewerActive));
        }
    }

    /// <summary>
    /// True when the active (or last active) document editor is an ImageViewer.
    /// Controls the visibility of the _Image_ menu in the menu bar.
    /// </summary>
    public bool IsImageViewerActive =>
        _activeDocumentEditor is WpfHexEditor.Editor.ImageViewer.Controls.ImageViewer;

    private HexEditorControl? _activeHexEditor;
    public HexEditorControl? ActiveHexEditor
    {
        get => _activeHexEditor;
        private set
        {
            // Unsubscribe from the previous editor's file-change event.
            if (_activeHexEditor != null)
                _activeHexEditor.FileExternallyChanged -= OnActiveFileExternallyChanged;

            _activeHexEditor = value;

            // Subscribe to the new editor and hide any stale file-change bar.
            if (_activeHexEditor != null)
                _activeHexEditor.FileExternallyChanged += OnActiveFileExternallyChanged;

            HideFileChangeInfoBar();
            OnPropertyChanged();
        }
    }

    // -- TBL toolbar dropdown ---------------------------------------------

    private readonly ObservableCollection<TblSelectionItem> _tblItems = new();
    public  ObservableCollection<TblSelectionItem> TblItems => _tblItems;

    private TblSelectionItem? _selectedTblItem;
    public TblSelectionItem? SelectedTblItem
    {
        get => _selectedTblItem;
        set
        {
            if (_selectedTblItem == value) return;
            _selectedTblItem = value;
            OnPropertyChanged();
            if (value?.IsSelectable == true)
                ApplyTblSelectionItem(value);
        }
    }

    private IStatusBarContributor? _activeStatusBarContributor;
    public IStatusBarContributor? ActiveStatusBarContributor
    {
        get => _activeStatusBarContributor;
        private set 
        { 
            // Unsubscribe from previous HexEditor refresh time updates
            if (_activeStatusBarContributor is HexEditor.HexEditor previousHex)
            {
                UnsubscribeFromRefreshTimeUpdates(previousHex);
            }

            _activeStatusBarContributor = value;

            // Subscribe to new HexEditor refresh time updates
            if (_activeStatusBarContributor is HexEditor.HexEditor currentHex)
            {
                SubscribeToRefreshTimeUpdates(currentHex);
            }
            else
            {
                // Hide refresh time for non-HexEditor documents
                if (RefreshTimeStatusItem != null)
                    RefreshTimeStatusItem.Visibility = Visibility.Collapsed;
            }

            OnPropertyChanged(); 
        }
    }

    private void SubscribeToRefreshTimeUpdates(HexEditor.HexEditor hexEditor)
    {
        if (RefreshTimeStatusItem == null || RefreshTimeText == null) return;

        // Access the RefreshTimeStatusBarItem to trigger lazy initialization
        var refreshItem = hexEditor.RefreshTimeStatusBarItem;
        if (refreshItem != null)
        {
            RefreshTimeStatusItem.Visibility = Visibility.Visible;
            // Update immediately
            UpdateRefreshTimeDisplay(refreshItem.Value);
            // Subscribe to property changes
            refreshItem.PropertyChanged += OnRefreshTimeItemPropertyChanged;
        }
    }

    private void UnsubscribeFromRefreshTimeUpdates(HexEditor.HexEditor hexEditor)
    {
        var refreshItem = hexEditor.RefreshTimeStatusBarItem;
        if (refreshItem != null)
        {
            refreshItem.PropertyChanged -= OnRefreshTimeItemPropertyChanged;
        }
    }

    private void OnRefreshTimeItemPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(Editor.Core.StatusBarItem.Value) && sender is Editor.Core.StatusBarItem item)
        {
            Dispatcher.InvokeAsync(() => UpdateRefreshTimeDisplay(item.Value));
        }
    }

    private void UpdateRefreshTimeDisplay(string? value)
    {
        if (RefreshTimeText != null)
        {
            RefreshTimeText.Text = string.IsNullOrEmpty(value) ? "" : $"Refresh: {value}";
        }
    }

    private IEditorToolbarContributor? _activeToolbarContributor;
    public IEditorToolbarContributor? ActiveToolbarContributor
    {
        get => _activeToolbarContributor;
        private set { _activeToolbarContributor = value; OnPropertyChanged(); }
    }

    private bool _hasSolution;
    public bool HasSolution
    {
        get => _hasSolution;
        private set { _hasSolution = value; OnPropertyChanged(); OnPropertyChanged(nameof(IsProjectMenuEnabled)); OnPropertyChanged(nameof(IsBuildMenuEnabled)); OnPropertyChanged(nameof(CanRunStartupProject)); }
    }

    // -- Long-running operation state (per active document) ---------------
    private bool _isDocumentBusy;
    private bool _isClosingForced;
    private bool _isShutdownComplete;
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

    // --- Constructor ---------------------------------------------------
    public MainWindow()
    {
        InitializeComponent();
        Closing      += OnWindowClosing;
        Loaded       += OnLoaded;
        StateChanged += OnStateChanged;
    }

    /// <summary>
    /// Registers all embedded .whlang syntax definitions into <see cref="LanguageRegistry"/>
    /// so that <see cref="CodeEditorFactory"/> can resolve the correct
    /// <see cref="ISyntaxHighlighter"/> for every file type it opens.
    /// Must be called before any editor factory is used.
    /// </summary>
    private static void LoadEmbeddedLanguageDefinitions()
    {
        var catalog  = EmbeddedSyntaxCatalog.Instance;
        var registry = LanguageRegistry.Instance;

        foreach (var entry in catalog.GetAll())
        {
            try
            {
                var json = catalog.GetContent(entry.ResourceKey);
                var def  = LanguageDefinitionSerializer.Parse(json);
                registry.RegisterBuiltin(def);
            }
            catch
            {
                // Skip malformed or incomplete .whlang entries to avoid blocking startup.
            }
        }
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        // Wire DocumentManager events (title changes, dirty state → all open editors)
        InitDocumentManager();

        // Wire SolutionManager events before restoring layout
        _solutionManager.SolutionChanged      += OnSolutionChanged;
        _solutionManager.ProjectChanged       += OnProjectChanged;
        _solutionManager.ItemAdded            += OnProjectItemAdded;
        _solutionManager.FormatUpgradeRequired += OnFormatUpgradeRequired;

        // Populate LanguageRegistry with all embedded .whlang syntax definitions.
        // This must run before any editor factory is used so that CodeEditorFactory
        // can resolve ExternalHighlighter via LanguageRegistry.GetLanguageForFile().
        LoadEmbeddedLanguageDefinitions();

        // Register editor factories (doc-proj-* dispatcher)
        _editorRegistry.Register(new TblEditorFactory());
        _editorRegistry.Register(new CodeEditorFactory());
        _editorRegistry.Register(new TextEditorFactory());
        _editorRegistry.Register(new ImageViewerFactory());
        _editorRegistry.Register(new EntropyViewerFactory());
        _editorRegistry.Register(new DiffViewerFactory());
        _editorRegistry.Register(new DisassemblyViewerFactory());
        _editorRegistry.Register(new StructureEditorFactory());
        _editorRegistry.Register(new TileEditorFactory());
        _editorRegistry.Register(new AudioViewerFactory());
        _editorRegistry.Register(new ScriptEditorFactory());
        _editorRegistry.Register(new ChangesetEditorFactory());
        _editorRegistry.Register(new XamlDesignerFactory());

        // Register VS-style project templates
        ProjectTemplateRegistry.RegisterDefaults();

        // Pre-create OutputPanel so OutputLogger.Register is called before any Info/Error calls
        _outputPanel = new OutputPanel();

        // Load user settings then apply persisted theme before layout loads
        AppSettingsService.Instance.Load();
        ApplyThemeFromSettings();
        InitAutoSerializeTimer();

        RebuildTblItemList();   // must be before LoadSavedLayoutOrDefault so SyncTblDropdown finds items
        LoadSavedLayoutOrDefault();
        PopulateRecentMenus();
        TryRestoreSession();
        HandleStartupFile();

        // Deferred theme sync: catches editors that the docking system creates lazily
        // (ContentFactory called on first render pass) or via async session restore.
        Dispatcher.InvokeAsync(SyncAllHexEditorThemes, System.Windows.Threading.DispatcherPriority.Background);

        // Plugin system — fire-and-forget after layout is ready
        _ = InitializePluginSystemAsync();
    }

    private void InitAutoSerializeTimer()
    {
        var s = AppSettingsService.Instance.Current;
        if (!s.AutoSerializeEnabled) return;

        _autoSerializeTimer = new DispatcherTimer(DispatcherPriority.Background)
        {
            Interval = TimeSpan.FromSeconds(Math.Max(5, s.AutoSerializeIntervalSeconds))
        };
        _autoSerializeTimer.Tick += OnAutoSerializeTick;
        _autoSerializeTimer.Start();
    }

    private void OnAutoSerializeTick(object? sender, EventArgs e)
    {
        var settings = AppSettingsService.Instance.Current;
        if (settings.DefaultFileSaveMode != FileSaveMode.Tracked) return;
        if (_solutionManager.CurrentSolution is null) return;

        // _dirtyTrackedContentIds is maintained by OnDocumentManagerDirtyChanged,
        // so this loop is O(dirty editors) instead of O(all solution items).
        foreach (var contentId in _dirtyTrackedContentIds)
        {
            if (!_contentCache.TryGetValue(contentId, out var ctrl)) continue;
            if (ctrl is not IEditorPersistable persistable) continue;
            if (!TryGetProjectItemFromContentId(contentId, out var item) || item is null) continue;

            var snapshot = persistable.GetChangesetSnapshot();
            if (!snapshot.HasEdits) continue;

            _ = ChangesetService.Instance.WriteChangesetAsync(item, snapshot);
        }
    }

    private void OnWindowClosing(object? sender, CancelEventArgs e)
    {
        // Shutdown completed — allow the final close through.
        if (_isShutdownComplete) return;

        if (_isClosingForced)
        {
            // Triggered by ConfirmAndCloseAsync: cancel close, run async shutdown, then re-close.
            e.Cancel = true;
            _fileMonitorService?.Dispose();
            AutoSaveLayout();
            _ = ShutdownThenCloseAsync();
            return;
        }

        // Collect all dirty items: solution/project files + open document editors
        var allDirty = CollectAllDirtyItems(out var dirtyDocs);

        if (allDirty.Count == 0)
        {
            // No unsaved work — cancel close, run async shutdown, then re-close.
            e.Cancel = true;
            _fileMonitorService?.Dispose();
            AutoSaveLayout();
            _ = ShutdownThenCloseAsync();
            return;
        }

        e.Cancel = true;
        _ = ConfirmAndCloseAsync(allDirty, dirtyDocs);
    }

    /// <summary>
    /// Awaits plugin system shutdown (including sandbox process termination),
    /// then re-triggers the window close on the UI thread.
    /// </summary>
    private async Task ShutdownThenCloseAsync()
    {
        await ShutdownPluginSystemAsync().ConfigureAwait(false);
        _isShutdownComplete = true;
        await Dispatcher.InvokeAsync(Close);
    }

    private async Task ConfirmAndCloseAsync(
        List<(string ContentId, string Title)> allDirty,
        List<DockItem> dirtyDocs)
    {
        if (!await PromptAndSaveDirtyAsync(allDirty, dirtyDocs)) return;

        _isClosingForced = true;
        AutoSaveLayout();
        Close();
    }

    // --- SolutionManager event handlers --------------------------------

    private void OnSolutionChanged(object? sender, SolutionChangedEventArgs e)
    {
        HasSolution = _solutionManager.CurrentSolution != null;

        // Refresh any "doc-projprops-*" tabs that showed "Projet introuvable." at layout-restore
        // time because the solution was not yet loaded when the content factory ran.
        if (_pendingProjectPropertiesContentIds.Count > 0 && _solutionManager.CurrentSolution != null)
        {
            foreach (var contentId in _pendingProjectPropertiesContentIds)
            {
                _displayContent.Remove(contentId);
                _contentCache.Remove(contentId);
            }
            _pendingProjectPropertiesContentIds.Clear();
            DockHost.RebuildVisualTree();
        }

        _solutionExplorerPanel?.SetSolution(_solutionManager.CurrentSolution);
        RefreshStartupProjectList();
        RebuildTblItemList();
        RefreshAllChangesetNodes();
        PopulateRecentMenus();

        // Start or stop the background file monitor
        if (_solutionManager.CurrentSolution is { } sol)
        {
            if (_fileMonitorService == null)
            {
                _fileMonitorService = new FileMonitorService(_editorRegistry, _fileMonitorSource);
                _fileMonitorService.ChangesetFileChanged += OnChangesetFileChanged;
            }
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

        // Solution status bar removed per user request
        // SolutionText.Text = _solutionManager.CurrentSolution is { } s
        //     ? $"Solution: {s.Name}"
        //     : "";
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

    // --- Layout persistence --------------------------------------------

    // True when the current session loaded a layout from disk (vs. first-run SetupDefaultLayout).
    // Used by DockingAdapter to decide whether plugin panels absent from the saved layout
    // should be deferred rather than auto-docked.
    private bool _layoutWasRestoredFromFile;

    private void LoadSavedLayoutOrDefault()
    {
        if (File.Exists(LayoutFilePath))
        {
            try
            {
                var layout = DockLayoutSerializer.Deserialize(File.ReadAllText(LayoutFilePath));
                RestoreWindowState(layout);
                PruneStaleDocumentItems(layout);
                ApplyLayout(layout);
                EnsureErrorPanel();
                _layoutWasRestoredFromFile = true;
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

    /// <summary>
    /// Removes document tabs whose backing file no longer exists on disk.
    /// Called before ApplyLayout to prevent creating editor instances for missing files.
    /// </summary>
    private void PruneStaleDocumentItems(DockLayoutRoot layout)
    {
        var pruned = new List<string>();
        PruneStaleItemsFromNode(layout.RootNode, pruned);
        PruneStaleItemsFromList(layout.FloatingItems, pruned);
        PruneStaleItemsFromList(layout.AutoHideItems, pruned);
        PruneStaleItemsFromList(layout.HiddenItems,   pruned);

        if (pruned.Count > 0)
            OutputLogger.Info(
                $"Layout restore: skipped {pruned.Count} document(s) — file no longer exists: " +
                string.Join(", ", pruned));
    }

    private static void PruneStaleItemsFromNode(DockNode node, List<string> pruned)
    {
        switch (node)
        {
            case DockGroupNode group:
                foreach (var item in group.Items.ToList())
                {
                    if (IsStaleDocumentItem(item, out var label))
                    {
                        group.RemoveItem(item);
                        pruned.Add(label);
                    }
                }
                break;

            case DockSplitNode split:
                foreach (var child in split.Children)
                    PruneStaleItemsFromNode(child, pruned);
                break;
        }
    }

    private static void PruneStaleItemsFromList(List<DockItem> items, List<string> pruned)
    {
        for (int i = items.Count - 1; i >= 0; i--)
        {
            if (IsStaleDocumentItem(items[i], out var label))
            {
                items.RemoveAt(i);
                pruned.Add(label);
            }
        }
    }

    /// <summary>
    /// Returns true if <paramref name="item"/> is a file-backed document whose physical file is missing.
    /// In-memory new files (IsNewFile=true) and items without a FilePath are excluded.
    /// </summary>
    private static bool IsStaleDocumentItem(DockItem item, out string label)
    {
        label = string.Empty;

        bool isDocument = item.ContentId.StartsWith("doc-file-")
                       || item.ContentId.StartsWith("doc-hex-")
                       || item.ContentId.StartsWith("doc-proj-");
        if (!isDocument) return false;

        if (!item.Metadata.TryGetValue("FilePath", out var filePath) || filePath is null)
            return false;

        // In-memory new documents have no physical backing file
        if (item.Metadata.TryGetValue("IsNewFile", out var isNew) && isNew == "true")
            return false;

        if (File.Exists(filePath)) return false;

        label = item.Title ?? Path.GetFileName(filePath) ?? filePath;
        return true;
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

            // Snapshot current editor state into each DockItem.Metadata before serialising.
            SnapshotEditorConfigs();

            Directory.CreateDirectory(Path.GetDirectoryName(LayoutFilePath)!);
            File.WriteAllText(LayoutFilePath, DockLayoutSerializer.Serialize(_layout));
            OutputLogger.Debug($"Layout auto-saved to: {LayoutFilePath}");
            SaveSession();

            // Persist SE collapse state to .whsln.user sidecar (fire-and-forget, non-blocking).
            if (_solutionExplorerPanel != null && _solutionManager.CurrentSolution != null)
                _ = SaveSolutionUserFileAsync(_solutionManager.CurrentSolution.FilePath);
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

    /// <summary>
    /// Iterates all open editor tabs and snapshots their current <see cref="EditorConfigDto"/>
    /// into <c>DockItem.Metadata["EditorConfigJson"]</c> so the layout serialiser captures
    /// up-to-date position data (scroll, selection, caret) for every editor.
    /// </summary>
    private void SnapshotEditorConfigs()
    {
        foreach (var (contentId, uiElement) in _contentCache)
        {
            if (uiElement is not IEditorPersistable persistable) continue;

            var dockItem = _engine.Layout.FindItemByContentId(contentId);
            if (dockItem is null) continue;

            try
            {
                var cfg = persistable.GetEditorConfig();
                dockItem.Metadata["EditorConfigJson"] =
                    System.Text.Json.JsonSerializer.Serialize(cfg);
            }
            catch
            {
                // Non-critical — skip editors that fail to report their state.
            }
        }
    }

    /// <summary>
    /// Writes the Solution Explorer expand state to the <c>.whsln.user</c> sidecar.
    /// Only runs when <c>AppSettings.SolutionExplorer.PersistCollapseState</c> is true.
    /// </summary>
    private async Task SaveSolutionUserFileAsync(string solutionFilePath)
    {
        if (!AppSettingsService.Instance.Current.SolutionExplorer.PersistCollapseState) return;
        try
        {
            var keys = _solutionExplorerPanel!.GetExpandedNodeKeys();
            await SolutionUserSerializer.WriteTreeStateAsync(solutionFilePath, keys);
        }
        catch (Exception ex)
        {
            OutputLogger.Error($"Failed to save .whsln.user: {ex.Message}");
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
            // WH-native formats can be opened immediately (no plugin loader required).
            // VS formats (.sln/.csproj/etc.) require ISolutionLoader extensions registered by
            // InitializePluginSystemAsync — store the path and open it there instead.
            var ext = Path.GetExtension(path).ToLowerInvariant();
            if (ext is ".whsln" or ".whproj")
                _ = OpenSolutionAsync(path);
            else
                _pendingRestoreSolutionPath = path;
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
        if (ext is ".whsln" or ".whproj" or ".sln" or ".csproj" or ".vbproj" or ".fsproj")
        {
            if (File.Exists(path)) _ = OpenSolutionAsync(path);
            else OutputLogger.Error($"Startup solution not found: {path}");
            return;
        }
        if (!File.Exists(path)) { OutputLogger.Error($"Startup file not found: {path}"); return; }

        OpenFileDirectly(path);
        PopulateRecentMenus();
    }

    // --- Layout helpers ------------------------------------------------

    private void SetupDefaultLayout()
    {
        _solutionExplorerPanel ??= CreateSolutionExplorerPanel();

        var layout = new DockLayoutRoot();
        var engine = new DockEngine(layout);

        layout.MainDocumentHost.AddItem(new DockItem { Title = "Welcome", ContentId = "doc-welcome" });

        var seItem = new DockItem { Title = "Solution Explorer", ContentId = SolutionExplorerContentId };
        engine.Dock(seItem, layout.MainDocumentHost, DockDirection.Left);

        // Error List and Plugin Monitor docked first; Output added last so it is the active tab.
        var errorsItem = new DockItem { Title = "Error List", ContentId = ErrorPanelContentId };
        engine.Dock(errorsItem, layout.MainDocumentHost, DockDirection.Bottom);

        var pluginMonitor = new DockItem { Title = "Plugin Monitor", ContentId = PluginMonitorContentId };
        engine.Dock(pluginMonitor, errorsItem.Owner!, DockDirection.Center);

        var output = new DockItem { Title = "Output", ContentId = "panel-output" };
        engine.Dock(output, errorsItem.Owner!, DockDirection.Center);

        // Note: analysis panels (DataInspector, StructureOverlay, FormatInfo, FileStatistics,
        // PatternAnalysis, ParsedFields) are registered by their respective plugins via UIRegistry.
        // They dock automatically based on PanelDescriptor.DefaultDockSide.

        ApplyLayout(layout, engine);
        OutputLogger.Debug("Default layout applied.");
    }

    private void ApplyLayout(DockLayoutRoot layout, DockEngine? engine = null)
    {
        DockHost.TabCloseRequested  -= OnTabCloseRequested;
        DockHost.ActiveItemChanged  -= OnActiveDocumentChanged;

        _layout  = layout;
        _isLocked = false;
        LockMenuItem.IsChecked = false;

        DockHost.ContentFactory    = CreateContentForItem;
        DockHost.TabCloseRequested += OnTabCloseRequested;
        DockHost.ActiveItemChanged += OnActiveDocumentChanged;
        DockHost.Layout = _layout;

        // DockHost.Layout creates its own DockEngine internally (OnLayoutChanged).
        // We MUST use that same engine so that _engine.Close(item) fires DockControl.ItemClosed,
        // which evicts the stale UIElement from DockControl._contentCache.
        // Using a separate engine here would leave the DockControl cache stale on close,
        // causing the next open of the same ContentId to return the old, already-Close()d control.
        _engine = DockHost.Engine!;

        // Rebind the DockingAdapter to the new engine/layout so plugin panel toggles continue
        // to work after Reset Layout or Load Layout replaces the layout tree.
        _dockingAdapter?.RebindLayout(_engine, _layout);

        SyncDocumentCounter();
        UpdateStatusBar();
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
            else if (id.StartsWith("doc-file-") && int.TryParse(id["doc-file-".Length..], out var n2))
                max = Math.Max(max, n2);
            else if (id.StartsWith("doc-diff-") && int.TryParse(id["doc-diff-".Length..], out var n3))
                max = Math.Max(max, n3);
            else if (id.StartsWith("doc-entropy-") && int.TryParse(id["doc-entropy-".Length..], out var n4))
                max = Math.Max(max, n4);
        }

        _documentCounter = max;
    }

    // --- Content factory (with cache) ----------------------------------

    private object CreateContentForItem(DockItem item)
    {
        if (_displayContent.TryGetValue(item.ContentId, out var cachedDisplay))
            return cachedDisplay;
        var display = BuildContentForItem(item);
        StoreContent(item.ContentId, display);
        RegisterDocumentFromItem(item, display);
        return display;
    }

    /// <summary>
    /// Stores <paramref name="displayElement"/> in both content caches:
    /// <c>_displayContent</c> (the visual element shown by the docking system — may be an InfoBar wrapper),
    /// and <c>_contentCache</c> (the actual editor — unwrapped when an InfoBar is present).
    /// </summary>
    private void StoreContent(string contentId, UIElement displayElement)
    {
        _displayContent[contentId] = displayElement;
        _contentCache[contentId]   = UnwrapEditor(displayElement);

        // In split-pane layouts the user can click inside a content area without
        // changing tab selection, so TrackActivation never fires.  Subscribe to
        // IsKeyboardFocusWithinChanged so the status bar stays in sync with the
        // editor that actually has keyboard focus.
        if (!contentId.StartsWith("panel-"))
        {
            displayElement.IsKeyboardFocusWithinChanged += (_, e) =>
            {
                if ((bool)e.NewValue)
                    SyncStatusBarToFocusedEditor(contentId);
            };
        }
    }

    private UIElement BuildContentForItem(DockItem item) =>
        item.ContentId switch
        {
            SolutionExplorerContentId  => CreateSolutionExplorerContent(),
            "panel-output"             => CreateOutputContent(),
            "panel-properties"         => CreatePropertiesContent(),
            ErrorPanelContentId        => CreateErrorPanelContent(),
            FileComparisonPanelContentId   => CreateFileComparisonContent(),
            ArchivePanelContentId          => CreateArchivePanelContent(),
            OptionsContentId               => CreateOptionsContent(),
            "doc-welcome"                  => CreateWelcomeContent(),
            TerminalPanelContentId         => CreateTerminalPanelContent(),
            PluginMonitorContentId         => CreatePluginMonitorPanelContent(),
            PluginManagerContentId         => CreatePluginManagerContent(),
            _ when item.ContentId.StartsWith("doc-file-")      => CreateSmartFileEditorContent(item),
            _ when item.ContentId.StartsWith("doc-hex-")       => WrapHexDocItemWithInfoBar(item),
            _ when item.ContentId.StartsWith("doc-projprops-") => CreateProjectPropertiesContent(item),
            _ when item.ContentId.StartsWith("doc-nuget-")     => CreateNuGetManagerContent(item),
            _ when item.ContentId.StartsWith("doc-proj-")      => CreateProjectItemContent(item),
            _ => CreateDocumentContent(item)
        };

    // --- Panel factories -----------------------------------------------

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
        panel.ItemDeleteRequested           += OnSolutionExplorerItemDeleteRequested;
        panel.ItemDeleteFromDiskRequested   += OnSEItemDeleteFromDiskRequested;
        panel.ItemMoveRequested         += OnSolutionExplorerItemMoveRequested;
        panel.DefaultTblChangeRequested += OnDefaultTblChangeRequested;
        panel.ApplyTblRequested         += OnSEApplyTbl;
        panel.AddNewItemRequested              += OnSEAddNewItem;
        panel.AddExistingItemRequested         += OnSEAddExistingItem;
        panel.ImportFormatDefinitionRequested  += OnSEImportFormatDefinition;
        panel.ImportSyntaxDefinitionRequested  += OnSEImportSyntaxDefinition;
        panel.ConvertTblRequested              += OnSEConvertTbl;
        panel.FolderCreateRequested            += OnSEFolderCreateRequested;
        panel.FolderRenameRequested            += OnSEFolderRenameRequested;
        panel.FolderDeleteRequested            += OnSEFolderDeleteRequested;
        panel.FolderFromDiskRequested          += OnSEFolderFromDiskRequested;
        panel.SolutionRenameRequested          += OnSESolutionRenameRequested;
        panel.ProjectRenameRequested           += OnSEProjectRenameRequested;
        panel.CloseSolutionRequested           += OnSECloseSolutionRequested;
        panel.SaveAllRequested                 += OnSESaveAll;
        panel.OpenWithRequested                += OnSEOpenWith;
        panel.OpenWithSpecificRequested        += OnSEOpenWithSpecific;
        panel.PhysicalFileIncludeRequested     += OnSEPhysicalFileInclude;
        panel.ImportExternalFileRequested      += OnSEImportExternalFile;
        panel.PropertiesRequested              += OnSEPropertiesRequested;
        panel.ManageNuGetPackagesRequested     += OnSEManageNuGetPackages;
        panel.WriteToDiskRequested             += OnSEWriteToDisk;
        panel.DiscardChangesetRequested        += OnSEDiscardChangeset;
        panel.SolutionFolderCreateRequested    += OnSESolutionFolderCreateRequested;
        panel.SolutionFolderRenameRequested    += OnSESolutionFolderRenameRequested;
        panel.SolutionFolderDeleteRequested    += OnSESolutionFolderDeleteRequested;
        panel.ProjectMoveRequested             += OnSEProjectMoveRequested;
        panel.ClipboardPasteRequested          += OnSEClipboardPaste;
        panel.SourceLineNavigationRequested    += OnSESourceLineNavigationRequested;
        _solutionManager.ItemRenamed           += OnProjectItemRenamed;
        // Build context-menu events (guards handle the case where build system isn't ready yet)
        panel.BuildProjectRequested       += (_, id) => { if (_buildSystem is not null) _ = RunBuildProjectByIdAsync(id); };
        panel.RebuildProjectRequested     += (_, id) => { if (_buildSystem is not null) _ = RunRebuildProjectByIdAsync(id); };
        panel.CleanProjectRequested       += (_, id) => { if (_buildSystem is not null) _ = RunCleanProjectByIdAsync(id); };
        panel.SetStartupProjectRequested  += (_, id) =>
        {
            SetStartupProject(id);
            OnPropertyChanged(nameof(ActiveStartupProjectName));
            OnPropertyChanged(nameof(CanRunStartupProject));
        };
        // Provide the editor registry so the panel can build the "Open With ›" submenu
        panel.SetEditorRegistry(_editorRegistry.GetAll());
        // Sync current solution if already loaded
        panel.SetSolution(_solutionManager.CurrentSolution);
        return panel;
    }

    private UIElement CreatePropertiesContent()
    {
        _propertiesPanel ??= new PropertiesPanel();
        return _propertiesPanel;
    }

    private UIElement CreateOutputContent()
    {
        _outputPanel ??= new OutputPanel();
        return _outputPanel;
    }

    private UIElement CreateFileComparisonContent()
    {
        // FileComparisonPanel manages its own file selection internally
        var panel = new WpfHexEditor.Panels.FileOps.FileComparisonPanel();
        return panel;
    }

    private UIElement CreateArchivePanelContent()
    {
        _archivePanel ??= new WpfHexEditor.Panels.FileOps.ArchiveStructurePanel();
        return _archivePanel;
    }

    private UIElement CreateErrorPanelContent() => EnsureErrorPanelInstance();

    private UIElement CreateOptionsContent()
    {
        if (_contentCache.TryGetValue(OptionsContentId, out var existing)) return existing;
        var ctrl = new OptionsEditorControl();
        ctrl.SettingsChanged   += OnOptionsSettingsChanged;
        ctrl.EditJsonRequested += OnOptionsEditJson;
        StoreContent(OptionsContentId, ctrl);
        return ctrl;
    }

    private UIElement CreateWelcomeContent() =>
        new Controls.WelcomePanel().Configure(
            onNewFile:           () => OnNewFile(this, new RoutedEventArgs()),
            onOpenFile:          () => OnOpenFile(this, new RoutedEventArgs()),
            onOpenProject:       () => OnOpenSolutionOrProject(this, new RoutedEventArgs()),
            onOptions:           () => OnSettings(this, new RoutedEventArgs()),
            recentFiles:         _solutionManager.RecentFiles,
            recentSolutions:     _solutionManager.RecentSolutions,
            openRecentFile:      path => { OpenFileDirectly(path); PopulateRecentMenus(); },
            openRecentSolution:  path => _ = OpenSolutionAsync(path));

    /// <summary>
    /// Creates <see cref="_errorPanel"/> the first time it is needed, without docking it.
    /// Call this instead of null-checking <see cref="_errorPanel"/> so that diagnostic sources
    /// (e.g. TblEditor, CodeEditor) can register even before the panel is first shown.
    /// </summary>
    private ErrorPanel EnsureErrorPanelInstance()
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
            // Re-lookup in case the file was just opened (content was null before OpenFileDirectly)
            var resolvedContent = content ?? _contentCache.Values.FirstOrDefault(v =>
            {
                if (v is HexEditorControl hex)
                    return string.Equals(hex.FileName, e.FilePath, System.StringComparison.OrdinalIgnoreCase);
                if (v is WpfHexEditor.Editor.TblEditor.Controls.TblEditor tbl)
                    return string.Equals(tbl.Title.TrimEnd('*', ' '),
                        Path.GetFileName(e.FilePath), System.StringComparison.OrdinalIgnoreCase);
                return false;
            });

            // HexEditor — byte offset
            if (e.Offset.HasValue && _connectedHexEditor != null)
                _connectedHexEditor.SetPosition(e.Offset.Value);

            // TblEditor — jump to hex entry (key stored in Tag by repair service)
            if (e.Tag is string hexKey && resolvedContent is WpfHexEditor.Editor.TblEditor.Controls.TblEditor tblEd)
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

            StoreContent(newContentId, editor);
            var newDockItem = new DockItem
            {
                ContentId = newContentId,
                Title     = Path.GetFileName(e.FilePath),
                Metadata  = { ["FilePath"] = e.FilePath }
            };
            _engine.Dock(newDockItem, _layout.MainDocumentHost, DockDirection.Center);
            RegisterDocumentFromItem(newDockItem, editor);
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
        IProject? project     = null,
        DockItem? ownerItem   = null)
    {
        var hexEditor = new HexEditorControl();
        ApplyHexEditorDefaults(hexEditor);
        hexEditor.ShowStatusBar       = false;
        hexEditor.ShowProgressOverlay = false;  // App handles progress in its own status bar

        // -- Phase 12: in-memory new document --------------------------
        if (isNewFile)
        {
            hexEditor.OpenNew(displayName ?? "New1.bin");
            OutputLogger.Info($"New in-memory document: {displayName}");
            return hexEditor;
        }

        // -- File-backed document ---------------------------------------
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
                ApplyDefaultTbl(hexEditor, project);

                // Apply theme eagerly before the editor enters the visual tree.
                // The Loaded event fires asynchronously (next dispatcher pass), so without
                // this call the docking system may render one frame with default light colors.
                hexEditor.ApplyThemeFromResources();

                OutputLogger.Info($"Opened: {filePath}");
                return hexEditor;
            }
            catch (IOException ex)
            {
                var msg = $"Cannot open '{Path.GetFileName(filePath)}': {ex.Message}";
                OutputLogger.Error(msg);
                ShowOrCreatePanel("Output", "panel-output", DockDirection.Bottom);
                if (ownerItem is not null)
                    Dispatcher.InvokeAsync(() => CloseTab(ownerItem, promptIfDirty: false),
                                           System.Windows.Threading.DispatcherPriority.Background);
                return MakeErrorBlock(msg);
            }
            catch (UnauthorizedAccessException ex)
            {
                var msg = $"Access denied '{Path.GetFileName(filePath)}': {ex.Message}";
                OutputLogger.Error(msg);
                ShowOrCreatePanel("Output", "panel-output", DockDirection.Bottom);
                if (ownerItem is not null)
                    Dispatcher.InvokeAsync(() => CloseTab(ownerItem, promptIfDirty: false),
                                           System.Windows.Threading.DispatcherPriority.Background);
                return MakeErrorBlock(msg);
            }
        }

        OutputLogger.Error($"File not found: {filePath}");
        ShowOrCreatePanel("Output", "panel-output", DockDirection.Bottom);
        if (ownerItem is not null)
            Dispatcher.InvokeAsync(() => CloseTab(ownerItem, promptIfDirty: false),
                                   System.Windows.Threading.DispatcherPriority.Background);
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

        // Priority 1: "Open With > Hex Editor" bypass — skip the registry entirely
        bool forceHex = item.Metadata.TryGetValue("ForceHexEditor", out var fh) && fh == "true";

        // Priority 2: explicit factory requested (e.g. via InfoBar or "Open With ›" submenu)
        IEditorFactory? factory;
        if (forceHex)
        {
            factory = null;
        }
        else if (item.Metadata.TryGetValue("ForceEditorId", out var eid) && eid is not null)
        {
            factory = _editorRegistry.GetAll().FirstOrDefault(f => f.Descriptor.Id == eid);
        }
        else
        {
            factory = _editorRegistry.FindFactory(filePath, GetPreferredEditorId(filePath));
        }

        // Record which editor was resolved so "View in" deduplication can match back to this tab.
        item.Metadata["ActiveEditorId"] = factory?.Descriptor.Id ?? "hex-editor";

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
                if (editor is IDiagnosticSource diagSrc)
                    EnsureErrorPanelInstance().AddSource(diagSrc);

                ActiveDocumentEditor       = editor;
                ActiveStatusBarContributor = editor as IStatusBarContributor;

                // Wrap with InfoBar (shows "View in: Hex Editor, …" action bar above the viewer)
                return WrapWithInfoBar(fe, factory, filePath, item.ContentId);
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

                    // Restore pending changeset from .whchg companion file
                    if (ChangesetService.Instance.HasChangeset(projItem))
                    {
                        var dto = ChangesetService.Instance.ReadChangeset(projItem);
                        if (dto is not null)
                            hexPers.ApplyChangeset(dto);
                    }
                }
            }
        }

        // Wrap with InfoBar so the user can switch to another editor (Structure Editor, Code Editor, etc.)
        return hexContent is FrameworkElement hexFe
            ? WrapWithInfoBar(hexFe, null, filePath, item.ContentId)
            : hexContent;
    }

    /// <summary>
    /// Opens a file using the editor registry (extension-first), falling back to HexEditor.
    /// Used for <c>doc-file-*</c> items (File→Open, MRU, startup, drag-and-drop).
    /// </summary>
    private UIElement CreateSmartFileEditorContent(DockItem item)
    {
        item.Metadata.TryGetValue("FilePath", out var filePath);
        if (filePath is null) return CreateDocumentContent(item);

        // Support "Open With" for standalone files too (ForceHexEditor / ForceEditorId)
        bool forceHex = item.Metadata.TryGetValue("ForceHexEditor", out var fh) && fh == "true";
        IEditorFactory? factory;
        if (forceHex)
        {
            factory = null;
        }
        else if (item.Metadata.TryGetValue("ForceEditorId", out var eid) && eid is not null)
        {
            factory = _editorRegistry.GetAll().FirstOrDefault(f => f.Descriptor.Id == eid);
        }
        else
        {
            factory = _editorRegistry.FindFactory(filePath, GetPreferredEditorId(filePath));
        }

        if (factory?.Create() is IDocumentEditor editor && editor is FrameworkElement fe)
        {
            if (editor is IOpenableDocument openable)
                _ = openable.OpenAsync(filePath);

            // Apply user settings (scroll speed, etc.) from Options to newly created editors.
            ApplyEditorSettings(editor);

            // Restore per-file position (scroll, selection, caret) from previous session.
            if (editor is IEditorPersistable persistable &&
                item.Metadata.TryGetValue("EditorConfigJson", out var cfgJson))
            {
                try
                {
                    var cfg = System.Text.Json.JsonSerializer.Deserialize<EditorConfigDto>(cfgJson);
                    if (cfg != null) persistable.ApplyEditorConfig(cfg);
                }
                catch { /* ignore malformed config */ }
            }

            editor.OutputMessage += OnEditorOutputMessage;

            if (editor is IDiagnosticSource diagSrc)
                EnsureErrorPanelInstance().AddSource(diagSrc);

            ActiveDocumentEditor       = editor;
            ActiveStatusBarContributor = editor as IStatusBarContributor;

            var display = WrapWithInfoBar(fe, factory, filePath, item.ContentId);

            // CodeEditor is a FrameworkElement with no embedded Canvas. Add a Canvas overlay
            // to the InfoBar Grid (Row 1) so the QuickSearchBar can be shown inline.
            if (editor is CodeEditorControl json && display is Grid infoBarGrid)
            {
                // No Background + default IsHitTestVisible=True → empty areas let clicks
                // through to the editor; the QuickSearchBar captures clicks in its own area.
                var canvas = new Canvas();
                Panel.SetZIndex(canvas, 5);
                Grid.SetRow(canvas, 1);
                var bar = new QuickSearchBar
                {
                    Width      = 520,
                    Visibility = Visibility.Collapsed,
                };
                bar.OnCloseRequested += (_, __) =>
                {
                    bar.Visibility = Visibility.Collapsed;
                    bar.Detach();
                };
                canvas.Children.Add(bar);
                infoBarGrid.Children.Add(canvas);
                _codeEditorBars[json] = bar;
            }

            return display;
        }

        // Fallback: HexEditor for any binary/unknown file — wrapped with InfoBar
        var hexFallback = CreateHexEditorContent(filePath, ownerItem: item);
        return hexFallback is FrameworkElement hexFe2
            ? WrapWithInfoBar(hexFe2, null, filePath, item.ContentId)
            : hexFallback;
    }

    /// <summary>
    /// Derives the preferred editor factory ID for <paramref name="filePath"/> by consulting the
    /// <see cref="EmbeddedFormatCatalog"/> for a matching format entry.
    /// Returns the explicit <c>preferredEditor</c> field from the .whfmt definition,
    /// or <c>null</c> to fall back to registry first-match order.
    /// All 427 built-in format definitions carry an explicit tag — no implicit derivation needed.
    /// </summary>
    private static string? GetPreferredEditorId(string filePath)
    {
        var ext = Path.GetExtension(filePath)?.ToLowerInvariant();
        if (string.IsNullOrEmpty(ext)) return null;

        var entry = EmbeddedFormatCatalog.Instance.GetAll()
            .FirstOrDefault(e => e.Extensions.Any(
                x => string.Equals(x, ext, StringComparison.OrdinalIgnoreCase)));

        return entry?.PreferredEditor;
    }

    /// <summary>
    /// Wraps a registry-dispatched editor <paramref name="editorElement"/> in a <see cref="Grid"/>
    /// that also contains a <see cref="DocumentInfoBar"/> offering quick "View in …" actions.
    /// The InfoBar is NOT shown when the editor was explicitly forced (e.g. via "Open With > Hex Editor").
    /// </summary>
    private UIElement WrapWithInfoBar(
        FrameworkElement editorElement,
        IEditorFactory?  usedFactory,     // null = HexEditor fallback
        string           filePath,
        string           sourceContentId)
    {
        var currentName = usedFactory?.Descriptor.DisplayName ?? "Hex Editor";
        var currentId   = usedFactory?.Descriptor.Id          ?? "hex-editor";

        // Build the list of alternative editors for the InfoBar buttons
        var alternatives = _editorRegistry.GetAll()
            .Where(f => f != usedFactory && f.CanOpen(filePath))
            .ToList();

        var bar = new DocumentInfoBar();
        bar.Configure(
            filePath:          filePath,
            sourceContentId:   sourceContentId,
            currentEditorName: currentName,
            currentEditorId:   currentId,
            alternatives:      alternatives);
        bar.OpenWithRequested += OnInfoBarOpenWith;

        var grid = new Grid();
        // Tag stores the actual editor so the docking system can retrieve it from the Grid wrapper.
        // _contentCache always holds the unwrapped editor via UnwrapEditor().
        grid.Tag = editorElement;
        grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        Grid.SetRow(bar,           0);
        Grid.SetRow(editorElement, 1);
        grid.Children.Add(bar);
        grid.Children.Add(editorElement);
        return grid;
    }

    /// <summary>
    /// Creates a HexEditor for a <c>doc-hex-*</c> <see cref="DockItem"/> and wraps it
    /// in an InfoBar so the user can switch to another editor from the View-in bar.
    /// New/unnamed documents (IsNewFile=true) and documents without a file path are
    /// returned unwrapped because there is no format context to offer alternatives.
    /// </summary>
    private UIElement WrapHexDocItemWithInfoBar(DockItem item)
    {
        item.Metadata.TryGetValue("FilePath",    out var fp);
        item.Metadata.TryGetValue("DisplayName", out var dn);
        var isNew = item.Metadata.TryGetValue("IsNewFile", out var isNewStr) && isNewStr == "true";
        var hex   = CreateHexEditorContent(fp, dn, isNew, ownerItem: item);

        // Only wrap when we have a real file path (new/unnamed docs have no format context)
        return fp is not null && !isNew && hex is FrameworkElement fe
            ? WrapWithInfoBar(fe, null, fp, item.ContentId)
            : hex;
    }

    /// <summary>
    /// Unwraps the actual editor element from a <see cref="WrapWithInfoBar"/> Grid wrapper,
    /// or returns <paramref name="content"/> directly when it is not a wrapper.
    /// </summary>
    private static UIElement UnwrapEditor(UIElement content)
        => content is Grid { Tag: UIElement inner } ? inner : content;

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

    // --- SolutionExplorer events ----------------------------------------

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

    /// <summary>
    /// Loads the project's default TBL (or TBLX) into <paramref name="hexEditor"/> if one is set.
    /// No-op when the project is null, has no default TBL, or the file cannot be read.
    /// </summary>
    private void ApplyDefaultTbl(HexEditorControl hexEditor, IProject? project)
    {
        if (project?.DefaultTblItemId is null) return;

        var tblItem = project.FindItem(project.DefaultTblItemId);
        if (tblItem is null || !File.Exists(tblItem.AbsolutePath)) return;

        try
        {
            var result = new TblImportService().ImportFromFile(tblItem.AbsolutePath);
            if (!result.Success || result.Entries.Count == 0) return;

            var tbl = new TblStream();
            foreach (var dte in result.Entries)
                tbl.Add(dte);

            hexEditor.LoadTBL(tbl, tblItem.AbsolutePath);
            OutputLogger.Debug($"Applied default TBL '{tblItem.Name}' ({result.Entries.Count} entries)");
        }
        catch (Exception ex)
        {
            OutputLogger.Warn($"Failed to apply default TBL '{tblItem.Name}': {ex.Message}");
        }
    }

    // -- TBL management ---------------------------------------------------

    /// <summary>
    /// Handles "Apply to Active Document" / "Apply to All Documents" from the Solution Explorer.
    /// </summary>
    private void OnSEApplyTbl(object? sender, ApplyTblRequestedEventArgs e)
    {
        var tbl = LoadTblFromProjectItem(e.TblItem);
        if (tbl is null) return;

        if (e.ApplyToAll)
        {
            foreach (var c in _contentCache.Values.OfType<HexEditorControl>())
            {
                c.LoadTBL(tbl, e.TblItem.AbsolutePath);
                _editorProjectTbl[c] = e.TblItem;
            }
            OutputLogger.Info($"TBL '{e.TblItem.Name}' applied to all open documents.");
        }
        else
        {
            if (ActiveHexEditor is null) { OutputLogger.Warn("No active HexEditor — cannot apply TBL."); return; }
            ActiveHexEditor.LoadTBL(tbl, e.TblItem.AbsolutePath);
            _editorProjectTbl[ActiveHexEditor] = e.TblItem;
            SyncTblDropdownToActiveEditor();
            OutputLogger.Info($"TBL '{e.TblItem.Name}' applied to active document.");
        }
    }

    /// <summary>
    /// Loads a <see cref="TblStream"/> from a project item using TblImportService
    /// (supports .tbl, .tblx, .csv, .atlas formats — same as <see cref="ApplyDefaultTbl"/>).
    /// </summary>
    private TblStream? LoadTblFromProjectItem(IProjectItem item)
    {
        if (!File.Exists(item.AbsolutePath))
        {
            OutputLogger.Warn($"TBL file not found: {item.AbsolutePath}");
            return null;
        }
        try
        {
            var result = new TblImportService().ImportFromFile(item.AbsolutePath);
            if (!result.Success || result.Entries.Count == 0) return null;
            var tbl = new TblStream();
            foreach (var dte in result.Entries) tbl.Add(dte);
            return tbl;
        }
        catch (Exception ex)
        {
            OutputLogger.Warn($"Failed to load TBL '{item.Name}': {ex.Message}");
            return null;
        }
    }

    /// <summary>
    /// Rebuilds the TBL ComboBox list from built-in tables + current solution project items.
    /// </summary>
    private void RebuildTblItemList()
    {
        _tblItems.Clear();

        // Section 1: Built-in tables
        _tblItems.Add(TblSelectionItem.MakeHeader("Built-in Tables"));
        _tblItems.Add(TblSelectionItem.MakeBuiltIn("Default (ASCII)", null));           // null → CloseTBL
        _tblItems.Add(TblSelectionItem.MakeBuiltIn("ASCII",            DefaultCharacterTableType.Ascii));
        _tblItems.Add(TblSelectionItem.MakeBuiltIn("EBCDIC + Special", DefaultCharacterTableType.EbcdicWithSpecialChar));
        _tblItems.Add(TblSelectionItem.MakeBuiltIn("EBCDIC (no spec)", DefaultCharacterTableType.EbcdicNoSpecialChar));

        // Section 2: Encodings
        _tblItems.Add(TblSelectionItem.MakeSeparator());
        _tblItems.Add(TblSelectionItem.MakeHeader("Encodings"));
        _tblItems.Add(TblSelectionItem.MakeEncoding("UTF-8",     CharacterTableType.UTF8));
        _tblItems.Add(TblSelectionItem.MakeEncoding("UTF-16 LE", CharacterTableType.UTF16LE));
        _tblItems.Add(TblSelectionItem.MakeEncoding("UTF-16 BE", CharacterTableType.UTF16BE));
        _tblItems.Add(TblSelectionItem.MakeEncoding("Latin-1",   CharacterTableType.Latin1));

        // Section 3: Project TBL files
        var projectTbls = _solutionManager.CurrentSolution?.Projects
            .SelectMany(p => p.Items)
            .Where(i => i.ItemType == ProjectItemType.Tbl && File.Exists(i.AbsolutePath))
            .ToList();

        if (projectTbls is { Count: > 0 })
        {
            _tblItems.Add(TblSelectionItem.MakeSeparator());
            _tblItems.Add(TblSelectionItem.MakeHeader("Project Tables"));
            foreach (var item in projectTbls)
                _tblItems.Add(TblSelectionItem.MakeProjectFile(item));
        }
    }

    /// <summary>
    /// Applies the selected TBL item to the currently active HexEditor.
    /// </summary>
    private void ApplyTblSelectionItem(TblSelectionItem item)
    {
        if (ActiveHexEditor is not { } hex) return;

        switch (item.Kind)
        {
            case TblSelectionKind.BuiltIn when item.BuiltInType is null:
                hex.CloseTBL();
                _editorProjectTbl[hex] = null;
                break;
            case TblSelectionKind.BuiltIn when item.BuiltInType is { } bt:
                hex.LoadTBL(TblStream.CreateDefaultTbl(bt), $"[{item.DisplayName}]");
                _editorProjectTbl[hex] = null;
                break;
            case TblSelectionKind.Encoding when item.EncodingType is { } enc:
                // CloseTBL() first so _tblStream is null before setting the encoding type.
                // Without this, the setter would call CloseTBL() itself and reset to Ascii,
                // then the assignment would be silently overwritten.
                hex.CloseTBL();
                hex.TypeOfCharacterTable = enc;
                _editorProjectTbl[hex] = null;
                break;
            case TblSelectionKind.ProjectFile when item.ProjectItem is { } pi:
                var tbl = LoadTblFromProjectItem(pi);
                if (tbl is not null)
                {
                    hex.LoadTBL(tbl, pi.AbsolutePath);
                    _editorProjectTbl[hex] = pi;
                }
                break;
        }
        OutputLogger.Info($"TBL changed to: {item.DisplayName}");
    }

    /// <summary>
    /// Syncs the TBL ComboBox selection to match the active HexEditor's current table state.
    /// Sets the backing field directly to avoid re-triggering ApplyTblSelectionItem.
    /// </summary>
    private void SyncTblDropdownToActiveEditor()
    {
        if (ActiveHexEditor is not { } hex)
        {
            _selectedTblItem = null;
            OnPropertyChanged(nameof(SelectedTblItem));
            return;
        }

        TblSelectionItem? match = null;

        if (hex.TypeOfCharacterTable == CharacterTableType.TblFile)
        {
            if (_editorProjectTbl.TryGetValue(hex, out var pi) && pi is not null)
                match = _tblItems.FirstOrDefault(i => i.Kind == TblSelectionKind.ProjectFile && i.ProjectItem?.Id == pi.Id);
        }
        else if (hex.TypeOfCharacterTable == CharacterTableType.Ascii)
        {
            match = _tblItems.FirstOrDefault(i => i.Kind == TblSelectionKind.BuiltIn && i.BuiltInType is null);
        }
        else
        {
            var enc = hex.TypeOfCharacterTable;
            match = _tblItems.FirstOrDefault(i => i.Kind == TblSelectionKind.Encoding && i.EncodingType == enc);
        }

        _selectedTblItem = match;
        OnPropertyChanged(nameof(SelectedTblItem));
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
        // -- Direct import path (D&D from Windows Explorer, clipboard paste with known paths) --
        if (e.FilePaths is { Count: > 0 } directPaths)
        {
            foreach (var srcPath in directPaths)
            {
                if (File.Exists(srcPath))
                    await _solutionManager.AddItemAsync(e.Project, srcPath,
                        virtualFolderId: e.TargetFolderId);
                else if (Directory.Exists(srcPath))
                    // Register the entire directory tree in-place (no physical copy — same
                    // behaviour as "Add Existing Folder From Disk" in the context menu).
                    await ImportFolderFromDiskAsync(e.Project, srcPath, e.TargetFolderId);
            }
            return;
        }

        // -- Normal flow: show dialog for user to pick file(s) --------------
        var dlg = new AddExistingItemDialog(e.Project) { Owner = this };
        if (dlg.ShowDialog() != true) return;

        foreach (var srcPath in dlg.SelectedFilePaths)
        {
            var finalPath = srcPath;

            // -- 1. Physical copy to user-selected destination folder ----------
            if (dlg.CopyToProject)
            {
                var destDir = dlg.SelectedPhysicalDestination
                           ?? Path.GetDirectoryName(e.Project.ProjectFilePath)!;
                Directory.CreateDirectory(destDir);
                finalPath = Path.Combine(destDir, Path.GetFileName(srcPath));
                if (!string.Equals(finalPath, srcPath, StringComparison.OrdinalIgnoreCase))
                    File.Copy(srcPath, finalPath, overwrite: false);
            }

            // -- 2. Register item in project (no virtual-folder assignment for files) --
            await _solutionManager.AddItemAsync(e.Project, finalPath, virtualFolderId: null);
        }
    }

    /// <summary>
    /// Handles clipboard paste from the Solution Explorer (Ctrl+V or "Paste" context menu).
    /// Always performs a physical file copy (or move for Cut) into the destination folder,
    /// then registers the item in the project under the correct virtual folder.
    /// Shows <see cref="PasteConflictDialog"/> when a file with the same name already exists.
    /// </summary>
    private async void OnSEClipboardPaste(object? sender, AddExistingItemEventArgs e)
    {
        var project = ResolveProjectForPaste(e.TargetFolder);
        if (project is null) return;

        // Resolve the physical directory that maps to the selected virtual folder.
        // If the folder has a PhysicalRelativePath (disk-backed folder), use it;
        // otherwise fall back to the project root directory.
        var projDir     = Path.GetDirectoryName(project.ProjectFilePath)!;
        var physDestDir = e.TargetFolder?.PhysicalRelativePath is { } rel
            ? Path.Combine(projDir, rel)
            : projDir;
        Directory.CreateDirectory(physDestDir);

        var targetFolderId = e.TargetFolder?.Id;

        try
        {
            foreach (var srcPath in e.FilePaths)
            {
                if (File.Exists(srcPath))
                {
                    var fileName = Path.GetFileName(srcPath);
                    var destPath = Path.Combine(physDestDir, fileName);

                    // -- Name-conflict resolution --------------------------------------
                    if (File.Exists(destPath) &&
                        !string.Equals(srcPath, destPath, StringComparison.OrdinalIgnoreCase))
                    {
                        var dlg = new PasteConflictDialog
                        {
                            OriginalName = fileName,
                            NewName      = BuildSuggestedName(physDestDir, fileName),
                            Owner        = this,
                        };

                        if (dlg.ShowDialog() != true)
                        {
                            if (dlg.CancelAll) return;  // user chose "Cancel All" — stop processing
                            continue;                    // user chose "Skip" — move to next file
                        }

                        destPath = Path.Combine(physDestDir, dlg.NewName);
                    }

                    // -- Physical file operation ---------------------------------------
                    if (e.IsCut)
                    {
                        if (!string.Equals(srcPath, destPath, StringComparison.OrdinalIgnoreCase))
                            File.Move(srcPath, destPath, overwrite: false);
                    }
                    else
                    {
                        // Copy: always produce a physical file at the destination,
                        // never keep a reference to the source path.
                        File.Copy(srcPath, destPath, overwrite: false);
                    }

                    await _solutionManager.AddItemAsync(project, destPath,
                                                        virtualFolderId: targetFolderId);
                }
                else if (Directory.Exists(srcPath))
                {
                    // -- Folder paste: physical copy (or move) + recursive registration --
                    var dirName = Path.GetFileName(
                        srcPath.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
                    var destDir = Path.Combine(physDestDir, dirName);

                    if (e.IsCut)
                    {
                        // Same-drive move is instantaneous; cross-drive requires copy + delete.
                        if (string.Equals(Path.GetPathRoot(srcPath), Path.GetPathRoot(destDir),
                                StringComparison.OrdinalIgnoreCase))
                            Directory.Move(srcPath, destDir);
                        else
                        {
                            CopyDirectoryRecursive(srcPath, destDir);
                            Directory.Delete(srcPath, recursive: true);
                        }
                    }
                    else
                    {
                        CopyDirectoryRecursive(srcPath, destDir);
                    }

                    await _solutionManager.AddFolderFromDiskAsync(project, destDir, targetFolderId);
                }
            }
        }
        finally
        {
            // Safety-net: guarantee the SE tree reflects the current solution state,
            // even when AddItemAsync threw silently (async void swallows exceptions).
            _solutionExplorerPanel?.SetSolution(_solutionManager.CurrentSolution);
        }
    }

    /// <summary>
    /// Returns a free file name by appending "(copy)", "(copy 2)", etc.
    /// until a name that does not already exist in <paramref name="directory"/> is found.
    /// </summary>
    private static string BuildSuggestedName(string directory, string fileName)
    {
        var nameNoExt = Path.GetFileNameWithoutExtension(fileName);
        var ext       = Path.GetExtension(fileName);
        var candidate = $"{nameNoExt} (copy){ext}";
        var counter   = 2;
        while (File.Exists(Path.Combine(directory, candidate)))
            candidate = $"{nameNoExt} (copy {counter++}){ext}";
        return candidate;
    }

    /// <summary>
    /// Recursively copies all files and subdirectories from <paramref name="sourceDir"/>
    /// into <paramref name="destDir"/>, creating <paramref name="destDir"/> if it does not exist.
    /// Existing files at the destination are not overwritten.
    /// </summary>
    private static void CopyDirectoryRecursive(string sourceDir, string destDir)
    {
        Directory.CreateDirectory(destDir);
        foreach (var file in Directory.GetFiles(sourceDir))
            File.Copy(file, Path.Combine(destDir, Path.GetFileName(file)), overwrite: false);
        foreach (var sub in Directory.GetDirectories(sourceDir))
            CopyDirectoryRecursive(sub, Path.Combine(destDir, Path.GetFileName(sub)));
    }

    /// <summary>
    /// Resolves the <see cref="IProject"/> that owns <paramref name="targetFolder"/>,
    /// or returns the first project in the solution when no folder is specified.
    /// </summary>
    private IProject? ResolveProjectForPaste(IVirtualFolder? targetFolder)
    {
        var solution = _solutionManager.CurrentSolution;
        if (solution is null || solution.Projects.Count == 0) return null;

        if (targetFolder is not null)
            return solution.Projects.FirstOrDefault(
                p => ContainsFolderById(p.RootFolders, targetFolder.Id));

        return solution.Projects[0];
    }

    private static bool ContainsFolderById(IReadOnlyList<IVirtualFolder> folders, string id)
    {
        foreach (var folder in folders)
        {
            if (folder.Id == id) return true;
            if (ContainsFolderById(folder.Children, id)) return true;
        }
        return false;
    }

    private async void OnSEImportFormatDefinition(object? sender, AddItemRequestedEventArgs e)
    {
        var catalog = EmbeddedFormatCatalog.Instance;
        var dlg     = new ImportEmbeddedFormatDialog(catalog, e.Project, e.TargetFolderId) { Owner = this };
        if (dlg.ShowDialog() != true || dlg.SelectedEntries.Count == 0) return;

        var projDir = Path.GetDirectoryName(e.Project.ProjectFilePath)!;
        foreach (var entry in dlg.SelectedEntries)
        {
            var destDir  = Path.Combine(projDir, entry.Category);
            Directory.CreateDirectory(destDir);
            var safeName = string.Concat(entry.Name.Split(Path.GetInvalidFileNameChars()))
                                 .Replace(' ', '_');
            var destPath = Path.Combine(destDir, safeName + ".whfmt");

            // Read the full embedded resource content and write it to disk.
            var json = catalog.GetJson(entry.ResourceKey);
            File.WriteAllText(destPath, json, System.Text.Encoding.UTF8);

            // Await the item registration so we can open it immediately in the editor,
            // ensuring the user sees the freshly imported content (not a stale open tab).
            var item = await _solutionManager.AddItemAsync(
                e.Project, destPath,
                virtualFolderId: dlg.TargetFolderId ?? e.TargetFolderId);
            OpenProjectItem(item, e.Project);
        }

        OutputLogger.Info(
            $"Imported {dlg.SelectedEntries.Count} format definition(s) into project '{e.Project.Name}'.");
    }

    private async void OnSEImportSyntaxDefinition(object? sender, AddItemRequestedEventArgs e)
    {
        var catalog = EmbeddedSyntaxCatalog.Instance;
        var dlg     = new ImportEmbeddedSyntaxDialog(catalog, e.Project, e.TargetFolderId) { Owner = this };
        if (dlg.ShowDialog() != true || dlg.SelectedEntries.Count == 0) return;

        var projDir = Path.GetDirectoryName(e.Project.ProjectFilePath)!;
        foreach (var entry in dlg.SelectedEntries)
        {
            var destDir  = Path.Combine(projDir, "SyntaxDefinitions", entry.Category);
            Directory.CreateDirectory(destDir);
            var safeName = string.Concat(entry.Name.Split(Path.GetInvalidFileNameChars()))
                                 .Replace(' ', '_');
            var destPath = Path.Combine(destDir, safeName + ".whlang");

            // Read the full embedded resource content and write it to disk.
            var content = catalog.GetContent(entry.ResourceKey);
            File.WriteAllText(destPath, content, System.Text.Encoding.UTF8);

            // Await registration then open the file so the user sees the imported content.
            var item = await _solutionManager.AddItemAsync(
                e.Project, destPath,
                virtualFolderId: dlg.TargetFolderId ?? e.TargetFolderId);
            OpenProjectItem(item, e.Project);
        }

        OutputLogger.Info(
            $"Imported {dlg.SelectedEntries.Count} syntax definition(s) into project '{e.Project.Name}'.");
    }

    // -- ROM-hint helpers for ConvertTblDialog auto-fill ----------------------

    /// <summary>
    /// Lazy-loaded lookup: file extension (lower-case, with dot) → platform name.
    /// Built once from the embedded .whfmt catalog; entries with an empty Platform
    /// field are excluded.
    /// </summary>
    private static Dictionary<string, string>? _romExtensionMap;

    private static Dictionary<string, string> RomExtensionMap
    {
        get
        {
            if (_romExtensionMap is not null) return _romExtensionMap;

            var map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            foreach (var entry in WpfHexEditor.Definitions.EmbeddedFormatCatalog.Instance.GetAll())
            {
                if (string.IsNullOrWhiteSpace(entry.Platform)) continue;

                foreach (var ext in entry.Extensions)
                {
                    var key = ext.StartsWith('.') ? ext : "." + ext;
                    map.TryAdd(key.ToLowerInvariant(), entry.Platform);
                }
            }

            return _romExtensionMap = map;
        }
    }

    /// <summary>
    /// Scans <paramref name="project"/> for binary items whose extension maps to
    /// a platform in the embedded .whfmt catalog and builds a
    /// <see cref="GameRomHint"/> for each match.
    /// Region and title are derived from common ROM filename conventions
    /// (region codes in parentheses such as (J), (USA), etc.).
    /// </summary>
    private static IReadOnlyList<GameRomHint> DetectRomHints(IProject project)
    {
        var hints = new List<GameRomHint>();
        var map   = RomExtensionMap;

        foreach (var item in project.Items.Where(i => i.ItemType == ProjectItemType.Binary))
        {
            var ext = Path.GetExtension(item.AbsolutePath).ToLowerInvariant();
            if (!map.TryGetValue(ext, out var platform)) continue;

            var rawName         = Path.GetFileNameWithoutExtension(item.AbsolutePath);
            var (title, region) = ParseRomFileName(rawName);
            hints.Add(new GameRomHint(platform, region, title, item.Name));
        }

        return hints;
    }

    /// <summary>
    /// Extracts a cleaned game title and region from a ROM filename that may
    /// contain region / revision tags such as <c>(J)</c>, <c>(USA)</c>,
    /// <c>[!]</c>, <c>[T-Eng]</c> etc.
    /// </summary>
    private static (string Title, string Region) ParseRomFileName(string rawName)
    {
        var regionMatch = Regex.Match(rawName,
            @"\((?<r>J|U|E|W|JU|EU|JE|JUE|USA|Japan|Europe|World)\)",
            RegexOptions.IgnoreCase);

        var region = regionMatch.Success
            ? regionMatch.Groups["r"].Value.ToUpperInvariant() switch
            {
                "J" or "JAPAN"                         => "Japan",
                "U" or "USA"                           => "USA",
                "E" or "EUROPE"                        => "Europe",
                "W" or "WORLD" or "JUE" or "JU" or "EU" or "JE" => "World",
                var r                                  => r,
            }
            : string.Empty;

        // Strip all parenthetical/bracketed tags to get a clean title
        var title = Regex.Replace(rawName, @"\s*[\(\[][^\)\]]*[\)\]]", string.Empty).Trim();

        return (title, region);
    }

    private async void OnSEConvertTbl(object? sender, ProjectItemEventArgs e)
    {
        var hints = DetectRomHints(e.Project);
        var dlg   = new ConvertTblDialog(e.Item.AbsolutePath,
                                         hints.Count > 0 ? hints : null) { Owner = this };
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
        // If the file is already open in ANY tab (project or standalone, any editor),
        // activate that tab instead of creating a duplicate.
        var allItems = _layout.GetAllGroups().SelectMany(g => g.Items)
            .Concat(_layout.FloatingItems)
            .Concat(_layout.AutoHideItems);
        foreach (var di in allItems)
        {
            if (!di.Metadata.TryGetValue("FilePath", out var fp)) continue;
            if (!string.Equals(fp, filePath, StringComparison.OrdinalIgnoreCase)) continue;
            if (di.Owner is { } o) o.ActiveItem = di;
            DockHost.RebuildVisualTree();
            return;
        }

        // Pre-flight: verify file accessibility before creating a document tab.
        // If the file is locked or missing the error is routed to the Output panel
        // and no tab is created.
        if (!TryCheckFileAccess(filePath, out var accessError))
        {
            OutputLogger.Error(accessError);
            ShowOrCreatePanel("Output", "panel-output", DockDirection.Bottom);
            return;
        }

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
        NotifyFileOpened(filePath);
    }

    /// <summary>
    /// Verifies that <paramref name="filePath"/> exists and can be opened for reading
    /// before a document tab is created. Catches <see cref="IOException"/> and
    /// <see cref="UnauthorizedAccessException"/> to prevent creating a broken tab.
    /// </summary>
    private static bool TryCheckFileAccess(string filePath, out string errorMessage)
    {
        errorMessage = string.Empty;

        if (!File.Exists(filePath))
        {
            errorMessage = $"File not found: {filePath}";
            return false;
        }

        try
        {
            using var _ = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            return true;
        }
        catch (IOException ex)
        {
            errorMessage = $"Cannot open '{Path.GetFileName(filePath)}': {ex.Message}";
            return false;
        }
        catch (UnauthorizedAccessException ex)
        {
            errorMessage = $"Access denied '{Path.GetFileName(filePath)}': {ex.Message}";
            return false;
        }
    }

    private void OnSolutionExplorerItemSelected(object? sender, ProjectItemEventArgs e)
    {
        _propertiesPanel?.SetProvider(new ProjectItemPropertyProvider(e.Item));
    }

    private async void OnSolutionExplorerItemRenameRequested(object? sender, ProjectItemEventArgs e)
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

        // Release the FileStream before File.Move (FileProvider opens with FileShare.Read)
        await CloseHexStreamIfOpenAsync(e.Item);

        _ = _solutionManager.RenameItemAsync(e.Project, e.Item, newName);
        // Reopen + log are handled in OnProjectItemRenamed via the ItemRenamed event
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

    private void OnSEItemDeleteFromDiskRequested(object? sender, ProjectItemEventArgs e)
    {
        if (e.Project is null) return;

        var path = e.Item.AbsolutePath;
        if (!File.Exists(path)) return;

        var result = MessageBox.Show(
            $"Delete '{e.Item.Name}' and send it to the Recycle Bin?\n\nThis will also remove it from the project.",
            "Delete File",
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning);

        if (result != MessageBoxResult.Yes) return;

        // Close any open editor tab for this file (no dirty prompt — file is being deleted)
        var contentId = $"doc-proj-{e.Item.Id}";
        if (_layout.FindItemByContentId(contentId) is { } dockItem)
            CloseTab(dockItem, promptIfDirty: false);

        // Remove from project model
        _ = _solutionManager.RemoveItemAsync(e.Project, e.Item, deleteFromDisk: false);

        // Send to Recycle Bin (recoverable delete)
        Microsoft.VisualBasic.FileIO.FileSystem.DeleteFile(
            path,
            Microsoft.VisualBasic.FileIO.UIOption.OnlyErrorDialogs,
            Microsoft.VisualBasic.FileIO.RecycleOption.SendToRecycleBin);

        OutputLogger.Info($"Deleted '{e.Item.Name}' from '{e.Project.Name}' → Recycle Bin");
    }

    private async void OnSolutionExplorerItemMoveRequested(object? sender, ItemMoveRequestedEventArgs e)
    {
        // Release the FileStream before File.Move (FileProvider opens with FileShare.Read which blocks moves)
        var openHex = await CloseHexStreamIfOpenAsync(e.Item);

        await _solutionManager.MoveItemToFolderAsync(e.Project, e.Item, e.TargetFolderId);

        // Reopen from the new path (updated by MoveItemToFolderAsync)
        openHex?.OpenFile(e.Item.AbsolutePath);

        var destination = e.TargetFolderId is null
            ? $"project root of '{e.Project.Name}'"
            : $"folder '{e.TargetFolderId}' in '{e.Project.Name}'";
        OutputLogger.Info($"Moved '{e.Item.Name}' → {destination}");
    }

    /// <summary>
    /// If <paramref name="item"/> is currently open in a <see cref="HexEditorControl"/> tab,
    /// saves pending changes (if any) and closes the file stream so callers can safely
    /// perform a <c>File.Move</c> or <c>File.Delete</c>.
    /// </summary>
    /// <returns>The <see cref="HexEditorControl"/> whose stream was closed, or <c>null</c>.</returns>
    private async Task<HexEditorControl?> CloseHexStreamIfOpenAsync(IProjectItem item)
    {
        var contentId = $"doc-proj-{item.Id}";
        if (!_contentCache.TryGetValue(contentId, out var cached) || cached is not HexEditorControl hex)
            return null;

        if ((hex as IDocumentEditor)?.IsDirty == true)
            await (hex as IDocumentEditor)!.SaveAsync();

        hex.Close();
        return hex;
    }

    // --- Folder management ----------------------------------------------

    private void OnSEFolderCreateRequested(object? sender, FolderCreateRequestedEventArgs e)
        => _ = CreateFolderAndBeginRenameAsync(e.Project, e.ParentFolderId, e.CreatePhysical);

    private async Task CreateFolderAndBeginRenameAsync(IProject project, string? parentId, bool physical)
    {
        var baseName = physical ? "New Folder" : "New Solution Folder";
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

        // Reopen from the new path (stream was closed before rename to release the file lock)
        if (_contentCache.TryGetValue(contentId, out var ctrl) && ctrl is HexEditorControl hex)
            hex.OpenFile(e.Item.AbsolutePath);

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

    // --- Solution Folder management -----------------------------------------

    private void OnSESolutionFolderCreateRequested(object? sender, SolutionFolderCreateRequestedEventArgs e)
        => _ = CreateSolutionFolderAndBeginRenameAsync(e.Solution, e.ParentFolderId);

    private async Task CreateSolutionFolderAndBeginRenameAsync(ISolution solution, string? parentId)
    {
        var folder = await _solutionManager.CreateSolutionFolderAsync(solution, "New Solution Folder", parentId);
        _solutionExplorerPanel?.SetSolution(_solutionManager.CurrentSolution);
        _solutionExplorerPanel?.BeginSolutionFolderRename(folder);
        OutputLogger.Info($"Solution folder created in '{solution.Name}'");
    }

    private void OnSESolutionFolderRenameRequested(object? sender, SolutionFolderRenameRequestedEventArgs e)
    {
        _ = _solutionManager.RenameSolutionFolderAsync(e.Solution, e.Folder, e.NewName);
        _solutionExplorerPanel?.SetSolution(_solutionManager.CurrentSolution);
        OutputLogger.Info($"Solution folder renamed to '{e.NewName}'");
    }

    private void OnSESolutionFolderDeleteRequested(object? sender, SolutionFolderDeleteRequestedEventArgs e)
    {
        var result = MessageBox.Show(
            $"Remove solution folder '{e.Folder.Name}'?\n" +
            "Projects inside will be kept at the solution root.",
            "Remove solution folder",
            MessageBoxButton.YesNo,
            MessageBoxImage.Question);

        if (result != MessageBoxResult.Yes) return;

        _ = _solutionManager.DeleteSolutionFolderAsync(e.Solution, e.Folder);
        _solutionExplorerPanel?.SetSolution(_solutionManager.CurrentSolution);
        OutputLogger.Info($"Solution folder '{e.Folder.Name}' removed");
    }

    private void OnSEProjectMoveRequested(object? sender, ProjectMovedEventArgs e)
    {
        _ = MoveProjectToSolutionFolderAsync(e.Solution, e.Project, e.TargetFolderId);
    }

    private async Task MoveProjectToSolutionFolderAsync(ISolution solution, IProject project, string? targetFolderId)
    {
        await _solutionManager.MoveProjectToSolutionFolderAsync(solution, project, targetFolderId);
        _solutionExplorerPanel?.SetSolution(_solutionManager.CurrentSolution);
        OutputLogger.Info($"Project '{project.Name}' moved to {(targetFolderId is null ? "solution root" : "solution folder")}");
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

    private void OnSESolutionRenameRequested(object? sender, SolutionRenameRequestedEventArgs e)
        => _ = _solutionManager.RenameSolutionAsync(e.Solution, e.NewName);

    private void OnSEProjectRenameRequested(object? sender, ProjectRenameRequestedEventArgs e)
        => _ = _solutionManager.RenameProjectAsync(e.Project, e.NewName);

    private void OnSECloseSolutionRequested(object? sender, EventArgs e)
        => _ = CloseSolutionAsync();

    private void OnSESaveAll(object? sender, EventArgs e)
    {
        if (_solutionManager.CurrentSolution is { } sol)
            _ = _solutionManager.SaveSolutionAsync(sol);
        ActiveDocumentEditor?.SaveCommand?.Execute(null);
        OutputLogger.Info("Save All executed.");
    }

    private void OnSEOpenWith(object? sender, OpenWithRequestedEventArgs e)
    {
        // Open the item forcing the HexEditor, regardless of the editor registry.
        // Uses a distinct contentId so it can coexist with the registry-based tab.
        var hexContentId = $"doc-proj-hex-{e.Item.Id}";
        if (_layout.FindItemByContentId(hexContentId) is { } existing)
        {
            if (existing.Owner is { } owner)
                owner.ActiveItem = existing;
            DockHost.RebuildVisualTree();
            return;
        }

        var dockItem = new DockItem
        {
            Title     = e.Item.Name,
            ContentId = hexContentId,
            Metadata  =
            {
                ["FilePath"]       = e.Item.AbsolutePath,
                ["ProjectId"]      = e.Project.Id,
                ["ItemId"]         = e.Item.Id,
                ["ForceHexEditor"] = "true",
            },
        };
        _engine.Dock(dockItem, _layout.MainDocumentHost, DockDirection.Center);
        DockHost.RebuildVisualTree();
    }

    private void OnSEOpenWithSpecific(object? sender, OpenWithSpecificEditorEventArgs e)
        => OpenProjectItemWithEditor(e.Item, e.Project, e.FactoryId);

    /// <summary>
    /// Opens <paramref name="item"/> in the editor identified by <paramref name="factoryId"/>
    /// (or the Hex Editor fallback when <paramref name="factoryId"/> is <see langword="null"/>).
    /// A distinct <c>ContentId</c> is used so the new tab can coexist with an already-open tab
    /// for the same file.
    /// </summary>
    private void OpenProjectItemWithEditor(IProjectItem item, IProject project, string? factoryId)
    {
        var suffix    = factoryId is null ? "hex" : factoryId;
        var contentId = $"doc-proj-{suffix}-{item.Id}";

        if (_layout.FindItemByContentId(contentId) is { } existing)
        {
            if (existing.Owner is { } owner) owner.ActiveItem = existing;
            DockHost.RebuildVisualTree();
            return;
        }

        // Also check the bare default tab (doc-proj-{itemId}) in case it was opened with
        // the same editor as the one being requested via "View in".
        var bareContentId = $"doc-proj-{item.Id}";
        if (_layout.FindItemByContentId(bareContentId) is { } bareItem)
        {
            bareItem.Metadata.TryGetValue("ActiveEditorId", out var activeEditorId);
            if (activeEditorId == suffix || (activeEditorId is null && suffix == "hex-editor"))
            {
                if (bareItem.Owner is { } bareOwner) bareOwner.ActiveItem = bareItem;
                DockHost.RebuildVisualTree();
                return;
            }
        }

        var meta = new System.Collections.Generic.Dictionary<string, string>
        {
            ["FilePath"]  = item.AbsolutePath,
            ["ProjectId"] = project.Id,
            ["ItemId"]    = item.Id,
        };

        if (factoryId is null)
            meta["ForceHexEditor"] = "true";
        else
            meta["ForceEditorId"] = factoryId;

        var dockItem = new DockItem { Title = item.Name, ContentId = contentId };
        foreach (var (k, v) in meta) dockItem.Metadata[k] = v;

        _engine.Dock(dockItem, _layout.MainDocumentHost, DockDirection.Center);
        DockHost.RebuildVisualTree();
    }

    /// <summary>
    /// Called by <see cref="DocumentInfoBar"/> when the user clicks an action button.
    /// Opens the source file in the requested editor (new parallel tab).
    /// </summary>
    private void OnInfoBarOpenWith(object? sender, OpenWithEditorRequestedEventArgs e)
    {
        // The bar embeds the sourceContentId of the DockItem that hosts the viewer.
        // Derive item / project from that tab's metadata so we can open a companion tab.
        if (_layout.FindItemByContentId(e.SourceContentId) is not { } sourceDockItem) return;

        sourceDockItem.Metadata.TryGetValue("ProjectId", out var projectId);
        sourceDockItem.Metadata.TryGetValue("ItemId",    out var itemId);

        if (projectId is not null && itemId is not null)
        {
            var proj = _solutionManager.CurrentSolution?.Projects.FirstOrDefault(p => p.Id == projectId);
            var item = proj?.FindItem(itemId);
            if (proj is not null && item is not null)
            {
                OpenProjectItemWithEditor(item, proj, e.FactoryId);
                return;
            }
        }

        // Fallback: standalone file (doc-file-*) — open without project context
        OpenStandaloneFileWithEditor(e.FilePath, e.FactoryId);
    }

    /// <summary>
    /// Opens a standalone (non-project) file in the specified editor.
    /// </summary>
    private void OpenStandaloneFileWithEditor(string filePath, string? factoryId)
    {
        // Activate existing tab if the same file is already open in the requested editor.
        var allItems = _layout.GetAllGroups().SelectMany(g => g.Items)
            .Concat(_layout.FloatingItems)
            .Concat(_layout.AutoHideItems);

        foreach (var di in allItems)
        {
            if (!di.Metadata.TryGetValue("FilePath", out var fp)) continue;
            if (!string.Equals(fp, filePath, StringComparison.OrdinalIgnoreCase)) continue;

            di.Metadata.TryGetValue("ForceEditorId",  out var feid);
            di.Metadata.TryGetValue("ActiveEditorId", out var aeid);
            var effectiveId = feid ?? aeid;

            var matches = factoryId is null
                ? (di.Metadata.TryGetValue("ForceHexEditor", out var fh) && fh == "true")
                : effectiveId == factoryId;

            if (!matches) continue;

            if (di.Owner is { } o) o.ActiveItem = di;
            DockHost.RebuildVisualTree();
            return;
        }

        _documentCounter++;
        var suffix    = factoryId is null ? "hex" : factoryId;
        var contentId = $"doc-file-{suffix}-{_documentCounter}";

        var dockItem = new DockItem
        {
            Title     = System.IO.Path.GetFileName(filePath),
            ContentId = contentId,
            Metadata  =
            {
                ["FilePath"] = filePath,
            },
        };
        if (factoryId is null)
            dockItem.Metadata["ForceHexEditor"] = "true";
        else
            dockItem.Metadata["ForceEditorId"] = factoryId;

        _engine.Dock(dockItem, _layout.MainDocumentHost, DockDirection.Center);
        DockHost.RebuildVisualTree();
    }

    private async void OnSEPhysicalFileInclude(object? sender, PhysicalFileIncludeRequestedEventArgs e)
    {
        await _solutionManager.AddItemAsync(e.Project, e.PhysicalPath, e.TargetFolderId);
        _solutionExplorerPanel?.SetSolution(_solutionManager.CurrentSolution);
    }

    private async void OnSEImportExternalFile(object? sender, ImportExternalFileRequestedEventArgs e)
    {
        var projDir  = Path.GetDirectoryName(e.Project.ProjectFilePath) ?? string.Empty;
        var fileName = Path.GetFileName(e.Item.AbsolutePath);
        var result   = MessageBox.Show(
            $"Copy \"{fileName}\" into the project directory?\n\n{projDir}\n\nThe original file will not be deleted.",
            "Import into Project",
            MessageBoxButton.YesNo,
            MessageBoxImage.Question);

        if (result != MessageBoxResult.Yes) return;

        await _solutionManager.ImportExternalItemAsync(e.Project, e.Item);
        _solutionExplorerPanel?.SetSolution(_solutionManager.CurrentSolution);
    }

    private void OnSEPropertiesRequested(object? sender, NodePropertiesEventArgs e)
    {
        if (e.Item is null && e.Project is not null)
        {
            // Project node → VS-Like Project Properties document tab
            OpenProjectPropertiesDocument(e.Project);
        }
        else if (e.Item is not null)
        {
            // File node → show/focus the Properties panel
            ShowOrCreatePanel("Properties", "panel-properties", DockDirection.Right);
        }
    }

    // --- Solution Explorer — changeset actions --------------------------

    private async void OnSEWriteToDisk(object? sender, ProjectItemEventArgs e)
    {
        if (e.Item is null || e.Project is null) return;
        if (!ChangesetService.Instance.HasChangeset(e.Item)) return;

        try
        {
            await _solutionManager.WriteItemToDiskAsync(e.Project, e.Item);

            // Reload the editor if open
            var contentId = $"doc-proj-{e.Item.Id}";
            if (_contentCache.TryGetValue(contentId, out var ctrl) && ctrl is HexEditorControl hex)
                hex.OpenFile(e.Item.AbsolutePath);

            OutputLogger.Info($"Write to Disk: '{e.Item.Name}' written and changeset removed.");
        }
        catch (Exception ex)
        {
            OutputLogger.Error($"Write to Disk failed for '{e.Item.Name}': {ex.Message}");
        }
    }

    private async void OnSEDiscardChangeset(object? sender, ProjectItemEventArgs e)
    {
        if (e.Item is null || e.Project is null) return;
        try
        {
            await _solutionManager.DiscardChangesetAsync(e.Project, e.Item);

            // If the file is currently open, close and reopen to restore the original state
            var contentId = $"doc-proj-{e.Item.Id}";
            if (_layout.FindItemByContentId(contentId) is { } dockItem)
            {
                CloseTab(dockItem, promptIfDirty: false);
                OpenProjectItem(e.Item, e.Project);
            }
            OutputLogger.Info($"Discarded changeset for '{e.Item.Name}'.");
        }
        catch (Exception ex)
        {
            OutputLogger.Error($"Discard changeset failed for '{e.Item.Name}': {ex.Message}");
        }
    }

    /// <summary>
    /// Opens a source file in the text editor and scrolls to the requested 1-based line number.
    /// Triggered when the user double-clicks a type or member node in the Solution Explorer.
    /// </summary>
    private void OnSESourceLineNavigationRequested(object? sender, SourceLineNavigationEventArgs e)
    {
        if (string.IsNullOrEmpty(e.AbsolutePath) || !File.Exists(e.AbsolutePath)) return;

        // Look for an already-open TextEditor tab for this file (match by file name via Title).
        var fileName   = Path.GetFileName(e.AbsolutePath);
        var existingKv = _contentCache.FirstOrDefault(kv =>
            kv.Value is WpfHexEditor.Editor.TextEditor.Controls.TextEditor tc &&
            string.Equals(tc.Title.TrimEnd('*', ' '), fileName,
                System.StringComparison.OrdinalIgnoreCase));

        WpfHexEditor.Editor.TextEditor.Controls.TextEditor? textEditor;

        if (existingKv.Key != null)
        {
            // Activate the existing tab.
            textEditor = existingKv.Value as WpfHexEditor.Editor.TextEditor.Controls.TextEditor;
            var existingItem = _layout.FindItemByContentId(existingKv.Key);
            if (existingItem?.Owner != null) existingItem.Owner.ActiveItem = existingItem;
        }
        else
        {
            // Open the file in a new TextEditor tab.
            _documentCounter++;
            var contentId  = $"doc-text-src-{_documentCounter}";
            var factory    = new WpfHexEditor.Editor.TextEditor.TextEditorFactory();
            textEditor     = factory.Create() as WpfHexEditor.Editor.TextEditor.Controls.TextEditor;
            if (textEditor == null) return;

            textEditor.OutputMessage += OnEditorOutputMessage;
            _ = textEditor.OpenAsync(e.AbsolutePath);

            StoreContent(contentId, textEditor);
            var dockItem = new DockItem
            {
                ContentId = contentId,
                Title     = Path.GetFileName(e.AbsolutePath),
                Metadata  = { ["FilePath"] = e.AbsolutePath }
            };
            _engine.Dock(dockItem, _layout.MainDocumentHost, DockDirection.Center);
            RegisterDocumentFromItem(dockItem, textEditor);
            ActiveDocumentEditor = textEditor;
        }

        // Scroll to line after layout is updated.
        if (e.LineNumber > 0 && textEditor != null)
        {
            var targetLine = e.LineNumber;
            var captured   = textEditor;
            Dispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.Loaded,
                () => captured.GoToLine(targetLine, 1));
        }
    }

    /// <summary>
    /// After a solution tree rebuild, refreshes the changeset child node under every item
    /// that currently has a .whchg companion file on disk.
    /// </summary>
    private void RefreshAllChangesetNodes()
    {
        if (_solutionExplorerPanel is null) return;
        foreach (var project in _solutionManager.CurrentSolution?.Projects ?? [])
        foreach (var item    in project.Items)
        {
            if (ChangesetService.Instance.HasChangeset(item))
                _solutionExplorerPanel.RefreshChangesetNode(item);
        }
    }

    private void OnChangesetFileChanged(string changesetPath, bool exists)
    {
        // Raised on a background thread — marshal to UI thread
        Dispatcher.BeginInvoke(() =>
        {
            if (_solutionExplorerPanel is null) return;

            // Find the project item whose .whchg companion matches
            var sourcePath = changesetPath.EndsWith(".whchg", StringComparison.OrdinalIgnoreCase)
                ? changesetPath[..^6]   // strip ".whchg"
                : null;
            if (sourcePath is null) return;

            foreach (var project in _solutionManager.CurrentSolution?.Projects ?? [])
            foreach (var item    in project.Items)
            {
                if (string.Equals(item.AbsolutePath, sourcePath, StringComparison.OrdinalIgnoreCase))
                {
                    _solutionExplorerPanel.RefreshChangesetNode(item);
                    return;
                }
            }
        });
    }

    // --- Active document tracking ---------------------------------------

    private void OnActiveDocumentChanged(DockItem item)
    {
        // Notify plugin focus context of the document change
        UpdatePluginFocusContext(item);

        // Panel tabs: exit without altering status bar or toolbar contributions.
        // The status bar and toolbar always reflect the last active document editor,
        // even when a panel tab is focused (same behaviour as Visual Studio).
        // Exception: non-editor documents (e.g. Plugin Manager) do NOT persist their
        // toolbar when a panel gains focus — clear it so the pod disappears.
        if (item.ContentId.StartsWith("panel-"))
        {
            if (ActiveDocumentEditor is null)
                ActiveToolbarContributor = null;
            return;
        }

        // Content not yet materialized (lazy tab never selected): clear and exit.
        if (!_contentCache.TryGetValue(item.ContentId, out var content))
        {
            ActiveStatusBarContributor = _defaultStatusBarContributor;
            return;
        }

        // Unwrap the InfoBar Grid wrapper if present — the actual editor is in Tag
        content = UnwrapEditor(content);

        var hex = content as HexEditorControl;

        // Only switch the connected hex editor when a hex editor tab becomes active.
        // When a non-hex editor (JSON, TBL…) becomes active in a split pane, keep the
        // last hex editor connected so the Parsed Fields panel stays populated.
        if (hex != null && hex != _connectedHexEditor)
        {
            // Switching to a different hex editor — reconnect panels.
            if (_connectedHexEditor is IDiagnosticSource prevDiag)
                _errorPanel?.RemoveSource(prevDiag);
            if (_connectedHexEditor is not null)
                _connectedHexEditor.ByteDistributionPanel = null;

            _connectedHexEditor = hex;
            if (_connectedHexEditor is IDiagnosticSource newDiag)
                EnsureErrorPanelInstance().AddSource(newDiag);

            // Refresh archive-structure panel for ZIP files (analysis panels handled by plugins)
            _ = RefreshAnalysisPanelsAsync(_connectedHexEditor);
        }

        ActiveDocumentEditor       = content as IDocumentEditor;
        ActiveHexEditor            = hex;
        ActiveStatusBarContributor = content as IStatusBarContributor ?? _defaultStatusBarContributor;
        ActiveToolbarContributor   = content as IEditorToolbarContributor;
        SyncTblDropdownToActiveEditor();

        // Clear the transient refresh-time text whenever the active editor is not a HexEditor,
        // or when there is no active editor at all.  For HexEditor, RefreshDocumentStatus()
        // fires StatusMessage → OnEditorStatusMessage which sets refresh time via StatusBarContributor.
        if (hex == null)
        {
            // RefreshText.Text = ""; // Removed - now handled via StatusBarContributor
        }
        else
            hex.RefreshDocumentStatus();

        // Refresh status bar item values for all contributor types (generic, not hex-only).
        ActiveStatusBarContributor.RefreshStatusBarItems();

        // Sync Solution Explorer highlight — hex FileName takes priority, then Metadata["FilePath"]
        var syncPath = hex?.FileName;
        if (string.IsNullOrEmpty(syncPath) && item.Metadata.TryGetValue("FilePath", out var fp))
            syncPath = fp;
        if (!string.IsNullOrEmpty(syncPath))
            _solutionExplorerPanel?.SyncWithFile(syncPath);

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

        // Sync active document in DocumentManager so IsActive / ActiveDocument are accurate.
        SyncActiveDocument(item.ContentId);
    }

    // --- Split-pane focus tracking ------------------------------------

    /// <summary>
    /// Lightweight status bar sync triggered when keyboard focus enters a document
    /// editor content area in a split-pane layout, bypassing tab selection events.
    /// Does NOT reconnect tool panels or refresh analysis results — those heavy
    /// operations are reserved for the full <see cref="OnActiveDocumentChanged"/> path
    /// (explicit tab click).
    /// </summary>
    private void SyncStatusBarToFocusedEditor(string contentId)
    {
        if (!_contentCache.TryGetValue(contentId, out var content)) return;
        var unwrapped   = content; // _contentCache already stores the unwrapped editor
        var contributor = unwrapped as IStatusBarContributor ?? _defaultStatusBarContributor;

        // Skip if this editor is already reflected in the status bar — avoids
        // a redundant refresh on the frame immediately after a normal tab switch
        // (which already went through OnActiveDocumentChanged).
        if (ReferenceEquals(contributor, ActiveStatusBarContributor) &&
            ReferenceEquals(unwrapped as IDocumentEditor, ActiveDocumentEditor))
            return;

        // Only sync for genuine document editors; non-editor viewers keep the current state.
        if (unwrapped is not IDocumentEditor docEditor) return;

        ActiveDocumentEditor       = docEditor;
        ActiveStatusBarContributor = contributor;
        contributor.RefreshStatusBarItems();
        SyncActiveDocument(contentId);

        var focusedHex = unwrapped as HexEditorControl;
        if (focusedHex != null)
            focusedHex.RefreshDocumentStatus();

        // Sync toolbar contributions for the newly focused editor — mirrors the assignments
        // in OnActiveDocumentChanged. Without this, ActiveHexEditor and ActiveToolbarContributor
        // are stale when keyboard focus moves between split panes without a tab click
        // (e.g., hex pane → TBL pane keeps the TBL character-table pod visible).
        ActiveHexEditor          = focusedHex;
        ActiveToolbarContributor = unwrapped as IEditorToolbarContributor;
        SyncTblDropdownToActiveEditor();
    }

    // --- IDocumentEditor event handlers --------------------------------

    private void OnEditorTitleChanged(object? sender, string newTitle)
    {
        var senderEl = sender as UIElement;
        // Search both direct matches and InfoBar-wrapped editors (Tag holds the actual editor)
        var contentId = _contentCache
            .FirstOrDefault(kv =>
                ReferenceEquals(kv.Value, senderEl) ||
                (kv.Value is Grid { Tag: UIElement inner } && ReferenceEquals(inner, senderEl))).Key;
        if (contentId != null)
        {
            var dockItem = _layout.FindItemByContentId(contentId);
            if (dockItem != null) dockItem.Title = newTitle;
        }
    }

    private void OnEditorModifiedChanged(object? sender, EventArgs e)
        => CommandManager.InvalidateRequerySuggested();

    // --- Find / Replace handlers ----------------------------------------

    /// <summary>Ctrl+F — always opens the inline QuickSearchBar for the active editor.</summary>
    private void OnShowAdvancedSearch(object sender, RoutedEventArgs e)
    {
        if (ActiveDocumentEditor is ISearchTarget t)
            ShowSearchBarForTarget(t);
    }

    /// <summary>Ctrl+Shift+F — opens the 5-mode Advanced Search dialog (HexEditor only).</summary>
    private void OnShowAdvancedSearchDialog(object sender, RoutedEventArgs e)
    {
        if (ActiveHexEditor is { } hex)
            hex.ShowAdvancedSearchDialog(this);
    }

    private void OnFindNext(object sender, RoutedEventArgs e)
    {
        if (ActiveDocumentEditor is ISearchTarget t)
            ShowSearchBarForTarget(t);
    }

    private void OnFindPrevious(object sender, RoutedEventArgs e)
    {
        if (ActiveDocumentEditor is ISearchTarget t)
            ShowSearchBarForTarget(t);
    }

    /// <summary>
    /// Shows (or refocuses) the inline QuickSearchBar for the given <see cref="ISearchTarget"/>.
    /// HexEditor and TblEditor have their own embedded Canvas; CodeEditor uses the one injected
    /// by the App during content creation.
    /// </summary>
    private void ShowSearchBarForTarget(ISearchTarget target)
    {
        if (target is HexEditorControl hex)
        { hex.ShowQuickSearchBar(); return; }

        if (target is TblEditorControl tbl)
        { tbl.ShowSearch(); return; }

        if (target is CodeEditorControl json && _codeEditorBars.TryGetValue(json, out var bar))
        {
            if (bar.Visibility == Visibility.Visible)
            { bar.FocusSearchInput(); return; }

            bar.BindToTarget(json);
            bar.Visibility = Visibility.Visible;
            if (bar.Parent is Canvas c)
                bar.EnsureDefaultPosition(c);
            bar.FocusSearchInput();
        }
    }

    private void OnEditorStatusMessage(object? sender, string message)
    {
        var parts = message.Split(new[] { "  |  " }, StringSplitOptions.None);

        // Refresh time is now handled via StatusBarContributor, not via status message
        // var refreshPart = parts.FirstOrDefault(
        //     p => p.TrimStart().StartsWith("Refresh:", StringComparison.OrdinalIgnoreCase));
        // RefreshText.Text = refreshPart?.Trim() ?? "";

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

    // --- Long-running operation handlers (per active document) ----------

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

            if (e.WasCancelled)
            {
                // RefreshText.Text = "Operation cancelled"; // Removed - status handled differently
                return;
            }

            if (!e.Success && !string.IsNullOrEmpty(e.ErrorMessage))
            {
                // Log as Error (red) and reveal the Output panel.
                OutputLogger.Error(e.ErrorMessage);
                ShowOrCreatePanel("Output", "panel-output", DockDirection.Bottom);
                // RefreshText.Text = $"Error: {e.ErrorMessage}"; // Removed - errors shown in Output panel

                // Silently close the tab whose editor raised the failure.
                if (e.CloseTabOnFailure)
                {
                    var contentId = _contentCache
                        .FirstOrDefault(kv => ReferenceEquals(kv.Value, sender)).Key;
                    if (contentId is not null &&
                        _layout.FindItemByContentId(contentId) is { } dockItem)
                        CloseTab(dockItem, promptIfDirty: false);
                }
            }
        });
    }

    private void OnProgressCancel(object sender, RoutedEventArgs e)
        => ActiveDocumentEditor?.CancelOperation();

    private void StatusBarItem_MouseLeftButtonDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        if (sender is Border border &&
            border.ContextMenu is { } cm &&
            border.DataContext is StatusBarItem { } si &&
            si.Choices.Count > 0)
        {
            cm.DataContext     = border.DataContext;
            cm.PlacementTarget = border;
            cm.Placement       = System.Windows.Controls.Primitives.PlacementMode.Top;
            cm.IsOpen          = true;
            e.Handled = true;
        }
    }

    // --- Tab / panel management ----------------------------------------

    /// <summary>Returns <c>true</c> when <paramref name="item"/> is a project document.
    /// Project items always use the Tracked (twin-file) strategy — the SaveChanges dialog
    /// is never shown for binary files that belong to a project.</summary>
    private static bool IsTrackedProjectItem(DockItem item) =>
        item.ContentId.StartsWith("doc-proj-") &&
        item.Metadata.ContainsKey("ItemId") &&
        item.Metadata.ContainsKey("ProjectId");

    /// <summary>Auto-serializes a dirty tracked project item to its <c>.whchg</c> companion
    /// file without showing a dialog.  On success clears the editor dirty flag and refreshes
    /// the Solution Explorer changeset node.</summary>
    private async Task AutoSerializeTrackedItemAsync(DockItem item)
    {
        if (!_contentCache.TryGetValue(item.ContentId, out var ctrl)) return;
        if (ctrl is not IEditorPersistable persistable) return;

        var snapshot = persistable.GetChangesetSnapshot();
        if (!snapshot.HasEdits) return;

        var proj = _solutionManager.CurrentSolution?.Projects
            .FirstOrDefault(p => p.Id == item.Metadata["ProjectId"]);
        var it = proj?.FindItem(item.Metadata["ItemId"]);
        if (it is null) return;

        await ChangesetService.Instance.WriteChangesetAsync(it, snapshot);

        // Mark the editor as clean after the tracked save
        persistable.MarkChangesetSaved();

        // Refresh the .whchg child node in Solution Explorer
        Dispatcher.BeginInvoke(() => _solutionExplorerPanel?.RefreshChangesetNode(it));
    }

    private void OnTabCloseRequested(DockItem item) => CloseTab(item, promptIfDirty: true);

    /// <summary>Core close logic. Set <paramref name="promptIfDirty"/> to false when
    /// the dirty-check has already been handled by a batch dialog.</summary>
    private void CloseTab(DockItem item, bool promptIfDirty)
    {
        if (_isLocked) return;

        // Dirty-close: in Tracked mode auto-serialize silently; in Direct mode show dialog
        if (promptIfDirty &&
            _contentCache.TryGetValue(item.ContentId, out var dirtyCtrl) &&
            dirtyCtrl is IDocumentEditor dirtyEditor && dirtyEditor.IsDirty)
        {
            if (IsTrackedItemWithChangeset(item))
            {
                // Auto-serialize to .whchg — no dialog
                _ = AutoSerializeTrackedItemAsync(item);
            }
            else
            {
                // Direct mode or changeset disabled — standard save dialog
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
        }

        if (_contentCache.TryGetValue(item.ContentId, out var ctrl))
        {
            if (ctrl is HexEditorControl hex)
            {
                if (ReferenceEquals(hex, _connectedHexEditor))
                    _connectedHexEditor = null;
                if (ReferenceEquals(hex, ActiveHexEditor))
                {
                    ActiveDocumentEditor = null;
                    ActiveHexEditor      = null;
                    // RefreshText.Text  = ""; // Removed - handled via StatusBarContributor
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
            UnregisterDocument(item.ContentId);
            _contentCache.Remove(item.ContentId);
            _displayContent.Remove(item.ContentId);   // must clear both caches so reopen gets a fresh editor
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

    // --- Menu: File — New ----------------------------------------------

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
        var suggestedDir = _solutionManager.CurrentSolution is not null
            ? Path.GetDirectoryName(_solutionManager.CurrentSolution.FilePath) ?? ""
            : null;

        var dlg = new NewProjectDialog(suggestedDir) { Owner = this };
        if (dlg.ShowDialog() != true) return;

        // Self-contained templates (VS .sln/.csproj) bypass the .whproj creation flow.
        if (dlg.SelectedTemplate is ISelfContainedProjectTemplate selfContained)
        {
            _ = CreateVsSolutionFromTemplateAsync(selfContained, dlg.ProjectDirectory, dlg.ProjectName);
            return;
        }

        // WH-native flow (unchanged).
        if (_solutionManager.CurrentSolution is null)
            _ = CreateSolutionAndProjectAsync(dlg.ProjectDirectory, dlg.ProjectName, dlg.SelectedTemplate);
        else
            _ = CreateProjectAsync(_solutionManager.CurrentSolution, dlg.ProjectDirectory, dlg.ProjectName, dlg.SelectedTemplate);
    }

    private async Task CreateVsSolutionFromTemplateAsync(
        ISelfContainedProjectTemplate template, string parentDir, string name)
    {
        try
        {
            var currentSlnPath = _solutionManager.CurrentSolution?.FilePath;
            var isVsSolution   = currentSlnPath?.EndsWith(".sln", StringComparison.OrdinalIgnoreCase) == true;

            string slnPath;
            if (isVsSolution)
            {
                // Add the new project into the already-open VS solution.
                var slnDir = Path.GetDirectoryName(currentSlnPath!) ?? parentDir;
                slnPath = await template.AddToSolutionAsync(currentSlnPath!, slnDir, name);
                OutputLogger.Info($".NET project added to solution: {currentSlnPath}");
            }
            else
            {
                // No VS solution open — create a brand-new .sln alongside the project.
                slnPath = await template.CreateAsync(parentDir, name);
                OutputLogger.Info($".NET solution scaffolded: {slnPath}");
            }

            await OpenSolutionAsync(slnPath);
        }
        catch (Exception ex)
        {
            OutputLogger.Error($"Failed to scaffold .NET project: {ex.Message}");
            MessageBox.Show($"Failed to create project:\n{ex.Message}", "Error",
                MessageBoxButton.OK, MessageBoxImage.Error);
        }
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
            // -- In-memory (Phase 12): file written on first Ctrl+S -----
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
            // -- Write to disk immediately ------------------------------
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

        // Auto-serialize Tracked project items silently (only when changeset is enabled for the editor)
        var tracked = dirty.Where(IsTrackedItemWithChangeset).ToList();
        foreach (var t in tracked)
        {
            _ = AutoSerializeTrackedItemAsync(t);
            dirty.Remove(t);
        }

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

    // --- Menu: File — Open ---------------------------------------------

    private void OnOpenSolutionOrProject(object sender, RoutedEventArgs e)
    {
        var dlg = new OpenFileDialog
        {
            Filter     = BuildSolutionFileFilter(),
            DefaultExt = ".whsln",
            Title      = "Open Solution or Project"
        };

        if (dlg.ShowDialog() != true) return;

        _ = OpenSolutionAsync(dlg.FileName);
    }

    /// <summary>
    /// Builds the OpenFileDialog filter dynamically from registered <see cref="ISolutionLoader"/> plugins.
    /// Falls back to native WH format if no loaders are registered.
    /// </summary>
    private string BuildSolutionFileFilter()
    {
        var loaders = _ideHostContext?.ExtensionRegistry.GetExtensions<ISolutionLoader>()
                      ?? [];

        if (loaders.Count == 0)
            return "Solution/Project Files (*.whsln;*.whproj)|*.whsln;*.whproj|" +
                   "Solution Files (*.whsln)|*.whsln|" +
                   "Project Files (*.whproj)|*.whproj";

        // Build "All supported" catch-all entry from all loader extensions.
        var allExts  = loaders.SelectMany(l => l.SupportedExtensions)
                              .Distinct(StringComparer.OrdinalIgnoreCase)
                              .OrderBy(e => e)
                              .ToList();
        var allGlobs = string.Join(";", allExts.Select(e => $"*.{e}"));
        var filter   = $"All Solution/Project Files ({allGlobs})|{allGlobs}";

        // One entry per loader.
        foreach (var loader in loaders)
        {
            var exts  = loader.SupportedExtensions;
            var globs = string.Join(";", exts.Select(e => $"*.{e}"));
            var names = string.Join("; ", exts.Select(e => $"*.{e}"));
            filter += $"|{loader.LoaderName} Files ({names})|{globs}";
        }

        filter += "|All Files (*.*)|*.*";
        return filter;
    }

    private async Task OpenSolutionAsync(string filePath)
    {
        // Prompt to save modified solution/project files + dirty editors before replacing the current solution.
        if (_solutionManager.CurrentSolution != null)
        {
            var allDirty = CollectAllDirtyItems(out var dirtyDocs);
            if (!await PromptAndSaveDirtyAsync(allDirty, dirtyDocs)) return;
        }

        try
        {
            // Route to the appropriate loader plugin if one is registered for this file type.
            var loaders = _ideHostContext?.ExtensionRegistry.GetExtensions<ISolutionLoader>() ?? [];
            var loader  = loaders.FirstOrDefault(l => l.CanLoad(filePath));

            if (loader != null && filePath.EndsWith(".whsln", StringComparison.OrdinalIgnoreCase) is false
                               && filePath.EndsWith(".whproj", StringComparison.OrdinalIgnoreCase) is false)
            {
                // External format (e.g. .sln, .csproj) — use plugin loader, then inject into SolutionManager.
                var solution = await loader.LoadAsync(filePath);
                await _solutionManager.LoadExternalSolutionAsync(solution, filePath);
            }
            else
            {
                await _solutionManager.OpenSolutionAsync(filePath);
            }

            OutputLogger.Info($"Solution opened: {filePath}");
            EnsureSolutionExplorerVisible();

            // Restore SE collapse state from .whsln.user sidecar (silently skipped if absent).
            if (_solutionExplorerPanel != null &&
                AppSettingsService.Instance.Current.SolutionExplorer.PersistCollapseState)
            {
                var keys = await SolutionUserSerializer.ReadExpandedKeysAsync(filePath);
                if (keys is { Count: > 0 })
                    _solutionExplorerPanel.ApplyExpandedNodeKeys(keys);
            }
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

        OpenFileDirectly(dlg.FileName);
        PopulateRecentMenus();
        OutputLogger.Info($"Open file: {dlg.FileName}");
    }

    // --- Toolbar dropdown helper ---------------------------------------
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

    // --- Menu: File — Close --------------------------------------------

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
            var allDirty = CollectAllDirtyItems(out var dirtyDocs);
            if (!await PromptAndSaveDirtyAsync(allDirty, dirtyDocs)) return false;
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

    // --- Solution dirty-save helpers -----------------------------------

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
    /// Returns combined dirty items: solution/project files + open dirty document editors.
    /// Also outputs the list of dirty DockItems needed to save editors afterwards.
    /// </summary>
    private List<(string ContentId, string Title)> CollectAllDirtyItems(out List<DockItem> dirtyDocs)
    {
        var allDirty = CollectDirtySolutionItems();

        // DocumentManager.GetDirty() is O(n) over registered models — faster than
        // walking the full docking layout and re-querying the content cache.
        dirtyDocs = _documentManager.GetDirty()
            .Select(m => _layout.FindItemByContentId(m.ContentId))
            .Where(i => i is not null)
            .ToList()!;

        allDirty.AddRange(dirtyDocs.Select(i => (i.ContentId, i.Title.TrimEnd('*', ' '))));
        return allDirty;
    }

    /// <summary>
    /// Shows the SaveChangesDialog for all dirty items (solution/project + open editors).
    /// Returns <c>false</c> if the user cancelled; <c>true</c> if Save or Don't Save was chosen.
    /// </summary>
    private async Task<bool> PromptAndSaveDirtyAsync(
        List<(string ContentId, string Title)> allDirty,
        List<DockItem> dirtyDocs)
    {
        // Auto-serialize Tracked project items silently — no dialog needed
        // (only when changeset is enabled for the editor type)
        var trackedItems = dirtyDocs.Where(IsTrackedItemWithChangeset).ToList();
        foreach (var t in trackedItems)
        {
            await AutoSerializeTrackedItemAsync(t);
            dirtyDocs.Remove(t);
            allDirty.RemoveAll(x => x.ContentId == t.ContentId);
        }

        if (allDirty.Count == 0) return true;

        var dlg = new Dialogs.SaveChangesDialog { DirtyItems = allDirty, Owner = this };
        if (dlg.ShowDialog() != true || dlg.Choice == Dialogs.SaveChangesChoice.Cancel) return false;

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
        return true;
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

    // --- Menu: File — Save ---------------------------------------------

    private void OnSaveAll(object sender, RoutedEventArgs e)
    {
        if (_solutionManager.CurrentSolution is { } saveSol)
            _ = _solutionManager.SaveSolutionAsync(saveSol);

        ActiveDocumentEditor?.SaveCommand?.Execute(null);
        OutputLogger.Info("Save All executed.");
    }

    /// <summary>
    /// Ctrl+S handler.  In Tracked mode, serialises edits to the .whchg companion file
    /// without touching the physical binary.  In Direct mode delegates to SaveCommand.
    /// </summary>
    private void OnSave(object sender, RoutedEventArgs e)
    {
        var settings = AppSettingsService.Instance.Current;

        // Try to resolve the active project item for Tracked mode.
        // ChangesetEnabled must also be true for the editor type — if not, fall through
        // to the direct-save branch so Ctrl+S behaves normally.
        if (settings.DefaultFileSaveMode == FileSaveMode.Tracked &&
            TryGetActiveProjectItem(out var saveProject, out var saveItem, out var saveContentId) &&
            _contentCache.TryGetValue(saveContentId, out var saveCtrl) &&
            saveCtrl is IEditorPersistable savePersistable &&
            IsChangesetEnabledForEditor(saveCtrl, settings))
        {
            var snapshot = savePersistable.GetChangesetSnapshot();
            if (!snapshot.HasEdits)
                return;   // nothing to write

            _ = ChangesetService.Instance.WriteChangesetAsync(saveItem!, snapshot)
                    .ContinueWith(_ =>
                    {
                        savePersistable.MarkChangesetSaved();
                        Dispatcher.BeginInvoke(() => _solutionExplorerPanel?.RefreshChangesetNode(saveItem!));
                    }, TaskScheduler.Default);
            OutputLogger.Info($"Saved '{saveItem!.Name}' (tracked).");
            return;
        }

        // Direct mode (or non-project tab) — respect per-editor standalone save preference.
        if (ActiveDocumentEditor is { } ed)
        {
            var sf = settings.StandaloneFileSave;

            bool directSave = ed switch
            {
                HexEditorControl                                         => sf.HexEditorDirectSave,
                CodeEditorControl                                        => sf.CodeEditorDirectSave,
                TblEditorControl                                         => sf.TblEditorDirectSave,
                WpfHexEditor.Editor.ImageViewer.Controls.ImageViewer    => sf.ImageViewerDirectSave,
                _                                                        => true
            };

            if (directSave)
            {
                ed.SaveCommand?.Execute(null);
                OutputLogger.Info($"Saved '{ed.Title.TrimEnd(' ', '*')}'.");
            }
            else
            {
                _ = ed.SaveAsAsync(string.Empty);
                OutputLogger.Info($"Save As invoked for '{ed.Title.TrimEnd(' ', '*')}'.");
            }
        }
    }

    /// <summary>
    /// Ctrl+Shift+W — applies the pending .whchg changeset to the physical file,
    /// then reloads the editor from the patched bytes.
    /// </summary>
    private async void OnWriteToDisk(object sender, RoutedEventArgs e)
    {
        if (!TryGetActiveProjectItem(out var wtdProject, out var wtdItem, out var wtdContentId))
            return;

        if (!ChangesetService.Instance.HasChangeset(wtdItem!))
        {
            OutputLogger.Info($"Write to Disk: no .whchg found for '{wtdItem!.Name}'.");
            return;
        }

        try
        {
            await _solutionManager.WriteItemToDiskAsync(wtdProject!, wtdItem);

            // Reload the editor from the updated file
            if (_contentCache.TryGetValue(wtdContentId, out var ctrl) &&
                ctrl is HexEditorControl hex)
            {
                hex.OpenFile(wtdItem.AbsolutePath);
            }
            OutputLogger.Info($"Write to Disk: '{wtdItem.Name}' written and changeset removed.");
        }
        catch (Exception ex)
        {
            OutputLogger.Error($"Write to Disk failed for '{wtdItem!.Name}': {ex.Message}");
        }
    }

    /// <summary>
    /// Resolves the active document's project + item from content cache + metadata.
    /// Returns false when the active editor is not a project item (doc-proj-*).
    /// </summary>
    private bool TryGetActiveProjectItem(
        out IProject?     project,
        out IProjectItem? item,
        out string        contentId)
    {
        project   = null;
        item      = null;
        contentId = string.Empty;

        if (ActiveDocumentEditor is null) return false;

        // Reverse-lookup the content ID for the active editor
        var found = _contentCache.FirstOrDefault(
            kv => ReferenceEquals(kv.Value, ActiveDocumentEditor as System.Windows.UIElement));
        if (found.Key is null) return false;
        contentId = found.Key;

        if (!contentId.StartsWith("doc-proj-")) return false;

        var dockItem = _engine.Layout.FindItemByContentId(contentId);
        if (dockItem is null) return false;
        if (!dockItem.Metadata.TryGetValue("ItemId",    out var itemId))    return false;
        if (!dockItem.Metadata.TryGetValue("ProjectId", out var projectId)) return false;

        project = _solutionManager.CurrentSolution?.Projects.FirstOrDefault(p => p.Id == projectId);
        item    = project?.FindItem(itemId);
        return item != null;
    }

    private void OnSettings(object sender, RoutedEventArgs e)
        => OpenSettingsAt(null, null);

    /// <summary>
    /// Opens the Options tab and optionally navigates to a specific page.
    /// </summary>
    private void OpenSettingsAt(string? category, string? pageName)
    {
        var existing = _layout.FindItemByContentId(OptionsContentId);
        if (existing is not null)
        {
            if (existing.Owner is { } owner) owner.ActiveItem = existing;
            DockHost.RebuildVisualTree();
        }
        else
        {
            var item = new DockItem { Title = "Options", ContentId = OptionsContentId };
            _engine.Dock(item, _layout.MainDocumentHost, DockDirection.Center);
            DockHost.RebuildVisualTree();
        }

        if (category is not null && pageName is not null &&
            _contentCache.TryGetValue(OptionsContentId, out var content) &&
            content is OptionsEditorControl ctrl)
        {
            ctrl.NavigateTo(category, pageName);
        }
    }

    private void OnOptionsSettingsChanged()
    {
        ApplyThemeFromSettings();

        // Re-apply per-editor-type settings (scroll speed, zoom, …) to all open editors
        // so that option changes take effect immediately without requiring a file re-open.
        foreach (var uiElement in _contentCache.Values)
        {
            if (uiElement is IDocumentEditor docEditor)
                ApplyEditorSettings(docEditor);
        }

        _autoSerializeTimer?.Stop();
        _autoSerializeTimer = null;
        InitAutoSerializeTimer();
    }

    private void OnOptionsEditJson(string filePath)
    {
        const string contentId = "doc-text-settings-json";
        var existing = _layout.FindItemByContentId(contentId);
        if (existing is not null)
        {
            if (existing.Owner is { } owner) owner.ActiveItem = existing;
            DockHost.RebuildVisualTree();
            return;
        }
        var factory = new WpfHexEditor.Editor.TextEditor.TextEditorFactory();
        if (factory.Create() is WpfHexEditor.Editor.TextEditor.Controls.TextEditor editor)
        {
            editor.OutputMessage += OnEditorOutputMessage;
            _ = editor.OpenAsync(filePath);
            StoreContent(contentId, editor);
            var dockItem = new DockItem { Title = "settings.json", ContentId = contentId };
            _engine.Dock(dockItem, _layout.MainDocumentHost, DockDirection.Center);
            DockHost.RebuildVisualTree();
        }
    }

    private void ApplyThemeFromSettings()
    {
        var stem = AppSettingsService.Instance.Current.ActiveThemeName;
        if (string.IsNullOrWhiteSpace(stem) || stem == _lastAppliedTheme) return;
        _lastAppliedTheme = stem;
        ApplyTheme($"{stem}.xaml", stem);
    }

    /// <summary>
    /// Applies per-editor-type user settings (scroll speed, etc.) to a newly created editor.
    /// Called once per editor instance from <see cref="CreateSmartFileEditorContent"/>.
    /// </summary>
    private static void ApplyEditorSettings(IDocumentEditor editor)
    {
        var settings = AppSettingsService.Instance.Current;

        switch (editor)
        {
            case WpfHexEditor.Editor.CodeEditor.Controls.CodeEditor ce:
                ce.MouseWheelSpeed = settings.CodeEditorDefaults.MouseWheelSpeed;
                ce.ZoomLevel       = settings.CodeEditorDefaults.DefaultZoom;
                break;
            case WpfHexEditor.Editor.TextEditor.Controls.TextEditor te:
                te.MouseWheelSpeed = settings.TextEditorDefaults.MouseWheelSpeed;
                te.ZoomLevel       = settings.TextEditorDefaults.DefaultZoom;
                break;
        }
    }

    private void ApplyHexEditorDefaults(HexEditorControl hex)
    {
        var d = AppSettingsService.Instance.Current.HexEditorDefaults;

        // Display
        hex.BytePerLine           = d.BytePerLine;
        hex.ShowOffset            = d.ShowOffset;
        hex.ShowAscii             = d.ShowAscii;
        hex.DataStringVisual      = d.DataStringVisual;
        hex.OffSetStringVisual    = d.OffSetStringVisual;
        hex.ByteGrouping          = d.ByteGrouping;
        hex.ByteSpacerPositioning = d.ByteSpacerPositioning;

        // Editing
        hex.EditMode              = d.DefaultEditMode;
        hex.AllowZoom             = d.AllowZoom;
        hex.MouseWheelSpeed       = d.MouseWheelSpeed;
        hex.AllowFileDrop         = d.AllowFileDrop;

        // Data interpretation
        hex.ByteSize                    = d.ByteSize;
        hex.ByteOrder                   = d.ByteOrder;
        hex.DefaultCopyToClipboardMode  = d.DefaultCopyToClipboardMode;
        hex.ByteSpacerVisualStyle       = d.ByteSpacerVisualStyle;

        // Scroll markers
        hex.ShowBookmarkMarkers     = d.ShowBookmarkMarkers;
        hex.ShowModifiedMarkers     = d.ShowModifiedMarkers;
        hex.ShowInsertedMarkers     = d.ShowInsertedMarkers;
        hex.ShowDeletedMarkers      = d.ShowDeletedMarkers;
        hex.ShowSearchResultMarkers = d.ShowSearchResultMarkers;

        // Status bar visibility
        hex.ShowStatusMessage          = d.ShowStatusMessage;
        hex.ShowFileSizeInStatusBar    = d.ShowFileSizeInStatusBar;
        hex.ShowSelectionInStatusBar   = d.ShowSelectionInStatusBar;
        hex.ShowPositionInStatusBar    = d.ShowPositionInStatusBar;
        hex.ShowEditModeInStatusBar    = d.ShowEditModeInStatusBar;
        hex.ShowBytesPerLineInStatusBar = d.ShowBytesPerLineInStatusBar;

        // Advanced behaviour
        hex.AllowAutoHighLightSelectionByte      = d.AllowAutoHighLightSelectionByte;
        hex.AllowAutoSelectSameByteAtDoubleClick = d.AllowAutoSelectSameByteAtDoubleClick;
        hex.AllowContextMenu                     = d.AllowContextMenu;
        hex.AllowDeleteByte                      = d.AllowDeleteByte;
        hex.AllowExtend                          = d.AllowExtend;
        hex.FileDroppingConfirmation             = d.FileDroppingConfirmation;
        hex.PreloadByteInEditorMode              = d.PreloadByteInEditorMode;
    }

    // --- Menu: Project -------------------------------------------------

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
        OpenProjectPropertiesDocument(project);
    }

    /// <summary>
    /// Opens (or activates) the VS-Like Project Properties document tab for the given project.
    /// Uses contentId "doc-projprops-{Name}" for deduplication.
    /// </summary>
    private void OpenProjectPropertiesDocument(IProject project)
    {
        var contentId = $"doc-projprops-{project.Name}";

        // Dedup: activate existing tab if already open
        var existing = _layout.GetAllGroups().SelectMany(g => g.Items)
            .Concat(_layout.FloatingItems)
            .Concat(_layout.AutoHideItems)
            .FirstOrDefault(di => di.ContentId == contentId);

        if (existing is not null)
        {
            if (existing.Owner is { } owner) owner.ActiveItem = existing;
            DockHost.RebuildVisualTree();
            return;
        }

        // Register project reference so the content factory can retrieve it
        _projectPropertiesMap[contentId] = project;

        var item = new DockItem
        {
            Title     = $"{project.Name} — Propriétés",
            ContentId = contentId,
            Metadata  = { ["ProjectName"] = project.Name }
        };

        _engine.Dock(item, _layout.MainDocumentHost, DockDirection.Center);
        DockHost.RebuildVisualTree();
    }

    /// <summary>
    /// Content factory for "doc-projprops-*" items.
    /// Creates the <see cref="ProjectPropertiesDocument"/> UserControl backed by its ViewModel.
    /// </summary>
    private UIElement CreateProjectPropertiesContent(DockItem item)
    {
        if (!_projectPropertiesMap.TryGetValue(item.ContentId, out var project))
        {
            // Layout-restore path: the map is empty on startup because it is only populated by
            // OpenProjectPropertiesDocument(). Try to resolve by name from the loaded solution.
            var name = item.ContentId["doc-projprops-".Length..];
            project = _solutionManager.CurrentSolution?.Projects
                          .FirstOrDefault(p => p.Name == name);

            if (project is null)
            {
                // Solution not yet loaded at layout-restore time. Track this tab so
                // OnSolutionChanged() can evict the placeholder and rebuild the content.
                _pendingProjectPropertiesContentIds.Add(item.ContentId);
                return new System.Windows.Controls.TextBlock { Text = "Projet introuvable." };
            }

            // Re-register so dirty-state title updates work for the rest of this session.
            _projectPropertiesMap[item.ContentId] = project;
        }

        var vm = new WpfHexEditor.ProjectSystem.Documents.ProjectPropertiesViewModel(
            project, _solutionManager);

        // Update the tab title with a '*' prefix whenever unsaved changes exist.
        var baseTitle = $"{project.Name} — Propriétés";
        vm.PropertyChanged += (_, args) =>
        {
            if (args.PropertyName != nameof(vm.IsDirty)) return;

            var dockItem = _layout.GetAllGroups()
                .SelectMany(g => g.Items)
                .Concat(_layout.FloatingItems)
                .FirstOrDefault(di => di.ContentId == item.ContentId);

            if (dockItem is null) return;
            dockItem.Title = vm.IsDirty ? $"*{baseTitle}" : baseTitle;
            DockHost.RebuildVisualTree();
        };

        // Open NuGet Manager when the user clicks "+ NuGet…" inside Project Properties.
        vm.ManageNuGetRequested += (_, e) => OpenNuGetManagerDocument(e.Project);

        return new WpfHexEditor.ProjectSystem.Documents.ProjectPropertiesDocument
        {
            DataContext = vm
        };
    }

    // ── NuGet Manager ────────────────────────────────────────────────────────

    private void OnSEManageNuGetPackages(object? sender, ManageNuGetRequestedEventArgs e)
        => OpenNuGetManagerDocument(e.Project);

    /// <summary>
    /// Opens (or activates) the VS-Like NuGet Package Manager document tab for the given project.
    /// Uses contentId "doc-nuget-{Name}" for deduplication.
    /// </summary>
    private void OpenNuGetManagerDocument(IProject project)
    {
        var contentId = $"doc-nuget-{project.Name}";

        // Dedup: activate existing tab if already open.
        var existing = _layout.GetAllGroups().SelectMany(g => g.Items)
            .Concat(_layout.FloatingItems)
            .Concat(_layout.AutoHideItems)
            .FirstOrDefault(di => di.ContentId == contentId);

        if (existing is not null)
        {
            if (existing.Owner is { } owner) owner.ActiveItem = existing;
            DockHost.RebuildVisualTree();
            return;
        }

        _nugetManagerMap[contentId] = project;

        var item = new DockItem
        {
            Title     = $"NuGet — {project.Name}",
            ContentId = contentId,
            Metadata  = { ["ProjectName"] = project.Name }
        };

        _engine.Dock(item, _layout.MainDocumentHost, DockDirection.Center);
        DockHost.RebuildVisualTree();
    }

    /// <summary>
    /// Content factory for "doc-nuget-*" items.
    /// Creates the <see cref="NuGetManagerDocument"/> UserControl backed by its ViewModel.
    /// </summary>
    private UIElement CreateNuGetManagerContent(DockItem item)
    {
        if (!_nugetManagerMap.TryGetValue(item.ContentId, out var project))
        {
            var name = item.ContentId["doc-nuget-".Length..];
            project = _solutionManager.CurrentSolution?.Projects
                          .FirstOrDefault(p => p.Name == name);

            if (project is null)
                return new System.Windows.Controls.TextBlock { Text = "Project not found." };

            _nugetManagerMap[item.ContentId] = project;
        }

        var vm = new WpfHexEditor.ProjectSystem.Documents.NuGet.NuGetManagerViewModel(
            project,
            new WpfHexEditor.ProjectSystem.Services.NuGet.NuGetV3Client());

        // Reload project after any .csproj write-back to keep the Solution Explorer in sync.
        vm.ProjectModified += (_, _) =>
        {
            var sol = _solutionManager.CurrentSolution;
            if (sol is not null)
                _solutionExplorerPanel?.SetSolution(sol);
        };

        return new WpfHexEditor.ProjectSystem.Documents.NuGet.NuGetManagerDocument
        {
            DataContext = vm
        };
    }

    // --- MRU menus (Recent Solutions / Recent Files) -------------------

    private void PopulateRecentMenus()
    {
        PopulateMruMenu(RecentSolutionsMenuItem,
            _solutionManager.RecentSolutions,
            path => _ = OpenSolutionAsync(path));

        PopulateMruMenu(RecentFilesMenuItem,
            _solutionManager.RecentFiles,
            path =>
            {
                OpenFileDirectly(path);
                PopulateRecentMenus(); // refresh MRU order (most-recently-used to top)
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

    // --- View panel helpers --------------------------------------------

    private void OnShowProperties(object sender, RoutedEventArgs e)
        => ShowOrCreatePanel("Properties", "panel-properties", DockDirection.Right);

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

        StoreContent(contentId, viewer);
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

        StoreContent(contentId, viewer);
        var item = new DockItem { Title = title, ContentId = contentId };
        _engine.Dock(item, _layout.MainDocumentHost, DockDirection.Center);
        DockHost.RebuildVisualTree();
        _ = viewer.OpenAsync(filePath);
    }

    // Toolbar shortcut — delegates to ParsedFields plugin panel via UIRegistry.
    private void OnShowParsedFields(object sender, RoutedEventArgs e)
        => _ideHostContext?.UIRegistry.ShowPanel(
               "WpfHexEditor.Plugins.ParsedFields.Panel.ParsedFieldsPanel");

    private void OnShowSolutionExplorer(object sender, RoutedEventArgs e)
    {
        ShowOrCreatePanel("Solution Explorer", SolutionExplorerContentId, DockDirection.Left);
        // If a singleton already exists but panel was closed, re-dock
        if (_solutionExplorerPanel != null)
            _solutionExplorerPanel.SetSolution(_solutionManager.CurrentSolution);
    }

    private void OnShowWelcome(object sender, RoutedEventArgs e)
    {
        const string welcomeContentId = "doc-welcome";

        var existing = _layout.FindItemByContentId(welcomeContentId);
        if (existing is not null)
        {
            if (existing.Owner is { } owner)
                owner.ActiveItem = existing;
            DockHost.RebuildVisualTree();
            return;
        }

        var item = new DockItem { Title = "Welcome", ContentId = welcomeContentId };
        _engine.Dock(item, _layout.MainDocumentHost, DockDirection.Center);
        DockHost.RebuildVisualTree();
    }

    private void OnShowOutput(object sender, RoutedEventArgs e)
        => ShowOrCreatePanel("Output", "panel-output", DockDirection.Bottom);

    private void OnShowErrorPanel(object sender, RoutedEventArgs e)
        => ShowOrCreatePanel("Error List", ErrorPanelContentId, DockDirection.Bottom);

    private async System.Threading.Tasks.Task RefreshAnalysisPanelsAsync(HexEditorControl hex)
    {
        try
        {
            // Auto-load archive structure for ZIP files
            if (_archivePanel is not null && !string.IsNullOrEmpty(hex.FileName))
            {
                var ext = System.IO.Path.GetExtension(hex.FileName).ToLowerInvariant();
                if (ext == ".zip")
                {
                    try
                    {
                        var root = await System.Threading.Tasks.Task.Run(() => BuildZipArchiveTree(hex.FileName));
                        if (!ReferenceEquals(hex, _connectedHexEditor)) return;
                        _archivePanel.LoadArchive(root);
                    }
                    catch { /* not a valid zip */ }
                }
            }

        }
        catch (Exception ex)
        {
            OutputLogger.Debug($"RefreshAnalysisPanels error: {ex.Message}");
        }
    }

    private static WpfHexEditor.Panels.FileOps.ArchiveNode BuildZipArchiveTree(string zipPath)
    {
        var root = new WpfHexEditor.Panels.FileOps.ArchiveNode
        {
            Name     = System.IO.Path.GetFileName(zipPath),
            IsFolder = true,
            Children = new System.Collections.ObjectModel.ObservableCollection<WpfHexEditor.Panels.FileOps.ArchiveNode>()
        };

        using var zip = System.IO.Compression.ZipFile.OpenRead(zipPath);
        var folderMap = new System.Collections.Generic.Dictionary<string, WpfHexEditor.Panels.FileOps.ArchiveNode>
        {
            [""] = root
        };

        // Ensure folder nodes exist for the given path
        WpfHexEditor.Panels.FileOps.ArchiveNode EnsureFolder(string folderPath)
        {
            if (folderMap.TryGetValue(folderPath, out var existing)) return existing;
            var parent = EnsureFolder(System.IO.Path.GetDirectoryName(folderPath)?.Replace('\\', '/') ?? "");
            var node = new WpfHexEditor.Panels.FileOps.ArchiveNode
            {
                Name     = System.IO.Path.GetFileName(folderPath.TrimEnd('/')),
                IsFolder = true,
                Children = new System.Collections.ObjectModel.ObservableCollection<WpfHexEditor.Panels.FileOps.ArchiveNode>()
            };
            parent.Children.Add(node);
            folderMap[folderPath] = node;
            return node;
        }

        foreach (var entry in zip.Entries)
        {
            var fullName = entry.FullName.Replace('\\', '/');
            if (fullName.EndsWith("/"))
            {
                EnsureFolder(fullName.TrimEnd('/'));
                continue;
            }
            var dirPart  = System.IO.Path.GetDirectoryName(fullName)?.Replace('\\', '/') ?? "";
            var parentNode = EnsureFolder(dirPart);
            parentNode.Children.Add(new WpfHexEditor.Panels.FileOps.ArchiveNode
            {
                Name              = entry.Name,
                IsFolder          = false,
                Size              = entry.Length,
                CompressedSize    = entry.CompressedLength,
                Crc               = $"{entry.Crc32:X8}",
                CompressionMethod = "Deflate",
                Children          = new System.Collections.ObjectModel.ObservableCollection<WpfHexEditor.Panels.FileOps.ArchiveNode>()
            });
        }

        return root;
    }

    private void OnGoToOffset(object sender, RoutedEventArgs e)
    {
        if (ActiveHexEditor is not { } hex) return;

        var dlg = new WpfHexEditor.App.Dialogs.GoToOffsetDialog { Owner = this };
        if (dlg.ShowDialog() != true) return;

        if (dlg.Offset >= 0 && dlg.Offset < hex.Length)
            hex.SetPosition(dlg.Offset);
        else
            OutputLogger.Debug($"Go To Offset: offset 0x{dlg.Offset:X} is out of range (file size = {hex.Length}).");
    }

    private void OnFileDrop(object sender, DragEventArgs e)
    {
        if (e.Data.GetData(DataFormats.FileDrop) is string[] files)
            foreach (var file in files)
                OpenFileDirectly(file);
    }

    private void OnFileDragOver(object sender, DragEventArgs e)
    {
        e.Effects = e.Data.GetDataPresent(DataFormats.FileDrop)
            ? DragDropEffects.Copy
            : DragDropEffects.None;
        e.Handled = true;
    }

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

    // --- Menu: Layout --------------------------------------------------

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
            PruneStaleDocumentItems(layout);
            _contentCache.Clear();
            ApplyLayout(layout);
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
        // Snapshot open document items before resetting (exclude the Welcome tab).
        // The _contentCache is intentionally NOT cleared — cached UIElements remain valid
        // and will be returned by ContentFactory when the tabs are re-docked below.
        var openDocs = _layout.GetAllGroups()
            .SelectMany(g => g.Items)
            .Concat(_layout.FloatingItems)
            .Where(i => i.ContentId.StartsWith("doc-") && i.ContentId != "doc-welcome")
            .Select(i => new DockItem { ContentId = i.ContentId, Title = i.Title, CanClose = i.CanClose })
            .ToList();

        SetupDefaultLayout();

        // Re-dock all previously open document tabs into the document host.
        foreach (var doc in openDocs)
            _engine.Dock(doc, _layout.MainDocumentHost, DockDirection.Center);

        if (openDocs.Count > 0)
            DockHost.RebuildVisualTree();

        OutputLogger.Debug($"Layout reset to default. {openDocs.Count} document(s) preserved.");
    }

    // --- Menu: other ---------------------------------------------------

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

        // Phase 9 — forward new theme resources to sandbox plugins so their
        // WPF controls apply the same brush/color tokens as the IDE.
        _themeService?.NotifyThemeChanged(themeName);
        if (_pluginHost is not null)
        {
            var themeXaml = WpfHexEditor.PluginHost.Sandbox.ThemeResourceSerializer.Serialize(
                Application.Current.Resources);
            _ = _pluginHost.NotifyThemeChangedAsync(themeXaml);
        }
    }

    // -----------------------------------------------------------------------
    // Image menu — delegates to the active ImageViewer
    // -----------------------------------------------------------------------

    private WpfHexEditor.Editor.ImageViewer.Controls.ImageViewer? ActiveImageViewer =>
        _activeDocumentEditor as WpfHexEditor.Editor.ImageViewer.Controls.ImageViewer;

    private void OnImageRotateRight     (object sender, RoutedEventArgs e) => ActiveImageViewer?.RotateRight();
    private void OnImageRotateLeft      (object sender, RoutedEventArgs e) => ActiveImageViewer?.RotateLeft();
    private void OnImageFlipH           (object sender, RoutedEventArgs e) => ActiveImageViewer?.FlipH();
    private void OnImageFlipV           (object sender, RoutedEventArgs e) => ActiveImageViewer?.FlipV();
    private void OnImageCropMode        (object sender, RoutedEventArgs e) => ActiveImageViewer?.ToggleCropMode();
    private void OnImageResize          (object sender, RoutedEventArgs e) => ActiveImageViewer?.OpenResizeDialog();
    private void OnImageSaveAs          (object sender, RoutedEventArgs e) => ActiveImageViewer?.SaveAs();
    private void OnImageResetTransforms (object sender, RoutedEventArgs e) => ActiveImageViewer?.ResetAllTransforms();

    // -----------------------------------------------------------------------
    // Themes
    // -----------------------------------------------------------------------

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
        // Sync editors in the visual tree (visible tabs).
        foreach (var editor in FindVisualChildren<HexEditorControl>(this))
            editor.ApplyThemeFromResources();

        // Also sync editors that are cached but not yet in the visual tree
        // (background tabs whose ContentFactory was called but the DockHost
        // hasn't added them to the live visual tree yet).
        foreach (var content in _contentCache.Values)
        {
            if (content is HexEditorControl cached)
                cached.ApplyThemeFromResources();
        }
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

    // --- Title bar ---------------------------------------------------

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

    // --- Logo click — native system menu -----------------------------

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

    // --- WM_GETMINMAXINFO — maximize respects taskbar (per-monitor) ------

    [StructLayout(LayoutKind.Sequential)] private struct POINT      { public int x, y; }
    [StructLayout(LayoutKind.Sequential)] private struct WINRECT    { public int left, top, right, bottom; }
    [StructLayout(LayoutKind.Sequential)] private struct MINMAXINFO
    {
        public POINT ptReserved, ptMaxSize, ptMaxPosition, ptMinTrackSize, ptMaxTrackSize;
    }
    [StructLayout(LayoutKind.Sequential)] private struct MONITORINFO
    {
        public int     cbSize;
        public WINRECT rcMonitor;
        public WINRECT rcWork;
        public uint    dwFlags;
    }

    [DllImport("user32.dll")] private static extern IntPtr MonitorFromWindow(IntPtr hwnd, int flags);
    [DllImport("user32.dll")] private static extern bool   GetMonitorInfo(IntPtr hMonitor, ref MONITORINFO lpmi);

    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);
        (PresentationSource.FromVisual(this) as HwndSource)?.AddHook(HwndHook);
    }

    private IntPtr HwndHook(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        // Intercept WM_GETMINMAXINFO to constrain maximize to the current monitor's
        // work area. MonitorFromWindow identifies the correct monitor for multi-screen
        // setups — unlike SystemParameters.WorkArea which always returns the primary
        // monitor's work area regardless of where the window currently lives.
        const int WM_GETMINMAXINFO = 0x0024;
        if (msg != WM_GETMINMAXINFO) return IntPtr.Zero;

        var hMon = MonitorFromWindow(hwnd, 2 /* MONITOR_DEFAULTTONEAREST */);
        var mi   = new MONITORINFO { cbSize = Marshal.SizeOf<MONITORINFO>() };
        if (!GetMonitorInfo(hMon, ref mi)) return IntPtr.Zero;

        var mmi = Marshal.PtrToStructure<MINMAXINFO>(lParam);
        // ptMaxPosition is relative to the monitor's top-left corner, NOT the virtual screen.
        // Using rcWork directly (screen coords) causes the window to offset by the monitor
        // origin and fly off-screen on secondary monitors.
        mmi.ptMaxPosition.x = Math.Abs(mi.rcWork.left - mi.rcMonitor.left);
        mmi.ptMaxPosition.y = Math.Abs(mi.rcWork.top  - mi.rcMonitor.top);
        mmi.ptMaxSize.x     = mi.rcWork.right  - mi.rcWork.left;
        mmi.ptMaxSize.y     = mi.rcWork.bottom - mi.rcWork.top;
        Marshal.StructureToPtr(mmi, lParam, true);

        handled = true;
        return IntPtr.Zero;
    }

    private void UpdateStatusBar()
    {
        var activeItem = _layout?.MainDocumentHost?.ActiveItem;
        if (activeItem is null)
        {
            ActiveStatusBarContributor = _defaultStatusBarContributor;
            ActiveDocumentEditor       = null;
            ActiveToolbarContributor   = null;
            return;
        }
        OnActiveDocumentChanged(activeItem);
    }
}
