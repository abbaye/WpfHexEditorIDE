// ==========================================================
// Project: WpfHexEditor.Editor.ClassDiagram.Core
// File: RoundTrip/Abstractions/ILanguageRoundTripEditor.cs
// Author: Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Opus 4.7
// Created: 2026-05-10
// Description:
//     Contract every language-specific round-trip editor must implement.
//     Parallels ILanguageGenerator (ADR-014) on the inbound direction:
//     ILanguageGenerator turns the diagram into source; ILanguageRound-
//     TripEditor turns a single MemberEdit into a patched source string.
//
// Architecture Notes:
//     Pure-function contract: (sourceText, edit) -> RoundTripResult.
//     No I/O. The caller (DiagramCodeEditService facade) handles file
//     reads, watcher suppression, undo snapshot, and writes.
//     Implementations must be thread-safe (no mutable instance state).
// ==========================================================

namespace WpfHexEditor.Editor.ClassDiagram.Core.RoundTrip.Abstractions;

/// <summary>
/// Contract for a language-specific round-trip editor that applies a
/// <see cref="MemberEdit"/> to a source-file text and returns the patched
/// content. Symmetric to <c>ILanguageGenerator</c>.
/// </summary>
public interface ILanguageRoundTripEditor
{
    /// <summary>Stable identifier of the source language (e.g. <c>csharp</c>, <c>vb</c>).</summary>
    string LanguageId { get; }

    /// <summary>Display name (e.g. "C#", "Visual Basic").</summary>
    string DisplayName { get; }

    /// <summary>File extensions this editor recognises (e.g. <c>.cs</c> or <c>.vb</c>).</summary>
    IReadOnlyList<string> FileExtensions { get; }

    /// <summary>
    /// Applies <paramref name="edit"/> to <paramref name="sourceText"/>.
    /// Returns a <see cref="RoundTripResult"/> describing the outcome.
    /// </summary>
    /// <param name="filePath">Absolute path used for diagnostic purposes only — implementations must NOT touch disk.</param>
    /// <param name="sourceText">Current text of the file.</param>
    /// <param name="edit">The edit to apply.</param>
    /// <param name="ct">Cancellation token.</param>
    Task<RoundTripResult> ApplyAsync(
        string             filePath,
        string             sourceText,
        MemberEdit         edit,
        CancellationToken  ct = default);
}
