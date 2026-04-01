// ==========================================================
// Project: WpfHexEditor.Core.AssemblyAnalysis
// File: Services/XRefService.cs
// Author: Derek Tremblay
// Created: 2026-03-16
// License: GNU Affero General Public License v3.0 (AGPL-3.0)
// Description:
//     BCL-only cross-reference scanner for .NET assemblies.
//     Performs a full IL scan across all method bodies to find callers,
//     callees, field reads, and field writes for a given target member.
//
// Architecture Notes:
//     Pattern: Service (stateless static API).
//     Scans every method body in the assembly using BCL PEReader + BlobReader.
//     call/callvirt/newobj → CalledBy when token matches target.
//     ldfld/stfld/ldsfld/stsfld → FieldReads / FieldWrites.
//     Calls from the target method itself → Calls list.
//     Token resolution: MethodDefinition or MemberReference tokens.
//     BCL-only: no NuGet required.
// ==========================================================

using System.IO;
using System.Reflection.Metadata;
using System.Reflection.Metadata.Ecma335;
using System.Reflection.PortableExecutable;
using WpfHexEditor.Core.AssemblyAnalysis.Models;

namespace WpfHexEditor.Core.AssemblyAnalysis.Services;

/// <summary>
/// Scans a .NET assembly IL to build cross-reference data for a target member.
/// </summary>
public static class XRefService
{
    // ── Public API ────────────────────────────────────────────────────────────

    /// <summary>
    /// Builds an <see cref="XRefResult"/> by scanning every method body in the assembly
    /// file at <paramref name="filePath"/> for references to <paramref name="target"/>.
    /// Heavy — must be called on a background thread.
    /// </summary>
    public static XRefResult BuildXRefs(
        MemberModel    target,
        string         filePath,
        CancellationToken ct = default)
    {
        if (!File.Exists(filePath) || target.MetadataToken == 0)
            return Empty;

        try
        {
            using var stream   = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            using var peReader = new PEReader(stream);
            if (!peReader.HasMetadata) return Empty;

            var mdReader = peReader.GetMetadataReader();
            return ScanAssembly(target, mdReader, peReader, ct);
        }
        catch { return Empty; }
    }

    private static readonly XRefResult Empty =
        new([], [], [], [], []);

    // ── Scanner ───────────────────────────────────────────────────────────────

