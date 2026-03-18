// ==========================================================
// Project: WpfHexEditor.App
// File: MainWindow.Build.cs
// Author: Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.6
// Created: 2026-03-16
// Description:
//     Partial class — Build system integration for MainWindow.
//     Contains:
//       - BuildSystem + ConfigurationManager initialization
//       - Properties: IsBuildMenuEnabled, HasActiveBuild,
//         BuildConfigurations, ActiveBuildConfiguration, BuildPlatforms
//       - Click handlers: Build/Rebuild/Clean Solution|Project, Cancel, ConfigManager
//       - StatusBar + ErrorList adapter wiring
//       - Keyboard shortcut: Ctrl+Shift+B → Build Solution
// ==========================================================

using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Input;
using WpfHexEditor.App.Build;
using WpfHexEditor.BuildSystem;
using WpfHexEditor.Docking.Core;
using WpfHexEditor.Editor.Core;
using WpfHexEditor.Events.IDEEvents;
using WpfHexEditor.Panels.IDE.Panels;

namespace WpfHexEditor.App;

public partial class MainWindow
{
    // -----------------------------------------------------------------------
    // Build infrastructure (lazy-initialized after plugin system is ready)
    // -----------------------------------------------------------------------

    private BuildSystem.BuildSystem?    _buildSystem;
    private ConfigurationManager?       _configManager;
    private BuildOutputAdapter?         _buildOutputAdapter;
    private BuildErrorListAdapter?      _buildErrorListAdapter;
    private BuildStatusBarAdapter?      _buildStatusBarAdapter;
    private StartupProjectRunner?       _startupRunner;
    private IDisposable[]?              _buildStateRefreshSubs;

    // -----------------------------------------------------------------------
    // Properties (bound in XAML)
    // -----------------------------------------------------------------------

    /// <summary>True when a solution is loaded and no build is running.</summary>
    public bool IsBuildMenuEnabled => _hasSolution && !(_buildSystem?.HasActiveBuild ?? false);

    /// <summary>True when a startup project is set and the IDE is idle (no active build).</summary>
    public bool CanRunStartupProject
        => IsBuildMenuEnabled
        && _solutionManager.CurrentSolution?.StartupProject is not null;

    /// <summary>True while a build is in progress — enables "Cancel Build".</summary>
    public bool HasActiveBuild => _buildSystem?.HasActiveBuild ?? false;

    /// <summary>Available build configuration names (Debug / Release / custom).</summary>
    public ObservableCollection<string> BuildConfigurations { get; } = ["Debug", "Release"];

    /// <summary>Active build configuration name.</summary>
    public string ActiveBuildConfiguration
    {
        get => _configManager?.ActiveConfiguration.Name ?? "Debug";
        set
        {
            if (_configManager is null) return;
            var cfg = _configManager.Configurations.FirstOrDefault(
                c => c.Name.Equals(value, StringComparison.OrdinalIgnoreCase));
            if (cfg is not null) _configManager.ActiveConfiguration = cfg;
            OnPropertyChanged();
        }
    }

    /// <summary>Available platform names (AnyCPU / x64 / x86).</summary>
    public ObservableCollection<string> BuildPlatforms { get; } = ["AnyCPU", "x64", "x86"];

    /// <summary>Active build platform.</summary>
    public string ActiveBuildPlatform
    {
        get => _configManager?.ActivePlatform ?? "AnyCPU";
        set
        {
            if (_configManager is null) return;
            _configManager.ActivePlatform = value;
            OnPropertyChanged();
        }
    }

    /// <summary>Project names shown in the startup-project selector ComboBox (launchable only + sentinel).</summary>
    public ObservableCollection<string> StartupProjectNames { get; } = [];

    /// <summary>Currently selected startup project name (two-way bound to the ComboBox).</summary>
    public string? ActiveStartupProjectName
    {
        get => _solutionManager.CurrentSolution?.StartupProject?.Name;
        set
        {
            if (value is null || _solutionManager.CurrentSolution is null) return;

            // Sentinel is intercepted in OnStartupProjectSelectionChanged before the
            // binding can push the value here.  Guard just in case.
            if (value == StartupProjectSentinel) return;

            var project = _solutionManager.CurrentSolution.Projects
                .FirstOrDefault(p => p.Name.Equals(value, StringComparison.OrdinalIgnoreCase));
            if (project is null) return;
            SetStartupProject(project.Id);
            OnPropertyChanged();
            OnPropertyChanged(nameof(CanRunStartupProject));
        }
    }

