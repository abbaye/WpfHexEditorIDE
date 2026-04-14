// ==========================================================
// Project: WpfHexEditor.HexEditor
// File: HexEditor.FormatDetection.cs
// Author: Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude (Anthropic)
// Created: 2026-03-06
// Description:
//     Partial class integrating automatic file format detection into the HexEditor.
//     On file open, runs the FormatDetectionService to identify the file format
//     and fires format-detected events for the IDE to route to appropriate editors.
//
// Architecture Notes:
//     Uses FormatDetectionService and EmbeddedFormatCatalog from WpfHexEditor.Definitions.
//     Raises FormatDetected event consumed by the IDE's editor routing logic.
//
// ==========================================================

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Media;
using WpfHexEditor.Core.FormatDetection;
using WpfHexEditor.Core.Events;
using WpfHexEditor.Core.Services;
using WpfHexEditor.Core.Definitions;

namespace WpfHexEditor.HexEditor
{
    /// <summary>
    /// HexEditor partial class - Format Detection
    /// Contains methods for automatic format detection and custom background generation
    /// </summary>
    public partial class HexEditor
    {
        #region Private Fields

        private readonly FormatDetectionService _formatDetectionService = new FormatDetectionService();

        // ── Static one-time cache for embedded format definitions ──────────────
        // FormatDefinition objects are parsed from JSON once (on first HexEditor instance)
        // and reused across all subsequent instances to avoid repeated stream I/O
        // and JSON deserialization on the UI thread.
        private static (FormatDefinition Format, string Category)[]? s_parsedEmbeddedFormats;
        private static readonly object s_parsedFormatsLock = new object();

        // ── Load-failure tracking (standalone pipeline) ───────────────────────
        private readonly List<FormatLoadFailure> _formatLoadFailures = new();

        /// <summary>
        /// Format definitions that failed to load during <see cref="InitializeFormatDetection"/>.
        /// Empty on a healthy initialization. In standalone mode, failures are surfaced in the StatusBar.
        /// </summary>
        public IReadOnlyList<FormatLoadFailure> FormatLoadFailures => _formatLoadFailures;

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
        /// 3. User custom directory (%AppData%/WpfHexEditor/FormatDefinitions/)
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
                    "WpfHexEditor",
                    "FormatDefinitions");

                if (Directory.Exists(userDir))
                {
                    int userCount = LoadFormatDefinitions(userDir);
                    totalLoaded += userCount;
                }

                // Update the LoadedFormatCount property for UI display
                LoadedFormatCount = totalLoaded;

