//////////////////////////////////////////////
// GNU Affero General Public License v3.0 - 2026
// Author : Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.5, Claude Sonnet 4.6
//////////////////////////////////////////////

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Xml.Linq;
using WpfHexEditor.Core;
using WpfHexEditor.Core.Models.Bookmarks;

namespace WpfHexEditor.Core.Services
{
    /// <summary>
    /// Service for exporting and importing bookmarks in multiple formats
    /// Supports JSON, XML, and CSV formats
    /// </summary>
    /// <example>
    /// Basic usage:
    /// <code>
    /// var exportService = new BookmarkExportService();
    /// var bookmarks = new List&lt;EnhancedBookmark&gt;
    /// {
    ///     new EnhancedBookmark(100, "Header", "Important", "File header"),
    ///     new EnhancedBookmark(500, "Data", "Normal", "Data section")
    /// };
    ///
    /// // Export to JSON
    /// exportService.ExportToFile(bookmarks, "bookmarks.json", BookmarkExportFormat.Json);
    ///
    /// // Export to XML
    /// exportService.ExportToFile(bookmarks, "bookmarks.xml", BookmarkExportFormat.Xml);
    ///
    /// // Export to CSV
    /// var csvOptions = new CsvExportOptions { IncludeAnnotation = true, IncludeTags = true };
    /// exportService.ExportToCsvFile(bookmarks, "bookmarks.csv", csvOptions);
    ///
    /// // Import bookmarks
    /// var result = exportService.ImportFromFile("bookmarks.json");
    /// if (result.Success)
    ///     Console.WriteLine($"Imported {result.Bookmarks.Count} bookmarks");
    /// </code>
    /// </example>
    public class BookmarkExportService
    {
        #region JSON Export/Import

        /// <summary>
        /// Export bookmarks to JSON file
        /// </summary>
        /// <param name="bookmarks">Bookmarks to export</param>
        /// <param name="filePath">Output file path</param>
        /// <param name="options">Export options</param>
        public void ExportToJsonFile(IEnumerable<EnhancedBookmark> bookmarks, string filePath, JsonExportOptions options = null)
        {
            options ??= new JsonExportOptions();

            try
            {
                var jsonContent = ExportToJson(bookmarks, options);
                File.WriteAllText(filePath, jsonContent, Encoding.UTF8);
            }
            catch (Exception ex)
            {
                throw new Exception($"Failed to export to JSON file: {ex.Message}", ex);
            }
        }

        /// <summary>
        /// Export bookmarks to JSON string
        /// </summary>
        /// <param name="bookmarks">Bookmarks to export</param>
        /// <param name="options">Export options</param>
        /// <returns>JSON string</returns>
        public string ExportToJson(IEnumerable<EnhancedBookmark> bookmarks, JsonExportOptions options = null)
        {
            options ??= new JsonExportOptions();

            var exportData = new
            {
                ExportDate = DateTime.Now,
                Version = "1.0",
                Count = bookmarks.Count(),
                Bookmarks = bookmarks.Select(b => new
                {
                    Position = b.BytePositionInStream,
                    Description = b.Description,
                    Marker = b.Marker.ToString(),
                    Category = b.Category,
                    CustomColor = b.CustomColor.HasValue ? $"#{b.CustomColor.Value.R:X2}{b.CustomColor.Value.G:X2}{b.CustomColor.Value.B:X2}" : null,
                    Annotation = b.Annotation,
                    CreatedDate = b.CreatedDate,
                    ModifiedDate = b.ModifiedDate,
                    Tags = b.Tags,
                    CreatedBy = b.CreatedBy,
                    IsReadOnly = b.IsReadOnly,
                    Priority = b.Priority
                })
            };

            var jsonOptions = new JsonSerializerOptions
            {
                WriteIndented = options.Indented,
                PropertyNamingPolicy = options.UseCamelCase ? JsonNamingPolicy.CamelCase : null
            };

            return JsonSerializer.Serialize(exportData, jsonOptions);
        }

        /// <summary>
        /// Import bookmarks from JSON file
        /// </summary>
        /// <param name="filePath">Input file path</param>
        /// <returns>Import result</returns>
        public BookmarkImportResult ImportFromJsonFile(string filePath)
        {
            try
            {
                var jsonContent = File.ReadAllText(filePath, Encoding.UTF8);
                return ImportFromJson(jsonContent);
            }
            catch (Exception ex)
            {
                return new BookmarkImportResult
                {
                    Success = false,
                    ErrorMessage = $"Failed to read JSON file: {ex.Message}"
                };
            }
        }

