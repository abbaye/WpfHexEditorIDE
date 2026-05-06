// ==========================================================
// Project: WpfHexEditor.Editor.XamlDesigner
// File: Services/CodeGen/CodeBehindSyncService.cs
// Author: Derek Tremblay
// Created: 2026-05-05
// Description:
//     Orchestrates the XAML → C# code-behind generation pipeline.
//     Subscribes to XAML text changes, debounces (400ms), scans the new XAML
//     with XamlCodeBehindScanner, diffs against the last model to detect changes,
//     merges with the existing .xaml.cs via CodeBehindMergeEngine, and writes
//     the result via ICodeBehindDocumentBuffer.
//     Implements IDiagnosticSource for ErrorList panel integration.
//
// Architecture Notes:
//     Observer pattern — subscribes to external XamlSourceChanged event.
//     Debounce via DispatcherTimer (UI thread) — scan + merge on a Task pool thread.
//     Diagnostics emitted for: no x:Class, parse failure, merge conflicts, handler
//     signature mismatches, identifier collisions.
//     Thread safety: _lastModel and _isEnabled protected by Interlocked / volatile.
// ==========================================================

using System.Threading;
using System.Windows.Threading;
using WpfHexEditor.Editor.Core;
using WpfHexEditor.Editor.XamlDesigner.Models;

namespace WpfHexEditor.Editor.XamlDesigner.Services.CodeGen;

/// <summary>
/// Event arguments raised when a code-behind regeneration completes.
/// </summary>
public sealed class CodeBehindRegenEventArgs : EventArgs
{
    public XamlCodeModel Model   { get; init; } = XamlCodeModel.Empty;
    public bool          Success { get; init; }
    public string?       Error   { get; init; }
}

/// <summary>
/// Orchestrates the XAML → code-behind generation pipeline with debouncing,
/// merging, and diagnostic reporting.
/// </summary>
public sealed class CodeBehindSyncService : IDiagnosticSource
{
    // ── Dependencies ──────────────────────────────────────────────────────────

    private readonly XamlCodeBehindScanner   _scanner;
    private readonly XamlCodeBehindGenerator _generator;
    private readonly CodeBehindMergeEngine   _merger;
    private readonly EventArgTypeResolver    _argResolver;

    // ── State ─────────────────────────────────────────────────────────────────

    private ICodeBehindDocumentBuffer? _buffer;
    private XamlCodeModel              _lastModel   = XamlCodeModel.Empty;
    private string?                    _xamlSource;
    private string?                    _xamlFilePath;
    private volatile bool              _isEnabled   = true;
    private CancellationTokenSource    _cts         = new();

    // Debounce timer — fires 400ms after last XAML change.
    private readonly DispatcherTimer _debounce;

    // ── Diagnostics (IDiagnosticSource) ───────────────────────────────────────

    private readonly List<DiagnosticEntry> _diagnostics = [];

    public string                          SourceLabel   => "Code-Behind Generator";
    public IReadOnlyList<DiagnosticEntry>  GetDiagnostics() => _diagnostics;
    public event EventHandler?             DiagnosticsChanged;

    // ── Public events ─────────────────────────────────────────────────────────

    /// <summary>Fired on the UI thread after each successful or failed regeneration.</summary>
    public event EventHandler<CodeBehindRegenEventArgs>? CodeBehindRegenerated;

    // ── Properties ────────────────────────────────────────────────────────────

    /// <summary>Enables or disables live synchronization.</summary>
    public bool IsEnabled
    {
        get => _isEnabled;
        set => _isEnabled = value;
    }

    /// <summary>The last successfully scanned code model.</summary>
    public XamlCodeModel CurrentModel => _lastModel;

    // ── Constructor ───────────────────────────────────────────────────────────

    public CodeBehindSyncService()
    {
        _argResolver = new EventArgTypeResolver();
        _scanner     = new XamlCodeBehindScanner();
        _generator   = new XamlCodeBehindGenerator(_argResolver);
        _merger      = new CodeBehindMergeEngine();

        _debounce = new DispatcherTimer(DispatcherPriority.Background)
        {
            Interval = TimeSpan.FromMilliseconds(400)
        };
        _debounce.Tick += async (_, _) =>
        {
            _debounce.Stop();
            await RunPipelineAsync().ConfigureAwait(false);
        };
    }

    // ── Configuration ─────────────────────────────────────────────────────────

    /// <summary>
    /// Attaches the service to a specific XAML file and its code-behind buffer.
    /// Call when the designer opens a new document.
    /// </summary>
    public void Attach(string xamlFilePath, ICodeBehindDocumentBuffer buffer)
    {
        _xamlFilePath = xamlFilePath;
        _buffer       = buffer;
        _lastModel    = XamlCodeModel.Empty;
        ClearDiagnostics();
    }

    /// <summary>Detaches from the current document.</summary>
    public void Detach()
    {
        _debounce.Stop();
        _cts.Cancel();
        _cts.Dispose();
        _cts = new CancellationTokenSource();
        _buffer       = null;
        _xamlFilePath = null;
        _xamlSource   = null;
        _lastModel    = XamlCodeModel.Empty;
        ClearDiagnostics();
    }

    /// <summary>
    /// Notifies the service that the XAML source has changed.
    /// Restarts the debounce timer. Call from the UI thread.
    /// </summary>
    public void OnXamlSourceChanged(string newSource)
    {
        _xamlSource = newSource;

        if (!_isEnabled || _buffer is null)
            return;

        _debounce.Stop();
        _debounce.Start();
    }

