// ==========================================================
// Project: whfmt.CodeGen
// File: Generator/ParserGenerator.cs
// Description: Generates strongly-typed C# parser classes from .whfmt JSON definitions.
// Architecture: Reads BlockDefinition array → emits C# source via StringBuilder template.
//   Phase 1: Rich types — enum for valueMap, [Flags] for bitfields, List<T> for repeating, nullable for conditionals
//   Phase 2: Typed exceptions — InvalidSignatureException, ChecksumMismatchException, TruncatedFileException
// ==========================================================

using System.Text;
using System.Text.Json;

namespace WhfmtCodeGen.Generator;

/// <summary>Generates a strongly-typed C# parser class from a .whfmt JSON definition.</summary>
internal static class ParserGenerator
{
    // .whfmt files use JSONC (// and /* */ comments, trailing commas)
    private static readonly JsonDocumentOptions _jsonc = new()
    {
        CommentHandling     = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
    };

    /// <summary>Generate C# source from .whfmt JSON.</summary>
    public static string GenerateFromJson(
        string json,
        string namespaceName,
        string className,
        bool includeValidation,
        bool generateAsync,
        OutputLanguage language = OutputLanguage.CSharp)
    {
        var trimmed = json.AsSpan().TrimStart();
        if (trimmed.IsEmpty || trimmed[0] != '{')
            throw new InvalidOperationException(
                $"Expected a JSON object but the content starts with '{(trimmed.IsEmpty ? "<empty>" : trimmed[0].ToString())}'. " +
                "Ensure the resolved resource is a valid .whfmt file and not a .grammar (Synalysis XML) or other non-JSON resource.");

        using var doc = JsonDocument.Parse(json, _jsonc);
        var root = doc.RootElement;

        string formatName = root.TryGetProperty("name",        out var n) ? n.GetString() ?? className : className;
        string category   = root.TryGetProperty("category",    out var c) ? c.GetString() ?? ""        : "";
        string version    = root.TryGetProperty("version",     out var v) ? v.GetString() ?? "1.0"     : "1.0";
        string desc       = root.TryGetProperty("description", out var d) ? d.GetString() ?? ""        : "";

        var blocks    = ParseBlocks(root);
        var checksums = ParseChecksums(root);
        var enums     = BuildEnumDefs(blocks);

        return language switch
        {
            OutputLanguage.CSharpSpan  => SpanGenerator.Generate(formatName, category, version, desc, namespaceName, className, blocks, checksums, includeValidation),
            OutputLanguage.FSharp      => FSharpGenerator.Generate(formatName, category, version, desc, namespaceName, className, blocks, checksums),
            OutputLanguage.Rust        => RustGenerator.Generate(formatName, category, version, desc, className, blocks),
            OutputLanguage.VisualBasic => VBGenerator.Generate(formatName, category, version, desc, namespaceName, className, blocks, checksums),
            _                          => GenerateCSharp(formatName, category, version, desc, namespaceName, className, blocks, checksums, enums, includeValidation, generateAsync),
        };
    }

    private static string GenerateCSharp(string formatName, string category, string version, string desc,
        string namespaceName, string className,
        List<BlockDef> blocks, List<ChecksumDef> checksums, List<EnumDef> enums,
        bool includeValidation, bool generateAsync)
    {
        var sb = new StringBuilder();
        EmitHeader(sb, formatName, category, version, desc, namespaceName, className);
        EmitEnums(sb, enums);
        if (includeValidation) EmitExceptionClasses(sb, className);
        EmitParserClass(sb, formatName, className, blocks, checksums, enums, includeValidation, generateAsync);
        return sb.ToString();
    }

    // ── Block / Checksum / Enum models ───────────────────────────────────────

