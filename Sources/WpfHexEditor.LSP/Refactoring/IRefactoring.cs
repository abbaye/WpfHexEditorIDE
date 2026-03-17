// ==========================================================
// Project: WpfHexEditor.LSP
// File: Refactoring/IRefactoring.cs
// Author: Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.6
// Created: 2026-03-16
// Description:
//     Base contract for all refactoring operations.
//     A refactoring computes a set of TextEdit operations and applies them
//     to the document(s) via the DocumentHostService.
// ==========================================================

namespace WpfHexEditor.LSP.Refactoring;

/// <summary>A single text replacement in a document.</summary>
/// <param name="FilePath">Absolute path of the file to edit.</param>
/// <param name="StartOffset">Zero-based character offset of the replacement start.</param>
/// <param name="Length">Number of characters to replace (0 = insert).</param>
/// <param name="NewText">Replacement text.</param>
public sealed record TextEdit(
    string FilePath,
    int    StartOffset,
    int    Length,
    string NewText);

/// <summary>
/// Contract for a refactoring operation.
/// Implementations are registered in <see cref="RefactoringContext"/> by the LSP layer.
/// </summary>
public interface IRefactoring
{
    /// <summary>Human-readable name shown in the refactoring menu.</summary>
    string Name { get; }

    /// <summary>Returns <c>true</c> when this refactoring can be applied in the given context.</summary>
    bool CanApply(RefactoringContext context);

    /// <summary>
    /// Computes the text edits required to apply this refactoring.
    /// Must not modify documents directly — return edits only.
    /// </summary>
    IReadOnlyList<TextEdit> Apply(RefactoringContext context);
}
