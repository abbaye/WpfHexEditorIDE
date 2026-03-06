// ==========================================================
// Project: WpfHexEditor.PluginHost
// File: PluginManagerViewModel.cs
// Author: Auto
// Created: 2026-03-06
// Description:
//     Master ViewModel for the Plugin Manager document tab.
//     Maintains the filtered/sorted list of plugins and delegates
//     all lifecycle actions to PluginHost.
//
// Architecture Notes:
//     MVVM — PluginManagerControl data-binds entirely to this ViewModel.
//     DispatcherTimer (2s) calls Refresh() on all item VMs for live metrics.
//     Filter is applied in-memory over the full list on FilterText change.
//
// ==========================================================

using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Threading;

namespace WpfHexEditor.PluginHost.UI;

/// <summary>
/// Master ViewModel for the Plugin Manager panel.
/// </summary>
public sealed class PluginManagerViewModel : INotifyPropertyChanged, IDisposable
{
    private readonly WpfPluginHost _host;
    private readonly DispatcherTimer _refreshTimer;
    private readonly List<PluginListItemViewModel> _allItems = new();

    private string _filterText = string.Empty;
    private string _sortBy = "Name";
    private PluginListItemViewModel? _selectedPlugin;

    public event PropertyChangedEventHandler? PropertyChanged;

    public PluginManagerViewModel(WpfPluginHost host, Dispatcher dispatcher)
    {
        _host = host ?? throw new ArgumentNullException(nameof(host));

        _refreshTimer = new DispatcherTimer(DispatcherPriority.Background, dispatcher)
        {
            Interval = TimeSpan.FromSeconds(2)
        };
        _refreshTimer.Tick += OnRefreshTick;
        _refreshTimer.Start();

        Rebuild();
    }

    public ObservableCollection<PluginListItemViewModel> Plugins { get; } = new();

    public PluginListItemViewModel? SelectedPlugin
    {
        get => _selectedPlugin;
        set { _selectedPlugin = value; OnPropertyChanged(); }
    }

    public string FilterText
    {
        get => _filterText;
        set
        {
            _filterText = value;
            OnPropertyChanged();
            ApplyFilterAndSort();
        }
    }

    public string SortBy
    {
        get => _sortBy;
        set
        {
            _sortBy = value;
            OnPropertyChanged();
            ApplyFilterAndSort();
        }
    }

    public IReadOnlyList<string> SortOptions { get; } = new[] { "Name", "State", "CPU", "InitTime" };

    // --- Plugin lifecycle callbacks passed into item VMs ---

    public void EnablePlugin(string id)
        => _ = Task.Run(async () => await _host.EnablePluginAsync(id)).ContinueWith(_ => Rebuild());

    public void DisablePlugin(string id)
        => _ = Task.Run(async () => await _host.DisablePluginAsync(id)).ContinueWith(_ => Rebuild());

    public void ReloadPlugin(string id)
        => _ = Task.Run(async () => await _host.ReloadPluginAsync(id)).ContinueWith(_ => Rebuild());

    public void UninstallPlugin(string id)
        => _ = Task.Run(async () => await _host.UninstallPluginAsync(id)).ContinueWith(_ => Rebuild());

    // --- Internal ---

    private void Rebuild()
    {
        _allItems.Clear();
        foreach (var entry in _host.GetAllPlugins())
        {
            _allItems.Add(new PluginListItemViewModel(entry,
                onEnable: EnablePlugin,
                onDisable: DisablePlugin,
                onReload: ReloadPlugin,
                onUninstall: UninstallPlugin));
        }
        ApplyFilterAndSort();
    }

    private void ApplyFilterAndSort()
    {
        var filtered = string.IsNullOrWhiteSpace(_filterText)
            ? _allItems
            : _allItems.Where(vm =>
                vm.Name.Contains(_filterText, StringComparison.OrdinalIgnoreCase) ||
                vm.Id.Contains(_filterText, StringComparison.OrdinalIgnoreCase) ||
                vm.Author.Contains(_filterText, StringComparison.OrdinalIgnoreCase));

        IOrderedEnumerable<PluginListItemViewModel> sorted = SortBy switch
        {
            "State" => filtered.OrderBy(vm => vm.State),
            "CPU" => filtered.OrderByDescending(vm => vm.CpuPercent),
            "InitTime" => filtered.OrderByDescending(vm => vm.InitTimeMs),
            _ => filtered.OrderBy(vm => vm.Name)
        };

        Plugins.Clear();
        foreach (var vm in sorted) Plugins.Add(vm);
    }

    private void OnRefreshTick(object? sender, EventArgs e)
    {
        foreach (var vm in Plugins) vm.Refresh();
    }

    public void Dispose()
    {
        _refreshTimer.Stop();
        _refreshTimer.Tick -= OnRefreshTick;
    }

    private void OnPropertyChanged([CallerMemberName] string? name = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
