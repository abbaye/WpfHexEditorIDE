// ==========================================================
// Project: WpfHexEditor.Core.AssemblyAnalysis
// File: Services/IlTextEmitter.cs
// Author: Derek Tremblay
// Created: 2026-03-08
// Updated: 2026-03-16 — Phase 3: branch target IL_XXXX labels, switch label lists,
//     local variable count, SEH try/catch/finally block headers.
// License: GNU Affero General Public License v3.0 (AGPL-3.0)
// Description:
//     BCL-only IL disassembler. Reads a method body via PEReader,
//     decodes IL instructions using BlobReader + ILOpCode (BCL enums),
//     and resolves operand tokens to human-readable names.
//     No external NuGet dependency.
//
// Architecture Notes:
//     Pattern: Service (stateless).
//     Requires a live PEReader (not disposed) because MethodBodyBlock
//     reads directly from the memory-mapped PE image.
//     Returns empty string for abstract/extern methods (no body).
//     Phase 3 improvements:
//       - Two-pass: pre-scan collects all branch target + SEH handler offsets,
//         used to emit IL_XXXX: labels before targeted instructions.
//       - Branch operands are emitted as IL_XXXX (absolute offset) not raw ints.
//       - Switch targets emitted as (IL_XXXX, IL_YYYY, ...) label list.
//       - Local variable count decoded from LocalSignature blob.
//       - SEH regions emitted as .try/.catch/.finally block headers.
// ==========================================================

using System.Collections.Immutable;
using System.Reflection.Metadata;
using System.Reflection.Metadata.Ecma335;
using System.Reflection.PortableExecutable;
using System.Text;

namespace WpfHexEditor.Core.AssemblyAnalysis.Services;

/// <summary>
/// Disassembles .NET method bodies to IL text using only BCL APIs.
/// </summary>
public sealed class IlTextEmitter
{
    // ── Public API ────────────────────────────────────────────────────────────

    /// <summary>
    /// Returns the IL disassembly of the method identified by <paramref name="methodDef"/>,
    /// or an empty string when the method has no body (abstract, extern, interface member).
    /// </summary>
    public string EmitMethod(
        MethodDefinition methodDef,
        MetadataReader   mdReader,
        PEReader         peReader)
    {
        var rva = methodDef.RelativeVirtualAddress;
        if (rva == 0) return string.Empty;

        try
        {
            var body = peReader.GetMethodBody(rva);
            return DisassembleBody(body, mdReader);
        }
        catch (Exception ex)
        {
            return $"// Error reading IL: {ex.Message}";
        }
    }

    // ── Disassembly core ──────────────────────────────────────────────────────

    private static string DisassembleBody(MethodBodyBlock body, MetadataReader mdReader)
    {
        var sb = new StringBuilder();

        // ── Header ────────────────────────────────────────────────────────────
        sb.AppendLine($"// MaxStack: {body.MaxStack}");
        AppendLocals(body, mdReader, sb);

        // ── SEH regions (try/catch/finally) ───────────────────────────────────
        var sehRegions = body.ExceptionRegions;
        if (sehRegions.Length > 0)
            AppendSehHeader(sehRegions, mdReader, sb);

        // ── Pre-scan: collect all branch targets and SEH handler entry points ─
        var branchTargets = BuildBranchTargetSet(body, sehRegions);

        // ── Instruction loop ──────────────────────────────────────────────────
        var reader = body.GetILReader();
        while (reader.RemainingBytes > 0)
        {
            var offset = reader.Offset;

            // Emit label for any instruction that is a branch target.
            if (branchTargets.Contains(offset))
                sb.AppendLine($"  IL_{offset:X4}:");

            sb.Append($"  IL_{offset:X4}:  ");

            var opCode = ReadOpCode(ref reader);
            sb.Append(opCode.ToString().ToLowerInvariant().Replace('_', '.'));

            AppendOperand(ref reader, opCode, offset, mdReader, sb);
            sb.AppendLine();
        }

        return sb.ToString();
    }

    // ── Locals decoder ────────────────────────────────────────────────────────

