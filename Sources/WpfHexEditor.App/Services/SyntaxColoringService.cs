// ==========================================================
// Project: WpfHexEditor.App
// File: Services/SyntaxColoringService.cs
// Author: Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Opus 4.6
// Created: 2026-04-02
// Description:
//     Centralised ISyntaxColoringService implementation.
//     Resolves languages via LanguageRegistry, builds highlighters from
//     whfmt-driven SyntaxRules, and maps SyntaxTokenKind to CE_* theme brushes.
//
// Architecture Notes:
//     Lives in App layer because it depends on CodeEditor (SyntaxRuleHighlighter)
//     and Core.ProjectSystem (LanguageRegistry). Injected into IDEHostContext.
//     Thread-safe: each Colorize call creates a fresh highlighter instance
//     (required for correct block-comment state tracking).
// ==========================================================

using System.Windows;
using System.Windows.Media;
using WpfHexEditor.Core.ProjectSystem.Languages;
using WpfHexEditor.Editor.CodeEditor.Helpers;
using WpfHexEditor.SDK.Contracts.Services;

namespace WpfHexEditor.App.Services;

/// <summary>
/// Centralised syntax coloring — single source of truth for language resolution,
/// highlighter construction, and token-kind-to-brush mapping.
/// </summary>
internal sealed class SyntaxColoringService : ISyntaxColoringService
{
    // ── ISyntaxColoringService ──────────────────────────────────────────

    public IReadOnlyList<ColoredSpan> ColorizeLine(string line, string languageId)
    {
        var highlighter = BuildHighlighterForLanguage(languageId);
        if (highlighter is null) return [];

        return Convert(highlighter.Highlight(line, 0));
    }

    public IReadOnlyList<IReadOnlyList<ColoredSpan>> ColorizeLines(IReadOnlyList<string> lines, string languageId)
    {
        var highlighter = BuildHighlighterForLanguage(languageId);
        if (highlighter is null) return [];

        highlighter.Reset();
        var result = new List<IReadOnlyList<ColoredSpan>>(lines.Count);
        for (int i = 0; i < lines.Count; i++)
            result.Add(Convert(highlighter.Highlight(lines[i], i)));

        return result;
    }

    public Brush GetTokenBrush(SyntaxTokenKind kind) => TokenKindToBrush(kind);

    public string? ResolveLanguageId(string aliasOrExtension)
    {
        if (string.IsNullOrEmpty(aliasOrExtension)) return null;

        // 1. Try whfmt-driven alias lookup (replaces the old static dictionary).
        var byAlias = LanguageRegistry.Instance.FindByAlias(aliasOrExtension);
        if (byAlias is not null) return byAlias.Id;

        // 2. Try direct ID match.
        var id = aliasOrExtension.ToLowerInvariant();
        if (LanguageRegistry.Instance.FindById(id) is not null) return id;

        // 3. Try as file extension.
        if (aliasOrExtension.StartsWith('.'))
        {
            var lang = LanguageRegistry.Instance.GetLanguageForFile("file" + aliasOrExtension);
            if (lang is not null) return lang.Id;
        }

        return null;
    }

    // ── Internals ───────────────────────────────────────────────────────

    private ISyntaxHighlighter? BuildHighlighterForLanguage(string languageId)
    {
        var id = ResolveLanguageId(languageId) ?? languageId.ToLowerInvariant();

        var langDef = LanguageRegistry.Instance.FindById(id);
        if (langDef is null || langDef.SyntaxRules.Count == 0) return null;

        var rules = langDef.SyntaxRules
            .Select(rule =>
            {
                try
                {
                    return new RegexHighlightRule(
                        rule.Pattern,
                        TokenKindToBrush(rule.Kind),
                        isBold:   rule.Kind is SyntaxTokenKind.Keyword,
                        isItalic: rule.Kind is SyntaxTokenKind.Comment,
                        kind:     rule.Kind);
                }
                catch (System.Text.RegularExpressions.RegexParseException)
                {
                    // Skip rules with invalid patterns — avoids crashing the preview
                    return null;
                }
            })
            .Where(r => r is not null)
            .Cast<RegexHighlightRule>();

        return new SyntaxRuleHighlighter(
            rules,
            langDef.Name,
            langDef.BlockCommentStart,
            langDef.BlockCommentEnd);
    }

    private static IReadOnlyList<ColoredSpan> Convert(IReadOnlyList<SyntaxHighlightToken> tokens)
    {
        if (tokens.Count == 0) return [];

        var result = new ColoredSpan[tokens.Count];
        for (int i = 0; i < tokens.Count; i++)
        {
            var t = tokens[i];
            result[i] = new ColoredSpan(t.StartColumn, t.Length, t.Text, t.Foreground, t.IsBold, t.IsItalic, t.Kind);
        }
        return result;
    }

    // ── Token kind → brush (centralised, single source of truth) ────────

    private static Brush TokenKindToBrush(SyntaxTokenKind kind)
    {
        var resourceKey = kind switch
        {
            SyntaxTokenKind.Keyword     => "CE_Keyword",
            SyntaxTokenKind.String      => "CE_String",
            SyntaxTokenKind.Number      => "CE_Number",
            SyntaxTokenKind.Comment     => "CE_Comment",
            SyntaxTokenKind.Type        => "CE_Type",
            SyntaxTokenKind.Identifier  => "CE_Identifier",
            SyntaxTokenKind.Operator    => "CE_Operator",
            SyntaxTokenKind.Bracket     => "CE_Bracket",
            SyntaxTokenKind.Attribute   => "CE_Attribute",
            SyntaxTokenKind.ControlFlow => "CE_ControlFlow",
            _                           => "CE_Foreground"
        };

        if (Application.Current?.TryFindResource(resourceKey) is Brush themeBrush)
            return themeBrush;

        return FallbackBrush(kind);
    }

    private static Brush FallbackBrush(SyntaxTokenKind kind)
    {
        var brush = new SolidColorBrush(kind switch
        {
            SyntaxTokenKind.Keyword     => Color.FromRgb(86,  156, 214),  // #569CD6
            SyntaxTokenKind.String      => Color.FromRgb(206, 145, 120),  // #CE9178
            SyntaxTokenKind.Number      => Color.FromRgb(181, 206, 168),  // #B5CEA8
            SyntaxTokenKind.Comment     => Color.FromRgb(106, 153, 85),   // #6A9955
            SyntaxTokenKind.Type        => Color.FromRgb(78,  201, 176),  // #4EC9B0
            SyntaxTokenKind.Identifier  => Color.FromRgb(220, 220, 170),  // #DCDCAA
            SyntaxTokenKind.Operator    => Color.FromRgb(212, 212, 212),  // #D4D4D4
            SyntaxTokenKind.Bracket     => Color.FromRgb(255, 215, 0),    // #FFD700
            SyntaxTokenKind.Attribute   => Color.FromRgb(156, 220, 254),  // #9CDCFE
            SyntaxTokenKind.ControlFlow => Color.FromRgb(197, 134, 192),  // #C586C0
            _                           => Color.FromRgb(212, 212, 212)   // #D4D4D4
        });
        brush.Freeze();
        return brush;
    }
}
