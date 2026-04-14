// ==========================================================
// Project: WpfHexEditor.Core
// File: FormatParsingService.cs
// Author: Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude (Anthropic)
// Created: 2026-03-26
// Description:
//     Universal format detection + field parsing orchestrator.
//     Decoupled from any specific editor — works with any IBinaryDataSource.
//     Manages the full lifecycle: attach → detect → parse → populate panel.
//     The whfmt engine (FormatDetection/) is consumed but never modified.
//
// Architecture Notes:
//     Implements IFormatParsingService. Delegates to:
//       - FormatDetectionService (detection)
//       - FieldParsingEngine (block traversal)
//       - FieldFormattingService (value formatting)
//       - FormatMetadataBuilder (bookmarks, forensics, inspector, exports)
//       - AssertionFieldLinker (assertion → field linking)
//     Auto-refresh throttled at 500ms after byte modifications.
// ==========================================================

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Threading;
using WpfHexEditor.Core.Events;
using WpfHexEditor.Core.FormatDetection;
using WpfHexEditor.Core.Interfaces;
using WpfHexEditor.Core.ViewModels;

namespace WpfHexEditor.Core.Services.FormatParsing
{
    /// <summary>
    /// Universal format detection + field parsing service.
    /// </summary>
    public sealed class FormatParsingService : IFormatParsingService
    {
        // ── State ────────────────────────────────────────────────────────
        private IBinaryDataSource? _source;
        private IParsedFieldsPanel? _panel;
        private FormatDefinition? _activeFormat;
        private FormatDetectionResult? _lastResult;
        private Dictionary<string, object>? _detectionVariables;
        private List<FormatMatchCandidate>? _detectionCandidates;
        private List<AssertionResult>? _detectionAssertions;
        private VariableContext _variableContext = new();
        private ExpressionEvaluator _expressionEvaluator;
        private readonly FieldFormattingService _formattingService = new();
        private readonly FormatDetectionService _detectionService = new();

        // ── Auto-refresh throttling ──────────────────────────────────────
        private DispatcherTimer? _autoRefreshTimer;
        private bool _pendingAutoRefresh;
        private const int AutoRefreshDelayMs = 500;

        // ── Navigation callback (optional) ───────────────────────────────
        private IEditorNavigationCallback? _navigationCallback;
        private readonly List<long> _formatBookmarkOffsets = new();

        public FormatParsingService()
        {
            _expressionEvaluator = new ExpressionEvaluator(_variableContext);
        }

        // ── IFormatParsingService: Data Source ───────────────────────────

        public IBinaryDataSource? ActiveSource => _source;

        public void Attach(IBinaryDataSource source, bool autoDetect = true)
        {
            if (_source != null)
                Detach();

            _source = source ?? throw new ArgumentNullException(nameof(source));

            // Wire data change for auto-refresh
            _source.DataChanged += OnSourceDataChanged;

            // Check if source also implements navigation callback
            _navigationCallback = source as IEditorNavigationCallback;

            // Initialize auto-refresh timer
            if (_autoRefreshTimer == null)
            {
                _autoRefreshTimer = new DispatcherTimer
                {
                    Interval = TimeSpan.FromMilliseconds(AutoRefreshDelayMs)
                };
                _autoRefreshTimer.Tick += OnAutoRefreshTick;
            }

            if (autoDetect)
                _ = DetectAndParseAsync();
        }

        public void Detach()
        {
            if (_source != null)
            {
                _source.DataChanged -= OnSourceDataChanged;
                _source = null;
            }

            _navigationCallback = null;
            Clear();
        }

        // ── IFormatParsingService: Panel Wiring ──────────────────────────

        public IParsedFieldsPanel? ActivePanel => _panel;

        public void ConnectPanel(IParsedFieldsPanel panel)
        {
            if (_panel != null)
                DisconnectPanel();

            _panel = panel ?? throw new ArgumentNullException(nameof(panel));

            // Wire panel events
            _panel.FieldSelected += OnPanelFieldSelected;
            _panel.RefreshRequested += OnPanelRefreshRequested;
            _panel.FormatterChanged += OnPanelFormatterChanged;
            _panel.FieldValueEdited += OnPanelFieldValueEdited;
            _panel.FormatCandidateSelected += OnPanelFormatCandidateSelected;

            // If we already have a detected format, populate immediately
            if (_source != null && _activeFormat != null)
                ParseFieldsOnDispatcher();
        }