                // Surface load failures in the StatusBar (standalone mode)
                if (_formatLoadFailures.Count > 0 && StatusText is not null)
                {
                    StatusText.Text = $"⚠ {_formatLoadFailures.Count} whfmt failed to load";
                    StatusText.Foreground = Brushes.Red;
                    StatusText.ToolTip = string.Join("\n",
                        _formatLoadFailures.Select(f => $"{f.Source}: {f.Reason}"));
                }
            }
            catch (Exception ex)
            {
                // Silently ignore format loading errors
                LoadedFormatCount = totalLoaded; // Update even if there was an error
                System.Diagnostics.Debug.WriteLine($"[FormatDetection] Critical error: {ex.Message}");
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
        /// Load embedded format definitions from <see cref="EmbeddedFormatCatalog"/>.
        /// Called automatically during initialization to load built-in formats.
        /// <para>
        /// Performance: JSON parsing and stream I/O are performed at most once per
        /// process lifetime (cached in <c>s_parsedEmbeddedFormats</c>). Subsequent
        /// HexEditor instances register the already-parsed definitions instantly.
        /// </para>
        /// </summary>
        /// <returns>Number of formats loaded from embedded resources.</returns>
        public int LoadEmbeddedFormatDefinitions()
        {
            // ── Step 1: ensure the static parsed cache is populated ──────────
            (FormatDefinition Format, string Category)[] parsed;
            lock (s_parsedFormatsLock)
            {
                // Populate if not yet done, OR if a previous attempt produced an empty array
                // (which can happen when GetAll() was called during startup before the fix — now
                // GetAll() itself is thread-safe, but this guard keeps the cache self-healing).
                if (s_parsedEmbeddedFormats is null || s_parsedEmbeddedFormats.Length == 0)
                {
                    var allEntries = EmbeddedFormatCatalog.Instance.GetAll();
                    var list = new System.Collections.Generic.List<(FormatDefinition, string)>(allEntries.Count);
                    foreach (var entry in allEntries)
                    {
                        try
                        {
                            // GetJson() is itself cached — no stream I/O after first call.
                            var json   = EmbeddedFormatCatalog.Instance.GetJson(entry.ResourceKey);
                            var format = _formatDetectionService.ImportFromJson(json);
                            if (format is not null)
                            {
                                if (string.IsNullOrWhiteSpace(format.Category))
                                    format.Category = entry.Category;
                                list.Add((format, entry.Category));
                            }
                            else
                            {
                                System.Diagnostics.Debug.WriteLine($"[FormatDetection] REJECTED (null format): {entry.ResourceKey}");
                                _formatLoadFailures.Add(new FormatLoadFailure(entry.ResourceKey, "ImportFromJson returned null"));
                            }
                        }
                        catch (Exception ex)
                        {
                            System.Diagnostics.Debug.WriteLine($"[FormatDetection] Error loading {entry.ResourceKey}: {ex.Message}");
                            _formatLoadFailures.Add(new FormatLoadFailure(entry.ResourceKey, ex.Message));
                        }
                    }
                    // Only commit to the static cache when we got a meaningful result.
                    if (list.Count > 0)
                        s_parsedEmbeddedFormats = list.ToArray();
                }
                parsed = s_parsedEmbeddedFormats ?? [];
            }

            // ── Step 2: register cached definitions in this instance's service ─
            // No JSON I/O or parsing — just AddFormatDefinition calls.
            int count = 0;
            try
            {
                foreach (var (format, _) in parsed)
                {
                    if (_formatDetectionService.AddFormatDefinition(format))
                        count++;
                }

                var finalCount = _formatDetectionService.GetFormatCount();
                LoadedFormatCount = finalCount;
                System.Diagnostics.Debug.WriteLine($"[FormatDetection] Registered {count} formats (total: {finalCount})");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[FormatDetection] Critical error registering formats: {ex.Message}");
                LoadedFormatCount = _formatDetectionService.GetFormatCount();
            }

            return count;
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

                    // Mark whole-file "catch-all" blocks as tooltip-ineligible so that
                    // OnCustomBackgroundBlocks mode does not fire on every byte.
                    var fileLen = Length;
                    if (fileLen > 0)
                    {
                        foreach (var b in _customBackgroundService.GetAllBlocks())
                        {
                            if (b.Length >= fileLen * 0.8)
                                b.ShowInTooltip = false;
                        }
                    }

                    // Store the detected format, variables, candidates, and assertion results
                    _detectedFormat = result.Format;
                    _detectionVariables = result.Variables; // Variables from function execution
                    _detectionCandidates = result.Candidates; // All candidates for format selector
                    _detectionAssertions = result.AssertionResults; // D3 — assertion results for forensic panel

                    // Parse fields for the parsed fields panel (Issue #111)
                    RefreshParsedFields();

                    // E4/E5 — Refresh toolbar + status bar so the Format chip shows the detected name
                    RefreshToolbarItems();
                    RefreshStatusBarItemValues();

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

                    // Mark whole-file "catch-all" blocks as tooltip-ineligible
                    var fileLenM = Length;
                    if (fileLenM > 0)
                    {
                        foreach (var b in _customBackgroundService.GetAllBlocks())
                        {
                            if (b.Length >= fileLenM * 0.8)
                                b.ShowInTooltip = false;
                        }
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
