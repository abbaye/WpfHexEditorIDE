// ==========================================================
// Project: WpfHexEditor.Editor.ClassDiagram
// File: Services/RoundTripScope.cs
// Author: Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Opus 4.7
// Created: 2026-05-11
// Description:
//     Phase 1B-6 — Centralised gateway for diagram-side mutations that
//     also need to patch their backing .cs/.vb source file. Wraps the
//     existing DiagramCodeEditService.ApplyEditAsync with the host-side
//     concerns: graceful no-op when the node has no SourceFilePath,
//     fire-and-forget for non-blocking UI, and chained undo so an undo
//     restores both the diagram and the source.
//
// Architecture Notes:
//     Stateless facade. The actual round-trip pipeline (Roslyn syntax
//     transforms, watcher suppression, file I/O) lives in the Plugins
//     assembly so this scope discovers it through a callback the
//     SplitHost wires at startup. That keeps the editor assembly free
//     of Roslyn/IO concerns.
// ==========================================================

using System.Collections.Concurrent;
using System.IO;
using WpfHexEditor.Editor.ClassDiagram.Core.Model;
using WpfHexEditor.Editor.ClassDiagram.Core.RoundTrip.Abstractions;

namespace WpfHexEditor.Editor.ClassDiagram.Services;

/// <summary>
/// Callback executed by <see cref="RoundTripScope"/> to apply a
/// <see cref="MemberEdit"/> against <paramref name="filePath"/>. Returns
/// the <see cref="RoundTripResult"/> reporting success and before/after
/// content for undo.
/// </summary>
public delegate Task<RoundTripResult> RoundTripApplyDelegate(
    string             filePath,
    MemberEdit         edit,
    CancellationToken  ct);

/// <summary>
/// Optional error-reporting callback. SplitHost wires this to the IDE
/// output logger or a status-bar toast in standalone mode.
/// </summary>
public delegate void RoundTripErrorReporter(string message);

/// <summary>
/// Diagram-side gateway to <see cref="DiagramCodeEditService.ApplyEditAsync"/>.
/// </summary>
public static class RoundTripScope
{
    /// <summary>
    /// Process-wide hook installed by <c>ClassDiagramPlugin.InitializeAsync</c>
    /// once <c>DiagramCodeEditService</c> is available. Null until then; calls
    /// short-circuit to a no-op so unit tests and editor-only scenarios run
    /// without a Roslyn workspace.
    /// </summary>
    public static RoundTripApplyDelegate? Applier { get; set; }

    /// <summary>Optional error sink (set once by the host).</summary>
    public static RoundTripErrorReporter? ErrorReporter { get; set; }

    // Per-path serialization: two rapid mutations targeting the same file
    // (paste-many-members, undo-spam) would race the read-modify-write cycle
    // of Applier and corrupt the file. A keyed SemaphoreSlim queues them.
    private static readonly ConcurrentDictionary<string, SemaphoreSlim> _fileLocks =
        new(StringComparer.OrdinalIgnoreCase);

    private static SemaphoreSlim GetLock(string filePath) =>
        _fileLocks.GetOrAdd(filePath, _ => new SemaphoreSlim(1, 1));

    /// <summary>
    /// Clears the installed Applier, ErrorReporter, and per-file locks.
    /// Intended for tests that need a clean slate; matches the same-named
    /// helper on RoundTripEditorRegistry and DiagramImporterRegistry.
    /// </summary>
    public static void ResetForTests()
    {
        Applier       = null;
        ErrorReporter = null;
        foreach (var s in _fileLocks.Values) s.Dispose();
        _fileLocks.Clear();
    }

    /// <summary>
    /// Determines whether a node carries source-file information so a
    /// round-trip edit can target it. False means the node is in-memory
    /// only (DSL-authored or freshly created without source).
    /// </summary>
    public static bool HasSource(ClassNode? node) =>
        !string.IsNullOrEmpty(node?.SourceFilePath) && File.Exists(node!.SourceFilePath!);

    /// <summary>
    /// Same as <see cref="HasSource(ClassNode?)"/> for a member's effective path
    /// (member-specific path wins; otherwise the owning node's path is used).
    /// </summary>
    public static bool HasSource(ClassNode owner, ClassMember member)
    {
        string? path = member.SourceFilePath ?? owner.SourceFilePath;
        return !string.IsNullOrEmpty(path) && File.Exists(path);
    }

    /// <summary>
    /// Fire-and-forget round-trip for a single edit against the file owning
    /// <paramref name="node"/>. Returns true when the edit was queued (the
    /// node carries a source path and an applier is installed). Returns false
    /// when the call is a no-op (in-memory only scenario) so the caller can
    /// skip building chained-undo entries that would never trigger anything.
    /// </summary>
    public static bool TryApply(
        ClassNode               node,
        MemberEdit              edit,
        ClassDiagramUndoManager undoManager,
        string                  description,
        Action?                 onSuccess = null)
    {
        if (Applier is null) return false;
        if (!HasSource(node))  return false;

        string filePath = node.SourceFilePath!;
        _ = RunAsync(filePath, edit, undoManager, description, onSuccess);
        return true;
    }

    /// <summary>
    /// Member-targeted variant — picks the member's own SourceFilePath when set,
    /// otherwise falls back to the owning node's path.
    /// </summary>
    public static bool TryApply(
        ClassNode               owner,
        ClassMember             member,
        MemberEdit              edit,
        ClassDiagramUndoManager undoManager,
        string                  description,
        Action?                 onSuccess = null)
    {
        if (Applier is null) return false;
        string? path = member.SourceFilePath ?? owner.SourceFilePath;
        if (string.IsNullOrEmpty(path) || !File.Exists(path)) return false;

        _ = RunAsync(path, edit, undoManager, description, onSuccess);
        return true;
    }

    // ── Internal runner ──────────────────────────────────────────────────────

    private static async Task RunAsync(
        string                  filePath,
        MemberEdit              edit,
        ClassDiagramUndoManager undoManager,
        string                  description,
        Action?                 onSuccess)
    {
        var gate = GetLock(filePath);
        await gate.WaitAsync().ConfigureAwait(true);
        try
        {
            var result = await Applier!(filePath, edit, CancellationToken.None).ConfigureAwait(true);
            if (!result.Success)
            {
                ErrorReporter?.Invoke($"Round-trip skipped: {result.ErrorMessage}");
                return;
            }

            // Caller has already pushed its diagram-side snapshot entry; here
            // we add a chained entry that only restores the source bytes on
            // undo/redo. Watcher suppression for the forward write was handled
            // inside Applier — on undo/redo we issue raw writes and accept a
            // brief re-analyze if the live-sync watcher catches them.
            undoManager.Push(new SourceFileBackupUndoEntry(
                FilePath:       result.FilePath,
                ContentBefore:  result.ContentBefore,
                ContentAfter:   result.ContentAfter,
                WriteSource:    (path, content) =>
                {
                    try { File.WriteAllText(path, content); }
                    catch (Exception ex) { ErrorReporter?.Invoke($"Undo source write failed: {ex.Message}"); }
                },
                Inner:          null,
                Description:    description));

            onSuccess?.Invoke();
        }
        catch (Exception ex)
        {
            ErrorReporter?.Invoke($"Round-trip error: {ex.Message}");
        }
        finally
        {
            gate.Release();
        }
    }
}