    private static XRefResult ScanAssembly(
        MemberModel    target,
        MetadataReader mdReader,
        PEReader       peReader,
        CancellationToken ct)
    {
        var calledBy    = new List<XRefEntry>();
        var calls       = new List<XRefEntry>();
        var fieldReads  = new List<XRefEntry>();
        var fieldWrites = new List<XRefEntry>();

        var isMethod = target.Kind == MemberKind.Method;
        var isField  = target.Kind == MemberKind.Field;
        var targetToken = target.MetadataToken;

        // Find the declaring type for "Calls" scanning (scan the target method's own body)
        int? targetRva = null;
        if (isMethod)
        {
            try
            {
                var targetHandle = MetadataTokens.MethodDefinitionHandle(targetToken & 0x00FFFFFF);
                var targetDef    = mdReader.GetMethodDefinition(targetHandle);
                targetRva        = targetDef.RelativeVirtualAddress;
            }
            catch { /* Non-fatal */ }
        }

        // Iterate every method definition in the assembly
        foreach (var typeHandle in mdReader.TypeDefinitions)
        {
            ct.ThrowIfCancellationRequested();

            var typeDef  = mdReader.GetTypeDefinition(typeHandle);
            var typeName = GetFullTypeName(typeDef, mdReader);
            var typeToken = MetadataTokens.GetToken(typeHandle);

            foreach (var methodHandle in typeDef.GetMethods())
            {
                ct.ThrowIfCancellationRequested();

                var methodDef = mdReader.GetMethodDefinition(methodHandle);
                var rva       = methodDef.RelativeVirtualAddress;
                if (rva == 0) continue;

                try
                {
                    var body   = peReader.GetMethodBody(rva);
                    var reader = body.GetILReader();

                    var isTargetMethod = MetadataTokens.GetToken(methodHandle) == targetToken;
                    var methodSig      = GetMethodSignatureText(methodDef, mdReader);
                    var methodToken    = MetadataTokens.GetToken(methodHandle);

                    while (reader.RemainingBytes > 0)
                    {
                        var opCode = ReadOpCode(ref reader);

                        switch (opCode)
                        {
                            // Method call opcodes
                            case ILOpCode.Call:
                            case ILOpCode.Callvirt:
                            case ILOpCode.Newobj:
                            case ILOpCode.Ldftn:
                            case ILOpCode.Ldvirtftn:
                            {
                                var token = reader.ReadInt32();
                                var resolvedToken = ResolveMethodToken(token, mdReader);

                                if (isMethod)
                                {
                                    if (resolvedToken == targetToken)
                                    {
                                        // This method calls our target
                                        if (!isTargetMethod)
                                            calledBy.Add(new XRefEntry(typeName, methodSig, methodToken, typeToken));
                                    }
                                    else if (isTargetMethod)
                                    {
                                        // Our target calls this method
                                        var calleeSig = TryGetSignatureForToken(token, mdReader);
                                        var calleeOwner = TryGetOwnerTypeToken(token, mdReader);
                                        calls.Add(new XRefEntry(
                                            TryGetOwnerTypeName(token, mdReader),
                                            calleeSig,
                                            token,
                                            calleeOwner));
                                    }
                                }
                                break;
                            }

                            // Field read opcodes
                            case ILOpCode.Ldfld:
                            case ILOpCode.Ldsfld:
                            case ILOpCode.Ldflda:
                            case ILOpCode.Ldsflda:
                            {
                                var token = reader.ReadInt32();
                                if (isField && ResolveFieldToken(token, mdReader) == targetToken)
                                    fieldReads.Add(new XRefEntry(typeName, methodSig, methodToken, typeToken));
                                break;
                            }

                            // Field write opcodes
                            case ILOpCode.Stfld:
                            case ILOpCode.Stsfld:
                            {
                                var token = reader.ReadInt32();
                                if (isField && ResolveFieldToken(token, mdReader) == targetToken)
                                    fieldWrites.Add(new XRefEntry(typeName, methodSig, methodToken, typeToken));
                                break;
                            }

                            default:
                                SkipOperand(ref reader, opCode);
                                break;
                        }
                    }
                }
                catch { /* Non-fatal — skip broken method body */ }
            }
        }

        // Deduplicate
        return new XRefResult(
            Deduplicate(calledBy),
            Deduplicate(calls),
            Deduplicate(fieldReads),
            Deduplicate(fieldWrites),
            []);
    }

    // ── Token resolution helpers ──────────────────────────────────────────────

    private static int ResolveMethodToken(int token, MetadataReader mdReader)
    {
        try
        {
            var handle = MetadataTokens.EntityHandle(token);
            if (handle.Kind == HandleKind.MethodSpecification)
            {
                var spec = mdReader.GetMethodSpecification((MethodSpecificationHandle)handle);
                return MetadataTokens.GetToken(spec.Method);
            }
            return token;
        }
        catch { return token; }
    }

    private static int ResolveFieldToken(int token, MetadataReader mdReader)
    {
        try
        {
            var handle = MetadataTokens.EntityHandle(token);
            if (handle.Kind == HandleKind.MemberReference)
            {
                // MemberRef tokens can't be directly compared to FieldDef tokens in general,
                // but for within-assembly refs the MemberDef token is what we have.
                return token;
            }
            return token;
        }
        catch { return token; }
    }

    private static string TryGetSignatureForToken(int token, MetadataReader mdReader)
    {
        try
        {
            var handle = MetadataTokens.EntityHandle(token);
            return handle.Kind switch
            {
                HandleKind.MethodDefinition =>
                    mdReader.GetString(mdReader.GetMethodDefinition((MethodDefinitionHandle)handle).Name),
                HandleKind.MemberReference =>
                    mdReader.GetString(mdReader.GetMemberReference((MemberReferenceHandle)handle).Name),
                _ => $"0x{token:X8}"
            };
        }
        catch { return $"0x{token:X8}"; }
    }

    private static string TryGetOwnerTypeName(int token, MetadataReader mdReader)
    {
        try
        {
            var handle = MetadataTokens.EntityHandle(token);
            if (handle.Kind == HandleKind.MemberReference)
            {
                var mref   = mdReader.GetMemberReference((MemberReferenceHandle)handle);
                var parent = mref.Parent;
                if (parent.Kind == HandleKind.TypeReference)
                    return mdReader.GetString(mdReader.GetTypeReference((TypeReferenceHandle)parent).Name);
                if (parent.Kind == HandleKind.TypeDefinition)
                    return mdReader.GetString(mdReader.GetTypeDefinition((TypeDefinitionHandle)parent).Name);
            }
            if (handle.Kind == HandleKind.MethodDefinition)
            {
                var mdef = mdReader.GetMethodDefinition((MethodDefinitionHandle)handle);
                return mdReader.GetString(mdReader.GetTypeDefinition(mdef.GetDeclaringType()).Name);
            }
        }
        catch { /* Non-fatal */ }
        return "?";
    }

