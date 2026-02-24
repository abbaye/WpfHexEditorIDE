//////////////////////////////////////////////
// Apache 2.0  - 2026
// Author : Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.5
//////////////////////////////////////////////

using System;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Windows;
using WpfHexaEditor.Core.FormatDetection;
using WpfHexaEditor.Events;
using WpfHexaEditor.Services;

namespace WpfHexaEditor
{
    /// <summary>
    /// HexEditor partial class - Format Detection
    /// Contains methods for automatic format detection and custom background generation
    /// </summary>
    public partial class HexEditor
    {
        #region Private Fields

        private readonly FormatDetectionService _formatDetectionService = new FormatDetectionService();

        #endregion

        #region Dependency Properties

        /// <summary>
        /// DependencyProperty for EnableAutoFormatDetection
        /// </summary>
        public static readonly DependencyProperty EnableAutoFormatDetectionProperty =
            DependencyProperty.Register(
                nameof(EnableAutoFormatDetection),
                typeof(bool),
                typeof(HexEditor),
                new PropertyMetadata(true));

        /// <summary>
        /// Enable or disable automatic format detection when file is opened
        /// </summary>
        [Category("Format Detection")]
        [Description("Automatically detect file format and apply custom background blocks when opening a file")]
        public bool EnableAutoFormatDetection
        {
            get => (bool)GetValue(EnableAutoFormatDetectionProperty);
            set => SetValue(EnableAutoFormatDetectionProperty, value);
        }

        /// <summary>
        /// DependencyProperty for FormatDefinitionsPath
        /// </summary>
        public static readonly DependencyProperty FormatDefinitionsPathProperty =
            DependencyProperty.Register(
                nameof(FormatDefinitionsPath),
                typeof(string),
                typeof(HexEditor),
                new PropertyMetadata(string.Empty, OnFormatDefinitionsPathChanged));

        /// <summary>
        /// Path to directory containing format definition JSON files
        /// </summary>
        [Category("Format Detection")]
        [Description("Directory path containing format definition JSON files (.zip, .png, .pdf, etc.)")]
        [System.ComponentModel.Browsable(false)]
        public string FormatDefinitionsPath
        {
            get => (string)GetValue(FormatDefinitionsPathProperty);
            set => SetValue(FormatDefinitionsPathProperty, value);
        }

        private static void OnFormatDefinitionsPathChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is HexEditor editor && e.NewValue is string path && !string.IsNullOrWhiteSpace(path))
            {
                if (Directory.Exists(path))
                {
                    var count = editor.LoadFormatDefinitions(path);
                    editor.LoadedFormatCount = count;
                }
            }
        }

        /// <summary>
        /// DependencyProperty for AutoApplyDetectedBlocks
        /// </summary>
        public static readonly DependencyProperty AutoApplyDetectedBlocksProperty =
            DependencyProperty.Register(
                nameof(AutoApplyDetectedBlocks),
                typeof(bool),
                typeof(HexEditor),
                new PropertyMetadata(true));

        /// <summary>
        /// Automatically apply detected custom background blocks
        /// </summary>
        [Category("Format Detection")]
        [Description("Automatically apply custom background blocks when format is detected")]
        public bool AutoApplyDetectedBlocks
        {
            get => (bool)GetValue(AutoApplyDetectedBlocksProperty);
            set => SetValue(AutoApplyDetectedBlocksProperty, value);
        }

        /// <summary>
        /// DependencyProperty for ShowFormatDetectionStatus
        /// </summary>
        public static readonly DependencyProperty ShowFormatDetectionStatusProperty =
            DependencyProperty.Register(
                nameof(ShowFormatDetectionStatus),
                typeof(bool),
                typeof(HexEditor),
                new PropertyMetadata(true));

        /// <summary>
        /// Show format detection status in status bar
        /// </summary>
        [Category("Format Detection")]
        [Description("Display format detection results in the status bar")]
        public bool ShowFormatDetectionStatus
        {
            get => (bool)GetValue(ShowFormatDetectionStatusProperty);
            set => SetValue(ShowFormatDetectionStatusProperty, value);
        }

        /// <summary>
        /// DependencyProperty for MaxFormatDetectionSize
        /// </summary>
        public static readonly DependencyProperty MaxFormatDetectionSizeProperty =
            DependencyProperty.Register(
                nameof(MaxFormatDetectionSize),
                typeof(int),
                typeof(HexEditor),
                new PropertyMetadata(1048576)); // 1MB default

        /// <summary>
        /// Maximum file size (in bytes) to read for format detection
        /// </summary>
        [Category("Format Detection")]
        [Description("Maximum number of bytes to read from file for format detection (default: 1MB)")]
        public int MaxFormatDetectionSize
        {
            get => (int)GetValue(MaxFormatDetectionSizeProperty);
            set => SetValue(MaxFormatDetectionSizeProperty, value);
        }

        /// <summary>
        /// DependencyPropertyKey for LoadedFormatCount (read-only)
        /// </summary>
        private static readonly DependencyPropertyKey LoadedFormatCountPropertyKey =
            DependencyProperty.RegisterReadOnly(
                nameof(LoadedFormatCount),
                typeof(int),
                typeof(HexEditor),
                new PropertyMetadata(0));

        /// <summary>
        /// DependencyProperty for LoadedFormatCount (public accessor)
        /// </summary>
        public static readonly DependencyProperty LoadedFormatCountProperty =
            LoadedFormatCountPropertyKey.DependencyProperty;

        /// <summary>
        /// Number of loaded format definitions
        /// </summary>
        [Category("Format Detection")]
        [Description("Number of format definitions currently loaded")]
        [System.ComponentModel.ReadOnly(true)]
        public int LoadedFormatCount
        {
            get => (int)GetValue(LoadedFormatCountProperty);
            private set => SetValue(LoadedFormatCountPropertyKey, value);
        }

        #endregion

        #region Events

        /// <summary>
        /// Raised when a format is automatically detected
        /// </summary>
        public event EventHandler<FormatDetectedEventArgs> FormatDetected;

        #endregion

        #region Initialization

        /// <summary>
        /// Initialize format detection system with hybrid loading strategy
        /// This should be called from the HexEditor constructor
        ///
        /// Loading Priority:
        /// 1. Embedded resources (351 built-in formats)
        /// 2. External directory next to executable (FormatDefinitions/)
        /// 3. User custom directory (%AppData%/WpfHexaEditor/FormatDefinitions/)
        /// </summary>
        private void InitializeFormatDetection()
        {
            System.Diagnostics.Debug.WriteLine("[FormatDetection] InitializeFormatDetection started");
            int totalLoaded = 0;

            try
            {
                // STEP 1: Load built-in embedded format definitions (always available)
                System.Diagnostics.Debug.WriteLine("[FormatDetection] Loading embedded format definitions...");
                int embeddedCount = LoadEmbeddedFormatDefinitions();
                totalLoaded += embeddedCount;
                System.Diagnostics.Debug.WriteLine($"[FormatDetection] Loaded {embeddedCount} embedded formats");

                // STEP 2: Load external formats from directory next to executable (optional)
                // Allows users to override built-in formats or add new ones
                var externalDir = Path.Combine(
                    Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location) ?? "",
                    "FormatDefinitions");

                if (Directory.Exists(externalDir))
                {
                    int externalCount = LoadFormatDefinitions(externalDir);
                    totalLoaded += externalCount;

                    // Set FormatDefinitionsPath for UI display
                    if (string.IsNullOrWhiteSpace(FormatDefinitionsPath))
                    {
                        FormatDefinitionsPath = externalDir;
                    }
                }

                // STEP 3: Load user custom formats from AppData (optional)
                // Allows users to create personal format definitions
                var userDir = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                    "WpfHexaEditor",
                    "FormatDefinitions");

                if (Directory.Exists(userDir))
                {
                    int userCount = LoadFormatDefinitions(userDir);
                    totalLoaded += userCount;
                }

                // Update the LoadedFormatCount property for UI display
                LoadedFormatCount = totalLoaded;
            }
            catch (Exception ex)
            {
                // Silently ignore format loading errors
                LoadedFormatCount = totalLoaded; // Update even if there was an error
            }
        }

        #endregion

        #region Public Methods - Format Loading

        /// <summary>
        /// Load format definitions from directory
        /// </summary>
        /// <param name="directory">Directory containing JSON format definitions</param>
        /// <returns>Number of formats loaded</returns>
        public int LoadFormatDefinitions(string directory)
        {
            var count = _formatDetectionService.LoadFormatDefinitionsFromDirectory(directory);
            LoadedFormatCount = _formatDetectionService.GetFormatCount(); // FIX: Use total count from service
            return count;
        }

        /// <summary>
        /// Load a single format definition from JSON file
        /// </summary>
        /// <param name="jsonFilePath">Path to JSON file</param>
        /// <returns>True if loaded successfully</returns>
        public bool LoadFormatDefinition(string jsonFilePath)
        {
            var result = _formatDetectionService.LoadFormatDefinition(jsonFilePath);
            LoadedFormatCount = _formatDetectionService.GetFormatCount();
            return result;
        }

        /// <summary>
        /// Import format definition from JSON string
        /// </summary>
        /// <param name="json">JSON string</param>
        /// <returns>Format definition or null</returns>
        public FormatDefinition ImportFormatFromJson(string json)
        {
            var result = _formatDetectionService.ImportFromJson(json);
            LoadedFormatCount = _formatDetectionService.GetFormatCount();
            return result;
        }

        /// <summary>
        /// Export format definition to JSON string
        /// </summary>
        /// <param name="format">Format to export</param>
        /// <param name="indented">Whether to indent JSON</param>
        /// <returns>JSON string</returns>
        public string ExportFormatToJson(FormatDefinition format, bool indented = true)
        {
            return _formatDetectionService.ExportToJson(format, indented);
        }

        /// <summary>
        /// Clear all loaded format definitions
        /// </summary>
        public void ClearFormatDefinitions()
        {
            _formatDetectionService.ClearFormats();
            LoadedFormatCount = 0;
        }

        /// <summary>
        /// Load embedded format definitions from assembly resources
        /// Called automatically during initialization to load built-in formats
        /// </summary>
        /// <returns>Number of formats loaded from embedded resources</returns>
        public int LoadEmbeddedFormatDefinitions()
        {
            int count = 0;

            try
            {
                var assembly = System.Reflection.Assembly.GetExecutingAssembly();

                var resourceNames = assembly.GetManifestResourceNames()
                    .Where(r => r.Contains("FormatDefinitions") && r.EndsWith(".json"))
                    .ToList();

                System.Diagnostics.Debug.WriteLine($"[FormatDetection] Found {resourceNames.Count} embedded JSON resources");

                foreach (var resourceName in resourceNames)
                {
                    try
                    {
                        using var stream = assembly.GetManifestResourceStream(resourceName);
                        if (stream == null)
                        {
                            continue;
                        }

                        using var reader = new System.IO.StreamReader(stream);
                        var json = reader.ReadToEnd();

                        var format = _formatDetectionService.ImportFromJson(json);
                        if (format != null)
                        {
                            // Extract category from embedded resource name
                            // Example: "WpfHexaEditor.FormatDefinitions.Archives.ZIP.json" -> "Archives"
                            if (string.IsNullOrWhiteSpace(format.Category))
                            {
                                format.Category = ExtractCategoryFromResourceName(resourceName);
                            }

                            if (_formatDetectionService.AddFormatDefinition(format))
                            {
                                count++;
                            }
                            else
                            {
                                System.Diagnostics.Debug.WriteLine($"[FormatDetection] REJECTED (invalid): {resourceName} - {format.FormatName}");
                            }
                        }
                        else
                        {
                            System.Diagnostics.Debug.WriteLine($"[FormatDetection] REJECTED (null format): {resourceName}");
                        }
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"[FormatDetection] Error loading {resourceName}: {ex.Message}");
                    }
                }

                var finalCount = _formatDetectionService.GetFormatCount();
                LoadedFormatCount = finalCount;
                System.Diagnostics.Debug.WriteLine($"[FormatDetection] Successfully loaded {count} formats, total in service: {finalCount}");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[FormatDetection] Critical error in LoadEmbeddedFormatDefinitions: {ex.Message}");
                LoadedFormatCount = _formatDetectionService.GetFormatCount();
            }

            return count;
        }

        /// <summary>
        /// Extract category from embedded resource name
        /// Example: "WpfHexaEditor.FormatDefinitions.Archives.ZIP.json" -> "Archives"
        /// </summary>
        private string ExtractCategoryFromResourceName(string resourceName)
        {
            if (string.IsNullOrWhiteSpace(resourceName))
                return "Other";

            try
            {
                // Embedded resource format: "WpfHexaEditor.FormatDefinitions.Archives.ZIP.json"
                var parts = resourceName.Split('.');
                var formatDefsIndex = System.Array.IndexOf(parts, "FormatDefinitions");

                if (formatDefsIndex >= 0 && formatDefsIndex < parts.Length - 2)
                {
                    return parts[formatDefsIndex + 1]; // Category is next part after "FormatDefinitions"
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error extracting category from resource name {resourceName}: {ex.Message}");
            }

            return "Other"; // Default category
        }

        #endregion

        #region Public Methods - Format Detection

        /// <summary>
        /// Auto-detect format and apply custom background blocks
        /// Reads first 1MB of file for detection
        /// </summary>
        /// <param name="fileName">Optional filename for extension hints</param>
        /// <returns>Detection result</returns>
        public FormatDetectionResult AutoDetectAndApplyFormat(string fileName = null)
        {
            // Check if file is loaded
            if (Stream == null || Stream.Length == 0)
            {
                return new FormatDetectionResult
                {
                    Success = false,
                    ErrorMessage = "No file loaded"
                };
            }

            try
            {
                // Read first 1MB for detection (or entire file if smaller)
                var bytesToRead = (int)Math.Min(Stream.Length, 1024 * 1024);
                var data = new byte[bytesToRead];

                var originalPosition = Stream.Position;
                Stream.Position = 0;
                var bytesRead = Stream.Read(data, 0, bytesToRead);
                Stream.Position = originalPosition; // Restore position

                if (bytesRead == 0)
                {
                    return new FormatDetectionResult
                    {
                        Success = false,
                        ErrorMessage = "Could not read file data"
                    };
                }

                // Resize data array if less bytes were read
                if (bytesRead < data.Length)
                {
                    Array.Resize(ref data, bytesRead);
                }

                // Detect format (pass ByteProvider for reading beyond sample buffer)
                var byteProvider = GetByteProvider();
                var result = _formatDetectionService.DetectFormat(data, fileName, byteProvider);

                if (result.Success && result.Blocks != null && result.Blocks.Count > 0)
                {
                    // Clear existing blocks
                    ClearCustomBackgroundBlock();

                    // Apply detected blocks
                    foreach (var block in result.Blocks)
                    {
                        AddCustomBackgroundBlock(block);
                    }

                    // Store the detected format and variables for parsed fields panel
                    _detectedFormat = result.Format;
                    _detectionVariables = result.Variables; // Variables from function execution

                    // Parse fields for the parsed fields panel (Issue #111)
                    RefreshParsedFields();

                    // Update enriched format panel with detected format
                    UpdateEnrichedFormatPanel(result.Format);

                    // Raise event
                    FormatDetected?.Invoke(this, new FormatDetectedEventArgs
                    {
                        Success = true,
                        Format = result.Format,
                        Blocks = result.Blocks,
                        DetectionTimeMs = result.DetectionTimeMs
                    });
                }

                return result;
            }
            catch (Exception ex)
            {
                return new FormatDetectionResult
                {
                    Success = false,
                    ErrorMessage = $"Error during detection: {ex.Message}"
                };
            }
        }

        /// <summary>
        /// Apply a specific format without detection
        /// Useful when format is already known
        /// </summary>
        /// <param name="formatName">Name of format to apply</param>
        /// <returns>True if applied successfully</returns>
        public bool ApplyFormat(string formatName)
        {
            var format = _formatDetectionService.GetFormatByName(formatName);
            if (format == null)
                return false;

            return ApplyFormat(format);
        }

        /// <summary>
        /// Apply a format definition to current file
        /// </summary>
        /// <param name="format">Format to apply</param>
        /// <returns>True if applied successfully</returns>
        public bool ApplyFormat(FormatDefinition format)
        {
            if (format == null || Stream == null || Stream.Length == 0)
                return false;

            try
            {
                // Read data for block generation
                var bytesToRead = (int)Math.Min(Stream.Length, 1024 * 1024);
                var data = new byte[bytesToRead];

                var originalPosition = Stream.Position;
                Stream.Position = 0;
                var bytesRead = Stream.Read(data, 0, bytesToRead);
                Stream.Position = originalPosition;

                if (bytesRead == 0)
                    return false;

                if (bytesRead < data.Length)
                {
                    Array.Resize(ref data, bytesRead);
                }

                // Generate blocks (pass ByteProvider for reading beyond sample buffer)
                var byteProvider = GetByteProvider();
                var blocks = _formatDetectionService.GenerateBlocks(data, format, byteProvider);

                if (blocks != null && blocks.Count > 0)
                {
                    // Clear existing blocks
                    ClearCustomBackgroundBlock();

                    // Apply blocks
                    foreach (var block in blocks)
                    {
                        AddCustomBackgroundBlock(block);
                    }

                    // Store the detected format for parsed fields panel
                    _detectedFormat = format;
                    _detectionVariables = null; // No function execution in manual format application

                    // Parse fields for the parsed fields panel (Issue #111)
                    RefreshParsedFields();

                    // Update enriched format panel with detected format
                    UpdateEnrichedFormatPanel(format);

                    return true;
                }
            }
            catch (Exception ex)
            {
                // Silently ignore format application errors
            }

            return false;
        }

        #endregion

        #region Public Properties - Format Query

        /// <summary>
        /// Get all loaded format definitions
        /// </summary>
        public FormatDefinition[] LoadedFormats => _formatDetectionService.GetAllFormats().ToArray();

        /// <summary>
        /// Check if any formats are loaded
        /// </summary>
        public bool HasLoadedFormats => _formatDetectionService.HasFormats();

        /// <summary>
        /// Get format by name
        /// </summary>
        /// <param name="name">Format name</param>
        /// <returns>Format or null</returns>
        public FormatDefinition GetFormatByName(string name)
        {
            return _formatDetectionService.GetFormatByName(name);
        }

        /// <summary>
        /// Get formats for file extension
        /// </summary>
        /// <param name="extension">File extension (e.g., ".zip", ".png")</param>
        /// <returns>Array of matching formats</returns>
        public FormatDefinition[] GetFormatsByExtension(string extension)
        {
            return _formatDetectionService.GetFormatsByExtension(extension).ToArray();
        }

        /// <summary>
        /// Get statistics about loaded formats
        /// </summary>
        public FormatStatistics GetFormatStatistics()
        {
            return _formatDetectionService.GetStatistics();
        }

        /// <summary>
        /// Update the enriched format panel with detected format information
        /// </summary>
        private void UpdateEnrichedFormatPanel(FormatDefinition format)
        {
            if (ParsedFieldsPanel == null)
                return;

            try
            {
                ParsedFieldsPanel.SetEnrichedFormat(format);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[FormatDetection] Error updating enriched format panel: {ex.Message}");
            }
        }

        #endregion
    }
}