        public void DisconnectPanel()
        {
            if (_panel != null)
            {
                _panel.FieldSelected -= OnPanelFieldSelected;
                _panel.RefreshRequested -= OnPanelRefreshRequested;
                _panel.FormatterChanged -= OnPanelFormatterChanged;
                _panel.FieldValueEdited -= OnPanelFieldValueEdited;
                _panel.FormatCandidateSelected -= OnPanelFormatCandidateSelected;
                _panel = null;
            }
        }

        // ── IFormatParsingService: Detection & Parsing ───────────────────

        public async Task DetectAndParseAsync(CancellationToken ct = default)
        {
            if (_source == null) return;

            try
            {
                // Read initial bytes for detection (max 4KB)
                int detectSize = (int)Math.Min(4096, _source.Length);
                byte[] header = _source.ReadBytes(0, detectSize);
                if (header == null || header.Length == 0) return;

                // Run detection (byteProvider=null — detection works on header bytes;
                // full-file reads use IBinaryDataSource via FieldParsingEngine)
                var result = _detectionService.DetectFormat(
                    header,
                    _source.FilePath);

                _lastResult = result;

                if (result != null && result.Success && result.Format != null)
                {
                    _activeFormat = result.Format;
                    _detectionVariables = result.Variables;
                    _detectionCandidates = result.Candidates;
                    _detectionAssertions = result.AssertionResults;

                    // Apply background blocks via navigation callback
                    if (_navigationCallback != null && result.Blocks?.Count > 0)
                    {
                        _navigationCallback.ClearCustomBackgroundBlocks();
                        foreach (var block in result.Blocks)
                            _navigationCallback.AddCustomBackgroundBlock(block);
                    }

                    // Raise FormatDetected event
                    FormatDetected?.Invoke(this, new FormatDetectedEventArgs
                    {
                        Success = true,
                        Format = result.Format,
                        Blocks = result.Blocks ?? new List<CustomBackgroundBlock>(),
                        DetectionTimeMs = result.DetectionTimeMs
                    });

                    // Parse fields
                    ParseFieldsOnDispatcher();
                }
                else
                {
                    FormatDetected?.Invoke(this, new FormatDetectedEventArgs
                    {
                        Success = false,
                        ErrorMessage = result?.ErrorMessage ?? "No format detected"
                    });
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[FormatParsingService] Detection error: {ex.Message}");
            }
        }

        public void Refresh()
        {
            if (_source == null || _activeFormat == null) return;
            ParseFieldsOnDispatcher();
        }

        public void Clear()
        {
            _activeFormat = null;
            _lastResult = null;
            _detectionVariables = null;
            _detectionCandidates = null;
            _detectionAssertions = null;
            _variableContext.Clear();
            _expressionEvaluator = new ExpressionEvaluator(_variableContext);
            _formattingService.ClearCache();
            _formatBookmarkOffsets.Clear();

            if (_panel != null)
            {
                _panel.Clear();
                if (_panel.FormatInfo != null)
                    _panel.FormatInfo.Candidates = null;
            }

            Cleared?.Invoke(this, EventArgs.Empty);
        }

        /// <summary>
        /// Apply a pre-computed detection result (e.g. from HexEditor's own detection).
        /// Stores the format, variables, candidates, and assertions, then triggers field parsing.
        /// Does NOT run detection — use <see cref="DetectAndParseAsync"/> for full detection.
        /// </summary>
        public void ApplyDetectionResult(FormatDetectionResult result)
        {
            if (result == null || !result.Success || result.Format == null) return;

            _lastResult = result;
            _activeFormat = result.Format;
            _detectionVariables = result.Variables;
            _detectionCandidates = result.Candidates;
            _detectionAssertions = result.AssertionResults;

            // Parse fields with the provided results
            ParseFieldsOnDispatcher();
        }

        // ── Format Catalog Loading ────────────────────────────────────────

        /// <summary>
        /// Load format definitions into the internal FormatDetectionService.
        /// Must be called before <see cref="DetectAndParseAsync"/> to enable format recognition.
        /// Typically called with entries from <c>EmbeddedFormatCatalog.Instance.GetAll()</c>.
        /// </summary>
        public void LoadFormats(IEnumerable<(string json, string? category)> formats)
        {
            foreach (var (json, category) in formats)
            {
                if (string.IsNullOrEmpty(json)) continue;
                var fmt = FormatDetectionService.ImportFromJson(json);
                if (fmt != null)
                {
                    fmt.Category ??= category;
                    _detectionService.AddFormatDefinition(fmt);
                }
            }
        }

        /// <summary>Number of format definitions currently loaded.</summary>
        public int LoadedFormatCount => _detectionService.GetFormatCount();

        // ── IFormatParsingService: State ─────────────────────────────────

        public FormatDetectionResult? LastDetectionResult => _lastResult;
        public FormatDefinition? ActiveFormat => _activeFormat;

        public IReadOnlyList<FormatMatchCandidate> Candidates =>
            (IReadOnlyList<FormatMatchCandidate>?)_detectionCandidates ?? Array.Empty<FormatMatchCandidate>();

        public bool HasParsedFields => _panel?.ParsedFields?.Count > 0;

        public IReadOnlyList<ParsedFieldViewModel> GetParsedFields()
            => _panel?.ParsedFields?.ToList() ?? (IReadOnlyList<ParsedFieldViewModel>)Array.Empty<ParsedFieldViewModel>();

        // ── IFormatParsingService: User Actions ──────────────────────────

        public void SelectCandidate(FormatMatchCandidate candidate)
        {
            if (candidate == null || _source == null) return;

            try
            {
                // Apply blocks from the selected candidate
                if (_navigationCallback != null)
                {
                    _navigationCallback.ClearCustomBackgroundBlocks();
                    if (candidate.Blocks != null)
                    {
                        foreach (var block in candidate.Blocks)
                            _navigationCallback.AddCustomBackgroundBlock(block);
                    }
                }

                _activeFormat = candidate.Format;
                _detectionVariables = candidate.Variables;

                // Re-parse fields
                Refresh();

                // Update enriched format panel
                _panel?.SetEnrichedFormat(candidate.Format);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error switching format candidate: {ex.Message}");
            }
        }

        public void SetFormatter(string formatterType)
        {
            _formattingService.SetFormatter(formatterType);

            // Re-format all existing fields
            if (_panel?.ParsedFields != null)
            {
                foreach (var field in _panel.ParsedFields)
                    _formattingService.FormatFieldValue(field);
                _panel.RefreshView();
            }
        }

        // ── IFormatParsingService: Events ────────────────────────────────

        public event EventHandler<FormatDetectedEventArgs>? FormatDetected;
        public event EventHandler<FormatParsingCompleteEventArgs>? ParsingComplete;
        public event EventHandler? Cleared;

        // ── Core Parsing Logic ───────────────────────────────────────────

        private void ParseFieldsOnDispatcher()
        {
            if (_source == null || _activeFormat == null || _panel == null)
                return;

            try
            {
                // Use Dispatcher if available (WPF thread safety)
                if (System.Windows.Application.Current?.Dispatcher != null)
                {
                    System.Windows.Application.Current.Dispatcher.InvokeAsync(
                        () => ExecuteParsing(),
                        DispatcherPriority.Background);
                }
                else
                {
                    ExecuteParsing();
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[FormatParsingService] Parse dispatch error: {ex.Message}");
            }
        }

        private void ExecuteParsing()
        {
            var panel = _panel;
            var source = _source;
            var format = _activeFormat;
            if (panel == null || source == null || format == null) return;

            try
            {
                // Clear existing fields and variables
                panel.ParsedFields.Clear();
                _variableContext.Clear();
                _formattingService.ClearCache();

                // Initialize buffered reader
                using var reader = new BufferedDataSourceReader(source, 65536);

                // Load format-defined variables
                if (format.Variables != null)
                {
                    foreach (var kvp in format.Variables)
                        _variableContext.SetVariable(kvp.Key, kvp.Value);
                }

                // Load variables from detection
                if (_detectionVariables != null)
                {
                    foreach (var kvp in _detectionVariables)
                        _variableContext.SetVariable(kvp.Key, kvp.Value);
                }

                // Update format info
                panel.FormatInfo.IsDetected = true;
                panel.FormatInfo.Name = format.FormatName;
                panel.FormatInfo.Description = format.Description;
                panel.FormatInfo.Category = format.Category ?? "Other";
                panel.FormatInfo.References = format.References;

                // Populate format candidates dropdown
                PopulateFormatCandidates(panel, format);

                // Set total file size for coverage bar
                panel.TotalFileSize = source.Length;

                // Parse all blocks
                var engine = new FieldParsingEngine(
                    source, reader, _variableContext, _expressionEvaluator,
                    _formattingService, format.FormatName);

                if (format.Blocks != null)
                    engine.ParseBlocks(format.Blocks, 0, panel.ParsedFields);

                // E3 — Navigation bookmarks
                ClearFormatBookmarks();
                var bookmarkOffsets = FormatMetadataBuilder.BuildBookmarks(
                    format, _variableContext, source.Length, panel);
                RegisterFormatBookmarks(bookmarkOffsets);

                // D3 — Forensic alerts
                var forensicAlerts = FormatMetadataBuilder.BuildForensicAlerts(
                    _detectionAssertions, panel);
                if (forensicAlerts?.Count > 0)
                    AssertionFieldLinker.ApplyAssertionFailuresToFields(forensicAlerts, panel.ParsedFields);

                // D4 — Inspector groups
                FormatMetadataBuilder.BuildInspectorGroups(format, _variableContext, panel);

                // D5 — Export templates
                FormatMetadataBuilder.BuildExportTemplates(format, panel);

                // D6 — AI Hints
                FormatMetadataBuilder.BuildAiHints(format, panel);

                // Raise parsing complete event
                ParsingComplete?.Invoke(this, new FormatParsingCompleteEventArgs
                {
                    FormatName = format.FormatName,
                    FieldCount = engine.ParsedFieldCount,
                    HasForensicAlerts = forensicAlerts?.Count > 0,
                    BookmarkCount = bookmarkOffsets.Count
                });
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[FormatParsingService] Parsing error: {ex.Message}");
            }
        }

        private void PopulateFormatCandidates(IParsedFieldsPanel panel, FormatDefinition format)
        {
            if (_detectionCandidates?.Count > 1 && panel.FormatInfo.Candidates == null)
            {
                var items = new ObservableCollection<FormatCandidateItem>();
                foreach (var c in _detectionCandidates.Take(8))
                {
                    items.Add(new FormatCandidateItem
                    {
                        DisplayName = $"{c.Format.FormatName} ({c.ConfidenceScore:P0})",
                        Candidate = c
                    });
                }
                panel.SuppressFormatCandidateEvents = true;
                panel.FormatInfo.Candidates = items;
                panel.FormatInfo.SetSelectedCandidateSilently(items[0]);
                panel.SuppressFormatCandidateEvents = false;
            }
            else if (_detectionCandidates?.Count > 1)
            {
                var match = panel.FormatInfo.Candidates?
                    .FirstOrDefault(c => c.Candidate?.Format?.FormatName == format.FormatName);
                if (match != null)
                {
                    panel.SuppressFormatCandidateEvents = true;
                    panel.FormatInfo.SetSelectedCandidateSilently(match);
                    panel.SuppressFormatCandidateEvents = false;
                }
            }
        }

        // ── Bookmark Management ──────────────────────────────────────────

        private void ClearFormatBookmarks()
        {
            if (_navigationCallback != null)
            {
                foreach (var offset in _formatBookmarkOffsets)
                    _navigationCallback.RemoveBookmark(offset);
            }
            _formatBookmarkOffsets.Clear();
        }

        private void RegisterFormatBookmarks(List<long> offsets)
        {
            if (_navigationCallback == null) return;
            foreach (var offset in offsets)
            {
                _navigationCallback.SetBookmark(offset);
                _formatBookmarkOffsets.Add(offset);
            }
        }

        // ── Panel Event Handlers ─────────────────────────────────────────

        private void OnPanelFieldSelected(object? sender, ParsedFieldViewModel field)
        {
            if (field == null || _navigationCallback == null) return;

            if (field.ValueType == "metadata" || field.Offset < 0)
                return;

            _navigationCallback.NavigateTo(field.Offset);
            _navigationCallback.SetSelection(field.Offset, field.Offset + field.Length - 1);
        }

        private void OnPanelRefreshRequested(object? sender, EventArgs e)
            => Refresh();

        private void OnPanelFormatterChanged(object? sender, string formatterType)
            => SetFormatter(formatterType);

        private void OnPanelFieldValueEdited(object? sender, FieldEditedEventArgs e)
        {
            if (e?.Field == null || e.NewBytes == null || _source == null) return;

            if (e.NewBytes.Length != e.Field.Length)
                return;

            if (_source.IsReadOnly) return;

            try
            {
                _source.WriteBytes(e.Field.Offset, e.NewBytes);
                // Auto-refresh will trigger from DataChanged event
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error writing field value: {ex.Message}");
            }
        }

        private void OnPanelFormatCandidateSelected(object? sender, FormatCandidateSelectedEventArgs e)
        {
            if (e?.Candidate != null)
                SelectCandidate(e.Candidate);
        }

        // ── Auto-Refresh Throttling ──────────────────────────────────────

        private void OnSourceDataChanged(object? sender, EventArgs e)
        {
            if (_activeFormat == null) return;

            _pendingAutoRefresh = true;
            _autoRefreshTimer?.Stop();
            _autoRefreshTimer?.Start();
        }

        private void OnAutoRefreshTick(object? sender, EventArgs e)
        {
            _autoRefreshTimer?.Stop();

            if (_pendingAutoRefresh)
            {
                _pendingAutoRefresh = false;
                Refresh();
            }
        }
    }
}
