// ==========================================================
// Project: WpfHexEditor.Core.DocumentStructure
// File: Providers/IniStructureProvider.cs
// Created: 2026-04-05
// Description:
//     Structure provider for INI-style configuration files.
//     Parses [Section] headers and key=value entries.
//
// Architecture Notes:
//     Priority 300. Pure regex line scan — no external dependencies.
// ==========================================================

using System.IO;
using System.Text.RegularExpressions;
using WpfHexEditor.SDK.ExtensionPoints.DocumentStructure;

namespace WpfHexEditor.Core.DocumentStructure.Providers;

/// <summary>
/// INI/config file structure provider (Priority 300).
/// </summary>
public sealed partial class IniStructureProvider : IDocumentStructureProvider
{
    public string DisplayName => "INI Sections";
    public int Priority => 300;

    private static readonly HashSet<string> s_extensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".ini", ".cfg", ".conf", ".properties", ".editorconfig", ".gitconfig",
    };

    public bool CanProvide(string? filePath, string? documentType, string? language)
    {
        if (string.IsNullOrEmpty(filePath)) return false;
        return s_extensions.Contains(Path.GetExtension(filePath));
    }

    public async Task<DocumentStructureResult?> GetStructureAsync(string filePath, CancellationToken ct = default)
    {
        if (!File.Exists(filePath)) return null;

        var lines = await File.ReadAllLinesAsync(filePath, ct).ConfigureAwait(false);
        var sections = new List<DocumentStructureNode>();
        List<DocumentStructureNode>? currentChildren = null;
        string? currentSection = null;
        int currentSectionLine = -1;

        for (var i = 0; i < lines.Length; i++)
        {
            ct.ThrowIfCancellationRequested();
            var line = lines[i].Trim();
            if (string.IsNullOrEmpty(line) || line.StartsWith(';') || line.StartsWith('#'))
                continue;

            var sectionMatch = SectionRegex().Match(line);
            if (sectionMatch.Success)
            {
                if (currentSection is not null)
                {
                    sections.Add(new DocumentStructureNode
                    {
                        Name = currentSection,
                        Kind = "section",
                        StartLine = currentSectionLine,
                        Children = currentChildren ?? [],
                    });
                }

                currentSection = sectionMatch.Groups[1].Value;
                currentSectionLine = i + 1;
                currentChildren = [];
                continue;
            }

            var keyMatch = KeyValueRegex().Match(line);
            if (keyMatch.Success && currentChildren is not null)
            {
                currentChildren.Add(new DocumentStructureNode
                {
                    Name = keyMatch.Groups[1].Value.Trim(),
                    Kind = "key",
                    Detail = keyMatch.Groups[2].Value.Trim(),
                    StartLine = i + 1,
                });
            }
            else if (keyMatch.Success && currentChildren is null)
            {
                // Keys before any section → root-level keys
                sections.Add(new DocumentStructureNode
                {
                    Name = keyMatch.Groups[1].Value.Trim(),
                    Kind = "key",
                    Detail = keyMatch.Groups[2].Value.Trim(),
                    StartLine = i + 1,
                });
            }
        }

        // Flush last section
        if (currentSection is not null)
        {
            sections.Add(new DocumentStructureNode
            {
                Name = currentSection,
                Kind = "section",
                StartLine = currentSectionLine,
                Children = currentChildren ?? [],
            });
        }

        if (sections.Count == 0) return null;

        return new DocumentStructureResult
        {
            Nodes = sections,
            FilePath = filePath,
            Language = "ini",
        };
    }

    [GeneratedRegex(@"^\[(.+)\]$")]
    private static partial Regex SectionRegex();

    [GeneratedRegex(@"^([^=]+)=(.*)$")]
    private static partial Regex KeyValueRegex();
}
