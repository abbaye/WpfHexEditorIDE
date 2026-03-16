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
using WpfHexEditor.Editor.Core;
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

    // -----------------------------------------------------------------------
    // Properties (bound in XAML)
    // -----------------------------------------------------------------------

    /// <summary>True when a solution is loaded and no build is running.</summary>
    public bool IsBuildMenuEnabled => _hasSolution && !(_buildSystem?.HasActiveBuild ?? false);

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

        _configManager = new ConfigurationManager();
        _buildSystem   = new BuildSystem.BuildSystem(_solutionManager, _ideEventBus, _configManager);

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

        // Register Ctrl+Shift+B keyboard shortcut.
        var buildGesture = new KeyBinding(
            new RelayCommand(_ => _ = RunBuildSolutionAsync()),
            Key.B, ModifierKeys.Control | ModifierKeys.Shift);
        InputBindings.Add(buildGesture);

        // Wire SolutionExplorer VS build context-menu events.
        if (_solutionExplorerPanel is not null)
        {
            _solutionExplorerPanel.BuildProjectRequested       += (_, id) => _ = RunBuildProjectByIdAsync(id);
            _solutionExplorerPanel.RebuildProjectRequested     += (_, id) => _ = RunRebuildProjectByIdAsync(id);
            _solutionExplorerPanel.CleanProjectRequested       += (_, id) => _ = RunCleanProjectByIdAsync(id);
            _solutionExplorerPanel.SetStartupProjectRequested  += (_, id) => SetStartupProject(id);
        }
    }

    // -----------------------------------------------------------------------
    // Click handlers (bound in MainWindow.xaml)
    // -----------------------------------------------------------------------

    private void OnBuildSolution  (object sender, RoutedEventArgs e) => _ = RunBuildSolutionAsync();
    private void OnBuildProject   (object sender, RoutedEventArgs e) => _ = RunBuildProjectAsync();
    private void OnRebuildSolution(object sender, RoutedEventArgs e) => _ = RunRebuildSolutionAsync();
    private void OnRebuildProject (object sender, RoutedEventArgs e) => _ = RunRebuildProjectAsync();
    private void OnCleanSolution  (object sender, RoutedEventArgs e) => _ = RunCleanSolutionAsync();
    private void OnCleanProject   (object sender, RoutedEventArgs e) => _ = RunCleanProjectAsync();
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

    private async Task RunBuildSolutionAsync()
    {
        if (_buildSystem is null) return;
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
        RefreshBuildProperties();
        var result = await _buildSystem.BuildProjectAsync(startup.Id);
        _buildErrorListAdapter?.SetDiagnostics(result.Errors.Concat(result.Warnings));
        RefreshBuildProperties();
    }

    private async Task RunRebuildSolutionAsync()
    {
        if (_buildSystem is null) return;
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
        RefreshBuildProperties();
        var result = await _buildSystem.RebuildProjectAsync(startup.Id);
        _buildErrorListAdapter?.SetDiagnostics(result.Errors.Concat(result.Warnings));
        RefreshBuildProperties();
    }

    private async Task RunCleanSolutionAsync()
    {
        if (_buildSystem is null) return;
        await _buildSystem.CleanSolutionAsync();
        RefreshBuildProperties();
    }

    private async Task RunCleanProjectAsync()
    {
        if (_buildSystem is null || _solutionManager.CurrentSolution is null) return;
        var startup = _solutionManager.CurrentSolution.StartupProject;
        if (startup is null) return;
        await _buildSystem.CleanProjectAsync(startup.Id);
        RefreshBuildProperties();
    }

    // -- Project-specific runners (from SolutionExplorer context menu) -----

    private async Task RunBuildProjectByIdAsync(string projectId)
    {
        if (_buildSystem is null) return;
        RefreshBuildProperties();
        var result = await _buildSystem.BuildProjectAsync(projectId);
        _buildErrorListAdapter?.SetDiagnostics(result.Errors.Concat(result.Warnings));
        RefreshBuildProperties();
    }

    private async Task RunRebuildProjectByIdAsync(string projectId)
    {
        if (_buildSystem is null) return;
        RefreshBuildProperties();
        var result = await _buildSystem.RebuildProjectAsync(projectId);
        _buildErrorListAdapter?.SetDiagnostics(result.Errors.Concat(result.Warnings));
        RefreshBuildProperties();
    }

    private async Task RunCleanProjectByIdAsync(string projectId)
    {
        if (_buildSystem is null) return;
        await _buildSystem.CleanProjectAsync(projectId);
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
    }

    /// <summary>Minimal inline ICommand for the Ctrl+Shift+B binding.</summary>
    private sealed class RelayCommand(Action<object?> execute) : ICommand
    {
        public bool CanExecute(object? parameter) => true;
        public void Execute(object? parameter) => execute(parameter);
        public event EventHandler? CanExecuteChanged;
    }
}
