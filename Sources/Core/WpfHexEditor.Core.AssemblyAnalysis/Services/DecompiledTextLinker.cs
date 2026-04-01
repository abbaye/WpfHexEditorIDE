// ==========================================================
// Project: WpfHexEditor.Core.AssemblyAnalysis
// File: Services/DecompiledTextLinker.cs
// Author: Derek Tremblay
// Created: 2026-03-16
// License: GNU Affero General Public License v3.0 (AGPL-3.0)
// Description:
//     Scans decompiled C# text for identifiers that look like type names
//     (PascalCase, not a C# keyword) and returns their character spans.
//     Consumed by the plugin layer to build clickable TextLink ranges
//     for goto-definition navigation in the code editor.
//
// Architecture Notes:
//     Pattern: Service (stateless, pure function).
//     No WPF / no NuGet — safe from the Core layer.
//     Uses BCL System.Text.RegularExpressions; no external deps.
// ==========================================================

using System.Text.RegularExpressions;
using WpfHexEditor.Core.AssemblyAnalysis.Models;

namespace WpfHexEditor.Core.AssemblyAnalysis.Services;

/// <summary>
/// Extracts candidate type-name spans from a decompiled C# source text.
/// </summary>
public static class DecompiledTextLinker
{
    // Matches PascalCase identifiers (start uppercase, followed by letters/digits/underscores).
    // Excludes identifiers that are entirely uppercase (constants, macros).
    private static readonly Regex TypeNamePattern = new(
        @"\b([A-Z][a-zA-Z0-9_]*[a-z][a-zA-Z0-9_]*)\b",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    // Standard C# keywords that begin with an uppercase letter (none in lowercase-keyword set,
    // but some BCL shorthands and contextual keywords that should be skipped).
    private static readonly HashSet<string> ExcludedWords = new(StringComparer.Ordinal)
    {
        // C# access modifiers and contextual keywords starting uppercase
        "String", "Boolean", "Byte", "SByte", "Int16", "Int32", "Int64",
        "UInt16", "UInt32", "UInt64", "Single", "Double", "Decimal",
        "Char", "Object", "Void",
        // Common noise
        "True", "False", "Null"
    };

    // ── Public API ────────────────────────────────────────────────────────────

    /// <summary>
    /// Scans <paramref name="decompiledText"/> for PascalCase identifier spans that
    /// are likely type names, and returns them as a list of <see cref="TextSpan"/> values.
    /// Each span is unique (deduplicated by text); the first occurrence wins.
    /// </summary>
    public static IReadOnlyList<TextSpan> ExtractTypeNames(string decompiledText)
    {
        ArgumentNullException.ThrowIfNull(decompiledText);

        var seen   = new HashSet<string>(StringComparer.Ordinal);
        var result = new List<TextSpan>();

        foreach (Match m in TypeNamePattern.Matches(decompiledText))
        {
            var name = m.Value;
            if (ExcludedWords.Contains(name)) continue;
            if (!seen.Add(name))              continue;

            result.Add(new TextSpan(m.Index, m.Length, name));
        }

        return result;
    }
}
