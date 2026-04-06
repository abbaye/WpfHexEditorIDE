// ==========================================================
// Project: WpfHexEditor.PluginDev
// File: UI/PluginDevToolbarViewModel.cs
// Author: Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.6
// Created: 2026-03-23
// Description:
//     ViewModel for PluginDevToolbar.
//     Owns the plugin project state machine and exposes commands for
//     Run, Rebuild, Hot-Reload, Stop, and Package operations.
//
// Architecture Notes:
//     Pattern: ViewModel (MVVM) + State Machine (Idle / Building / Running / Error).
//     Commands are ICommand implementations; state changes drive IsEnabled logic.
//     PluginDevLoader and PluginBuildOrchestrator are composed here.
// ==========================================================

using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using WpfHexEditor.PluginDev.Build;
using WpfHexEditor.PluginDev.Loading;
using WpfHexEditor.PluginDev.Panels;
using WpfHexEditor.SDK.Commands;
using WpfHexEditor.Core.ViewModels;

namespace WpfHexEditor.PluginDev.UI;

/// <summary>
/// Plugin project lifecycle states.
/// </summary>
public enum PluginDevState
{
    Idle,
    Building,
    Running,
    Error,
}

/// <summary>
/// ViewModel for <see cref="PluginDevToolbar"/>.
/// Manages the build/load/unload lifecycle for the active plugin project.
/// </summary>
public sealed class PluginDevToolbarViewModel : ViewModelBase, IDisposable
{
    // -----------------------------------------------------------------------
    // Fields
    // -----------------------------------------------------------------------

    private readonly PluginDevLoader          _loader;
    private readonly PluginBuildOrchestrator  _buildOrchestrator;
    private readonly PluginDevLogViewModel    _log;

    private PluginDevState _state = PluginDevState.Idle;
    private string?        _activeProjectPath;
    private string         _selectedConfig = "Debug";
    private string?        _activePluginName;

    // -----------------------------------------------------------------------
    // Construction
    // -----------------------------------------------------------------------

    public PluginDevToolbarViewModel(PluginDevLogViewModel log)
    {
        _log               = log ?? throw new ArgumentNullException(nameof(log));
        _loader            = new PluginDevLoader();
        _buildOrchestrator = new PluginBuildOrchestrator();

        _loader.Loaded   += (_, e) => OnPluginLoaded(e.AssemblyPath);
        _loader.Unloaded += (_, _) => OnPluginUnloaded();

        RunCommand       = new RelayCommand(_ => _ = RunAsync(),       _ => State is PluginDevState.Idle or PluginDevState.Error);
        RebuildCommand   = new RelayCommand(_ => _ = RebuildAsync(),   _ => State is PluginDevState.Idle or PluginDevState.Error or PluginDevState.Running);
        HotReloadCommand = new RelayCommand(_ => _ = HotReloadAsync(), _ => State == PluginDevState.Running);
        StopCommand      = new RelayCommand(_ => Stop(),               _ => State == PluginDevState.Running);
        PackageCommand   = new RelayCommand(_ => Package(),            _ => State is PluginDevState.Idle or PluginDevState.Running);
    }

    // -----------------------------------------------------------------------
    // Properties
    // -----------------------------------------------------------------------

    public PluginDevState State
    {
        get => _state;
        private set
        {
            _state = value;
            OnPropertyChanged();
            InvalidateCommands();
        }
    }

    public string? ActiveProjectPath
    {
        get => _activeProjectPath;
        set { _activeProjectPath = value; OnPropertyChanged(); }
    }

    public string SelectedConfig
    {
        get => _selectedConfig;
        set { _selectedConfig = value; OnPropertyChanged(); }
    }

    public string? ActivePluginName
    {
        get => _activePluginName;
        private set { _activePluginName = value; OnPropertyChanged(); }
    }

    public ObservableCollection<string> AvailableProjects { get; } = [];
    public ObservableCollection<string> Configurations    { get; } = ["Debug", "Release"];

    // -----------------------------------------------------------------------
    // Commands
    // -----------------------------------------------------------------------

    public ICommand RunCommand       { get; }
    public ICommand RebuildCommand   { get; }
    public ICommand HotReloadCommand { get; }
    public ICommand StopCommand      { get; }
    public ICommand PackageCommand   { get; }

    // -----------------------------------------------------------------------
    // Actions
    // -----------------------------------------------------------------------