        /// <summary>
        /// Import bookmarks from JSON string
        /// </summary>
        /// <param name="jsonContent">JSON string</param>
        /// <returns>Import result</returns>
        public BookmarkImportResult ImportFromJson(string jsonContent)
        {
            try
            {
                using var document = JsonDocument.Parse(jsonContent);
                var root = document.RootElement;

                var bookmarks = new List<EnhancedBookmark>();

                if (root.TryGetProperty("Bookmarks", out var bookmarksArray) ||
                    root.TryGetProperty("bookmarks", out bookmarksArray))
                {
                    foreach (var item in bookmarksArray.EnumerateArray())
                    {
                        var bookmark = ParseJsonBookmark(item);
                        if (bookmark != null)
                            bookmarks.Add(bookmark);
                    }
                }

                return new BookmarkImportResult
                {
                    Success = true,
                    Bookmarks = bookmarks,
                    Count = bookmarks.Count
                };
            }
            catch (Exception ex)
            {
                return new BookmarkImportResult
                {
                    Success = false,
                    ErrorMessage = $"Failed to parse JSON: {ex.Message}"
                };
            }
        }

        private EnhancedBookmark ParseJsonBookmark(JsonElement item)
        {
            try
            {
                var position = item.GetProperty("Position").GetInt64();
                var description = item.TryGetProperty("Description", out var desc) ? desc.GetString() : "";

                var bookmark = new EnhancedBookmark(position, description);

                // Parse marker
                if (item.TryGetProperty("Marker", out var marker))
                {
                    if (Enum.TryParse<ScrollMarker>(marker.GetString(), out var markerValue))
                        bookmark.Marker = markerValue;
                }

                // Parse category
                if (item.TryGetProperty("Category", out var category))
                    bookmark.Category = category.GetString();

                // Parse custom color
                if (item.TryGetProperty("CustomColor", out var color) && color.ValueKind != System.Text.Json.JsonValueKind.Null)
                {
                    var colorStr = color.GetString();
                    if (!string.IsNullOrEmpty(colorStr))
                        bookmark.CustomColor = ParseColor(colorStr);
                }

                // Parse annotation
                if (item.TryGetProperty("Annotation", out var annotation))
                    bookmark.Annotation = annotation.GetString();

                // Parse dates
                if (item.TryGetProperty("CreatedDate", out var created))
                    bookmark.CreatedDate = created.GetDateTime();

                if (item.TryGetProperty("ModifiedDate", out var modified))
                    bookmark.ModifiedDate = modified.GetDateTime();

                // Parse tags
                if (item.TryGetProperty("Tags", out var tags))
                {
                    foreach (var tag in tags.EnumerateArray())
                    {
                        var tagStr = tag.GetString();
                        if (!string.IsNullOrWhiteSpace(tagStr))
                            bookmark.Tags.Add(tagStr);
                    }
                }

                // Parse creator
                if (item.TryGetProperty("CreatedBy", out var createdBy))
                    bookmark.CreatedBy = createdBy.GetString();

                // Parse read-only
                if (item.TryGetProperty("IsReadOnly", out var isReadOnly))
                    bookmark.IsReadOnly = isReadOnly.GetBoolean();

                // Parse priority
                if (item.TryGetProperty("Priority", out var priority))
                    bookmark.Priority = priority.GetInt32();

                return bookmark;
            }
            catch
            {
                return null;
            }
        }

        #endregion

        #region XML Export/Import

        /// <summary>
        /// Export bookmarks to XML file (compatible with StateService)
        /// </summary>
        /// <param name="bookmarks">Bookmarks to export</param>
        /// <param name="filePath">Output file path</param>
        public void ExportToXmlFile(IEnumerable<EnhancedBookmark> bookmarks, string filePath)
        {
            try
            {
                var xmlContent = ExportToXml(bookmarks);
                File.WriteAllText(filePath, xmlContent, Encoding.UTF8);
            }
            catch (Exception ex)
            {
                throw new Exception($"Failed to export to XML file: {ex.Message}", ex);
            }
        }

