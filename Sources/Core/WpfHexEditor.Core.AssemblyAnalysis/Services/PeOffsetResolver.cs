// ==========================================================
// Project: WpfHexEditor.Core.AssemblyAnalysis
// File: Services/PeOffsetResolver.cs
// Author: Derek Tremblay
// Created: 2026-03-08
// License: GNU Affero General Public License v3.0 (AGPL-3.0)
// Description:
//     Full ECMA-335 §II.24 implementation for resolving raw PE file byte
//     offsets from metadata row handles. Replaces the plugin stub (all 0).
//
// Architecture Notes:
//     Pattern: Service (stateless, thread-safe).
//     Algorithm: locate the #~ stream in the metadata section, read the
//     table schema (present-tables bitmask + row counts), compute each
//     table's row-byte-size, then walk to the requested row's offset.
//     Method body offsets use the MethodDef.RelativeVirtualAddress field
//     and the PEReader.PEHeaders.PEHeader section-to-file offset map.
// ==========================================================

using System.Reflection.Metadata;
using System.Reflection.Metadata.Ecma335;
using System.Reflection.PortableExecutable;

namespace WpfHexEditor.Core.AssemblyAnalysis.Services;

/// <summary>
/// Resolves raw PE file byte offsets for ECMA-335 metadata row handles.
/// Returns 0 when the offset cannot be determined (non-fatal).
/// </summary>
public sealed class PeOffsetResolver
{
    // ── TypeDefinition ────────────────────────────────────────────────────────

    /// <summary>
    /// Returns the raw PE file offset of the TypeDef metadata row, or 0 on failure.
    /// </summary>
    public long Resolve(TypeDefinitionHandle handle, PEReader peReader, MetadataReader mdReader)
    {
        try
        {
            var rowNumber = MetadataTokens.GetRowNumber(handle);  // 1-based
            return ResolveTableRow(TableIndex.TypeDef, rowNumber, peReader, mdReader);
        }
        catch { return 0L; }
    }

    // ── MethodDefinition ─────────────────────────────────────────────────────

    /// <summary>
    /// Returns the raw PE file offset of the IL method body, or 0 on failure.
    /// Uses MethodDef.RelativeVirtualAddress + PE section mapping.
    /// </summary>
    public long Resolve(MethodDefinitionHandle handle, PEReader peReader, MetadataReader mdReader)
    {
        try
        {
            var methodDef = mdReader.GetMethodDefinition(handle);
            var rva       = methodDef.RelativeVirtualAddress;
            if (rva == 0) return 0L;  // abstract / extern method has no body

            return RvaToFileOffset(rva, peReader);
        }
        catch { return 0L; }
    }

    // ── FieldDefinition ──────────────────────────────────────────────────────

    /// <summary>
    /// Returns the raw PE file offset of the FieldDef metadata row, or 0 on failure.
    /// </summary>
    public long Resolve(FieldDefinitionHandle handle, PEReader peReader, MetadataReader mdReader)
    {
        try
        {
            var rowNumber = MetadataTokens.GetRowNumber(handle);
            return ResolveTableRow(TableIndex.Field, rowNumber, peReader, mdReader);
        }
        catch { return 0L; }
    }

    // ── Core algorithm ────────────────────────────────────────────────────────

