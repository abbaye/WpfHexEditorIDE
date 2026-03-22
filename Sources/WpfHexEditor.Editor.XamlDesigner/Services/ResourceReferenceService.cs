// ==========================================================
// Project: WpfHexEditor.Editor.XamlDesigner
// File: ResourceReferenceService.cs
// Author: Derek Tremblay
// Created: 2026-03-17
// Description:
//     Finds all usages of a specific resource key within the raw XAML text.
//     Returns a list of (line, column) locations for navigation.
//
// Architecture Notes:
//     Pure service — stateless text analysis.
//     Uses regex matching for {StaticResource Key} and {DynamicResource Key}.
// ==========================================================

using System.Text.RegularExpressions;

namespace WpfHexEditor.Editor.XamlDesigner.Services;

/// <summary>
/// Finds resource key usages in XAML source text.
/// </summary>
public sealed class ResourceReferenceService
{
    // ── Public API ────────────────────────────────────────────────────────────

    /// <summary>
    /// Returns all line numbers (1-based) where <paramref name="resourceKey"/>
    /// is referenced as StaticResource or DynamicResource in <paramref name="xaml"/>.
    /// </summary>
    public IReadOnlyList<ResourceUsage> FindUsages(string xaml, string resourceKey)
    {
        if (string.IsNullOrWhiteSpace(xaml) || string.IsNullOrWhiteSpace(resourceKey))
            return Array.Empty<ResourceUsage>();

        var results = new List<ResourceUsage>();
        var pattern = $@"\{{(?:StaticResource|DynamicResource)\s+{Regex.Escape(resourceKey)}\}}";
        var regex   = new Regex(pattern, RegexOptions.Compiled);

        var lines = xaml.Split('\n');
        for (int lineIdx = 0; lineIdx < lines.Length; lineIdx++)
        {
            var line = lines[lineIdx];
            foreach (Match m in regex.Matches(line))
            {
                results.Add(new ResourceUsage(
                    Line:   lineIdx + 1,
                    Column: m.Index + 1,
                    Snippet: line.Trim()));
            }
        }

        return results;
    }

    /// <summary>
    /// Returns all resource keys referenced in <paramref name="xaml"/>.
    /// </summary>
    public IReadOnlyList<string> GetAllReferencedKeys(string xaml)
    {
        if (string.IsNullOrWhiteSpace(xaml)) return Array.Empty<string>();

        var keys    = new HashSet<string>(StringComparer.Ordinal);
        var pattern = @"\{(?:StaticResource|DynamicResource)\s+(\S+?)\}";
        var regex   = new Regex(pattern, RegexOptions.Compiled);

        foreach (Match m in regex.Matches(xaml))
            keys.Add(m.Groups[1].Value);

        return keys.OrderBy(k => k).ToList();
    }

    /// <summary>
    /// Replaces the attribute value of <paramref name="attributeName"/> on the element
    /// with uid tag <c>xd_<paramref name="elementUid"/></c> with a StaticResource reference.
    /// Returns the patched XAML or null on failure.
    /// </summary>
    public string? ApplyResourceReference(string xaml, int elementUid, string attributeName, string resourceKey)
    {
        if (string.IsNullOrWhiteSpace(xaml)) return null;

        // Find the element by its UID tag attribute (Tag="xd_N").
        var tagPattern = $@"Tag=""xd_{elementUid}""";
        int tagPos     = xaml.IndexOf(tagPattern, StringComparison.Ordinal);
        if (tagPos < 0) return null;

        // Find the start of the element's opening tag.
        int tagStart = xaml.LastIndexOf('<', tagPos);
        if (tagStart < 0) return null;

        // Find the end of the opening tag (either '>' or '/>').
        int tagEnd = xaml.IndexOf('>', tagPos);
        if (tagEnd < 0) return null;
        string openTag = xaml.Substring(tagStart, tagEnd - tagStart + 1);

        // Build the replacement value.
        string newRef    = $"{{StaticResource {resourceKey}}}";
        string attrValue = $"{attributeName}=\"{newRef}\"";

        // Try to replace an existing attribute.
        var existingPattern = new System.Text.RegularExpressions.Regex(
            $@"{System.Text.RegularExpressions.Regex.Escape(attributeName)}=""[^""]*""");
        string newTag;
        if (existingPattern.IsMatch(openTag))
            newTag = existingPattern.Replace(openTag, attrValue, 1);
        else
        {
            // Insert before the closing '>' or '/>'.
            int insertBefore = openTag.EndsWith("/>", StringComparison.Ordinal)
                ? openTag.Length - 2
                : openTag.Length - 1;
            newTag = openTag.Insert(insertBefore, $" {attrValue}");
        }

        return xaml.Substring(0, tagStart) + newTag + xaml.Substring(tagEnd + 1);
    }
}

/// <summary>Location of a single resource reference within a XAML file.</summary>
public sealed record ResourceUsage(int Line, int Column, string Snippet);
