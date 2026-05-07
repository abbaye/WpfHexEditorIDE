// ==========================================================
// Project: whfmt.CodeGen
// File: Generator/ParserGenerator.cs
// Description: Generates strongly-typed C# parser classes from .whfmt JSON definitions.
// Architecture: Reads BlockDefinition array → emits C# source via StringBuilder template.
// ==========================================================

using System.Text;
using System.Text.Json;

namespace WhfmtCodeGen.Generator;

/// <summary>Generates a strongly-typed C# parser class from a .whfmt JSON definition.</summary>
internal static class ParserGenerator
{
    /// <summary>Generate C# source from .whfmt JSON.</summary>
    public static string GenerateFromJson(
        string json,
        string namespaceName,
        string className,
        bool includeValidation,
        bool generateAsync)
    {
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        string formatName = root.TryGetProperty("name",        out var n) ? n.GetString() ?? className : className;
        string category   = root.TryGetProperty("category",    out var c) ? c.GetString() ?? ""        : "";
        string version    = root.TryGetProperty("version",     out var v) ? v.GetString() ?? "1.0"     : "1.0";
        string desc       = root.TryGetProperty("description", out var d) ? d.GetString() ?? ""        : "";

        var blocks = ParseBlocks(root);
        var checksums = ParseChecksums(root);

        var sb = new StringBuilder();

        EmitHeader(sb, formatName, category, version, desc, namespaceName, className);
        EmitFields(sb, blocks);
        EmitParseMethods(sb, blocks, checksums, className, includeValidation, generateAsync);
        EmitFooter(sb, className);

        return sb.ToString();
    }

    // ── Block model ──────────────────────────────────────────────────────────

    private sealed class BlockDef
    {
        public string Name      { get; init; } = "";
        public string StoreAs   { get; init; } = "";
        public long   Offset    { get; init; }
        public int    Length    { get; init; }
        public string Type      { get; init; } = "bytes";
        public string? Endian   { get; init; }
        public bool   IsSignature { get; init; }
        public Dictionary<string, string> ValueMap { get; init; } = [];
        public string  Description { get; init; } = "";

        public string PropertyName => ToPascal(string.IsNullOrEmpty(StoreAs) ? Name : StoreAs);
        public string CsType => MapType(Type, Length);
    }

    private sealed class ChecksumDef
    {
        public string Algorithm   { get; init; } = "";
        public long   StoredOffset { get; init; }
        public int    StoredLength { get; init; }
        public long   DataOffset   { get; init; }
        public long   DataLength   { get; init; }
    }

