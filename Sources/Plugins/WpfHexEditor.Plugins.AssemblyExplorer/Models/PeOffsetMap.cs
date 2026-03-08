// ==========================================================
// Project: WpfHexEditor.Plugins.AssemblyExplorer
// File: Models/PeOffsetMap.cs
// Author: Derek Tremblay
// Created: 2026-03-08
// Description:
//     Maps ECMA-335 metadata tokens to raw PE file byte offsets.
//     Populated by PeOffsetResolver during assembly analysis.
//     Consumed by node ViewModels to supply HexEditor navigation offsets.
//
// Architecture Notes:
//     Simple dictionary wrapper — intentionally not thread-safe.
//     PeOffsetResolver fills it on the background thread, then the
//     completed map is handed to the UI thread as part of AssemblyModel.
// ==========================================================

namespace WpfHexEditor.Plugins.AssemblyExplorer.Models;

/// <summary>
/// Maps ECMA-335 metadata tokens → raw PE file byte offsets.
/// A token of 0 is never a valid metadata token, so it is used as the "not found" sentinel.
/// </summary>
public sealed class PeOffsetMap
{
    private readonly Dictionary<int, long> _map = [];

    /// <summary>Number of resolved offsets in this map.</summary>
    public int Count => _map.Count;

    /// <summary>Records a token → file offset association.</summary>
    public void Add(int metadataToken, long fileOffset)
        => _map[metadataToken] = fileOffset;

    /// <summary>
    /// Attempts to retrieve the file offset for the given metadata token.
    /// Returns false (fileOffset = 0) when the token was not resolved.
    /// </summary>
    public bool TryResolve(int metadataToken, out long fileOffset)
        => _map.TryGetValue(metadataToken, out fileOffset);

    /// <summary>
    /// Returns the file offset for the given token, or 0 if not resolved.
    /// A return value of 0 means HexEditor sync should be skipped for this node.
    /// </summary>
    public long Resolve(int metadataToken)
        => _map.TryGetValue(metadataToken, out var offset) ? offset : 0L;
}
