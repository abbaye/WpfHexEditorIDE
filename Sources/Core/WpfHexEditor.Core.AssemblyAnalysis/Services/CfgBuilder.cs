// ==========================================================
// Project: WpfHexEditor.Core.AssemblyAnalysis
// File: Services/CfgBuilder.cs
// Author: Derek Tremblay
// Created: 2026-03-16
// License: GNU Affero General Public License v3.0 (AGPL-3.0)
// Description:
//     BCL-only Control Flow Graph builder for .NET method bodies.
//     Constructs a ControlFlowGraph from a MethodBodyBlock + MetadataReader
//     using a two-pass algorithm: leader identification then block construction.
//
// Architecture Notes:
//     Pattern: Factory (static BuildCfg method).
//     Pass 1 — instruction decode: walks the IL byte stream and records
//       (offset, endOffset, opcode, kind, targets) for every instruction.
//     Pass 2 — leader set: entry + all branch targets + SEH starts +
//       instructions immediately after branches.
//     Pass 3 — block construction: groups instructions into basic blocks
//       delimited by leaders; classifies each block and computes successors.
//     Handles loops (back edges) naturally — BFS in CfgCanvas assigns layers.
//     BCL-only: uses System.Reflection.Metadata; no NuGet required.
// ==========================================================

using System.Collections.Immutable;
using System.Reflection.Metadata;
using System.Reflection.Metadata.Ecma335;
using System.Reflection.PortableExecutable;
using System.Text;
using WpfHexEditor.Core.AssemblyAnalysis.Models;

namespace WpfHexEditor.Core.AssemblyAnalysis.Services;

/// <summary>
/// Builds a <see cref="ControlFlowGraph"/> from a method body using BCL-only metadata APIs.
/// </summary>
public static class CfgBuilder
{
    // ── Public API ────────────────────────────────────────────────────────────

    /// <summary>
    /// Builds the CFG for the method at the given RVA.
    /// Returns <c>null</c> when the method has no body (abstract, extern, native).
    /// </summary>
    public static ControlFlowGraph? BuildCfg(
        MethodDefinition methodDef,
        MetadataReader   mdReader,
        PEReader         peReader)
    {
        var rva = methodDef.RelativeVirtualAddress;
        if (rva == 0) return null;

        try
        {
            var body = peReader.GetMethodBody(rva);
            return BuildFromBody(body, mdReader);
        }
        catch { return null; }
    }

    // ── Core algorithm ────────────────────────────────────────────────────────

    private static ControlFlowGraph BuildFromBody(MethodBodyBlock body, MetadataReader mdReader)
    {
        // Pass 1 — decode all instructions
        var instrs = DecodeInstructions(body, mdReader);
        if (instrs.Count == 0) return EmptyGraph();

        // Pass 2 — compute leader set
        var leaders = ComputeLeaders(body, instrs);

        // Pass 3 — build basic blocks
        var blocks  = BuildBlocks(instrs, leaders, body.ExceptionRegions);

        if (blocks.Count == 0) return EmptyGraph();

        var entry = blocks[0]; // blocks are sorted by StartOffset
        return new ControlFlowGraph(blocks, entry);
    }

    // ── Pass 1: instruction decode ────────────────────────────────────────────

    private enum InstrKind { Normal, ConditionalBranch, UnconditionalBranch, Switch, Terminator }

    private sealed class InstrInfo
    {
        public int          Offset     { get; init; }
        public int          EndOffset  { get; init; }
        public ILOpCode     OpCode     { get; init; }
        public string       Display    { get; init; } = string.Empty;
        public InstrKind    Kind       { get; init; }
        public int[]        Targets    { get; init; } = [];
    }

    private static List<InstrInfo> DecodeInstructions(MethodBodyBlock body, MetadataReader mdReader)
    {
        var result = new List<InstrInfo>();
        var reader = body.GetILReader();

        while (reader.RemainingBytes > 0)
        {
            var offset = reader.Offset;
            var opCode = ReadOpCode(ref reader);
            var sb     = new StringBuilder();
            sb.Append($"IL_{offset:X4}  {opCode.ToString().ToLowerInvariant().Replace('_', '.')}");

            var (kind, targets) = AppendOperandAndClassify(ref reader, opCode, offset, mdReader, sb);

            result.Add(new InstrInfo
            {
                Offset    = offset,
                EndOffset = reader.Offset,
                OpCode    = opCode,
                Display   = sb.ToString(),
                Kind      = kind,
                Targets   = targets
            });
        }

        return result;
    }

