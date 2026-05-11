// ==========================================================
// Project: WpfHexEditor.Editor.ClassDiagram.Core
// File: Import/DiagramImporterRegistry.cs
// Author: Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Opus 4.7
// Created: 2026-05-10
// Description:
//     Phase 4 — process-wide registry mapping file extensions and
//     content sniffs to IDiagramImporter implementations. Parallels
//     RoundTripEditorRegistry (Phase 1B-1) and CodeGenLanguageRegistry
//     (ADR-014) in shape and intent.
// ==========================================================

using System.Collections.Concurrent;

namespace WpfHexEditor.Editor.ClassDiagram.Core.Import;

/// <summary>
/// Process-wide registry of <see cref="IDiagramImporter"/> implementations.
/// </summary>
public static class DiagramImporterRegistry
{
    private static readonly ConcurrentDictionary<string, IDiagramImporter> _byId =
        new(StringComparer.OrdinalIgnoreCase);

    private static readonly ConcurrentDictionary<string, IDiagramImporter> _byExt =
        new(StringComparer.OrdinalIgnoreCase);

    /// <summary>Registers <paramref name="importer"/> under its id and every declared extension.</summary>
    public static void Register(IDiagramImporter importer)
    {
        ArgumentNullException.ThrowIfNull(importer);
        _byId[importer.FormatId] = importer;
        foreach (var ext in importer.FileExtensions)
            _byExt[ext] = importer;
    }

    /// <summary>Returns the importer for the given format id, or null.</summary>
    public static IDiagramImporter? TryGetById(string formatId) =>
        _byId.TryGetValue(formatId, out var i) ? i : null;

    /// <summary>Returns the importer whose declared extensions match <paramref name="filePath"/>, or null.</summary>
    public static IDiagramImporter? TryGetByFilePath(string filePath)
    {
        string ext = System.IO.Path.GetExtension(filePath);
        return string.IsNullOrEmpty(ext)
            ? null
            : _byExt.TryGetValue(ext, out var i) ? i : null;
    }

    /// <summary>
    /// Returns the first registered importer whose <c>CanHandle</c> returns
    /// true for <paramref name="content"/>, or null when none recognises it.
    /// </summary>
    public static IDiagramImporter? TryDetectByContent(string content)
    {
        foreach (var imp in _byId.Values)
            if (imp.CanHandle(content)) return imp;
        return null;
    }

    /// <summary>Snapshot of all registered importers (stable order by FormatId).</summary>
    public static IReadOnlyList<IDiagramImporter> All() =>
        _byId.Values.OrderBy(i => i.FormatId, StringComparer.Ordinal).ToArray();

    /// <summary>Removes every registration. Intended for tests only.</summary>
    public static void ResetForTests()
    {
        _byId.Clear();
        _byExt.Clear();
    }
}
