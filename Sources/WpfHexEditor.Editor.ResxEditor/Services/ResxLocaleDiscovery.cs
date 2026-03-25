// ==========================================================
// Project: WpfHexEditor.Editor.ResxEditor
// File: Services/ResxLocaleDiscovery.cs
// Description:
//     Scans the same directory as the base .resx file for
//     locale-specific siblings (e.g. Resources.fr.resx,
//     Resources.de-DE.resx) using a regex that matches the
//     standard .NET resource naming convention.
// ==========================================================

using System.Globalization;
using System.IO;
using System.Text.RegularExpressions;
using WpfHexEditor.Editor.ResxEditor.Models;

namespace WpfHexEditor.Editor.ResxEditor.Services;

/// <summary>
/// Discovers locale-specific siblings of a base <c>.resx</c> file.
/// </summary>
public static class ResxLocaleDiscovery
{
    // Matches: BaseName.fr.resx, BaseName.de-DE.resx, BaseName.zh-Hans.resx
    private static readonly Regex LocalePattern =
        new(@"^(.+?)\.([a-z]{2,3}(?:-[A-Za-z]{2,4})?)\.resx$",
            RegexOptions.IgnoreCase | RegexOptions.Compiled);

    /// <summary>
    /// Finds all locale variant files for <paramref name="baseResxPath"/>.
    /// Returns a <see cref="ResxLocaleSet"/> including the base and all variants.
    /// </summary>
    public static ResxLocaleSet Discover(string baseResxPath)
    {
        var dir      = Path.GetDirectoryName(baseResxPath) ?? string.Empty;
        var baseName = Path.GetFileNameWithoutExtension(baseResxPath); // "Resources"

        var variants = new List<(CultureInfo, string)>();

        if (!Directory.Exists(dir)) return new ResxLocaleSet(baseResxPath, variants);

        foreach (var file in Directory.EnumerateFiles(dir, "*.resx"))
        {
            if (string.Equals(file, baseResxPath, StringComparison.OrdinalIgnoreCase))
                continue;

            var fileName = Path.GetFileName(file);
            var match    = LocalePattern.Match(fileName);
            if (!match.Success) continue;

            var fileBase     = match.Groups[1].Value;
            var cultureCode  = match.Groups[2].Value;

            if (!string.Equals(fileBase, baseName, StringComparison.OrdinalIgnoreCase))
                continue;

            try
            {
                var culture = CultureInfo.GetCultureInfo(cultureCode);
                variants.Add((culture, file));
            }
            catch (CultureNotFoundException)
            {
                // Unknown culture code — skip silently
            }
        }

        variants.Sort((a, b) => string.Compare(a.Item1.Name, b.Item1.Name, StringComparison.Ordinal));
        return new ResxLocaleSet(baseResxPath, variants);
    }
}
