// ==========================================================
// Project: WpfHexEditor.Editor.CodeEditor
// File: IFoldingStrategy.cs
// Author: Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.6
// Created: 2026-03-05
// Description:
//     Strategy interface for computing foldable regions from a list of lines.
//     Implement this to add language-specific folding (brace, indent, XML, …).
//
// Architecture Notes:
//     Strategy Pattern — FoldingEngine holds an IFoldingStrategy and delegates
//     region analysis to it, remaining agnostic of the language syntax.
// ==========================================================

using System.Collections.Generic;
using WpfHexEditor.Editor.CodeEditor.Models;

namespace WpfHexEditor.Editor.CodeEditor.Folding;

/// <summary>
/// Analyses document lines and returns the set of <see cref="FoldingRegion"/> objects
/// that the editor should display fold markers for.
/// </summary>
public interface IFoldingStrategy
{
    /// <summary>
    /// Computes folding regions for the given document lines.
    /// </summary>
    /// <param name="lines">All lines of the current document (snapshot).</param>
    /// <returns>
    /// Unordered list of non-empty regions (each spans at least 2 lines).
    /// The caller is responsible for merging these with the previous state
    /// to preserve <see cref="FoldingRegion.IsCollapsed"/> flags.
    /// </returns>
    IReadOnlyList<FoldingRegion> Analyze(IReadOnlyList<CodeLine> lines);
}
