// ==========================================================
// Project: WpfHexEditor.Plugins.AssemblyExplorer
// File: ViewModels/SourceViewModel.cs
// Author: Derek Tremblay
// Created: 2026-03-16
// License: GNU Affero General Public License v3.0 (AGPL-3.0)
// Description:
//     ViewModel for the Source tab in the Assembly Explorer detail pane.
//     When a method node is selected, LoadAsync:
//       1. Opens the portable PDB (PdbReader.TryOpen)
//       2. Reads sequence points for the method
//       3. Resolves the SourceLink URL (PdbReader + SourceLinkResolver)
//       4. Loads a 3-line preview from the local source file (if present)
//     GoToSourceCommand fires OpenFileRequested so the hosting panel can open
//     the file in the IDE TextEditor without creating a WPF dependency here.
//     OpenSourceLinkCommand launches the browser via Process.Start.
//
// Architecture Notes:
//     Pattern: MVVM — populated by AssemblyDetailViewModel.ShowNodeAsync.
//     Runs on a background Task to avoid blocking the UI thread.
//     CancellationTokenSource is recreated on each selection.
// ==========================================================

using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Windows.Input;
using WpfHexEditor.Core.AssemblyAnalysis.Models;
using WpfHexEditor.Core.AssemblyAnalysis.Services;
using WpfHexEditor.SDK.Commands;

namespace WpfHexEditor.Plugins.AssemblyExplorer.ViewModels;

/// <summary>
/// Wraps a <see cref="SequencePoint"/> for display in the Source tab DataGrid.
/// </summary>
public sealed class SequencePointViewModel
{
    public SequencePointViewModel(SequencePoint sp)
    {
        IlOffset    = $"IL_{sp.IlOffset:X4}";
        Line        = sp.IsHidden ? "hidden" : sp.StartLine.ToString();
        Column      = sp.IsHidden ? string.Empty : sp.StartColumn.ToString();
        SourceFile  = Path.GetFileName(sp.DocumentPath ?? string.Empty);
        IsHidden    = sp.IsHidden;
    }

    public string IlOffset   { get; }
    public string Line       { get; }
    public string Column     { get; }
    public string SourceFile { get; }
    public bool   IsHidden   { get; }
}

/// <summary>
/// Provides PDB / SourceLink data for the Source tab.
/// </summary>
public sealed class SourceViewModel : AssemblyNodeViewModel
{
    private CancellationTokenSource? _cts;

    // ── AssemblyNodeViewModel overrides ───────────────────────────────────────

    public override string DisplayName => "Source";
    public override string IconGlyph   => "\uE943"; // FileCode glyph

    // ── State ─────────────────────────────────────────────────────────────────

    private bool    _isPdbAvailable;
    private bool    _isSourceLinkAvailable;
    private bool    _isLoading;
    private string? _sourceFilePath;
    private int     _sourceStartLine;
    private string? _sourcePreview;
    private string? _sourceLinkUrl;
    private string  _statusMessage = "Select a method node to view source information.";

    public bool IsPdbAvailable
    {
        get => _isPdbAvailable;
        private set => SetField(ref _isPdbAvailable, value);
    }

    public bool IsSourceLinkAvailable
    {
        get => _isSourceLinkAvailable;
        private set => SetField(ref _isSourceLinkAvailable, value);
    }

    public new bool IsLoading
    {
        get => _isLoading;
        private set => SetField(ref _isLoading, value);
    }

    public string? SourceFilePath
    {
        get => _sourceFilePath;
        private set => SetField(ref _sourceFilePath, value);
    }

    public int SourceStartLine
    {
        get => _sourceStartLine;
        private set => SetField(ref _sourceStartLine, value);
    }

    /// <summary>
    /// 3-line snippet from the local source file around the first sequence point.
    /// Null when the local file is not found on disk.
    /// </summary>
    public string? SourcePreview
    {
        get => _sourcePreview;
        private set => SetField(ref _sourcePreview, value);
    }

    /// <summary>
    /// HTTP URL linking directly to the source line on GitHub / AzDO.
    /// Null when SourceLink is not embedded in the PDB or the mapping doesn't match.
    /// </summary>
    public string? SourceLinkUrl
    {
        get => _sourceLinkUrl;
        private set => SetField(ref _sourceLinkUrl, value);
    }

    public string StatusMessage
    {
        get => _statusMessage;
        private set => SetField(ref _statusMessage, value);
    }

    /// <summary>All sequence points for the currently selected method.</summary>
    public ObservableCollection<SequencePointViewModel> SequencePoints { get; } = [];

    // ── Commands ──────────────────────────────────────────────────────────────

    /// <summary>
    /// Opens the local source file in the IDE at <see cref="SourceStartLine"/>.
    /// Fires <see cref="OpenFileRequested"/> — the hosting code-behind handles the actual navigation.
    /// </summary>
    public ICommand GoToSourceCommand     { get; }

    /// <summary>Opens <see cref="SourceLinkUrl"/> in the default browser.</summary>
    public ICommand OpenSourceLinkCommand { get; }

    /// <summary>Fired when the user requests to open the local source file.</summary>
    public event Action<string, int>? OpenFileRequested;

    // ── Constructor ───────────────────────────────────────────────────────────

