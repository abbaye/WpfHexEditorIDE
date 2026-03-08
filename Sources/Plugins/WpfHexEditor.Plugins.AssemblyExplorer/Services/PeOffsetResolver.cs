// ==========================================================
// Project: WpfHexEditor.Plugins.AssemblyExplorer
// File: Services/PeOffsetResolver.cs
// Author: Derek Tremblay
// Created: 2026-03-08
// Description:
//     Resolves raw PE file byte offsets for metadata row handles.
//     Phase 1 stub: all methods return 0 (offset not resolved).
//     Full ECMA-335 §II.24 table offset resolution is deferred to Phase 2.
//
// Architecture Notes:
//     A return value of 0 means "not resolved" — callers (ViewModels)
//     must skip HexEditor sync when PeOffset == 0.
//     Future implementation will read the Metadata section RVA from
//     PEReader.PEHeaders, walk the #~ stream table headers, compute
//     per-row byte offsets using ECMA-335 §II.22 row sizes.
// ==========================================================

using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;

namespace WpfHexEditor.Plugins.AssemblyExplorer.Services;

/// <summary>
/// Resolves raw PE file byte offsets for ECMA-335 metadata row handles.
/// Phase 1 stub — all overloads return 0.
/// </summary>
public sealed class PeOffsetResolver
{
    // ── TypeDefinition ────────────────────────────────────────────────────────

    /// <summary>
    /// Returns the raw PE file offset of the TypeDef row identified by
    /// <paramref name="handle"/>, or 0 if resolution is not yet implemented.
    /// </summary>
    public long Resolve(TypeDefinitionHandle handle, PEReader peReader, MetadataReader mdReader)
    {
        // TODO (Phase 2): compute offset from #~ stream table header
        // Algorithm:
        //   1. peReader.GetMetadata() → find #~ stream base RVA
        //   2. Walk table headers to locate TypeDef table start offset
        //   3. row index = MetadataTokens.GetRowNumber(handle) - 1 (0-based)
        //   4. offset = tableStart + rowIndex * TypeDefRowSize
        return 0L;
    }

    // ── MethodDefinition ─────────────────────────────────────────────────────

    /// <summary>
    /// Returns the raw PE file offset of the MethodDef row, or 0 if not resolved.
    /// </summary>
    public long Resolve(MethodDefinitionHandle handle, PEReader peReader, MetadataReader mdReader)
    {
        // TODO (Phase 2): same algorithm as TypeDefinition, MethodDef table
        return 0L;
    }

    // ── FieldDefinition ──────────────────────────────────────────────────────

    /// <summary>
    /// Returns the raw PE file offset of the FieldDef row, or 0 if not resolved.
    /// </summary>
    public long Resolve(FieldDefinitionHandle handle, PEReader peReader, MetadataReader mdReader)
    {
        // TODO (Phase 2): FieldDef table
        return 0L;
    }
}
