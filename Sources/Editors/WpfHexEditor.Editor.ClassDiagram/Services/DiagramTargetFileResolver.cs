// ==========================================================
// Project: WpfHexEditor.Editor.ClassDiagram
// File: Services/DiagramTargetFileResolver.cs
// Author: Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Opus 4.7
// Created: 2026-05-11
// Description:
//     Phase 1B-7 — Infers a default source-file target for AddType
//     operations triggered from the canvas-background context menu or
//     the Ctrl+C/I/E/S keyboard shortcuts. Used by ClassDiagramSplitHost
//     and DiagramCanvas to give new nodes a SourceFilePath before
//     handing them off to RoundTripScope.
//
// Architecture Notes:
//     Pure inference — no dialog, no I/O. Returns null when no
//     reasonable target can be derived; the caller falls back to
//     in-memory-only behavior (current pre-Phase-1B-7 behavior).
//     Strategy order (first match wins):
//       1) Selected node's SourceFilePath (caller hint)
//       2) Single distinct SourceFilePath across all classes in the document
//       3) Most populated namespace's most populated file
//       4) Most populated file overall
// ==========================================================

using System.IO;
using WpfHexEditor.Editor.ClassDiagram.Core.Model;

namespace WpfHexEditor.Editor.ClassDiagram.Services;

/// <summary>
/// Stateless helper that picks a sensible target source file for a new
/// type declaration emitted from the canvas-background "Add Class" /
/// "Add Interface" / "Add Enum" / "Add Struct" actions.
/// </summary>
public static class DiagramTargetFileResolver
{
    /// <summary>
    /// Returns the path of the file to receive a new type declaration, or
    /// <c>null</c> when no reasonable target can be inferred.
    /// </summary>
    /// <param name="doc">The current diagram document.</param>
    /// <param name="selectedNodeHint">
    /// Optional currently-selected node. When it carries a SourceFilePath
    /// that exists on disk, the result is that path (locality wins over
    /// document-wide heuristics).
    /// </param>
    public static string? Resolve(DiagramDocument? doc, ClassNode? selectedNodeHint = null)
    {
        // Strategy 1 — selected node wins.
        if (TryReadablePath(selectedNodeHint?.SourceFilePath) is { } sel) return sel;

        if (doc is null || doc.Classes.Count == 0) return null;

        var paths = doc.Classes
            .Select(c => c.SourceFilePath)
            .Where(p => !string.IsNullOrEmpty(p))
            .Where(p => File.Exists(p!))
            .ToList();
        if (paths.Count == 0) return null;

        // Strategy 2 — single distinct path across the whole document.
        var distinct = paths.Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        if (distinct.Count == 1) return distinct[0];

        // Strategy 3 — densest namespace's densest file.
        var byNs = doc.Classes
            .Where(c => !string.IsNullOrEmpty(c.SourceFilePath) && File.Exists(c.SourceFilePath!))
            .GroupBy(c => c.Namespace ?? string.Empty, StringComparer.Ordinal)
            .OrderByDescending(g => g.Count())
            .ToList();
        if (byNs.Count > 0)
        {
            var topNsFile = byNs[0]
                .GroupBy(c => c.SourceFilePath!, StringComparer.OrdinalIgnoreCase)
                .OrderByDescending(g => g.Count())
                .First()
                .Key;
            return topNsFile;
        }

        // Strategy 4 — densest file overall.
        return paths
            .GroupBy(p => p!, StringComparer.OrdinalIgnoreCase)
            .OrderByDescending(g => g.Count())
            .First()
            .Key;
    }

    private static string? TryReadablePath(string? path) =>
        !string.IsNullOrEmpty(path) && File.Exists(path) ? path : null;
}
