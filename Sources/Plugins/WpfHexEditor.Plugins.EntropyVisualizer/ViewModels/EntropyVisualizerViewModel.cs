// ==========================================================
// Project: WpfHexEditor.Plugins.EntropyVisualizer
// File: ViewModels/EntropyVisualizerViewModel.cs
// Description: MVVM ViewModel for the Entropy Visualizer panel.
//              Drives analysis, progress, graph data, and navigation.
// Architecture Notes:
//     Standalone-safe: _navigateTo is null when no IDE context is available.
//     Chunks list is immutable after analysis — no incremental mutation.
// ==========================================================

using System.ComponentModel;
using System.Runtime.CompilerServices;
using WpfHexEditor.Plugins.EntropyVisualizer.Models;
using WpfHexEditor.Plugins.EntropyVisualizer.Services;

namespace WpfHexEditor.Plugins.EntropyVisualizer.ViewModels;

internal sealed class EntropyVisualizerViewModel : INotifyPropertyChanged
{
    private readonly EntropyCalculatorService _service = new();
    private Action<long>?                     _navigateTo;
    private CancellationTokenSource?          _analysisCts;

    // ── Bindable state ────────────────────────────────────────────────────────

    private IReadOnlyList<EntropyChunk> _chunks = [];
    public  IReadOnlyList<EntropyChunk> Chunks
    {
        get => _chunks;
        private set { _chunks = value; OnPropertyChanged(); OnPropertyChanged(nameof(HasResults)); }
    }

    public bool HasResults => _chunks.Count > 0;

    private bool _hasFile;
    public  bool  HasFile
    {
        get => _hasFile;
        set { _hasFile = value; OnPropertyChanged(); }
    }

    private bool _isAnalyzing;
    public  bool  IsAnalyzing
    {
        get => _isAnalyzing;
        set { _isAnalyzing = value; OnPropertyChanged(); OnPropertyChanged(nameof(IsIdle)); }
    }

    public bool IsIdle => !_isAnalyzing;

    private double _progress;
    public  double  Progress
    {
        get => _progress;
        set { _progress = value; OnPropertyChanged(); }
    }

    private string _statusText = string.Empty;
    public  string  StatusText
    {
        get => _statusText;
        set { _statusText = value; OnPropertyChanged(); }
    }

    private int _chunkSize = 256;
    public  int  ChunkSize
    {
        get => _chunkSize;
        set { _chunkSize = Math.Max(16, value); OnPropertyChanged(); }
    }

    private EntropyChunk? _hoveredChunk;
    public  EntropyChunk? HoveredChunk
    {
        get => _hoveredChunk;
        set { _hoveredChunk = value; OnPropertyChanged(); UpdateHoverStatus(); }
    }

    // ── Wiring ────────────────────────────────────────────────────────────────

    public void SetNavigateCallback(Action<long>? navigateTo) => _navigateTo = navigateTo;

    public void NavigateTo(EntropyChunk? chunk)
    {
        if (chunk is not null) _navigateTo?.Invoke(chunk.Offset);
    }

    // ── Analysis ──────────────────────────────────────────────────────────────

    public async Task AnalyzeAsync(byte[] data)
    {
        CancelAndDisposeCts();
        _analysisCts = new CancellationTokenSource();
        var ct = _analysisCts.Token;

        Chunks      = [];
        IsAnalyzing = true;
        Progress    = 0;
        StatusText  = Properties.EntropyVisualizerResources.EntropyVisualizer_Analyzing;

        try
        {
            var progress = new Progress<double>(p => Progress = p * 100);
            var result   = await _service.CalculateAsync(data, _chunkSize, progress, ct);
            Chunks     = result;
            StatusText = string.Format(
                Properties.EntropyVisualizerResources.EntropyVisualizer_StatusChunks,
                result.Count);
        }
        catch (OperationCanceledException)
        {
            StatusText = string.Empty;
        }
        finally
        {
            IsAnalyzing = false;
            Progress    = 0;
        }
    }

    public void Cancel()
    {
        CancelAndDisposeCts();
    }

    public void Clear()
    {
        CancelAndDisposeCts();
        Chunks       = [];
        HasFile      = false;
        StatusText   = string.Empty;
        IsAnalyzing  = false;
        HoveredChunk = null;
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private void CancelAndDisposeCts()
    {
        _analysisCts?.Cancel();
        _analysisCts?.Dispose();
        _analysisCts = null;
    }

    private void UpdateHoverStatus()
    {
        if (_hoveredChunk is null)
        {
            StatusText = _chunks.Count > 0
                ? string.Format(Properties.EntropyVisualizerResources.EntropyVisualizer_StatusChunks, _chunks.Count)
                : Properties.EntropyVisualizerResources.EntropyVisualizer_StatusReady;
            return;
        }

        StatusText = string.Format(
            Properties.EntropyVisualizerResources.EntropyVisualizer_StatusOffset,
            $"0x{_hoveredChunk.Offset:X8}",
            _hoveredChunk.Entropy);
    }

    // ── INotifyPropertyChanged ────────────────────────────────────────────────

    public event PropertyChangedEventHandler? PropertyChanged;
    private void OnPropertyChanged([CallerMemberName] string? name = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
