// ==========================================================
// Project: WpfHexEditor.SDK
// File: Models/PluginVersionConstraint.cs
// Created: 2026-03-15
// Description:
//     Parses and evaluates a version constraint expression used in plugin dependencies.
//     Supported operators: >=, >, <=, <, =, ^ (compatible with / same major).
//
// Architecture Notes:
//     Zero external dependencies — pure BCL parsing.
//     Used by PluginDependencySpec and PluginDependencyGraph.
// ==========================================================

namespace WpfHexEditor.SDK.Models;

/// <summary>
/// Represents a version constraint like <c>>=1.0</c>, <c>^2.0</c>, or <c>=1.2.3</c>.
/// Used in plugin dependency declarations: <c>"BinaryAnalysisCore >=1.0"</c>.
/// </summary>
public sealed class PluginVersionConstraint
{
    private readonly string _operator;
    private readonly Version _version;

    /// <summary>A constraint that accepts any version (≥ 0.0).</summary>
    public static readonly PluginVersionConstraint Any = new(">=", new Version(0, 0));

    /// <summary>Parses a version constraint expression such as <c>>=1.0</c> or <c>^2.0</c>.</summary>
    public static PluginVersionConstraint Parse(string expression)
    {
        expression = expression.Trim();

        foreach (var op in new[] { ">=", "<=", ">", "<", "=", "^" })
        {
            if (!expression.StartsWith(op, StringComparison.Ordinal))
                continue;

            var versionStr = expression[op.Length..].Trim();
            return Version.TryParse(NormalizeVersion(versionStr), out var v)
                ? new PluginVersionConstraint(op, v)
                : Any;
        }

        // No operator — treat bare version string as exact match
        return Version.TryParse(NormalizeVersion(expression), out var bare)
            ? new PluginVersionConstraint("=", bare)
            : Any;
    }

    private PluginVersionConstraint(string op, Version version)
    {
        _operator = op;
        _version  = version;
    }

    /// <summary>Returns true when <paramref name="candidate"/> satisfies this constraint.</summary>
    public bool Satisfies(Version candidate)
    {
        var cmp = candidate.CompareTo(_version);
        return _operator switch
        {
            ">="  => cmp >= 0,
            ">"   => cmp > 0,
            "<="  => cmp <= 0,
            "<"   => cmp < 0,
            "="   => cmp == 0,
            "^"   => candidate.Major == _version.Major && cmp >= 0,
            _     => true
        };
    }

    public override string ToString() => $"{_operator}{_version}";

    // Ensures "1.0" becomes "1.0.0.0" so Version.TryParse succeeds
    private static string NormalizeVersion(string v)
    {
        var parts = v.Split('.');
        return parts.Length switch
        {
            1 => $"{v}.0.0.0",
            2 => $"{v}.0.0",
            3 => $"{v}.0",
            _ => v
        };
    }
}