    private static (InstrKind kind, int[] targets) AppendOperandAndClassify(
        ref BlobReader reader,
        ILOpCode       opCode,
        int            instrOffset,
        MetadataReader mdReader,
        StringBuilder  sb)
    {
        switch (opCode)
        {
            // Short branch
            case ILOpCode.Br_s:     case ILOpCode.Leave_s:
            {
                var delta  = reader.ReadSByte();
                var target = reader.Offset + delta;
                sb.Append($" IL_{target:X4}");
                return (opCode == ILOpCode.Leave_s ? InstrKind.UnconditionalBranch : InstrKind.UnconditionalBranch,
                        [target]);
            }

            case ILOpCode.Brtrue_s:  case ILOpCode.Brfalse_s:
            case ILOpCode.Beq_s:     case ILOpCode.Bge_s:    case ILOpCode.Bgt_s:
            case ILOpCode.Ble_s:     case ILOpCode.Blt_s:
            case ILOpCode.Bne_un_s:  case ILOpCode.Bge_un_s: case ILOpCode.Bgt_un_s:
            case ILOpCode.Ble_un_s:  case ILOpCode.Blt_un_s:
            {
                var delta  = reader.ReadSByte();
                var target = reader.Offset + delta;
                sb.Append($" IL_{target:X4}");
                return (InstrKind.ConditionalBranch, [target]);
            }

            // Long unconditional branch
            case ILOpCode.Br:     case ILOpCode.Leave:
            {
                var delta  = reader.ReadInt32();
                var target = reader.Offset + delta;
                sb.Append($" IL_{target:X4}");
                return (InstrKind.UnconditionalBranch, [target]);
            }

            // Long conditional branches
            case ILOpCode.Brtrue:  case ILOpCode.Brfalse:
            case ILOpCode.Beq:     case ILOpCode.Bge:     case ILOpCode.Bgt:
            case ILOpCode.Ble:     case ILOpCode.Blt:
            case ILOpCode.Bne_un:  case ILOpCode.Bge_un:  case ILOpCode.Bgt_un:
            case ILOpCode.Ble_un:  case ILOpCode.Blt_un:
            {
                var delta  = reader.ReadInt32();
                var target = reader.Offset + delta;
                sb.Append($" IL_{target:X4}");
                return (InstrKind.ConditionalBranch, [target]);
            }

            // Switch
            case ILOpCode.Switch:
            {
                var n          = reader.ReadUInt32();
                var baseOffset = (int)(reader.Offset + n * 4);
                var targets    = new int[n];
                for (var i = 0; i < n; i++) targets[i] = baseOffset + reader.ReadInt32();
                sb.Append($" ({n} cases)");
                return (InstrKind.Switch, targets);
            }

            // Terminators
            case ILOpCode.Ret:
            case ILOpCode.Throw:
            case ILOpCode.Rethrow:
            case ILOpCode.Jmp:
                SkipInlineToken(ref reader, opCode);
                return (InstrKind.Terminator, []);

            // Method calls / token instructions — consume token, display name
            case ILOpCode.Call:    case ILOpCode.Callvirt:  case ILOpCode.Newobj:
            case ILOpCode.Ldftn:   case ILOpCode.Ldvirtftn: case ILOpCode.Calli:
            {
                var token = reader.ReadInt32();
                sb.Append($" {ResolveTokenName(token, mdReader)}");
                return (InstrKind.Normal, []);
            }

            case ILOpCode.Ldtoken: case ILOpCode.Initobj:   case ILOpCode.Constrained:
            case ILOpCode.Newarr:  case ILOpCode.Isinst:    case ILOpCode.Castclass:
            case ILOpCode.Box:     case ILOpCode.Unbox:      case ILOpCode.Unbox_any:
            case ILOpCode.Ldelema: case ILOpCode.Ldobj:      case ILOpCode.Stobj:
            case ILOpCode.Cpobj:   case ILOpCode.Sizeof:
            case ILOpCode.Mkrefany: case ILOpCode.Refanyval:
            {
                var token = reader.ReadInt32();
                sb.Append($" {ResolveTokenName(token, mdReader)}");
                return (InstrKind.Normal, []);
            }

            case ILOpCode.Ldfld:   case ILOpCode.Ldflda:
            case ILOpCode.Stfld:   case ILOpCode.Ldsfld:
            case ILOpCode.Ldsflda: case ILOpCode.Stsfld:
            {
                var token = reader.ReadInt32();
                sb.Append($" {ResolveTokenName(token, mdReader)}");
                return (InstrKind.Normal, []);
            }

            case ILOpCode.Ldstr:
            {
                var token = reader.ReadInt32();
                if ((token >> 24) == 0x70)
                {
                    try
                    {
                        var s = mdReader.GetUserString(MetadataTokens.UserStringHandle(token & 0x00FFFFFF));
                        sb.Append($" \"{(s.Length > 20 ? s[..17] + "..." : s)}\"");
                    }
                    catch { sb.Append($" 0x{token:X8}"); }
                }
                return (InstrKind.Normal, []);
            }

            // Inline integers / floats — just consume and show value
            case ILOpCode.Ldc_i4_s:
                sb.Append($" {reader.ReadSByte()}"); return (InstrKind.Normal, []);
            case ILOpCode.Ldc_i4:
                sb.Append($" {reader.ReadInt32()}"); return (InstrKind.Normal, []);
            case ILOpCode.Ldc_i8:
                sb.Append($" {reader.ReadInt64()}L"); return (InstrKind.Normal, []);
            case ILOpCode.Ldc_r4:
                sb.Append($" {reader.ReadSingle():R}f"); return (InstrKind.Normal, []);
            case ILOpCode.Ldc_r8:
                sb.Append($" {reader.ReadDouble():R}"); return (InstrKind.Normal, []);

            case ILOpCode.Ldarg_s: case ILOpCode.Starg_s: case ILOpCode.Ldarga_s:
            case ILOpCode.Ldloc_s: case ILOpCode.Stloc_s:
                sb.Append($" {reader.ReadByte()}"); return (InstrKind.Normal, []);

            case ILOpCode.Ldarg:   case ILOpCode.Starg:   case ILOpCode.Ldarga:
            case ILOpCode.Ldloc:   case ILOpCode.Stloc:   case ILOpCode.Ldloca:
                sb.Append($" {reader.ReadUInt16()}"); return (InstrKind.Normal, []);

            // No operand opcodes — nothing to consume
            default:
                return (InstrKind.Normal, []);
        }
    }

