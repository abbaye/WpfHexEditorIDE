// ==========================================================
// Project: WpfHexEditor.PluginHost
// File: PluginListItemViewModel.cs
// Author: Auto
// Created: 2026-03-06
// Description:
//     ViewModel for a single plugin row in the PluginManagerControl list.
//     Exposes live diagnostics, state badge, and action commands.
//     Implements INotifyPropertyChanged for binding support.
//
// Architecture Notes:
//     MVVM pattern — wraps PluginEntry, exposes formatted display properties.
//     Commands delegate to PluginManagerViewModel callbacks for single-source-of-truth.
//     Diagnostics are refreshed via Refresh() called by the ViewModel's timer.
//
// ==========================================================

using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using WpfHexEditor.SDK.Models;

namespace WpfHexEditor.PluginHost.UI;

/// <summary>
/// ViewModel for a single plugin entry in the Plugin Manager list.
/// </summary>
public sealed class PluginListItemViewModel : INotifyPropertyChanged
{
    private readonly PluginEntry _entry;

    // Injected command callbacks from PluginManagerViewModel
    private readonly Action<string> _onEnable;
    private readonly Action<string> _onDisable;
    private readonly Action<string> _onReload;
    private readonly Action<string> _onUninstall;

    public event PropertyChangedEventHandler? PropertyChanged;

    public PluginListItemViewModel(
        PluginEntry entry,
        Action<string> onEnable,
        Action<string> onDisable,
        Action<string> onReload,
        Action<string> onUninstall)
    {
        _entry = entry ?? throw new ArgumentNullException(nameof(entry));
        _onEnable = onEnable;
        _onDisable = onDisable;
        _onReload = onReload;
        _onUninstall = onUninstall;

        EnableCommand = new RelayCommand(_ => _onEnable(Id), _ => State == PluginState.Disabled);
        DisableCommand = new RelayCommand(_ => _onDisable(Id), _ => State == PluginState.Loaded);
        ReloadCommand = new RelayCommand(_ => _onReload(Id), _ => State is PluginState.Loaded or PluginState.Faulted or PluginState.Disabled);
        UninstallCommand = new RelayCommand(_ => _onUninstall(Id));
    }

    public string Id => _entry.Manifest.Id;
    public string Name => _entry.Manifest.Name;
    public string Version => _entry.Manifest.Version;
    public string Author => _entry.Manifest.Author;
    public string Publisher => _entry.Manifest.Publisher;
    public bool IsTrustedPublisher => _entry.Manifest.TrustedPublisher;
    public string Description => _entry.Manifest.Description;
    public string IsolationMode => _entry.Manifest.IsolationMode.ToString();

    public PluginState State => _entry.State;

    public string StateLabel => _entry.State switch
    {
        PluginState.Loaded => "Running",
        PluginState.Loading => "Loading...",
        PluginState.Disabled => "Disabled",
        PluginState.Faulted => "Error",
        PluginState.Incompatible => "Incompatible",
        PluginState.Unloaded => "Unloaded",
        _ => "Unknown"
    };

    public string StateBadgeColor => _entry.State switch
    {
        PluginState.Loaded => "#22C55E",   // green
        PluginState.Loading => "#F59E0B",  // amber
        PluginState.Disabled => "#6B7280", // gray
        PluginState.Faulted => "#EF4444",  // red
        PluginState.Incompatible => "#F97316", // orange
        _ => "#9CA3AF"
    };

    public string? FaultMessage => _entry.FaultException?.Message;

    // Live diagnostics
    public double CpuPercent { get; private set; }
    public long MemoryMb { get; private set; }
    public double InitTimeMs { get; private set; }
    public double AvgExecMs { get; private set; }

    public string LoadedSince => _entry.LoadedAt.HasValue
        ? _entry.LoadedAt.Value.ToString("HH:mm:ss")
        : "—";

    // Commands
    public ICommand EnableCommand { get; }
    public ICommand DisableCommand { get; }
    public ICommand ReloadCommand { get; }
    public ICommand UninstallCommand { get; }

    /// <summary>
    /// Called periodically by PluginManagerViewModel to refresh live metrics.
    /// </summary>
    public void Refresh()
    {
        var snap = _entry.Diagnostics.GetLatest();
        if (snap is not null)
        {
            CpuPercent = snap.CpuPercent;
            MemoryMb = snap.MemoryBytes / (1024 * 1024);
            AvgExecMs = _entry.Diagnostics.AverageExecutionTime.TotalMilliseconds;
        }
        InitTimeMs = _entry.InitDuration.TotalMilliseconds;

        OnPropertyChanged(nameof(State));
        OnPropertyChanged(nameof(StateLabel));
        OnPropertyChanged(nameof(StateBadgeColor));
        OnPropertyChanged(nameof(CpuPercent));
        OnPropertyChanged(nameof(MemoryMb));
        OnPropertyChanged(nameof(AvgExecMs));
        OnPropertyChanged(nameof(InitTimeMs));
        OnPropertyChanged(nameof(FaultMessage));

        ((RelayCommand)EnableCommand).RaiseCanExecuteChanged();
        ((RelayCommand)DisableCommand).RaiseCanExecuteChanged();
        ((RelayCommand)ReloadCommand).RaiseCanExecuteChanged();
    }

    private void OnPropertyChanged([CallerMemberName] string? name = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));

    // Minimal ICommand relay
    private sealed class RelayCommand : ICommand
    {
        private readonly Action<object?> _execute;
        private readonly Func<object?, bool>? _canExecute;

        public event EventHandler? CanExecuteChanged;

        public RelayCommand(Action<object?> execute, Func<object?, bool>? canExecute = null)
        {
            _execute = execute;
            _canExecute = canExecute;
        }

        public bool CanExecute(object? parameter) => _canExecute?.Invoke(parameter) ?? true;
        public void Execute(object? parameter) => _execute(parameter);
        public void RaiseCanExecuteChanged() => CanExecuteChanged?.Invoke(this, EventArgs.Empty);
    }
}