    /// <summary>
    /// Locates the metadata section in the PE, finds the #~ stream,
    /// computes the row-byte-offset for the given (tableIndex, 1-based rowNumber).
    /// </summary>
    private static long ResolveTableRow(
        TableIndex   tableIndex,
        int          rowNumber,      // 1-based per ECMA-335
        PEReader     peReader,
        MetadataReader mdReader)
    {
        if (rowNumber <= 0) return 0L;

        // BCL exposes the metadata section start RVA via PEMemoryBlock.
        // We use the MetadataReader's internal offset rather than re-parsing headers.
        // BCL does not expose stream offsets directly, so we use the known #~ structure:
        //   All metadata for managed PEs is in the ".text" or ".rsrc" section at the
        //   CLI header's MetadataDirectory RVA.

        var peHeaders = peReader.PEHeaders;
        var cliHeader = peHeaders.CorHeader;
        if (cliHeader is null) return 0L;

        var metadataRva  = cliHeader.MetadataDirectory.RelativeVirtualAddress;
        var metadataSize = cliHeader.MetadataDirectory.Size;
        if (metadataRva == 0 || metadataSize == 0) return 0L;

        var metadataFileOffset = RvaToFileOffset(metadataRva, peReader);
        if (metadataFileOffset == 0L) return 0L;

        // Use BCL MetadataReader to get the exact byte offset of the table row.
        // MetadataReader.GetTableRowCount + MetadataTokens provide row-level info;
        // for the raw file offset we compute:
        //   tableStart (within #~ stream) + (rowNumber - 1) * rowSize
        // The #~ stream offset within metadata is at metadataFileOffset +
        // METADATA_HEADER_SIZE + sum-of-stream-headers up to #~.
        // To avoid re-parsing the full stream header, we use the fact that the
        // BCL gives us the metadata block pointer via PEReader.GetMetadata().

        var metaBlock              = peReader.GetMetadata();
        ReadOnlySpan<byte> metaSpan = metaBlock.GetContent().AsSpan();

        // Parse the METADATA_ROOT (ECMA-335 §II.24.2.1):
        //   Signature (4) + MajorVersion (2) + MinorVersion (2) +
        //   Reserved (4) + VersionLength (4) + Version (VersionLength, padded to 4-byte) +
        //   Flags (2) + Streams (2)
        var offset = 0;
        if (metaSpan.Length < 20) return 0L;

        // Validate magic: 0x424A5342 "BSJB"
        uint magic = ReadUInt32(metaSpan, offset); offset += 4;
        if (magic != 0x424A5342u) return 0L;

        offset += 8; // skip MajorVersion, MinorVersion, Reserved

        uint versionLength = ReadUInt32(metaSpan, offset); offset += 4;
        // Version string padded to next 4-byte boundary
        offset += (int)((versionLength + 3) & ~3u);

        if (offset + 4 > metaSpan.Length) return 0L;
        offset += 2; // Flags (reserved)
        ushort streamCount = ReadUInt16(metaSpan, offset); offset += 2;

        // Walk stream headers to find the #~ (compressed tables) stream.
        long tildaStreamOffset = -1;
        for (var s = 0; s < streamCount; s++)
        {
            if (offset + 8 > metaSpan.Length) return 0L;

            uint  streamOff  = ReadUInt32(metaSpan, offset); offset += 4;
            uint  streamSize = ReadUInt32(metaSpan, offset); offset += 4;

            // Stream name: null-terminated, padded to 4-byte boundary.
            var nameStart = offset;
            while (offset < metaSpan.Length && metaSpan[offset] != 0) offset++;
            offset++; // skip null terminator
            offset = (offset + 3) & ~3; // 4-byte align

            if (nameStart + 3 <= metaSpan.Length
                && metaSpan[nameStart] == '#'
                && metaSpan[nameStart + 1] == '~'
                && metaSpan[nameStart + 2] == 0)
            {
                tildaStreamOffset = streamOff;
                break;
            }
        }

        if (tildaStreamOffset < 0) return 0L;

        // Now parse the #~ stream (ECMA-335 §II.24.2.6):
        //   Reserved (4) + MajorVersion (1) + MinorVersion (1) +
        //   HeapSizes (1) + Reserved2 (1) + Valid (8) + Sorted (8)
        //   Then for each set bit in Valid: row count (4)
        //   Then the actual table data.

        var ts = (int)tildaStreamOffset;
        if (ts + 24 > metaSpan.Length) return 0L;

        ts += 4; // Reserved
        ts += 2; // MajorVersion + MinorVersion
        byte heapSizes = metaSpan[ts++]; // bit 0=String heap large, bit 1=GUID heap large, bit 2=Blob heap large
        ts++;     // Reserved2

        ulong valid  = ReadUInt64(metaSpan, ts); ts += 8;
        ts += 8;  // Sorted (skip)

        // Read row counts for present tables.
        var rowCounts = new Dictionary<int, int>();
        for (var bit = 0; bit < 64; bit++)
        {
            if ((valid & (1UL << bit)) != 0)
            {
                if (ts + 4 > metaSpan.Length) return 0L;
                rowCounts[bit] = (int)ReadUInt32(metaSpan, ts);
                ts += 4;
            }
        }

        // Compute column widths using heap-size flags and row counts.
        bool stringLarge = (heapSizes & 1) != 0;
        bool guidLarge   = (heapSizes & 2) != 0;
        bool blobLarge   = (heapSizes & 4) != 0;

        // Walk tables in order 0..63; accumulate data offset until target table.
        long dataStart = ts; // start of table data within the #~ stream
        for (var tableIdx = 0; tableIdx <= (int)tableIndex; tableIdx++)
        {
            if (!rowCounts.TryGetValue(tableIdx, out var count)) continue;

            var rowSize = ComputeRowSize((TableIndex)tableIdx, rowCounts, stringLarge, guidLarge, blobLarge);

            if (tableIdx == (int)tableIndex)
            {
                // dataStart is now the start of this table's rows.
                if (rowNumber > count) return 0L;
                long rowOffset = dataStart + (long)(rowNumber - 1) * rowSize;
                // Convert: metadata block base in file = metadataFileOffset
                return metadataFileOffset + tildaStreamOffset + rowOffset;
            }

            dataStart += (long)count * rowSize;
        }

        return 0L;
    }

