// ==========================================================
// Project: WpfHexEditor.Editor.StructureEditor
// File: Services/TemplatePackageService.cs
// Description:
//     Import and export of .whfmt templates in multiple formats:
//       - Json        — the .whfmt JSON itself
//       - CStruct     — generates a C-style typedef struct
//       - PythonBytes — generates a struct.pack format string
//     Import currently supports .whfmt JSON only.
// Architecture: pure C#, deterministic, file-system aware, no UI types.
// ==========================================================

using System.IO;
using System.Text;
using System.Text.Json;
using WpfHexEditor.Core.FormatDetection;

namespace WpfHexEditor.Editor.StructureEditor.Services;

/// <summary>Output format supported by <see cref="TemplatePackageService"/>.</summary>
public enum TemplateExportFormat
{
    Json,
    CStruct,
    PythonBytes,
}

/// <summary>Import/export helpers for binary template packages.</summary>
public sealed class TemplatePackageService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented          = true,
        PropertyNamingPolicy   = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull,
    };

    internal static readonly JsonSerializerOptions ImportOptions = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    internal static readonly JsonSerializerOptions WriteIndentedOptions = new()
    {
        WriteIndented          = true,
        PropertyNamingPolicy   = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull,
    };

    /// <summary>Exports <paramref name="def"/> to <paramref name="outputPath"/> in the requested format.</summary>
    public async Task ExportAsync(FormatDefinition def, string outputPath, TemplateExportFormat format)
    {
        var content = format switch
        {
            TemplateExportFormat.Json        => JsonSerializer.Serialize(def, JsonOptions),
            TemplateExportFormat.CStruct     => BuildCStruct(def),
            TemplateExportFormat.PythonBytes => BuildPythonBytes(def),
            _                                => throw new ArgumentOutOfRangeException(nameof(format)),
        };
        await File.WriteAllTextAsync(outputPath, content).ConfigureAwait(false);
    }

    /// <summary>Imports a .whfmt JSON file into a <see cref="FormatDefinition"/>; null on failure.</summary>
    public async Task<FormatDefinition?> ImportAsync(string filePath)
    {
        try
        {
            var json = await File.ReadAllTextAsync(filePath).ConfigureAwait(false);
            return JsonSerializer.Deserialize<FormatDefinition>(json, ImportOptions);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[TemplatePackageService] import failed: {filePath} — {ex.Message}");
            return null;
        }
    }

    /// <summary>
    /// Merges blocks and variables from <paramref name="source"/> into <paramref name="target"/>.
    /// Blocks with duplicate names are renamed with a "_merged" suffix.
    /// Variables that already exist in target are skipped.
    /// </summary>
    public static FormatDefinition Merge(FormatDefinition target, FormatDefinition source)
    {
        var existingNames = new HashSet<string>(
            (target.Blocks ?? []).Select(b => b.Name ?? "").Where(n => n.Length > 0),
            StringComparer.Ordinal);

        foreach (var block in source.Blocks ?? [])
        {
            if (block is null) continue;
            var name = !string.IsNullOrEmpty(block.Name) && existingNames.Contains(block.Name!)
                ? block.Name + "_merged"
                : block.Name;
            block.Name = name;
            (target.Blocks ??= []).Add(block);
        }

        foreach (var kv in source.Variables ?? new Dictionary<string, object>())
        {
            (target.Variables ??= new Dictionary<string, object>())
                .TryAdd(kv.Key, kv.Value);
        }

        return target;
    }

    // ── C struct ──────────────────────────────────────────────────────────────

    private static string BuildCStruct(FormatDefinition def)
    {
        var sb = new StringBuilder();
        var name = SanitizeIdentifier(def.FormatName) ?? "GeneratedStruct";
        sb.AppendLine($"// Auto-generated from {def.FormatName} v{def.Version}");
        sb.AppendLine("#pragma pack(push, 1)");
        sb.AppendLine($"typedef struct {name} {{");
        foreach (var f in FlattenFields(def.Blocks))
            sb.AppendLine($"    {MapCType(f.ValueType, f.Length)} {SanitizeIdentifier(f.Name)};");
        sb.AppendLine($"}} {name};");
        sb.AppendLine("#pragma pack(pop)");
        return sb.ToString();
    }

    private static string MapCType(string? valueType, object? length)
    {
        return valueType?.ToLowerInvariant() switch
        {
            "uint8"  => "uint8_t",
            "uint16" => "uint16_t",
            "uint32" => "uint32_t",
            "uint64" => "uint64_t",
            "int8"   => "int8_t",
            "int16"  => "int16_t",
            "int32"  => "int32_t",
            "int64"  => "int64_t",
            "float"  => "float",
            "double" => "double",
            "ascii" or "utf8" => $"char[{LengthAsInt(length, 1)}]",
            _ => $"uint8_t[{LengthAsInt(length, 1)}]",
        };
    }

    // ── Python bytes / struct.pack ────────────────────────────────────────────

    private static string BuildPythonBytes(FormatDefinition def)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"# Auto-generated from {def.FormatName} v{def.Version}");
        sb.AppendLine("import struct");
        sb.AppendLine();
        var fmt = new StringBuilder(">");
        var names = new List<string>();
        foreach (var f in FlattenFields(def.Blocks))
        {
            fmt.Append(MapPyFormat(f.ValueType, f.Length));
            names.Add(SanitizeIdentifier(f.Name) ?? "field");
        }
        sb.AppendLine($"FORMAT = \"{fmt}\"");
        sb.AppendLine($"FIELDS = ({string.Join(", ", names.Select(n => $"\"{n}\""))})");
        sb.AppendLine();
        sb.AppendLine("def unpack(data: bytes) -> dict:");
        sb.AppendLine("    values = struct.unpack(FORMAT, data[:struct.calcsize(FORMAT)])");
        sb.AppendLine("    return dict(zip(FIELDS, values))");
        return sb.ToString();
    }

    private static string MapPyFormat(string? valueType, object? length) =>
        valueType?.ToLowerInvariant() switch
        {
            "uint8"  => "B",
            "uint16" => "H",
            "uint32" => "I",
            "uint64" => "Q",
            "int8"   => "b",
            "int16"  => "h",
            "int32"  => "i",
            "int64"  => "q",
            "float"  => "f",
            "double" => "d",
            "ascii" or "utf8" => $"{LengthAsInt(length, 1)}s",
            _ => $"{LengthAsInt(length, 1)}s",
        };

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static IEnumerable<BlockDefinition> FlattenFields(IEnumerable<BlockDefinition>? blocks)
    {
        if (blocks is null) yield break;
        foreach (var b in blocks)
        {
            if (b is null) continue;
            if (IsLeafField(b)) yield return b;
            foreach (var c in EnumerateChildren(b))
                if (IsLeafField(c)) yield return c;
        }
    }

    private static IEnumerable<BlockDefinition> EnumerateChildren(BlockDefinition b)
    {
        if (b.Fields is { } f) foreach (var c in f) yield return c;
        if (b.Then   is { } t) foreach (var c in t) yield return c;
        if (b.Body   is { } y) foreach (var c in y) yield return c;
    }

    private static bool IsLeafField(BlockDefinition b) =>
        string.Equals(b.Type, "field", StringComparison.OrdinalIgnoreCase)
        && !string.IsNullOrEmpty(b.Name);

    private static int LengthAsInt(object? length, int fallback)
    {
        if (length is null) return fallback;
        if (length is int i) return i;
        if (length is long l) return (int)l;
        if (int.TryParse(length.ToString(), out var parsed)) return parsed;
        return fallback;
    }

    private static string? SanitizeIdentifier(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return null;
        var sb = new StringBuilder(raw.Length);
        foreach (var ch in raw)
            sb.Append(char.IsLetterOrDigit(ch) || ch == '_' ? ch : '_');
        if (sb.Length > 0 && char.IsDigit(sb[0])) sb.Insert(0, '_');
        return sb.ToString();
    }
}
