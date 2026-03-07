// ==========================================================
// Project: WpfHexEditor.App
// File: PluginQuickStatusViewModel.cs
// Author: Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.6
// Created: 2026-03-07
// Description:
//     Lightweight ViewModel for the quick-status popup anchored to the
//     StatusBar plugin indicator. Shows a compact plugin list with per-item
//     state colors plus Manage and Install shortcut actions.
//
// Architecture Notes:
//     Not MVVM-heavy on purpose — this is a transient popup VM with no
//     persistent state. It reads a snapshot from WpfPluginHost on construction
//     and refreshes on demand via Refresh().
// ==========================================================

using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using WpfHexEditor.PluginHost;
using WpfHexEditor.SDK.Models;

namespace WpfHexEditor.App.ViewModels;

/// <summary>
/// Single plugin row shown in the quick-status popup.
/// </summary>
public sealed class QuickPluginRow
{
    public string Name { get; init; } = string.Empty;
    public string StateLabel { get; init; } = string.Empty;
    public string StateDot { get; init; } = string.Empty;  // Segoe MDL2 glyph
    public string StateColor { get; init; } = "#9CA3AF";
    public bool IsSlow { get; init; }
}

/// <summary>
/// ViewModel for <see cref="Controls.PluginQuickStatusPopup"/>.
/// </summary>
public sealed class PluginQuickStatusViewModel : INotifyPropertyChanged
{
    private readonly WpfPluginHost _host;
    private readonly Action _onManage;
    private readonly Action _onInstall;

    public event PropertyChangedEventHandler? PropertyChanged;

    public PluginQuickStatusViewModel(WpfPluginHost host, Action onManage, Action onInstall)
    {
        _host      = host      ?? throw new ArgumentNullException(nameof(host));
        _onManage  = onManage  ?? throw new ArgumentNullException(nameof(onManage));
        _onInstall = onInstall ?? throw new ArgumentNullException(nameof(onInstall));

        ManageCommand  = new QuickRelayCommand(_ => _onManage());
        InstallCommand = new QuickRelayCommand(_ => _onInstall());

        Refresh();
    }

    // -- Bindable properties -----------------------------------------------

    public ObservableCollection<QuickPluginRow> Rows { get; } = new();

    private string _summaryText = string.Empty;
    public string SummaryText
    {
        get => _summaryText;
        private set { _summaryText = value; OnPropertyChanged(); }
    }

    public System.Windows.Input.ICommand ManageCommand  { get; }
    public System.Windows.Input.ICommand InstallCommand { get; }

    // -- Refresh snapshot --------------------------------------------------

    public void Refresh()
    {
        var plugins = _host.GetAllPlugins();

        Rows.Clear();
        foreach (var entry in plugins)
        {
            Rows.Add(new QuickPluginRow
            {
                Name       = entry.Manifest.Name,
                StateLabel = StateLabel(entry.State),
                StateDot   = StateDotGlyph(entry.State),
                StateColor = StateColor(entry.State),
                IsSlow     = false   // SlowPluginDetected wired separately if needed
            });
        }

        var loaded = plugins.Count(p => p.State == PluginState.Loaded);
        var faults = plugins.Count(p => p.State == PluginState.Faulted);

        SummaryText = faults > 0
            ? $"{faults} fault{(faults == 1 ? "" : "s")} — {loaded}/{plugins.Count} running"
            : plugins.Count == 0
                ? "No plugins loaded"
                : $"{loaded}/{plugins.Count} plugins running";
    }

    // -- Helpers -----------------------------------------------------------

    private static string StateLabel(PluginState state) => state switch
    {
        PluginState.Loaded       => "Running",
        PluginState.Loading      => "Loading",
        PluginState.Disabled     => "Disabled",
        PluginState.Faulted      => "Error",
        PluginState.Incompatible => "Incompatible",
        PluginState.Unloaded     => "Unloaded",
        _                        => "Unknown"
    };

    private static string StateDotGlyph(PluginState state) => state switch
    {
        PluginState.Loaded   => "\uE915",   // CheckMark circle
        PluginState.Faulted  => "\uE783",   // Error badge
        PluginState.Disabled => "\uECA5",   // Blocked
        _                    => "\uE91F"    // Circle outline
    };

    private static string StateColor(PluginState state) => state switch
    {
        PluginState.Loaded       => "#22C55E",
        PluginState.Loading      => "#F59E0B",
        PluginState.Disabled     => "#6B7280",
        PluginState.Faulted      => "#EF4444",
        PluginState.Incompatible => "#F97316",
        _                        => "#9CA3AF"
    };

    private void OnPropertyChanged([CallerMemberName] string? name = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}

/// <summary>Minimal relay command for the popup VM.</summary>
internal sealed class QuickRelayCommand : System.Windows.Input.ICommand
{
    private readonly Action<object?> _execute;

    public QuickRelayCommand(Action<object?> execute) => _execute = execute;

    public event EventHandler? CanExecuteChanged { add { } remove { } }
    public bool CanExecute(object? parameter) => true;
    public void Execute(object? parameter) => _execute(parameter);
}
