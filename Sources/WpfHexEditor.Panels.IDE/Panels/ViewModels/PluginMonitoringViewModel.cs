// ==========================================================
// Project: WpfHexEditor.Panels.IDE
// File: PluginMonitoringViewModel.cs
// Author: Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.6
// Created: 2026-03-06
// Description:
//     Master ViewModel for the Plugin Monitoring docking panel.
//     Drives global CPU/Memory charts, per-plugin sparkline mini-charts,
//     interactive permissions editor, plugin settings routing, configurable
//     alert thresholds, CSV/JSON diagnostics export, and the live event log.
//
// Architecture Notes:
//     Observer pattern — subscribes to WpfPluginHost lifecycle events.
//     Per-plugin CPU estimation: process CPU × (plugin.AvgExecMs / sum all AvgExecMs).
//     Canvas-based Polyline charts (global) + SparklineControl (per-plugin).
//     PluginAlertEngine evaluated on every sampling tick.
//     PluginDiagnosticsExporter driven by Export commands with SaveFileDialog.
// ==========================================================

using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Runtime.CompilerServices;
using System.Text;
using System.Windows;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Threading;
using WpfHexEditor.PluginHost;
using WpfHexEditor.PluginHost.Monitoring;
using WpfHexEditor.PluginHost.Services;
using WpfHexEditor.SDK.Contracts;
using WpfHexEditor.SDK.Contracts.Services;
using WpfHexEditor.SDK.Models;

namespace WpfHexEditor.Panels.IDE.Panels.ViewModels;

/// <summary>Side of the panel where the plugin list appears in landscape (bottom-dock) mode.</summary>
public enum PanelListSide { Left, Right }

/// <summary>
/// Controls where the CPU/Memory charts area is positioned relative to the plugin table.
/// Mirrors the <c>ChartPosition</c> pattern used in the DataInspector panel.
/// </summary>
public enum MonitorChartsPosition { Top, Bottom, Left, Right }

/// <summary>A single data point for the time-series chart.</summary>
public sealed record ChartPoint(DateTime Time, double Value);

/// <summary>A timestamped event entry displayed in the live event log.</summary>
public sealed record PluginEventEntry(
    string TimeLabel, string Icon, string Color, string PluginName, string Message);

// ==========================================================================
// PluginPermissionRowViewModel
// ==========================================================================

/// <summary>
/// Represents a single permission flag for a plugin in the interactive
/// Permissions tab. Toggling <see cref="IsGranted"/> immediately calls
/// <see cref="PermissionService.Grant"/> or <see cref="PermissionService.Revoke"/>.
/// </summary>
public sealed class PluginPermissionRowViewModel : INotifyPropertyChanged
{
    private readonly PermissionService _permissionService;
    private readonly string _pluginId;
    private bool _isGranted;

    public event PropertyChangedEventHandler? PropertyChanged;

    public PluginPermissionRowViewModel(
        PermissionService permissionService,
        string pluginId,
        PluginPermission flag,
        string name,
        string description,
        string riskLevel,
        bool isDeclared,
        bool isGranted)
    {
        _permissionService = permissionService;
        _pluginId          = pluginId;
        Flag               = flag;
        Name               = name;
        Description        = description;
        RiskLevel          = riskLevel;
        IsDeclared         = isDeclared;
        _isGranted         = isGranted;
    }

    public PluginPermission Flag        { get; }
    public string           Name        { get; }
    public string           Description { get; }

    /// <summary>Risk classification: "High", "Medium", or "Low".</summary>
    public string RiskLevel  { get; }

    /// <summary>True when the plugin declared this capability in its manifest.</summary>
    public bool   IsDeclared { get; }

    /// <summary>Color hint for the risk badge.</summary>
    public string RiskColor  => RiskLevel switch
    {
        "High"   => "#EF4444",
        "Medium" => "#F59E0B",
        _        => "#22C55E"
    };

    /// <summary>
    /// Whether this permission is currently granted.
    /// Two-way binding in the UI — setter calls Grant/Revoke immediately.
    /// </summary>
    public bool IsGranted
    {
        get => _isGranted;
        set
        {
            if (_isGranted == value) return;
            _isGranted = value;
            OnPropertyChanged();
            if (value)
                _permissionService.Grant(_pluginId, Flag);
            else
                _permissionService.Revoke(_pluginId, Flag);
        }
    }

    private void OnPropertyChanged([CallerMemberName] string? name = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}

// ==========================================================================
// PluginMonitorRow
// ==========================================================================

/// <summary>
/// Summary row for a single plugin in the monitoring table.
/// Holds a reference to its <see cref="PluginMiniChartViewModel"/> for inline sparklines.
/// </summary>
public sealed class PluginMonitorRow : INotifyPropertyChanged
{
    private string _name         = string.Empty;
    private string _state        = string.Empty;
    private string _stateColor   = "#9CA3AF";
    private double _cpuPercent;
    private double _weightedCpu;
    private long   _memoryMb;
    private long   _weightedMemMb;
    private double _avgExecMs;
    private double _initTimeMs;
    private string _uptimeLabel  = string.Empty;
    private double _peakCpu;
    private bool   _isResponsive = true;
    private bool   _isSlow;
    private string _faultMessage = string.Empty;
    private string _version      = string.Empty;

    public event PropertyChangedEventHandler? PropertyChanged;

    public string Id { get; init; } = string.Empty;

    private PluginMiniChartViewModel? _miniChart;

    /// <summary>Reference to this plugin's per-plugin mini-chart ViewModel.</summary>
    public PluginMiniChartViewModel? MiniChart
    {
        get => _miniChart;
        set { _miniChart = value; OnPropertyChanged(); }
    }

    public string Name         { get => _name;         set { _name         = value; OnPropertyChanged(); } }
    public string State        { get => _state;        set { _state        = value; OnPropertyChanged(); } }
    public string StateColor   { get => _stateColor;   set { _stateColor   = value; OnPropertyChanged(); } }
    public double CpuPercent   { get => _cpuPercent;   set { _cpuPercent   = value; OnPropertyChanged(); } }

    /// <summary>Weighted CPU estimate for this plugin (process CPU × exec-time weight).</summary>
    public double WeightedCpu    { get => _weightedCpu;    set { _weightedCpu    = value; OnPropertyChanged(); } }
    public long   MemoryMb       { get => _memoryMb;       set { _memoryMb       = value; OnPropertyChanged(); } }

    /// <summary>Weighted memory estimate for this plugin (process GC heap × exec-time weight).</summary>
    public long   WeightedMemMb  { get => _weightedMemMb;  set { _weightedMemMb  = value; OnPropertyChanged(); } }
    public double AvgExecMs    { get => _avgExecMs;    set { _avgExecMs    = value; OnPropertyChanged(); } }
    public double InitTimeMs   { get => _initTimeMs;   set { _initTimeMs   = value; OnPropertyChanged(); } }
    public string UptimeLabel  { get => _uptimeLabel;  set { _uptimeLabel  = value; OnPropertyChanged(); } }
    public double PeakCpu      { get => _peakCpu;      set { _peakCpu      = value; OnPropertyChanged(); } }
    public string FaultMessage { get => _faultMessage; set { _faultMessage = value; OnPropertyChanged(); } }
    public string Version      { get => _version;      set { _version      = value; OnPropertyChanged(); } }

