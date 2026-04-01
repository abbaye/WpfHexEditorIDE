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
using WpfHexEditor.SDK.Commands;
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
    private readonly Func<string, PluginIsolationMode, Task>? _onIsolationModeChanged;
    private readonly Action<string>? _onMigrateToSandbox;
    private readonly Action<string>? _onDismissMigrationSuggestion;
    private readonly Action<string>? _onLoadNow;
    private readonly Action<string>? _onCascadeUnload;
    private readonly Action<string>? _onCascadeReload;

    // Delegate to get memory thresholds from settings (injected to avoid circular dependency)
    private readonly Func<(int warning, int high, int critical, bool enabled,
                           string normalColor, string warningColor, string highColor, string criticalColor)>? _getMemoryThresholds;

    private PluginIsolationMode _selectedIsolationMode;
    private bool _isReloading;

    // Migration suggestion state
    private bool   _migrationSuggested;
    private string _migrationSuggestionReason = string.Empty;

    public event PropertyChangedEventHandler? PropertyChanged;

    public PluginListItemViewModel(
        PluginEntry entry,
        Action<string> onEnable,
        Action<string> onDisable,
        Action<string> onReload,
        Action<string> onUninstall,
        PermissionService? permissionService = null,
        Func<(int warning, int high, int critical, bool enabled,
              string normalColor, string warningColor, string highColor, string criticalColor)>? getMemoryThresholds = null,
        PluginIsolationMode? initialIsolationMode = null,
        Func<string, PluginIsolationMode, Task>? onIsolationModeChanged = null,
        Action<string>? onMigrateToSandbox = null,
        Action<string>? onDismissMigrationSuggestion = null,
        Action<string>? onLoadNow = null,
        Action<string>? onCascadeUnload = null,
        Action<string>? onCascadeReload = null)
    {
        _entry = entry ?? throw new ArgumentNullException(nameof(entry));
        _onEnable = onEnable;
        _onDisable = onDisable;
        _onReload = onReload;
        _onUninstall = onUninstall;
        _permissionService = permissionService;
        _getMemoryThresholds = getMemoryThresholds;
        _onIsolationModeChanged = onIsolationModeChanged;
        _onMigrateToSandbox = onMigrateToSandbox;
        _onDismissMigrationSuggestion = onDismissMigrationSuggestion;
        _onLoadNow = onLoadNow;
        _onCascadeUnload = onCascadeUnload;
        _onCascadeReload = onCascadeReload;
        _selectedIsolationMode = initialIsolationMode ?? entry.Manifest.IsolationMode;

        EnableCommand = new RelayCommand(_ => _onEnable(Id), _ => State == PluginState.Disabled);
        DisableCommand = new RelayCommand(_ => _onDisable(Id), _ => State == PluginState.Loaded);
        ReloadCommand = new RelayCommand(_ => _onReload(Id), _ => State is PluginState.Loaded or PluginState.Faulted or PluginState.Disabled);
        UninstallCommand = new RelayCommand(_ => _onUninstall(Id));
        MigrateToSandboxCommand = new RelayCommand(
            _ => _onMigrateToSandbox?.Invoke(Id),
            _ => CanMigrateToSandbox);
        DismissMigrationSuggestionCommand = new RelayCommand(
            _ => { _onDismissMigrationSuggestion?.Invoke(Id); ClearMigrationSuggestion(); });

        LoadNowCommand = new RelayCommand(
            _ => _onLoadNow?.Invoke(Id),
            _ => IsDormant && _onLoadNow is not null);
        CascadeUnloadCommand = new RelayCommand(
            _ => _onCascadeUnload?.Invoke(Id),
            _ => State == PluginState.Loaded && _onCascadeUnload is not null);
        CascadeReloadCommand = new RelayCommand(
            _ => _onCascadeReload?.Invoke(Id),
            _ => State == PluginState.Loaded && _onCascadeReload is not null);

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
    // IsolationMode — bindable ComboBox selection; changing triggers a hot-swap reload.
    public static IReadOnlyList<string> IsolationModes { get; } = ["Auto", "InProcess", "Sandbox"];

    /// <summary>
    /// For Auto-mode plugins: shows "(→ InProcess)" or "(→ Sandbox)" inline next to the ComboBox.
    /// Visible only when Auto is selected; empty string otherwise.
    /// </summary>
    public string ResolvedIsolationModeLabel
    {
        get
        {
            var declared = _entry.Manifest.IsolationMode;
            var resolved = _entry.ResolvedIsolationMode;
            return declared == PluginIsolationMode.Auto
                ? $"(→ {resolved})"
                : string.Empty;
        }
    }

    /// <summary>
    /// Semantic foreground color for the resolved isolation mode label.
    /// InProcess → green (fast, in-host); Sandbox → amber (isolated, external process).
    /// </summary>
    public string ResolvedIsolationModeColor => _entry.ResolvedIsolationMode switch
    {
        PluginIsolationMode.InProcess => "#22C55E",  // green — same as Loaded state badge
        PluginIsolationMode.Sandbox   => "#F59E0B",  // amber — same as SLOW badge / Loading state
        _                             => "#9CA3AF"   // gray fallback (Auto not yet resolved)
    };

    public string SelectedIsolationMode
    {
        get => _selectedIsolationMode.ToString();
        set
        {
            if (!Enum.TryParse<PluginIsolationMode>(value, out var mode) || mode == _selectedIsolationMode)
                return;
            _selectedIsolationMode = mode;
            OnPropertyChanged();
            if (_onIsolationModeChanged is not null)
                _ = ApplyIsolationModeAsync(mode);
        }
    }

    public bool IsReloading
    {
        get => _isReloading;
        private set { _isReloading = value; OnPropertyChanged(); OnPropertyChanged(nameof(CanChangeIsolationMode)); }
    }

    public bool CanChangeIsolationMode => !_isReloading;

    private async Task ApplyIsolationModeAsync(PluginIsolationMode mode)
    {
        IsReloading = true;
        try { await _onIsolationModeChanged!(Id, mode).ConfigureAwait(false); }
        finally { IsReloading = false; }
    }

    public PluginState State => _entry.State;

    public string StateLabel => _entry.State switch
    {
        PluginState.Loaded => "Running",
        PluginState.Loading => "Loading...",
        PluginState.Disabled => "Disabled",
        PluginState.Faulted => "Error",
        PluginState.Incompatible => "Incompatible",
        PluginState.Unloaded => "Unloaded",
        PluginState.Dormant => "Dormant",
        _ => "Unknown"
    };

    public string StateBadgeColor => _entry.State switch
    {
        PluginState.Loaded => "#22C55E",   // green
        PluginState.Loading => "#F59E0B",  // amber
        PluginState.Disabled => "#6B7280", // gray
        PluginState.Faulted => "#EF4444",  // red
        PluginState.Incompatible => "#F97316", // orange
        PluginState.Dormant => "#A855F7",  // purple
        _ => "#9CA3AF"
    };

    // -- Lazy Loading (Dormant) ---------------------------------------------------

    public bool IsDormant => _entry.State == PluginState.Dormant;

    public string ActivationTriggerLabel
    {
        get
        {
            var activation = _entry.Manifest.Activation;
            if (activation is null) return string.Empty;
            var parts = new List<string>();
            if (activation.FileExtensions.Count > 0)
                parts.Add("Files: " + string.Join(", ", activation.FileExtensions));
            if (activation.Commands.Count > 0)
                parts.Add("Cmds: " + string.Join(", ", activation.Commands));
            return string.Join(" | ", parts);
        }
    }

    // -- ALC Diagnostics (InProcess only) ----------------------------------------

    public bool IsInProcess => _entry.ResolvedIsolationMode == PluginIsolationMode.InProcess;
    public int AlcAssemblyCount => _entry.Diagnostics.AlcAssemblyCount;
    public int AlcConflictCount => _entry.Diagnostics.AlcConflictCount;
    public bool HasAlcConflicts => _entry.Diagnostics.AlcConflictCount > 0;

    public IReadOnlyList<string> AssemblyConflictLabels
        => _entry.AssemblyConflicts
            .Select(c => $"{c.AssemblyName}: host={c.HostVersion} requested={c.RequestedVersion}")
            .ToList();

    // -- Capability Features ------------------------------------------------------

    public IReadOnlyList<string> Features => _entry.Manifest.Features;
    public bool HasFeatures => _entry.Manifest.Features.Count > 0;

    // -- Extension Points ---------------------------------------------------------

    public IReadOnlyList<string> ExtensionLabels
        => _entry.Manifest.Extensions.Select(kv => $"{kv.Key} → {kv.Value}").ToList();
    public bool HasExtensions => _entry.Manifest.Extensions.Count > 0;

    // -- Dependency Graph ---------------------------------------------------------

    public IReadOnlyList<string> UnresolvedDepLabels
        => _entry.UnresolvedDependencies
            .Select(e => $"{e.RequiredPluginId} ({e.Kind})")
            .ToList();
    public bool HasUnresolvedDeps => _entry.UnresolvedDependencies.Count > 0;

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
    public ICommand MigrateToSandboxCommand { get; }
    public ICommand DismissMigrationSuggestionCommand { get; }
    public ICommand LoadNowCommand { get; }
    public ICommand CascadeUnloadCommand { get; }
    public ICommand CascadeReloadCommand { get; }

    // -- Migration suggestion -----------------------------------------------------

    /// <summary>
    /// True when the migration monitor has suggested moving this plugin to Sandbox.
    /// Drives the warning banner visibility in the Plugin Manager detail pane.
    /// </summary>
    public bool MigrationSuggested
    {
        get => _migrationSuggested;
        private set { _migrationSuggested = value; OnPropertyChanged(); OnPropertyChanged(nameof(CanMigrateToSandbox)); }
    }

    /// <summary>Human-readable reason for the migration suggestion.</summary>
    public string MigrationSuggestionReason
    {
        get => _migrationSuggestionReason;
        private set { _migrationSuggestionReason = value; OnPropertyChanged(); }
    }

    /// <summary>
    /// True when this plugin can be manually migrated to Sandbox via the "Move to Sandbox" button.
    /// Requires: plugin is InProcess, Loaded, and not currently reloading.
    /// </summary>
    public bool CanMigrateToSandbox
        => _entry.ResolvedIsolationMode == PluginIsolationMode.InProcess
        && _entry.State == PluginState.Loaded
        && !_isReloading;

    /// <summary>
    /// Called by <see cref="PluginManagerViewModel"/> when <c>WpfPluginHost.MigrationSuggested</c>
    /// fires for this plugin.
    /// </summary>
    public void SetMigrationSuggestion(string reason)
    {
        MigrationSuggestionReason = reason;
        MigrationSuggested = true;
        ((RelayCommand)MigrateToSandboxCommand).RaiseCanExecuteChanged();
    }

    /// <summary>Clears the migration suggestion banner (user dismissed or migration applied).</summary>
    public void ClearMigrationSuggestion()
    {
        MigrationSuggested = false;
        MigrationSuggestionReason = string.Empty;
    }

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

        OnPropertyChanged(nameof(IsDormant));
        OnPropertyChanged(nameof(IsInProcess));
        OnPropertyChanged(nameof(ResolvedIsolationModeLabel));
        OnPropertyChanged(nameof(ResolvedIsolationModeColor));
        OnPropertyChanged(nameof(AlcAssemblyCount));
        OnPropertyChanged(nameof(AlcConflictCount));
        OnPropertyChanged(nameof(HasAlcConflicts));
        OnPropertyChanged(nameof(AssemblyConflictLabels));
        OnPropertyChanged(nameof(HasUnresolvedDeps));
        OnPropertyChanged(nameof(UnresolvedDepLabels));

        ((RelayCommand)EnableCommand).RaiseCanExecuteChanged();
        ((RelayCommand)DisableCommand).RaiseCanExecuteChanged();
        ((RelayCommand)ReloadCommand).RaiseCanExecuteChanged();
        ((RelayCommand)MigrateToSandboxCommand).RaiseCanExecuteChanged();
        ((RelayCommand)LoadNowCommand).RaiseCanExecuteChanged();
        ((RelayCommand)CascadeUnloadCommand).RaiseCanExecuteChanged();
        ((RelayCommand)CascadeReloadCommand).RaiseCanExecuteChanged();
        OnPropertyChanged(nameof(CanMigrateToSandbox));
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