    internal sealed class BlockDef
    {
        public string Name        { get; init; } = "";
        public string StoreAs     { get; init; } = "";
        public long   Offset      { get; init; }
        public int    Length      { get; init; }
        public string Type        { get; init; } = "bytes";
        public string? Endian     { get; init; }
        public bool   IsSignature { get; init; }
        public bool   IsRepeating { get; init; }
        public bool   IsConditional { get; init; }
        public bool   IsBitFlags  { get; init; }
        public string? DependsOn  { get; init; }
        public byte[]? ExpectedBytes { get; init; }
        public Dictionary<string, string> ValueMap { get; init; } = [];
        public string  Description { get; init; } = "";

        public string PropertyName => ToPascal(string.IsNullOrEmpty(StoreAs) ? Name : StoreAs);
        public string CsType(List<EnumDef> enums)
        {
            // If there's a rich enum for this block, use it
            var enumDef = enums.FirstOrDefault(e => e.OwnerPropertyName == PropertyName);
            if (enumDef is not null)
            {
                string enumType = enumDef.Name;
                if (IsConditional) return enumType + "?";
                if (IsRepeating)   return $"List<{enumType}>";
                return enumType;
            }

            string mapped = MapType(Type, Length);
            if (IsConditional && mapped != "byte[]") return mapped + "?";
            if (IsRepeating   && mapped != "byte[]") return $"List<{mapped}>";
            return mapped;
        }

        public string CsTypeSimple => MapType(Type, Length);
    }

    internal sealed class ChecksumDef
    {
        public string Algorithm   { get; init; } = "";
        public long   StoredOffset { get; init; }
        public int    StoredLength { get; init; }
        public long   DataOffset   { get; init; }
        public long   DataLength   { get; init; }
    }

    internal sealed class EnumDef
    {
        public string Name              { get; init; } = "";
        public string OwnerPropertyName { get; init; } = "";
        public bool   IsFlags           { get; init; }
        public Dictionary<string, string> Members { get; init; } = [];
    }

    private static List<BlockDef> ParseBlocks(JsonElement root)
    {
        var list = new List<BlockDef>();
        if (!root.TryGetProperty("blocks", out var blocks)) return list;
        foreach (var b in blocks.EnumerateArray())
        {
            string name       = b.TryGetProperty("name",         out var n)  ? n.GetString()  ?? "" : "";
            string storeAs    = b.TryGetProperty("storeAs",      out var s)  ? s.GetString()  ?? "" : "";
            long   offset     = b.TryGetProperty("offset",       out var o)  && o.ValueKind == JsonValueKind.Number ? o.GetInt64() : 0;
            int    length     = b.TryGetProperty("length",       out var l)  && l.ValueKind == JsonValueKind.Number ? l.GetInt32() : 1;
            string type       = b.TryGetProperty("type",         out var t)  ? t.GetString()  ?? "bytes" : "bytes";
            string? endian    = b.TryGetProperty("endian",       out var e)  ? e.GetString()  : null;
            bool   isSig      = b.TryGetProperty("isSignature",  out var sg) && sg.GetBoolean();
            bool   isRepeat   = b.TryGetProperty("repeating",    out var rp) && rp.GetBoolean();
            bool   isCond     = b.TryGetProperty("conditional",  out var cd) && cd.GetBoolean();
            bool   isFlags    = b.TryGetProperty("bitflags",     out var bf) && bf.GetBoolean();
            string? dependsOn = b.TryGetProperty("dependsOn",    out var dep) ? dep.GetString() : null;
            string desc       = b.TryGetProperty("description",  out var ds) ? ds.GetString()  ?? "" : "";

            byte[]? expectedBytes = null;
            if (isSig && b.TryGetProperty("value", out var vEl) && vEl.ValueKind == JsonValueKind.String)
            {
                var hex = vEl.GetString()?.Replace(" ", "").Replace("0x", "").Replace("-", "") ?? "";
                if (hex.Length % 2 == 0 && hex.Length > 0)
                    expectedBytes = Convert.FromHexString(hex);
            }

            var vm = new Dictionary<string, string>();
            if (b.TryGetProperty("valueMap", out var vmEl))
                foreach (var kv in vmEl.EnumerateObject())
                    vm[kv.Name] = kv.Value.GetString() ?? kv.Name;

            if (string.IsNullOrEmpty(name) && string.IsNullOrEmpty(storeAs)) continue;
            list.Add(new BlockDef
            {
                Name = name, StoreAs = storeAs, Offset = offset, Length = length,
                Type = type, Endian = endian, IsSignature = isSig,
                IsRepeating = isRepeat, IsConditional = isCond, IsBitFlags = isFlags,
                DependsOn = dependsOn, ExpectedBytes = expectedBytes, ValueMap = vm, Description = desc
            });
        }
        return list;
    }