    /// <summary>
    /// Decodes the local variable count from the <c>LOCAL_SIG</c> blob
    /// and emits a <c>// Locals: N</c> header comment.
    /// </summary>
    private static void AppendLocals(MethodBodyBlock body, MetadataReader mdReader, StringBuilder sb)
    {
        if (body.LocalSignature.IsNil) return;
        try
        {
            var sig  = mdReader.GetStandaloneSignature(body.LocalSignature);
            var blob = mdReader.GetBlobReader(sig.Signature);
            if (blob.RemainingBytes < 2) return;

            var magic = blob.ReadByte(); // 0x07 = LOCAL_SIG
            if (magic != 0x07) return;

            var count = blob.ReadCompressedInteger();
            sb.AppendLine($"// Locals: {count}");
        }
        catch { /* Non-fatal — skip locals header */ }
    }

    // ── SEH region header ─────────────────────────────────────────────────────

    private static void AppendSehHeader(
        ImmutableArray<ExceptionRegion> regions, MetadataReader mdReader, StringBuilder sb)
    {
        sb.AppendLine("// Exception regions:");
        foreach (var r in regions)
        {
            var kind = r.Kind switch
            {
                ExceptionRegionKind.Catch   => $"catch [{ResolveType(MetadataTokens.GetToken(r.CatchType), mdReader)}]",
                ExceptionRegionKind.Filter  => "filter",
                ExceptionRegionKind.Finally => "finally",
                ExceptionRegionKind.Fault   => "fault",
                _                           => "?"
            };
            sb.AppendLine(
                $"//   .try   IL_{r.TryOffset:X4} to IL_{r.TryOffset + r.TryLength:X4}  " +
                $"{kind}  handler IL_{r.HandlerOffset:X4} to IL_{r.HandlerOffset + r.HandlerLength:X4}");
        }
        sb.AppendLine();
    }

    // ── Branch target pre-scan ────────────────────────────────────────────────

    /// <summary>
    /// Scans the IL byte stream and collects all absolute offsets that are the
    /// destination of a branch or switch instruction, plus all SEH handler entry points.
    /// These are used to emit <c>IL_XXXX:</c> labels on the targeted instructions.
    /// </summary>
    private static HashSet<int> BuildBranchTargetSet(
        MethodBodyBlock body, ImmutableArray<ExceptionRegion> sehRegions)
    {
        var targets = new HashSet<int>();

        // Include all SEH try/handler starts as implicit labels.
        foreach (var r in sehRegions)
        {
            targets.Add(r.TryOffset);
            targets.Add(r.HandlerOffset);
            if (r.Kind == ExceptionRegionKind.Filter)
                targets.Add(r.FilterOffset);
        }

        var reader = body.GetILReader();
        while (reader.RemainingBytes > 0)
        {
            var instrOffset = reader.Offset;
            var opCode      = ReadOpCode(ref reader);
            var instrSize   = reader.Offset - instrOffset; // 1 or 2 bytes

            switch (opCode)
            {
                // Short branch: 1-byte signed relative to end of instruction (instrOffset + instrSize + 1)
                case ILOpCode.Br_s:     case ILOpCode.Brtrue_s:  case ILOpCode.Brfalse_s:
                case ILOpCode.Beq_s:    case ILOpCode.Bge_s:     case ILOpCode.Bgt_s:
                case ILOpCode.Ble_s:    case ILOpCode.Blt_s:
                case ILOpCode.Bne_un_s: case ILOpCode.Bge_un_s:  case ILOpCode.Bgt_un_s:
                case ILOpCode.Ble_un_s: case ILOpCode.Blt_un_s:
                case ILOpCode.Leave_s:
                {
                    var delta  = reader.ReadSByte();
                    targets.Add(reader.Offset + delta);
                    break;
                }

                // Long branch: 4-byte signed relative to end of instruction
                case ILOpCode.Br:     case ILOpCode.Brtrue:  case ILOpCode.Brfalse:
                case ILOpCode.Beq:    case ILOpCode.Bge:     case ILOpCode.Bgt:
                case ILOpCode.Ble:    case ILOpCode.Blt:
                case ILOpCode.Bne_un: case ILOpCode.Bge_un:  case ILOpCode.Bgt_un:
                case ILOpCode.Ble_un: case ILOpCode.Blt_un:
                case ILOpCode.Leave:
                {
                    var delta = reader.ReadInt32();
                    targets.Add(reader.Offset + delta);
                    break;
                }

                // Switch: uint32 n, then n * int32 deltas relative to end of switch instruction
                case ILOpCode.Switch:
                {
                    var n    = reader.ReadUInt32();
                    var baseOffset = (int)(reader.Offset + n * 4);
                    for (var i = 0; i < n; i++)
                    {
                        var delta = reader.ReadInt32();
                        targets.Add(baseOffset + delta);
                    }
                    break;
                }

                default:
                    // Skip over the operand bytes for this opcode.
                    SkipOperand(ref reader, opCode);
                    break;
            }
        }

        return targets;
    }

