// ==========================================================
// Project: WpfHexEditor.Core.AssemblyAnalysis
// File: Models/PeOffsetMap.cs
// Author: Derek Tremblay
// Created: 2026-03-08
// License: GNU Affero General Public License v3.0 (AGPL-3.0)
// Description:
//     Thin Dictionary<int,long> wrapper mapping ECMA-335 metadata tokens
//     to raw PE file byte offsets resolved by PeOffsetResolver.
//
// Architecture Notes:
//     Pattern: Value Object / thin wrapper.
//     Immutable after construction; populated by PeOffsetResolver during analysis.
// ==========================================================

namespace WpfHexEditor.Core.AssemblyAnalysis.Models;

/// <summary>
/// Maps ECMA-335 metadata tokens to raw PE file byte offsets.
/// Populated by <see cref="Services.PeOffsetResolver"/> during assembly analysis.
/// A return value of 0 from <see cref="Resolve"/> means "not resolved".
/// </summary>
public sealed class PeOffsetMap
{
    private readonly Dictionary<int, long> _map = new();

    /// <summary>Records a token → file offset mapping.</summary>
    public void Add(int metadataToken, long fileOffset)
        => _map[metadataToken] = fileOffset;

    /// <summary>
    /// Looks up the file offset for <paramref name="metadataToken"/>.
    /// Returns false when the token was not resolved.
    /// </summary>
    public bool TryResolve(int metadataToken, out long fileOffset)
        => _map.TryGetValue(metadataToken, out fileOffset);

    /// <summary>
    /// Returns the file offset for <paramref name="metadataToken"/>,
    /// or 0 when the token was not resolved.
    /// </summary>
    public long Resolve(int metadataToken)
        => _map.TryGetValue(metadataToken, out var offset) ? offset : 0L;

    /// <summary>Number of resolved token mappings.</summary>
    public int Count => _map.Count;
}
