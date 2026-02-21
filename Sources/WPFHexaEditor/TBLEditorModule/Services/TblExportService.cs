//////////////////////////////////////////////
// Apache 2.0  - 2016-2026
// Author : Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.5
//////////////////////////////////////////////

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using WpfHexaEditor.Core.CharacterTable;
using WpfHexaEditor.TBLEditorModule.Models;

namespace WpfHexaEditor.TBLEditorModule.Services
{
    /// <summary>
    /// Service for exporting TBL entries to various formats
    /// </summary>
    public class TblExportService
    {
        #region CSV Export

        /// <summary>
        /// Export to CSV file
        /// </summary>
        public void ExportToCsvFile(IEnumerable<Dte> entries, string filePath, CsvExportOptions options = null)
        {
            options ??= new CsvExportOptions();

            try
            {
                var csvContent = ExportToCsv(entries, options);
                File.WriteAllText(filePath, csvContent, options.Encoding);
            }
            catch (Exception ex)
            {
                throw new Exception($"Failed to export to CSV file: {ex.Message}", ex);
            }
        }

        /// <summary>
        /// Export to CSV string
        /// </summary>
        public string ExportToCsv(IEnumerable<Dte> entries, CsvExportOptions options = null)
        {
            options ??= new CsvExportOptions();
            var sb = new StringBuilder();

            // Build header
            var headers = new List<string> { "Hex", "Character" };
            if (options.IncludeType)
                headers.Add("Type");
            if (options.IncludeByteCount)
                headers.Add("ByteCount");
            if (options.IncludeComment)
                headers.Add("Comment");

            sb.AppendLine(FormatCsvLine(headers, options));

            // Export entries
            foreach (var entry in entries)
            {
                var values = new List<string>
                {
                    entry.Entry,
                    EscapeValue(entry.Value)
                };

                if (options.IncludeType)
                    values.Add(entry.Type.ToString());

                if (options.IncludeByteCount)
                    values.Add((entry.Entry.Length / 2).ToString());

                if (options.IncludeComment)
                    values.Add(EscapeValue(entry.Comment ?? string.Empty));

                sb.AppendLine(FormatCsvLine(values, options));
            }

            return sb.ToString();
        }

        private string FormatCsvLine(IEnumerable<string> values, CsvExportOptions options)
        {
            var formattedValues = values.Select(v =>
            {
                if (options.QuoteStrings)
                {
                    // Escape quotes by doubling them
                    var escaped = v.Replace("\"", "\"\"");
                    return $"\"{escaped}\"";
                }
                else
                {
                    // Only quote if contains delimiter
                    if (v.Contains(options.Delimiter))
                    {
                        var escaped = v.Replace("\"", "\"\"");
                        return $"\"{escaped}\"";
                    }
                    return v;
                }
            });

            return string.Join(options.Delimiter, formattedValues);
        }

        private string EscapeValue(string value)
        {
            if (string.IsNullOrEmpty(value))
                return string.Empty;

            // Escape newlines and tabs for CSV
            return value.Replace("\n", "\\n")
                       .Replace("\r", "\\r")
                       .Replace("\t", "\\t");
        }

        #endregion

        #region JSON Export

        /// <summary>
        /// Export to JSON file
        /// </summary>
        public void ExportToJsonFile(IEnumerable<Dte> entries, string filePath, JsonExportOptions options = null)
        {
            options ??= new JsonExportOptions();

            try
            {
                var jsonContent = ExportToJson(entries, options);
                File.WriteAllText(filePath, jsonContent, Encoding.UTF8);
            }
            catch (Exception ex)
            {
                throw new Exception($"Failed to export to JSON file: {ex.Message}", ex);
            }
        }