        /// <summary>
        /// Export bookmarks to XML string
        /// </summary>
        /// <param name="bookmarks">Bookmarks to export</param>
        /// <returns>XML string</returns>
        public string ExportToXml(IEnumerable<EnhancedBookmark> bookmarks)
        {
            var root = new XElement("BookmarksExport",
                new XAttribute("Version", "1.0"),
                new XAttribute("ExportDate", DateTime.Now),
                new XAttribute("Count", bookmarks.Count()));

            foreach (var bookmark in bookmarks)
            {
                var bookmarkElement = new XElement("Bookmark",
                    new XElement("Position", bookmark.BytePositionInStream),
                    new XElement("Description", bookmark.Description ?? string.Empty),
                    new XElement("Marker", bookmark.Marker),
                    new XElement("Category", bookmark.Category),
                    new XElement("Annotation", bookmark.Annotation),
                    new XElement("CreatedDate", bookmark.CreatedDate),
                    new XElement("ModifiedDate", bookmark.ModifiedDate),
                    new XElement("CreatedBy", bookmark.CreatedBy ?? string.Empty),
                    new XElement("IsReadOnly", bookmark.IsReadOnly),
                    new XElement("Priority", bookmark.Priority)
                );

                // Add custom color if set
                if (bookmark.CustomColor.HasValue)
                {
                    var color = bookmark.CustomColor.Value;
                    bookmarkElement.Add(new XElement("CustomColor",
                        $"#{color.R:X2}{color.G:X2}{color.B:X2}"));
                }

                // Add tags
                if (bookmark.Tags.Count > 0)
                {
                    var tagsElement = new XElement("Tags");
                    foreach (var tag in bookmark.Tags)
                    {
                        tagsElement.Add(new XElement("Tag", tag));
                    }
                    bookmarkElement.Add(tagsElement);
                }

                root.Add(bookmarkElement);
            }

            var document = new XDocument(new XDeclaration("1.0", "utf-8", "yes"), root);
            return document.ToString();
        }

        /// <summary>
        /// Import bookmarks from XML file
        /// </summary>
        /// <param name="filePath">Input file path</param>
        /// <returns>Import result</returns>
        public BookmarkImportResult ImportFromXmlFile(string filePath)
        {
            try
            {
                var xmlContent = File.ReadAllText(filePath, Encoding.UTF8);
                return ImportFromXml(xmlContent);
            }
            catch (Exception ex)
            {
                return new BookmarkImportResult
                {
                    Success = false,
                    ErrorMessage = $"Failed to read XML file: {ex.Message}"
                };
            }
        }

        /// <summary>
        /// Import bookmarks from XML string
        /// </summary>
        /// <param name="xmlContent">XML string</param>
        /// <returns>Import result</returns>
        public BookmarkImportResult ImportFromXml(string xmlContent)
        {
            try
            {
                var document = XDocument.Parse(xmlContent);
                var bookmarks = new List<EnhancedBookmark>();

                foreach (var element in document.Root.Elements("Bookmark"))
                {
                    var bookmark = ParseXmlBookmark(element);
                    if (bookmark != null)
                        bookmarks.Add(bookmark);
                }

                return new BookmarkImportResult
                {
                    Success = true,
                    Bookmarks = bookmarks,
                    Count = bookmarks.Count
                };
            }
            catch (Exception ex)
            {
                return new BookmarkImportResult
                {
                    Success = false,
                    ErrorMessage = $"Failed to parse XML: {ex.Message}"
                };
            }
        }

        private EnhancedBookmark ParseXmlBookmark(XElement element)
        {
            try
            {
                var position = long.Parse(element.Element("Position").Value);
                var description = element.Element("Description")?.Value ?? "";

                var bookmark = new EnhancedBookmark(position, description);

                // Parse marker
                var markerStr = element.Element("Marker")?.Value;
                if (!string.IsNullOrEmpty(markerStr) && Enum.TryParse<ScrollMarker>(markerStr, out var marker))
                    bookmark.Marker = marker;

                // Parse category
                bookmark.Category = element.Element("Category")?.Value ?? "Default";

                // Parse annotation
                bookmark.Annotation = element.Element("Annotation")?.Value ?? "";

                // Parse dates
                if (DateTime.TryParse(element.Element("CreatedDate")?.Value, out var createdDate))
                    bookmark.CreatedDate = createdDate;

                if (DateTime.TryParse(element.Element("ModifiedDate")?.Value, out var modifiedDate))
                    bookmark.ModifiedDate = modifiedDate;

                // Parse creator
                bookmark.CreatedBy = element.Element("CreatedBy")?.Value ?? Environment.UserName;

                // Parse read-only
                if (bool.TryParse(element.Element("IsReadOnly")?.Value, out var isReadOnly))
                    bookmark.IsReadOnly = isReadOnly;

                // Parse priority
                if (int.TryParse(element.Element("Priority")?.Value, out var priority))
                    bookmark.Priority = priority;

                // Parse custom color
                var colorStr = element.Element("CustomColor")?.Value;
                if (!string.IsNullOrEmpty(colorStr))
                    bookmark.CustomColor = ParseColor(colorStr);

                // Parse tags
                var tagsElement = element.Element("Tags");
                if (tagsElement != null)
                {
                    foreach (var tagElement in tagsElement.Elements("Tag"))
                    {
                        var tag = tagElement.Value;
                        if (!string.IsNullOrWhiteSpace(tag))
                            bookmark.Tags.Add(tag);
                    }
                }

                return bookmark;
            }
            catch
            {
                return null;
            }
        }