    private static int TryGetOwnerTypeToken(int token, MetadataReader mdReader)
    {
        try
        {
            var handle = MetadataTokens.EntityHandle(token);
            if (handle.Kind == HandleKind.MethodDefinition)
            {
                var mdef  = mdReader.GetMethodDefinition((MethodDefinitionHandle)handle);
                return MetadataTokens.GetToken(mdef.GetDeclaringType());
            }
        }
        catch { /* Non-fatal */ }
        return 0;
    }

    // ── Member/type name helpers ──────────────────────────────────────────────

    private static string GetFullTypeName(TypeDefinition typeDef, MetadataReader mdReader)
    {
        var ns   = mdReader.GetString(typeDef.Namespace);
        var name = mdReader.GetString(typeDef.Name);
        return string.IsNullOrEmpty(ns) ? name : $"{ns}.{name}";
    }

    private static string GetMethodSignatureText(MethodDefinition mDef, MetadataReader mdReader)
    {
        try { return mdReader.GetString(mDef.Name); }
        catch { return "?"; }
    }

    // ── IL helpers ────────────────────────────────────────────────────────────

    private static ILOpCode ReadOpCode(ref BlobReader reader)
    {
        var b = reader.ReadByte();
        return b == 0xFE ? (ILOpCode)(0xFE00 | reader.ReadByte()) : (ILOpCode)b;
    }

