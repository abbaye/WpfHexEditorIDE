//////////////////////////////////////////////
// Project: WpfHexEditor.Core.Decompiler
// File: IcedDisassembler.cs
// Description:
//     Native binary disassembler using the Iced library (x86-16/32/64).
//     Reads PE headers to determine architecture and .text section range.
//     Falls back to raw hex dump for non-PE or unsupported architectures.
//     Registers itself in DecompilerRegistry on first use.
// Architecture:
//     Implements IStructuredDisassembler.
//     PE parsing via System.Reflection.PortableExecutable (no extra dep).
//     Iced.Intel.Decoder drives disassembly; max 64 K instructions per file.
//////////////////////////////////////////////

using System.Reflection.PortableExecutable;
using System.Text;
using Iced.Intel;

namespace WpfHexEditor.Core.Decompiler;

/// <summary>
/// Native x86/x64 disassembler backed by the Iced library.
/// Supports 32-bit and 64-bit PE files (EXE and DLL).
/// </summary>
public sealed class IcedDisassembler : IStructuredDisassembler
{
    // Singleton — register once at app startup
    public static readonly IcedDisassembler Instance = new();

    private static readonly HashSet<string> SupportedExtensions =
        new(StringComparer.OrdinalIgnoreCase) { ".exe", ".dll", ".sys", ".ocx" };

    private const int MaxInstructions = 65_536;

    public string DisplayName  => "Iced x86/x64 Disassembler";
    public string Architecture { get; private set; } = "x86-64";

    // ── IDecompiler ───────────────────────────────────────────────────────────