    // ── Operand skip (pre-scan only) ─────────────────────────────────────────

    private static void SkipOperand(ref BlobReader reader, ILOpCode opCode)
    {
        switch (opCode)
        {
            // No operand
            case ILOpCode.Nop: case ILOpCode.Break:
            case ILOpCode.Ldarg_0: case ILOpCode.Ldarg_1: case ILOpCode.Ldarg_2: case ILOpCode.Ldarg_3:
            case ILOpCode.Ldloc_0: case ILOpCode.Ldloc_1: case ILOpCode.Ldloc_2: case ILOpCode.Ldloc_3:
            case ILOpCode.Stloc_0: case ILOpCode.Stloc_1: case ILOpCode.Stloc_2: case ILOpCode.Stloc_3:
            case ILOpCode.Ldnull:
            case ILOpCode.Ldc_i4_m1: case ILOpCode.Ldc_i4_0: case ILOpCode.Ldc_i4_1:
            case ILOpCode.Ldc_i4_2:  case ILOpCode.Ldc_i4_3: case ILOpCode.Ldc_i4_4:
            case ILOpCode.Ldc_i4_5:  case ILOpCode.Ldc_i4_6: case ILOpCode.Ldc_i4_7:
            case ILOpCode.Ldc_i4_8:  case ILOpCode.Dup: case ILOpCode.Pop: case ILOpCode.Ret:
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
            case ILOpCode.Call:  case ILOpCode.Callvirt: case ILOpCode.Newobj:
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

            // 8-byte operand
            case ILOpCode.Ldc_i8:
                reader.ReadInt64(); return;

            // 4-byte float
            case ILOpCode.Ldc_r4:
                reader.ReadSingle(); return;

            // 8-byte double
            case ILOpCode.Ldc_r8:
                reader.ReadDouble(); return;

            default:
                return;
        }
    }

    // ── Opcode reader ─────────────────────────────────────────────────────────

    private static ILOpCode ReadOpCode(ref BlobReader reader)
    {
        var b = reader.ReadByte();
        if (b == 0xFE)
            return (ILOpCode)(0xFE00 | reader.ReadByte());
        return (ILOpCode)b;
    }

    // ── Operand emitter ───────────────────────────────────────────────────────