    // Sentinel shown at the bottom of the startup project ComboBox.
    private const string StartupProjectSentinel = "⚙ Configure startup projects…";

    // -----------------------------------------------------------------------
    // Initialization — called from MainWindow.PluginSystem.cs after host ready
    // -----------------------------------------------------------------------

    /// <summary>
    /// Wires the build system infrastructure. Must be called after
    /// <c>_ideEventBus</c> and <c>_outputService</c> are initialized.
    /// </summary>
    internal void InitializeBuildSystem()
    {
        if (_ideEventBus is null) return;

        _configManager   = new ConfigurationManager();
        _buildSystem     = new BuildSystem.BuildSystem(_solutionManager, _ideEventBus, _configManager);
        _startupRunner   = new StartupProjectRunner(_solutionManager, _buildSystem, _ideEventBus, _configManager);

        // Wire output adapter (routes build lines → OutputPanel).
        if (_outputService is not null)
            _buildOutputAdapter = new BuildOutputAdapter(_ideEventBus, _outputService);

        // Wire error list adapter (populates ErrorPanel after each build).
        _buildErrorListAdapter = new BuildErrorListAdapter(_ideEventBus);

        // Register the error list adapter as a diagnostic source in the ErrorPanel.
        EnsureErrorPanelInstance().AddSource(_buildErrorListAdapter);

        // Wire status bar adapter.
        _buildStatusBarAdapter = new BuildStatusBarAdapter(_ideEventBus, UpdateBuildStatusBar);

        // Wire config changes to toolbar ComboBox refresh.
        _configManager.ConfigurationChanged += (_, _) =>
        {
            OnPropertyChanged(nameof(ActiveBuildConfiguration));
            OnPropertyChanged(nameof(ActiveBuildPlatform));
            OnPropertyChanged(nameof(IsBuildMenuEnabled));
        };

        // Belt-and-suspenders: refresh toolbar state from build lifecycle events.
        // BuildSucceeded/Failed/Cancelled are published BEFORE BuildSystem's finally
        // clears _activeCts, so we use Dispatcher.InvokeAsync — the deferred item
        // runs after the current synchronous stack unwinds, by which time _activeCts
        // is already null and HasActiveBuild correctly returns false.
        _buildStateRefreshSubs =
        [
            _ideEventBus.Subscribe<BuildSucceededEvent>(_ => Dispatcher.InvokeAsync(RefreshBuildProperties)),
            _ideEventBus.Subscribe<BuildFailedEvent>   (_ => Dispatcher.InvokeAsync(RefreshBuildProperties)),
            _ideEventBus.Subscribe<BuildCancelledEvent>(_ => Dispatcher.InvokeAsync(RefreshBuildProperties)),
        ];

        // Register Ctrl+Shift+B → Build Solution.
        var buildGesture = new KeyBinding(
            new RelayCommand(async _ => await RunBuildSolutionAsync()),
            Key.B, ModifierKeys.Control | ModifierKeys.Shift);
        InputBindings.Add(buildGesture);

        // Register Ctrl+F5 → Start Without Debugging.
        var runGesture = new KeyBinding(
            new RelayCommand(async _ => await RunStartupProjectAsync()),
            Key.F5, ModifierKeys.Control);
        InputBindings.Add(runGesture);

        // Wire SolutionExplorer VS build context-menu events.
        if (_solutionExplorerPanel is not null)
        {
            _solutionExplorerPanel.BuildProjectRequested       += (_, id) => _ = RunBuildProjectByIdAsync(id);
            _solutionExplorerPanel.RebuildProjectRequested     += (_, id) => _ = RunRebuildProjectByIdAsync(id);
            _solutionExplorerPanel.CleanProjectRequested       += (_, id) => _ = RunCleanProjectByIdAsync(id);
            _solutionExplorerPanel.SetStartupProjectRequested  += (_, id) =>
            {
                SetStartupProject(id);
                OnPropertyChanged(nameof(ActiveStartupProjectName));
                OnPropertyChanged(nameof(CanRunStartupProject));
            };
        }
    }

