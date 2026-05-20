//////////////////////////////////////////////
// GNU Affero General Public License v3.0 - 2026
// Author : Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.6
//////////////////////////////////////////////

using System.Buffers.Binary;
using System.IO;
using System.Text;

namespace WpfHexEditor.App.BinaryAnalysis.Services;

// ---------------------------------------------------------------------------
// Models
// ---------------------------------------------------------------------------

public sealed record PeHeader(
    bool     Is64Bit,
    string   Machine,
    long     EntryPointRva,
    long     ImageBase,
    long     SizeOfImage,
    long     SizeOfHeaders,
    string   Subsystem,
    DateTime TimeDateStamp,
    int      NumberOfSections,
    int      NumberOfRvaAndSizes);

public sealed record ImportEntry(string Name, int? Ordinal, long Rva, long FileOffset);

public sealed record ImportModule(string Dll, IReadOnlyList<ImportEntry> Functions);

public sealed record ExportEntry(string Name, int Ordinal, long Rva, long FileOffset, bool IsForwarder);

public sealed record PeSection(
    string Name,
    long   VirtualAddress,
    long   RawOffset,
    long   Size,
    uint   Characteristics);

public sealed record PeAnalysisResult(
    bool                        Is64Bit,
    PeHeader                    Header,
    IReadOnlyList<ImportModule> Imports,
    IReadOnlyList<ExportEntry>  Exports,
    IReadOnlyList<PeSection>    Sections);

// ---------------------------------------------------------------------------
// Analyzer
// ---------------------------------------------------------------------------

/// <summary>
/// Parses PE32/PE32+ import and export tables directly from a stream,
/// using only BCL primitives (no ILSpy dependency).
/// </summary>
public static class PeFileAnalyzer
{
    private const ushort PeMagic32 = 0x10B;
    private const ushort PeMagic64 = 0x20B;

    /// <summary>
    /// Attempts to parse the PE structure from <paramref name="stream"/>.
    /// Returns <c>null</c> if the stream is not a valid PE file.
    /// </summary>
    public static PeAnalysisResult? TryAnalyze(Stream stream)
    {
        if (stream.Length < 64) return null;
        using var reader = new BinaryReader(stream, Encoding.ASCII, leaveOpen: true);

        // DOS header
        stream.Position = 0;
        if (reader.ReadUInt16() != 0x5A4D) return null;  // 'MZ'
        stream.Position = 60;
        int lfanew = reader.ReadInt32();
        if (lfanew < 0 || lfanew + 24 > stream.Length) return null;

        // PE signature
        stream.Position = lfanew;
        if (reader.ReadUInt32() != 0x00004550) return null;  // 'PE\0\0'

        // COFF header
        ushort machine          = reader.ReadUInt16();
        ushort numberOfSections = reader.ReadUInt16();
        uint   timeDateStamp    = reader.ReadUInt32();
        reader.ReadUInt32(); // PointerToSymbolTable
        reader.ReadUInt32(); // NumberOfSymbols
        ushort sizeOfOptHeader  = reader.ReadUInt16();
        reader.ReadUInt16();  // Characteristics

        if (sizeOfOptHeader == 0) return null;
        long optHeaderOffset = stream.Position;

        // Optional header
        ushort magic = reader.ReadUInt16();
        bool is64 = magic switch
        {
            PeMagic32 => false,
            PeMagic64 => true,
            _         => throw new InvalidDataException($"Unknown PE magic: {magic:X}")
        };

        reader.ReadByte();  // MajorLinkerVersion
        reader.ReadByte();  // MinorLinkerVersion
        reader.ReadUInt32(); // SizeOfCode
        reader.ReadUInt32(); // SizeOfInitializedData
        reader.ReadUInt32(); // SizeOfUninitializedData
        uint entryPointRva = reader.ReadUInt32();
        reader.ReadUInt32(); // BaseOfCode
        if (!is64) reader.ReadUInt32(); // BaseOfData (PE32 only)

        long imageBase  = is64 ? (long)reader.ReadUInt64() : reader.ReadUInt32();
        reader.ReadUInt32(); // SectionAlignment
        reader.ReadUInt32(); // FileAlignment
        reader.ReadUInt16(); // MajorOSVersion
        reader.ReadUInt16(); // MinorOSVersion
        reader.ReadUInt16(); // MajorImageVersion
        reader.ReadUInt16(); // MinorImageVersion
        reader.ReadUInt16(); // MajorSubsystemVersion
        reader.ReadUInt16(); // MinorSubsystemVersion
        reader.ReadUInt32(); // Win32VersionValue
        uint sizeOfImage   = reader.ReadUInt32();
        uint sizeOfHeaders = reader.ReadUInt32();
        reader.ReadUInt32(); // CheckSum
        ushort subsystem   = reader.ReadUInt16();
        reader.ReadUInt16(); // DllCharacteristics
        if (is64) { reader.ReadUInt64(); reader.ReadUInt64(); reader.ReadUInt64(); reader.ReadUInt64(); }
        else      { reader.ReadUInt32(); reader.ReadUInt32(); reader.ReadUInt32(); reader.ReadUInt32(); }
        reader.ReadUInt32(); // LoaderFlags
        uint numberOfRvaAndSizes = reader.ReadUInt32();

        // Data directories — we need [1]=Import, [0]=Export
        uint exportRva = 0, exportSize = 0;
        uint importRva = 0;
        if (numberOfRvaAndSizes > 0) { exportRva  = reader.ReadUInt32(); exportSize = reader.ReadUInt32(); }
        if (numberOfRvaAndSizes > 1) { importRva  = reader.ReadUInt32(); reader.ReadUInt32(); }

        // Section headers
        long sectionTableOffset = optHeaderOffset + sizeOfOptHeader;
        stream.Position = sectionTableOffset;
        var sections = ParseSections(reader, numberOfSections);

        var header = new PeHeader(
            Is64Bit:            is64,
            Machine:            ResolveMachine(machine),
            EntryPointRva:      entryPointRva,
            ImageBase:          imageBase,
            SizeOfImage:        sizeOfImage,
            SizeOfHeaders:      sizeOfHeaders,
            Subsystem:          ResolveSubsystem(subsystem),
            TimeDateStamp:      DateTimeOffset.FromUnixTimeSeconds(timeDateStamp).UtcDateTime,
            NumberOfSections:   numberOfSections,
            NumberOfRvaAndSizes:(int)numberOfRvaAndSizes);

        var imports = importRva != 0
            ? ParseImports(reader, stream, sections, importRva)
            : [];

        var exports = (exportRva != 0 && exportSize != 0)
            ? ParseExports(reader, stream, sections, exportRva, imageBase)
            : [];

        return new PeAnalysisResult(is64, header, imports, exports, sections);
    }

