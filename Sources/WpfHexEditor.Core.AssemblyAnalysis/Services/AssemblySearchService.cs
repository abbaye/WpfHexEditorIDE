// ==========================================================
// Project: WpfHexEditor.Core.AssemblyAnalysis
// File: Services/AssemblySearchService.cs
// Author: Derek Tremblay
// Created: 2026-03-16
// Description:
//     BCL-only service that searches across one or more loaded AssemblyModel instances.
//     Supports name substring/regex, TypeKind filter, visibility filter, namespace filter,
//     and metadata token lookup. Returns a flat ranked list of AssemblySearchResult records.
//
// Architecture Notes:
//     Pattern: Service (stateless, pure function).
//     No WPF / no NuGet dependencies — safe to use from the Core layer.
//     The caller is responsible for running this on a background thread via Task.Run().
// ==========================================================

using System.Text.RegularExpressions;
using WpfHexEditor.Core.AssemblyAnalysis.Models;

namespace WpfHexEditor.Core.AssemblyAnalysis.Services;

/// <summary>
/// Search query parameters for <see cref="AssemblySearchService.Search"/>.
/// All filters are AND-combined; null / empty values are ignored.
/// </summary>
public sealed class AssemblySearchQuery
{
    /// <summary>Case-insensitive substring match on type or member name. Null = no filter.</summary>
    public string? NameContains { get; init; }

    /// <summary>
    /// Optional .NET regex pattern matched against the full name.
    /// Takes precedence over <see cref="NameContains"/> when set.
    /// Null = no regex filter.
    /// </summary>
    public string? NameRegex { get; init; }

    /// <summary>Filter to a specific type kind (Class, Struct, Interface, Enum, Delegate). Null = all.</summary>
    public TypeKind? Kind { get; init; }

    /// <summary>Filter by visibility. True = public only, False = non-public only, Null = all.</summary>
    public bool? IsPublic { get; init; }

    /// <summary>Case-insensitive namespace substring filter. Null = no filter.</summary>
    public string? Namespace { get; init; }

    /// <summary>
    /// Exact metadata token (decimal or hex with 0x prefix accepted by callers).
    /// 0 = no filter.
    /// </summary>
    public int MetadataToken { get; init; }

    /// <summary>When true, also search member names (methods, fields, properties, events).</summary>
    public bool IncludeMembers { get; init; } = true;
}

/// <summary>A single search hit returned by <see cref="AssemblySearchService.Search"/>.</summary>
public sealed class AssemblySearchResult
{
    /// <summary>The assembly this result belongs to.</summary>
    public AssemblyModel Assembly { get; init; } = null!;

    /// <summary>Fully qualified type name, e.g. "System.Collections.Generic.List".</summary>
    public string TypeFullName { get; init; } = string.Empty;

    /// <summary>
    /// Simple member name when the match is on a member (method/field/property/event),
    /// or null when the match is on the type itself.
    /// </summary>
    public string? MemberName { get; init; }

    /// <summary>MemberKind when the result is a member hit; null for type hits.</summary>
    public MemberKind? MemberKind { get; init; }

    /// <summary>PE file offset for navigation; 0 when not resolved.</summary>
    public long PeOffset { get; init; }

    /// <summary>Metadata token of the matched type or member.</summary>
    public int MetadataToken { get; init; }

    /// <summary>Display name shown in the results grid.</summary>
    public string DisplayName => MemberName is null
        ? TypeFullName
        : $"{TypeFullName}.{MemberName}";
}