    // -----------------------------------------------------------------------
    // Click handlers (bound in MainWindow.xaml)
    // -----------------------------------------------------------------------

    private async void OnRunStartupProject(object sender, RoutedEventArgs e) => await RunStartupProjectAsync();
    private async void OnBuildSolution  (object sender, RoutedEventArgs e) => await RunBuildSolutionAsync();
    private async void OnBuildProject   (object sender, RoutedEventArgs e) => await RunBuildProjectAsync();
    private async void OnRebuildSolution(object sender, RoutedEventArgs e) => await RunRebuildSolutionAsync();
    private async void OnRebuildProject (object sender, RoutedEventArgs e) => await RunRebuildProjectAsync();
    private async void OnCleanSolution  (object sender, RoutedEventArgs e) => await RunCleanSolutionAsync();
    private async void OnCleanProject   (object sender, RoutedEventArgs e) => await RunCleanProjectAsync();
    private void OnCancelBuild    (object sender, RoutedEventArgs e) => _buildSystem?.CancelBuild();

    /// <summary>
    /// SelectionChanged handler for CbStartupProject.
    /// Intercepts the sentinel item BEFORE the TwoWay binding can push the value
    /// to <see cref="ActiveStartupProjectName"/>, reverts the ComboBox display
    /// synchronously, then defers the property-pages dialog.
    /// </summary>
    internal void OnStartupProjectSelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
    {
        if (e.AddedItems.Count == 0) return;
        if (e.AddedItems[0] is not string s || s != StartupProjectSentinel) return;

        // Revert the ComboBox display immediately — setting SelectedItem directly
        // is synchronous and guaranteed to update SelectionBoxItem before any render.
        var cb = (System.Windows.Controls.ComboBox)sender;
        cb.SelectedItem = ActiveStartupProjectName;

        // Open dialog after Background priority so the reverted display is painted first.
        Dispatcher.InvokeAsync(() => OpenSolutionPropertyPages("startup"),
                               System.Windows.Threading.DispatcherPriority.Background);
    }

    private void OnOpenConfigManager(object sender, RoutedEventArgs e)
    {
        if (_configManager is null) return;
        var dlg = new ConfigurationManagerDialog(_configManager, _solutionManager)
        {
            Owner = this,
        };
        dlg.ShowDialog();
        // Refresh toolbar ComboBoxes after dialog closes.
        OnPropertyChanged(nameof(ActiveBuildConfiguration));
        OnPropertyChanged(nameof(ActiveBuildPlatform));
    }

    // -----------------------------------------------------------------------
    // Async build runners
    // -----------------------------------------------------------------------

    private async Task RunStartupProjectAsync()
    {
        if (_startupRunner is null) return;
        ShowOrCreatePanel("Output", "panel-output", DockDirection.Bottom);
        OutputLogger.FocusChannel(OutputLogger.SourceBuild);
        // Start the task first — the sync preamble inside RunAsync calls BuildSolutionAsync
        // which sets _activeCts before its first true await, so RefreshBuildProperties()
        // here correctly sees HasActiveBuild=true and disables Build/Rebuild/Clean.
        var runTask = _startupRunner.RunAsync();
        RefreshBuildProperties();
        try   { await runTask; }
        finally{ RefreshBuildProperties(); }
    }

    private async Task RunBuildSolutionAsync()
    {
        if (_buildSystem is null) return;
        ShowOrCreatePanel("Output", "panel-output", DockDirection.Bottom);
        OutputLogger.FocusChannel(OutputLogger.SourceBuild);
        if (_solutionManager.CurrentSolution is null
            || _solutionManager.CurrentSolution.Projects.Count == 0)
        {
            OutputLogger.BuildWarn("No solution or projects loaded — nothing to build.");
            return;
        }
        _buildErrorListAdapter?.ClearDiagnostics();
        // BuildSolutionAsync sets _activeCts synchronously before its first await,
        // so calling RefreshBuildProperties() after starting the task (but before
        // awaiting it) correctly reflects HasActiveBuild=true in the toolbar.
        var buildTask = _buildSystem.BuildSolutionAsync();
        RefreshBuildProperties();
        try
        {
            var result = await buildTask;
            _buildErrorListAdapter?.SetDiagnostics(result.Errors.Concat(result.Warnings));
        }
        finally { RefreshBuildProperties(); }
    }