    private static void AppendOperand(
        ref BlobReader reader,
        ILOpCode       opCode,
        int            instrOffset,
        MetadataReader mdReader,
        StringBuilder  sb)
    {
        // Compute how many bytes the opcode itself consumed (1 or 2).
        // instrOffset was captured before ReadOpCode; reader.Offset is now past the opcode.
        switch (opCode)
        {
            // No operand
            case ILOpCode.Nop: case ILOpCode.Break:
            case ILOpCode.Ldarg_0: case ILOpCode.Ldarg_1: case ILOpCode.Ldarg_2: case ILOpCode.Ldarg_3:
            case ILOpCode.Ldloc_0: case ILOpCode.Ldloc_1: case ILOpCode.Ldloc_2: case ILOpCode.Ldloc_3:
            case ILOpCode.Stloc_0: case ILOpCode.Stloc_1: case ILOpCode.Stloc_2: case ILOpCode.Stloc_3:
            case ILOpCode.Ldnull:
            case ILOpCode.Ldc_i4_m1: case ILOpCode.Ldc_i4_0: case ILOpCode.Ldc_i4_1:
            case ILOpCode.Ldc_i4_2:  case ILOpCode.Ldc_i4_3: case ILOpCode.Ldc_i4_4:
            case ILOpCode.Ldc_i4_5:  case ILOpCode.Ldc_i4_6: case ILOpCode.Ldc_i4_7:
            case ILOpCode.Ldc_i4_8:  case ILOpCode.Dup: case ILOpCode.Pop: case ILOpCode.Ret:
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

            // Inline int8 — non-branch
            case ILOpCode.Ldc_i4_s:
                sb.Append($" {reader.ReadSByte()}"); return;

            // Short local/arg index (display as int)
            case ILOpCode.Ldloc_s: case ILOpCode.Stloc_s:
            case ILOpCode.Ldarg_s: case ILOpCode.Starg_s: case ILOpCode.Ldarga_s:
                sb.Append($" {reader.ReadByte()}"); return;

            // Short branch — emit IL_XXXX target label
            case ILOpCode.Br_s:     case ILOpCode.Brtrue_s:  case ILOpCode.Brfalse_s:
            case ILOpCode.Beq_s:    case ILOpCode.Bge_s:     case ILOpCode.Bgt_s:
            case ILOpCode.Ble_s:    case ILOpCode.Blt_s:
            case ILOpCode.Bne_un_s: case ILOpCode.Bge_un_s:  case ILOpCode.Bgt_un_s:
            case ILOpCode.Ble_un_s: case ILOpCode.Blt_un_s:
            case ILOpCode.Leave_s:
            {
                var delta  = reader.ReadSByte();
                var target = reader.Offset + delta;  // reader.Offset = after operand
                sb.Append($" IL_{target:X4}");
                return;
            }

            // Long branch — emit IL_XXXX target label
            case ILOpCode.Br:     case ILOpCode.Brtrue:  case ILOpCode.Brfalse:
            case ILOpCode.Beq:    case ILOpCode.Bge:     case ILOpCode.Bgt:
            case ILOpCode.Ble:    case ILOpCode.Blt:
            case ILOpCode.Bne_un: case ILOpCode.Bge_un:  case ILOpCode.Bgt_un:
            case ILOpCode.Ble_un: case ILOpCode.Blt_un:
            case ILOpCode.Leave:
            {
                var delta  = reader.ReadInt32();
                var target = reader.Offset + delta;
                sb.Append($" IL_{target:X4}");
                return;
            }

            // Long local/arg index
            case ILOpCode.Ldloc: case ILOpCode.Stloc: case ILOpCode.Ldloca:
            case ILOpCode.Ldarg: case ILOpCode.Starg: case ILOpCode.Ldarga:
                sb.Append($" {reader.ReadUInt16()}"); return;

            // Inline int32
            case ILOpCode.Ldc_i4:
                sb.Append($" {reader.ReadInt32()}"); return;

            // Inline int64
            case ILOpCode.Ldc_i8:
                sb.Append($" {reader.ReadInt64()}L"); return;

            // Inline float32 / float64
            case ILOpCode.Ldc_r4:
                sb.Append($" {reader.ReadSingle():R}f"); return;
            case ILOpCode.Ldc_r8:
                sb.Append($" {reader.ReadDouble():R}"); return;

            // Inline token — method/member
            case ILOpCode.Call:    case ILOpCode.Callvirt:  case ILOpCode.Newobj:
            case ILOpCode.Ldftn:   case ILOpCode.Ldvirtftn:
            case ILOpCode.Jmp:     case ILOpCode.Calli:
            {
                var token = reader.ReadInt32();
                sb.Append($" {ResolveMethodOrMember(token, mdReader)}");
                return;
            }

            // Inline token — type
            case ILOpCode.Ldtoken:
            case ILOpCode.Initobj:  case ILOpCode.Constrained:
            case ILOpCode.Newarr:   case ILOpCode.Isinst:   case ILOpCode.Castclass:
            case ILOpCode.Box:      case ILOpCode.Unbox:    case ILOpCode.Unbox_any:
            case ILOpCode.Ldelema:  case ILOpCode.Ldobj:    case ILOpCode.Stobj:
            case ILOpCode.Cpobj:    case ILOpCode.Sizeof:
            case ILOpCode.Mkrefany: case ILOpCode.Refanyval:
            {
                var token = reader.ReadInt32();
                sb.Append($" {ResolveType(token, mdReader)}");
                return;
            }

            // Inline token — field
            case ILOpCode.Ldfld:   case ILOpCode.Ldflda:
            case ILOpCode.Stfld:   case ILOpCode.Ldsfld:
            case ILOpCode.Ldsflda: case ILOpCode.Stsfld:
            {
                var token = reader.ReadInt32();
                sb.Append($" {ResolveMethodOrMember(token, mdReader)}");
                return;
            }

            // Inline string token
            case ILOpCode.Ldstr:
            {
                var token = reader.ReadInt32();
                sb.Append($" \"{ResolveString(token, mdReader)}\"");
                return;
            }

            // Switch: uint32 n, then n * int32 deltas — emit (IL_XXXX, IL_YYYY, ...)
            case ILOpCode.Switch:
            {
                var n          = reader.ReadUInt32();
                var baseOffset = (int)(reader.Offset + n * 4);
                var offsets    = new int[n];
                for (var i = 0; i < n; i++) offsets[i] = reader.ReadInt32();

                sb.Append(" (");
                for (var i = 0; i < n; i++)
                {
                    if (i > 0) sb.Append(", ");
                    sb.Append($"IL_{baseOffset + offsets[i]:X4}");
                }
                sb.Append(')');
                return;
            }

            default:
                return;
        }
    }