        /// <summary>
        /// Export to JSON string
        /// </summary>
        public string ExportToJson(IEnumerable<Dte> entries, JsonExportOptions options = null)
        {
            options ??= new JsonExportOptions();

            try
            {
                using (var stream = new MemoryStream())
                {
                    using (var writer = new Utf8JsonWriter(stream, new JsonWriterOptions
                    {
                        Indented = options.Indented
                    }))
                    {
                        if (options.IncludeMetadata && options.Metadata != null)
                        {
                            // Root object with metadata
                            writer.WriteStartObject();

                            // Metadata section
                            writer.WriteStartObject("metadata");
                            writer.WriteString("version", options.Metadata.Version);
                            if (!string.IsNullOrWhiteSpace(options.Metadata.Description))
                                writer.WriteString("description", options.Metadata.Description);
                            if (!string.IsNullOrWhiteSpace(options.Metadata.Author))
                                writer.WriteString("author", options.Metadata.Author);
                            writer.WriteString("createdDate", options.Metadata.CreatedDate ?? DateTime.Now.ToString("O"));
                            writer.WriteEndObject();

                            // Entries array
                            writer.WriteStartArray("entries");
                            WriteEntries(writer, entries, options);
                            writer.WriteEndArray();

                            writer.WriteEndObject();
                        }
                        else
                        {
                            // Simple array format
                            writer.WriteStartArray();
                            WriteEntries(writer, entries, options);
                            writer.WriteEndArray();
                        }

                        writer.Flush();
                    }

                    return Encoding.UTF8.GetString(stream.ToArray());
                }
            }
            catch (Exception ex)
            {
                throw new Exception($"Failed to export to JSON: {ex.Message}", ex);
            }
        }

        private void WriteEntries(Utf8JsonWriter writer, IEnumerable<Dte> entries, JsonExportOptions options)
        {
            foreach (var entry in entries)
            {
                writer.WriteStartObject();

                // Hex value
                writer.WriteString(options.HexPropertyName, entry.Entry);

                // Character value
                writer.WriteString(options.ValuePropertyName, entry.Value);

                // Optional: Type
                if (options.IncludeType)
                    writer.WriteString("type", entry.Type.ToString());

                // Optional: ByteCount
                if (options.IncludeByteCount)
                    writer.WriteNumber("byteCount", entry.Entry.Length / 2);

                // Optional: Comment
                if (options.IncludeComment && !string.IsNullOrWhiteSpace(entry.Comment))
                    writer.WriteString("comment", entry.Comment);

                writer.WriteEndObject();
            }
        }

        #endregion

        #region TBL Standard Format Export

        /// <summary>
        /// Export to standard TBL format file
        /// </summary>
        public void ExportToTblFile(IEnumerable<Dte> entries, string filePath)
        {
            try
            {
                var tblContent = ExportToTbl(entries);
                File.WriteAllText(filePath, tblContent, Encoding.UTF8);
            }
            catch (Exception ex)
            {
                throw new Exception($"Failed to export to TBL file: {ex.Message}", ex);
            }
        }

        /// <summary>
        /// Export to standard TBL format string
        /// </summary>
        public string ExportToTbl(IEnumerable<Dte> entries)
        {
            var sb = new StringBuilder();

            foreach (var entry in entries)
            {
                // Format: HEX=VALUE
                var value = entry.Value.Replace("\n", "\\n")
                                      .Replace("\r", "\\r")
                                      .Replace("\t", "\\t");

                // Handle special types
                if (entry.Type == DteType.EndBlock)
                {
                    sb.AppendLine($"/{entry.Entry}");
                }
                else if (entry.Type == DteType.EndLine)
                {
                    sb.AppendLine($"*{entry.Entry}");
                }
                else
                {
                    sb.AppendLine($"{entry.Entry}={value}");
                }
            }

            return sb.ToString();
        }

        #endregion

        #region Auto-detect Format Export

        /// <summary>
        /// Export to file with auto-detected format based on extension
        /// </summary>
        public void ExportToFile(IEnumerable<Dte> entries, string filePath,
            CsvExportOptions csvOptions = null,
            JsonExportOptions jsonOptions = null)
        {
            var extension = Path.GetExtension(filePath)?.ToLowerInvariant();

            switch (extension)
            {
                case ".csv":
                    ExportToCsvFile(entries, filePath, csvOptions);
                    break;

                case ".json":
                    ExportToJsonFile(entries, filePath, jsonOptions);
                    break;

                case ".tbl":
                    ExportToTblFile(entries, filePath);
                    break;

                default:
                    throw new NotSupportedException($"Unsupported export format: {extension}");
            }
        }

        #endregion
    }
}
