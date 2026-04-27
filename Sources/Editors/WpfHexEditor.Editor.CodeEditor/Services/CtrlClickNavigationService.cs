// ==========================================================
// Project: WpfHexEditor.Editor.CodeEditor
// File: Services/CtrlClickNavigationService.cs
// Author: Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.6
// Created: 2026-03-17
// Description:
//     Background service that resolves the definition target for the symbol
//     currently under Ctrl+hover.  When resolved it raises TargetResolved so
//     the CodeEditor can update _hoveredSymbolZone with the navigation target
//     and display the hand cursor + underline.
//
// Architecture Notes:
//     Pattern: Service / Observer — mirrors InlineHintsService / HoverQuickInfoService.
//     Debounce: 200 ms (fast — user is already holding Ctrl and hovering).
//     Resolution priority:
//       1. ILspClient.DefinitionAsync  (preferred — cross-file, external detection)
//       2. Local CodeStructureParser scan  (no LSP fallback)
//     IsExternal flag: set when LSP URI is a metadata scheme OR resolved file path
//     does not exist on disk.
//     Threading: Task.Run background, ConfigureAwait(true) back to UI thread.
// ==========================================================

using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Threading;
using WpfHexEditor.Editor.CodeEditor.Models;
using WpfHexEditor.Editor.CodeEditor.NavigationBar;
using WpfHexEditor.Editor.Core.LSP;

namespace WpfHexEditor.Editor.CodeEditor.Services;

// ── DTO ───────────────────────────────────────────────────────────────────────

/// <summary>Resolved navigation target for a Ctrl+hovered symbol.</summary>
internal sealed record CtrlClickTarget(
    int     Line,
    int     StartCol,
    int     EndCol,
    string  SymbolName,
    string  TargetFilePath,
    int     TargetLine,
    int     TargetColumn,
    bool    IsExternal);

// ── Service ───────────────────────────────────────────────────────────────────

internal sealed class CtrlClickNavigationService : IDisposable
{
    // ── State ─────────────────────────────────────────────────────────────────

    private readonly DispatcherTimer _debounce;
    private CancellationTokenSource  _cts      = new();
    private ILspClient?              _lspClient;
    private bool                     _disposed;

    // Pending request parameters
    private string _pendingFilePath = string.Empty;
    private int    _pendingLine;
    private int    _pendingColumn;
    private int    _pendingStartCol;
    private int    _pendingEndCol;
    private string _pendingWord     = string.Empty;

    // Document line snapshot taken on the UI thread
    private IReadOnlyList<CodeLine> _lineSnapshot = [];

    // ── Public API ────────────────────────────────────────────────────────────

    /// <summary>
    /// Raised on the UI thread when the target has been resolved.
    /// Null means resolution failed or was cancelled.
    /// </summary>
    public event EventHandler<CtrlClickTarget?>? TargetResolved;

    // ── Constructor ───────────────────────────────────────────────────────────

