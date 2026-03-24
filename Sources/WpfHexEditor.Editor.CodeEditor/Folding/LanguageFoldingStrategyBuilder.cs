// ==========================================================
// Project: WpfHexEditor.Editor.CodeEditor
// File: LanguageFoldingStrategyBuilder.cs
// Author: WpfHexEditor Team
// Contributors: Claude Sonnet 4.6
// Created: 2026-03-22
// Description:
//     Factory that builds an IFoldingStrategy from a language's FoldingRules.
//     Returns null when no rules are declared so the CodeEditor can fall back
//     to its built-in default strategy.
//
// Architecture Notes:
//     Factory Pattern — data-driven strategy construction.
//     Keeps CodeEditor and FoldingEngine decoupled from ProjectSystem types.
// ==========================================================

using System.Collections.Generic;
using WpfHexEditor.Core.ProjectSystem.Languages;

namespace WpfHexEditor.Editor.CodeEditor.Folding;

/// <summary>
/// Converts a <see cref="FoldingRules"/> descriptor into a concrete
/// <see cref="IFoldingStrategy"/> (or composite of strategies).
/// </summary>
internal static class LanguageFoldingStrategyBuilder
{
    /// <summary>
    /// Builds an <see cref="IFoldingStrategy"/> from a language definition's
    /// <see cref="FoldingRules"/>.
    /// Returns <see langword="null"/> when <paramref name="rules"/> is null —
    /// the caller should retain its built-in default strategy.
    /// </summary>
    public static IFoldingStrategy? Build(FoldingRules? rules, string? lineCommentPrefix = null)
    {
        if (rules is null) return null;

        var strategies = new List<IFoldingStrategy>();

        if (rules.IndentBased)
            strategies.Add(new IndentFoldingStrategy(rules.IndentTabWidth, rules.BlockStartPattern));

        if (rules.TagBased)
            strategies.Add(new TagFoldingStrategy(rules.SelfClosingTags, rules.MultilineTagSupport));

        if (rules.HeadingBased)
            strategies.Add(new HeadingFoldingStrategy(rules.MinHeadingLevel));

        if (rules.StartPatterns.Count > 0)
            strategies.Add(new PatternFoldingStrategy(rules.StartPatterns, rules.EndPatterns, lineCommentPrefix));

        if (rules.NamedRegionStartPattern is not null)
            strategies.Add(new NamedRegionFoldingStrategy(
                rules.NamedRegionStartPattern,
                rules.NamedRegionEndPattern ?? string.Empty));

        return strategies.Count switch
        {
            0 => null,
            1 => strategies[0],
            _ => new CompositeFoldingStrategy([.. strategies])
        };
    }
}