    private static List<ChecksumDef> ParseChecksums(JsonElement root)
    {
        var list = new List<ChecksumDef>();
        if (!root.TryGetProperty("checksums", out var cks)) return list;
        foreach (var ck in cks.EnumerateArray())
        {
            string algo    = ck.TryGetProperty("algorithm", out var a)   ? a.GetString()   ?? "" : "";
            long storedOff = ck.TryGetProperty("storedAt",  out var sat)  && sat.TryGetProperty("fixedOffset", out var sfo) ? sfo.GetInt64() : -1;
            int  storedLen = ck.TryGetProperty("storedAt",  out var sat2) && sat2.TryGetProperty("length",     out var sl)  ? sl.GetInt32()  : 4;
            long dataOff   = ck.TryGetProperty("dataRange", out var dr)   && dr.TryGetProperty("fixedOffset",  out var dfo) ? dfo.GetInt64() : 0;
            long dataLen   = ck.TryGetProperty("dataRange", out var dr2)  && dr2.TryGetProperty("fixedLength", out var dfl) ? dfl.GetInt64() : -1;
            if (storedOff < 0) continue;
            list.Add(new ChecksumDef { Algorithm = algo, StoredOffset = storedOff, StoredLength = storedLen, DataOffset = dataOff, DataLength = dataLen });
        }
        return list;
    }

    // ── Enum extraction ──────────────────────────────────────────────────────

    private static List<EnumDef> BuildEnumDefs(List<BlockDef> blocks)
    {
        var list = new List<EnumDef>();
        foreach (var b in blocks)
        {
            if (b.ValueMap.Count < 2) continue;
            string enumName = b.PropertyName + "Type";
            var members = new Dictionary<string, string>();
            foreach (var kv in b.ValueMap)
            {
                string memberName = ToEnumMember(kv.Value);
                if (!members.ContainsKey(memberName))
                    members[memberName] = kv.Key;
            }
            list.Add(new EnumDef { Name = enumName, OwnerPropertyName = b.PropertyName, IsFlags = b.IsBitFlags, Members = members });
        }
        return list;
    }

    // ── Code emission ────────────────────────────────────────────────────────

    private static void EmitHeader(StringBuilder sb, string formatName, string category, string version,
        string desc, string ns, string className)
    {
        sb.AppendLine("// <auto-generated>");
        sb.AppendLine($"//   Generated by whfmt-codegen from '{formatName}' v{version}");
        if (!string.IsNullOrEmpty(category)) sb.AppendLine($"//   Category: {category}");
        if (!string.IsNullOrEmpty(desc))     sb.AppendLine($"//   {desc}");
        sb.AppendLine("//   Do not edit — regenerate with: whfmt-codegen generate");
        sb.AppendLine("// </auto-generated>");
        sb.AppendLine();
        sb.AppendLine("using System;");
        sb.AppendLine("using System.Collections.Generic;");
        sb.AppendLine("using System.IO;");
        sb.AppendLine("using System.Threading;");
        sb.AppendLine("using System.Threading.Tasks;");
        sb.AppendLine();
        sb.AppendLine($"namespace {ns};");
        sb.AppendLine();
    }

    private static void EmitEnums(StringBuilder sb, List<EnumDef> enums)
    {
        foreach (var e in enums)
        {
            if (e.IsFlags) sb.AppendLine("    [Flags]");
            sb.AppendLine($"    public enum {e.Name}");
            sb.AppendLine("    {");
            foreach (var m in e.Members)
            {
                // Try to parse as integer for the enum value
                if (long.TryParse(m.Value, out long numVal))
                    sb.AppendLine($"        {m.Key} = {numVal},");
                else
                    sb.AppendLine($"        {m.Key},");
            }
            sb.AppendLine("    }");
            sb.AppendLine();
        }
    }

