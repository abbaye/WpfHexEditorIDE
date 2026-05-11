// ==========================================================
// Project: WpfHexEditor.Editor.ClassDiagram.Core
// File: RoundTrip/Abstractions/RoundTripResult.cs
// Author: Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Opus 4.7
// Created: 2026-05-10
// Description:
//     Return type of ILanguageRoundTripEditor.ApplyAsync. Carries the
//     before/after source text plus a success flag and optional error
//     diagnostic. Caller is responsible for I/O (write + watcher
//     suppression + undo backup).
//
// Architecture Notes:
//     Immutable record. Producers should never write to disk themselves —
//     by returning the result, they let the calling facade decide on
//     preview, confirmation, and persistence.
// ==========================================================

namespace WpfHexEditor.Editor.ClassDiagram.Core.RoundTrip.Abstractions;

/// <summary>
/// Outcome of applying a <see cref="MemberEdit"/> to a single source file.
/// </summary>
/// <param name="Success">True when the edit was applied; false on parse failure / target not found.</param>
/// <param name="FilePath">Absolute path of the file the editor targeted.</param>
/// <param name="ContentBefore">Original file content (snapshot taken before patching). Used by undo.</param>
/// <param name="ContentAfter">Post-patch content. Caller writes this to disk after preview/confirmation.</param>
/// <param name="ErrorMessage">Human-readable reason when <paramref name="Success"/> is false.</param>
public sealed record RoundTripResult(
    bool    Success,
    string  FilePath,
    string  ContentBefore,
    string  ContentAfter,
    string? ErrorMessage = null)
{
    /// <summary>Convenience factory for failure paths.</summary>
    public static RoundTripResult Fail(string filePath, string error) =>
        new(false, filePath, ContentBefore: string.Empty, ContentAfter: string.Empty, error);
}
