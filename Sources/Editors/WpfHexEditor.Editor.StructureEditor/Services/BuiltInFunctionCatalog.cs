//////////////////////////////////////////////
// GNU Affero General Public License v3.0 - 2026
// Author : Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.6
//////////////////////////////////////////////
// Project: WpfHexEditor.Editor.StructureEditor
// File: Services/BuiltInFunctionCatalog.cs
// Description: Lazy reflection catalog over BuiltInFunctions.
//              Produces ExpressionCompleteSuggestion items for all 57 public methods.
//              Also exposes the list of auto-generated output variables.
// Architecture Notes:
//     Built once via static Lazy<> — reflection cost is ~57 methods, negligible.
//     No XML-doc loading attempted; signatures are derived from MethodInfo directly.
//////////////////////////////////////////////

using System.Reflection;
using WpfHexEditor.Core.FormatDetection;
using WpfHexEditor.Editor.StructureEditor.Models;

namespace WpfHexEditor.Editor.StructureEditor.Services;

internal static class BuiltInFunctionCatalog
{
    // ── Function suggestions ──────────────────────────────────────────────────

    private static readonly Lazy<IReadOnlyList<ExpressionCompleteSuggestion>> _all =
        new(BuildFunctionSuggestions, isThreadSafe: false);

    /// <summary>All public methods of <see cref="BuiltInFunctions"/> as suggestions.</summary>
    internal static IReadOnlyList<ExpressionCompleteSuggestion> All => _all.Value;

    // ── Built-in output variables ─────────────────────────────────────────────

    /// <summary>
    /// Variable names automatically set by built-in function calls.
    /// These appear after calling DetectBOM(), CountLines(), CalculateCRC32(), etc.
    /// </summary>
    internal static readonly IReadOnlyList<string> BuiltInOutputVars =
    [
        // DetectBOM()
        "bomDetected", "bomSize", "encoding",
        // CountLines()
        "lineCount",
        // DetectLineEnding()
        "lineEnding", "lfCount", "crlfCount", "crCount",
        // CalculateCRC32()
        "crc32",
        // ValidateCRC32()
        "crc32Valid",
        // CalculateMD5()
        "md5",
        // CalculateSHA1()
        "sha1",
        // CalculateSHA256()
        "sha256",
        // ReadPNGDimensions() / ValidatePNGIHDR()
        "pngWidth", "pngHeight", "pngBitDepth", "pngColorType",
        // ParseJPEGDimensions()
        "jpegWidth", "jpegHeight",
        // ParseMP3Header()
        "mp3Bitrate", "mp3SampleRate", "mp3Channels", "mp3TotalFrames",
        // ParseWAVFormatChunk()
        "wavChannels", "wavSampleRate", "wavBitsPerSample",
        // ParseBMPInfoHeader()
        "bmpWidth", "bmpHeight", "bmpBitCount",
        // ParseFLACStreamInfo()
        "flacSampleRate", "flacChannels", "flacBitsPerSample", "flacTotalSamples",
        // ParseGIFHeader()
        "gifWidth", "gifHeight",
        // ParseELFHeader()
        "elfBitWidth", "elfEndian", "elfOsAbi", "elfMachine",
        // ParseSQLiteHeader()
        "sqlitePageSize", "sqlitePageCount",
        // ComputeFromVariables()
        "computeResult",
    ];

    // ── Builder ───────────────────────────────────────────────────────────────

    private static IReadOnlyList<ExpressionCompleteSuggestion> BuildFunctionSuggestions()
    {
        var methods = typeof(BuiltInFunctions)
            .GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
            .Where(m => !m.IsSpecialName)  // skip property accessors
            .OrderBy(m => m.Name, StringComparer.OrdinalIgnoreCase);

        var result = new List<ExpressionCompleteSuggestion>();

        foreach (var m in methods)
        {
            var sig  = BuildSignature(m);
            var doc  = BuildDoc(m);
            var insert = m.GetParameters().Length == 0
                ? m.Name + "()"
                : m.Name + "(";

            result.Add(new ExpressionCompleteSuggestion
            {
                DisplayText   = m.Name,
                InsertText    = insert,
                Icon          = "\uE8F4",   // Segoe MDL2: Code / function glyph
                TypeHint      = "function",
                Documentation = sig + (doc is not null ? "\n" + doc : ""),
                SortPriority  = 10,
                CursorOffset  = m.GetParameters().Length == 0 ? 0 : 1,
            });
        }

        return result;
    }

    private static string BuildSignature(MethodInfo m)
    {
        var parms = m.GetParameters()
            .Select(p => $"{FriendlyType(p.ParameterType)} {p.Name}");
        return $"{m.Name}({string.Join(", ", parms)})";
    }

    private static string FriendlyType(Type t)
    {
        if (t == typeof(long))   return "long";
        if (t == typeof(int))    return "int";
        if (t == typeof(uint))   return "uint";
        if (t == typeof(ulong))  return "ulong";
        if (t == typeof(string)) return "string";
        if (t == typeof(bool))   return "bool";
        if (t == typeof(double)) return "double";
        if (t == typeof(float))  return "float";
        if (t == typeof(byte[])) return "byte[]";
        return t.Name;
    }

    private static string? BuildDoc(MethodInfo m) =>
        // Could load XML-doc; for now return null — signature alone is sufficient.
        null;
}
