// ==========================================================
// Project: WpfHexEditor.PluginHost
// File: TimedHexEditorService.cs
// Author: Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.6
// Created: 2026-03-08
// Description:
//     Per-plugin proxy for IHexEditorService that intercepts event
//     subscriptions and records the synchronous execution time of each
//     plugin's event handlers into its diagnostics ring buffer.
//     This gives PluginMonitoringViewModel a non-zero avgMs value to
//     use for proportional CPU/RAM distribution in the Plugin Monitor.
//
// Architecture Notes:
//     Implements the Proxy / Decorator pattern over IHexEditorService.
//     One instance per loaded plugin; injected via PluginScopedContext.
//     All properties and methods delegate directly to the inner service.
//     Events intercept add/remove to maintain a per-plugin invocation list.
//     The inner service subscription is lazily created on first add (once only).
//     Thread-safety: subscriptions and event firings both happen on the UI thread.
//
// ==========================================================

using System.Diagnostics;
using WpfHexEditor.Core.Interfaces;
using WpfHexEditor.PluginHost.Monitoring;
using WpfHexEditor.SDK.Contracts.Services;

namespace WpfHexEditor.PluginHost;

/// <summary>
/// Proxy over <see cref="IHexEditorService"/> that measures the synchronous
/// execution time of each plugin's event handlers and feeds the samples into the
/// plugin's <see cref="PluginDiagnosticsCollector"/>.
/// <para>
/// PHASE 4: Now also notifies MetricsEngine for active sampling.
/// </para>
/// </summary>
internal sealed class TimedHexEditorService : IHexEditorService
{
    private readonly IHexEditorService _inner;
    private readonly PluginDiagnosticsCollector _diagnostics;
    private readonly PluginMetricsEngine _metricsEngine;
    private string? _pluginId; // Set lazily on first callback

    // Per-event managed invocation lists — independent per plugin instance.
    private EventHandler? _selectionChanged;
    private EventHandler? _viewportScrolled;
    private EventHandler? _fileOpened;
    private EventHandler<FormatDetectedArgs>? _formatDetected;
    private EventHandler? _activeEditorChanged;

    // Lazy-subscribe guards: attach to the inner service at most once per event.
    private bool _selectionChangedSubscribed;
    private bool _viewportScrolledSubscribed;
    private bool _fileOpenedSubscribed;
    private bool _formatDetectedSubscribed;
    private bool _activeEditorChangedSubscribed;

    public TimedHexEditorService(
        IHexEditorService inner, 
        PluginDiagnosticsCollector diagnostics,
        PluginMetricsEngine metricsEngine)
    {
        _inner       = inner       ?? throw new ArgumentNullException(nameof(inner));
        _diagnostics = diagnostics ?? throw new ArgumentNullException(nameof(diagnostics));
        _metricsEngine = metricsEngine ?? throw new ArgumentNullException(nameof(metricsEngine));
    }

    /// <summary>
    /// Sets the plugin ID for activity tracking (called by WpfPluginHost after context creation).
    /// </summary>
    internal void SetPluginId(string pluginId) => _pluginId = pluginId;

    // ── Properties (pure delegation) ─────────────────────────────────────────

    public bool    IsActive              => _inner.IsActive;
    public string? CurrentFilePath       => _inner.CurrentFilePath;
    public long    FileSize              => _inner.FileSize;
    public long    CurrentOffset         => _inner.CurrentOffset;
    public long    SelectionStart        => _inner.SelectionStart;
    public long    SelectionStop         => _inner.SelectionStop;
    public long    SelectionLength       => _inner.SelectionLength;
    public long    FirstVisibleByteOffset => _inner.FirstVisibleByteOffset;
    public long    LastVisibleByteOffset  => _inner.LastVisibleByteOffset;

    // ── Methods (pure delegation) ─────────────────────────────────────────────

    public byte[]              ReadBytes(long offset, int length)      => _inner.ReadBytes(offset, length);
    public byte[]              GetSelectedBytes()                       => _inner.GetSelectedBytes();
    public IReadOnlyList<long> SearchHex(string hexPattern)            => _inner.SearchHex(hexPattern);
    public IReadOnlyList<long> SearchText(string text)                 => _inner.SearchText(text);
    public void                WriteBytes(long offset, byte[] data)    => _inner.WriteBytes(offset, data);
    public void                SetSelection(long start, long end)      => _inner.SetSelection(start, end);
    public void                NavigateTo(long offset)                 => _inner.NavigateTo(offset);
    public void                ConnectParsedFieldsPanel(IParsedFieldsPanel panel) => _inner.ConnectParsedFieldsPanel(panel);
    public void                DisconnectParsedFieldsPanel()           => _inner.DisconnectParsedFieldsPanel();