    internal CtrlClickNavigationService()
    {
        _debounce       = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(200) };
        _debounce.Tick += OnDebounceTick;
    }

    // ── Configuration ─────────────────────────────────────────────────────────

    internal void SetLspClient(ILspClient? client)
        => _lspClient = client;

    // ── Request / Cancel ──────────────────────────────────────────────────────

    internal void RequestAsync(
        string filePath, int line, int column,
        int startCol, int endCol, string word,
        IReadOnlyList<CodeLine> lineSnapshot)
    {
        _pendingFilePath = filePath;
        _pendingLine     = line;
        _pendingColumn   = column;
        _pendingStartCol = startCol;
        _pendingEndCol   = endCol;
        _pendingWord     = word;
        _lineSnapshot    = lineSnapshot;

        _debounce.Stop();
        _debounce.Start();
    }

    internal void Cancel()
    {
        _debounce.Stop();
        CancelPending();
        TargetResolved?.Invoke(this, null);
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _debounce.Stop();
        CancelPending();
        _cts.Dispose();
    }

    // ── Debounce tick ─────────────────────────────────────────────────────────

    private void OnDebounceTick(object? sender, EventArgs e)
    {
        _debounce.Stop();
        _ = ResolveAsync();
    }

    // ── Core resolution ───────────────────────────────────────────────────────

    private void CancelPending()
    {
        _cts.Cancel();
        _cts.Dispose();
        _cts = new CancellationTokenSource();
    }

    private async Task ResolveAsync()
    {
        CancelPending();
        var ct = _cts.Token;

        string filePath  = _pendingFilePath;
        int    line      = _pendingLine;
        int    column    = _pendingColumn;
        int    startCol  = _pendingStartCol;
        int    endCol    = _pendingEndCol;
        string word      = _pendingWord;
        var    lineSnap  = _lineSnapshot;
        var    lsp       = _lspClient;

        try
        {
            var target = await Task.Run(
                () => ComputeAsync(filePath, line, column, startCol, endCol,
                                   word, lineSnap, lsp, ct),
                ct).ConfigureAwait(true);

            if (ct.IsCancellationRequested) return;
            TargetResolved?.Invoke(this, target);
        }
        catch (OperationCanceledException) { /* expected */ }
    }

    // ── Background computation ────────────────────────────────────────────────

    private static async Task<CtrlClickTarget?> ComputeAsync(
        string filePath, int line, int column,
        int startCol, int endCol, string word,
        IReadOnlyList<CodeLine> lines,
        ILspClient? lsp,
        CancellationToken ct)
    {
        // 1. LSP definition — cross-file + external detection ─────────────────
        if (lsp?.IsInitialized == true)
        {
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            using var linked  = CancellationTokenSource.CreateLinkedTokenSource(ct, timeout.Token);

            try
            {
                var locations = await lsp
                    .DefinitionAsync(filePath, line, column, linked.Token)
                    .ConfigureAwait(false);

                if (locations.Count > 0)
                {
                    var loc        = locations[0];
                    bool external  = IsExternalUri(loc.Uri);
                    string target  = external ? loc.Uri : UriToPath(loc.Uri);

                    return new CtrlClickTarget(
                        Line:           line,
                        StartCol:       startCol,
                        EndCol:         endCol,
                        SymbolName:     word,
                        TargetFilePath: target,
                        TargetLine:     loc.StartLine,
                        TargetColumn:   loc.StartColumn,
                        IsExternal:     external);
                }
            }
            catch (OperationCanceledException) when (!ct.IsCancellationRequested)
            {
                // LSP timeout — fall through
            }
        }

        ct.ThrowIfCancellationRequested();

        // 2. Local document scan ───────────────────────────────────────────────
        return ComputeLocalTarget(filePath, line, startCol, endCol, word, lines);
    }

    // ── Local fallback ────────────────────────────────────────────────────────

    private static CtrlClickTarget? ComputeLocalTarget(
        string filePath, int hoveredLine,
        int startCol, int endCol, string word,
        IReadOnlyList<CodeLine> lines)
    {
        if (string.IsNullOrEmpty(word) || lines.Count == 0) return null;

        // whfmt: var:name → find declaration ("storeAs", "varName", "indexVar", "mappedValueStoreAs")
        string? varRefName = null;
        if (word.StartsWith("var:", StringComparison.Ordinal))
            varRefName = word.Substring(4);
        else if (word.StartsWith("\"var:", StringComparison.Ordinal))
            varRefName = word.Substring(5).TrimEnd('"');

        if (!string.IsNullOrEmpty(varRefName))
        {
            var declLine = FindWhfmtVarDeclaration(lines, varRefName, hoveredLine);
            if (declLine >= 0)
                return new CtrlClickTarget(
                    Line:           hoveredLine,
                    StartCol:       startCol,
                    EndCol:         endCol,
                    SymbolName:     word,
                    TargetFilePath: filePath,
                    TargetLine:     declLine,
                    TargetColumn:   0,
                    IsExternal:     false);
        }

        var snapshot  = CodeStructureParser.Parse(lines);
        var all       = snapshot.Types.Concat(snapshot.Members).ToList();

        var decl = all.FirstOrDefault(item =>
            string.Equals(item.Name, word, StringComparison.Ordinal)
            && item.Line != hoveredLine);

        if (decl is null) return null;

        return new CtrlClickTarget(
            Line:           hoveredLine,
            StartCol:       startCol,
            EndCol:         endCol,
            SymbolName:     word,
            TargetFilePath: filePath,
            TargetLine:     decl.Line,
            TargetColumn:   0,
            IsExternal:     false);
    }

    /// <summary>
    /// Scans whfmt JSON lines for the first line that declares <paramref name="varName"/>
    /// via "storeAs", "varName", "indexVar", or "mappedValueStoreAs".
    /// Returns the 0-based line index or -1 if not found.
    /// </summary>
    private static int FindWhfmtVarDeclaration(IReadOnlyList<CodeLine> lines, string varName, int excludeLine)
    {
        // Patterns: "storeAs": "varName"  |  "varName": "varName"  etc.
        var declaringProps = new[] { "storeAs", "varName", "indexVar", "mappedValueStoreAs" };
        for (int i = 0; i < lines.Count; i++)
        {
            if (i == excludeLine) continue;
            var text = lines[i].Text;
            foreach (var prop in declaringProps)
            {
                // e.g. "storeAs": "myVar"
                if (text.Contains($"\"{prop}\"") && text.Contains($"\"{varName}\""))
                    return i;
            }
        }
        return -1;
    }

    // ── URI helpers ───────────────────────────────────────────────────────────

    private static bool IsExternalUri(string uri) =>
        uri.StartsWith("metadata:", StringComparison.OrdinalIgnoreCase) ||
        (!uri.StartsWith("file:", StringComparison.OrdinalIgnoreCase));

    private static string UriToPath(string uri)
    {
        try
        {
            string path = new Uri(uri).LocalPath;
            return File.Exists(path) ? path : uri;
        }
        catch
        {
            return uri;
        }
    }
}
