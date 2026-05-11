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
        try
        {
            var result = await Applier!(filePath, edit, CancellationToken.None).ConfigureAwait(true);
            if (!result.Success)
            {
                ErrorReporter?.Invoke($"Round-trip skipped: {result.ErrorMessage}");
                return;
            }

            // Build a paired source-backup undo entry so an undo at the diagram
            // level also rolls back the source file. The "WriteSource" delegate
            // is invoked on undo/redo — it must push watcher suppression to the
            // live-sync service via the same Applier-aware path, which we
            // approximate here by re-writing the file directly (the Applier
            // itself handles SuppressNextChange during forward edits; for
            // undo/redo we issue raw writes — the user can detect a brief
            // re-analyze if the watcher catches them).
            var sourceBackup = new SourceFileBackupUndoEntry(
                FilePath:       result.FilePath,
                ContentBefore:  result.ContentBefore,
                ContentAfter:   result.ContentAfter,
                WriteSource:    (path, content) =>
                {
                    try { File.WriteAllText(path, content); }
                    catch (Exception ex) { ErrorReporter?.Invoke($"Undo source write failed: {ex.Message}"); }
                },
                Inner:          NoOpEntry.Instance,
                Description:    description);

            undoManager.Push(sourceBackup);
            onSuccess?.Invoke();
        }
        catch (Exception ex)
        {
            ErrorReporter?.Invoke($"Round-trip error: {ex.Message}");
        }
    }

    /// <summary>
    /// Stand-in for the "inner" diagram-side undo when the diagram mutation
    /// has its own snapshot entry pushed separately by the caller. The
    /// <see cref="SourceFileBackupUndoEntry"/> contract requires a non-null
    /// inner entry; this no-op satisfies that without double-pushing the
    /// caller's snapshot.
    /// </summary>
    private sealed class NoOpEntry : IClassDiagramUndoEntry
    {
        public static readonly NoOpEntry Instance = new();
        public string Description => "(source-file backup)";
        public DateTime Timestamp { get; } = DateTime.UtcNow;
        public void Undo() { }
        public void Redo() { }
    }
}