    public bool IsResponsive
    {
        get => _isResponsive;
        set
        {
            _isResponsive = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(HasWarning));
            OnPropertyChanged(nameof(WarningIcon));
            OnPropertyChanged(nameof(WarningColor));
            OnPropertyChanged(nameof(WarningTooltip));
        }
    }

    public bool IsSlow
    {
        get => _isSlow;
        set
        {
            _isSlow = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(HasWarning));
            OnPropertyChanged(nameof(WarningIcon));
            OnPropertyChanged(nameof(WarningColor));
            OnPropertyChanged(nameof(WarningTooltip));
        }
    }

    public bool   HasWarning    => !_isResponsive || _isSlow || _hasMemoryAlert;
    public string WarningIcon   => !_isResponsive ? "\uE7BA" : _isSlow ? "\uE946" : _hasMemoryAlert ? "\uE7BA" : string.Empty;
    public string WarningColor  => !_isResponsive ? "#F97316" : _isSlow ? "#F59E0B" : _hasMemoryAlert ? MemoryAlertColor : string.Empty;
    public string WarningTooltip => !_isResponsive ? "Plugin not responsive" : _isSlow ? "Slow plugin detected" : _hasMemoryAlert ? MemoryAlertMessage : string.Empty;

    // -- Memory alert properties -----------------------------------------------

    private bool _hasMemoryAlert;
    private string _memoryAlertLevel = "Normal";
    private string _memoryAlertColor = "#22C55E";
    private string _memoryAlertIcon = "🟢";
    private string _memoryAlertMessage = string.Empty;

    /// <summary>True if memory usage exceeds any configured threshold.</summary>
    public bool HasMemoryAlert
    {
        get => _hasMemoryAlert;
        set
        {
            _hasMemoryAlert = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(HasWarning));
            OnPropertyChanged(nameof(WarningIcon));
            OnPropertyChanged(nameof(WarningColor));
            OnPropertyChanged(nameof(WarningTooltip));
        }
    }

    /// <summary>Memory alert level name: "Normal", "Warning", "High", or "Critical".</summary>
    public string MemoryAlertLevel
    {
        get => _memoryAlertLevel;
        set { _memoryAlertLevel = value; OnPropertyChanged(); }
    }

    /// <summary>Memory alert color (hex string, e.g., "#22C55E").</summary>
    public string MemoryAlertColor
    {
        get => _memoryAlertColor;
        set { _memoryAlertColor = value; OnPropertyChanged(); }
    }

    /// <summary>Memory alert icon/emoji (🟢🟡🟠🔴).</summary>
    public string MemoryAlertIcon
    {
        get => _memoryAlertIcon;
        set { _memoryAlertIcon = value; OnPropertyChanged(); }
    }

    /// <summary>Memory alert descriptive message.</summary>
    public string MemoryAlertMessage
    {
        get => _memoryAlertMessage;
        set { _memoryAlertMessage = value; OnPropertyChanged(); }
    }

    private void OnPropertyChanged([CallerMemberName] string? name = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}

// ==========================================================================
// PluginDetailViewModel
// ==========================================================================

/// <summary>
/// Rich metadata + runtime metrics + interactive permissions + optional
/// settings page for the currently selected plugin.
/// </summary>
public sealed class PluginDetailViewModel : INotifyPropertyChanged
{
    // Known permission flags with display metadata.
    private static readonly (PluginPermission Flag, string Name, string Description, string Risk)[] AllPermissions =
    [
        (PluginPermission.AccessFileSystem, "File System",   "Read and write files on disk",                    "High"),
        (PluginPermission.AccessNetwork,    "Network",       "Make outbound network requests",                   "High"),
        (PluginPermission.AccessHexEditor,  "Hex Editor",    "Read active file content, selection, and offset",  "Medium"),
        (PluginPermission.AccessCodeEditor, "Code Editor",   "Read and interact with the active code editor",    "Medium"),
        (PluginPermission.RegisterMenus,    "Menus & UI",    "Register menus, toolbars, and panels",             "Medium"),
        (PluginPermission.AccessSettings,   "Settings",      "Read and write plugin settings",                   "Medium"),
        (PluginPermission.WriteOutput,      "Output Panel",  "Write to the IDE Output panel",                    "Low"),
        (PluginPermission.WriteErrorPanel,  "Error Panel",   "Write to the IDE Error panel",                     "Low"),
        (PluginPermission.WriteTerminal,    "Terminal",      "Write output lines to the IDE Terminal",           "Low"),
        (PluginPermission.TerminalOnly,     "Terminal Only", "Plugin is exclusively a Terminal extension",       "Low"),
    ];

    public event PropertyChangedEventHandler? PropertyChanged;

    // -- Identity & metadata --
    public string  Name             { get; private set; } = string.Empty;
    public string  Version          { get; private set; } = string.Empty;
    public string  Author           { get; private set; } = string.Empty;
    public string  Description      { get; private set; } = string.Empty;
    public bool    TrustedPublisher { get; private set; }
    public string  TrustLabel       { get; private set; } = string.Empty;
    public string  IsolationMode    { get; private set; } = string.Empty;
    public string  SdkVersion       { get; private set; } = string.Empty;

    // -- Runtime metrics --
    public string LoadedAtLabel   { get; private set; } = "—";
    public string UptimeLabel     { get; private set; } = "—";
    public string InitTimeMsLabel { get; private set; } = string.Empty;
    public string AvgExecMsLabel  { get; private set; } = string.Empty;
    public string PeakCpuLabel    { get; private set; } = string.Empty;
    public string AvgCpuLabel     { get; private set; } = string.Empty;

    // -- State --
    public string? FaultMessage { get; private set; }
    public bool    HasFault     { get; private set; }
    public string  StateColor   { get; private set; } = "#9CA3AF";

    // -- Permissions (interactive) --
    public ObservableCollection<PluginPermissionRowViewModel> PermissionRows { get; } = new();

    // -- Options page (IPluginWithOptions) --
    public bool             HasOptions       { get; private set; }
    public FrameworkElement? OptionsPageContent { get; private set; }
    public ICommand         SaveOptionsCommand { get; }

    private IPluginWithOptions? _optionsPlugin;
    private string              _optionsPluginId = string.Empty;

    public PluginDetailViewModel()
    {
        SaveOptionsCommand = new RelayCommand(_ => _optionsPlugin?.SaveOptions());
    }

