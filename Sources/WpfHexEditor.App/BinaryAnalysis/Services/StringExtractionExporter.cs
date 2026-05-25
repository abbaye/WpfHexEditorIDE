//////////////////////////////////////////////
// GNU Affero General Public License v3.0 - 2026
// Author : Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.6
//////////////////////////////////////////////

using System.IO;
using System.Text;
using System.Text.Json;

namespace WpfHexEditor.App.BinaryAnalysis.Services;

/// <summary>Contract for exporting a list of <see cref="StringRun"/> results to a file.</summary>
public interface IStringExtractionExporter
{
    string Format      { get; }
    string FileFilter  { get; }
    string DefaultExt  { get; }
    Task ExportAsync(IEnumerable<StringRun> runs, string path, CancellationToken ct = default);
}

/// <summary>Exports string runs as plain text — one string per line.</summary>
public sealed class TxtStringExporter : IStringExtractionExporter
{
    public string Format     => "TXT";
    public string FileFilter => "Text Files (*.txt)|*.txt";
    public string DefaultExt => ".txt";

    public async Task ExportAsync(IEnumerable<StringRun> runs, string path, CancellationToken ct = default)
    {
        await using var sw = new StreamWriter(path, append: false, Encoding.UTF8);
        foreach (var run in runs)
        {
            ct.ThrowIfCancellationRequested();
            await sw.WriteLineAsync(run.Value);
        }
    }
}

/// <summary>Exports string runs as CSV with headers: Offset,Length,Encoding,Value.</summary>
public sealed class CsvStringExporter : IStringExtractionExporter
{
    public string Format     => "CSV";
    public string FileFilter => "CSV Files (*.csv)|*.csv";
    public string DefaultExt => ".csv";

    public async Task ExportAsync(IEnumerable<StringRun> runs, string path, CancellationToken ct = default)
    {
        await using var sw = new StreamWriter(path, append: false, Encoding.UTF8);
        await sw.WriteLineAsync("Offset,Length,Encoding,Bytes,Value");
        foreach (var run in runs)
        {
            ct.ThrowIfCancellationRequested();
            var escaped = run.Value.Replace("\"", "\"\"");
            await sw.WriteLineAsync($"0x{run.Offset:X8},{run.Length},{run.Encoding},\"{run.RawHex}\",\"{escaped}\"");
        }
    }
}

/// <summary>Exports string runs as a JSON array.</summary>
public sealed class JsonStringExporter : IStringExtractionExporter
{
    public string Format     => "JSON";
    public string FileFilter => "JSON Files (*.json)|*.json";
    public string DefaultExt => ".json";

    public async Task ExportAsync(IEnumerable<StringRun> runs, string path, CancellationToken ct = default)
    {
        var list = runs.Select(r => new
        {
            Offset   = $"0x{r.Offset:X8}",
            Length   = r.Length,
            Encoding = r.Encoding.ToString(),
            Bytes    = r.RawHex,
            Value    = r.Value,
        }).ToList();

        var json = JsonSerializer.Serialize(list, new JsonSerializerOptions { WriteIndented = true });
        await File.WriteAllTextAsync(path, json, Encoding.UTF8, ct);
    }
}

/// <summary>Exports string runs as an XML document.</summary>
public sealed class XmlStringExporter : IStringExtractionExporter
{
    public string Format     => "XML";
    public string FileFilter => "XML Files (*.xml)|*.xml";
    public string DefaultExt => ".xml";

    public async Task ExportAsync(IEnumerable<StringRun> runs, string path, CancellationToken ct = default)
    {
        await using var sw = new StreamWriter(path, append: false, Encoding.UTF8);
        await sw.WriteLineAsync("<?xml version=\"1.0\" encoding=\"utf-8\"?>");
        await sw.WriteLineAsync("<strings>");
        foreach (var run in runs)
        {
            ct.ThrowIfCancellationRequested();
            var escaped = System.Security.SecurityElement.Escape(run.Value) ?? run.Value;
            await sw.WriteLineAsync($"  <string offset=\"0x{run.Offset:X8}\" length=\"{run.Length}\" encoding=\"{run.Encoding}\" bytes=\"{run.RawHex}\">{escaped}</string>");
        }
        await sw.WriteLineAsync("</strings>");
    }
}

/// <summary>Exports string runs as a Markdown table.</summary>
public sealed class MarkdownStringExporter : IStringExtractionExporter
{
    public string Format     => "Markdown";
    public string FileFilter => "Markdown Files (*.md)|*.md";
    public string DefaultExt => ".md";