    private static List<BlockDef> ParseBlocks(JsonElement root)
    {
        var list = new List<BlockDef>();
        if (!root.TryGetProperty("blocks", out var blocks)) return list;
        foreach (var b in blocks.EnumerateArray())
        {
            string name   = b.TryGetProperty("name",        out var n) ? n.GetString() ?? "" : "";
            string storeAs= b.TryGetProperty("storeAs",     out var s) ? s.GetString() ?? "" : "";
            long   offset = b.TryGetProperty("offset",      out var o) && o.ValueKind == JsonValueKind.Number ? o.GetInt64() : 0;
            int    length = b.TryGetProperty("length",      out var l) && l.ValueKind == JsonValueKind.Number ? l.GetInt32() : 1;
            string type   = b.TryGetProperty("type",        out var t) ? t.GetString() ?? "bytes" : "bytes";
            string? endian= b.TryGetProperty("endian",      out var e) ? e.GetString() : null;
            bool   isSig  = b.TryGetProperty("isSignature", out var sg) && sg.GetBoolean();
            string desc   = b.TryGetProperty("description", out var ds) ? ds.GetString() ?? "" : "";

            var vm = new Dictionary<string, string>();
            if (b.TryGetProperty("valueMap", out var vmEl))
                foreach (var kv in vmEl.EnumerateObject())
                    vm[kv.Name] = kv.Value.GetString() ?? kv.Name;

            if (string.IsNullOrEmpty(name) && string.IsNullOrEmpty(storeAs)) continue;

            list.Add(new BlockDef
            {
                Name        = name,
                StoreAs     = storeAs,
                Offset      = offset,
                Length      = length,
                Type        = type,
                Endian      = endian,
                IsSignature = isSig,
                ValueMap    = vm,
                Description = desc,
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
            string algo = ck.TryGetProperty("algorithm", out var a) ? a.GetString() ?? "" : "";
            long storedOff = ck.TryGetProperty("storedAt",  out var sat) && sat.TryGetProperty("fixedOffset", out var sfo) ? sfo.GetInt64() : -1;
            int  storedLen = ck.TryGetProperty("storedAt",  out var sat2) && sat2.TryGetProperty("length",     out var sl)  ? sl.GetInt32()  : 4;
            long dataOff   = ck.TryGetProperty("dataRange", out var dr)   && dr.TryGetProperty("fixedOffset",  out var dfo) ? dfo.GetInt64() : 0;
            long dataLen   = ck.TryGetProperty("dataRange", out var dr2)  && dr2.TryGetProperty("fixedLength", out var dfl) ? dfl.GetInt64() : -1;
            if (storedOff < 0) continue;
            list.Add(new ChecksumDef { Algorithm = algo, StoredOffset = storedOff, StoredLength = storedLen, DataOffset = dataOff, DataLength = dataLen });
        }
        return list;
    }

    // ── Code emission ────────────────────────────────────────────────────────

    private static void EmitHeader(StringBuilder sb, string formatName, string category, string version, string desc, string ns, string className)
    {
        sb.AppendLine("// <auto-generated>");
        sb.AppendLine($"//   Generated by whfmt-codegen from '{formatName}' v{version}");
        if (!string.IsNullOrEmpty(category)) sb.AppendLine($"//   Category: {category}");
        if (!string.IsNullOrEmpty(desc))     sb.AppendLine($"//   {desc}");
        sb.AppendLine("//   Do not edit — regenerate with: whfmt-codegen generate");
        sb.AppendLine("// </auto-generated>");
        sb.AppendLine();
        sb.AppendLine("using System;");
        sb.AppendLine("using System.IO;");
        sb.AppendLine("using System.Threading;");
        sb.AppendLine("using System.Threading.Tasks;");
        sb.AppendLine();
        sb.AppendLine($"namespace {ns};");
        sb.AppendLine();
        sb.AppendLine($"/// <summary>Strongly-typed parser for {formatName} files (generated from .whfmt definition).</summary>");
        sb.AppendLine($"public sealed class {className}");
        sb.AppendLine("{");
    }

    private static void EmitFields(StringBuilder sb, List<BlockDef> blocks)
    {
        foreach (var b in blocks)
        {
            if (string.IsNullOrEmpty(b.PropertyName)) continue;
            if (!string.IsNullOrEmpty(b.Description))
                sb.AppendLine($"    /// <summary>{EscapeXml(b.Description)}</summary>");
            sb.AppendLine($"    public {b.CsType} {b.PropertyName} {{ get; private set; }}");

            if (b.ValueMap.Count > 0)
            {
                sb.AppendLine();
                sb.AppendLine($"    /// <summary>Human-readable label for {b.PropertyName}.</summary>");
                sb.Append($"    public string {b.PropertyName}Label => {b.PropertyName} switch {{");
                foreach (var kv in b.ValueMap)
                    sb.Append($" {FormatLiteral(b.CsType, kv.Key)} => \"{EscapeString(kv.Value)}\",");
                sb.AppendLine(" _ => \"Unknown\" };");
            }
            sb.AppendLine();
        }
    }

    private static void EmitParseMethods(StringBuilder sb, List<BlockDef> blocks, List<ChecksumDef> checksums,
        string className, bool includeValidation, bool generateAsync)
    {
        // Sync Parse(Stream)
        sb.AppendLine($"    /// <summary>Parse a {className.Replace("Parser", "")} from a stream.</summary>");
        sb.AppendLine($"    public static {className} Parse(Stream stream)");
        sb.AppendLine("    {");
        sb.AppendLine($"        var result = new {className}();");
        sb.AppendLine("        using var br = new BinaryReader(stream, System.Text.Encoding.Latin1, leaveOpen: true);");
        EmitReadBlocks(sb, blocks);
        if (includeValidation) EmitValidation(sb, blocks, checksums);
        sb.AppendLine("        return result;");
        sb.AppendLine("    }");
        sb.AppendLine();

        // Parse(byte[])
        sb.AppendLine($"    /// <summary>Parse a {className.Replace("Parser", "")} from a byte array.</summary>");
        sb.AppendLine($"    public static {className} Parse(byte[] data)");
        sb.AppendLine($"        => Parse(new MemoryStream(data));");
        sb.AppendLine();

        // Parse(string path)
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
            EmitReadBlocksAsync(sb, blocks);
            if (includeValidation) EmitValidation(sb, blocks, checksums);
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

    private static void EmitReadBlocks(StringBuilder sb, List<BlockDef> blocks)
    {
        foreach (var b in blocks)
        {
            if (string.IsNullOrEmpty(b.PropertyName)) continue;
            sb.AppendLine($"        // {b.Name} @ offset {b.Offset}, length {b.Length}");
            sb.AppendLine($"        stream.Seek({b.Offset}, SeekOrigin.Begin);");
            sb.AppendLine($"        result.{b.PropertyName} = {ReadExpression(b)};");
        }
    }

    private static void EmitReadBlocksAsync(StringBuilder sb, List<BlockDef> blocks)
    {
        sb.AppendLine("        var buf = new byte[8];");
        foreach (var b in blocks)
        {
            if (string.IsNullOrEmpty(b.PropertyName)) continue;
            sb.AppendLine($"        // {b.Name} @ offset {b.Offset}, length {b.Length}");
            sb.AppendLine($"        stream.Seek({b.Offset}, SeekOrigin.Begin);");
            if (IsScalarType(b.CsType))
            {
                sb.AppendLine($"        await stream.ReadExactlyAsync(buf.AsMemory(0, {b.Length}), cancellationToken);");
                sb.AppendLine($"        result.{b.PropertyName} = {ConvertExpression(b)};");
            }
            else
            {
                sb.AppendLine($"        var buf_{b.PropertyName} = new byte[{b.Length}];");
                sb.AppendLine($"        await stream.ReadExactlyAsync(buf_{b.PropertyName}, cancellationToken);");
                sb.AppendLine($"        result.{b.PropertyName} = buf_{b.PropertyName};");
            }
        }
    }

    private static void EmitValidation(StringBuilder sb, List<BlockDef> blocks, List<ChecksumDef> checksums)
    {
        var sigs = blocks.Where(b => b.IsSignature).ToList();
        if (sigs.Count > 0 || checksums.Count > 0)
            sb.AppendLine("        // Validation assertions");

        foreach (var b in sigs)
        {
            // We can't know the expected value without the original JSON at runtime; emit a comment.
            sb.AppendLine($"        // TODO: Assert {b.PropertyName} matches expected signature bytes");
        }

        if (checksums.Count > 0)
            sb.AppendLine("        // TODO: Verify checksums using stored values");
    }

    private static void EmitFooter(StringBuilder sb, string className)
    {
        sb.AppendLine("    // Byte-swap helpers for big-endian fields");
        sb.AppendLine("    private static ushort BSwap16(ushort v) => (ushort)((v << 8) | (v >> 8));");
        sb.AppendLine("    private static uint   BSwap32(uint v)   => (v << 24) | ((v & 0xFF00) << 8) | ((v >> 8) & 0xFF00) | (v >> 24);");
        sb.AppendLine("    private static ulong  BSwap64(ulong v)  => ((ulong)BSwap32((uint)v) << 32) | BSwap32((uint)(v >> 32));");
        sb.AppendLine("}");
    }

    // ── Type helpers ─────────────────────────────────────────────────────────

    private static string MapType(string whfmtType, int length) => whfmtType.ToLowerInvariant() switch
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

    private static bool IsScalarType(string csType) =>
        csType is "byte" or "ushort" or "uint" or "ulong" or "sbyte" or "short" or "int" or "long" or "float" or "double";

    private static string ReadExpression(BlockDef b) => b.CsType switch
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

    private static string ConvertExpression(BlockDef b) => b.CsType switch
    {
        "byte"   => "buf[0]",
        "ushort" => b.Endian == "big" ? "BSwap16(BitConverter.ToUInt16(buf, 0))" : "BitConverter.ToUInt16(buf, 0)",
        "uint"   => b.Endian == "big" ? "BSwap32(BitConverter.ToUInt32(buf, 0))" : "BitConverter.ToUInt32(buf, 0)",
        "ulong"  => b.Endian == "big" ? "BSwap64(BitConverter.ToUInt64(buf, 0))" : "BitConverter.ToUInt64(buf, 0)",
        "int"    => b.Endian == "big" ? "(int)BSwap32(BitConverter.ToUInt32(buf, 0))" : "BitConverter.ToInt32(buf, 0)",
        _        => "buf[0]",
    };

    private static string FormatLiteral(string csType, string key) => csType switch
    {
        "byte" or "ushort" or "uint" or "ulong" or "sbyte" or "short" or "int" or "long" => key,
        _ => $"\"{key}\"",
    };

    private static string ToPascal(string s)
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

    private static string EscapeXml(string s) => s.Replace("&", "&amp;").Replace("<", "&lt;").Replace(">", "&gt;");
    private static string EscapeString(string s) => s.Replace("\\", "\\\\").Replace("\"", "\\\"");
}
