// ==========================================================
// Project: WpfHexEditor.SDK
// File: Models/PluginDependencySpec.cs
// Created: 2026-03-15
// Description:
//     Parsed representation of a single plugin dependency string.
//     Splits "BinaryAnalysisCore >=1.0" into PluginId and VersionConstraint.
// ==========================================================

namespace WpfHexEditor.SDK.Models;

/// <summary>
/// A parsed plugin dependency — a plugin ID combined with an optional version constraint.
/// Parsed from raw manifest dependency strings like <c>"BinaryAnalysisCore >=1.0"</c>.
/// </summary>
public sealed record PluginDependencySpec(string PluginId, PluginVersionConstraint Constraint)
{
    /// <summary>
    /// Parses a raw dependency string.
    /// Formats accepted:
    ///   "BinaryAnalysisCore"         → any version
    ///   "BinaryAnalysisCore >=1.0"   → version 1.0 or higher
    ///   "WpfHexEditor.Core =0.8.0"   → exact version
    ///   "SomePlugin ^2.0"            → compatible with 2.x
    /// </summary>
    public static PluginDependencySpec Parse(string raw)
    {
        raw = raw.Trim();
        var spaceIdx = raw.IndexOf(' ');

        if (spaceIdx < 0)
            return new PluginDependencySpec(raw, PluginVersionConstraint.Any);

        var pluginId    = raw[..spaceIdx].Trim();
        var versionExpr = raw[(spaceIdx + 1)..].Trim();
        return new PluginDependencySpec(pluginId, PluginVersionConstraint.Parse(versionExpr));
    }

    /// <summary>
    /// Returns true when the given <paramref name="version"/> satisfies this dependency's constraint.
    /// </summary>
    public bool IsSatisfiedBy(Version version) => Constraint.Satisfies(version);

    public override string ToString() => $"{PluginId} {Constraint}";
}