    public async Task ExportAsync(IEnumerable<StringRun> runs, string path, CancellationToken ct = default)
    {
        await using var sw = new StreamWriter(path, append: false, Encoding.UTF8);
        await sw.WriteLineAsync("| Offset | Length | Encoding | Bytes | Value |");
        await sw.WriteLineAsync("|--------|--------|----------|-------|-------|");
        foreach (var run in runs)
        {
            ct.ThrowIfCancellationRequested();
            var escaped = run.Value.Replace("|", "\\|").Replace("\n", " ").Replace("\r", "");
            await sw.WriteLineAsync($"| `0x{run.Offset:X8}` | {run.Length} | {run.Encoding} | `{run.RawHex}` | {escaped} |");
        }
    }
}

/// <summary>Exports string runs as a C byte-array header — one array per run.</summary>
public sealed class CArrayStringExporter : IStringExtractionExporter
{
    public string Format     => "C Array";
    public string FileFilter => "C Header Files (*.h)|*.h";
    public string DefaultExt => ".h";

    public async Task ExportAsync(IEnumerable<StringRun> runs, string path, CancellationToken ct = default)
    {
        await using var sw = new StreamWriter(path, append: false, Encoding.UTF8);
        await sw.WriteLineAsync("/* Generated by WpfHexEditor StringExtractor */");
        await sw.WriteLineAsync("#pragma once");
        await sw.WriteLineAsync("#include <stdint.h>");
        await sw.WriteLineAsync();
        foreach (var run in runs)
        {
            ct.ThrowIfCancellationRequested();
            var hex  = run.RawHex.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            var name = $"str_{run.Offset:X8}";
            await sw.WriteAsync($"static const uint8_t {name}[] = {{ ");
            await sw.WriteAsync(string.Join(", ", hex.Select(h => $"0x{h}")));
            await sw.WriteLineAsync($" }}; /* [{run.Encoding}] {run.Value} */");
        }
    }
}

/// <summary>Exports string runs as a SARIF 2.1.0 log with one result per run.</summary>
public sealed class SarifStringExporter : IStringExtractionExporter
{
    public string Format     => "SARIF";
    public string FileFilter => "SARIF Files (*.sarif)|*.sarif";
    public string DefaultExt => ".sarif";

    public async Task ExportAsync(IEnumerable<StringRun> runs, string path, CancellationToken ct = default)
    {
        var results = runs.Select(r => new
        {
            ruleId  = "StringExtraction",
            message = new { text = $"[{r.Encoding}] {r.Value}" },
            locations = new[]
            {
                new
                {
                    physicalLocation = new
                    {
                        artifactLocation = new { uri = "binary" },
                        region = new { byteOffset = r.Offset, byteLength = r.Length },
                    },
                }
            },
            properties = new { encoding = r.Encoding.ToString(), rawHex = r.RawHex },
        }).ToArray();

        var sarif = new
        {
            version = "2.1.0",
            runs = new[]
            {
                new
                {
                    tool = new { driver = new { name = "WpfHexEditor.StringExtractor", version = "1.0" } },
                    results,
                }
            }
        };

        var json = JsonSerializer.Serialize(sarif, new JsonSerializerOptions { WriteIndented = true });
        await File.WriteAllTextAsync(path, json, Encoding.UTF8, ct);
    }
}

/// <summary>Exports string runs as a .whann (WpfHexEditor Annotations) JSON file.</summary>
public sealed class WhannStringExporter : IStringExtractionExporter
{
    public string Format     => "Annotations";
    public string FileFilter => "WpfHexEditor Annotations (*.whann)|*.whann";
    public string DefaultExt => ".whann";

    public async Task ExportAsync(IEnumerable<StringRun> runs, string path, CancellationToken ct = default)
    {
        var annotations = runs.Select(r => new
        {
            offset   = r.Offset,
            length   = r.Length,
            label    = r.Value,
            encoding = r.Encoding.ToString(),
            category = "StringExtraction",
            color    = EncodingColor(r.Encoding),
        }).ToArray();

        var doc = new { version = "1.0", annotations };
        var json = JsonSerializer.Serialize(doc, new JsonSerializerOptions { WriteIndented = true });
        await File.WriteAllTextAsync(path, json, Encoding.UTF8, ct);
    }

    private static string EncodingColor(StringEncoding enc) => EncodingPalette.HexColor(enc);
}

/// <summary>Registry of all available exporters, keyed by format name.</summary>
public static class StringExtractionExporters
{
    public static readonly IReadOnlyList<IStringExtractionExporter> All =
    [
        new TxtStringExporter(),
        new CsvStringExporter(),
        new JsonStringExporter(),
        new XmlStringExporter(),
        new MarkdownStringExporter(),
        new CArrayStringExporter(),
        new SarifStringExporter(),
        new WhannStringExporter(),
    ];
}
