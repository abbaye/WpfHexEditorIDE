//////////////////////////////////////////////
// GNU Affero General Public License v3.0 - 2026
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
    private readonly Func<(int warning, int high, int critical, bool enabled,
                           string normalColor, string warningColor, string highColor, string criticalColor)>? _getMemoryThresholds;

    // Metrics-only refresh timer (10 s — lighter than full Rebuild)
    private readonly DispatcherTimer _metricsTimer;

    // Debounce timer for filter text (200 ms)
    private readonly DispatcherTimer _filterDebounce;

    private readonly List<PluginListItemViewModel> _allItems = new();

    private string _filterText = string.Empty;
    private string _rawFilterText = string.Empty;
    private string _sortBy = "Name";
    private PluginListItemViewModel? _selectedPlugin;
    private bool _isInstalling;

    // Global summary metrics (updated on every metrics tick)
    private double _globalCpuPercent;
    private long   _globalMemoryMb;
    private int    _globalRunningCount;
    private int    _globalTotalCount;
    private int    _globalFaultCount;

    public event PropertyChangedEventHandler? PropertyChanged;

    public PluginManagerViewModel(
        WpfPluginHost host, 
        Dispatcher dispatcher,
        Func<(int warning, int high, int critical, bool enabled,
              string normalColor, string warningColor, string highColor, string criticalColor)>? getMemoryThresholds = null)
    {
        _host       = host       ?? throw new ArgumentNullException(nameof(host));
        _dispatcher = dispatcher ?? throw new ArgumentNullException(nameof(dispatcher));
        _getMemoryThresholds = getMemoryThresholds;

        // Metrics timer — only refreshes live CPU/RAM + sparkline history, does NOT rebuild the list
        _metricsTimer = new DispatcherTimer(DispatcherPriority.Background, _dispatcher)
        {
            Interval = TimeSpan.FromSeconds(5)
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

        RefreshCommand             = new RelayCommand(_ => RebuildOnUiThread());
        InstallFromFileCommand     = new RelayCommand(_ => ExecuteInstallFromFile());
        ClearFilterCommand         = new RelayCommand(_ => FilterText = string.Empty,
                                                      _ => !string.IsNullOrEmpty(_rawFilterText));
        ExportDiagnosticsCommand   = new RelayCommand(_ => ExecuteExportDiagnostics());
        ExportCrashReportCommand   = new RelayCommand(
            _ => ExecuteExportCrashReport(),
            _ => _selectedPlugin?.State == PluginState.Faulted);

        Rebuild();

        // Immediately populate metrics so the detail pane shows real values on first open,
        // rather than showing all-zeroes until the first 5-second timer tick.
        foreach (var vm in Plugins) vm.Refresh();
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

    // --- Global summary metrics ---

    public double GlobalCpuPercent
    {
        get => _globalCpuPercent;
        private set { _globalCpuPercent = value; OnPropertyChanged(); }
    }

    public long GlobalMemoryMb
    {
        get => _globalMemoryMb;
        private set { _globalMemoryMb = value; OnPropertyChanged(); }
    }

    /// <summary>E.g. "10/10" — loaded/total.</summary>
    public string RunningLabel => $"{_globalRunningCount}/{_globalTotalCount}";

    public int FaultCount
    {
        get => _globalFaultCount;
        private set { _globalFaultCount = value; OnPropertyChanged(); }
    }

    // --- Commands ---

    public ICommand RefreshCommand           { get; }
    public ICommand InstallFromFileCommand   { get; }
    public ICommand ClearFilterCommand       { get; }
    public ICommand ExportDiagnosticsCommand { get; }
    public ICommand ExportCrashReportCommand { get; }

    // --- Install progress ---

    public bool IsInstalling
    {
        get => _isInstalling;
        private set { _isInstalling = value; OnPropertyChanged(); }
    }

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
                permissionService: _host.Permissions,
                getMemoryThresholds: _getMemoryThresholds,
                initialIsolationMode: _host.GetDeclaredIsolationMode(entry.Manifest),
                onIsolationModeChanged: (id, mode) => _host.SetIsolationOverrideAsync(id, mode)));
        }

        ApplyFilterAndSort();

        // Restore selection if still present
        if (previousId is not null)
            SelectedPlugin = Plugins.FirstOrDefault(p => p.Id == previousId);

        RefreshGlobalMetrics();
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
        RefreshGlobalMetrics();
    }

    private void RefreshGlobalMetrics()
    {
        // CPU: use the host's last sampled process-level value directly.
        // All plugins share the same process-level sample (InProcess mode); reading from the
        // first plugin's diagnostics was equivalent but semantically misleading.
        GlobalCpuPercent = _host.LastSampledCpuPercent;

        // Memory: total managed heap (process-wide).
        GlobalMemoryMb = GC.GetTotalMemory(forceFullCollection: false) / (1024 * 1024);

        var running = Plugins.Count(p => p.State == PluginState.Loaded);
        var total   = Plugins.Count;
        var faults  = Plugins.Count(p => p.State == PluginState.Faulted);

        _globalRunningCount = running;
        _globalTotalCount   = total;

        // Raise RunningLabel only when values change to avoid spurious bindings.
        if (_globalFaultCount != faults)
            FaultCount = faults;
        OnPropertyChanged(nameof(RunningLabel));
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

    // --- Install / Uninstall from file ---

    private async void ExecuteInstallFromFile()
    {
        var dialog = new OpenFileDialog
        {
            Title           = "Install Plugin Package",
            Filter          = "Plugin Package (*.whxplugin)|*.whxplugin|All Files (*.*)|*.*",
            CheckFileExists = true
        };

        if (dialog.ShowDialog() != true) return;
        await InstallFromPathAsync(dialog.FileName);
    }

    /// <summary>Called by code-behind when a .whxplugin is dropped onto the panel.</summary>
    public Task InstallFromDropAsync(string path) => InstallFromPathAsync(path);

    private async Task InstallFromPathAsync(string filePath)
    {
        if (IsInstalling) return;
        IsInstalling = true;
        try
        {
            await Task.Run(() => _host.InstallFromFileAsync(filePath)).ConfigureAwait(true);
            Rebuild();
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Installation failed:\n{ex.Message}",
                "Plugin Install Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
        finally { IsInstalling = false; }
    }

    // --- Export ---

    private void ExecuteExportDiagnostics()
    {
        var dlg = new SaveFileDialog
        {
            Title      = "Export Plugin Diagnostics",
            Filter     = "CSV files (*.csv)|*.csv|JSON files (*.json)|*.json|All files (*.*)|*.*",
            FileName   = $"plugin-diagnostics-{DateTime.Now:yyyyMMdd-HHmmss}.csv"
        };
        if (dlg.ShowDialog() != true) return;

        var plugins  = _host.GetAllPlugins();
        var exporter = new Services.PluginDiagnosticsExporter();
        var isCsv    = System.IO.Path.GetExtension(dlg.FileName)
                             .Equals(".csv", StringComparison.OrdinalIgnoreCase);

        _ = isCsv
            ? exporter.ExportCsvAsync(plugins, dlg.FileName)
            : exporter.ExportJsonAsync(plugins, dlg.FileName);
    }

    private void ExecuteExportCrashReport()
    {
        if (_selectedPlugin is null) return;
        var entry = _host.GetPlugin(_selectedPlugin.Id);
        if (entry is null) return;

        var dlg = new SaveFileDialog
        {
            Title      = $"Export Crash Report — {_selectedPlugin.Name}",
            Filter     = "Text files (*.txt)|*.txt|All files (*.*)|*.*",
            FileName   = $"crash-{_selectedPlugin.Id}-{DateTime.Now:yyyyMMdd-HHmmss}.txt"
        };
        if (dlg.ShowDialog() != true) return;

        var exporter = new Services.PluginDiagnosticsExporter();
        _ = exporter.ExportCrashReportAsync(entry, dlg.FileName);
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