    // ── Reverse map: file offset → MetadataToken ──────────────────────────────

    /// <summary>
    /// Lazily-built reverse map: raw PE file byte offset → ECMA-335 metadata token (int).
    /// Built once on first call to <see cref="ResolveToken"/>.
    /// </summary>
    private Dictionary<long, int>? _reverseMap;
    private readonly object         _reverseMapLock = new();

    /// <summary>
    /// Resolves a raw PE file byte offset to an ECMA-335 metadata token.
    /// Builds a reverse map on first call (scans all TypeDef, MethodDef, and FieldDef rows).
    /// Returns null when the offset does not correspond to any known token.
    /// Thread-safe: uses double-checked locking for map construction.
    /// </summary>
    public int? ResolveToken(long fileOffset, PEReader peReader, MetadataReader mdReader)
    {
        if (fileOffset <= 0) return null;

        var map = GetOrBuildReverseMap(peReader, mdReader);
        return map.TryGetValue(fileOffset, out var token) ? token : null;
    }

    /// <summary>
    /// Builds (or returns the cached) reverse offset → token map by scanning all
    /// TypeDef, MethodDef, and FieldDef rows.  Method offsets use the IL body RVA
    /// (same as the forward Resolve call); type/field offsets use the metadata row byte.
    /// </summary>
    private Dictionary<long, int> GetOrBuildReverseMap(PEReader peReader, MetadataReader mdReader)
    {
        if (_reverseMap is not null) return _reverseMap;

        lock (_reverseMapLock)
        {
            if (_reverseMap is not null) return _reverseMap;

            var map = new Dictionary<long, int>(1024);

            try
            {
                // MethodDef → IL body offset (most useful for navigation)
                foreach (var handle in mdReader.MethodDefinitions)
                {
                    try
                    {
                        var offset = Resolve(handle, peReader, mdReader);
                        if (offset > 0)
                        {
                            var token = MetadataTokens.GetToken(handle);
                            map.TryAdd(offset, token);
                        }
                    }
                    catch { /* Skip individual failures */ }
                }

                // TypeDef → metadata row offset
                foreach (var handle in mdReader.TypeDefinitions)
                {
                    try
                    {
                        var offset = Resolve(handle, peReader, mdReader);
                        if (offset > 0)
                        {
                            var token = MetadataTokens.GetToken(handle);
                            map.TryAdd(offset, token);
                        }
                    }
                    catch { /* Skip individual failures */ }
                }

                // FieldDef → metadata row offset
                foreach (var handle in mdReader.FieldDefinitions)
                {
                    try
                    {
                        var offset = Resolve(handle, peReader, mdReader);
                        if (offset > 0)
                        {
                            var token = MetadataTokens.GetToken(handle);
                            map.TryAdd(offset, token);
                        }
                    }
                    catch { /* Skip individual failures */ }
                }
            }
            catch { /* Return partial map on unexpected failure */ }

            _reverseMap = map;
            return map;
        }
    }

