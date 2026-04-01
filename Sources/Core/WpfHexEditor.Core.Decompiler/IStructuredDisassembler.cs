//////////////////////////////////////////////
// Project: WpfHexEditor.Core.Decompiler
// File: IStructuredDisassembler.cs
// Description:
//     Extended decompiler contract that returns structured DisassemblyLine
//     records instead of raw text.  DisassemblyViewer uses this interface
//     to drive the GlyphRun canvas renderer with syntax colouring and
//     click-to-offset navigation.
// Architecture:
//     Extends IDecompiler so structured backends are also usable as text
//     backends (DecompileAsync falls back to joining lines).
//////////////////////////////////////////////

namespace WpfHexEditor.Core.Decompiler;

// ---------------------------------------------------------------------------
// Data model
// ---------------------------------------------------------------------------

/// <summary>Semantic role of a token within one disassembly line.</summary>
public enum DisassemblyTokenKind
{
    Address,    // "0x00401000"
    Bytes,      // "55 48 89 E5"
    Mnemonic,   // "mov", "call", "jmp"
    Operand,    // "rax", "[rbp-8]", "0x1234"
    Comment,    // "; entry point"
    Label,      // "sub_401000:"
    Arrow,      // "→" for jump/call targets
}

/// <summary>One coloured token within a <see cref="DisassemblyLine"/>.</summary>
public sealed record DisassemblyToken(DisassemblyTokenKind Kind, string Text);

/// <summary>One disassembled instruction line.</summary>
public sealed record DisassemblyLine(
    long                        FileOffset,   // byte offset in the file
    ulong                       VirtualAddress,
    IReadOnlyList<DisassemblyToken> Tokens,
    string                      RawText,      // fallback for TextBox mode
    bool                        IsJump,
    bool                        IsCall,
    ulong                       TargetVA      // 0 if not a direct branch
);

// ---------------------------------------------------------------------------
// Interface
// ---------------------------------------------------------------------------

/// <summary>
/// A decompiler backend that produces structured <see cref="DisassemblyLine"/>
/// records suitable for syntax-coloured rendering.
/// </summary>
public interface IStructuredDisassembler : IDecompiler
{
    /// <summary>
    /// Disassembles the file and returns structured lines with per-token colours.
    /// </summary>
    Task<IReadOnlyList<DisassemblyLine>> DisassembleAsync(
        string filePath, CancellationToken ct = default);
}
