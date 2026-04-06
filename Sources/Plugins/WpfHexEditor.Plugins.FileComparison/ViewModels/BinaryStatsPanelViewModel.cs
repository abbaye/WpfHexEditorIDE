// Project      : WpfHexEditorControl
// File         : ViewModels/BinaryStatsPanelViewModel.cs
// Description  : ViewModel for the BinaryStatsPanel drawer â€” exposes entropy, frequency,
//                and file summary data derived from BinaryDiffAnalysis.
// Architecture : INPC, no WPF dependency.  Populated asynchronously after diff completes.

using System.ComponentModel;
using System.Runtime.CompilerServices;
using WpfHexEditor.Core.Diff.Models;
using WpfHexEditor.Core.Events;
using WpfHexEditor.Core.ViewModels;

namespace WpfHexEditor.Plugins.FileComparison.ViewModels;

public sealed class BinaryStatsPanelViewModel : ViewModelBase
{
    // ── Backing fields ────────────────────────────────────────────────────────

    private BinaryDiffAnalysis?    _analysis;
    private BinaryDiffStats?       _stats;
    private string                 _leftFileName  = string.Empty;
    private string                 _rightFileName = string.Empty;
    private long                   _leftFileSize  = 0;
    private long                   _rightFileSize = 0;
    private bool                   _isVisible     = true;
    private FormatDetectionResult? _leftFormat;
    private FormatDetectionResult? _rightFormat;

    // ── Public properties ──────────────────────────────────────────────────────

    public string LeftFileName
    {
        get => _leftFileName;
        set => SetField(ref _leftFileName, value);
    }

    public string RightFileName
    {
        get => _rightFileName;
        set => SetField(ref _rightFileName, value);
    }

    public long LeftFileSize
    {
        get => _leftFileSize;
        set { SetField(ref _leftFileSize, value); OnPropertyChanged(nameof(LeftFileSizeText)); }
    }

    public long RightFileSize
    {
        get => _rightFileSize;
        set { SetField(ref _rightFileSize, value); OnPropertyChanged(nameof(RightFileSizeText)); }
    }

    public string LeftFileSizeText  => FormatSize(_leftFileSize);
    public string RightFileSizeText => FormatSize(_rightFileSize);

    /// <summary>Whether the stats panel drawer is open.</summary>
    public bool IsVisible
    {
        get => _isVisible;
        set => SetField(ref _isVisible, value);
    }

    // ── Format detection results ──────────────────────────────────────────────

    public FormatDetectionResult? LeftFormat
    {
        get => _leftFormat;
        set
        {
            SetField(ref _leftFormat, value);
            OnPropertyChanged(nameof(LeftFormatBadge));
            OnPropertyChanged(nameof(HasLeftFormatBadge));
        }
    }

    public FormatDetectionResult? RightFormat
    {
        get => _rightFormat;
        set
        {
            SetField(ref _rightFormat, value);
            OnPropertyChanged(nameof(RightFormatBadge));
            OnPropertyChanged(nameof(HasRightFormatBadge));
        }
    }

    public string LeftFormatBadge   => BuildBadge(_leftFormat);
    public string RightFormatBadge  => BuildBadge(_rightFormat);
    public bool   HasLeftFormatBadge  => _leftFormat is { Success: true };
    public bool   HasRightFormatBadge => _rightFormat is { Success: true };

    private static string BuildBadge(FormatDetectionResult? r)
    {
        if (r is not { Success: true, Format: { } fmt }) return string.Empty;
        return $"{fmt.FormatName}  Â·  {r.Confidence:P0}";
    }

    // ── Analysis data ─────────────────────────────────────────────────────────

    public BinaryDiffAnalysis? Analysis
    {
        get => _analysis;
        set
        {
            if (_analysis == value) return;
            _analysis = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(EntropyLeft));
            OnPropertyChanged(nameof(EntropyRight));
            OnPropertyChanged(nameof(NibbleFreqLeft));
            OnPropertyChanged(nameof(NibbleFreqRight));
            OnPropertyChanged(nameof(AvgEntropyLeftText));
            OnPropertyChanged(nameof(AvgEntropyRightText));
        }
    }

    public BinaryDiffStats? Stats
    {
        get => _stats;
        set
        {
            if (_stats == value) return;
            _stats = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(ModifiedBytes));
            OnPropertyChanged(nameof(InsertedBytes));
            OnPropertyChanged(nameof(DeletedBytes));
            OnPropertyChanged(nameof(ModifiedPercent));
            OnPropertyChanged(nameof(InsertedPercent));
            OnPropertyChanged(nameof(DeletedPercent));
        }
    }

    // ── Shortcut properties for XAML binding ──────────────────────────────────

    public double[] EntropyLeft   => _analysis?.EntropyLeft  ?? [];
    public double[] EntropyRight  => _analysis?.EntropyRight ?? [];
    public int[]    NibbleFreqLeft  => _analysis?.NibbleFreqLeft  ?? new int[16];
    public int[]    NibbleFreqRight => _analysis?.NibbleFreqRight ?? new int[16];

    public string AvgEntropyLeftText  => _analysis is not null
        ? $"H = {_analysis.AvgEntropyLeft:F2} bits" : "â€”";
    public string AvgEntropyRightText => _analysis is not null
        ? $"H = {_analysis.AvgEntropyRight:F2} bits" : "â€”";

    public long ModifiedBytes => _stats?.ModifiedBytes ?? 0;
    public long InsertedBytes => _stats?.InsertedBytes ?? 0;
    public long DeletedBytes  => _stats?.DeletedBytes  ?? 0;

    public double ModifiedPercent => _stats is not null && _stats.LeftFileSize > 0
        ? Math.Min(100.0, (double)_stats.ModifiedBytes / _stats.LeftFileSize * 100) : 0;
    public double InsertedPercent => _stats is not null && _stats.RightFileSize > 0
        ? Math.Min(100.0, (double)_stats.InsertedBytes / _stats.RightFileSize * 100) : 0;
    public double DeletedPercent  => _stats is not null && _stats.LeftFileSize > 0
        ? Math.Min(100.0, (double)_stats.DeletedBytes  / _stats.LeftFileSize  * 100) : 0;

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static string FormatSize(long bytes)
        => bytes switch
        {
            >= 1024 * 1024 * 1024L => $"{bytes / (1024.0 * 1024 * 1024):F1} GB",
            >= 1024 * 1024L        => $"{bytes / (1024.0 * 1024):F1} MB",
            >= 1024L               => $"{bytes / 1024.0:F1} KB",
            _                      => $"{bytes} B"
        };

    // ── INotifyPropertyChanged ────────────────────────────────────────────────



    private void SetField<T>(ref T field, T value, [CallerMemberName] string? name = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value)) return;
        field = value;
        OnPropertyChanged(name);
    }
}