    // ── RVA → file offset ─────────────────────────────────────────────────────

    /// <summary>
    /// Converts a Relative Virtual Address to a raw file byte offset using PE section headers.
    /// </summary>
    internal static long RvaToFileOffset(int rva, PEReader peReader)
    {
        foreach (var section in peReader.PEHeaders.SectionHeaders)
        {
            var sectionRvaStart = section.VirtualAddress;
            var sectionRvaEnd   = sectionRvaStart + section.VirtualSize;
            if (rva >= sectionRvaStart && rva < sectionRvaEnd)
            {
                return section.PointerToRawData + (rva - sectionRvaStart);
            }
        }
        return 0L;
    }

    // ── Row size computation ──────────────────────────────────────────────────

    /// <summary>
    /// Computes the byte size of one row in the specified table.
    /// Based on ECMA-335 §II.22 table column definitions.
    /// Only tables actually used by the resolver are fully specified;
    /// others fall back to a safe minimum of 2 bytes.
    /// </summary>
    private static int ComputeRowSize(
        TableIndex             table,
        Dictionary<int, int>   rowCounts,
        bool                   stringLarge,
        bool                   guidLarge,
        bool                   blobLarge)
    {
        // Helper: coded-index or simple index width.
        int Idx(TableIndex t)
            => rowCounts.TryGetValue((int)t, out var n) && n > 0xFFFF ? 4 : 2;
        int CodedIdx(params TableIndex[] tables)
        {
            var max = tables.Max(t => rowCounts.TryGetValue((int)t, out var n) ? n : 0);
            return max > 0x3FFF ? 4 : 2;
        }
        int Str() => stringLarge ? 4 : 2;
        int Guid() => guidLarge  ? 4 : 2;
        int Blob() => blobLarge  ? 4 : 2;

        return table switch
        {
            // §II.22.2 Assembly: HashAlgId(4)+MajorVersion(2)+MinorVersion(2)+BuildNumber(2)+RevisionNumber(2)+Flags(4)+PublicKey(blob)+Name(str)+Culture(str)
            TableIndex.Assembly => 4 + 2 + 2 + 2 + 2 + 4 + Blob() + Str() + Str(),

            // §II.22.5 AssemblyRef
            TableIndex.AssemblyRef => 2 + 2 + 2 + 2 + 4 + Blob() + Str() + Str() + Blob(),

            // §II.22.15 Field: Flags(2)+Name(str)+Signature(blob)
            TableIndex.Field => 2 + Str() + Blob(),

            // §II.22.26 MethodDef: RVA(4)+ImplFlags(2)+Flags(2)+Name(str)+Signature(blob)+ParamList(Param idx)
            TableIndex.MethodDef => 4 + 2 + 2 + Str() + Blob() + Idx(TableIndex.Param),

            // §II.22.37 TypeDef: Flags(4)+TypeName(str)+TypeNamespace(str)+Extends(TypeDefOrRef)+FieldList(Field idx)+MethodList(MethodDef idx)
            TableIndex.TypeDef => 4 + Str() + Str()
                                   + CodedIdx(TableIndex.TypeDef, TableIndex.TypeRef, TableIndex.TypeSpec)
                                   + Idx(TableIndex.Field) + Idx(TableIndex.MethodDef),

            // §II.22.38 TypeRef: ResolutionScope(coded)+TypeName(str)+TypeNamespace(str)
            TableIndex.TypeRef => CodedIdx(TableIndex.Module, TableIndex.ModuleRef,
                                           TableIndex.AssemblyRef, TableIndex.TypeRef)
                                  + Str() + Str(),

            // §II.22.39 TypeSpec: Signature(blob)
            TableIndex.TypeSpec => Blob(),

            // §II.22.30 Param: Flags(2)+Sequence(2)+Name(str)
            TableIndex.Param => 2 + 2 + Str(),

            // §II.22.23 InterfaceImpl: Class(TypeDef idx)+Interface(TypeDefOrRef coded)
            TableIndex.InterfaceImpl => Idx(TableIndex.TypeDef)
                                       + CodedIdx(TableIndex.TypeDef, TableIndex.TypeRef, TableIndex.TypeSpec),

            // §II.22.25 MemberRef: Class(MemberRefParent coded)+Name(str)+Signature(blob)
            TableIndex.MemberRef => CodedIdx(TableIndex.TypeDef, TableIndex.TypeRef, TableIndex.ModuleRef,
                                             TableIndex.MethodDef, TableIndex.TypeSpec)
                                   + Str() + Blob(),

            // §II.22.9 Constant: Type(1)+Padding(1)+Parent(HasConstant coded)+Value(blob)
            TableIndex.Constant => 2
                                   + CodedIdx(TableIndex.Field, TableIndex.Param, TableIndex.Property)
                                   + Blob(),

            // §II.22.10 CustomAttribute: Parent(HasCustomAttribute coded)+Type(CustomAttributeType coded)+Value(blob)
            TableIndex.CustomAttribute
                => CodedIdx(TableIndex.MethodDef, TableIndex.Field, TableIndex.TypeRef, TableIndex.TypeDef,
                            TableIndex.Param, TableIndex.InterfaceImpl, TableIndex.MemberRef,
                            TableIndex.Module, TableIndex.DeclSecurity, TableIndex.Property,
                            TableIndex.Event, TableIndex.StandAloneSig, TableIndex.ModuleRef,
                            TableIndex.TypeSpec, TableIndex.Assembly, TableIndex.AssemblyRef,
                            TableIndex.File, TableIndex.ExportedType, TableIndex.ManifestResource,
                            TableIndex.GenericParam, TableIndex.GenericParamConstraint, TableIndex.MethodSpec)
                  + CodedIdx(TableIndex.MethodDef, TableIndex.MemberRef)
                  + Blob(),

            // §II.22.13 Event: EventFlags(2)+Name(str)+EventType(TypeDefOrRef coded)
            TableIndex.Event => 2 + Str()
                                + CodedIdx(TableIndex.TypeDef, TableIndex.TypeRef, TableIndex.TypeSpec),

            // §II.22.33 Property: Flags(2)+Name(str)+Type(blob)
            TableIndex.Property => 2 + Str() + Blob(),

            // §II.22.27 Module: Generation(2)+Name(str)+Mvid(guid)+EncId(guid)+EncBaseId(guid)
            TableIndex.Module => 2 + Str() + Guid() + Guid() + Guid(),

            // §II.22.31 ModuleRef: Name(str)
            TableIndex.ModuleRef => Str(),

            // §II.22.32 NestedClass: NestedClass(TypeDef idx)+EnclosingClass(TypeDef idx)
            TableIndex.NestedClass => Idx(TableIndex.TypeDef) + Idx(TableIndex.TypeDef),

            // §II.22.20 GenericParam: Number(2)+Flags(2)+Owner(TypeOrMethodDef coded)+Name(str)
            TableIndex.GenericParam => 2 + 2
                                      + CodedIdx(TableIndex.TypeDef, TableIndex.MethodDef)
                                      + Str(),

            // §II.22.29 MethodSpec: Method(MethodDefOrRef coded)+Instantiation(blob)
            TableIndex.MethodSpec => CodedIdx(TableIndex.MethodDef, TableIndex.MemberRef) + Blob(),

            // §II.22.21 GenericParamConstraint: Owner(GenericParam idx)+Constraint(TypeDefOrRef coded)
            TableIndex.GenericParamConstraint => Idx(TableIndex.GenericParam)
                                                + CodedIdx(TableIndex.TypeDef, TableIndex.TypeRef, TableIndex.TypeSpec),

            // §II.22.11 DeclSecurity: Action(2)+Parent(HasDeclSecurity coded)+PermissionSet(blob)
            TableIndex.DeclSecurity => 2
                                      + CodedIdx(TableIndex.TypeDef, TableIndex.MethodDef, TableIndex.Assembly)
                                      + Blob(),

            // §II.22.22 ImplMap: MappingFlags(2)+MemberForwarded(MemberForwarded coded)+ImportName(str)+ImportScope(ModuleRef idx)
            TableIndex.ImplMap => 2
                                 + CodedIdx(TableIndex.Field, TableIndex.MethodDef)
                                 + Str() + Idx(TableIndex.ModuleRef),

            // §II.22.16 FieldLayout: Offset(4)+Field(Field idx)
            TableIndex.FieldLayout => 4 + Idx(TableIndex.Field),

            // §II.22.17 FieldMarshal: Parent(HasFieldMarshal coded)+NativeType(blob)
            TableIndex.FieldMarshal => CodedIdx(TableIndex.Field, TableIndex.Param) + Blob(),

            // §II.22.18 FieldRVA: RVA(4)+Field(Field idx)
            TableIndex.FieldRva => 4 + Idx(TableIndex.Field),

            // §II.22.36 StandAloneSig: Signature(blob)
            TableIndex.StandAloneSig => Blob(),

            // §II.22.28 MethodImpl: Class(TypeDef idx)+MethodBody(MethodDefOrRef coded)+MethodDeclaration(MethodDefOrRef coded)
            TableIndex.MethodImpl => Idx(TableIndex.TypeDef)
                                    + CodedIdx(TableIndex.MethodDef, TableIndex.MemberRef)
                                    + CodedIdx(TableIndex.MethodDef, TableIndex.MemberRef),

            // §II.22.24 ManifestResource: Offset(4)+Flags(4)+Name(str)+Implementation(Implementation coded)
            TableIndex.ManifestResource => 4 + 4 + Str()
                                          + CodedIdx(TableIndex.File, TableIndex.AssemblyRef, TableIndex.ExportedType),

            // §II.22.14 ExportedType: Flags(4)+TypeDefId(4)+TypeName(str)+TypeNamespace(str)+Implementation(Implementation coded)
            TableIndex.ExportedType => 4 + 4 + Str() + Str()
                                      + CodedIdx(TableIndex.File, TableIndex.AssemblyRef, TableIndex.ExportedType),

            // §II.22.19 File: Flags(4)+Name(str)+HashValue(blob)
            TableIndex.File => 4 + Str() + Blob(),

            // §II.22.34 PropertyMap / §II.22.12 EventMap: just two indexes
            TableIndex.PropertyMap => Idx(TableIndex.TypeDef) + Idx(TableIndex.Property),
            TableIndex.EventMap    => Idx(TableIndex.TypeDef) + Idx(TableIndex.Event),

            // §II.22.35 MethodSemantics: Semantics(2)+Method(MethodDef idx)+Association(HasSemantics coded)
            TableIndex.MethodSemantics => 2 + Idx(TableIndex.MethodDef)
                                         + CodedIdx(TableIndex.Event, TableIndex.Property),

            // §II.22.7 ClassLayout: PackingSize(2)+ClassSize(4)+Parent(TypeDef idx)
            TableIndex.ClassLayout => 2 + 4 + Idx(TableIndex.TypeDef),

            // Unknown / rare tables: minimum 2 bytes (avoids crash on unseen tables)
            _ => 2
        };
    }

    // ── Low-level binary readers ──────────────────────────────────────────────

    private static uint ReadUInt32(ReadOnlySpan<byte> data, int offset)
        => (uint)(data[offset] | (data[offset + 1] << 8) | (data[offset + 2] << 16) | (data[offset + 3] << 24));

    private static ushort ReadUInt16(ReadOnlySpan<byte> data, int offset)
        => (ushort)(data[offset] | (data[offset + 1] << 8));

    private static ulong ReadUInt64(ReadOnlySpan<byte> data, int offset)
        => (ulong)ReadUInt32(data, offset) | ((ulong)ReadUInt32(data, offset + 4) << 32);
}