    // ── Token resolution ──────────────────────────────────────────────────────

    private static string ResolveMethodOrMember(int token, MetadataReader mdReader)
    {
        try
        {
            var handle = MetadataTokens.EntityHandle(token);
            switch (handle.Kind)
            {
                case HandleKind.MethodDefinition:
                    return mdReader.GetString(
                        mdReader.GetMethodDefinition((MethodDefinitionHandle)handle).Name);
                case HandleKind.MemberReference:
                {
                    var mref = mdReader.GetMemberReference((MemberReferenceHandle)handle);
                    return $"{ResolveParentType(mref.Parent, mdReader)}::{mdReader.GetString(mref.Name)}";
                }
                case HandleKind.MethodSpecification:
                {
                    var spec = mdReader.GetMethodSpecification((MethodSpecificationHandle)handle);
                    return ResolveMethodOrMember(MetadataTokens.GetToken(spec.Method), mdReader);
                }
                default:
                    return $"0x{token:X8}";
            }
        }
        catch { return $"0x{token:X8}"; }
    }

    private static string ResolveType(int token, MetadataReader mdReader)
    {
        if (token == 0) return "?";
        try
        {
            var handle = MetadataTokens.EntityHandle(token);
            return handle.Kind switch
            {
                HandleKind.TypeDefinition   =>
                    mdReader.GetString(mdReader.GetTypeDefinition((TypeDefinitionHandle)handle).Name),
                HandleKind.TypeReference    =>
                    mdReader.GetString(mdReader.GetTypeReference((TypeReferenceHandle)handle).Name),
                HandleKind.TypeSpecification => "<TypeSpec>",
                _ => $"0x{token:X8}"
            };
        }
        catch { return $"0x{token:X8}"; }
    }

    private static string ResolveParentType(EntityHandle parent, MetadataReader mdReader)
    {
        try
        {
            return parent.Kind switch
            {
                HandleKind.TypeDefinition =>
                    mdReader.GetString(mdReader.GetTypeDefinition((TypeDefinitionHandle)parent).Name),
                HandleKind.TypeReference  =>
                    mdReader.GetString(mdReader.GetTypeReference((TypeReferenceHandle)parent).Name),
                _ => "?"
            };
        }
        catch { return "?"; }
    }

    private static string ResolveString(int token, MetadataReader mdReader)
    {
        try
        {
            // User-string token: upper byte = 0x70
            if ((token >> 24) == 0x70)
            {
                var us = mdReader.GetUserString(MetadataTokens.UserStringHandle(token & 0x00FFFFFF));
                return us.Length > 60 ? us[..57] + "..." : us;
            }
            return $"0x{token:X8}";
        }
        catch { return "?"; }
    }
}