/// <summary>
/// Stateless search engine that queries across multiple loaded <see cref="AssemblyModel"/> instances.
/// All operations are synchronous; run inside Task.Run() for background use.
/// </summary>
public static class AssemblySearchService
{
    /// <summary>
    /// Searches <paramref name="assemblies"/> using the supplied <paramref name="query"/>.
    /// Results are sorted by assembly name then by type full name.
    /// </summary>
    public static IReadOnlyList<AssemblySearchResult> Search(
        IEnumerable<AssemblyModel> assemblies,
        AssemblySearchQuery        query)
    {
        ArgumentNullException.ThrowIfNull(query);

        Regex? compiledRegex = null;
        if (!string.IsNullOrEmpty(query.NameRegex))
        {
            try
            {
                compiledRegex = new Regex(
                    query.NameRegex,
                    RegexOptions.IgnoreCase | RegexOptions.CultureInvariant,
                    TimeSpan.FromMilliseconds(200));
            }
            catch (ArgumentException)
            {
                // Invalid regex — treat as plain substring.
            }
        }

        var results = new List<AssemblySearchResult>();

        foreach (var assembly in assemblies)
        {
            if (!assembly.IsManaged) continue;

            foreach (var type in assembly.Types)
            {
                // -- Token filter (exact match, skips everything else)
                if (query.MetadataToken != 0)
                {
                    if (type.MetadataToken == query.MetadataToken)
                        results.Add(MakeTypeResult(assembly, type));

                    if (query.IncludeMembers)
                    {
                        foreach (var m in AllMembers(type))
                            if (m.MetadataToken == query.MetadataToken)
                                results.Add(MakeMemberResult(assembly, type, m));
                    }
                    continue;
                }

                // -- TypeKind filter
                if (query.Kind.HasValue && type.Kind != query.Kind.Value) continue;

                // -- Namespace filter
                if (!string.IsNullOrEmpty(query.Namespace)
                    && !type.Namespace.Contains(query.Namespace, StringComparison.OrdinalIgnoreCase))
                    continue;

                // -- Visibility filter on type
                if (query.IsPublic.HasValue && type.IsPublic != query.IsPublic.Value) continue;

                // -- Name match on type
                if (TypeNameMatches(type, query.NameContains, compiledRegex))
                    results.Add(MakeTypeResult(assembly, type));

                // -- Member search
                if (!query.IncludeMembers) continue;
                if (string.IsNullOrEmpty(query.NameContains) && compiledRegex is null) continue;

                foreach (var member in AllMembers(type))
                {
                    if (query.IsPublic.HasValue && member.IsPublic != query.IsPublic.Value) continue;
                    if (MemberNameMatches(member, query.NameContains, compiledRegex))
                        results.Add(MakeMemberResult(assembly, type, member));
                }
            }
        }

        results.Sort(static (a, b) =>
        {
            var byAssembly = string.Compare(
                a.Assembly.Name, b.Assembly.Name,
                StringComparison.OrdinalIgnoreCase);
            if (byAssembly != 0) return byAssembly;

            return string.Compare(
                a.DisplayName, b.DisplayName,
                StringComparison.OrdinalIgnoreCase);
        });

        return results;
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static bool TypeNameMatches(TypeModel type, string? nameContains, Regex? regex)
    {
        if (string.IsNullOrEmpty(nameContains) && regex is null) return false;

        if (regex is not null)
            return regex.IsMatch(type.Name) || regex.IsMatch(type.FullName);

        return type.Name.Contains(nameContains!, StringComparison.OrdinalIgnoreCase)
            || type.FullName.Contains(nameContains!, StringComparison.OrdinalIgnoreCase);
    }

    private static bool MemberNameMatches(MemberModel member, string? nameContains, Regex? regex)
    {
        if (regex is not null)
            return regex.IsMatch(member.Name)
                || (member.Signature is not null && regex.IsMatch(member.Signature));

        if (string.IsNullOrEmpty(nameContains)) return false;
        return member.Name.Contains(nameContains, StringComparison.OrdinalIgnoreCase)
            || (member.Signature?.Contains(nameContains, StringComparison.OrdinalIgnoreCase) ?? false);
    }

    private static IEnumerable<MemberModel> AllMembers(TypeModel type)
        => type.Methods
               .Concat(type.Fields)
               .Concat(type.Properties)
               .Concat(type.Events);

    private static AssemblySearchResult MakeTypeResult(AssemblyModel assembly, TypeModel type)
        => new()
        {
            Assembly     = assembly,
            TypeFullName = type.FullName,
            PeOffset     = type.PeOffset,
            MetadataToken = type.MetadataToken
        };

    private static AssemblySearchResult MakeMemberResult(
        AssemblyModel assembly, TypeModel type, MemberModel member)
        => new()
        {
            Assembly      = assembly,
            TypeFullName  = type.FullName,
            MemberName    = member.Name,
            MemberKind    = member.Kind,
            PeOffset      = member.PeOffset,
            MetadataToken = member.MetadataToken
        };
}