    // ── Pass 2: leader computation ────────────────────────────────────────────

    private static HashSet<int> ComputeLeaders(
        MethodBodyBlock body, List<InstrInfo> instrs)
    {
        var leaders = new HashSet<int> { 0 }; // entry is always a leader

        // SEH starts are leaders
        foreach (var r in body.ExceptionRegions)
        {
            leaders.Add(r.TryOffset);
            leaders.Add(r.HandlerOffset);
            if (r.Kind == ExceptionRegionKind.Filter)
                leaders.Add(r.FilterOffset);
        }

        // Branch targets and fall-through instructions after branches are leaders
        foreach (var instr in instrs)
        {
            foreach (var t in instr.Targets) leaders.Add(t);

            if (instr.Kind is InstrKind.UnconditionalBranch
                           or InstrKind.ConditionalBranch
                           or InstrKind.Switch)
                leaders.Add(instr.EndOffset); // instruction after branch = new block start
        }

        return leaders;
    }

    // ── Pass 3: block construction ────────────────────────────────────────────

    private static List<BasicBlock> BuildBlocks(
        List<InstrInfo>          instrs,
        HashSet<int>             leaders,
        ImmutableArray<ExceptionRegion> sehRegions)
    {
        if (instrs.Count == 0) return [];

        var sehHandlerOffsets = new HashSet<int>(sehRegions.Select(r => r.HandlerOffset));
        var sortedLeaders     = leaders.OrderBy(l => l).ToList();
        var methodEnd         = instrs[^1].EndOffset;

        // Index instructions by offset for fast lookup
        var instrByOffset = instrs.ToDictionary(i => i.Offset);

        var blocks = new List<BasicBlock>();

        for (var li = 0; li < sortedLeaders.Count; li++)
        {
            var blockStart = sortedLeaders[li];
            var blockEnd   = li + 1 < sortedLeaders.Count ? sortedLeaders[li + 1] : methodEnd;

            // Collect instructions in [blockStart, blockEnd)
            var blockInstrs = instrs
                .Where(i => i.Offset >= blockStart && i.Offset < blockEnd)
                .ToList();

            if (blockInstrs.Count == 0) continue;

            var lastInstr = blockInstrs[^1];

            // Compute successors
            var successors = new List<int>();
            switch (lastInstr.Kind)
            {
                case InstrKind.Terminator:
                    break;
                case InstrKind.UnconditionalBranch:
                    successors.AddRange(lastInstr.Targets);
                    break;
                case InstrKind.ConditionalBranch:
                    successors.AddRange(lastInstr.Targets);
                    successors.Add(lastInstr.EndOffset);   // fall-through
                    break;
                case InstrKind.Switch:
                    successors.AddRange(lastInstr.Targets);
                    successors.Add(lastInstr.EndOffset);   // default fall-through
                    break;
                default:
                    // Normal fall-through to next block
                    if (blockEnd < methodEnd) successors.Add(blockEnd);
                    break;
            }

            // Remove out-of-range successors (e.g. leave targets into other methods)
            successors = successors.Where(s => s >= 0 && s < methodEnd).Distinct().ToList();

            // Classify block kind
            BlockKind kind;
            if (blockStart == 0)
                kind = BlockKind.Entry;
            else if (sehHandlerOffsets.Contains(blockStart))
                kind = BlockKind.ExceptionHandler;
            else if (lastInstr.OpCode is ILOpCode.Ret)
                kind = BlockKind.Return;
            else if (lastInstr.OpCode is ILOpCode.Throw or ILOpCode.Rethrow)
                kind = BlockKind.Throw;
            else
                kind = BlockKind.Normal;

            blocks.Add(new BasicBlock(
                blockStart,
                blockEnd,
                kind,
                blockInstrs.Select(i => i.Display).ToArray(),
                successors));
        }

        return blocks;
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static ILOpCode ReadOpCode(ref BlobReader reader)
    {
        var b = reader.ReadByte();
        return b == 0xFE ? (ILOpCode)(0xFE00 | reader.ReadByte()) : (ILOpCode)b;
    }

    /// <summary>Skips the operand bytes for opcodes that have a 4-byte token but aren't handled above.</summary>
    private static void SkipInlineToken(ref BlobReader reader, ILOpCode opCode)
    {
        if (opCode == ILOpCode.Jmp) reader.ReadInt32();
    }

    private static string ResolveTokenName(int token, MetadataReader mdReader)
    {
        try
        {
            var handle = MetadataTokens.EntityHandle(token);
            switch (handle.Kind)
            {
                case HandleKind.MethodDefinition:
                    return mdReader.GetString(mdReader.GetMethodDefinition((MethodDefinitionHandle)handle).Name);
                case HandleKind.MemberReference:
                    return mdReader.GetString(mdReader.GetMemberReference((MemberReferenceHandle)handle).Name);
                case HandleKind.TypeDefinition:
                    return mdReader.GetString(mdReader.GetTypeDefinition((TypeDefinitionHandle)handle).Name);
                case HandleKind.TypeReference:
                    return mdReader.GetString(mdReader.GetTypeReference((TypeReferenceHandle)handle).Name);
                case HandleKind.MethodSpecification:
                {
                    var spec = mdReader.GetMethodSpecification((MethodSpecificationHandle)handle);
                    return ResolveTokenName(MetadataTokens.GetToken(spec.Method), mdReader);
                }
                default:
                    return $"0x{token:X8}";
            }
        }
        catch { return $"0x{token:X8}"; }
    }

    private static ControlFlowGraph EmptyGraph()
    {
        var entry = new BasicBlock(0, 0, BlockKind.Entry, [], []);
        return new ControlFlowGraph([entry], entry);
    }
}
