// ==========================================================
// Project: WpfHexEditor.Core.AssemblyAnalysis
// File: Services/AssemblyDiffService.cs
// Author: Derek Tremblay
// Created: 2026-03-16
// Description:
//     BCL-only service that compares two AssemblyModel instances and returns
//     a structured diff: which types/members were Added, Removed, or Changed.
//     Comparison key: TypeModel.FullName + MemberModel.Signature.
//
// Architecture Notes:
//     Pattern: Service (stateless, pure function).
//     No WPF / no NuGet — safe from Core layer.
//     Run on a background thread via Task.Run().
// ==========================================================

using WpfHexEditor.Core.AssemblyAnalysis.Models;

namespace WpfHexEditor.Core.AssemblyAnalysis.Services;

/// <summary>Describes how a diff entry changed between baseline and target.</summary>
public enum DiffKind
{
    Added,
    Removed,
    Changed
}

/// <summary>
/// A single diff entry representing a type or member that was Added, Removed, or Changed.
/// </summary>
public sealed class DiffEntry
{
    /// <summary>Fully qualified type name.</summary>
    public string TypeFullName { get; init; } = string.Empty;

    /// <summary>Member signature, or null for type-level changes.</summary>
    public string? MemberSignature { get; init; }

    /// <summary>The kind of change.</summary>
    public DiffKind Kind { get; init; }

    /// <summary>Metadata token of the matching item in the baseline (or 0 if Added).</summary>
    public int BaselineToken { get; init; }

    /// <summary>Metadata token of the matching item in the target (or 0 if Removed).</summary>
    public int TargetToken { get; init; }

    /// <summary>
    /// Display label shown in the diff panel. Includes both type and member (if applicable).
    /// </summary>
    public string DisplayName => MemberSignature is null
        ? TypeFullName
        : $"{TypeFullName} :: {MemberSignature}";
}

/// <summary>Result of a diff comparison between two assemblies.</summary>
public sealed class AssemblyDiff
{
    /// <summary>The baseline assembly (left side / "old version").</summary>
    public AssemblyModel Baseline { get; init; } = null!;

    /// <summary>The target assembly (right side / "new version").</summary>
    public AssemblyModel Target { get; init; } = null!;

    /// <summary>All diff entries, sorted by Kind then by TypeFullName.</summary>
    public IReadOnlyList<DiffEntry> Entries { get; init; } = [];

    // Convenience counts for the status bar summary.
    public int AddedCount   => Entries.Count(e => e.Kind == DiffKind.Added);
    public int RemovedCount => Entries.Count(e => e.Kind == DiffKind.Removed);
    public int ChangedCount => Entries.Count(e => e.Kind == DiffKind.Changed);
}

/// <summary>
/// Stateless diff engine that compares two <see cref="AssemblyModel"/> instances.
/// </summary>
public static class AssemblyDiffService
{
    /// <summary>
    /// Compares <paramref name="baseline"/> against <paramref name="target"/> and returns
    /// the full set of type/member differences. Synchronous — wrap in Task.Run() as needed.
    /// </summary>
    public static AssemblyDiff Compare(AssemblyModel baseline, AssemblyModel target)
    {
        ArgumentNullException.ThrowIfNull(baseline);
        ArgumentNullException.ThrowIfNull(target);

        var entries = new List<DiffEntry>();

        // Build lookup dictionaries keyed by FullName
        var baselineTypes = baseline.Types.ToDictionary(t => t.FullName, StringComparer.Ordinal);
        var targetTypes   = target.Types.ToDictionary(t => t.FullName, StringComparer.Ordinal);

        // -- Types that exist in target but not baseline → Added
        foreach (var (fullName, targetType) in targetTypes)
        {
            if (!baselineTypes.ContainsKey(fullName))
            {
                entries.Add(new DiffEntry
                {
                    TypeFullName  = fullName,
                    Kind          = DiffKind.Added,
                    TargetToken   = targetType.MetadataToken
                });
            }
        }

        // -- Types that exist in baseline but not target → Removed
        foreach (var (fullName, baselineType) in baselineTypes)
        {
            if (!targetTypes.ContainsKey(fullName))
            {
                entries.Add(new DiffEntry
                {
                    TypeFullName   = fullName,
                    Kind           = DiffKind.Removed,
                    BaselineToken  = baselineType.MetadataToken
                });
            }
        }

        // -- Types that exist in both → diff their members
        foreach (var (fullName, baselineType) in baselineTypes)
        {
            if (!targetTypes.TryGetValue(fullName, out var targetType)) continue;

            DiffMembers(entries, fullName, baselineType, targetType);
        }

        // Sort: Removed first, then Added, then Changed; alphabetical within each group.
        entries.Sort(static (a, b) =>
        {
            var byKind = a.Kind.CompareTo(b.Kind);
            if (byKind != 0) return byKind;
            return string.Compare(a.DisplayName, b.DisplayName, StringComparison.OrdinalIgnoreCase);
        });

        return new AssemblyDiff
        {
            Baseline = baseline,
            Target   = target,
            Entries  = entries
        };
    }

    // ── Member-level diff ─────────────────────────────────────────────────────

    private static void DiffMembers(
        List<DiffEntry> entries,
        string          typeFullName,
        TypeModel       baseline,
        TypeModel       target)
    {
        var baseSigs   = BuildMemberSignatureSet(baseline);
        var targetSigs = BuildMemberSignatureSet(target);

        foreach (var (sig, token) in targetSigs)
        {
            if (!baseSigs.ContainsKey(sig))
                entries.Add(new DiffEntry
                {
                    TypeFullName     = typeFullName,
                    MemberSignature  = sig,
                    Kind             = DiffKind.Added,
                    TargetToken      = token
                });
        }

        foreach (var (sig, token) in baseSigs)
        {
            if (!targetSigs.ContainsKey(sig))
                entries.Add(new DiffEntry
                {
                    TypeFullName    = typeFullName,
                    MemberSignature = sig,
                    Kind            = DiffKind.Removed,
                    BaselineToken   = token
                });
        }
    }

    /// <summary>
    /// Builds a dictionary of "member key → metadata token" from a TypeModel.
    /// The key is the member signature (or Name for events without signatures).
    /// </summary>
    private static Dictionary<string, int> BuildMemberSignatureSet(TypeModel type)
    {
        var dict = new Dictionary<string, int>(StringComparer.Ordinal);

        foreach (var m in type.Methods.Concat(type.Fields).Concat(type.Properties).Concat(type.Events))
        {
            var key = m.Signature ?? m.Name;
            dict.TryAdd(key, m.MetadataToken);
        }

        return dict;
    }
}
