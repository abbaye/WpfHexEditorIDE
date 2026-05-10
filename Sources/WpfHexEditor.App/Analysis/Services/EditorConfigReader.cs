// ==========================================================
// Project: WpfHexEditor.App
// File: Analysis/Services/EditorConfigReader.cs
// Description: Reads `dotnet_diagnostic.WH00xx.severity = warning|error|info|none`
//              entries from .editorconfig files (root + parent walk) and
//              applies them to CodeAnalysisOptions.Rules at runtime.
// Architecture Notes:
//     Stateless. Pure parser — no filesystem cache (called once per analysis).
//     Walks up from solutionDir looking for .editorconfig with `root = true`.
// ==========================================================

using System.IO;
using System.Text.RegularExpressions;
using WpfHexEditor.App.Analysis.Models;

namespace WpfHexEditor.App.Analysis.Services;

internal static class EditorConfigReader
{
    private static readonly Regex EntryPattern =
        new(@"dotnet_diagnostic\.(WH\d{4})\.severity\s*=\s*(none|silent|suggestion|info|warning|error)",
            RegexOptions.Compiled | RegexOptions.IgnoreCase);

    /// <summary>Apply severities found in .editorconfig to the rules list (in-place).</summary>
    internal static void ApplyTo(CodeAnalysisOptions options, string solutionDir)
    {
        foreach (var path in DiscoverEditorConfigs(solutionDir))
        {
            try
            {
                foreach (var line in File.ReadAllLines(path))
                {
                    var match = EntryPattern.Match(line);
                    if (!match.Success) continue;

                    string ruleId = match.Groups[1].Value.ToUpperInvariant();
                    string sev    = match.Groups[2].Value.ToLowerInvariant();
                    var rule      = options.GetRule(ruleId);
                    if (rule is null) continue;

                    rule.Severity = sev switch
                    {
                        "error"            => RuleSeverity.Error,
                        "warning"          => RuleSeverity.Warning,
                        "none" or "silent" => RuleSeverity.Disabled,
                        _                  => RuleSeverity.Info,
                    };
                }
            }
            catch { /* unreadable .editorconfig — skip */ }
        }
    }

    private static IEnumerable<string> DiscoverEditorConfigs(string startDir)
    {
        var dir = startDir;
        for (int i = 0; i < 8 && !string.IsNullOrEmpty(dir); i++)
        {
            var path = Path.Combine(dir, ".editorconfig");
            if (File.Exists(path))
            {
                yield return path;
                if (IsRoot(path)) yield break;
            }
            dir = Path.GetDirectoryName(dir) ?? string.Empty;
        }
    }

    private static bool IsRoot(string path)
    {
        try
        {
            foreach (var line in File.ReadAllLines(path))
            {
                var trimmed = line.Trim();
                if (trimmed.StartsWith("root", StringComparison.OrdinalIgnoreCase)
                    && trimmed.Contains('=') && trimmed.Contains("true", StringComparison.OrdinalIgnoreCase))
                    return true;
            }
        }
        catch { }
        return false;
    }
}
