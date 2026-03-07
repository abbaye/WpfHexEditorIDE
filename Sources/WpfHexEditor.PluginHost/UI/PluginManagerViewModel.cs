//////////////////////////////////////////////
// Apache 2.0  - 2026
// Author : Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.6
//////////////////////////////////////////////

using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Input;
using System.Windows.Threading;
using Microsoft.Win32;
using WpfHexEditor.SDK.Models;

namespace WpfHexEditor.PluginHost.UI;

/// <summary>
/// Master ViewModel for the Plugin Manager panel.
/// Subscribes to WpfPluginHost events to keep the list in sync with the live plugin state.
/// All Rebuild/Refresh calls are marshalled to the Dispatcher thread.
/// </summary>
public sealed class PluginManagerViewModel : INotifyPropertyChanged, IDisposable
{
    private readonly WpfPluginHost _host;
    private readonly Dispatcher _dispatcher;

    // Metrics-only refresh timer (10 s — lighter than full Rebuild)
    private readonly DispatcherTimer _metricsTimer;

    // Debounce timer for filter text (200 ms)
    private readonly DispatcherTimer _filterDebounce;

    private readonly List<PluginListItemViewModel> _allItems = new();

    private string _filterText = string.Empty;
    private string _rawFilterText = string.Empty;
    private string _sortBy = "Name";
    private PluginListItemViewModel? _selectedPlugin;

    public event PropertyChangedEventHandler? PropertyChanged;

    public PluginManagerViewModel(WpfPluginHost host, Dispatcher dispatcher)
    {
        _host       = host       ?? throw new ArgumentNullException(nameof(host));
        _dispatcher = dispatcher ?? throw new ArgumentNullException(nameof(dispatcher));

        // Metrics timer — only refreshes live CPU/RAM, does NOT rebuild the list
        _metricsTimer = new DispatcherTimer(DispatcherPriority.Background, _dispatcher)
        {
            Interval = TimeSpan.FromSeconds(10)
        };
        _metricsTimer.Tick += OnMetricsTick;
        _metricsTimer.Start();

        // Filter debounce timer — rebuilds filtered view after user stops typing
        _filterDebounce = new DispatcherTimer(DispatcherPriority.Background, _dispatcher)
        {
            Interval = TimeSpan.FromMilliseconds(200)
        };
        _filterDebounce.Tick += OnFilterDebounced;

        // Keep list in sync with plugin lifecycle events
        _host.PluginLoaded   += OnHostPluginChanged;
        _host.PluginUnloaded += OnHostPluginChanged;
        _host.SlowPluginDetected += OnHostSlowPlugin;

        RefreshCommand         = new RelayCommand(_ => RebuildOnUiThread());
        InstallFromFileCommand = new RelayCommand(_ => ExecuteInstallFromFile());
        ClearFilterCommand     = new RelayCommand(_ => FilterText = string.Empty,
                                                  _ => !string.IsNullOrEmpty(_rawFilterText));

        Rebuild();
    }

    // --- Observable collections ---

    public ObservableCollection<PluginListItemViewModel> Plugins { get; } = new();

    // --- Bindable properties ---

    public PluginListItemViewModel? SelectedPlugin
    {
        get => _selectedPlugin;
        set { _selectedPlugin = value; OnPropertyChanged(); }
    }

    public string FilterText
    {
        get => _rawFilterText;
        set
        {
            if (_rawFilterText == value) return;
            _rawFilterText = value;
            OnPropertyChanged();
            // Debounce: restart timer each keystroke
            _filterDebounce.Stop();
            _filterDebounce.Start();
            ((RelayCommand)ClearFilterCommand).RaiseCanExecuteChanged();
        }
    }

    public string SortBy
    {
        get => _sortBy;
        set
        {
            if (_sortBy == value) return;
            _sortBy = value;
            OnPropertyChanged();
            ApplyFilterAndSort();
        }
    }

    public IReadOnlyList<string> SortOptions { get; } = ["Name", "State", "CPU", "InitTime"];

    // --- Commands ---

    public ICommand RefreshCommand         { get; }
    public ICommand InstallFromFileCommand { get; }
    public ICommand ClearFilterCommand     { get; }

    // --- Plugin lifecycle callbacks (called from PluginListItemViewModel commands) ---

    public void EnablePlugin(string id)    => RunLifecycleAndRebuild(() => _host.EnablePluginAsync(id));
    public void DisablePlugin(string id)   => RunLifecycleAndRebuild(() => _host.DisablePluginAsync(id));
    public void ReloadPlugin(string id)    => RunLifecycleAndRebuild(() => _host.ReloadPluginAsync(id));
    public void UninstallPlugin(string id) => RunLifecycleAndRebuild(() => _host.UninstallPluginAsync(id));

    // --- Host event handlers ---

    private void OnHostPluginChanged(object? sender, EventArgs e)
        => _dispatcher.InvokeAsync(Rebuild, DispatcherPriority.Background);

