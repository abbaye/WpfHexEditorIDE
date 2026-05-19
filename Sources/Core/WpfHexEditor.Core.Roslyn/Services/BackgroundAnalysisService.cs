// ==========================================================
// Project: WpfHexEditor.Core.Roslyn
// File: Services/BackgroundAnalysisService.cs
// Contributors: Claude Opus 4.6
// Created: 2026-04-01
// Description:
//     Debounced background analysis loop. Receives change notifications,
//     waits for quiet period, then runs Roslyn diagnostics and fires events.
//
// Architecture Notes:
//     Uses Channel<string> as work queue. Single consumer loop with 500ms debounce.
//     Produces immutable Solution snapshots — no locking needed on consumer side.
// ==========================================================

using System.Threading.Channels;
using Microsoft.CodeAnalysis;
using WpfHexEditor.Editor.Core.LSP;

namespace WpfHexEditor.Core.Roslyn.Services;

internal sealed class BackgroundAnalysisService : IDisposable
{
    private readonly RoslynWorkspaceManager _workspaceManager;
    private readonly Channel<string> _changeQueue = Channel.CreateUnbounded<string>(
        new UnboundedChannelOptions { SingleReader = true });
    private readonly CancellationTokenSource _cts = new();
    private readonly Task _loopTask;
    private readonly System.Windows.Threading.Dispatcher _dispatcher;
    private int _disposed;

    public event EventHandler<LspDiagnosticsReceivedEventArgs>? DiagnosticsReady;

    public BackgroundAnalysisService(
        RoslynWorkspaceManager workspaceManager,
        System.Windows.Threading.Dispatcher dispatcher)
    {
        _workspaceManager = workspaceManager;
        _dispatcher = dispatcher;
        _loopTask = Task.Run(AnalysisLoopAsync);
    }

    public void NotifyChanged(string filePath)
    {
        _changeQueue.Writer.TryWrite(filePath);
    }

    private async Task AnalysisLoopAsync()
    {
        var pending = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var ct = _cts.Token;

        try
        {
            while (!ct.IsCancellationRequested)
            {
                // Wait for first change.
                var first = await _changeQueue.Reader.ReadAsync(ct).ConfigureAwait(false);
                pending.Add(first);

                // Drain any queued changes with 500ms debounce.
                using var debounce = new CancellationTokenSource(TimeSpan.FromMilliseconds(500));
                using var linked = CancellationTokenSource.CreateLinkedTokenSource(ct, debounce.Token);
                try
                {
                    while (await _changeQueue.Reader.WaitToReadAsync(linked.Token).ConfigureAwait(false))
                        while (_changeQueue.Reader.TryRead(out var path))
                            pending.Add(path);
                }
                catch (OperationCanceledException) when (debounce.IsCancellationRequested)
                {
                    // Debounce expired — proceed with analysis.
                }

                // Analyze all pending documents + cascade to other open docs in same project.
                var analyzed = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                foreach (var filePath in pending)
                {
                    if (ct.IsCancellationRequested) break;
                    await AnalyzeDocumentAsync(filePath, ct).ConfigureAwait(false);
                    analyzed.Add(filePath);
                }

                // Cascade: re-analyze other open documents that may be affected by the change.
                foreach (var openPath in _workspaceManager.OpenDocumentPaths)
                {
                    if (ct.IsCancellationRequested) break;
                    if (analyzed.Contains(openPath)) continue;
                    await AnalyzeDocumentAsync(openPath, ct).ConfigureAwait(false);
                }

                pending.Clear();
            }
        }
        catch (OperationCanceledException) { }
    }

    private async Task AnalyzeDocumentAsync(string filePath, CancellationToken ct)
    {
        var document = _workspaceManager.GetDocument(filePath);
        if (document is null) return;

        var semanticModel = await document.GetSemanticModelAsync(ct).ConfigureAwait(false);
        if (semanticModel is null) return;

        var rawDiagnostics = semanticModel.GetDiagnostics(cancellationToken: ct);
        var mapped = new List<LspDiagnostic>();

        foreach (var diag in rawDiagnostics)
        {
            if (diag.Severity == DiagnosticSeverity.Hidden) continue;

            var span = diag.Location.GetLineSpan();
            if (!span.IsValid) continue;

            mapped.Add(new LspDiagnostic
            {
                StartLine   = span.StartLinePosition.Line,
                StartColumn = span.StartLinePosition.Character,
                EndLine     = span.EndLinePosition.Line,
                EndColumn   = span.EndLinePosition.Character,
                Message     = diag.GetMessage(),
                Severity    = MapSeverity(diag.Severity),
                Code        = diag.Id,
            });
        }

        var uri = new Uri(filePath).AbsoluteUri;
        var args = new LspDiagnosticsReceivedEventArgs
        {
            DocumentUri = uri,
            Diagnostics = mapped,
        };

        _ = _dispatcher.InvokeAsync(() => DiagnosticsReady?.Invoke(this, args));
    }

    private static string MapSeverity(DiagnosticSeverity severity) => severity switch
    {
        DiagnosticSeverity.Error   => "error",
        DiagnosticSeverity.Warning => "warning",
        DiagnosticSeverity.Info    => "information",
        _                          => "hint",
    };

    public void Dispose()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0) return;
        _cts.Cancel();
        _changeQueue.Writer.TryComplete();
        try { _loopTask.Wait(TimeSpan.FromSeconds(2)); } catch { /* cancelled or timed out */ }
        _cts.Dispose();
    }
}