    /// <summary>Forces an immediate regeneration, bypassing the debounce timer.</summary>
    public async Task ForceRegenerateAsync(CancellationToken ct = default)
    {
        _debounce.Stop();
        await RunPipelineAsync(ct).ConfigureAwait(false);
    }

    /// <summary>
    /// Generates a preview of the code-behind without writing to disk.
    /// </summary>
    public Task<string> GeneratePreviewAsync(string xamlSource, CancellationToken ct = default)
    {
        var source    = string.IsNullOrEmpty(xamlSource) ? _xamlSource : xamlSource;
        if (source is null)
            return Task.FromResult("// No XAML source available.");
        var model     = _scanner.Scan(source);
        var generated = _generator.Generate(model, _xamlFilePath ?? "Preview.xaml");
        return Task.FromResult(generated ?? "// No x:Class found — code generation disabled.");
    }

    // ── Plugin-level event arg provider registration ──────────────────────────

    /// <summary>Registers a plugin-contributed event-arg type provider.</summary>
    public void RegisterEventArgProvider(IEventArgTypeProvider provider)
        => _argResolver.Register(provider);

    // ── Pipeline ──────────────────────────────────────────────────────────────

    private async Task RunPipelineAsync(CancellationToken ct = default)
    {
        if (!_isEnabled || _buffer is null || _xamlSource is null || _xamlFilePath is null)
            return;

        using var linked = CancellationTokenSource.CreateLinkedTokenSource(ct, _cts.Token);
        var token = linked.Token;

        try
        {
            // Scan on a background thread (XDocument.Parse is CPU-bound).
            var newModel = await Task.Run(() => _scanner.Scan(_xamlSource), token)
                                     .ConfigureAwait(false);

            // Emit diagnostic when x:Class is absent.
            if (!newModel.IsCodeGenEnabled)
            {
                ReportDiagnostic(DiagnosticSeverity.Warning,
                    "No x:Class attribute found — code-behind generation disabled. " +
                    "Add x:Class=\"Namespace.ClassName\" to the root element to enable it.",
                    -1, -1);
                FireRegen(newModel, success: false,
                    error: "No x:Class attribute — code generation disabled.");
                return;
            }

            // Diff against last known model.
            var patch = _scanner.Diff(_lastModel, newModel);

            // Short-circuit when nothing changed.
            if (patch.IsEmpty && _lastModel.IsCodeGenEnabled)
            {
                ClearDiagnostics();
                return;
            }

            // Generate new code.
            var generated = _generator.Generate(newModel, _xamlFilePath);
            if (generated is null)
                return;

            // Read existing code-behind.
            string existing = await _buffer.ReadAsync(token).ConfigureAwait(false);

            // Merge.
            var mergeResult = await Task.Run(() => _merger.Merge(existing, generated), token)
                                         .ConfigureAwait(false);

            // Report merge conflicts as warnings.
            ClearDiagnostics();
            foreach (var conflict in mergeResult.Conflicts)
            {
                ReportDiagnostic(DiagnosticSeverity.Warning,
                    $"Code-behind merge: {conflict.MemberName} — {conflict.Reason}", -1, -1);
            }

            // Identifier collision check.
            var names = newModel.NamedElements.Select(e => e.Name).ToList();
            var dupes = names.GroupBy(n => n).Where(g => g.Count() > 1).Select(g => g.Key);
            foreach (var dupe in dupes)
            {
                ReportDiagnostic(DiagnosticSeverity.Error,
                    $"Duplicate x:Name identifier '{dupe}' — code-behind field collision. Rename one element.", -1, -1);
            }

            // Write merged result.
            await _buffer.WriteAsync(mergeResult.MergedSource, token).ConfigureAwait(false);

            _lastModel = newModel;
            FireRegen(newModel, success: true);
        }
        catch (OperationCanceledException)
        {
            // Normal cancellation — no diagnostic.
        }
        catch (Exception ex)
        {
            ReportDiagnostic(DiagnosticSeverity.Error,
                $"Code-behind generation failed: {ex.Message}", -1, -1);
            FireRegen(XamlCodeModel.Empty, success: false, error: ex.Message);
        }
    }

    // ── Diagnostic helpers ────────────────────────────────────────────────────

    private void ReportDiagnostic(DiagnosticSeverity severity, string message, int line, int col)
    {
        _diagnostics.Add(new DiagnosticEntry(
            Severity:    severity,
            Code:        "CODEGEN",
            Description: message,
            Line:        line < 0 ? null : line,
            Column:      col  < 0 ? null : col));
        DiagnosticsChanged?.Invoke(this, EventArgs.Empty);
    }

    private void ClearDiagnostics()
    {
        if (_diagnostics.Count == 0) return;
        _diagnostics.Clear();
        DiagnosticsChanged?.Invoke(this, EventArgs.Empty);
    }

    private void FireRegen(XamlCodeModel model, bool success, string? error = null)
    {
        // Marshal back to UI thread for event subscribers (panel VMs).
        if (Dispatcher.CurrentDispatcher.CheckAccess())
            RaiseRegen(model, success, error);
        else
            Dispatcher.CurrentDispatcher.BeginInvoke(() => RaiseRegen(model, success, error));
    }

    private void RaiseRegen(XamlCodeModel model, bool success, string? error)
    {
        CodeBehindRegenerated?.Invoke(this, new CodeBehindRegenEventArgs
        {
            Model   = model,
            Success = success,
            Error   = error
        });
    }
}