    private static void EmitExceptionClasses(StringBuilder sb, string className)
    {
        string prefix = className.Replace("Parser", "");
        sb.AppendLine($"    /// <summary>Thrown when the {prefix} file signature does not match the expected magic bytes.</summary>");
        sb.AppendLine($"    public sealed class InvalidSignatureException : Exception");
        sb.AppendLine("    {");
        sb.AppendLine("        public byte[] Actual   { get; }");
        sb.AppendLine("        public byte[] Expected { get; }");
        sb.AppendLine($"        public InvalidSignatureException(byte[] actual, byte[] expected)");
        sb.AppendLine($"            : base($\"Invalid {prefix} signature: expected {{BitConverter.ToString(expected)}}, got {{BitConverter.ToString(actual)}}\")");
        sb.AppendLine("        { Actual = actual; Expected = expected; }");
        sb.AppendLine("    }");
        sb.AppendLine();
        sb.AppendLine($"    /// <summary>Thrown when a computed checksum does not match the stored value.</summary>");
        sb.AppendLine($"    public sealed class ChecksumMismatchException : Exception");
        sb.AppendLine("    {");
        sb.AppendLine("        public string Algorithm { get; }");
        sb.AppendLine("        public byte[] Computed  { get; }");
        sb.AppendLine("        public byte[] Stored    { get; }");
        sb.AppendLine($"        public ChecksumMismatchException(string algorithm, byte[] computed, byte[] stored)");
        sb.AppendLine($"            : base($\"{{algorithm}} checksum mismatch: computed {{BitConverter.ToString(computed)}}, stored {{BitConverter.ToString(stored)}}\")");
        sb.AppendLine("        { Algorithm = algorithm; Computed = computed; Stored = stored; }");
        sb.AppendLine("    }");
        sb.AppendLine();
        sb.AppendLine($"    /// <summary>Thrown when the file is too short to read a required field.</summary>");
        sb.AppendLine($"    public sealed class TruncatedFileException : Exception");
        sb.AppendLine("    {");
        sb.AppendLine("        public string FieldName      { get; }");
        sb.AppendLine("        public long   RequiredOffset { get; }");
        sb.AppendLine("        public long   ActualLength   { get; }");
        sb.AppendLine($"        public TruncatedFileException(string fieldName, long requiredOffset, long actualLength)");
        sb.AppendLine($"            : base($\"File truncated: field '{{fieldName}}' requires offset {{requiredOffset}} but file is only {{actualLength}} bytes\")");
        sb.AppendLine("        { FieldName = fieldName; RequiredOffset = requiredOffset; ActualLength = actualLength; }");
        sb.AppendLine("    }");
        sb.AppendLine();
    }

    private static void EmitParserClass(StringBuilder sb, string formatName, string className,
        List<BlockDef> blocks, List<ChecksumDef> checksums, List<EnumDef> enums,
        bool includeValidation, bool generateAsync)
    {
        sb.AppendLine($"/// <summary>Strongly-typed parser for {formatName} files (generated from .whfmt definition).</summary>");
        sb.AppendLine($"public sealed class {className}");
        sb.AppendLine("{");

        EmitProperties(sb, blocks, enums);
        EmitParseMethods(sb, blocks, checksums, enums, className, includeValidation, generateAsync);
        EmitFooter(sb);

        sb.AppendLine("}");
    }