    private void OnHostSlowPlugin(object? sender, Monitoring.SlowPluginDetectedEventArgs e)
    {
        _dispatcher.InvokeAsync(() =>
        {
            var vm = _allItems.FirstOrDefault(i => i.Id == e.PluginId);
            if (vm is not null) vm.IsSlow = true;
        });
    }

    // --- Internal ---

    private void Rebuild()
    {
        // Must be called on Dispatcher thread (ObservableCollection)
        var previousId = _selectedPlugin?.Id;

        _allItems.Clear();
        foreach (var entry in _host.GetAllPlugins())
        {
            _allItems.Add(new PluginListItemViewModel(entry,
                onEnable: EnablePlugin,
                onDisable: DisablePlugin,
                onReload: ReloadPlugin,
                onUninstall: UninstallPlugin,
                permissionService: _host.Permissions));
        }

        ApplyFilterAndSort();

        // Restore selection if still present
        if (previousId is not null)
            SelectedPlugin = Plugins.FirstOrDefault(p => p.Id == previousId);
    }

    private void RebuildOnUiThread()
        => _dispatcher.InvokeAsync(Rebuild, DispatcherPriority.Background);

    private void ApplyFilterAndSort()
    {
        var filtered = string.IsNullOrWhiteSpace(_rawFilterText)
            ? _allItems
            : _allItems.Where(vm =>
                vm.Name.Contains(_rawFilterText, StringComparison.OrdinalIgnoreCase) ||
                vm.Id.Contains(_rawFilterText, StringComparison.OrdinalIgnoreCase) ||
                vm.Author.Contains(_rawFilterText, StringComparison.OrdinalIgnoreCase));

        // Consistent sort: ascending for Name/State, descending for metrics
        IOrderedEnumerable<PluginListItemViewModel> sorted = SortBy switch
        {
            "State"    => filtered.OrderBy(vm => vm.StateLabel),
            "CPU"      => filtered.OrderByDescending(vm => vm.CpuPercent),
            "InitTime" => filtered.OrderByDescending(vm => vm.InitTimeMs),
            _          => filtered.OrderBy(vm => vm.Name)
        };

        // Re-sync ObservableCollection without clear (preserves scroll position)
        var newList = sorted.ToList();
        for (int i = 0; i < newList.Count; i++)
        {
            if (i < Plugins.Count)
            {
                if (!ReferenceEquals(Plugins[i], newList[i]))
                    Plugins[i] = newList[i];
            }
            else
            {
                Plugins.Add(newList[i]);
            }
        }
        while (Plugins.Count > newList.Count)
            Plugins.RemoveAt(Plugins.Count - 1);
    }

    private void OnMetricsTick(object? sender, EventArgs e)
    {
        foreach (var vm in Plugins) vm.Refresh();
    }

    private void OnFilterDebounced(object? sender, EventArgs e)
    {
        _filterDebounce.Stop();
        _filterText = _rawFilterText;
        ApplyFilterAndSort();
    }

    /// <summary>
    /// Runs an async plugin lifecycle operation on a background thread,
    /// then rebuilds the plugin list on the Dispatcher thread when done.
    /// </summary>
    private void RunLifecycleAndRebuild(Func<Task> operation)
    {
        _ = Task.Run(async () =>
        {
            try { await operation().ConfigureAwait(false); }
            catch { /* individual errors surfaced by WpfPluginHost events */ }
        }).ContinueWith(_ => _dispatcher.InvokeAsync(Rebuild, DispatcherPriority.Background));
    }

    // --- Install from file (must show dialog on UI thread) ---

    private async void ExecuteInstallFromFile()
    {
        // OpenFileDialog MUST run on the UI thread
        var dialog = new OpenFileDialog
        {
            Title           = "Install Plugin Package",
            Filter          = "Plugin Package (*.whxplugin)|*.whxplugin|All Files (*.*)|*.*",
            CheckFileExists = true
        };

        if (dialog.ShowDialog() != true) return;

        var filePath = dialog.FileName;

        // Install on background thread, then rebuild on UI thread
        try
        {
            await Task.Run(() => _host.InstallFromFileAsync(filePath)).ConfigureAwait(true);
            Rebuild(); // back on UI thread via ConfigureAwait(true)
        }
        catch (Exception ex)
        {
            // MessageBox must be called on UI thread — we're already there (ConfigureAwait(true))
            MessageBox.Show(
                $"Installation failed:\n{ex.Message}",
                "Plugin Install Error",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
        }
    }

    public void Dispose()
    {
        _metricsTimer.Stop();
        _metricsTimer.Tick -= OnMetricsTick;
        _filterDebounce.Stop();
        _filterDebounce.Tick -= OnFilterDebounced;
        _host.PluginLoaded       -= OnHostPluginChanged;
        _host.PluginUnloaded     -= OnHostPluginChanged;
        _host.SlowPluginDetected -= OnHostSlowPlugin;
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
            _execute    = execute;
            _canExecute = canExecute;
        }

        public bool CanExecute(object? parameter) => _canExecute?.Invoke(parameter) ?? true;
        public void Execute(object? parameter)     => _execute(parameter);
        public void RaiseCanExecuteChanged()       => CanExecuteChanged?.Invoke(this, EventArgs.Empty);
    }
}