    // -- Sections -----------------------------------------------------------

    private static IReadOnlyList<PeSection> ParseSections(BinaryReader r, int count)
    {
        var list = new List<PeSection>(count);
        for (int i = 0; i < count; i++)
        {
            var nameBytes = r.ReadBytes(8);
            int nullIdx   = Array.IndexOf(nameBytes, (byte)0);
            string name   = Encoding.ASCII.GetString(nameBytes, 0, nullIdx < 0 ? 8 : nullIdx);
            uint virtSize = r.ReadUInt32();
            uint virtAddr = r.ReadUInt32();
            uint rawSize  = r.ReadUInt32();
            uint rawPtr   = r.ReadUInt32();
            r.ReadUInt32(); r.ReadUInt32(); r.ReadUInt32(); // reloc/linenum/counts
            uint chars    = r.ReadUInt32();
            list.Add(new PeSection(name, virtAddr, rawPtr, virtSize > 0 ? virtSize : rawSize, chars));
        }
        return list;
    }

    // -- Import table -------------------------------------------------------

    private static IReadOnlyList<ImportModule> ParseImports(
        BinaryReader r, Stream s,
        IReadOnlyList<PeSection> sections, uint importRva)
    {
        long importOffset = RvaToOffset(importRva, sections);
        if (importOffset < 0) return [];

        var modules = new List<ImportModule>();
        s.Position  = importOffset;

        while (true)
        {
            uint originalFirstThunk = r.ReadUInt32();
            r.ReadUInt32(); r.ReadUInt32(); // TimeDateStamp, ForwarderChain
            uint nameRva       = r.ReadUInt32();
            uint firstThunk    = r.ReadUInt32();

            if (nameRva == 0 && firstThunk == 0) break;

            long nameOffset = RvaToOffset(nameRva, sections);
            if (nameOffset < 0) break;

            string dllName   = ReadNullTerminatedAscii(s, nameOffset);
            uint   thunkRva  = originalFirstThunk != 0 ? originalFirstThunk : firstThunk;
            long   thunkOff  = RvaToOffset(thunkRva, sections);

            var entries = new List<ImportEntry>();
            if (thunkOff >= 0)
            {
                long savedPos = s.Position;
                s.Position    = thunkOff;
                while (true)
                {
                    uint thunk = r.ReadUInt32();
                    if (thunk == 0) break;

                    if ((thunk & 0x8000_0000) != 0)
                    {
                        // Ordinal import
                        int ord = (int)(thunk & 0xFFFF);
                        entries.Add(new ImportEntry($"#{ord}", ord, firstThunk, thunkOff));
                    }
                    else
                    {
                        // Name import — hint+name table
                        long hintNameOff = RvaToOffset(thunk, sections);
                        if (hintNameOff >= 0)
                        {
                            s.Position = hintNameOff + 2; // skip hint word
                            string funcName = ReadNullTerminatedAscii(s, s.Position);
                            entries.Add(new ImportEntry(funcName, null, thunk, hintNameOff));
                            s.Position = thunkOff + (entries.Count) * 4;
                        }
                    }
                }
                s.Position = savedPos;
            }

            modules.Add(new ImportModule(dllName, entries));
        }

        return modules;
    }

