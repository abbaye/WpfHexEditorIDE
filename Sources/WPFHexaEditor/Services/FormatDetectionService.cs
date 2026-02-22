//////////////////////////////////////////////
// Apache 2.0  - 2026
// Contributors: Claude Sonnet 4.5
//////////////////////////////////////////////

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using WpfHexaEditor.Core;
using WpfHexaEditor.Core.FormatDetection;
using WpfHexaEditor.Events;

namespace WpfHexaEditor.Services
{
    /// <summary>
    /// Service for detecting file formats and generating custom background blocks
    /// Loads format definitions from JSON files and executes them via FormatScriptInterpreter
    /// </summary>
    public class FormatDetectionService
    {
        private readonly List<FormatDefinition> _loadedFormats = new List<FormatDefinition>();

        #region Format Loading

        /// <summary>
        /// Load a format definition from JSON file
        /// </summary>
        /// <param name="jsonPath">Path to JSON file</param>
        /// <returns>True if loaded successfully</returns>
        public bool LoadFormatDefinition(string jsonPath)
        {
            if (string.IsNullOrWhiteSpace(jsonPath) || !File.Exists(jsonPath))
                return false;

            try
            {
                var json = File.ReadAllText(jsonPath);
                var format = ImportFromJson(json);

                if (format != null && format.IsValid())
                {
                    return AddFormatDefinition(format);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error loading format definition from {jsonPath}: {ex.Message}");
            }

            return false;
        }

        /// <summary>
        /// Add a format definition directly (used for embedded resources)
        /// </summary>
        /// <param name="format">Format definition to add</param>
        /// <returns>True if added successfully</returns>
        public bool AddFormatDefinition(FormatDefinition format)
        {
            if (format == null || !format.IsValid())
                return false;

            try
            {
                // Check if already loaded
                var existing = _loadedFormats.FirstOrDefault(f =>
                    f.FormatName == format.FormatName && f.Version == format.Version);

                if (existing != null)
                {
                    // Replace existing (allows user formats to override built-in)
                    _loadedFormats.Remove(existing);
                }

                _loadedFormats.Add(format);
                return true;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error adding format definition: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Load all format definitions from a directory
        /// </summary>
        /// <param name="directory">Directory containing JSON files</param>
        /// <returns>Number of formats loaded</returns>
        public int LoadFormatDefinitionsFromDirectory(string directory)
        {
            if (string.IsNullOrWhiteSpace(directory) || !Directory.Exists(directory))
                return 0;

            int count = 0;

            try
            {
                // Load all .json files recursively
                var jsonFiles = Directory.GetFiles(directory, "*.json", SearchOption.AllDirectories);

                foreach (var file in jsonFiles)
                {
                    if (LoadFormatDefinition(file))
                    {
                        count++;
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error loading formats from directory {directory}: {ex.Message}");
            }

            return count;
        }

        /// <summary>
        /// Clear all loaded format definitions
        /// </summary>
        public void ClearFormats()
        {
            _loadedFormats.Clear();
        }

        #endregion

        #region Format Detection

        /// <summary>
        /// Detect format from file data
        /// Tries all loaded formats and returns the first match
        /// </summary>
        /// <param name="data">File data (at least first 1KB recommended)</param>
        /// <param name="fileName">Optional filename for extension-based hints</param>
        /// <returns>Detection result</returns>
        public FormatDetectionResult DetectFormat(byte[] data, string fileName = null)
        {
            if (data == null || data.Length == 0)
            {
                return new FormatDetectionResult
                {
                    Success = false,
                    ErrorMessage = "No data provided"
                };
            }

            var sw = Stopwatch.StartNew();

            // Get candidate formats (by extension if filename provided)
            var candidates = GetCandidateFormats(fileName);

            // Try each candidate
            foreach (var format in candidates)
            {
                if (TryDetectFormat(data, format, out var blocks))
                {
                    sw.Stop();
                    return new FormatDetectionResult
                    {
                        Success = true,
                        Format = format,
                        Blocks = blocks,
                        DetectionTimeMs = sw.Elapsed.TotalMilliseconds
                    };
                }
            }

            sw.Stop();
            return new FormatDetectionResult
            {
                Success = false,
                ErrorMessage = "No matching format found",
                DetectionTimeMs = sw.Elapsed.TotalMilliseconds
            };
        }

        /// <summary>
        /// Try to detect a specific format
        /// </summary>
        private bool TryDetectFormat(byte[] data, FormatDefinition format, out List<CustomBackgroundBlock> blocks)
        {
            blocks = new List<CustomBackgroundBlock>();

            if (format == null || !format.IsValid())
                return false;

            // Check signature
            if (format.Detection != null && format.Detection.Required)
            {
                if (!CheckSignature(data, format.Detection))
                    return false;
            }

            // Generate blocks using interpreter
            try
            {
                var interpreter = new FormatScriptInterpreter(data, format.Variables);
                blocks = interpreter.ExecuteBlocks(format.Blocks);
                return blocks.Count > 0;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error executing format {format.FormatName}: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Check if data matches format signature
        /// </summary>
        private bool CheckSignature(byte[] data, DetectionRule detection)
        {
            if (detection == null || !detection.IsValid())
                return false;

            var signatureBytes = detection.GetSignatureBytes();
            if (signatureBytes == null)
                return false;

            long offset = detection.Offset;
            if (offset < 0 || offset + signatureBytes.Length > data.Length)
                return false;

            for (int i = 0; i < signatureBytes.Length; i++)
            {
                if (data[offset + i] != signatureBytes[i])
                    return false;
            }

            return true;
        }

        /// <summary>
        /// Get candidate formats for detection
        /// Prioritizes formats matching the file extension
        /// </summary>
        private List<FormatDefinition> GetCandidateFormats(string fileName)
        {
            var candidates = new List<FormatDefinition>();

            if (!string.IsNullOrWhiteSpace(fileName))
            {
                var extension = Path.GetExtension(fileName)?.ToLowerInvariant();
                if (!string.IsNullOrWhiteSpace(extension))
                {
                    // Add formats matching extension first
                    candidates.AddRange(_loadedFormats.Where(f =>
                        f.Extensions != null && f.Extensions.Any(ext =>
                            ext.Equals(extension, StringComparison.OrdinalIgnoreCase))));
                }
            }

            // Add remaining formats
            candidates.AddRange(_loadedFormats.Where(f => !candidates.Contains(f)));

            return candidates;
        }

        /// <summary>
        /// Generate blocks for a known format (skip detection)
        /// </summary>
        /// <param name="data">File data</param>
        /// <param name="format">Format to apply</param>
        /// <returns>Generated blocks</returns>
        public List<CustomBackgroundBlock> GenerateBlocks(byte[] data, FormatDefinition format)
        {
            if (data == null || format == null || !format.IsValid())
                return new List<CustomBackgroundBlock>();

            try
            {
                var interpreter = new FormatScriptInterpreter(data, format.Variables);
                return interpreter.ExecuteBlocks(format.Blocks);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error generating blocks for {format.FormatName}: {ex.Message}");
                return new List<CustomBackgroundBlock>();
            }
        }

        #endregion

        #region Import/Export

        /// <summary>
        /// Import format definition from JSON string
        /// </summary>
        /// <param name="json">JSON string</param>
        /// <returns>Format definition or null if invalid</returns>
        public FormatDefinition ImportFromJson(string json)
        {
            if (string.IsNullOrWhiteSpace(json))
                return null;

            try
            {
                var options = new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true,
                    ReadCommentHandling = JsonCommentHandling.Skip,
                    AllowTrailingCommas = true
                };

                var format = JsonSerializer.Deserialize<FormatDefinition>(json, options);
                return format?.IsValid() == true ? format : null;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error parsing JSON: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// Export format definition to JSON string
        /// </summary>
        /// <param name="format">Format to export</param>
        /// <param name="indented">Whether to indent JSON</param>
        /// <returns>JSON string</returns>
        public string ExportToJson(FormatDefinition format, bool indented = true)
        {
            if (format == null)
                return null;

            try
            {
                var options = new JsonSerializerOptions
                {
                    WriteIndented = indented,
                    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
                };

                return JsonSerializer.Serialize(format, options);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error serializing format: {ex.Message}");
                return null;
            }
        }

        #endregion

        #region Query Methods

        /// <summary>
        /// Get format by name
        /// </summary>
        /// <param name="name">Format name</param>
        /// <returns>Format definition or null</returns>
        public FormatDefinition GetFormatByName(string name)
        {
            if (string.IsNullOrWhiteSpace(name))
                return null;

            return _loadedFormats.FirstOrDefault(f =>
                f.FormatName.Equals(name, StringComparison.OrdinalIgnoreCase));
        }

        /// <summary>
        /// Get formats by file extension
        /// </summary>
        /// <param name="extension">File extension (e.g., ".zip", ".png")</param>
        /// <returns>List of matching formats</returns>
        public List<FormatDefinition> GetFormatsByExtension(string extension)
        {
            if (string.IsNullOrWhiteSpace(extension))
                return new List<FormatDefinition>();

            var ext = extension.ToLowerInvariant();
            if (!ext.StartsWith("."))
                ext = "." + ext;

            return _loadedFormats
                .Where(f => f.Extensions != null && f.Extensions.Any(e =>
                    e.Equals(ext, StringComparison.OrdinalIgnoreCase)))
                .ToList();
        }

        /// <summary>
        /// Get all loaded formats
        /// </summary>
        /// <returns>List of all formats</returns>
        public List<FormatDefinition> GetAllFormats()
        {
            return _loadedFormats.ToList();
        }

        /// <summary>
        /// Get number of loaded formats
        /// </summary>
        public int GetFormatCount() => _loadedFormats.Count;

        /// <summary>
        /// Check if any formats are loaded
        /// </summary>
        public bool HasFormats() => _loadedFormats.Count > 0;

        #endregion

        #region Statistics

        /// <summary>
        /// Get statistics about loaded formats
        /// </summary>
        public FormatStatistics GetStatistics()
        {
            return new FormatStatistics
            {
                TotalFormats = _loadedFormats.Count,
                TotalExtensions = _loadedFormats.SelectMany(f => f.Extensions ?? new List<string>()).Distinct().Count(),
                FormatsByCategory = _loadedFormats
                    .GroupBy(f => GetCategory(f.FormatName))
                    .ToDictionary(g => g.Key, g => g.Count())
            };
        }

        /// <summary>
        /// Get category from format name
        /// </summary>
        private string GetCategory(string formatName)
        {
            if (string.IsNullOrWhiteSpace(formatName))
                return "Unknown";

            var lower = formatName.ToLowerInvariant();

            // Archives
            if (lower.Contains("archive") || lower.Contains("zip") || lower.Contains("rar") ||
                lower.Contains("7z") || lower.Contains("tar") || lower.Contains("gzip") ||
                lower.Contains("bzip") || lower.Contains("xz") || lower.Contains("cab") || lower.Contains("lzh"))
                return "Archives";

            // Images
            if (lower.Contains("image") || lower.Contains("png") || lower.Contains("jpg") ||
                lower.Contains("jpeg") || lower.Contains("gif") || lower.Contains("bmp") ||
                lower.Contains("tiff") || lower.Contains("webp") || lower.Contains("ico") ||
                lower.Contains("psd") || lower.Contains("svg") || lower.Contains("tga") ||
                lower.Contains("xcf") || lower.Contains("dds") || lower.Contains("pcx"))
                return "Images";

            // Audio
            if (lower.Contains("audio") || lower.Contains("mp3") || lower.Contains("wav") ||
                lower.Contains("flac") || lower.Contains("ogg") || lower.Contains("m4a") ||
                lower.Contains("aac") || lower.Contains("aiff") || lower.Contains("midi"))
                return "Audio";

            // Video
            if (lower.Contains("video") || lower.Contains("mp4") || lower.Contains("avi") ||
                lower.Contains("mkv") || lower.Contains("webm") || lower.Contains("mov") ||
                lower.Contains("flv") || lower.Contains("wmv") || lower.Contains("3gp") || lower.Contains("vob"))
                return "Video";

            // Documents
            if (lower.Contains("document") || lower.Contains("pdf") || lower.Contains("docx") ||
                lower.Contains("xlsx") || lower.Contains("rtf") || lower.Contains("epub") ||
                lower.Contains("ps") || lower.Contains("xml") || lower.Contains("chm") ||
                lower.Contains("djvu") || lower.Contains("mobi") || lower.Contains("azw"))
                return "Documents";

            // Executables
            if (lower.Contains("executable") || lower.Contains("exe") || lower.Contains("elf") ||
                lower.Contains("mach-o") || lower.Contains("dll") || lower.Contains("com"))
                return "Executables";

            // 3D
            if (lower.Contains("3d") || lower.Contains("stl") || lower.Contains("obj") ||
                lower.Contains("3ds") || lower.Contains("fbx") || lower.Contains("model"))
                return "3D";

            // Database
            if (lower.Contains("database") || lower.Contains("sqlite") || lower.Contains("db"))
                return "Database";

            // Fonts
            if (lower.Contains("font") || lower.Contains("ttf") || lower.Contains("otf") ||
                lower.Contains("woff"))
                return "Fonts";

            // Disk Images
            if (lower.Contains("disk") || lower.Contains("iso") || lower.Contains("vhd") ||
                lower.Contains("vmdk") || lower.Contains("vdi"))
                return "Disk";

            // Network
            if (lower.Contains("network") || lower.Contains("pcap") || lower.Contains("packet"))
                return "Network";

            // Programming
            if (lower.Contains("java") || lower.Contains("class") || lower.Contains("dex") ||
                lower.Contains("bytecode") || lower.Contains("wasm") || lower.Contains("lua") ||
                lower.Contains("python") || lower.Contains("script"))
                return "Programming";

            // Game
            if (lower.Contains("game") || lower.Contains("unity") || lower.Contains("unreal") ||
                lower.Contains("rom") || lower.Contains("pak") || lower.Contains("bsp") ||
                lower.Contains("wad") || lower.Contains("minecraft"))
                return "Game";

            // CAD
            if (lower.Contains("cad") || lower.Contains("dwg") || lower.Contains("dxf") ||
                lower.Contains("step") || lower.Contains("iges") || lower.Contains("stl"))
                return "CAD";

            // Medical
            if (lower.Contains("medical") || lower.Contains("dicom") || lower.Contains("nifti") ||
                lower.Contains("imaging"))
                return "Medical";

            // Science
            if (lower.Contains("science") || lower.Contains("fits") || lower.Contains("hdf") ||
                lower.Contains("netcdf") || lower.Contains("matlab") || lower.Contains("scientific"))
                return "Science";

            // Certificates
            if (lower.Contains("certificate") || lower.Contains("der") || lower.Contains("p12") ||
                lower.Contains("pfx"))
                return "Certificates";

            // System
            if (lower.Contains("system") || lower.Contains("dmp") || lower.Contains("reg") ||
                lower.Contains("dump") || lower.Contains("registry") || lower.Contains("evt"))
                return "System";

            // Crypto
            if (lower.Contains("crypto") || lower.Contains("pgp") || lower.Contains("gpg") ||
                lower.Contains("encryption"))
                return "Crypto";

            // Data
            if (lower.Contains("json") || lower.Contains("data") || lower.Contains("yaml") ||
                lower.Contains("toml") || lower.Contains("csv"))
                return "Data";

            return "Other";
        }

        #endregion
    }

    /// <summary>
    /// Statistics about loaded formats
    /// </summary>
    public class FormatStatistics
    {
        public int TotalFormats { get; set; }
        public int TotalExtensions { get; set; }
        public Dictionary<string, int> FormatsByCategory { get; set; } = new Dictionary<string, int>();

        public override string ToString()
        {
            return $"{TotalFormats} formats, {TotalExtensions} extensions";
        }
    }
}