    private static void EmitProperties(StringBuilder sb, List<BlockDef> blocks, List<EnumDef> enums)
    {
        foreach (var b in blocks)
        {
            if (string.IsNullOrEmpty(b.PropertyName)) continue;
            if (!string.IsNullOrEmpty(b.Description))
                sb.AppendLine($"    /// <summary>{EscapeXml(b.Description)}</summary>");

            string propType = b.CsType(enums);
            sb.AppendLine($"    public {propType} {b.PropertyName} {{ get; private set; }}");

            // For non-enum blocks with valueMap, emit a Label property
            var enumDef = enums.FirstOrDefault(e => e.OwnerPropertyName == b.PropertyName);
            if (enumDef is null && b.ValueMap.Count > 0)
            {
                sb.AppendLine();
                sb.AppendLine($"    /// <summary>Human-readable label for {b.PropertyName}.</summary>");
                sb.Append($"    public string {b.PropertyName}Label => {b.PropertyName} switch {{");
                foreach (var kv in b.ValueMap)
                    sb.Append($" {FormatLiteral(b.CsTypeSimple, kv.Key)} => \"{EscapeString(kv.Value)}\",");
                sb.AppendLine(" _ => \"Unknown\" };");
            }
            sb.AppendLine();
        }
    }

    private static void EmitParseMethods(StringBuilder sb, List<BlockDef> blocks, List<ChecksumDef> checksums,
        List<EnumDef> enums, string className, bool includeValidation, bool generateAsync)
    {
        // Sync Parse(Stream)
        sb.AppendLine($"    /// <summary>Parse a {className.Replace("Parser", "")} from a stream.</summary>");
        sb.AppendLine($"    public static {className} Parse(Stream stream)");
        sb.AppendLine("    {");
        sb.AppendLine($"        var result = new {className}();");
        sb.AppendLine("        using var br = new BinaryReader(stream, System.Text.Encoding.Latin1, leaveOpen: true);");
        sb.AppendLine("        long fileLen = stream.Length;");
        EmitReadBlocks(sb, blocks, enums, includeValidation);
        if (includeValidation) EmitChecksumValidation(sb, checksums);
        sb.AppendLine("        return result;");
        sb.AppendLine("    }");
        sb.AppendLine();

        sb.AppendLine($"    /// <summary>Parse a {className.Replace("Parser", "")} from a byte array.</summary>");
        sb.AppendLine($"    public static {className} Parse(byte[] data)");
        sb.AppendLine($"        => Parse(new MemoryStream(data));");
        sb.AppendLine();

        sb.AppendLine($"    /// <summary>Parse a {className.Replace("Parser", "")} from a file path.</summary>");
        sb.AppendLine($"    public static {className} ParseFile(string path)");
        sb.AppendLine("    {");
        sb.AppendLine("        using var fs = File.OpenRead(path);");
        sb.AppendLine($"        return Parse(fs);");
        sb.AppendLine("    }");
        sb.AppendLine();

        if (generateAsync)
        {
            sb.AppendLine($"    /// <summary>Asynchronously parse a {className.Replace("Parser", "")} from a stream.</summary>");
            sb.AppendLine($"    public static async Task<{className}> ParseAsync(Stream stream, CancellationToken cancellationToken = default)");
            sb.AppendLine("    {");
            sb.AppendLine($"        var result = new {className}();");
            sb.AppendLine("        var buf = new byte[8];");
            sb.AppendLine("        long fileLen = stream.Length;");
            EmitReadBlocksAsync(sb, blocks, enums, includeValidation);
            sb.AppendLine("        return result;");
            sb.AppendLine("    }");
            sb.AppendLine();

            sb.AppendLine($"    /// <summary>Asynchronously parse a {className.Replace("Parser", "")} from a file path.</summary>");
            sb.AppendLine($"    public static async Task<{className}> ParseFileAsync(string path, CancellationToken cancellationToken = default)");
            sb.AppendLine("    {");
            sb.AppendLine("        using var fs = File.OpenRead(path);");
            sb.AppendLine($"        return await ParseAsync(fs, cancellationToken);");
            sb.AppendLine("    }");
            sb.AppendLine();
        }
    }