    private async Task RunBuildProjectAsync()
    {
        if (_buildSystem is null || _solutionManager.CurrentSolution is null) return;
        var startup = _solutionManager.CurrentSolution.StartupProject;
        if (startup is null) return;
        ShowOrCreatePanel("Output", "panel-output", DockDirection.Bottom);
        OutputLogger.FocusChannel(OutputLogger.SourceBuild);
        _buildErrorListAdapter?.ClearDiagnostics();
        var buildTask = _buildSystem.BuildProjectAsync(startup.Id);
        RefreshBuildProperties();
        try
        {
            var result = await buildTask;
            _buildErrorListAdapter?.SetDiagnostics(result.Errors.Concat(result.Warnings));
        }
        finally { RefreshBuildProperties(); }
    }

    private async Task RunRebuildSolutionAsync()
    {
        if (_buildSystem is null) return;
        ShowOrCreatePanel("Output", "panel-output", DockDirection.Bottom);
        OutputLogger.FocusChannel(OutputLogger.SourceBuild);
        if (_solutionManager.CurrentSolution is null
            || _solutionManager.CurrentSolution.Projects.Count == 0)
        {
            OutputLogger.BuildWarn("No solution or projects loaded — nothing to rebuild.");
            return;
        }
        _buildErrorListAdapter?.ClearDiagnostics();
        var rebuildTask = _buildSystem.RebuildSolutionAsync();
        RefreshBuildProperties();
        try
        {
            var result = await rebuildTask;
            _buildErrorListAdapter?.SetDiagnostics(result.Errors.Concat(result.Warnings));
        }
        finally { RefreshBuildProperties(); }
    }

    private async Task RunRebuildProjectAsync()
    {
        if (_buildSystem is null || _solutionManager.CurrentSolution is null) return;
        var startup = _solutionManager.CurrentSolution.StartupProject;
        if (startup is null) return;
        ShowOrCreatePanel("Output", "panel-output", DockDirection.Bottom);
        OutputLogger.FocusChannel(OutputLogger.SourceBuild);
        _buildErrorListAdapter?.ClearDiagnostics();
        var rebuildTask = _buildSystem.RebuildProjectAsync(startup.Id);
        RefreshBuildProperties();
        try
        {
            var result = await rebuildTask;
            _buildErrorListAdapter?.SetDiagnostics(result.Errors.Concat(result.Warnings));
        }
        finally { RefreshBuildProperties(); }
    }

    private async Task RunCleanSolutionAsync()
    {
        if (_buildSystem is null) return;
        ShowOrCreatePanel("Output", "panel-output", DockDirection.Bottom);
        OutputLogger.FocusChannel(OutputLogger.SourceBuild);
        // Start clean synchronously so _activeCts is set before RefreshBuildProperties().
        var cleanTask = _buildSystem.CleanSolutionAsync();
        RefreshBuildProperties();
        await cleanTask;
        RefreshBuildProperties();
    }

    private async Task RunCleanProjectAsync()
    {
        if (_buildSystem is null || _solutionManager.CurrentSolution is null) return;
        var startup = _solutionManager.CurrentSolution.StartupProject;
        if (startup is null) return;
        ShowOrCreatePanel("Output", "panel-output", DockDirection.Bottom);
        OutputLogger.FocusChannel(OutputLogger.SourceBuild);
        var cleanTask = _buildSystem.CleanProjectAsync(startup.Id);
        RefreshBuildProperties();
        await cleanTask;
        RefreshBuildProperties();
    }

    // -- Project-specific runners (from SolutionExplorer context menu) -----

    private async Task RunBuildProjectByIdAsync(string projectId)
    {
        if (_buildSystem is null) return;
        ShowOrCreatePanel("Output", "panel-output", DockDirection.Bottom);
        OutputLogger.FocusChannel(OutputLogger.SourceBuild);
        _buildErrorListAdapter?.ClearDiagnostics();
        var buildTask = _buildSystem.BuildProjectAsync(projectId);
        RefreshBuildProperties();
        try
        {
            var result = await buildTask;
            _buildErrorListAdapter?.SetDiagnostics(result.Errors.Concat(result.Warnings));
        }
        finally { RefreshBuildProperties(); }
    }

