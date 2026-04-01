//////////////////////////////////////////////
// GNU Affero General Public License v3.0 - 2026
// Author : Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.6
//////////////////////////////////////////////

using System.IO;
using System.Text.Json;
using WpfHexEditor.Core.CharacterTable;
using WpfHexEditor.Editor.TblEditor.Models;

namespace WpfHexEditor.Editor.TblEditor.Services;

/// <summary>
/// Service for importing TBL entries from various formats
/// </summary>
public class TblImportService
{
    #region CSV Import

    public TblImportResult ImportFromCsv(string filePath, CsvImportOptions? options = null)
    {
        options ??= new CsvImportOptions();
        try
        {
            var csvContent = File.ReadAllText(filePath, options.Encoding);
            return ImportFromCsvString(csvContent, options);
        }
        catch (Exception ex)
        {
            return new TblImportResult { Success = false, Errors = [$"Failed to read CSV file: {ex.Message}"] };
        }
    }

    public TblImportResult ImportFromCsvString(string csvContent, CsvImportOptions? options = null)
    {
        options ??= new CsvImportOptions();
        var result = new TblImportResult { DetectedFormat = TblFileFormat.Csv };
        try
        {
            var lines = csvContent.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries);
            if (lines.Length == 0) { result.Errors.Add("CSV file is empty"); return result; }

            int startIndex = 0;
            Dictionary<string, int>? columnMap = null;
            if (options.HasHeader) { columnMap = ParseCsvHeader(lines[0], options.Delimiter); startIndex = 1; }

            for (int i = startIndex; i < lines.Length; i++)
            {
                try
                {
                    var entry = ParseCsvRow(lines[i], options.Delimiter, columnMap, options);
                    if (entry != null && entry.IsValid) { result.Entries.Add(entry); result.ImportedCount++; }
                    else if (!options.SkipInvalidRows) { result.Errors.Add($"Row {i + 1}: Invalid entry"); result.Success = false; return result; }
                    else { result.SkippedCount++; result.Warnings.Add($"Row {i + 1}: Skipped invalid entry"); }
                }
                catch (Exception ex)
                {
                    if (options.SkipInvalidRows) { result.SkippedCount++; result.Warnings.Add($"Row {i + 1}: {ex.Message}"); }
                    else { result.Errors.Add($"Row {i + 1}: {ex.Message}"); result.Success = false; return result; }
                }
            }
            result.Success = true;
        }
        catch (Exception ex) { result.Success = false; result.Errors.Add($"CSV parsing failed: {ex.Message}"); }
        return result;
    }

    private Dictionary<string, int> ParseCsvHeader(string headerLine, string delimiter)
    {
        var columns = ParseCsvLine(headerLine, delimiter);
        var map = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        for (int i = 0; i < columns.Count; i++) map[columns[i]] = i;
        return map;
    }

    private Dte? ParseCsvRow(string line, string delimiter, Dictionary<string, int>? columnMap, CsvImportOptions options)
    {
        var values = ParseCsvLine(line, delimiter);
        if (values.Count == 0) return null;

        string hex, value;
        string? comment = null;

        if (columnMap != null)
        {
            if (!columnMap.TryGetValue("Hex", out int hexIndex)) throw new Exception("Required column 'Hex' not found");
            if (!columnMap.TryGetValue("Value", out int valueIndex) &&
                !columnMap.TryGetValue("Character", out valueIndex) &&
                !columnMap.TryGetValue("Character(s)", out valueIndex)) throw new Exception("Required column 'Value' or 'Character(s)' not found");
            hex = values[hexIndex];
            value = values[valueIndex];
            if (columnMap.TryGetValue("Comment", out int commentIndex) && commentIndex < values.Count) comment = values[commentIndex];
        }
        else
        {
            if (values.Count < 2) return null;
            hex = values[0]; value = values[1];
            if (values.Count > 2) comment = values[2];
        }

        var dte = new Dte(hex, value);
        if (!string.IsNullOrWhiteSpace(comment)) dte.Comment = comment;
        return dte;
    }

    private List<string> ParseCsvLine(string line, string delimiter)
    {
        var result = new List<string>();
        bool inQuotes = false;
        var current = new System.Text.StringBuilder();
        foreach (char c in line)
        {
            if (c == '"') inQuotes = !inQuotes;
            else if (c == delimiter[0] && !inQuotes) { result.Add(current.ToString().Trim()); current.Clear(); }
            else current.Append(c);
        }
        result.Add(current.ToString().Trim());
        return result;
    }

    #endregion

    #region JSON Import

    public TblImportResult ImportFromJson(string filePath, JsonImportOptions? options = null)
    {
        options ??= new JsonImportOptions();
        try
        {
            var jsonContent = File.ReadAllText(filePath);
            return ImportFromJsonString(jsonContent, options);
        }
        catch (Exception ex)
        {
            return new TblImportResult { Success = false, Errors = [$"Failed to read JSON file: {ex.Message}"] };
        }
    }

    public TblImportResult ImportFromJsonString(string jsonContent, JsonImportOptions? options = null)
    {
        options ??= new JsonImportOptions();
        var result = new TblImportResult { DetectedFormat = TblFileFormat.Json };
        try
        {
            using var document = JsonDocument.Parse(jsonContent);
            var root = document.RootElement;
            JsonElement entriesElement;

            if (root.ValueKind == JsonValueKind.Array) entriesElement = root;
            else if (root.ValueKind == JsonValueKind.Object && root.TryGetProperty("entries", out entriesElement)) { }
            else { result.Errors.Add("Invalid JSON structure. Expected array or object with 'entries' property"); return result; }

            foreach (var entryElement in entriesElement.EnumerateArray())
            {
                try
                {
                    var entry = ParseJsonEntry(entryElement, options);
                    if (entry != null && entry.IsValid) { result.Entries.Add(entry); result.ImportedCount++; }
                    else if (!options.SkipInvalidEntries) { result.Errors.Add("Invalid entry found"); result.Success = false; return result; }
                    else { result.SkippedCount++; result.Warnings.Add("Skipped invalid entry"); }
                }
                catch (Exception ex)
                {
                    if (options.SkipInvalidEntries) { result.SkippedCount++; result.Warnings.Add($"Entry error: {ex.Message}"); }
                    else { result.Errors.Add($"Entry error: {ex.Message}"); result.Success = false; return result; }
                }
            }
            result.Success = true;
        }
        catch (Exception ex) { result.Success = false; result.Errors.Add($"JSON parsing failed: {ex.Message}"); }
        return result;
    }

    private Dte? ParseJsonEntry(JsonElement entryElement, JsonImportOptions options)
    {
        string? hex = null;
        if (entryElement.TryGetProperty(options.HexPropertyName, out var hexElement)) hex = hexElement.GetString();
        else if (entryElement.TryGetProperty("entry", out hexElement)) hex = hexElement.GetString();
        else if (entryElement.TryGetProperty("Entry", out hexElement)) hex = hexElement.GetString();

        string? value = null;
        if (entryElement.TryGetProperty(options.ValuePropertyName, out var valueElement)) value = valueElement.GetString();
        else if (entryElement.TryGetProperty("Value", out valueElement)) value = valueElement.GetString();
        else if (entryElement.TryGetProperty("character", out valueElement)) value = valueElement.GetString();

        if (string.IsNullOrWhiteSpace(hex) || string.IsNullOrWhiteSpace(value)) return null;

        var dte = new Dte(hex, value);
        if (entryElement.TryGetProperty("comment", out var commentElement) ||
            entryElement.TryGetProperty("Comment", out commentElement))
        {
            var comment = commentElement.GetString();
            if (!string.IsNullOrWhiteSpace(comment)) dte.Comment = comment;
        }
        return dte;
    }

    #endregion

    #region Auto-detect Format

    public TblImportResult ImportFromFile(string filePath)
    {
        var extension = Path.GetExtension(filePath)?.ToLowerInvariant();
        return extension switch
        {
            ".csv"  => ImportFromCsv(filePath),
            ".json" => ImportFromJson(filePath),
            ".tbl"  => ImportFromTbl(filePath),
            ".tblx" => new TblxService().ImportFromTblxFile(filePath),
            _       => new TblImportResult { Success = false, Errors = [$"Unsupported file format: {extension}"] }
        };
    }

    private TblImportResult ImportFromTbl(string filePath)
    {
        // Detect variant (Thingy vs Atlas) before loading; TblStream.Load() handles Atlas
        // normalization internally, so we just need to capture the detected format.
        var detectedFormat = TblStream.DetectFileFormat(filePath); // may return Atlas or Tbl
        var result = new TblImportResult { DetectedFormat = detectedFormat };
        try
        {
            // TblStream constructor calls FileName setter → Load() → Atlas-aware parsing
            var tbl = new TblStream(filePath);
            result.Entries = tbl.GetAllEntries().ToList();
            result.ImportedCount = result.Entries.Count;
            result.Success = true;
        }
        catch (Exception ex) { result.Success = false; result.Errors.Add($"Failed to load TBL file: {ex.Message}"); }
        return result;
    }

    #endregion
}