    private static void EmitReadBlocks(StringBuilder sb, List<BlockDef> blocks, List<EnumDef> enums, bool validate)
    {
        foreach (var b in blocks)
        {
            if (string.IsNullOrEmpty(b.PropertyName)) continue;
            sb.AppendLine($"        // {b.Name} @ offset {b.Offset}, length {b.Length}");
            if (validate && b.IsSignature)
                sb.AppendLine($"        if (fileLen < {b.Offset + b.Length}) throw new TruncatedFileException(\"{b.Name}\", {b.Offset + b.Length}, fileLen);");

            sb.AppendLine($"        stream.Seek({b.Offset}, SeekOrigin.Begin);");

            string readExpr = ReadExpressionWithCast(b, enums);
            sb.AppendLine($"        result.{b.PropertyName} = {readExpr};");

            // Signature validation with typed exception
            if (validate && b.IsSignature && b.ExpectedBytes is { Length: > 0 })
            {
                string expectedHex = "new byte[] { " + string.Join(", ", b.ExpectedBytes.Select(x => $"0x{x:X2}")) + " }";
                sb.AppendLine($"        {{");
                sb.AppendLine($"            var _expected_{b.PropertyName} = {expectedHex};");
                sb.AppendLine($"            if (!result.{b.PropertyName}.AsSpan().SequenceEqual(_expected_{b.PropertyName}))");
                sb.AppendLine($"                throw new InvalidSignatureException(result.{b.PropertyName}, _expected_{b.PropertyName});");
                sb.AppendLine($"        }}");
            }
        }
    }

    private static void EmitReadBlocksAsync(StringBuilder sb, List<BlockDef> blocks, List<EnumDef> enums, bool validate)
    {
        foreach (var b in blocks)
        {
            if (string.IsNullOrEmpty(b.PropertyName)) continue;
            sb.AppendLine($"        // {b.Name} @ offset {b.Offset}, length {b.Length}");
            sb.AppendLine($"        stream.Seek({b.Offset}, SeekOrigin.Begin);");

            string simpleType = b.CsTypeSimple;
            if (IsScalarType(simpleType) && b.Length <= 8)
            {
                sb.AppendLine($"        await stream.ReadExactlyAsync(buf.AsMemory(0, {b.Length}), cancellationToken);");
                string castExpr = ConvertExpressionWithCast(b, enums);
                sb.AppendLine($"        result.{b.PropertyName} = {castExpr};");
            }
            else
            {
                sb.AppendLine($"        var buf_{b.PropertyName} = new byte[{b.Length}];");
                sb.AppendLine($"        await stream.ReadExactlyAsync(buf_{b.PropertyName}, cancellationToken);");
                sb.AppendLine($"        result.{b.PropertyName} = buf_{b.PropertyName};");
            }
        }
    }

    private static void EmitChecksumValidation(StringBuilder sb, List<ChecksumDef> checksums)
    {
        if (checksums.Count == 0) return;
        sb.AppendLine("        // Checksum verification");
        sb.AppendLine("        {");
        sb.AppendLine("            stream.Seek(0, SeekOrigin.Begin);");
        sb.AppendLine("            byte[] _allBytes = new byte[stream.Length];");
        sb.AppendLine("            stream.ReadExactly(_allBytes);");
        foreach (var cs in checksums)
        {
            sb.AppendLine($"            // {cs.Algorithm} checksum @ stored offset {cs.StoredOffset}");
            string dataSliceEnd = cs.DataLength > 0
                ? $"{cs.DataOffset} + {cs.DataLength}"
                : "_allBytes.Length";
            sb.AppendLine($"            if ({cs.StoredOffset} + {cs.StoredLength} <= _allBytes.Length)");
            sb.AppendLine("            {");
            sb.AppendLine($"                var _stored_{cs.StoredOffset} = _allBytes[{cs.StoredOffset}..{cs.StoredOffset + cs.StoredLength}];");
            sb.AppendLine($"                var _data_{cs.StoredOffset} = _allBytes[{cs.DataOffset}..{dataSliceEnd}];");

            string computeCall = cs.Algorithm.ToLowerInvariant() switch
            {
                "md5"    => "System.Security.Cryptography.MD5.HashData(_data_" + cs.StoredOffset + ")",
                "sha1"   => "System.Security.Cryptography.SHA1.HashData(_data_" + cs.StoredOffset + ")",
                "sha256" => "System.Security.Cryptography.SHA256.HashData(_data_" + cs.StoredOffset + ")",
                "crc32"  => "BitConverter.GetBytes(Crc32(_data_" + cs.StoredOffset + "))",
                _        => "null /* unsupported algorithm: " + cs.Algorithm + " */",
            };
            if (computeCall.StartsWith("null")) { sb.AppendLine("            }"); continue; }
            sb.AppendLine($"                var _computed_{cs.StoredOffset} = {computeCall};");
            sb.AppendLine($"                var _cmpLen = Math.Min(_stored_{cs.StoredOffset}.Length, _computed_{cs.StoredOffset}.Length);");
            sb.AppendLine($"                if (!_stored_{cs.StoredOffset}.AsSpan(0, _cmpLen).SequenceEqual(_computed_{cs.StoredOffset}.AsSpan(0, _cmpLen)))");
            sb.AppendLine($"                    throw new ChecksumMismatchException(\"{cs.Algorithm}\", _computed_{cs.StoredOffset}[.._cmpLen], _stored_{cs.StoredOffset}[.._cmpLen]);");
            sb.AppendLine("            }");
        }
        sb.AppendLine("        }");
    }

