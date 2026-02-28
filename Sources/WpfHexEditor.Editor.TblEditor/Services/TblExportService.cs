using System.IO;
using System.Text;
using System.Text.Json;
using WpfHexEditor.Core.CharacterTable;
using WpfHexEditor.Editor.TblEditor.Models;

namespace WpfHexEditor.Editor.TblEditor.Services;

/// <summary>Service for exporting TBL entries to various formats</summary>
public class TblExportService
{
    #region CSV Export

    public void ExportToCsvFile(IEnumerable<Dte> entries, string filePath, CsvExportOptions? options = null)
    {
        options ??= new CsvExportOptions();
        try { File.WriteAllText(filePath, ExportToCsv(entries, options), options.Encoding); }
        catch (Exception ex) { throw new Exception($"Failed to export to CSV file: {ex.Message}", ex); }
    }

    public string ExportToCsv(IEnumerable<Dte> entries, CsvExportOptions? options = null)
    {
        options ??= new CsvExportOptions();
        var sb = new StringBuilder();
        var headers = new List<string> { "Hex", "Character" };
        if (options.IncludeType) headers.Add("Type");
        if (options.IncludeByteCount) headers.Add("ByteCount");
        if (options.IncludeComment) headers.Add("Comment");
        sb.AppendLine(FormatCsvLine(headers, options));

        foreach (var entry in entries)
        {
            var values = new List<string> { entry.Entry, EscapeValue(entry.Value) };
            if (options.IncludeType) values.Add(entry.Type.ToString());
            if (options.IncludeByteCount) values.Add((entry.Entry.Length / 2).ToString());
            if (options.IncludeComment) values.Add(EscapeValue(entry.Comment ?? string.Empty));
            sb.AppendLine(FormatCsvLine(values, options));
        }
        return sb.ToString();
    }

    private string FormatCsvLine(IEnumerable<string> values, CsvExportOptions options) =>
        string.Join(options.Delimiter, values.Select(v =>
        {
            if (options.QuoteStrings) return $"\"{v.Replace("\"", "\"\"")}\"";
            if (v.Contains(options.Delimiter)) return $"\"{v.Replace("\"", "\"\"")}\"";
            return v;
        }));

    private string EscapeValue(string value) =>
        string.IsNullOrEmpty(value) ? string.Empty : value.Replace("\n", "\\n").Replace("\r", "\\r").Replace("\t", "\\t");

    #endregion

    #region JSON Export

    public void ExportToJsonFile(IEnumerable<Dte> entries, string filePath, JsonExportOptions? options = null)
    {
        options ??= new JsonExportOptions();
        try { File.WriteAllText(filePath, ExportToJson(entries, options), Encoding.UTF8); }
        catch (Exception ex) { throw new Exception($"Failed to export to JSON file: {ex.Message}", ex); }
    }

    public string ExportToJson(IEnumerable<Dte> entries, JsonExportOptions? options = null)
    {
        options ??= new JsonExportOptions();
        try
        {
            using var stream = new MemoryStream();
            using var writer = new Utf8JsonWriter(stream, new JsonWriterOptions { Indented = options.Indented });

            if (options.IncludeMetadata && options.Metadata != null)
            {
                writer.WriteStartObject();
                writer.WriteStartObject("metadata");
                writer.WriteString("version", options.Metadata.Version);
                if (!string.IsNullOrWhiteSpace(options.Metadata.Description)) writer.WriteString("description", options.Metadata.Description);
                if (!string.IsNullOrWhiteSpace(options.Metadata.Author)) writer.WriteString("author", options.Metadata.Author);
                writer.WriteString("createdDate", options.Metadata.CreatedDate ?? DateTime.Now.ToString("O"));
                writer.WriteEndObject();
                writer.WriteStartArray("entries");
                WriteEntries(writer, entries, options);
                writer.WriteEndArray();
                writer.WriteEndObject();
            }
            else
            {
                writer.WriteStartArray();
                WriteEntries(writer, entries, options);
                writer.WriteEndArray();
            }
            writer.Flush();
            return Encoding.UTF8.GetString(stream.ToArray());
        }
        catch (Exception ex) { throw new Exception($"Failed to export to JSON: {ex.Message}", ex); }
    }

    private void WriteEntries(Utf8JsonWriter writer, IEnumerable<Dte> entries, JsonExportOptions options)
    {
        foreach (var entry in entries)
        {
            writer.WriteStartObject();
            writer.WriteString(options.HexPropertyName, entry.Entry);
            writer.WriteString(options.ValuePropertyName, entry.Value);
            if (options.IncludeType) writer.WriteString("type", entry.Type.ToString());
            if (options.IncludeByteCount) writer.WriteNumber("byteCount", entry.Entry.Length / 2);
            if (options.IncludeComment && !string.IsNullOrWhiteSpace(entry.Comment)) writer.WriteString("comment", entry.Comment);
            writer.WriteEndObject();
        }
    }

    #endregion

    #region TBL Standard Export

    public void ExportToTblFile(IEnumerable<Dte> entries, string filePath)
    {
        try { File.WriteAllText(filePath, ExportToTbl(entries), Encoding.UTF8); }
        catch (Exception ex) { throw new Exception($"Failed to export to TBL file: {ex.Message}", ex); }
    }

    public string ExportToTbl(IEnumerable<Dte> entries)
    {
        var sb = new StringBuilder();
        foreach (var entry in entries)
        {
            var value = entry.Value.Replace("\n", "\\n").Replace("\r", "\\r").Replace("\t", "\\t");
            if (entry.Type == DteType.EndBlock) sb.AppendLine($"/{entry.Entry}");
            else if (entry.Type == DteType.EndLine) sb.AppendLine($"*{entry.Entry}");
            else sb.AppendLine($"{entry.Entry}={value}");
        }
        return sb.ToString();
    }

    #endregion

    #region Auto-detect Format Export

    public void ExportToFile(IEnumerable<Dte> entries, string filePath,
        CsvExportOptions? csvOptions = null, JsonExportOptions? jsonOptions = null, TblxMetadata? tblxMetadata = null)
    {
        var extension = Path.GetExtension(filePath)?.ToLowerInvariant();
        switch (extension)
        {
            case ".csv":  ExportToCsvFile(entries, filePath, csvOptions); break;
            case ".json": ExportToJsonFile(entries, filePath, jsonOptions); break;
            case ".tbl":  ExportToTblFile(entries, filePath); break;
            case ".tblx": new TblxService().ExportFromTblStream(new TblStream(), filePath, tblxMetadata); break;
            default: throw new NotSupportedException($"Unsupported export format: {extension}");
        }
    }

    #endregion
}