    /// <summary>Refreshes all properties from the given <paramref name="entry"/>.</summary>
    public void Update(PluginEntry entry, PermissionService permSvc)
    {
        Name             = entry.Manifest.Name;
        Version          = entry.Manifest.Version ?? string.Empty;
        Author           = string.IsNullOrEmpty(entry.Manifest.Author)
                           ? entry.Manifest.Publisher : entry.Manifest.Author;
        Description      = entry.Manifest.Description ?? string.Empty;
        TrustedPublisher = entry.Manifest.TrustedPublisher;
        TrustLabel       = entry.Manifest.TrustedPublisher ? "Official" : "Community";
        IsolationMode    = entry.Manifest.IsolationMode.ToString();
        SdkVersion       = entry.Manifest.SdkVersion ?? string.Empty;
        LoadedAtLabel    = entry.LoadedAt?.ToLocalTime().ToString("HH:mm:ss") ?? "—";

        var uptime       = entry.Diagnostics.Uptime;
        UptimeLabel      = FormatUptime(uptime);
        InitTimeMsLabel  = $"{entry.InitDuration.TotalMilliseconds:F0} ms";
        AvgExecMsLabel   = $"{entry.Diagnostics.AverageExecutionTime.TotalMilliseconds:F1} ms";
        PeakCpuLabel     = $"{entry.Diagnostics.PeakCpu():F1} %";
        AvgCpuLabel      = $"{entry.Diagnostics.AverageCpu():F1} %";
        FaultMessage     = entry.FaultException?.ToString();
        HasFault         = entry.FaultException is not null;
        StateColor       = StateBadgeColor(entry.State);

        RefreshPermissions(entry, permSvc);
        RefreshOptionsPage(entry);

        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(string.Empty));
    }

    private void RefreshPermissions(PluginEntry entry, PermissionService permSvc)
    {
        PermissionRows.Clear();

        var granted  = permSvc.GetGranted(entry.Manifest.Id);
        var declared = entry.Instance?.Capabilities.ToPermissionFlags() ?? PluginPermission.None;

        foreach (var (flag, name, desc, risk) in AllPermissions)
        {
            PermissionRows.Add(new PluginPermissionRowViewModel(
                permSvc,
                entry.Manifest.Id,
                flag,
                name,
                desc,
                risk,
                isDeclared: (declared & flag) != PluginPermission.None,
                isGranted:  (granted  & flag) != PluginPermission.None));
        }
    }

    private void RefreshOptionsPage(PluginEntry entry)
    {
        if (entry.Instance is IPluginWithOptions opts)
        {
            HasOptions = true;

            // Only recreate the options page when the plugin changes.
            if (_optionsPluginId != entry.Manifest.Id)
            {
                _optionsPluginId = entry.Manifest.Id;
                _optionsPlugin   = opts;

                // Guard: a buggy plugin must not crash the host.
                try
                {
                    OptionsPageContent = opts.CreateOptionsPage();
                    opts.LoadOptions();
                }
                catch (Exception ex)
                {
                    OptionsPageContent = null;
                    System.Diagnostics.Debug.WriteLine(
                        $"[PluginMonitor] CreateOptionsPage threw for plugin '{entry.Manifest.Id}': {ex}");
                }
            }
        }
        else
        {
            HasOptions         = false;
            _optionsPlugin     = null;
            _optionsPluginId   = string.Empty;
            OptionsPageContent = null;
        }
    }

    private static string FormatUptime(TimeSpan uptime)
    {
        if (uptime.TotalSeconds < 1) return "—";
        return uptime.TotalHours >= 1
            ? $"{(int)uptime.TotalHours:D2}:{uptime.Minutes:D2}:{uptime.Seconds:D2}"
            : $"{uptime.Minutes:D2}:{uptime.Seconds:D2}";
    }

    private static string StateBadgeColor(PluginState state) => state switch
    {
        PluginState.Loaded       => "#22C55E",
        PluginState.Loading      => "#F59E0B",
        PluginState.Disabled     => "#6B7280",
        PluginState.Faulted      => "#EF4444",
        PluginState.Incompatible => "#F97316",
        _                        => "#9CA3AF"
    };
}

// ==========================================================================
// PluginMonitoringViewModel
// ==========================================================================

/// <summary>
/// Master ViewModel for the Plugin Monitoring docking panel.
/// </summary>
public sealed class PluginMonitoringViewModel : INotifyPropertyChanged, IDisposable
{
    private const int MaxChartPoints = 60; // 5 min at 5 s intervals
    private const int MaxEventLog    = 200;

    private readonly WpfPluginHost    _host;
    private readonly Dispatcher       _dispatcher;
    private readonly DispatcherTimer  _timer;
    private readonly PluginAlertEngine _alertEngine = new();
    private readonly PluginDiagnosticsExporter _exporter = new();
    private readonly IOutputService?  _outputService;
    private MemoryAlertService? _memoryAlertService;

    // Per-plugin mini-chart lookup (pluginId → ViewModel)
    private readonly Dictionary<string, PluginMiniChartViewModel> _miniCharts =
        new(StringComparer.OrdinalIgnoreCase);

    private readonly HashSet<string>   _slowPluginIds = new();
    private readonly ICollectionView   _filteredRows  = null!;

    // Filter chip state
    private bool _filterStateLoaded   = false;
    private bool _filterStateDisabled = false;
    private bool _filterStateFaulted  = false;
    private bool _filterWarningsOnly  = false;

    private string             _searchText           = string.Empty;
    private PluginMonitorRow?  _selectedPlugin;
    private PluginDetailViewModel? _selectedPluginDetail;
    private PluginMiniChartViewModel? _selectedMiniChart;

    private double        _currentCpu;
    private long          _currentMemoryMb;
    private int           _loadedCount;
    private int           _totalCount;
    private int           _faultCount;
    private int           _alertCount;
    private bool          _isRunning    = true;
    private bool          _showSparklines = true;
    private int           _intervalSeconds = 5;
    private PanelListSide         _listSide       = PanelListSide.Right;
    private bool                  _isLandscape;
    private bool                  _isInstalling;
    private MonitorChartsPosition _chartsPosition = MonitorChartsPosition.Top;
    private bool                  _showEventLog   = true;

    public event PropertyChangedEventHandler? PropertyChanged;

