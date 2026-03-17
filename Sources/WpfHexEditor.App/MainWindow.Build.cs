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
//         BuildConfigurations, ActiveBuildConfiguration, BuildPlatforms,
//         StartupProjectNames, ActiveStartupProjectName
//       - Click handlers: Build/Rebuild/Clean Solution|Project, Cancel, ConfigManager
//       - StatusBar + ErrorList adapter wiring
//       - Keyboard shortcuts: Ctrl+Shift+B → Build Solution, Ctrl+F5 → Start
//       - Build output channel is cleared at the start of every build operation
// ==========================================================

using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Input;
using WpfHexEditor.App.Build;
using WpfHexEditor.BuildSystem;
using WpfHexEditor.Editor.Core;

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

    /// <summary>
    /// Project names shown in the startup-project selector ComboBox.
    /// Rebuilt whenever the solution changes.
    /// </summary>
    public ObservableCollection<string> StartupProjectNames { get; } = [];

    /// <summary>
    /// The name of the currently selected startup project (bound two-way to the ComboBox).
    /// Setting it calls <see cref="SetStartupProject"/> and refreshes CanRunStartupProject.
    /// </summary>
    public string? ActiveStartupProjectName
    {
        get => _solutionManager.CurrentSolution?.StartupProject?.Name;
        set
        {
            if (value is null || _solutionManager.CurrentSolution is null) return;
            var project = _solutionManager.CurrentSolution.Projects
                .FirstOrDefault(p => p.Name.Equals(value, StringComparison.OrdinalIgnoreCase));
            if (project is null) return;
            SetStartupProject(project.Id);
            OnPropertyChanged();
            OnPropertyChanged(nameof(CanRunStartupProject));
        }
    }

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

        // Note: SolutionExplorer build events (Build/Rebuild/Clean/SetStartupProject)
        // are wired in CreateSolutionExplorerPanel() (MainWindow.xaml.cs) because the
        // panel is lazily created after InitializeBuildSystem() runs.
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
        OutputLogger.ClearChannel(OutputLogger.SourceBuild);
        RefreshBuildProperties();
        await _startupRunner.RunAsync();
        RefreshBuildProperties();
    }

    private async Task RunBuildSolutionAsync()
    {
        if (_buildSystem is null) return;
        OutputLogger.ClearChannel(OutputLogger.SourceBuild);
        if (_solutionManager.CurrentSolution is null
            || _solutionManager.CurrentSolution.Projects.Count == 0)
        {
            OutputLogger.BuildWarn("No solution or projects loaded — nothing to build.");
            return;
        }
        RefreshBuildProperties();
        var result = await _buildSystem.BuildSolutionAsync();
        _buildErrorListAdapter?.SetDiagnostics(result.Errors.Concat(result.Warnings));
        RefreshBuildProperties();
    }

    private async Task RunBuildProjectAsync()
    {
        if (_buildSystem is null || _solutionManager.CurrentSolution is null) return;
        var startup = _solutionManager.CurrentSolution.StartupProject;
        if (startup is null) return;
        OutputLogger.ClearChannel(OutputLogger.SourceBuild);
        RefreshBuildProperties();
        var result = await _buildSystem.BuildProjectAsync(startup.Id);
        _buildErrorListAdapter?.SetDiagnostics(result.Errors.Concat(result.Warnings));
        RefreshBuildProperties();
    }

    private async Task RunRebuildSolutionAsync()
    {
        if (_buildSystem is null) return;
        OutputLogger.ClearChannel(OutputLogger.SourceBuild);
        if (_solutionManager.CurrentSolution is null
            || _solutionManager.CurrentSolution.Projects.Count == 0)
        {
            OutputLogger.BuildWarn("No solution or projects loaded — nothing to rebuild.");
            return;
        }
        RefreshBuildProperties();
        var result = await _buildSystem.RebuildSolutionAsync();
        _buildErrorListAdapter?.SetDiagnostics(result.Errors.Concat(result.Warnings));
        RefreshBuildProperties();
    }

    private async Task RunRebuildProjectAsync()
    {
        if (_buildSystem is null || _solutionManager.CurrentSolution is null) return;
        var startup = _solutionManager.CurrentSolution.StartupProject;
        if (startup is null) return;
        OutputLogger.ClearChannel(OutputLogger.SourceBuild);
        RefreshBuildProperties();
        var result = await _buildSystem.RebuildProjectAsync(startup.Id);
        _buildErrorListAdapter?.SetDiagnostics(result.Errors.Concat(result.Warnings));
        RefreshBuildProperties();
    }

    private async Task RunCleanSolutionAsync()
    {
        if (_buildSystem is null) return;
        OutputLogger.ClearChannel(OutputLogger.SourceBuild);
        await _buildSystem.CleanSolutionAsync();
        RefreshBuildProperties();
    }

    private async Task RunCleanProjectAsync()
    {
        if (_buildSystem is null || _solutionManager.CurrentSolution is null) return;
        var startup = _solutionManager.CurrentSolution.StartupProject;
        if (startup is null) return;
        OutputLogger.ClearChannel(OutputLogger.SourceBuild);
        await _buildSystem.CleanProjectAsync(startup.Id);
        RefreshBuildProperties();
    }

    // -- Project-specific runners (from SolutionExplorer context menu) -----

    private async Task RunBuildProjectByIdAsync(string projectId)
    {
        if (_buildSystem is null) return;
        OutputLogger.ClearChannel(OutputLogger.SourceBuild);
        RefreshBuildProperties();
        var result = await _buildSystem.BuildProjectAsync(projectId);
        _buildErrorListAdapter?.SetDiagnostics(result.Errors.Concat(result.Warnings));
        RefreshBuildProperties();
    }

    private async Task RunRebuildProjectByIdAsync(string projectId)
    {
        if (_buildSystem is null) return;
        OutputLogger.ClearChannel(OutputLogger.SourceBuild);
        RefreshBuildProperties();
        var result = await _buildSystem.RebuildProjectAsync(projectId);
        _buildErrorListAdapter?.SetDiagnostics(result.Errors.Concat(result.Warnings));
        RefreshBuildProperties();
    }

    private async Task RunCleanProjectByIdAsync(string projectId)
    {
        if (_buildSystem is null) return;
        OutputLogger.ClearChannel(OutputLogger.SourceBuild);
        await _buildSystem.CleanProjectAsync(projectId);
        RefreshBuildProperties();
    }

    private void SetStartupProject(string projectId)
        => _solutionManager.SetStartupProject(projectId);

    /// <summary>
    /// Rebuilds <see cref="StartupProjectNames"/> from the current solution's project list
    /// and refreshes the ComboBox binding. Safe to call from any thread.
    /// </summary>
    internal void RefreshStartupProjectList()
    {
        Dispatcher.InvokeAsync(() =>
        {
            StartupProjectNames.Clear();
            var projects = _solutionManager.CurrentSolution?.Projects;
            if (projects is not null)
                foreach (var p in projects)
                    StartupProjectNames.Add(p.Name);

            OnPropertyChanged(nameof(ActiveStartupProjectName));
            OnPropertyChanged(nameof(CanRunStartupProject));
        });
    }

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

    /// <summary>Minimal inline ICommand for the Ctrl+Shift+B binding.</summary>
    private sealed class RelayCommand(Action<object?> execute) : ICommand
    {
        public bool CanExecute(object? parameter) => true;
        public void Execute(object? parameter) => execute(parameter);
        public event EventHandler? CanExecuteChanged;
    }
}