    // -- Export table -------------------------------------------------------

    private static IReadOnlyList<ExportEntry> ParseExports(
        BinaryReader r, Stream s,
        IReadOnlyList<PeSection> sections, uint exportRva, long imageBase)
    {
        long exportOffset = RvaToOffset(exportRva, sections);
        if (exportOffset < 0) return [];

        s.Position = exportOffset;
        r.ReadUInt32(); // Characteristics
        r.ReadUInt32(); // TimeDateStamp
        r.ReadUInt16(); r.ReadUInt16(); // Major/MinorVersion
        r.ReadUInt32(); // NameRVA
        uint ordinalBase      = r.ReadUInt32();
        uint numberOfFunctions= r.ReadUInt32();
        uint numberOfNames    = r.ReadUInt32();
        uint addressTableRva  = r.ReadUInt32();
        uint namePointerRva   = r.ReadUInt32();
        uint ordinalTableRva  = r.ReadUInt32();

        long addrOff    = RvaToOffset(addressTableRva, sections);
        long nameOff    = RvaToOffset(namePointerRva,  sections);
        long ordinalOff = RvaToOffset(ordinalTableRva, sections);
        if (addrOff < 0 || nameOff < 0 || ordinalOff < 0) return [];

        // Read address table
        s.Position = addrOff;
        var addresses = new uint[numberOfFunctions];
        for (int i = 0; i < numberOfFunctions; i++) addresses[i] = r.ReadUInt32();

        // Read name pointer table
        s.Position = nameOff;
        var nameRvas = new uint[numberOfNames];
        for (int i = 0; i < numberOfNames; i++) nameRvas[i] = r.ReadUInt32();

        // Read ordinal table
        s.Position = ordinalOff;
        var ordinals = new ushort[numberOfNames];
        for (int i = 0; i < numberOfNames; i++) ordinals[i] = r.ReadUInt16();

        // Build name lookup: ordinal index → name
        var nameByIndex = new Dictionary<int, string>();
        for (int i = 0; i < numberOfNames; i++)
        {
            long nOff = RvaToOffset(nameRvas[i], sections);
            if (nOff >= 0)
                nameByIndex[ordinals[i]] = ReadNullTerminatedAscii(s, nOff);
        }

        var entries = new List<ExportEntry>((int)numberOfFunctions);
        for (int i = 0; i < numberOfFunctions; i++)
        {
            if (addresses[i] == 0) continue;
            int    ord         = (int)(ordinalBase + i);
            string name        = nameByIndex.TryGetValue(i, out var n) ? n : $"#{ord}";
            long   fileOffset  = RvaToOffset(addresses[i], sections);
            entries.Add(new ExportEntry(name, ord, addresses[i], fileOffset, false));
        }
        return entries;
    }

    // -- Helpers ------------------------------------------------------------

    private static long RvaToOffset(uint rva, IReadOnlyList<PeSection> sections)
    {
        foreach (var sec in sections)
        {
            long rvaLong = rva;
            if (rvaLong >= sec.VirtualAddress && rvaLong < sec.VirtualAddress + sec.Size)
                return sec.RawOffset + (rvaLong - sec.VirtualAddress);
        }
        return -1;
    }

    private static string ReadNullTerminatedAscii(Stream s, long offset)
    {
        s.Position = offset;
        var sb = new StringBuilder(32);
        int b;
        while ((b = s.ReadByte()) > 0) sb.Append((char)b);
        return sb.ToString();
    }

    private static string ResolveMachine(ushort machine) => machine switch
    {
        0x014C => "x86",
        0x8664 => "x64",
        0xAA64 => "ARM64",
        0x01C4 => "ARM",
        0x0200 => "IA-64",
        _      => $"0x{machine:X4}"
    };

    private static string ResolveSubsystem(ushort subsystem) => subsystem switch
    {
        1  => "Native",
        2  => "Windows GUI",
        3  => "Windows CUI",
        7  => "POSIX CUI",
        9  => "Windows CE GUI",
        10 => "EFI Application",
        _  => $"Unknown ({subsystem})"
    };

    /// <summary>
    /// Returns true for DLL names commonly associated with suspicious behaviour
    /// (network, injection, crypto by ordinal-only).
    /// </summary>
    public static bool IsSuspectModule(ImportModule m)
    {
        var suspicious = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "ws2_32.dll", "wininet.dll", "urlmon.dll", "winhttp.dll",
            "ntdll.dll",  "msvcrt.dll"
        };
        bool suspectDll      = suspicious.Contains(m.Dll);
        bool ordinalOnlyImps = m.Functions.Count > 0 && m.Functions.All(f => f.Ordinal.HasValue && f.Name.StartsWith('#'));
        return suspectDll || ordinalOnlyImps;
    }
}
