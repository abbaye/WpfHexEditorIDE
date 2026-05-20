//////////////////////////////////////////////////////
// GNU Affero General Public License v3.0 - 2026
// Author : Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.6
// Project: WpfHexEditor.App
// File: BinaryAnalysis/Services/EntropyHeatmapService.cs
// Description: Orchestrates lazy entropy heatmap rendering on the active HexEditor.
//              Subscribes to IHexEditorService.ViewportScrolled and FileOpened,
//              debounces 200 ms, computes entropy for the visible window + lookahead
//              buffer, then injects CustomBackgroundBlocks tagged "entropy-heatmap".
//              IDisposable — unsubscribes cleanly.
//////////////////////////////////////////////////////

using System.Windows.Media;
using System.Windows.Threading;
using WpfHexEditor.Core;
using WpfHexEditor.Core.Options;
using WpfHexEditor.SDK.Contracts.Services;

namespace WpfHexEditor.App.BinaryAnalysis.Services;

/// <summary>
/// Manages the entropy heatmap overlay on the active HexEditor.
/// Lifecycle: Enable() → scrolls trigger lazy recompute → Disable() clears overlay.
/// </summary>
public sealed class EntropyHeatmapService : IDisposable
{
    private const string Tag         = "entropy-heatmap";
    private const long   LookaheadBytes = 8192; // bytes ahead/behind viewport to pre-compute

    private readonly IHexEditorService   _hex;
    private readonly HexEditorDefaultSettings _settings;
    private readonly DispatcherTimer     _debounce;

    private bool _enabled;
    private bool _disposed;

    public EntropyHeatmapService(IHexEditorService hex, HexEditorDefaultSettings settings)
    {
        _hex      = hex;
        _settings = settings;

        _debounce          = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(200) };
        _debounce.Tick    += OnDebounceElapsed;

        _hex.ViewportScrolled += OnViewportScrolled;
        _hex.FileOpened       += OnFileOpened;
    }

    // ── Public API ───────────────────────────────────────────────────────────

    public bool IsEnabled => _enabled;

    public void Enable()
    {
        _enabled = true;
        RequestRefresh();
    }

    public void Disable()
    {
        _enabled = false;
        _debounce.Stop();
        _hex.ClearCustomBackgroundBlockByTag(Tag);
    }

    public void Toggle()
    {
        if (_enabled) Disable();
        else          Enable();
    }

    /// <summary>Called when color theme or window size changes in settings.</summary>
    public void RefreshSettings()
    {
        if (_enabled) RequestRefresh();
    }

    // ── Event handlers ───────────────────────────────────────────────────────

    private void OnViewportScrolled(object? sender, EventArgs e) => RequestRefresh();

    private void OnFileOpened(object? sender, EventArgs e)
    {
        _hex.ClearCustomBackgroundBlockByTag(Tag);
        if (_enabled) RequestRefresh();
    }

    // ── Debounce + compute ───────────────────────────────────────────────────

    private void RequestRefresh()
    {
        _debounce.Stop();
        _debounce.Start();
    }

    private void OnDebounceElapsed(object? sender, EventArgs e)
    {
        _debounce.Stop();
        if (!_enabled || !_hex.IsActive) return;

        var filePath = _hex.CurrentFilePath;
        if (string.IsNullOrEmpty(filePath)) return;

        long first     = Math.Max(0, _hex.FirstVisibleByteOffset - LookaheadBytes);
        long last      = _hex.LastVisibleByteOffset + LookaheadBytes;
        long fileSize  = _hex.FileSize;
        if (fileSize <= 0) return;

        last = Math.Min(last, fileSize);
        long length   = last - first;
        if (length <= 0) return;

        int windowSize = _settings.EntropyWindowSize > 0
            ? _settings.EntropyWindowSize
            : (int)EntropyWindowSize.Medium;

        var theme = (EntropyColorTheme)Math.Clamp(_settings.EntropyColorTheme, 0, 2);

        // Compute off-thread, inject on UI thread
        Task.Run(() =>
        {
            var blocks = EntropyService.ComputeRange(filePath, first, length, windowSize);
            return (blocks, theme);
        }).ContinueWith(t =>
        {
            if (t.IsFaulted || !_enabled || !_hex.IsActive) return;

            _hex.ClearCustomBackgroundBlockByTag(Tag);
            foreach (var eb in t.Result.blocks)
            {
                var color  = EntropyColorMapper.Map(eb.Entropy, t.Result.theme);
                var brush  = new SolidColorBrush(color) { Opacity = 0.25 };
                brush.Freeze();
                _hex.AddCustomBackgroundBlock(new CustomBackgroundBlock
                {
                    StartOffset  = eb.Offset,
                    Length       = eb.Length,
                    Color        = brush,
                    Description  = Tag,
                    ShowInTooltip = false,
                });
            }
        }, TaskScheduler.FromCurrentSynchronizationContext());
    }

    // ── IDisposable ──────────────────────────────────────────────────────────

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _debounce.Stop();
        _debounce.Tick -= OnDebounceElapsed;
        _hex.ViewportScrolled -= OnViewportScrolled;
        _hex.FileOpened       -= OnFileOpened;
        _hex.ClearCustomBackgroundBlockByTag(Tag);
    }
}
