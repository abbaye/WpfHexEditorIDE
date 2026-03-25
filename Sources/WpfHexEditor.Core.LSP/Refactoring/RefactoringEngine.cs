// ==========================================================
// Project: WpfHexEditor.Core.LSP
// File: Refactoring/RefactoringEngine.cs
// Author: Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.6
// Created: 2026-03-16
// Description:
//     Aggregates all registered IRefactoring implementations and
//     exposes them for use by CommandIntegration and IDE menus.
//
// Architecture Notes:
//     Pattern: Registry / Strategy collection
//     Consumers obtain available refactorings for a context via
//     GetAvailable(RefactoringContext) and execute the chosen one.
// ==========================================================

namespace WpfHexEditor.Core.LSP.Refactoring;

/// <summary>
/// Central registry for <see cref="IRefactoring"/> implementations.
/// Provides contextual filtering so only applicable refactorings are surfaced.
/// </summary>
public sealed class RefactoringEngine
{
    private readonly List<IRefactoring> _refactorings;

    public RefactoringEngine(IEnumerable<IRefactoring>? refactorings = null)
        => _refactorings = refactorings?.ToList() ?? [];

    /// <summary>Registers an additional refactoring at runtime.</summary>
    public void Register(IRefactoring refactoring)
        => _refactorings.Add(refactoring);

    /// <summary>
    /// Returns all refactorings that can be applied in the given context.
    /// </summary>
    public IReadOnlyList<IRefactoring> GetAvailable(RefactoringContext context)
        => _refactorings.Where(r => r.CanApply(context)).ToList();
}