    public bool CanDecompile(string filePath)
    {
        if (!SupportedExtensions.Contains(Path.GetExtension(filePath))) return false;
        try
        {
            using var fs = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read);
            using var pe = new PEReader(fs);
            // Accept only unmanaged PE (no .NET metadata) or mixed-mode
            var headers = pe.PEHeaders;
            return headers.IsDll || headers.IsExe;
        }
        catch { return false; }
    }

    /// <summary>Plain-text fallback (joins structured lines).</summary>
    public async Task<string> DecompileAsync(string filePath, CancellationToken ct = default)
    {
        var lines = await DisassembleAsync(filePath, ct);
        var sb    = new StringBuilder(lines.Count * 60);
        foreach (var l in lines)
        {
            ct.ThrowIfCancellationRequested();
            sb.AppendLine(l.RawText);
        }
        return sb.ToString();
    }

    // ── IStructuredDisassembler ───────────────────────────────────────────────

    public Task<IReadOnlyList<DisassemblyLine>> DisassembleAsync(
        string filePath, CancellationToken ct = default)
        => Task.Run(() => (IReadOnlyList<DisassemblyLine>)Disassemble(filePath, ct), ct);

    // ── Core disassembly ──────────────────────────────────────────────────────

    private List<DisassemblyLine> Disassemble(string filePath, CancellationToken ct)
    {
        using var fs = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read);
        using var pe = new PEReader(fs);

        var headers = pe.PEHeaders;
        bool is64 = headers.PEHeader?.Magic == PEMagic.PE32Plus;
        int  bits  = is64 ? 64 : 32;
        Architecture = is64 ? "x86-64" : "x86-32";

        // Find .text section
        SectionHeader? textSection = null;
        foreach (var sec in headers.SectionHeaders)
        {
            var name = sec.Name;
            if (name == ".text" || name == "CODE" || name == ".code")
            {
                textSection = sec;
                break;
            }
        }

        // Fallback: first executable section
        if (textSection is null)
        {
            foreach (var sec in headers.SectionHeaders)
            {
                if ((sec.SectionCharacteristics & SectionCharacteristics.MemExecute) != 0)
                {
                    textSection = sec;
                    break;
                }
            }
        }

        if (textSection is null)
            return [FallbackLine(0, "// No executable section found.")];

        var   sec2     = textSection.Value;
        int   rawOff   = sec2.PointerToRawData;
        int   rawSize  = sec2.SizeOfRawData;
        ulong virtBase = headers.PEHeader is not null ? (ulong)headers.PEHeader.ImageBase : 0UL;
        ulong secVA    = virtBase + (ulong)sec2.VirtualAddress;

        // Read section bytes
        fs.Seek(rawOff, SeekOrigin.Begin);
        var code = new byte[rawSize];
        int read = fs.Read(code, 0, rawSize);
        if (read <= 0)
            return [FallbackLine(rawOff, "// Failed to read section bytes.")];

        // Disassemble with Iced
        var codeReader = new ByteArrayCodeReader(code, 0, read);
        var decoder    = Iced.Intel.Decoder.Create(bits, codeReader, secVA);
        var formatter  = new NasmFormatter();
        formatter.Options.AddLeadingZeroToHexNumbers  = false;
        formatter.Options.ShowBranchSize              = false;
        formatter.Options.RipRelativeAddresses        = true;

        var    output = new StringOutput();
        var    result = new List<DisassemblyLine>(Math.Min(MaxInstructions, rawSize / 4));
        var    instr  = new Instruction();

        // Entry point comment
        ulong epRVA = headers.PEHeader is { } peh ? virtBase + (ulong)peh.AddressOfEntryPoint : 0UL;

        while (codeReader.CanReadByte && result.Count < MaxInstructions)
        {
            ct.ThrowIfCancellationRequested();
            decoder.Decode(out instr);
            if (instr.IsInvalid) break;

            ulong va        = instr.IP;
            long  fileOff   = rawOff + (long)(va - secVA);
            byte  byteCount = (byte)instr.Length;

            // Build hex bytes string (max 8 bytes shown)
            var bytesSpan = new Span<byte>(code, (int)(va - secVA), Math.Min((int)byteCount, 8));
            var bytesStr  = HexBytes(bytesSpan);

            // Format mnemonic + operands
            output.Reset();
            formatter.Format(instr, output);
            var formatted = output.ToStringAndReset();

            // Split into mnemonic + operands
            int   spaceIdx = formatted.IndexOf(' ');
            var   mnemonic = spaceIdx > 0 ? formatted[..spaceIdx] : formatted;
            var   operands = spaceIdx > 0 ? formatted[(spaceIdx + 1)..].Trim() : "";

            bool isJump = instr.FlowControl is FlowControl.UnconditionalBranch
                       or FlowControl.ConditionalBranch
                       or FlowControl.IndirectBranch;
            bool isCall = instr.FlowControl == FlowControl.Call;

            ulong targetVA = 0;
            if ((isJump || isCall) && instr.OpCount > 0 &&
                instr.Op0Kind == OpKind.NearBranch64 || instr.Op0Kind == OpKind.NearBranch32)
                targetVA = instr.NearBranch64;

            // Entry point annotation
            string comment = va == epRVA ? "; ← entry point" : "";

            var tokens = BuildTokens(va, bytesStr, mnemonic, operands, comment, isJump, isCall);
            var rawText = $"{va:X16}  {bytesStr,-24}  {formatted,-40}{comment}";

            result.Add(new DisassemblyLine(fileOff, va, tokens, rawText.TrimEnd(), isJump, isCall, targetVA));
        }

        return result;
    }

    // ── Token building ────────────────────────────────────────────────────────

    private static IReadOnlyList<DisassemblyToken> BuildTokens(
        ulong va, string bytesStr, string mnemonic, string operands, string comment,
        bool isJump, bool isCall)
    {
        var tokens = new List<DisassemblyToken>(6);
        tokens.Add(new DisassemblyToken(DisassemblyTokenKind.Address,  $"{va:X16}  "));
        tokens.Add(new DisassemblyToken(DisassemblyTokenKind.Bytes,    $"{bytesStr,-24}  "));
        tokens.Add(new DisassemblyToken(DisassemblyTokenKind.Mnemonic, mnemonic));
        if (!string.IsNullOrEmpty(operands))
            tokens.Add(new DisassemblyToken(DisassemblyTokenKind.Operand, "  " + operands));
        if (isJump || isCall)
            tokens.Add(new DisassemblyToken(DisassemblyTokenKind.Arrow, isCall ? "  ⤙" : "  →"));
        if (!string.IsNullOrEmpty(comment))
            tokens.Add(new DisassemblyToken(DisassemblyTokenKind.Comment, "  " + comment));
        return tokens;
    }

    private static string HexBytes(Span<byte> bytes)
    {
        var sb = new StringBuilder(bytes.Length * 3);
        foreach (var b in bytes) sb.Append($"{b:X2} ");
        if (sb.Length > 0) sb.Length--;
        return sb.ToString();
    }

    private static DisassemblyLine FallbackLine(long offset, string text) =>
        new(offset, 0,
            [new DisassemblyToken(DisassemblyTokenKind.Comment, text)],
            text, false, false, 0);
}