        #endregion

        #region CSV Export/Import

        /// <summary>
        /// Export bookmarks to CSV file
        /// </summary>
        /// <param name="bookmarks">Bookmarks to export</param>
        /// <param name="filePath">Output file path</param>
        /// <param name="options">Export options</param>
        public void ExportToCsvFile(IEnumerable<EnhancedBookmark> bookmarks, string filePath, CsvExportOptions options = null)
        {
            options ??= new CsvExportOptions();

            try
            {
                var csvContent = ExportToCsv(bookmarks, options);
                File.WriteAllText(filePath, csvContent, options.Encoding);
            }
            catch (Exception ex)
            {
                throw new Exception($"Failed to export to CSV file: {ex.Message}", ex);
            }
        }

        /// <summary>
        /// Export bookmarks to CSV string
        /// </summary>
        /// <param name="bookmarks">Bookmarks to export</param>
        /// <param name="options">Export options</param>
        /// <returns>CSV string</returns>
        public string ExportToCsv(IEnumerable<EnhancedBookmark> bookmarks, CsvExportOptions options = null)
        {
            options ??= new CsvExportOptions();
            var sb = new StringBuilder();

            // Build header
            var headers = new List<string> { "Position", "Description", "Category", "Marker" };

            if (options.IncludeAnnotation)
                headers.Add("Annotation");
            if (options.IncludeTags)
                headers.Add("Tags");
            if (options.IncludeColor)
                headers.Add("Color");
            if (options.IncludeDates)
            {
                headers.Add("CreatedDate");
                headers.Add("ModifiedDate");
            }
            if (options.IncludeCreator)
                headers.Add("CreatedBy");
            if (options.IncludePriority)
                headers.Add("Priority");

            sb.AppendLine(FormatCsvLine(headers, options));

            // Export bookmarks
            foreach (var bookmark in bookmarks)
            {
                var values = new List<string>
                {
                    bookmark.BytePositionInStream.ToString(),
                    EscapeValue(bookmark.Description),
                    bookmark.Category,
                    bookmark.Marker.ToString()
                };

                if (options.IncludeAnnotation)
                    values.Add(EscapeValue(bookmark.Annotation));

                if (options.IncludeTags)
                    values.Add(string.Join(";", bookmark.Tags));

                if (options.IncludeColor)
                {
                    var color = bookmark.CustomColor.HasValue
                        ? $"#{bookmark.CustomColor.Value.R:X2}{bookmark.CustomColor.Value.G:X2}{bookmark.CustomColor.Value.B:X2}"
                        : "";
                    values.Add(color);
                }

                if (options.IncludeDates)
                {
                    values.Add(bookmark.CreatedDate.ToString("o"));
                    values.Add(bookmark.ModifiedDate.ToString("o"));
                }

                if (options.IncludeCreator)
                    values.Add(bookmark.CreatedBy ?? "");

                if (options.IncludePriority)
                    values.Add(bookmark.Priority.ToString());

                sb.AppendLine(FormatCsvLine(values, options));
            }

            return sb.ToString();
        }

        private string FormatCsvLine(IEnumerable<string> values, CsvExportOptions options)
        {
            var formattedValues = values.Select(v =>
            {
                if (options.QuoteStrings || v.Contains(options.Delimiter) || v.Contains("\"") || v.Contains("\n"))
                {
                    // Escape quotes by doubling them
                    var escaped = v.Replace("\"", "\"\"");
                    return $"\"{escaped}\"";
                }
                return v;
            });

            return string.Join(options.Delimiter, formattedValues);
        }

        private string EscapeValue(string value)
        {
            if (string.IsNullOrEmpty(value))
                return string.Empty;

            // Preserve newlines in CSV (will be quoted)
            return value;
        }

        #endregion

        #region Multi-Format Export/Import