    public PluginMonitoringViewModel(WpfPluginHost host, Dispatcher dispatcher, IOutputService? outputService = null, MemoryAlertThresholds? memoryThresholds = null)
    {
        _host          = host ?? throw new ArgumentNullException(nameof(host));
        _dispatcher    = dispatcher ?? throw new ArgumentNullException(nameof(dispatcher));
        _outputService = outputService;

        // Initialize memory alert service with provided or default thresholds
        InitializeMemoryAlertService(memoryThresholds);

        _timer = new DispatcherTimer(DispatcherPriority.Background, dispatcher)
        {
            Interval = TimeSpan.FromSeconds(_intervalSeconds)
        };
        _timer.Tick += OnTimerTick;
        _timer.Start();

        // -- Commands --
        StartStopCommand         = new RelayCommand(_ => ToggleRunning());
        ResetCommand             = new RelayCommand(_ => Reset());
        ToggleListSideCommand    = new RelayCommand(_ => ListSide = ListSide == PanelListSide.Right ? PanelListSide.Left : PanelListSide.Right);
        ClearLogCommand          = new RelayCommand(_ => ClearLog());
        ReloadPluginCommand      = new RelayCommand(_ => { if (_selectedPlugin is not null) _ = _host.ReloadPluginAsync(_selectedPlugin.Id); });
        TogglePluginCommand      = new RelayCommand(_ => TogglePlugin());
        SetIntervalCommand       = new RelayCommand(p => { if (p is int s) SetInterval(s); });
        ToggleSparklinesCommand  = new RelayCommand(_ => ShowSparklines = !ShowSparklines);
        ToggleEventLogCommand    = new RelayCommand(_ => ShowEventLog   = !ShowEventLog);
        ExportCsvCommand         = new RelayCommand(_ => ExportTo("csv"));
        ExportJsonCommand        = new RelayCommand(_ => ExportTo("json"));
        ExportCrashReportCommand = new RelayCommand(
            _ => ExportCrashReport(),
            _ => _selectedPlugin?.State == "Error");
        InstallPluginCommand     = new RelayCommand(_ => InstallFromBrowse());
        UninstallPluginCommand        = new RelayCommand(
            _ => RequestUninstall?.Invoke(_selectedPlugin!),
            _ => _selectedPlugin is not null);
        OpenInPluginManagerCommand   = new RelayCommand(
            _ => RequestOpenInPluginManager?.Invoke(_selectedPlugin!.Id),
            _ => _selectedPlugin is not null);
        CopyTableCommand             = new RelayCommand(_ => CopyTableToClipboard());
        ExportEventLogCommand    = new RelayCommand(_ => ExportEventLog());

        // -- Host event subscriptions --
        _host.PluginLoaded       += OnPluginLoaded;
        _host.PluginCrashed      += OnPluginCrashed;
        _host.SlowPluginDetected += OnSlowPluginDetected;

        // -- Alert engine --
        _alertEngine.AlertTriggered += OnAlertTriggered;

        // -- Filtered view --
        _filteredRows = CollectionViewSource.GetDefaultView(Rows);
        _filteredRows.Filter = FilterPlugin;

        Refresh();
        SynthesizeInitialLoadEvents();
    }

    // -- Commands ----------------------------------------------------------------

    public ICommand StartStopCommand         { get; }
    public ICommand ResetCommand             { get; }
    public ICommand ToggleListSideCommand    { get; }
    public ICommand ClearLogCommand          { get; }
    public ICommand ReloadPluginCommand      { get; }
    public ICommand TogglePluginCommand      { get; }
    public ICommand SetIntervalCommand       { get; }
    public ICommand ToggleSparklinesCommand  { get; }
    public ICommand ToggleEventLogCommand    { get; }
    public ICommand ExportCsvCommand         { get; }
    public ICommand ExportJsonCommand        { get; }
    public ICommand ExportCrashReportCommand { get; }
    public ICommand InstallPluginCommand     { get; }
    public ICommand UninstallPluginCommand        { get; }
    public ICommand OpenInPluginManagerCommand   { get; }

    /// <summary>
    /// Raised when the user triggers Uninstall on a plugin.
    /// Code-behind shows a confirmation dialog, then calls <see cref="UninstallConfirmedAsync"/>.
    /// </summary>
    public event Action<PluginMonitorRow>? RequestUninstall;

    /// <summary>
    /// Raised when the user chooses "Open in Plugin Manager" from the context menu.
    /// Carries the plugin ID — code-behind opens/focuses the Plugin Manager tab
    /// and pre-selects the matching entry.
    /// </summary>
    public event Action<string>? RequestOpenInPluginManager;

    public ICommand CopyTableCommand         { get; }
    public ICommand ExportEventLogCommand    { get; }

    // -- Alert thresholds (hot-configurable, bound to threshold editor UI) -------

    public double   AlertCpuThreshold
    {
        get => _alertEngine.CpuAlertThreshold;
        set { _alertEngine.CpuAlertThreshold = value; OnPropertyChanged(); }
    }

    public long     AlertMemoryThresholdMb
    {
        get => _alertEngine.MemoryAlertThresholdMb;
        set { _alertEngine.MemoryAlertThresholdMb = value; OnPropertyChanged(); }
    }

    public double   AlertExecThresholdMs
    {
        get => _alertEngine.ExecTimeAlertThreshold.TotalMilliseconds;
        set { _alertEngine.ExecTimeAlertThreshold = TimeSpan.FromMilliseconds(value); OnPropertyChanged(); }
    }

    // -- Running state -----------------------------------------------------------

