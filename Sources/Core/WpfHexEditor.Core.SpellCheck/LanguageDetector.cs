// ==========================================================
// Project: WpfHexEditor.Core.SpellCheck
// File: LanguageDetector.cs
// Description:
//     Lightweight language detection based on stopword frequency scoring.
//     Scores each installed language by counting how many of its stopwords
//     appear in the sample text. No external dependency — O(words * langs).
// Architecture:
//     Only languages present in LanguageCatalog AND installed on disk are
//     candidates. Minimum confidence threshold avoids false positives on
//     very short texts.
// ==========================================================

using System.Text.RegularExpressions;

namespace WpfHexEditor.Core.SpellCheck;

public static class LanguageDetector
{
    private static readonly Regex TokenRx = new(
        @"\b\p{L}{2,}\b",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    /// <summary>
    /// Returns the BCP-47 language code most likely matching <paramref name="text"/>,
    /// or <c>null</c> if confidence is below threshold or no installed language matches.
    /// </summary>
    /// <param name="text">Sample text (first 2000 chars is enough).</param>
    /// <param name="installedCodes">Only languages actually installed on disk are candidates.</param>
    /// <param name="minConfidence">Fraction of stopwords that must match (0–1). Default 0.05.</param>
    public static string? Detect(
        string               text,
        IEnumerable<string>  installedCodes,
        double               minConfidence = 0.05)
    {
        if (string.IsNullOrWhiteSpace(text)) return null;

        var sample = text.Length > 4000 ? text[..4000] : text;

        // Tokenize sample to lower-case word set for fast lookup
        var wordSet = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (Match m in TokenRx.Matches(sample))
            wordSet.Add(m.Value.ToLowerInvariant());

        if (wordSet.Count == 0) return null;

        var installedSet = new HashSet<string>(installedCodes, StringComparer.OrdinalIgnoreCase);

        string? bestCode  = null;
        double  bestScore = 0;

        foreach (var entry in LanguageCatalog.Languages)
        {
            if (!installedSet.Contains(entry.Code)) continue;
            if (entry.Stopwords.Length == 0)        continue;

            int hits = entry.Stopwords.Count(sw => wordSet.Contains(sw));
            double score = (double)hits / entry.Stopwords.Length;

            if (score > bestScore)
            {
                bestScore = score;
                bestCode  = entry.Code;
            }
        }

        return bestScore >= minConfidence ? bestCode : null;
    }
}