    private static void SkipOperand(ref BlobReader reader, ILOpCode opCode)
    {
        switch (opCode)
        {
            // No operand
            case ILOpCode.Nop: case ILOpCode.Break:
            case ILOpCode.Ldarg_0: case ILOpCode.Ldarg_1: case ILOpCode.Ldarg_2: case ILOpCode.Ldarg_3:
            case ILOpCode.Ldloc_0: case ILOpCode.Ldloc_1: case ILOpCode.Ldloc_2: case ILOpCode.Ldloc_3:
            case ILOpCode.Stloc_0: case ILOpCode.Stloc_1: case ILOpCode.Stloc_2: case ILOpCode.Stloc_3:
            case ILOpCode.Ldnull: case ILOpCode.Ldc_i4_m1: case ILOpCode.Ldc_i4_0: case ILOpCode.Ldc_i4_1:
            case ILOpCode.Ldc_i4_2: case ILOpCode.Ldc_i4_3: case ILOpCode.Ldc_i4_4: case ILOpCode.Ldc_i4_5:
            case ILOpCode.Ldc_i4_6: case ILOpCode.Ldc_i4_7: case ILOpCode.Ldc_i4_8:
            case ILOpCode.Dup: case ILOpCode.Pop: case ILOpCode.Ret:
            case ILOpCode.Add: case ILOpCode.Sub: case ILOpCode.Mul: case ILOpCode.Div: case ILOpCode.Rem:
            case ILOpCode.And: case ILOpCode.Or: case ILOpCode.Xor: case ILOpCode.Shl:
            case ILOpCode.Shr: case ILOpCode.Shr_un: case ILOpCode.Neg: case ILOpCode.Not:
            case ILOpCode.Conv_i1: case ILOpCode.Conv_i2: case ILOpCode.Conv_i4: case ILOpCode.Conv_i8:
            case ILOpCode.Conv_r4: case ILOpCode.Conv_r8: case ILOpCode.Conv_u4: case ILOpCode.Conv_u8:
            case ILOpCode.Conv_r_un: case ILOpCode.Throw: case ILOpCode.Rethrow: case ILOpCode.Ldlen:
            case ILOpCode.Ldelem_i1: case ILOpCode.Ldelem_u1: case ILOpCode.Ldelem_i2: case ILOpCode.Ldelem_u2:
            case ILOpCode.Ldelem_i4: case ILOpCode.Ldelem_u4: case ILOpCode.Ldelem_i8: case ILOpCode.Ldelem_i:
            case ILOpCode.Ldelem_r4: case ILOpCode.Ldelem_r8: case ILOpCode.Ldelem_ref:
            case ILOpCode.Stelem_i: case ILOpCode.Stelem_i1: case ILOpCode.Stelem_i2: case ILOpCode.Stelem_i4:
            case ILOpCode.Stelem_i8: case ILOpCode.Stelem_r4: case ILOpCode.Stelem_r8: case ILOpCode.Stelem_ref:
            case ILOpCode.Endfilter: case ILOpCode.Endfinally:
            case ILOpCode.Ceq: case ILOpCode.Cgt: case ILOpCode.Cgt_un: case ILOpCode.Clt: case ILOpCode.Clt_un:
            case ILOpCode.Volatile: case ILOpCode.Tail: case ILOpCode.Readonly:
                return;

            // 1-byte operand
            case ILOpCode.Ldc_i4_s:
            case ILOpCode.Ldloc_s: case ILOpCode.Stloc_s:
            case ILOpCode.Ldarg_s: case ILOpCode.Starg_s: case ILOpCode.Ldarga_s:
                reader.ReadByte(); return;

            // 4-byte operand
            case ILOpCode.Ldc_i4:
            case ILOpCode.Ldarg: case ILOpCode.Starg: case ILOpCode.Ldarga:
            case ILOpCode.Ldloc: case ILOpCode.Stloc: case ILOpCode.Ldloca:
            case ILOpCode.Br_s: case ILOpCode.Brtrue_s: case ILOpCode.Brfalse_s:
            case ILOpCode.Beq_s: case ILOpCode.Bge_s: case ILOpCode.Bgt_s:
            case ILOpCode.Ble_s: case ILOpCode.Blt_s:
            case ILOpCode.Bne_un_s: case ILOpCode.Bge_un_s: case ILOpCode.Bgt_un_s:
            case ILOpCode.Ble_un_s: case ILOpCode.Blt_un_s: case ILOpCode.Leave_s:
                reader.ReadByte(); return; // short branch: 1 byte

            case ILOpCode.Br: case ILOpCode.Brtrue: case ILOpCode.Brfalse:
            case ILOpCode.Beq: case ILOpCode.Bge: case ILOpCode.Bgt:
            case ILOpCode.Ble: case ILOpCode.Blt:
            case ILOpCode.Bne_un: case ILOpCode.Bge_un: case ILOpCode.Bgt_un:
            case ILOpCode.Ble_un: case ILOpCode.Blt_un: case ILOpCode.Leave:
            case ILOpCode.Call: case ILOpCode.Callvirt: case ILOpCode.Newobj:
            case ILOpCode.Ldftn: case ILOpCode.Ldvirtftn: case ILOpCode.Jmp: case ILOpCode.Calli:
            case ILOpCode.Ldtoken: case ILOpCode.Initobj: case ILOpCode.Constrained:
            case ILOpCode.Newarr: case ILOpCode.Isinst: case ILOpCode.Castclass:
            case ILOpCode.Box: case ILOpCode.Unbox: case ILOpCode.Unbox_any:
            case ILOpCode.Ldelema: case ILOpCode.Ldobj: case ILOpCode.Stobj: case ILOpCode.Cpobj:
            case ILOpCode.Sizeof: case ILOpCode.Mkrefany: case ILOpCode.Refanyval:
            case ILOpCode.Ldfld: case ILOpCode.Ldflda: case ILOpCode.Stfld:
            case ILOpCode.Ldsfld: case ILOpCode.Ldsflda: case ILOpCode.Stsfld:
            case ILOpCode.Ldstr:
                reader.ReadInt32(); return;

            case ILOpCode.Ldc_i8:
                reader.ReadInt64(); return;
            case ILOpCode.Ldc_r4:
                reader.ReadSingle(); return;
            case ILOpCode.Ldc_r8:
                reader.ReadDouble(); return;

            case ILOpCode.Switch:
            {
                var n = reader.ReadUInt32();
                for (var i = 0; i < n; i++) reader.ReadInt32();
                return;
            }

            default: return;
        }
    }

    // ── Deduplication ─────────────────────────────────────────────────────────

    private static IReadOnlyList<XRefEntry> Deduplicate(List<XRefEntry> entries)
    {
        var seen   = new HashSet<int>();
        var result = new List<XRefEntry>(entries.Count);
        foreach (var e in entries)
        {
            if (seen.Add(e.MetadataToken))
                result.Add(e);
        }
        return result;
    }
}