    private async Task RunRebuildProjectByIdAsync(string projectId)
    {
        if (_buildSystem is null) return;
        ShowOrCreatePanel("Output", "panel-output", DockDirection.Bottom);
        OutputLogger.FocusChannel(OutputLogger.SourceBuild);
        _buildErrorListAdapter?.ClearDiagnostics();
        var rebuildTask = _buildSystem.RebuildProjectAsync(projectId);
        RefreshBuildProperties();
        try
        {
            var result = await rebuildTask;
            _buildErrorListAdapter?.SetDiagnostics(result.Errors.Concat(result.Warnings));
        }
        finally { RefreshBuildProperties(); }
    }

    private async Task RunCleanProjectByIdAsync(string projectId)
    {
        if (_buildSystem is null) return;
        ShowOrCreatePanel("Output", "panel-output", DockDirection.Bottom);
        OutputLogger.FocusChannel(OutputLogger.SourceBuild);
        var cleanTask = _buildSystem.CleanProjectAsync(projectId);
        RefreshBuildProperties();
        await cleanTask;
        RefreshBuildProperties();
    }

    private void SetStartupProject(string projectId)
        => _solutionManager.SetStartupProject(projectId);

    // -----------------------------------------------------------------------
    // StatusBar update (dispatched to WPF thread)
    // -----------------------------------------------------------------------

    private void UpdateBuildStatusBar(string text, string icon, bool visible)
    {
        Dispatcher.InvokeAsync(() =>
        {
            if (BuildStatusItem  is null) return;
            if (BuildStatusText  is null) return;
            if (BuildStatusIcon  is null) return;

            BuildStatusText.Text     = text;
            BuildStatusIcon.Text     = icon;
            BuildStatusItem.Visibility = visible ? Visibility.Visible : Visibility.Collapsed;
        });
    }

    // -----------------------------------------------------------------------
    // Helpers
    // -----------------------------------------------------------------------

    private void RefreshBuildProperties()
    {
        OnPropertyChanged(nameof(IsBuildMenuEnabled));
        OnPropertyChanged(nameof(HasActiveBuild));
        OnPropertyChanged(nameof(CanRunStartupProject));
    }

    /// <summary>
    /// Rebuilds <see cref="StartupProjectNames"/> with only launchable projects
    /// plus a sentinel "Configure…" item at the bottom.
    /// </summary>
    internal void RefreshStartupProjectList()
    {
        Dispatcher.InvokeAsync(() =>
        {
            StartupProjectNames.Clear();
            var projects = _solutionManager.CurrentSolution?.Projects;
            if (projects is not null)
                foreach (var p in projects)
                    if (IsLaunchableProject(p))
                        StartupProjectNames.Add(p.Name);

            if (StartupProjectNames.Count > 0)
                StartupProjectNames.Add(StartupProjectSentinel);

            OnPropertyChanged(nameof(ActiveStartupProjectName));
            OnPropertyChanged(nameof(CanRunStartupProject));
        });
    }

    /// <summary>
    /// Opens the Solution Property Pages dialog, optionally pre-selecting a page.
    /// After the dialog closes with OK, the startup project list is refreshed.
    /// </summary>
    internal void OpenSolutionPropertyPages(string initialPage = "startup")
    {
        if (_solutionManager.CurrentSolution is null) return;
        var dlg = new Dialogs.SolutionPropertyPagesDialog(
            _solutionManager, _configManager, initialPage)
        {
            Owner = this
        };
        if (dlg.ShowDialog() == true)
            RefreshStartupProjectList();
    }

    // A project is launchable only when it is an MSBuild project with an
    // executable OutputType (Exe / WinExe).  WH-native .whproj projects do not
    // produce a compiled executable and cannot be resolved via
    // `dotnet msbuild -getProperty:TargetPath`.  Fixes #197 RC-4.
    private static bool IsLaunchableProject(IProject p)
    {
        if (p is not WpfHexEditor.Editor.Core.IProjectWithReferences vp) return false;
        return vp.OutputType.Equals("Exe",    StringComparison.OrdinalIgnoreCase)
            || vp.OutputType.Equals("WinExe", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>Minimal inline ICommand for the Ctrl+Shift+B binding.</summary>
    private sealed class RelayCommand(Action<object?> execute) : ICommand
    {
        public bool CanExecute(object? parameter) => true;
        public void Execute(object? parameter) => execute(parameter);
        public event EventHandler? CanExecuteChanged;
    }
}
