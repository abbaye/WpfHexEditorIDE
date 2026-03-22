// ==========================================================
// Project: WpfHexEditor.Editor.ClassDiagram
// File: Services/ClassToSyncService.cs
// Author: Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.6
// Created: 2026-03-19
// Description:
//     Debounced bidirectional sync service between the DSL text pane
//     and the diagram canvas. Canvas→Code fires after 300 ms;
//     Code→Canvas fires after 500 ms.
//
// Architecture Notes:
//     Pattern: Observer with debounce timers.
//     Two independent DispatcherTimers prevent flooding: each schedule
//     call restarts its own timer. Events carry the ready payload;
//     the host wires CodeUpdateReady and CanvasUpdateReady.
// ==========================================================

using System.Windows.Threading;
using WpfHexEditor.Editor.ClassDiagram.Core.Model;

namespace WpfHexEditor.Editor.ClassDiagram.Services;

/// <summary>
/// Debounced bidirectional synchronisation between the DSL code pane and diagram canvas.
/// </summary>
public sealed class ClassToSyncService
{
    private readonly DispatcherTimer _canvasToCodeTimer;
    private readonly DispatcherTimer _codeToCanvasTimer;

    private string? _pendingDsl;
    private DiagramDocument? _pendingDocument;

    public ClassToSyncService()
    {
        _canvasToCodeTimer = new DispatcherTimer(DispatcherPriority.Background)
        {
            Interval = TimeSpan.FromMilliseconds(300)
        };
        _canvasToCodeTimer.Tick += OnCanvasToCodeTick;

        _codeToCanvasTimer = new DispatcherTimer(DispatcherPriority.Background)
        {
            Interval = TimeSpan.FromMilliseconds(500)
        };
        _codeToCanvasTimer.Tick += OnCodeToCanvasTick;
    }

    // ---------------------------------------------------------------------------
    // Events
    // ---------------------------------------------------------------------------

    /// <summary>Fired when the debounced canvas-to-code payload is ready.</summary>
    public event EventHandler<string>? CodeUpdateReady;

    /// <summary>Fired when the debounced code-to-canvas payload is ready.</summary>
    public event EventHandler<DiagramDocument>? CanvasUpdateReady;

    // ---------------------------------------------------------------------------
    // Schedule methods
    // ---------------------------------------------------------------------------

    /// <summary>
    /// Schedules a canvas→code update. Repeated calls within 300 ms restart the timer.
    /// </summary>
    public void ScheduleCanvasToCode(string dsl)
    {
        _pendingDsl = dsl;
        _canvasToCodeTimer.Stop();
        _canvasToCodeTimer.Start();
    }

    /// <summary>
    /// Schedules a code→canvas update. Repeated calls within 500 ms restart the timer.
    /// </summary>
    public void ScheduleCodeToCanvas(DiagramDocument doc)
    {
        _pendingDocument = doc;
        _codeToCanvasTimer.Stop();
        _codeToCanvasTimer.Start();
    }

    // ---------------------------------------------------------------------------
    // Stop
    // ---------------------------------------------------------------------------

    /// <summary>Stops both timers and discards pending payloads.</summary>
    public void Stop()
    {
        _canvasToCodeTimer.Stop();
        _codeToCanvasTimer.Stop();
        _pendingDsl = null;
        _pendingDocument = null;
    }

    // ---------------------------------------------------------------------------
    // Timer callbacks
    // ---------------------------------------------------------------------------

    private void OnCanvasToCodeTick(object? sender, EventArgs e)
    {
        _canvasToCodeTimer.Stop();
        if (_pendingDsl is null) return;
        string dsl = _pendingDsl;
        _pendingDsl = null;
        CodeUpdateReady?.Invoke(this, dsl);
    }

    private void OnCodeToCanvasTick(object? sender, EventArgs e)
    {
        _codeToCanvasTimer.Stop();
        if (_pendingDocument is null) return;
        DiagramDocument doc = _pendingDocument;
        _pendingDocument = null;
        CanvasUpdateReady?.Invoke(this, doc);
    }
}
