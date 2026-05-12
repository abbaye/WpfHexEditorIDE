// ==========================================================
// Project: WpfHexEditor.Editor.ClassDiagram.Core
// File: Import/IDiagramImporter.cs
// Author: Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Opus 4.7
// Created: 2026-05-10
// Description:
//     Phase 4 — symmetric counterpart to the existing exporters
//     (MermaidExporter, PlantUmlExporter, etc.). Each importer parses
//     a textual DSL into a DiagramDocument. The registry pattern
//     lets the host pick an importer by file path or by CanHandle().
//
// Architecture Notes:
//     Pure function (text → DiagramDocument), no I/O.
//     Implementations live in WpfHexEditor.Editor.ClassDiagram.Core
//     so they can be unit-tested without WPF. They will be exposed
//     publicly in Phase 7E via Wht.SDK.Diagrams.
// ==========================================================

using WpfHexEditor.Editor.ClassDiagram.Core.Model;

namespace WpfHexEditor.Editor.ClassDiagram.Core.Import;

/// <summary>
/// Contract for a textual-DSL → DiagramDocument importer.
/// </summary>
public interface IDiagramImporter
{
    /// <summary>Stable identifier (e.g. <c>mermaid</c>, <c>plantuml</c>).</summary>
    string FormatId { get; }

    /// <summary>Display name shown in dialogs.</summary>
    string DisplayName { get; }

    /// <summary>File extensions this importer recognises (e.g. <c>.mmd</c>, <c>.puml</c>).</summary>
    IReadOnlyList<string> FileExtensions { get; }

    /// <summary>
    /// Quick content sniff. Returns true when the content looks like the
    /// supported format (e.g. starts with <c>@startuml</c> or <c>classDiagram</c>).
    /// </summary>
    bool CanHandle(string content);

    /// <summary>
    /// Parses <paramref name="content"/> into a <see cref="DiagramDocument"/>.
    /// Throws <see cref="ImportException"/> on unrecoverable parse failure;
    /// returns a partial document with warnings for soft errors.
    /// </summary>
    ImportResult Import(string content);
}

/// <summary>Outcome of an import operation.</summary>
public sealed record ImportResult(
    DiagramDocument        Document,
    IReadOnlyList<string>  Warnings)
{
    public static ImportResult Ok(DiagramDocument doc) => new(doc, []);
}

/// <summary>Thrown when the importer cannot recover from a parse failure.</summary>
public sealed class ImportException(string message) : Exception(message);