    // ── Events (timed interception) ───────────────────────────────────────────

    /// <inheritdoc />
    public event EventHandler SelectionChanged
    {
        add
        {
            _selectionChanged += value;
            if (!_selectionChangedSubscribed)
            {
                _inner.SelectionChanged += OnInnerSelectionChanged;
                _selectionChangedSubscribed = true;
            }
        }
        remove => _selectionChanged -= value;
    }

    /// <inheritdoc />
    public event EventHandler ViewportScrolled
    {
        add
        {
            _viewportScrolled += value;
            if (!_viewportScrolledSubscribed)
            {
                _inner.ViewportScrolled += OnInnerViewportScrolled;
                _viewportScrolledSubscribed = true;
            }
        }
        remove => _viewportScrolled -= value;
    }

    /// <inheritdoc />
    public event EventHandler FileOpened
    {
        add
        {
            _fileOpened += value;
            if (!_fileOpenedSubscribed)
            {
                _inner.FileOpened += OnInnerFileOpened;
                _fileOpenedSubscribed = true;
            }
        }
        remove => _fileOpened -= value;
    }

    /// <inheritdoc />
    public event EventHandler<FormatDetectedArgs> FormatDetected
    {
        add
        {
            _formatDetected += value;
            if (!_formatDetectedSubscribed)
            {
                _inner.FormatDetected += OnInnerFormatDetected;
                _formatDetectedSubscribed = true;
            }
        }
        remove => _formatDetected -= value;
    }

    /// <inheritdoc />
    public event EventHandler ActiveEditorChanged
    {
        add
        {
            _activeEditorChanged += value;
            if (!_activeEditorChangedSubscribed)
            {
                _inner.ActiveEditorChanged += OnInnerActiveEditorChanged;
                _activeEditorChangedSubscribed = true;
            }
        }
        remove => _activeEditorChanged -= value;
    }

    // ── Inner event handlers (relay + timing) ─────────────────────────────────

    private void OnInnerSelectionChanged(object? s, EventArgs e)
        => InvokeTimedHandlers(ref _selectionChanged, e);

    private void OnInnerViewportScrolled(object? s, EventArgs e)
        => InvokeTimedHandlers(ref _viewportScrolled, e);

    private void OnInnerFileOpened(object? s, EventArgs e)
        => InvokeTimedHandlers(ref _fileOpened, e);

    private void OnInnerActiveEditorChanged(object? s, EventArgs e)
        => InvokeTimedHandlers(ref _activeEditorChanged, e);

    private void OnInnerFormatDetected(object? s, FormatDetectedArgs e)
    {
        var snapshot = _formatDetected;
        if (snapshot is null) return;
        var sw = Stopwatch.StartNew();
        snapshot.Invoke(this, e);
        sw.Stop();
        RecordExecution(sw.Elapsed);
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private void InvokeTimedHandlers(ref EventHandler? handlers, EventArgs e)
    {
        var snapshot = handlers;
        if (snapshot is null) return;
        var sw = Stopwatch.StartNew();
        snapshot.Invoke(this, e);
        sw.Stop();
        RecordExecution(sw.Elapsed);
    }

    /// <summary>
    /// Appends an execution-time sample to the plugin's diagnostics ring buffer.
    /// CPU% and MemoryBytes are carried forward from the latest periodic snapshot
    /// so the ring buffer remains internally consistent.
    /// 
    /// PHASE 4: Also notifies MetricsEngine for active sampling and activity tracking.
    /// </summary>
    private void RecordExecution(TimeSpan elapsed)
    {
        var latest = _diagnostics.GetLatest();
        _diagnostics.Record(
            latest?.CpuPercent  ?? 0,
            latest?.MemoryBytes ?? 0,
            elapsed);

        // PHASE 4: Notify MetricsEngine of plugin activity
        if (!string.IsNullOrEmpty(_pluginId))
        {
            _metricsEngine.RecordPluginActivity(_pluginId);

            // Queue active sample for immediate processing
            _ = _metricsEngine.EnqueueActiveSampleAsync(_pluginId, elapsed);
        }

        // Update diagnostics collector activity tracking
        _diagnostics.LastActivityTimestamp = DateTime.UtcNow;
        _diagnostics.SamplingPriority = Math.Min(_diagnostics.SamplingPriority + 1, 10);
    }
}