    private static void EmitFooter(StringBuilder sb)
    {
        sb.AppendLine("    // Byte-swap helpers for big-endian fields");
        sb.AppendLine("    private static ushort BSwap16(ushort v) => (ushort)((v << 8) | (v >> 8));");
        sb.AppendLine("    private static uint   BSwap32(uint v)   => (v << 24) | ((v & 0xFF00) << 8) | ((v >> 8) & 0xFF00) | (v >> 24);");
        sb.AppendLine("    private static ulong  BSwap64(ulong v)  => ((ulong)BSwap32((uint)v) << 32) | BSwap32((uint)(v >> 32));");
        sb.AppendLine();
        sb.AppendLine("    private static uint Crc32(byte[] data)");
        sb.AppendLine("    {");
        sb.AppendLine("        uint crc = 0xFFFFFFFF;");
        sb.AppendLine("        foreach (var b in data) crc = (crc >> 8) ^ _crc32Table[(crc & 0xFF) ^ b];");
        sb.AppendLine("        return ~crc;");
        sb.AppendLine("    }");
        sb.AppendLine("    private static readonly uint[] _crc32Table = BuildCrc32Table();");
        sb.AppendLine("    private static uint[] BuildCrc32Table()");
        sb.AppendLine("    {");
        sb.AppendLine("        var t = new uint[256];");
        sb.AppendLine("        for (uint i = 0; i < 256; i++) { uint c = i; for (int j = 8; j > 0; j--) c = (c & 1) != 0 ? (c >> 1) ^ 0xEDB88320u : c >> 1; t[i] = c; }");
        sb.AppendLine("        return t;");
        sb.AppendLine("    }");
    }

    // ── Type helpers ─────────────────────────────────────────────────────────

    internal static string MapType(string whfmtType, int length) => whfmtType.ToLowerInvariant() switch
    {
        "uint8"  or "byte"   => "byte",
        "uint16" or "ushort" => "ushort",
        "uint32" or "uint"   => "uint",
        "uint64" or "ulong"  => "ulong",
        "int8"   or "sbyte"  => "sbyte",
        "int16"  or "short"  => "short",
        "int32"  or "int"    => "int",
        "int64"  or "long"   => "long",
        "float"  or "float32"=> "float",
        "double" or "float64"=> "double",
        "string" or "ascii"  or "utf8" or "utf-8" => "string",
        _ => "byte[]",
    };

    internal static bool IsScalarType(string csType) =>
        csType is "byte" or "ushort" or "uint" or "ulong" or "sbyte" or "short" or "int" or "long" or "float" or "double";

