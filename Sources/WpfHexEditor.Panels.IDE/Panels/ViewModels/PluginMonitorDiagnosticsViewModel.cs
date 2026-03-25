//////////////////////////////////////////////
// GNU Affero General Public License v3.0 - 2026
// Author : Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.6
//////////////////////////////////////////////

using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using WpfHexEditor.PluginHost;
using WpfHexEditor.SDK.Commands;

namespace WpfHexEditor.Panels.IDE.Panels.ViewModels;

/// <summary>
/// PHASE 5: Diagnostics tab ViewModel for Plugin Monitor panel.
/// Provides observability into the metrics engine health and sampling status.
/// </summary>
public sealed class PluginMonitorDiagnosticsViewModel : INotifyPropertyChanged
{
    private readonly WpfPluginHost _host;
    private bool _isInitialized;
    private long _sampleCount;
    private double _lastCpuPercent;
    private string _samplingInterval = "5s";
    private string _engineStatus = "Initializing...";

    public event PropertyChangedEventHandler? PropertyChanged;

    public PluginMonitorDiagnosticsViewModel(WpfPluginHost host)
    {
        _host = host ?? throw new ArgumentNullException(nameof(host));

        ForceSampleCommand = new RelayCommand(_ => _ = ForceSampleAsync());
        RestartEngineCommand = new RelayCommand(_ => RestartEngine());

        UpdateMetrics();

        // Subscribe to metrics events for live updates
        _host.MetricsEngine.MetricsSampled += (s, e) => UpdateMetrics();
    }

    // -- Properties --

    public bool IsInitialized
    {
        get => _isInitialized;
        private set { _isInitialized = value; OnPropertyChanged(); }
    }

    public long SampleCount
    {
        get => _sampleCount;
        private set { _sampleCount = value; OnPropertyChanged(); }
    }

    public double LastCpuPercent
    {
        get => _lastCpuPercent;
        private set { _lastCpuPercent = value; OnPropertyChanged(); }
    }

    public string SamplingInterval
    {
        get => _samplingInterval;
        private set { _samplingInterval = value; OnPropertyChanged(); }
    }

    public string EngineStatus
    {
        get => _engineStatus;
        private set { _engineStatus = value; OnPropertyChanged(); }
    }

    // -- Commands --

    public ICommand ForceSampleCommand { get; }
    public ICommand RestartEngineCommand { get; }

    // -- Methods --

    private void UpdateMetrics()
    {
        IsInitialized = _host.MetricsEngine.IsInitialized;
        SampleCount = _host.MetricsEngine.SampleCount;
        LastCpuPercent = _host.MetricsEngine.LastSampledCpuPercent;
        SamplingInterval = $"{_host.DiagnosticSamplingInterval.TotalSeconds:F1}s";
        EngineStatus = IsInitialized ? "Running" : "Initializing...";
    }

    private async Task ForceSampleAsync()
    {
        await _host.MetricsEngine.ForceSampleNowAsync();
        UpdateMetrics();
    }

    private void RestartEngine()
    {
        _host.MetricsEngine.Stop();
        _host.MetricsEngine.Start();
        UpdateMetrics();
    }

    private void OnPropertyChanged([CallerMemberName] string? name = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