    public bool IsRunning
    {
        get => _isRunning;
        private set
        {
            if (_isRunning == value) return;
            _isRunning = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(StartStopLabel));
            OnPropertyChanged(nameof(StartStopIcon));
        }
    }

    public string StartStopLabel => _isRunning ? "Pause" : "Resume";
    public string StartStopIcon  => _isRunning ? "\uE769" : "\uE768";

    public int IntervalSeconds
    {
        get => _intervalSeconds;
        private set { _intervalSeconds = value; OnPropertyChanged(); }
    }

    public PanelListSide ListSide
    {
        get => _listSide;
        private set
        {
            if (_listSide == value) return;
            _listSide = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(ListSideIcon));
            OnPropertyChanged(nameof(ListSideTooltip));
        }
    }

    public string ListSideIcon    => _listSide == PanelListSide.Right ? "\uE89F" : "\uE8A0";
    public string ListSideTooltip => _listSide == PanelListSide.Right ? "Move list to left" : "Move list to right";

    public bool IsLandscape
    {
        get => _isLandscape;
        set { if (_isLandscape == value) return; _isLandscape = value; OnPropertyChanged(); }
    }

    /// <summary>
    /// Controls where the CPU/Memory charts area appears relative to the plugin table.
    /// Persisted for the session; the XAML combo box stays in sync via code-behind.
    /// </summary>
    public MonitorChartsPosition ChartsPosition
    {
        get => _chartsPosition;
        set
        {
            if (_chartsPosition == value) return;
            _chartsPosition = value;
            OnPropertyChanged();
        }
    }

    /// <summary>When true, the event log section is visible at the bottom of the panel.</summary>
    public bool ShowEventLog
    {
        get => _showEventLog;
        set
        {
            if (_showEventLog == value) return;
            _showEventLog = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(ShowEventLogIcon));
            OnPropertyChanged(nameof(ShowEventLogTooltip));
        }
    }

    public string ShowEventLogIcon    => _showEventLog ? "\uE81C" : "\uE81B";  // ChevronDown / ChevronUp
    public string ShowEventLogTooltip => _showEventLog ? "Hide event log"      : "Show event log";

    /// <summary>True while a plugin package is being installed. Drives the drop-zone progress overlay.</summary>
    public bool IsInstalling
    {
        get => _isInstalling;
        private set { _isInstalling = value; OnPropertyChanged(); }
    }

    /// <summary>When true, the DataGrid shows inline mini-sparkline columns.</summary>
    public bool ShowSparklines
    {
        get => _showSparklines;
        set
        {
            if (_showSparklines == value) return;
            _showSparklines = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(ShowSparklinesIcon));
            OnPropertyChanged(nameof(ShowSparklinesLabel));
        }
    }

    public string ShowSparklinesIcon  => _showSparklines ? "\uE9D2" : "\uE9D3";
    public string ShowSparklinesLabel => _showSparklines ? "Hide sparklines" : "Show sparklines";

    // -- Alert badge -------------------------------------------------------------

    /// <summary>Number of alerts triggered since last log clear.</summary>
    public int AlertCount
    {
        get => _alertCount;
        private set { _alertCount = value; OnPropertyChanged(); OnPropertyChanged(nameof(HasAlerts)); OnPropertyChanged(nameof(AlertBadgeLabel)); }
    }

    public bool   HasAlerts       => _alertCount > 0;
    public string AlertBadgeLabel => $"\uE7BA {_alertCount}";

    // -- Chart data --------------------------------------------------------------

    public ObservableCollection<ChartPoint> CpuHistory    { get; } = new();
    public ObservableCollection<ChartPoint> MemoryHistory { get; } = new();

    // -- Summary metrics ---------------------------------------------------------

    public double CurrentCpu
    {
        get => _currentCpu;
        private set { _currentCpu = value; OnPropertyChanged(); }
    }

    public long CurrentMemoryMb
    {
        get => _currentMemoryMb;
        private set { _currentMemoryMb = value; OnPropertyChanged(); }
    }

    public int LoadedCount
    {
        get => _loadedCount;
        private set { _loadedCount = value; OnPropertyChanged(); }
    }

    public int TotalCount
    {
        get => _totalCount;
        private set { _totalCount = value; OnPropertyChanged(); }
    }

    public int FaultCount
    {
        get => _faultCount;
        private set { _faultCount = value; OnPropertyChanged(); }
    }

    public string HealthLabel => FaultCount > 0
        ? $"{FaultCount} fault{(FaultCount == 1 ? "" : "s")}"
        : LoadedCount == TotalCount && TotalCount > 0
            ? "Healthy"
            : TotalCount == 0 ? "No plugins" : $"{LoadedCount}/{TotalCount} running";

    public string HealthColor => FaultCount > 0
        ? "#EF4444"
        : LoadedCount < TotalCount ? "#F59E0B"
        : "#22C55E";

    // -- Plugin table + search ---------------------------------------------------

    public ObservableCollection<PluginMonitorRow> Rows { get; } = new();
    public ICollectionView FilteredRows => _filteredRows;

    public string SearchText
    {
        get => _searchText;
        set
        {
            if (_searchText == value) return;
            _searchText = value;
            OnPropertyChanged();
            RefreshFilter();
        }
    }

    // -- Filter chips ------------------------------------------------------------

    public bool FilterStateLoaded
    {
        get => _filterStateLoaded;
        set { _filterStateLoaded = value; OnPropertyChanged(); RefreshFilter(); }
    }

    public bool FilterStateDisabled
    {
        get => _filterStateDisabled;
        set { _filterStateDisabled = value; OnPropertyChanged(); RefreshFilter(); }
    }

    public bool FilterStateFaulted
    {
        get => _filterStateFaulted;
        set { _filterStateFaulted = value; OnPropertyChanged(); RefreshFilter(); }
    }

    public bool FilterWarningsOnly
    {
        get => _filterWarningsOnly;
        set { _filterWarningsOnly = value; OnPropertyChanged(); RefreshFilter(); }
    }

    /// <summary>Shows "X of Y plugins" when a filter is active; empty string otherwise.</summary>
    public string VisibleCountLabel
    {
        get
        {
            var visible = _filteredRows.Cast<PluginMonitorRow>().Count();
            var total   = Rows.Count;
            return total == 0 ? string.Empty : $"{visible} of {total} plugins";
        }
    }

    // -- Selection ---------------------------------------------------------------

    public PluginMonitorRow? SelectedPlugin
    {
        get => _selectedPlugin;
        set
        {
            if (_selectedPlugin == value) return;
            _selectedPlugin = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(HasSelectedPlugin));
            OnPropertyChanged(nameof(TogglePluginLabel));
            OnPropertyChanged(nameof(TogglePluginIcon));
            OnPropertyChanged(nameof(HasFaultedPlugin));
            UpdateSelectedPluginDetail();
            UpdateSelectedMiniChart();
        }
    }

    public bool HasSelectedPlugin  => _selectedPlugin is not null;
    public bool HasFaultedPlugin   => _selectedPlugin?.State == "Error";

    public PluginDetailViewModel? SelectedPluginDetail
    {
        get => _selectedPluginDetail;
        private set { _selectedPluginDetail = value; OnPropertyChanged(); }
    }

    /// <summary>Mini-chart ViewModel for the currently selected plugin (drives the CPU/RAM tab).</summary>
    public PluginMiniChartViewModel? SelectedMiniChart
    {
        get => _selectedMiniChart;
        private set { _selectedMiniChart = value; OnPropertyChanged(); }
    }

    public string TogglePluginLabel => _selectedPlugin?.State is "Running" or "Loading" ? "Disable" : "Enable";
    public string TogglePluginIcon  => _selectedPlugin?.State is "Running" or "Loading" ? "\uE711" : "\uE8D8";

    // -- Event log ---------------------------------------------------------------

    public ObservableCollection<PluginEventEntry> EventLog { get; } = new();

    // -- Internals ---------------------------------------------------------------

    private void OnTimerTick(object? sender, EventArgs e) => Refresh();

    public void Refresh()
    {
        var plugins = _host.GetAllPlugins();
        var loaded  = plugins.Where(p => p.State == PluginState.Loaded).ToList();
        var now     = DateTime.UtcNow;

        // Aggregate CPU and memory (process-level, capped for display).
        double totalCpu = 0;
        long   totalMem = 0;

        if (loaded.Count > 0)
        {
            // Use the host's directly-sampled CPU% — NOT the ring-buffer latest.
            // The ring-buffer latest may be the init sample (recorded during batch startup
            // at ~100% CPU) until the first periodic tick fires. LastSampledCpuPercent is
            // only set by OnSamplingTick and starts at 0, giving a correct idle reading.
            totalCpu = _host.LastSampledCpuPercent;

            // All loaded plugins record the same process-level GC memory per tick.
            // Use the first plugin that has a snapshot rather than always loaded[0],
            // which may be null during the brief window before the first sampling tick.
            var memSnap = loaded
                .Select(e => e.Diagnostics.GetLatest())
                .FirstOrDefault(s => s is not null);
            if (memSnap is not null)
                totalMem = memSnap.MemoryBytes / (1024 * 1024);
        }

        CurrentCpu      = totalCpu;
        CurrentMemoryMb = totalMem;
        LoadedCount     = loaded.Count;
        TotalCount      = plugins.Count;
        FaultCount      = plugins.Count(p => p.State == PluginState.Faulted);
        OnPropertyChanged(nameof(HealthLabel));
        OnPropertyChanged(nameof(HealthColor));

        AddChartPoint(CpuHistory,    new ChartPoint(now, totalCpu));
        AddChartPoint(MemoryHistory, new ChartPoint(now, totalMem));

        // Weighted CPU per plugin: totalCpu × (plugin.AvgExecMs / Σ AvgExecMs)
        double sumExecMs = loaded.Sum(e => e.Diagnostics.AverageExecutionTime.TotalMilliseconds);

        // Evaluate alert thresholds on every tick.
        _alertEngine.Evaluate(loaded);

        // Sync rows collection
        var currentIds = plugins.Select(p => p.Manifest.Id).ToHashSet(StringComparer.OrdinalIgnoreCase);

        foreach (var stale in Rows.Where(r => !currentIds.Contains(r.Id)).ToList())
        {
            Rows.Remove(stale);
            _miniCharts.Remove(stale.Id);
        }

        foreach (var entry in plugins)
        {
            var snap = entry.Diagnostics.GetLatest();
            var row  = Rows.FirstOrDefault(r => r.Id == entry.Manifest.Id);

            if (row is null)
            {
                // Pre-assign MiniChart so the DataGrid binding resolves immediately.
                // Assigning after Rows.Add would be invisible to WPF (no PropertyChanged
                // on a plain auto-property at the time the cell is first bound).
                var newMini = GetOrCreateMiniChart(entry.Manifest.Id, entry.Manifest.Name);
                row = new PluginMonitorRow { Id = entry.Manifest.Id, MiniChart = newMini };
                Rows.Add(row);
            }

            // Compute weighted CPU and memory estimates for this plugin.
            // Weight is proportional to measured average execution time.
            //
            // Rules:
            //   • Non-loaded plugin (Faulted, Unloaded…) → weight = 0, no allocation.
            //   • sumExecMs > 0 (at least one plugin has measured data):
            //       – avgMs > 0 → proportional share   (avgMs / sumExecMs)
            //       – avgMs = 0 → weight = 0 (no evidence of CPU usage; do NOT fall back
            //                     to equal share here — that inflates the running total
            //                     above 100% and gives metrics to unmeasured plugins).
            //   • sumExecMs = 0 (no plugin has been measured yet at startup):
            //       → equal share across all loaded plugins as initial estimate.
            double avgMs  = entry.Diagnostics.AverageExecutionTime.TotalMilliseconds;
            double weight = entry.State != PluginState.Loaded
                ? 0
                : sumExecMs > 0
                    ? (avgMs > 0 ? avgMs / sumExecMs : 0)
                    : (loaded.Count > 0 ? 1.0 / loaded.Count : 0);
            double weightedCpu = Math.Clamp(totalCpu * weight, 0, 100);
            long   weightedMem = (long)Math.Round(totalMem * weight);

            row.Name           = entry.Manifest.Name;
            row.State          = StateLabel(entry.State);
            row.StateColor     = StateBadgeColor(entry.State);
            row.CpuPercent     = snap?.CpuPercent ?? 0;
            row.WeightedCpu    = weightedCpu;
            row.MemoryMb       = snap is not null ? snap.MemoryBytes / (1024 * 1024) : 0;
            row.WeightedMemMb  = weightedMem;
            row.AvgExecMs      = entry.Diagnostics.AverageExecutionTime.TotalMilliseconds;
            row.InitTimeMs     = entry.InitDuration.TotalMilliseconds;
            row.UptimeLabel    = FormatUptime(entry.Diagnostics.Uptime);
            // Track peak from weighted CPU (process-level PeakCpu() is identical for all plugins).
            if (weightedCpu > row.PeakCpu) row.PeakCpu = weightedCpu;
            row.IsResponsive   = entry.Diagnostics.IsResponsive;
            row.IsSlow         = _slowPluginIds.Contains(entry.Manifest.Id);
            row.FaultMessage   = entry.FaultException?.Message ?? string.Empty;
            row.Version        = entry.Manifest.Version ?? string.Empty;

            // Evaluate memory alert for this plugin
            if (_memoryAlertService != null)
            {
                var level = _memoryAlertService.EvaluateMemoryUsage((int)weightedMem);
                row.HasMemoryAlert = level != MemoryAlertLevel.Normal;
                row.MemoryAlertLevel = level.ToString();
                row.MemoryAlertColor = _memoryAlertService.GetAlertColorHex(level);
                row.MemoryAlertIcon = _memoryAlertService.GetAlertIcon(level);
                row.MemoryAlertMessage = _memoryAlertService.GetAlertMessage(level, (int)weightedMem);
            }

            // Push per-plugin weighted samples to sparkline mini chart.
            var mini = GetOrCreateMiniChart(entry.Manifest.Id, entry.Manifest.Name);
            row.MiniChart = mini;
            mini.PushSample(weightedCpu, weightedMem);
        }

        // Refresh detail pane when a plugin is selected.
        if (_selectedPlugin is not null)
        {
            OnPropertyChanged(nameof(TogglePluginLabel));
            OnPropertyChanged(nameof(TogglePluginIcon));
            OnPropertyChanged(nameof(HasFaultedPlugin));
            UpdateSelectedPluginDetail();
        }
    }

    private PluginMiniChartViewModel GetOrCreateMiniChart(string pluginId, string pluginName)
    {
        if (!_miniCharts.TryGetValue(pluginId, out var mini))
        {
            mini = new PluginMiniChartViewModel(pluginId, pluginName);
            _miniCharts[pluginId] = mini;
        }
        return mini;
    }

    private void UpdateSelectedPluginDetail()
    {
        if (_selectedPlugin is null) { SelectedPluginDetail = null; return; }
        var entry = _host.GetPlugin(_selectedPlugin.Id);
        if (entry is null) { SelectedPluginDetail = null; return; }
        var detail = _selectedPluginDetail ?? new PluginDetailViewModel();
        detail.Update(entry, _host.Permissions);
        SelectedPluginDetail = detail;
    }

    private void UpdateSelectedMiniChart()
    {
        SelectedMiniChart = _selectedPlugin is not null
            && _miniCharts.TryGetValue(_selectedPlugin.Id, out var mini) ? mini : null;
    }

    private void TogglePlugin()
    {
        if (_selectedPlugin is null) return;
        if (_selectedPlugin.State is "Running" or "Loading")
        {
            _ = _host.DisablePluginAsync(_selectedPlugin.Id);
            _outputService?.Info($"[{_selectedPlugin.Name}] Disabled by user");
        }
        else
        {
            _ = _host.EnablePluginAsync(_selectedPlugin.Id);
            _outputService?.Info($"[{_selectedPlugin.Name}] Enabled by user");
        }
    }

    private void ToggleRunning()
    {
        if (_isRunning) { _timer.Stop(); IsRunning = false; }
        else            { _timer.Start(); IsRunning = true; }
    }

    private void Reset()
    {
        CpuHistory.Clear();
        MemoryHistory.Clear();
        foreach (var mini in _miniCharts.Values) mini.Reset();
        AlertCount = 0;
        _alertEngine.ResetCooldowns();
    }

    private void ClearLog()
    {
        EventLog.Clear();
        AlertCount = 0;
    }

    private void SetInterval(int seconds)
    {
        if (seconds <= 0) return;
        IntervalSeconds = seconds;
        _timer.Interval = TimeSpan.FromSeconds(seconds);
    }

    // -- Host event handlers -----------------------------------------------------

    private void OnPluginLoaded(object? sender, PluginEventArgs e)
    {
        _slowPluginIds.Remove(e.PluginId);
        var initMs = _host.GetPlugin(e.PluginId)?.InitDuration.TotalMilliseconds ?? 0;
        AddEvent(new PluginEventEntry(Now(), "\uE73E", "#22C55E", e.PluginName, "Plugin loaded"));
        _outputService?.Info($"[{e.PluginName}] Loaded — init: {initMs:F0} ms");
    }

    private void OnPluginCrashed(object? sender, PluginFaultedEventArgs e)
    {
        var msg = $"Crashed ({e.Phase}): {e.Exception?.Message ?? "unknown error"}";
        AddEvent(new PluginEventEntry(Now(), "\uEA39", "#EF4444", e.PluginName, msg));
        _outputService?.Error($"[{e.PluginName}] {msg}");
    }

    private void OnSlowPluginDetected(object? sender, SlowPluginDetectedEventArgs e)
    {
        _slowPluginIds.Add(e.PluginId);
        var avgMs = e.AverageExecutionTime.TotalMilliseconds;
        AddEvent(new PluginEventEntry(Now(), "\uE946", "#F59E0B", e.PluginName,
                 $"Slow: avg {avgMs:F0} ms (threshold {e.Threshold.TotalMilliseconds:F0} ms)"));
        _outputService?.Warning($"[{e.PluginName}] Slow — avg exec: {avgMs:F0} ms");
    }

    private void OnAlertTriggered(object? sender, PluginAlertEventArgs e)
    {
        AlertCount++;
        AddEvent(new PluginEventEntry(Now(), "\uE7BA", "#F97316", e.PluginName, e.Message));
        _outputService?.Warning($"[{e.PluginName}] Alert: {e.Message}");
    }

    // -- Export commands ---------------------------------------------------------

    private void ExportTo(string format)
    {
        var dlg = new Microsoft.Win32.SaveFileDialog
        {
            Title      = "Export Plugin Diagnostics",
            Filter     = format == "csv"
                         ? "CSV files (*.csv)|*.csv|All files (*.*)|*.*"
                         : "JSON files (*.json)|*.json|All files (*.*)|*.*",
            DefaultExt = format,
            FileName   = $"plugin-diagnostics-{DateTime.Now:yyyyMMdd-HHmmss}.{format}"
        };

        if (dlg.ShowDialog() != true) return;

        var plugins  = _host.GetAllPlugins();
        var filePath = dlg.FileName;
        _ = format == "csv"
            ? _exporter.ExportCsvAsync(plugins, filePath)
            : _exporter.ExportJsonAsync(plugins, filePath);

        AddEvent(new PluginEventEntry(Now(), "\uE74E", "#22C55E", "Monitor",
                 $"Exported to {System.IO.Path.GetFileName(filePath)}"));
    }

    private void ExportCrashReport()
    {
        if (_selectedPlugin is null) return;
        var entry = _host.GetPlugin(_selectedPlugin.Id);
        if (entry is null) return;

        var dlg = new Microsoft.Win32.SaveFileDialog
        {
            Title      = $"Export Crash Report — {_selectedPlugin.Name}",
            Filter     = "Text files (*.txt)|*.txt|All files (*.*)|*.*",
            DefaultExt = "txt",
            FileName   = $"crash-{_selectedPlugin.Id}-{DateTime.Now:yyyyMMdd-HHmmss}.txt"
        };

        if (dlg.ShowDialog() != true) return;

        _ = _exporter.ExportCrashReportAsync(entry, dlg.FileName);
        AddEvent(new PluginEventEntry(Now(), "\uE74E", "#22C55E", _selectedPlugin.Name,
                 $"Crash report exported to {System.IO.Path.GetFileName(dlg.FileName)}"));
    }

    // -- Install / Uninstall -----------------------------------------------------

    /// <summary>
    /// Opens a file dialog and installs the selected .whxplugin package.
    /// Shows a progress overlay via <see cref="IsInstalling"/> during the async operation.
    /// </summary>
    private void InstallFromBrowse()
    {
        var dlg = new Microsoft.Win32.OpenFileDialog
        {
            Title      = "Install Plugin Package",
            Filter     = "Plugin packages (*.whxplugin)|*.whxplugin|All files (*.*)|*.*",
            DefaultExt = "whxplugin"
        };

        if (dlg.ShowDialog() != true) return;
        _ = InstallFromPathAsync(dlg.FileName);
    }

    /// <summary>
    /// Called by code-behind when a .whxplugin file is dropped onto the panel.
    /// </summary>
    public Task InstallFromDropAsync(string packagePath)
        => InstallFromPathAsync(packagePath);

    private async Task InstallFromPathAsync(string packagePath)
    {
        if (IsInstalling) return;
        IsInstalling = true;

        try
        {
            var entry = await _host.InstallFromFileAsync(packagePath).ConfigureAwait(true);
            AddEvent(new PluginEventEntry(Now(), "\uE73E", "#22C55E", entry.Manifest.Name,
                     $"Plugin installed from {System.IO.Path.GetFileName(packagePath)}"));
            Refresh();
        }
        catch (Exception ex)
        {
            AddEvent(new PluginEventEntry(Now(), "\uEA39", "#EF4444", "Monitor",
                     $"Install failed: {ex.Message}"));
        }
        finally
        {
            IsInstalling = false;
        }
    }

    /// <summary>
    /// Uninstalls a plugin after the code-behind has confirmed with the user.
    /// Removes the plugin from the host and deletes its directory from disk.
    /// </summary>
    public async Task UninstallConfirmedAsync(PluginMonitorRow row)
    {
        var entry = _host.GetPlugin(row.Id);
        var pluginDir = entry?.Manifest.ResolvedDirectory;

        try
        {
            await _host.UninstallPluginAsync(row.Id).ConfigureAwait(true);

            // Delete the physical plugin directory if we have a path.
            if (!string.IsNullOrEmpty(pluginDir) && Directory.Exists(pluginDir))
                Directory.Delete(pluginDir, recursive: true);

            AddEvent(new PluginEventEntry(Now(), "\uE74D", "#6B7280", row.Name, "Plugin uninstalled"));
            Refresh();
        }
        catch (Exception ex)
        {
            AddEvent(new PluginEventEntry(Now(), "\uEA39", "#EF4444", row.Name,
                     $"Uninstall failed: {ex.Message}"));
        }
    }

    // -- Helpers -----------------------------------------------------------------

    private void AddEvent(PluginEventEntry entry)
    {
        // Marshal to the UI thread — event handlers (PluginLoaded, Crashed, Slow) can fire
        // from background threads (LoadAllAsync uses ConfigureAwait(false)).
        if (!_dispatcher.CheckAccess())
        {
            _dispatcher.InvokeAsync(() => AddEvent(entry));
            return;
        }
        while (EventLog.Count >= MaxEventLog)
            EventLog.RemoveAt(0);
        EventLog.Add(entry);
    }

    /// <summary>
    /// Synthesizes "Plugin loaded" entries for plugins already in Loaded state when this
    /// ViewModel was created. Necessary because the VM is constructed after LoadAllAsync
    /// completes — the initial PluginLoaded events have already fired before subscription.
    /// </summary>
    private void SynthesizeInitialLoadEvents()
    {
        foreach (var entry in _host.GetAllPlugins()
                     .Where(p => p.State == PluginState.Loaded))
        {
            var initMs = entry.InitDuration.TotalMilliseconds;
            AddEvent(new PluginEventEntry(
                entry.LoadedAt.HasValue
                    ? entry.LoadedAt.Value.ToLocalTime().ToString("HH:mm:ss")
                    : Now(),
                "\uE73E", "#22C55E",
                entry.Manifest.Name,
                $"Plugin loaded (init: {initMs:F0} ms)"));
        }
    }

    private static void AddChartPoint(ObservableCollection<ChartPoint> col, ChartPoint point)
    {
        while (col.Count >= MaxChartPoints)
            col.RemoveAt(0);
        col.Add(point);
    }

    private static string FormatUptime(TimeSpan uptime)
    {
        if (uptime.TotalSeconds < 1) return "—";
        return uptime.TotalHours >= 1
            ? $"{(int)uptime.TotalHours:D2}:{uptime.Minutes:D2}:{uptime.Seconds:D2}"
            : $"{uptime.Minutes:D2}:{uptime.Seconds:D2}";
    }

    // -- Filter helpers ----------------------------------------------------------

    private sealed record FilterQuery(
        string? NameSubstring,
        string? StateFilter,
        bool WarningOnly,
        bool SlowOnly);

    private FilterQuery ParseSearchQuery(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return new FilterQuery(null, null, _filterWarningsOnly, false);

        if (text.StartsWith("state:", StringComparison.OrdinalIgnoreCase))
            return new FilterQuery(null, text[6..].Trim().ToLowerInvariant(), _filterWarningsOnly, false);

        if (text.Equals("warn", StringComparison.OrdinalIgnoreCase) ||
            text.Equals("warn:true", StringComparison.OrdinalIgnoreCase))
            return new FilterQuery(null, null, true, false);

        if (text.Equals("slow", StringComparison.OrdinalIgnoreCase) ||
            text.Equals("slow:true", StringComparison.OrdinalIgnoreCase))
            return new FilterQuery(null, null, _filterWarningsOnly, true);

        return new FilterQuery(text, null, _filterWarningsOnly, false);
    }

    private bool FilterPlugin(object item)
    {
        if (item is not PluginMonitorRow row) return false;
        var q = ParseSearchQuery(_searchText);

        // State chip filter — OR among active chips, AND with other criteria.
        var anyChipActive = _filterStateLoaded || _filterStateDisabled || _filterStateFaulted;
        if (anyChipActive)
        {
            var stateMatch = (_filterStateLoaded   && row.State == "Running")  ||
                             (_filterStateDisabled && row.State == "Disabled") ||
                             (_filterStateFaulted  && row.State == "Error");
            if (!stateMatch) return false;
        }

        if (q.StateFilter is not null &&
            !row.State.Contains(q.StateFilter, StringComparison.OrdinalIgnoreCase))
            return false;

        if (q.NameSubstring is not null &&
            !row.Name.Contains(q.NameSubstring, StringComparison.OrdinalIgnoreCase))
            return false;

        if (q.WarningOnly && !row.HasWarning) return false;
        if (q.SlowOnly    && !row.IsSlow)     return false;

        return true;
    }

    private void RefreshFilter()
    {
        _filteredRows.Refresh();
        OnPropertyChanged(nameof(VisibleCountLabel));
    }

    // -- New export commands (table copy & event log) ----------------------------

    private void CopyTableToClipboard()
    {
        var sb = new StringBuilder();
        sb.AppendLine("Name\tState\tCPU%\tPeak%\tMem MB\tUptime\tAvg ms\tInit ms");
        foreach (PluginMonitorRow row in _filteredRows)
            sb.AppendLine($"{row.Name}\t{row.State}\t{row.WeightedCpu:F1}\t{row.PeakCpu:F1}\t{row.WeightedMemMb}\t{row.UptimeLabel}\t{row.AvgExecMs:F1}\t{row.InitTimeMs:F0}");
        Clipboard.SetText(sb.ToString());
        AddEvent(new PluginEventEntry(Now(), "\uE8C8", "#22C55E", "Monitor", "Table copied to clipboard"));
    }

    private void ExportEventLog()
    {
        var dlg = new Microsoft.Win32.SaveFileDialog
        {
            Title      = "Export Event Log",
            Filter     = "Text files (*.txt)|*.txt|All files (*.*)|*.*",
            DefaultExt = "txt",
            FileName   = $"plugin-events-{DateTime.Now:yyyyMMdd-HHmmss}.txt"
        };
        if (dlg.ShowDialog() != true) return;
        var lines = EventLog.Select(e => $"{e.TimeLabel}  {e.PluginName}  {e.Message}");
        File.WriteAllLines(dlg.FileName, lines, Encoding.UTF8);
        AddEvent(new PluginEventEntry(Now(), "\uE74E", "#22C55E", "Monitor",
                 $"Event log exported to {Path.GetFileName(dlg.FileName)}"));
    }

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

    private static string StateBadgeColor(PluginState state) => state switch
    {
        PluginState.Loaded       => "#22C55E",
        PluginState.Loading      => "#F59E0B",
        PluginState.Disabled     => "#6B7280",
        PluginState.Faulted      => "#EF4444",
        PluginState.Incompatible => "#F97316",
        _                        => "#9CA3AF"
    };

    private static string Now() => DateTime.Now.ToString("HH:mm:ss");

    // -- Memory Alert Service Initialization -------------------------------------

    private void InitializeMemoryAlertService(MemoryAlertThresholds? thresholds)
    {
        try
        {
            var effectiveThresholds = thresholds ?? MemoryAlertThresholds.CreateDefault();

            if (effectiveThresholds.Validate())
            {
                _memoryAlertService = new MemoryAlertService(effectiveThresholds);
            }
            else
            {
                // Fallback to defaults if validation fails
                _memoryAlertService = new MemoryAlertService(MemoryAlertThresholds.CreateDefault());
                _outputService?.Warning("[Plugin Monitor] Invalid memory thresholds, using defaults.");
            }
        }
        catch (Exception ex)
        {
            // Fallback to defaults on any error
            _memoryAlertService = new MemoryAlertService(MemoryAlertThresholds.CreateDefault());
            _outputService?.Error($"[Plugin Monitor] Failed to initialize memory alert service: {ex.Message}");
        }
    }

    // -- IDisposable -------------------------------------------------------------

    public void Dispose()
    {
        _timer.Stop();
        _timer.Tick                -= OnTimerTick;
        _host.PluginLoaded         -= OnPluginLoaded;
        _host.PluginCrashed        -= OnPluginCrashed;
        _host.SlowPluginDetected   -= OnSlowPluginDetected;
        _alertEngine.AlertTriggered -= OnAlertTriggered;
    }

    private void OnPropertyChanged([CallerMemberName] string? name = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}

// ==========================================================================
// RelayCommand
// ==========================================================================

/// <summary>Minimal ICommand implementation for toolbar actions.</summary>
internal sealed class RelayCommand : ICommand
{
    private readonly Action<object?>    _execute;
    private readonly Func<object?, bool>? _canExecute;

    public RelayCommand(Action<object?> execute, Func<object?, bool>? canExecute = null)
    {
        _execute    = execute;
        _canExecute = canExecute;
    }

    public event EventHandler? CanExecuteChanged
    {
        add    => System.Windows.Input.CommandManager.RequerySuggested += value;
        remove => System.Windows.Input.CommandManager.RequerySuggested -= value;
    }

    public bool CanExecute(object? parameter) => _canExecute?.Invoke(parameter) ?? true;
    public void Execute(object? parameter)    => _execute(parameter);
}