    private static string ReadExpressionWithCast(BlockDef b, List<EnumDef> enums)
    {
        var enumDef = enums.FirstOrDefault(e => e.OwnerPropertyName == b.PropertyName);
        string baseExpr = b.CsTypeSimple switch
        {
            "byte"   => "br.ReadByte()",
            "sbyte"  => "br.ReadSByte()",
            "ushort" => b.Endian == "big" ? "BSwap16(br.ReadUInt16())" : "br.ReadUInt16()",
            "short"  => b.Endian == "big" ? "(short)BSwap16((ushort)br.ReadInt16())" : "br.ReadInt16()",
            "uint"   => b.Endian == "big" ? "BSwap32(br.ReadUInt32())" : "br.ReadUInt32()",
            "int"    => b.Endian == "big" ? "(int)BSwap32((uint)br.ReadInt32())" : "br.ReadInt32()",
            "ulong"  => b.Endian == "big" ? "BSwap64(br.ReadUInt64())" : "br.ReadUInt64()",
            "long"   => b.Endian == "big" ? "(long)BSwap64((ulong)br.ReadInt64())" : "br.ReadInt64()",
            "float"  => "br.ReadSingle()",
            "double" => "br.ReadDouble()",
            "string" => $"new string(br.ReadChars({b.Length})).TrimEnd('\\0')",
            _        => $"br.ReadBytes({b.Length})",
        };

        if (enumDef is not null && b.CsTypeSimple != "byte[]" && b.CsTypeSimple != "string")
            return $"({enumDef.Name}){baseExpr}";
        return baseExpr;
    }

    private static string ConvertExpressionWithCast(BlockDef b, List<EnumDef> enums)
    {
        var enumDef = enums.FirstOrDefault(e => e.OwnerPropertyName == b.PropertyName);
        string baseExpr = b.CsTypeSimple switch
        {
            "byte"   => "buf[0]",
            "ushort" => b.Endian == "big" ? "BSwap16(BitConverter.ToUInt16(buf, 0))" : "BitConverter.ToUInt16(buf, 0)",
            "uint"   => b.Endian == "big" ? "BSwap32(BitConverter.ToUInt32(buf, 0))" : "BitConverter.ToUInt32(buf, 0)",
            "ulong"  => b.Endian == "big" ? "BSwap64(BitConverter.ToUInt64(buf, 0))" : "BitConverter.ToUInt64(buf, 0)",
            "int"    => b.Endian == "big" ? "(int)BSwap32(BitConverter.ToUInt32(buf, 0))" : "BitConverter.ToInt32(buf, 0)",
            _        => "buf[0]",
        };
        if (enumDef is not null && b.CsTypeSimple != "byte[]" && b.CsTypeSimple != "string")
            return $"({enumDef.Name}){baseExpr}";
        return baseExpr;
    }

    private static string FormatLiteral(string csType, string key) => csType switch
    {
        "byte" or "ushort" or "uint" or "ulong" or "sbyte" or "short" or "int" or "long" => key,
        _ => $"\"{key}\"",
    };

    // ── String helpers ───────────────────────────────────────────────────────

    internal static string ToPascal(string s)
    {
        if (string.IsNullOrEmpty(s)) return s;
        var sb = new StringBuilder();
        bool upper = true;
        foreach (char c in s)
        {
            if (c == '_' || c == '-' || c == ' ') { upper = true; continue; }
            sb.Append(upper ? char.ToUpper(c) : c);
            upper = false;
        }
        return sb.ToString();
    }

    private static string ToEnumMember(string label)
    {
        if (string.IsNullOrEmpty(label)) return "Unknown";
        var sb = new StringBuilder();
        bool upper = true;
        foreach (char c in label)
        {
            if (!char.IsLetterOrDigit(c)) { upper = true; continue; }
            sb.Append(upper ? char.ToUpper(c) : c);
            upper = false;
        }
        var result = sb.ToString();
        // Prefix with underscore if starts with digit
        return result.Length > 0 && char.IsDigit(result[0]) ? "_" + result : result;
    }

    private static string EscapeXml(string s) => s.Replace("&", "&amp;").Replace("<", "&lt;").Replace(">", "&gt;");
    private static string EscapeString(string s) => s.Replace("\\", "\\\\").Replace("\"", "\\\"");
}