    private async Task RunAsync()
    {
        if (ActiveProjectPath is null)
        {
            _log.Add(LogLevel.Warning, "Toolbar", "No plugin project selected. Use File > New > Plugin Projectâ€¦ first.");
            return;
        }

        State = PluginDevState.Building;
        _log.Add(LogLevel.Info, "Toolbar", $"Building {System.IO.Path.GetFileName(ActiveProjectPath)}â€¦");

        try
        {
            var result = await _buildOrchestrator.BuildAsync(
                ActiveProjectPath, SelectedConfig, _log);

            if (!result.IsSuccess)
            {
                _log.Add(LogLevel.Error, "Toolbar", $"Build failed â€” {result.Errors.Count} error(s).");
                State = PluginDevState.Error;
                return;
            }

            _log.Add(LogLevel.Info, "Toolbar", "Build succeeded â€” loading pluginâ€¦");
            _loader.LoadPlugin(result.OutputAssembly);
        }
        catch (Exception ex)
        {
            _log.Add(LogLevel.Error, "Toolbar", $"Run exception: {ex.Message}");
            State = PluginDevState.Error;
        }
    }

    private async Task RebuildAsync()
    {
        if (ActiveProjectPath is null)
        {
            _log.Add(LogLevel.Warning, "Toolbar", "No plugin project selected.");
            return;
        }

        if (State == PluginDevState.Running)
            _loader.UnloadPlugin();

        State = PluginDevState.Building;
        _log.Add(LogLevel.Info, "Toolbar", "Rebuildingâ€¦");

        try
        {
            var result = await _buildOrchestrator.BuildAsync(
                ActiveProjectPath, SelectedConfig, _log);

            if (!result.IsSuccess)
            {
                _log.Add(LogLevel.Error, "Toolbar", $"Rebuild failed â€” {result.Errors.Count} error(s).");
                State = PluginDevState.Error;
                return;
            }

            _log.Add(LogLevel.Info, "Toolbar", "Rebuild succeeded.");
            State = PluginDevState.Idle;
        }
        catch (Exception ex)
        {
            _log.Add(LogLevel.Error, "Toolbar", $"Rebuild exception: {ex.Message}");
            State = PluginDevState.Error;
        }
    }

    private async Task HotReloadAsync()
    {
        if (ActiveProjectPath is null) return;

        _log.Add(LogLevel.Info, "Toolbar", "Hot-reload: rebuildingâ€¦");
        State = PluginDevState.Building;

        try
        {
            var result = await _buildOrchestrator.BuildAsync(
                ActiveProjectPath, SelectedConfig, _log);

            if (!result.IsSuccess)
            {
                _log.Add(LogLevel.Error, "Toolbar", $"Hot-reload build failed â€” {result.Errors.Count} error(s).");
                State = PluginDevState.Error;
                return;
            }

            _log.Add(LogLevel.Info, "Toolbar", "Hot-reload: reloading assemblyâ€¦");
            _loader.ReloadPlugin(result.OutputAssembly);
        }
        catch (Exception ex)
        {
            _log.Add(LogLevel.Error, "Toolbar", $"Hot-reload exception: {ex.Message}");
            State = PluginDevState.Error;
        }
    }

    private void Stop()
    {
        _loader.UnloadPlugin();
        _log.Add(LogLevel.Info, "Toolbar", "Plugin stopped.");
    }

    private void Package()
    {
        if (ActiveProjectPath is null)
        {
            _log.Add(LogLevel.Warning, "Toolbar", "No plugin project selected.");
            return;
        }

        var projectDir    = System.IO.Path.GetDirectoryName(ActiveProjectPath) ?? ".";
        var buildOutputDir = System.IO.Path.Combine(projectDir, "bin", SelectedConfig, "net8.0-windows");
        var defaultOut    = System.IO.Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "WpfHexEditor", "Packages");

        var dlg = new PluginPackagePublisherDialog(buildOutputDir, projectDir, defaultOut)
        {
            Owner = System.Windows.Application.Current?.MainWindow,
        };
        dlg.ShowDialog();
    }

    // -----------------------------------------------------------------------
    // Plugin lifecycle callbacks
    // -----------------------------------------------------------------------

    private void OnPluginLoaded(string assemblyPath)
    {
        ActivePluginName = System.IO.Path.GetFileNameWithoutExtension(assemblyPath);
        State = PluginDevState.Running;
        _log.Add(LogLevel.Info, "Toolbar", $"Plugin loaded: {ActivePluginName}");
    }

    private void OnPluginUnloaded()
    {
        State = State == PluginDevState.Building ? PluginDevState.Building : PluginDevState.Idle;
        _log.Add(LogLevel.Info, "Toolbar", "Plugin unloaded.");
    }

    // -----------------------------------------------------------------------
    // INotifyPropertyChanged
    // -----------------------------------------------------------------------


    private static void InvalidateCommands()
        => System.Windows.Input.CommandManager.InvalidateRequerySuggested();

    // -----------------------------------------------------------------------
    // IDisposable
    // -----------------------------------------------------------------------

    public void Dispose() => _loader.Dispose();

}
