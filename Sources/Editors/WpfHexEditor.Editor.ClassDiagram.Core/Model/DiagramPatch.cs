// ==========================================================
// Project: WpfHexEditor.Editor.ClassDiagram.Core
// File: Model/DiagramPatch.cs
// Author: Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.6
// Created: 2026-04-07
// Description:
//     Immutable patch record describing the delta between two
//     versions of a DiagramDocument. Produced by the live-sync
//     service and consumed by DiagramCanvas.ApplyPatch().
// ==========================================================

namespace WpfHexEditor.Editor.ClassDiagram.Core.Model;

/// <summary>
/// Delta between two versions of a <see cref="DiagramDocument"/>.
/// All collections are non-null; empty when nothing changed in that category.
/// </summary>
public sealed record DiagramPatch
{
    public IReadOnlyList<ClassNode>         AddedNodes           { get; init; } = [];
    public IReadOnlyList<string>            RemovedNodeIds       { get; init; } = [];
    public IReadOnlyList<ClassNode>         ModifiedNodes        { get; init; } = [];
    public IReadOnlyList<ClassRelationship> AddedRelationships   { get; init; } = [];
    /// <summary>Keys of removed relationships in "SourceId>TargetId>Kind" format.</summary>
    public IReadOnlyList<string>            RemovedRelationshipIds { get; init; } = [];

    /// <summary>True when no changes were detected.</summary>
    public bool IsEmpty =>
        AddedNodes.Count           == 0 &&
        RemovedNodeIds.Count       == 0 &&
        ModifiedNodes.Count        == 0 &&
        AddedRelationships.Count   == 0 &&
        RemovedRelationshipIds.Count == 0;

    /// <summary>
    /// Computes a patch by diffing <paramref name="previous"/> against <paramref name="next"/>.
    /// </summary>
    public static DiagramPatch Diff(DiagramDocument previous, DiagramDocument next)
    {
        var prevById = previous.Classes.ToDictionary(n => n.Id, StringComparer.Ordinal);
        var nextById = next.Classes.ToDictionary(n => n.Id, StringComparer.Ordinal);

        var added    = next.Classes.Where(n => !prevById.ContainsKey(n.Id)).ToList();
        var removed  = previous.Classes.Where(n => !nextById.ContainsKey(n.Id))
                                        .Select(n => n.Id).ToList();
        var modified = next.Classes
            .Where(n => prevById.TryGetValue(n.Id, out var old) && !NodeEquals(old, n))
            .ToList();

        static string RelKey(ClassRelationship r) =>
            $"{r.SourceId}>{r.TargetId}>{(int)r.Kind}";

        var prevRelKeys = previous.Relationships
            .Select(RelKey).ToHashSet(StringComparer.Ordinal);
        var nextRelKeys = next.Relationships
            .Select(RelKey).ToHashSet(StringComparer.Ordinal);

        var addedRels   = next.Relationships.Where(r => !prevRelKeys.Contains(RelKey(r))).ToList();
        var removedRels = previous.Relationships.Where(r => !nextRelKeys.Contains(RelKey(r)))
                                                 .Select(RelKey).ToList();

        return new DiagramPatch
        {
            AddedNodes              = added,
            RemovedNodeIds          = removed,
            ModifiedNodes           = modified,
            AddedRelationships      = addedRels,
            RemovedRelationshipIds  = removedRels,
        };
    }

    private static bool NodeEquals(ClassNode a, ClassNode b) =>
        a.Name    == b.Name    &&
        a.Kind    == b.Kind    &&
        a.Members.Count == b.Members.Count &&
        a.Members.Select(m => m.DisplayLabel)
         .SequenceEqual(b.Members.Select(m => m.DisplayLabel));
}
