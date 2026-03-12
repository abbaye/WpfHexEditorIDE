//////////////////////////////////////////////
// GNU Affero General Public License v3.0 - 2026
// Author : Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.6
//////////////////////////////////////////////

using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using WpfHexEditor.PluginHost.Services;
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
    private readonly PermissionService? _permissionService;

    // Delegate to get memory thresholds from settings (injected to avoid circular dependency)
    private readonly Func<(int warning, int high, int critical, bool enabled, 
                           string normalColor, string warningColor, string highColor, string criticalColor)>? _getMemoryThresholds;

    public event PropertyChangedEventHandler? PropertyChanged;

    public PluginListItemViewModel(
        PluginEntry entry,
        Action<string> onEnable,
        Action<string> onDisable,
        Action<string> onReload,
        Action<string> onUninstall,
        PermissionService? permissionService = null,
        Func<(int warning, int high, int critical, bool enabled, 
              string normalColor, string warningColor, string highColor, string criticalColor)>? getMemoryThresholds = null)
    {
        _entry = entry ?? throw new ArgumentNullException(nameof(entry));
        _onEnable = onEnable;
        _onDisable = onDisable;
        _onReload = onReload;
        _onUninstall = onUninstall;
        _permissionService = permissionService;
        _getMemoryThresholds = getMemoryThresholds;

        EnableCommand = new RelayCommand(_ => _onEnable(Id), _ => State == PluginState.Disabled);
        DisableCommand = new RelayCommand(_ => _onDisable(Id), _ => State == PluginState.Loaded);
        ReloadCommand = new RelayCommand(_ => _onReload(Id), _ => State is PluginState.Loaded or PluginState.Faulted or PluginState.Disabled);
        UninstallCommand = new RelayCommand(_ => _onUninstall(Id));

        Permissions = BuildPermissions();

        // Eagerly populate InitTimeMs so the detail pane never shows "0 ms"
        // before the first 5-second metrics timer tick.
        InitTimeMs = _entry.InitDuration.TotalMilliseconds;
    }

    public string Id => _entry.Manifest.Id;
    public string Name => _entry.Manifest.Name;
    public string Version => _entry.Manifest.Version;
    public string VersionLabel => string.IsNullOrEmpty(_entry.Manifest.Version) ? "—" : $"v{_entry.Manifest.Version}";
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

    /// <summary>
    /// Set to true by PluginManagerViewModel when SlowPluginDetected fires for this plugin.
    /// Drives the "SLOW" badge visibility in the list.
    /// </summary>
    private bool _isSlow;
    public bool IsSlow
    {
        get => _isSlow;
        set { _isSlow = value; OnPropertyChanged(); }
    }

    /// <summary>Category label used for grouping in the plugin list.</summary>
    public string CategoryLabel => _entry.Manifest.TrustedPublisher ? "Official" : "Community";

    // Live diagnostics
    public double CpuPercent { get; private set; }
    public long MemoryMb { get; private set; }
    public double InitTimeMs { get; private set; }
    public double AvgExecMs { get; private set; }

    // Memory alert properties
    private string _memoryAlertColor = "#22C55E";
    private string _memoryAlertIcon = "🟢";
    private string _memoryAlertMessage = string.Empty;

    public string MemoryAlertColor
    {
        get => _memoryAlertColor;
        private set { _memoryAlertColor = value; OnPropertyChanged(); }
    }

    public string MemoryAlertIcon
    {
        get => _memoryAlertIcon;
        private set { _memoryAlertIcon = value; OnPropertyChanged(); }
    }

    public string MemoryAlertMessage
    {
        get => _memoryAlertMessage;
        private set { _memoryAlertMessage = value; OnPropertyChanged(); }
    }

    // Rolling history for sparkline charts (60-point rolling window).
    private const int HistoryCapacity = 60;
    public ObservableCollection<double> CpuHistory    { get; } = new();
    public ObservableCollection<double> MemoryHistory { get; } = new();

    public double PeakCpuPercent { get; private set; }
    public long   PeakMemoryMb  { get; private set; }

    public string LoadedSince => _entry.LoadedAt.HasValue
        ? _entry.LoadedAt.Value.ToString("HH:mm:ss")
        : "—";

    public TimeSpan Uptime => _entry.Diagnostics.Uptime;

    public string UptimeLabel
    {
        get
        {
            var u = _entry.Diagnostics.Uptime;
            if (u.TotalSeconds < 1) return "—";
            return u.TotalHours >= 1
                ? $"{(int)u.TotalHours:D2}:{u.Minutes:D2}:{u.Seconds:D2}"
                : $"{u.Minutes:D2}:{u.Seconds:D2}";
        }
    }

    // Commands
    public ICommand EnableCommand { get; }
    public ICommand DisableCommand { get; }
    public ICommand ReloadCommand { get; }
    public ICommand UninstallCommand { get; }

    // -- Permissions -----------------------------------------------------------

    /// <summary>Interactive permission rows shown in the Permissions tab.</summary>
    public IReadOnlyList<PluginPermissionItemViewModel> Permissions { get; }

    private IReadOnlyList<PluginPermissionItemViewModel> BuildPermissions()
    {
        static (PluginPermission perm, string label, string description) Desc(
            PluginPermission p, string label, string desc) => (p, label, desc);

        var defs = new[]
        {
            Desc(PluginPermission.AccessHexEditor,   "HexEditor Access",  "Read hex content, selection, and offset"),
            Desc(PluginPermission.AccessFileSystem,  "File System",       "Read and write files on disk"),
            Desc(PluginPermission.AccessNetwork,     "Network",           "Make outbound network requests"),
            Desc(PluginPermission.AccessCodeEditor,  "Code Editor",       "Read and interact with the active CodeEditor"),
            Desc(PluginPermission.RegisterMenus,     "Register UI",       "Add menus, panels, and toolbar items"),
            Desc(PluginPermission.WriteOutput,       "Write Output",      "Write to the Output panel"),
            Desc(PluginPermission.WriteErrorPanel,   "Write Errors",      "Write to the Error panel"),
            Desc(PluginPermission.AccessSettings,    "Settings Access",   "Read and write own settings section"),
        };

        var items = new List<PluginPermissionItemViewModel>();
        foreach (var (perm, label, desc) in defs)
        {
            bool granted = _permissionService?.IsGranted(Id, perm) ?? false;
            items.Add(new PluginPermissionItemViewModel(Id, perm, label, desc, granted, _permissionService));
        }
        return items;
    }

    // -- Plugin options --------------------------------------------------------

    /// <summary>True if the plugin instance is loaded and exposes an options page.</summary>
    public bool HasOptions => _entry.Instance is not null && _entry.Instance is SDK.Contracts.IPluginWithOptions;

    private System.Windows.FrameworkElement? _optionsPage;
    // Set to true on first failed CreateOptionsPage() call so we never retry.
    // Without this flag, every selection of the plugin re-throws and stops the VS debugger.
    private bool _optionsPageFailed;

    /// <summary>
    /// Lazily created options page control. Null if the plugin has no options, is not loaded,
    /// or if CreateOptionsPage() threw on the first attempt (failure is not retried).
    /// </summary>
    public System.Windows.FrameworkElement? OptionsPage
    {
        get
        {
            if (_optionsPage is not null) return _optionsPage;
            if (_optionsPageFailed)       return null;
            if (_entry.Instance is null)  return null;
            if (_entry.Instance is not SDK.Contracts.IPluginWithOptions opts) return null;

            // LoadOptions/CreateOptionsPage may do I/O — call synchronously here but OK since
            // this getter is triggered by tab selection (UI thread, user interaction, not hot path).
            // Guard: a buggy plugin must not crash the host — catch and swallow any plugin exception.
            // TODO: Put the Debug in the output panel of App instead of the VS debug output, which is invisible to most users and causes crashes when no debugger is attached.
            try
            {
                opts.LoadOptions();

                _optionsPage = opts.CreateOptionsPage();
            }
            catch (Exception ex)
            {
                _optionsPageFailed = true;
                System.Diagnostics.Debug.WriteLine(
                    $"[PluginManager] CreateOptionsPage threw for plugin '{_entry.Manifest.Id}': {ex}");
                
                return null;
            }

            return _optionsPage;
        }
    }

    /// <summary>
    /// Called periodically by PluginManagerViewModel to refresh live metrics and rolling history.
    /// </summary>
    public void Refresh()
    {
        var snap = _entry.Diagnostics.GetLatest();
        if (snap is not null)
        {
            CpuPercent = snap.CpuPercent;
            MemoryMb   = snap.MemoryBytes / (1024 * 1024);
            AvgExecMs  = _entry.Diagnostics.AverageExecutionTime.TotalMilliseconds;

            PushHistory(CpuHistory,    snap.CpuPercent);
            PushHistory(MemoryHistory, snap.MemoryBytes / (1024.0 * 1024.0));

            // Use rolling-buffer max (consistent with PluginMonitoringViewModel) so that
            // artificially high peaks from startup age out of the buffer naturally.
            var rollingPeakCpu = _entry.Diagnostics.PeakCpu();
            if (Math.Abs(rollingPeakCpu - PeakCpuPercent) > 0.01)
            {
                PeakCpuPercent = rollingPeakCpu;
                OnPropertyChanged(nameof(PeakCpuPercent));
            }

            var rollingPeakMem = _entry.Diagnostics.PeakMemoryBytes() / (1024 * 1024);
            if (rollingPeakMem != PeakMemoryMb)
            {
                PeakMemoryMb = rollingPeakMem;
                OnPropertyChanged(nameof(PeakMemoryMb));
            }

            // Update memory alert badge
            UpdateMemoryAlert(MemoryMb);
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
        OnPropertyChanged(nameof(Uptime));
        OnPropertyChanged(nameof(UptimeLabel));

        ((RelayCommand)EnableCommand).RaiseCanExecuteChanged();
        ((RelayCommand)DisableCommand).RaiseCanExecuteChanged();
        ((RelayCommand)ReloadCommand).RaiseCanExecuteChanged();
    }

    private static void PushHistory(ObservableCollection<double> col, double value)
    {
        while (col.Count >= HistoryCapacity)
            col.RemoveAt(0);
        col.Add(value);
    }

    /// <summary>
    /// Evaluates current memory usage against thresholds and updates alert properties.
    /// </summary>
    private void UpdateMemoryAlert(long memoryMb)
    {
        // If no threshold provider, default to green (no alerts)
        if (_getMemoryThresholds is null)
        {
            MemoryAlertColor = "#22C55E";
            MemoryAlertIcon = "🟢";
            MemoryAlertMessage = string.Empty;
            return;
        }

        var (warning, high, critical, enabled, normalColor, warningColor, highColor, criticalColor) 
            = _getMemoryThresholds();

        if (!enabled)
        {
            // Reset to configured normal color when alerts are disabled
            MemoryAlertColor = normalColor;
            MemoryAlertIcon = "🟢";
            MemoryAlertMessage = string.Empty;
            return;
        }

        // Evaluate thresholds with configured colors (critical > high > warning)
        if (memoryMb >= critical)
        {
            MemoryAlertColor = criticalColor;
            MemoryAlertIcon = "🔴";
            MemoryAlertMessage = $"Critical: {memoryMb} MB (threshold: {critical} MB)";
        }
        else if (memoryMb >= high)
        {
            MemoryAlertColor = highColor;
            MemoryAlertIcon = "🟠";
            MemoryAlertMessage = $"High: {memoryMb} MB (threshold: {high} MB)";
        }
        else if (memoryMb >= warning)
        {
            MemoryAlertColor = warningColor;
            MemoryAlertIcon = "🟡";
            MemoryAlertMessage = $"Warning: {memoryMb} MB (threshold: {warning} MB)";
        }
        else
        {
            // Normal - use configured normal color
            MemoryAlertColor = normalColor;
            MemoryAlertIcon = "🟢";
            MemoryAlertMessage = string.Empty;
        }
    }

    private void OnPropertyChanged([CallerMemberName] string? name = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));

    // Minimal ICommand relay
    internal sealed class RelayCommand : ICommand
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

/// <summary>
/// Represents a single permission row in the Plugin Manager Permissions tab.
/// Toggling <see cref="IsGranted"/> calls Grant/Revoke on the PermissionService.
/// </summary>
public sealed class PluginPermissionItemViewModel : INotifyPropertyChanged
{
    private readonly string _pluginId;
    private readonly PluginPermission _permission;
    private readonly PermissionService? _service;
    private bool _isGranted;

    public event PropertyChangedEventHandler? PropertyChanged;

    public PluginPermissionItemViewModel(
        string pluginId,
        PluginPermission permission,
        string label,
        string description,
        bool isGranted,
        PermissionService? service)
    {
        _pluginId   = pluginId;
        _permission = permission;
        _service    = service;
        _isGranted  = isGranted;
        Label       = label;
        Description = description;
    }

    public string Label { get; }
    public string Description { get; }

    public bool IsGranted
    {
        get => _isGranted;
        set
        {
            if (_isGranted == value) return;
            _isGranted = value;
            if (_service is not null)
            {
                if (value) _service.Grant(_pluginId, _permission);
                else       _service.Revoke(_pluginId, _permission);
            }
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsGranted)));
        }
    }
}
