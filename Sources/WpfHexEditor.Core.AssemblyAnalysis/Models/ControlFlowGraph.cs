// ==========================================================
// Project: WpfHexEditor.Core.AssemblyAnalysis
// File: Models/ControlFlowGraph.cs
// Author: Derek Tremblay
// Created: 2026-03-16
// License: GNU Affero General Public License v3.0 (AGPL-3.0)
// Description:
//     Immutable model for a method's control-flow graph (CFG).
//     Produced by CfgBuilder from a MethodBodyBlock.
//     Consumed by CfgCanvas (WPF) for visual rendering.
//
// Architecture Notes:
//     Pattern: Immutable data model (record types).
//     BasicBlock.Successors contains the StartOffset of each successor block.
//     BlockKind.Entry is assigned to the block starting at IL offset 0.
//     BackEdges (loops) are represented normally; layout layer handles them.
// ==========================================================

namespace WpfHexEditor.Core.AssemblyAnalysis.Models;

/// <summary>Classifies the semantic role of a basic block in the CFG.</summary>
public enum BlockKind
{
    /// <summary>The method entry point (IL_0000).</summary>
    Entry,

    /// <summary>Ends with <c>ret</c> — normal exit.</summary>
    Return,

    /// <summary>Ends with <c>throw</c> or <c>rethrow</c> — exception throw exit.</summary>
    Throw,

    /// <summary>Exception handler entry (catch, finally, filter, fault).</summary>
    ExceptionHandler,

    /// <summary>All other blocks.</summary>
    Normal
}

/// <summary>
/// A single basic block: a maximal sequence of instructions
/// with no internal branches or branch targets.
/// </summary>
public sealed record BasicBlock(
    /// <summary>IL offset of the first instruction in this block.</summary>
    int StartOffset,

    /// <summary>IL offset immediately after the last instruction (exclusive end).</summary>
    int EndOffset,

    /// <summary>Semantic classification of this block.</summary>
    BlockKind Kind,

    /// <summary>
    /// Formatted IL lines for each instruction in this block.
    /// Used by <c>CfgCanvas</c> to populate the block's text area.
    /// </summary>
    IReadOnlyList<string> InstructionLines,

    /// <summary>
    /// Start offsets of successor blocks.
    /// Conditional branches have two successors (taken, fall-through).
    /// Terminators (ret/throw) have zero successors.
    /// </summary>
    IReadOnlyList<int> Successors);

/// <summary>
/// The complete control-flow graph for a single method body.
/// </summary>
public sealed record ControlFlowGraph(
    /// <summary>All basic blocks in the method, ordered by <see cref="BasicBlock.StartOffset"/>.</summary>
    IReadOnlyList<BasicBlock> Blocks,

    /// <summary>The entry block (always the first block, starting at IL offset 0).</summary>
    BasicBlock Entry);