    public SourceViewModel()
    {
        GoToSourceCommand = new RelayCommand(
            _ => { if (SourceFilePath is not null) OpenFileRequested?.Invoke(SourceFilePath, SourceStartLine); },
            _ => SourceFilePath is not null && File.Exists(SourceFilePath));

        OpenSourceLinkCommand = new RelayCommand(
            _ =>
            {
                if (SourceLinkUrl is null) return;
                try { Process.Start(new ProcessStartInfo(SourceLinkUrl) { UseShellExecute = true }); }
                catch { /* ignore if browser can't be launched */ }
            },
            _ => SourceLinkUrl is not null);
    }

    // ── Public API ────────────────────────────────────────────────────────────

    /// <summary>
    /// Asynchronously loads PDB data for <paramref name="method"/>.
    /// Any previous in-flight load is cancelled first.
    /// </summary>
    public async Task LoadAsync(MemberModel method, string assemblyFilePath, CancellationToken ct)
    {
        _cts?.Cancel();
        _cts = new CancellationTokenSource();
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(ct, _cts.Token);
        var linkedCt = linked.Token;

        bool first = TryFirstLoad();
        if (first) IsLoading = true;
        Reset();

        try
        {
            var (seqPoints, locals, sourceLink, filePath, startLine) = await Task.Run(() =>
            {
                linkedCt.ThrowIfCancellationRequested();
                return ReadPdbData(method.MetadataToken, assemblyFilePath);
            }, linkedCt);

            if (linkedCt.IsCancellationRequested) return;

            IsPdbAvailable = seqPoints.Count > 0 || locals.Count > 0;

            if (!IsPdbAvailable)
            {
                StatusMessage = "No portable PDB found next to the assembly.";
                return;
            }

            // Populate sequence point list.
            SequencePoints.Clear();
            foreach (var sp in seqPoints)
                SequencePoints.Add(new SequencePointViewModel(sp));

            // First non-hidden sequence point gives us the primary source location.
            var firstSeqPt = seqPoints.FirstOrDefault(sp => !sp.IsHidden && sp.DocumentPath is not null);
            SourceFilePath  = firstSeqPt?.DocumentPath;
            SourceStartLine = firstSeqPt?.StartLine ?? 0;

            // SourceLink URL resolution.
            if (sourceLink is not null && SourceFilePath is not null)
            {
                var resolver       = new SourceLinkResolver(sourceLink);
                SourceLinkUrl      = resolver.ResolveUrlWithLine(SourceFilePath, SourceStartLine);
                IsSourceLinkAvailable = SourceLinkUrl is not null;
            }

            // Local source preview (3 lines around first sequence point).
            if (SourceFilePath is not null && File.Exists(SourceFilePath) && SourceStartLine > 0)
            {
                SourcePreview = await Task.Run(
                    () => BuildPreview(SourceFilePath, SourceStartLine), linkedCt);
            }

            StatusMessage = SourceFilePath is not null
                ? $"{Path.GetFileName(SourceFilePath)}  line {SourceStartLine}"
                : "Source file path not available in PDB.";
        }
        catch (OperationCanceledException) { /* silent */ }
        catch (Exception ex)
        {
            StatusMessage = $"// PDB read failed: {ex.Message}";
        }
        finally
        {
            if (first) IsLoading = false;
        }
    }

    /// <summary>Clears all state and resets to the initial placeholder message.</summary>
    public void Clear()
    {
        _cts?.Cancel();
        Reset();
        StatusMessage = "Select a method node to view source information.";
        IsLoading     = false;
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private void Reset()
    {
        SequencePoints.Clear();
        IsPdbAvailable        = false;
        IsSourceLinkAvailable = false;
        SourceFilePath        = null;
        SourceStartLine       = 0;
        SourcePreview         = null;
        SourceLinkUrl         = null;
    }

    private static (
        IReadOnlyList<SequencePoint> seqPoints,
        IReadOnlyList<LocalVariableInfo> locals,
        SourceLinkMap? sourceLink,
        string? filePath,
        int startLine) ReadPdbData(int methodToken, string assemblyFilePath)
    {
        if (!PdbReader.TryOpen(assemblyFilePath, out var reader) || reader is null)
            return ([], [], null, null, 0);

        using (reader)
        {
            var seqPoints  = reader.GetSequencePoints(methodToken);
            var locals     = reader.GetLocalVariables(methodToken);
            var sourceLink = reader.GetSourceLinkMap();

            var first   = seqPoints.FirstOrDefault(sp => !sp.IsHidden && sp.DocumentPath is not null);
            return (seqPoints, locals, sourceLink, first?.DocumentPath, first?.StartLine ?? 0);
        }
    }

    private static string BuildPreview(string filePath, int startLine)
    {
        try
        {
            var lines   = File.ReadAllLines(filePath);
            var from    = Math.Max(0, startLine - 2);        // 1 line before
            var to      = Math.Min(lines.Length - 1, startLine + 1); // 1 line after
            return string.Join(Environment.NewLine,
                Enumerable.Range(from, to - from + 1)
                           .Select(i => $"{i + 1,4}: {lines[i]}"));
        }
        catch
        {
            return string.Empty;
        }
    }
}
