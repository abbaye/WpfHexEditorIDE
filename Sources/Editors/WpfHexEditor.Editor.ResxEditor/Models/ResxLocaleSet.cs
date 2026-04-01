// ==========================================================
// Project: WpfHexEditor.Editor.ResxEditor
// File: Models/ResxLocaleSet.cs
// Description:
//     Groups a base .resx file with all its discovered locale
//     siblings (e.g. Resources.fr.resx, Resources.de-DE.resx).
// ==========================================================

using System.Globalization;

namespace WpfHexEditor.Editor.ResxEditor.Models;

/// <summary>
/// A base resource file together with all its locale-specific variants
/// found in the same directory.
/// </summary>
/// <param name="BasePath">Absolute path of the culture-neutral base file (e.g. <c>Resources.resx</c>).</param>
/// <param name="Variants">Ordered list of locale variant paths with their associated <see cref="CultureInfo"/>.</param>
public sealed record ResxLocaleSet(
    string BasePath,
    IReadOnlyList<(CultureInfo Culture, string Path)> Variants)
{
    /// <summary>Returns true when at least one locale variant exists.</summary>
    public bool HasVariants => Variants.Count > 0;

    /// <summary>All paths including the base.</summary>
    public IEnumerable<string> AllPaths
        => Variants.Select(v => v.Path).Prepend(BasePath);
}
