//////////////////////////////////////////////
// Apache 2.0  - 2026
// Author : Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.6
//////////////////////////////////////////////

using System.IO;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using WpfHexEditor.Core.CharacterTable;
using WpfHexEditor.Editor.TblEditor.Models;

namespace WpfHexEditor.Editor.TblEditor.Services;

/// <summary>
/// Service for .tblx extended format operations
/// </summary>
public class TblxService
{
    private static readonly JsonSerializerOptions _jsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        Converters = { new JsonStringEnumConverter() }
    };

    public TblxDocument LoadFromFile(string filePath)
    {
        try { return LoadFromString(File.ReadAllText(filePath, Encoding.UTF8)); }
        catch (Exception ex) { throw new Exception($"Failed to load .tblx file: {ex.Message}", ex); }
    }

    public TblxDocument LoadFromString(string jsonContent)
    {
        try
        {
            var doc = JsonSerializer.Deserialize<TblxDocument>(jsonContent, _jsonOptions);
            if (doc?.Format != "tblx") throw new Exception("Invalid .tblx format: missing or incorrect format identifier");
            return doc;
        }
        catch (JsonException ex) { throw new Exception($"Invalid .tblx JSON: {ex.Message}", ex); }
    }

    public TblImportResult ImportFromTblxFile(string filePath)
    {
        var result = new TblImportResult { DetectedFormat = TblFileFormat.Tblx };
        try
        {
            var doc = LoadFromFile(filePath);
            foreach (var entry in doc.Entries)
            {
                try
                {
                    var dte = entry.ToDte();
                    if (dte.IsValid) { result.Entries.Add(dte); result.ImportedCount++; }
                    else { result.SkippedCount++; result.Warnings.Add($"Skipped invalid entry: {entry.Entry}"); }
                }
                catch (Exception ex) { result.SkippedCount++; result.Warnings.Add($"Entry {entry.Entry}: {ex.Message}"); }
            }
            result.Success = true;
        }
        catch (Exception ex) { result.Success = false; result.Errors.Add(ex.Message); }
        return result;
    }

    public void SaveToFile(TblxDocument document, string filePath)
    {
        try
        {
            document.Metadata.ModifiedDate = DateTime.Now;
            File.WriteAllText(filePath, SaveToString(document), Encoding.UTF8);
        }
        catch (Exception ex) { throw new Exception($"Failed to save .tblx file: {ex.Message}", ex); }
    }

    public string SaveToString(TblxDocument document)
    {
        try { return JsonSerializer.Serialize(document, _jsonOptions); }
        catch (Exception ex) { throw new Exception($"Failed to serialize .tblx: {ex.Message}", ex); }
    }

    public void ExportFromTblStream(TblStream tbl, string filePath, TblxMetadata? metadata = null)
    {
        try
        {
            var doc = TblxDocument.FromTblStream(tbl, metadata);
            if (doc.Metadata.CreatedDate == null) doc.Metadata.CreatedDate = DateTime.Now;
            SaveToFile(doc, filePath);
        }
        catch (Exception ex) { throw new Exception($"Failed to export .tblx: {ex.Message}", ex); }
    }

    public void ExportFromEntries(IEnumerable<Dte> entries, string filePath, TblxMetadata? metadata = null)
    {
        try
        {
            var doc = new TblxDocument
            {
                Metadata = metadata ?? new TblxMetadata(),
                Entries  = entries.Select(d => TblxEntry.FromDte(d)).ToList()
            };
            if (doc.Metadata.CreatedDate == null) doc.Metadata.CreatedDate = DateTime.Now;
            SaveToFile(doc, filePath);
        }
        catch (Exception ex) { throw new Exception($"Failed to export .tblx: {ex.Message}", ex); }
    }

    public TblxDocument CreateNew(TblxMetadata? metadata = null) => new()
    {
        Metadata = metadata ?? new TblxMetadata { CreatedDate = DateTime.Now, Version = "1.0" }
    };

    public TblValidationResult Validate(TblxDocument document)
    {
        var result = new TblValidationResult { IsValid = true };
        if (document.Format != "tblx") { result.IsValid = false; result.Errors.Add("Invalid format identifier. Expected 'tblx'."); }
        if (document.Metadata == null) { result.IsValid = false; result.Errors.Add("Metadata is required."); }
        if (document.Entries == null || document.Entries.Count == 0) { result.Warnings.Add("Document contains no entries."); }
        else
        {
            var vs = new TblValidationService();
            foreach (var entry in document.Entries)
            {
                var er = vs.ValidateEntry(entry.Entry, entry.Value);
                if (!er.IsValid) { result.IsValid = false; result.Errors.Add($"Entry {entry.Entry}: {er.ErrorMessage}"); }
            }
        }
        if (document.Metadata?.Validation != null) ValidateAgainstRules(document, result);
        return result;
    }

    private void ValidateAgainstRules(TblxDocument document, TblValidationResult result)
    {
        var rules = document.Metadata!.Validation!;
        foreach (var entry in document.Entries)
        {
            int byteLength = (entry.Entry?.Length ?? 0) / 2;
            if (rules.MinByteLength.HasValue && byteLength < rules.MinByteLength.Value)
                result.Warnings.Add($"Entry {entry.Entry}: Byte length {byteLength} is below minimum {rules.MinByteLength.Value}");
            if (rules.MaxByteLength.HasValue && byteLength > rules.MaxByteLength.Value)
            { result.Errors.Add($"Entry {entry.Entry}: Byte length {byteLength} exceeds maximum {rules.MaxByteLength.Value}"); result.IsValid = false; }
            if (!rules.AllowMultiByte && byteLength > 1)
            { result.Errors.Add($"Entry {entry.Entry}: Multi-byte entries are not allowed"); result.IsValid = false; }
            if (byteLength > rules.MaxMultiByteLength)
            { result.Errors.Add($"Entry {entry.Entry}: Multi-byte length {byteLength} exceeds maximum {rules.MaxMultiByteLength}"); result.IsValid = false; }
        }
    }
}