        /// <summary>
        /// Export bookmarks to file with auto-format detection
        /// </summary>
        /// <param name="bookmarks">Bookmarks to export</param>
        /// <param name="filePath">Output file path</param>
        /// <param name="format">Export format</param>
        public void ExportToFile(IEnumerable<EnhancedBookmark> bookmarks, string filePath, BookmarkExportFormat format)
        {
            switch (format)
            {
                case BookmarkExportFormat.Json:
                    ExportToJsonFile(bookmarks, filePath);
                    break;
                case BookmarkExportFormat.Xml:
                    ExportToXmlFile(bookmarks, filePath);
                    break;
                case BookmarkExportFormat.Csv:
                    ExportToCsvFile(bookmarks, filePath);
                    break;
                default:
                    throw new ArgumentException($"Unsupported export format: {format}");
            }
        }

        /// <summary>
        /// Import bookmarks from file with auto-format detection
        /// </summary>
        /// <param name="filePath">Input file path</param>
        /// <returns>Import result</returns>
        public BookmarkImportResult ImportFromFile(string filePath)
        {
            if (!File.Exists(filePath))
            {
                return new BookmarkImportResult
                {
                    Success = false,
                    ErrorMessage = "File not found"
                };
            }

            // Detect format from extension
            var extension = Path.GetExtension(filePath).ToLowerInvariant();
            return extension switch
            {
                ".json" => ImportFromJsonFile(filePath),
                ".xml" => ImportFromXmlFile(filePath),
                ".csv" => new BookmarkImportResult
                {
                    Success = false,
                    ErrorMessage = "CSV import not yet implemented (CSV structure varies)"
                },
                _ => new BookmarkImportResult
                {
                    Success = false,
                    ErrorMessage = $"Unsupported file format: {extension}"
                }
            };
        }

        #endregion

        #region Helper Methods

        /// <summary>
        /// Parse color from hex string (#RRGGBB or RRGGBB)
        /// </summary>
        private System.Windows.Media.Color ParseColor(string colorStr)
        {
            try
            {
                colorStr = colorStr.TrimStart('#');
                if (colorStr.Length == 6)
                {
                    var r = Convert.ToByte(colorStr.Substring(0, 2), 16);
                    var g = Convert.ToByte(colorStr.Substring(2, 2), 16);
                    var b = Convert.ToByte(colorStr.Substring(4, 2), 16);
                    return System.Windows.Media.Color.FromRgb(r, g, b);
                }
            }
            catch
            {
                // Return default color on parse error
            }

            return System.Windows.Media.Colors.Blue;
        }

        #endregion
    }

    #region Export Options

    /// <summary>
    /// Options for JSON export
    /// </summary>
    public class JsonExportOptions
    {
        /// <summary>
        /// Pretty-print JSON with indentation
        /// </summary>
        public bool Indented { get; set; } = true;

        /// <summary>
        /// Use camelCase property names
        /// </summary>
        public bool UseCamelCase { get; set; } = false;
    }

    /// <summary>
    /// Options for CSV export
    /// </summary>
    public class CsvExportOptions
    {
        /// <summary>
        /// CSV delimiter character
        /// </summary>
        public string Delimiter { get; set; } = ",";

        /// <summary>
        /// Quote all string values
        /// </summary>
        public bool QuoteStrings { get; set; } = false;

        /// <summary>
        /// Include annotation column
        /// </summary>
        public bool IncludeAnnotation { get; set; } = true;

        /// <summary>
        /// Include tags column
        /// </summary>
        public bool IncludeTags { get; set; } = true;

        /// <summary>
        /// Include custom color column
        /// </summary>
        public bool IncludeColor { get; set; } = false;

        /// <summary>
        /// Include date columns
        /// </summary>
        public bool IncludeDates { get; set; } = false;

        /// <summary>
        /// Include creator column
        /// </summary>
        public bool IncludeCreator { get; set; } = false;

        /// <summary>
        /// Include priority column
        /// </summary>
        public bool IncludePriority { get; set; } = false;

        /// <summary>
        /// Text encoding for CSV file
        /// </summary>
        public Encoding Encoding { get; set; } = Encoding.UTF8;
    }

    #endregion

    #region Import Result

    /// <summary>
    /// Result of bookmark import operation
    /// </summary>
    public class BookmarkImportResult
    {
        /// <summary>
        /// Whether import was successful
        /// </summary>
        public bool Success { get; set; }

        /// <summary>
        /// Error message if import failed
        /// </summary>
        public string ErrorMessage { get; set; }

        /// <summary>
        /// Imported bookmarks
        /// </summary>
        public List<EnhancedBookmark> Bookmarks { get; set; } = new List<EnhancedBookmark>();

        /// <summary>
        /// Number of bookmarks imported
        /// </summary>
        public int Count { get; set; }
    }

    #endregion
}
