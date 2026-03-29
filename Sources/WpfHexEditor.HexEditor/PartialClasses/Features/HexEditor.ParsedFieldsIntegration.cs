// ==========================================================
// Project: WpfHexEditor.HexEditor
// File: HexEditor.ParsedFieldsIntegration.cs
// Author: Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude (Anthropic)
// Created: 2026-03-06
// Description:
//     Partial class integrating the HexEditor with the ParsedFieldsPanel.
//     Wires parsed field definitions to custom background blocks in the viewport
//     so that known fields are visually highlighted by field type and color.
//
// Architecture Notes:
//     Observer pattern — subscribes to ParsedFieldsPanel.FieldsChanged event.
//     Translates IParsedField definitions into CustomBackgroundBlock entries.
//
// ==========================================================

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using WpfHexEditor.Core;
using WpfHexEditor.Core.Events;
using WpfHexEditor.Core.FormatDetection;
using WpfHexEditor.Core.Formatters;
using WpfHexEditor.Core.Interfaces;
using WpfHexEditor.Core.Models;
using WpfHexEditor.Core.ViewModels;

namespace WpfHexEditor.HexEditor
{
    public partial class HexEditor
    {
        #region Dependency Properties

        /// <summary>
        /// External ParsedFieldsPanel connected to this HexEditor.
        /// Set via ConnectParsedFieldsPanel() or direct binding.
        /// </summary>
        public static readonly DependencyProperty ParsedFieldsPanelProperty =
            DependencyProperty.Register(
                nameof(ParsedFieldsPanel),
                typeof(IParsedFieldsPanel),
                typeof(HexEditor),
                new PropertyMetadata(null, OnParsedFieldsPanelChanged));

        public IParsedFieldsPanel ParsedFieldsPanel
        {
            get => (IParsedFieldsPanel)GetValue(ParsedFieldsPanelProperty);
            set => SetValue(ParsedFieldsPanelProperty, value);
        }

        private static void OnParsedFieldsPanelChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is not HexEditor editor) return;

            // Déconnecter l'ancien panel
            if (e.OldValue is IParsedFieldsPanel oldPanel)
            {
                oldPanel.FieldSelected -= editor.ParsedFieldsPanel_FieldSelected;
                oldPanel.RefreshRequested -= editor.ParsedFieldsPanel_RefreshRequested;
                oldPanel.FormatterChanged -= editor.ParsedFieldsPanel_FormatterChanged;
                oldPanel.FieldValueEdited -= editor.ParsedFieldsPanel_FieldValueEdited;
                oldPanel.FormatCandidateSelected -= editor.ParsedFieldsPanel_FormatCandidateSelected;
            }

            // Connecter le nouveau panel
            if (e.NewValue is IParsedFieldsPanel newPanel)
            {
                newPanel.FieldSelected += editor.ParsedFieldsPanel_FieldSelected;
                newPanel.RefreshRequested += editor.ParsedFieldsPanel_RefreshRequested;
                newPanel.FormatterChanged += editor.ParsedFieldsPanel_FormatterChanged;
                newPanel.FieldValueEdited += editor.ParsedFieldsPanel_FieldValueEdited;
                newPanel.FormatCandidateSelected += editor.ParsedFieldsPanel_FormatCandidateSelected;

                // Déclencher le parsing si un fichier est déjà ouvert
                if (editor.Stream != null && editor._detectedFormat != null)
                {
                    editor.ParseFieldsAsync();
                    // Re-populate enriched format metadata — ParseFieldsAsync only sets FormatInfo,
                    // not the EnrichedFormatViewModel. Without this, switching back to a tab with a
                    // previously-detected format leaves the Enriched Format Metadata section empty.
                    newPanel.SetEnrichedFormat(editor._detectedFormat);
                }
            }
        }

        /// <summary>
        /// Enable or disable auto-refresh of parsed fields when bytes are modified
        /// </summary>
        public static readonly DependencyProperty AutoRefreshParsedFieldsProperty =
            DependencyProperty.Register(
                nameof(AutoRefreshParsedFields),
                typeof(bool),
                typeof(HexEditor),
                new PropertyMetadata(true));

        public bool AutoRefreshParsedFields
        {
            get => (bool)GetValue(AutoRefreshParsedFieldsProperty);
            set => SetValue(AutoRefreshParsedFieldsProperty, value);
        }

        #endregion

        #region Fields

        private IFieldValueFormatter _currentFormatter;
        private readonly FieldValueReader _fieldValueReader = new FieldValueReader();
        private readonly FieldValidator _fieldValidator = new FieldValidator();
        private readonly ChecksumValidator _checksumValidator = new ChecksumValidator();
        private FormatDefinition _detectedFormat;
        private Dictionary<string, object> _detectionVariables; // Variables from function execution
        private List<FormatMatchCandidate> _detectionCandidates; // All candidates from detection
        private VariableContext _variableContext;
        private ExpressionEvaluator _expressionEvaluator;
        private readonly FormattedValueCache _formattedValueCache = new FormattedValueCache();
        private BufferedFileReader _bufferedReader;

        // Performance tracking
        private int _parsedFieldCount;
        private const int MaxFieldsLimit = 500; // Safety limit to prevent UI freeze from binding storms
        private const int DepthLimit = 10; // Maximum recursion depth

        // Auto-refresh throttling
        private System.Windows.Threading.DispatcherTimer _autoRefreshTimer;
        private bool _pendingAutoRefresh;
        private const int AutoRefreshDelayMs = 500; // Delay before auto-refresh triggers

        #endregion

        #region Initialization

        /// <summary>
        /// Initialize parsed fields panel integration
        /// Called from HexEditor constructor
        /// </summary>
        private void InitializeParsedFieldsPanel()
        {
            // Set default formatter to Mixed (smart format: decimal + hex + ASCII)
            _currentFormatter = new MixedValueFormatter();

            // Initialize variable context and expression evaluator
            _variableContext = new VariableContext();
            _expressionEvaluator = new ExpressionEvaluator(_variableContext);

            // Initialize auto-refresh timer (throttle mechanism)
            _autoRefreshTimer = new System.Windows.Threading.DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(AutoRefreshDelayMs)
            };
            _autoRefreshTimer.Tick += AutoRefreshTimer_Tick;

            // Events are wired via OnParsedFieldsPanelChanged when ParsedFieldsPanel DP is set.
            // Subscribe to byte modification events for auto-refresh
            ByteModified += HexEditor_ByteModified;
        }

        #endregion

        #region Event Handlers

        /// <summary>
        /// Handle field selection in the parsed fields panel
        /// Sync with hex view by highlighting the corresponding bytes
        /// </summary>
        private void ParsedFieldsPanel_FieldSelected(object sender, ParsedFieldViewModel field)
        {
            if (field == null)
                return;

            try
            {
                // Special handling for metadata fields (no byte offset in file)
                if (field.ValueType == "metadata" || field.Offset < 0)
                {
                    // Clear selection for metadata fields since they don't represent bytes
                    ClearSelection();
                    System.Diagnostics.Debug.WriteLine($"[FieldSelected] Metadata field '{field.Name}' selected - clearing hex selection");
                    return;
                }

                // Scroll to make the field visible first (without touching selection state)
                EnsurePositionVisible(new VirtualPosition(field.Offset));

                // Set selection range to cover the full field
                SelectionStart = field.Offset;
                SelectionStop = field.Offset + field.Length - 1;

                // Force visual refresh so the selection highlight is always rendered
                RefreshView();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error selecting field: {ex.Message}");
            }
        }

        /// <summary>
        /// Handle refresh request from the parsed fields panel
        /// Re-parse all fields from the current file
        /// </summary>
        private void ParsedFieldsPanel_RefreshRequested(object sender, EventArgs e)
        {
            ParseFieldsAsync();
        }

        /// <summary>
        /// Handle format candidate selection from the dropdown
        /// Switches the active format, re-applies blocks and re-parses fields
        /// </summary>
        private void ParsedFieldsPanel_FormatCandidateSelected(object sender, FormatCandidateSelectedEventArgs e)
        {
            if (e?.Candidate == null) return;

            try
            {
                var candidate = e.Candidate;

                // Clear and re-apply blocks from the selected candidate
                ClearCustomBackgroundBlock();
                if (candidate.Blocks != null)
                {
                    foreach (var block in candidate.Blocks)
                        AddCustomBackgroundBlock(block);
                }

                // Update stored format and variables
                _detectedFormat = candidate.Format;
                _detectionVariables = candidate.Variables;

                // Re-parse fields with new format (but preserve candidate list)
                RefreshParsedFields();

                // Update enriched format panel
                UpdateEnrichedFormatPanel(candidate.Format);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error switching format candidate: {ex.Message}");
            }
        }

        /// <summary>
        /// Handle formatter change from the parsed fields panel
        /// Re-format all field values with the new formatter
        /// </summary>
        private void ParsedFieldsPanel_FormatterChanged(object sender, string formatterType)
        {
            // Update current formatter based on selection
            _currentFormatter = formatterType switch
            {
                "hex" => new HexValueFormatter(),
                "decimal" => new DecimalValueFormatter(),
                "string" => new StringValueFormatter(),
                "mixed" => new MixedValueFormatter(),
                _ => _currentFormatter
            };

            // Clear cache since we're changing formatter (old cached values are for different formatter)
            _formattedValueCache.Clear();

            // Re-format all existing fields (will populate cache with new formatter)
            if (ParsedFieldsPanel?.ParsedFields != null)
            {
                foreach (var field in ParsedFieldsPanel.ParsedFields)
                {
                    FormatFieldValue(field);
                }

                // Force UI refresh by rebuilding the FilteredFields collection
                // This ensures the updated FormattedValue properties are displayed
                ParsedFieldsPanel.RefreshView();
            }
        }

        /// <summary>
        /// Handle field value edited in the parsed fields panel
        /// Write the new value back to the file
        /// </summary>
        private void ParsedFieldsPanel_FieldValueEdited(object sender, FieldEditedEventArgs e)
        {
            if (e == null || e.Field == null || e.NewBytes == null || Stream == null)
                return;

            try
            {
                // Verify length matches
                if (e.NewBytes.Length != e.Field.Length)
                {
                    System.Windows.MessageBox.Show(
                        $"Byte length mismatch. Expected {e.Field.Length} bytes, got {e.NewBytes.Length} bytes.",
                        "Edit Error",
                        System.Windows.MessageBoxButton.OK,
                        System.Windows.MessageBoxImage.Error);
                    return;
                }

                // Write bytes to stream
                Stream.Position = e.Field.Offset;
                Stream.Write(e.NewBytes, 0, e.NewBytes.Length);
                Stream.Flush();

                // Refresh view to show changes
                RefreshView();

                // Auto-refresh will trigger from ByteModified event
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show(
                    $"Error writing value to file: {ex.Message}",
                    "Edit Error",
                    System.Windows.MessageBoxButton.OK,
                    System.Windows.MessageBoxImage.Error);
            }
        }

        /// <summary>
        /// Handle byte modification event for auto-refresh
        /// Uses throttling to avoid excessive refreshes
        /// </summary>
        private void HexEditor_ByteModified(object sender, ByteModifiedEventArgs e)
        {
            if (!AutoRefreshParsedFields || _detectedFormat == null)
                return;

            // Mark that a refresh is pending
            _pendingAutoRefresh = true;

            // Restart the timer (throttle)
            _autoRefreshTimer.Stop();
            _autoRefreshTimer.Start();
        }

        /// <summary>
        /// Timer tick for auto-refresh throttling
        /// Triggers the actual refresh after the delay period
        /// </summary>
        private void AutoRefreshTimer_Tick(object sender, EventArgs e)
        {
            _autoRefreshTimer.Stop();

            if (_pendingAutoRefresh && AutoRefreshParsedFields)
            {
                _pendingAutoRefresh = false;
                ParseFieldsAsync();
            }
        }

        #endregion

        #region Field Parsing

        /// <summary>
        /// Parse fields from the currently open file using the detected format
        /// </summary>
        private async void ParseFieldsAsync()
        {
            if (Stream == null || _detectedFormat == null || ParsedFieldsPanel == null)
                return;

            try
            {
                await Dispatcher.InvokeAsync(() =>
                {
                    // Capture once: the DP getter is re-evaluated on each access; a tab switch
                    // between the pre-check and any subsequent use would yield null mid-lambda.
                    var panel = ParsedFieldsPanel;
                    if (panel == null) return;

                    // Suppress CollectionChanged → ApplyFilter during bulk population.
                    // BeginBulkUpdate covers both Clear() and all ParseBlocks() adds.
                    panel.BeginBulkUpdate();

                    // Clear existing fields and variables
                    panel.ParsedFields.Clear();
                    _variableContext?.Clear();
                    _parsedFieldCount = 0; // Reset performance counter
                    _formattedValueCache.Clear(); // Clear cache for new parsing session

                    // Initialize buffered reader for efficient file access
                    _bufferedReader?.Dispose();
                    _bufferedReader = new BufferedFileReader(Stream, 65536); // 64KB buffer

                    // Load format-defined variables
                    if (_detectedFormat.Variables != null)
                    {
                        foreach (var kvp in _detectedFormat.Variables)
                            _variableContext?.SetVariable(kvp.Key, kvp.Value);
                    }

                    // Load variables from detection (includes function execution results)
                    if (_detectionVariables != null)
                    {
                        foreach (var kvp in _detectionVariables)
                            _variableContext?.SetVariable(kvp.Key, kvp.Value);
                    }

                    // Update format info
                    panel.FormatInfo.IsDetected = true;
                    panel.FormatInfo.Name = _detectedFormat.FormatName;
                    panel.FormatInfo.Description = _detectedFormat.Description;
                    panel.FormatInfo.Category = _detectedFormat.Category ?? "Other";
                    panel.FormatInfo.References = _detectedFormat.References;

                    // Populate format candidates dropdown — suppress ComboBox SelectionChanged
                    // events during programmatic updates to avoid re-entrant RefreshParsedFields.
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
                        // Update selected candidate to match current format (after switch)
                        var match = panel.FormatInfo.Candidates?
                            .FirstOrDefault(c => c.Candidate?.Format?.FormatName == _detectedFormat.FormatName);
                        if (match != null)
                        {
                            panel.SuppressFormatCandidateEvents = true;
                            panel.FormatInfo.SetSelectedCandidateSilently(match);
                            panel.SuppressFormatCandidateEvents = false;
                        }
                    }

                    // Set total file size for coverage bar (C6)
                    panel.TotalFileSize = Length;

                    // Parse all blocks from the format definition, then fire ApplyFilter once.
                    if (_detectedFormat.Blocks != null)
                        ParseBlocks(_detectedFormat.Blocks, 0, panel);

                    panel.EndBulkUpdate(); // fires ApplyFilter exactly once
                }, System.Windows.Threading.DispatcherPriority.Background);
            }
            catch (Exception ex)
            {
                //System.Diagnostics.Debug.WriteLine($"Error parsing fields: {ex.Message}");
            }
            finally
            {
                // Cleanup buffered reader after parsing
                _bufferedReader?.Dispose();
                _bufferedReader = null;
            }
        }

        /// <summary>
        /// Recursively parse blocks and their nested children
        /// </summary>
        private void ParseBlocks(System.Collections.Generic.List<BlockDefinition> blocks, int depth, IParsedFieldsPanel panel)
        {
            // Performance safeguards
            if (blocks == null || depth > DepthLimit)
                return;

            if (_parsedFieldCount >= MaxFieldsLimit)
            {
                System.Diagnostics.Debug.WriteLine($"Field limit reached ({MaxFieldsLimit}). Stopping parsing to prevent UI freeze.");
                return;
            }

            foreach (var block in blocks)
            {
                try
                {
                    // Handle metadata blocks (from function execution)
                    if (block.Type == "metadata")
                    {
                        // Get variable value from context
                        if (!string.IsNullOrWhiteSpace(block.Variable))
                        {
                            var value = _variableContext?.GetVariable(block.Variable);
                            if (value != null)
                            {
                                // Create a field view model for the metadata
                                var metadataField = new ParsedFieldViewModel
                                {
                                    Name = block.Name,
                                    Offset = -1,  // Special value to indicate "not a byte range"
                                    Length = 0,
                                    ValueType = "metadata",
                                    RawValue = value,
                                    FormattedValue = value.ToString(),
                                    Description = block.Description ?? "",
                                    Color = "#E3F2FD",  // Light blue background for metadata
                                    IsValid = true,  // Mark as valid (not an error/warning)
                                    FieldIcon = "ℹ️",  // Information icon for metadata
                                    IndentLevel = depth,
                                    GroupName = "Computed Values"
                                };

                                panel.ParsedFields.Add(metadataField);
                                _parsedFieldCount++;
                            }
                        }
                        continue;
                    }

                    // Handle conditional blocks
                    if (block.Type == "conditional")
                    {
                        if (EvaluateCondition(block.Condition))
                        {
                            if (block.Then != null)
                                ParseBlocks(block.Then, depth + 1, panel);
                        }
                        else
                        {
                            if (block.Else != null)
                                ParseBlocks(block.Else, depth + 1, panel);
                        }
                        continue;
                    }

                    // Handle loop blocks
                    if (block.Type == "loop" && block.Body != null)
                    {
                        int count = ResolveLength(block.Count);
                        for (int i = 0; i < count && i < 1000; i++) // Safety limit: max 1000 iterations
                        {
                            // Store loop index variable
                            _variableContext?.SetVariable("i", i);
                            _variableContext?.SetVariable("index", i);

                            ParseBlocks(block.Body, depth + 1, panel);
                        }
                        continue;
                    }

                    // Handle computeFromVariables blocks (evaluate expression, store result)
                    if (block.Type?.ToLowerInvariant() == "computefromvariables")
                    {
                        if (!string.IsNullOrWhiteSpace(block.Expression) && !string.IsNullOrWhiteSpace(block.StoreAs))
                        {
                            long result = _expressionEvaluator?.Evaluate(block.Expression) ?? 0;
                            _variableContext?.SetVariable(block.StoreAs, result);
                        }
                        continue;
                    }

                    // Regular field blocks
                    // Calculate offset (handle variables and calculations)
                    long offset = ResolveOffset(block.Offset);
                    if (offset < 0 || offset >= Length)
                        continue;

                    // Calculate length (handle variables and calculations)
                    int length = ResolveLength(block.Length);
                    if (length <= 0 || offset + length > Length)
                        continue;

                    // Create field view model with indentation
                    var fieldVm = ParsedFieldViewModel.FromBlockDefinition(block, offset, length, depth);

                    // Assign group name for section headers (C3)
                    fieldVm.GroupName = block.Type?.ToLowerInvariant() switch
                    {
                        "signature" => "Signature",
                        _ => depth > 0 ? "Data Fields" : "Header Fields"
                    };

                    // Read and format value
                    ReadFieldValue(fieldVm);
                    FormatFieldValue(fieldVm);

                    // Store value as variable if specified
                    if (!string.IsNullOrWhiteSpace(block.StoreAs) && fieldVm.RawValue != null)
                    {
                        _variableContext?.SetVariable(block.StoreAs, fieldVm.RawValue);
                    }

                    // Apply field-level valueMap (translate raw value to human name)
                    if (block.ValueMap != null && fieldVm.RawValue != null)
                    {
                        string mapKey = fieldVm.RawValue.ToString();
                        if (block.ValueMap.TryGetValue(mapKey, out string mappedName))
                        {
                            fieldVm.FormattedValue = $"{fieldVm.RawValue} ({mappedName})";
                            if (!string.IsNullOrWhiteSpace(block.MappedValueStoreAs))
                                _variableContext?.SetVariable(block.MappedValueStoreAs, mappedName);
                        }
                    }

                    // Add to panel (skip hidden fields)
                    if (block.Hidden != true)
                    {
                        panel.ParsedFields.Add(fieldVm);
                        _parsedFieldCount++;

                    // Process bitfield extractions (create sub-field ViewModels)
                    if (block.Bitfields != null && fieldVm.RawValue != null)
                    {
                        try
                        {
                            long rawVal = Convert.ToInt64(fieldVm.RawValue);
                            foreach (var bf in block.Bitfields)
                            {
                                long bfValue = bf.ExtractValue(rawVal);

                                // Store in variable context
                                if (!string.IsNullOrWhiteSpace(bf.StoreAs))
                                    _variableContext?.SetVariable(bf.StoreAs, bfValue);

                                // Determine display value (with optional valueMap)
                                string displayValue = bfValue.ToString();
                                if (bf.ValueMap != null && bf.ValueMap.TryGetValue(bfValue.ToString(), out string bfMapped))
                                {
                                    displayValue = $"{bfValue} ({bfMapped})";
                                    if (!string.IsNullOrWhiteSpace(bf.StoreAs))
                                        _variableContext?.SetVariable(bf.StoreAs + "Name", bfMapped);
                                }

                                // Create sub-field ViewModel
                                var subField = new ParsedFieldViewModel
                                {
                                    Name = bf.Name ?? $"Bits {bf.Bits}",
                                    Offset = fieldVm.Offset,
                                    Length = fieldVm.Length,
                                    ValueType = "bitfield",
                                    RawValue = bfValue,
                                    FormattedValue = displayValue,
                                    Description = bf.Description ?? $"Bits {bf.Bits} of {fieldVm.Name}",
                                    Color = fieldVm.Color,
                                    IndentLevel = depth + 1,
                                    GroupName = "Bitfields",
                                    IsValid = true,
                                    FieldIcon = "\U0001f527"
                                };
                                panel.ParsedFields.Add(subField);
                                _parsedFieldCount++;
                            }
                        }
                        catch { /* Bitfield extraction failed, continue */ }
                    }

                        // Check limit after adding
                        if (_parsedFieldCount >= MaxFieldsLimit)
                        {
                            System.Diagnostics.Debug.WriteLine($"Reached maximum field limit ({MaxFieldsLimit})");
                            return;
                        }
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Error parsing block {block.Name}: {ex.Message}");
                }
            }
        }

        /// <summary>
        /// Resolve an offset value (handle int, var:name, calc:expression)
        /// </summary>
        private long ResolveOffset(object offsetValue)
        {
            return offsetValue switch
            {
                int intOffset => intOffset,
                long longOffset => longOffset,
                System.Text.Json.JsonElement jsonElement => ResolveJsonElementAsLong(jsonElement),
                string strOffset when strOffset.StartsWith("var:") =>
                    _variableContext?.GetVariableAsLong(strOffset.Substring(4)) ?? 0,
                string strOffset when strOffset.StartsWith("calc:") =>
                    _expressionEvaluator?.Evaluate(strOffset.Substring(5)) ?? 0,
                _ => 0
            };
        }

        /// <summary>
        /// Resolve a length value (handle int, var:name, calc:expression)
        /// </summary>
        private int ResolveLength(object lengthValue)
        {
            return lengthValue switch
            {
                int intLength => intLength,
                long longLength => (int)longLength, // JSON.NET deserializes numbers as Int64
                System.Text.Json.JsonElement jsonElement => (int)ResolveJsonElementAsLong(jsonElement),
                string strLength when strLength.StartsWith("var:") =>
                    (int)(_variableContext?.GetVariableAsLong(strLength.Substring(4)) ?? 0),
                string strLength when strLength.StartsWith("calc:") =>
                    (int)(_expressionEvaluator?.Evaluate(strLength.Substring(5)) ?? 0),
                _ => 1
            };
        }

        /// <summary>
        /// Resolve a JsonElement to a long value
        /// Handles numbers and strings (including var: and calc: prefixes)
        /// </summary>
        private long ResolveJsonElementAsLong(System.Text.Json.JsonElement element)
        {
            switch (element.ValueKind)
            {
                case System.Text.Json.JsonValueKind.Number:
                    return element.TryGetInt64(out long longValue) ? longValue :
                           element.TryGetInt32(out int intValue) ? intValue : 0;

                case System.Text.Json.JsonValueKind.String:
                    string strValue = element.GetString();
                    if (strValue.StartsWith("var:"))
                        return _variableContext?.GetVariableAsLong(strValue.Substring(4)) ?? 0;
                    else if (strValue.StartsWith("calc:"))
                        return _expressionEvaluator?.Evaluate(strValue.Substring(5)) ?? 0;
                    else if (long.TryParse(strValue, out long parsed))
                        return parsed;
                    return 0;

                default:
                    return 0;
            }
        }

        /// <summary>
        /// Evaluate a ConditionDefinition object
        /// </summary>
        private bool EvaluateCondition(ConditionDefinition condition)
        {
            if (condition == null)
                return true;

            try
            {
                // Get field value
                long fieldValue = 0;
                if (!string.IsNullOrWhiteSpace(condition.Field))
                {
                    if (condition.Field.StartsWith("offset:"))
                    {
                        // Read from file at offset
                        var offsetStr = condition.Field.Substring(7);
                        if (long.TryParse(offsetStr, out long offset))
                        {
                            var buffer = new byte[condition.Length];
                            Stream.Position = offset;
                            Stream.Read(buffer, 0, condition.Length);

                            // Convert to long based on length
                            fieldValue = condition.Length switch
                            {
                                1 => buffer[0],
                                2 => System.BitConverter.ToUInt16(buffer, 0),
                                4 => System.BitConverter.ToUInt32(buffer, 0),
                                8 => (long)System.BitConverter.ToUInt64(buffer, 0),
                                _ => buffer[0]
                            };
                        }
                    }
                    else if (condition.Field.StartsWith("var:"))
                    {
                        fieldValue = _variableContext?.GetVariableAsLong(condition.Field.Substring(4)) ?? 0;
                    }
                }

                // Get comparison value
                long compareValue = 0;
                if (!string.IsNullOrWhiteSpace(condition.Value))
                {
                    if (condition.Value.StartsWith("0x"))
                        long.TryParse(condition.Value.Substring(2), System.Globalization.NumberStyles.HexNumber, null, out compareValue);
                    else
                        long.TryParse(condition.Value, out compareValue);
                }

                // Compare based on operator
                return condition.Operator?.ToLowerInvariant() switch
                {
                    "equals" or "==" => fieldValue == compareValue,
                    "notequals" or "!=" => fieldValue != compareValue,
                    "greaterthan" or ">" => fieldValue > compareValue,
                    "lessthan" or "<" => fieldValue < compareValue,
                    "greaterorequal" or ">=" => fieldValue >= compareValue,
                    "lessorequal" or "<=" => fieldValue <= compareValue,
                    _ => false
                };
            }
            catch (Exception)
            {
                return false;
            }
        }

        /// <summary>
        /// Evaluate a conditional expression (string format).
        /// Supports comparison operators and variable references.
        /// </summary>
        private bool EvaluateCondition(string condition)
        {
            if (string.IsNullOrWhiteSpace(condition))
                return true; // Empty condition = always true

            try
            {
                // Find comparison operator
                string[] operators = { "==", "!=", "<=", ">=", "<", ">" };
                foreach (var op in operators)
                {
                    int opIndex = condition.IndexOf(op);
                    if (opIndex > 0)
                    {
                        string leftStr = condition.Substring(0, opIndex).Trim();
                        string rightStr = condition.Substring(opIndex + op.Length).Trim();

                        // Evaluate both sides
                        long leftValue = EvaluateConditionValue(leftStr);
                        long rightValue = EvaluateConditionValue(rightStr);

                        // Compare based on operator
                        return op switch
                        {
                            "==" => leftValue == rightValue,
                            "!=" => leftValue != rightValue,
                            "<" => leftValue < rightValue,
                            ">" => leftValue > rightValue,
                            "<=" => leftValue <= rightValue,
                            ">=" => leftValue >= rightValue,
                            _ => false
                        };
                    }
                }

                // No operator found, evaluate as boolean expression
                return EvaluateConditionValue(condition) != 0;
            }
            catch (Exception)
            {
                return false; // Evaluation error = false
            }
        }

        /// <summary>
        /// Evaluate a value in a condition (variable reference or expression)
        /// </summary>
        private long EvaluateConditionValue(string value)
        {
            value = value.Trim();

            if (value.StartsWith("var:"))
            {
                return _variableContext?.GetVariableAsLong(value.Substring(4)) ?? 0;
            }
            else if (value.StartsWith("calc:"))
            {
                return _expressionEvaluator?.Evaluate(value.Substring(5)) ?? 0;
            }
            else if (long.TryParse(value, out long numValue))
            {
                return numValue;
            }
            else if (value.StartsWith("0x") && long.TryParse(value.Substring(2), System.Globalization.NumberStyles.HexNumber, null, out long hexValue))
            {
                return hexValue;
            }
            else
            {
                // Try as variable name without "var:" prefix
                return _variableContext?.GetVariableAsLong(value) ?? 0;
            }
        }

        /// <summary>
        /// Read the raw value from the file for a given field (using buffered reader for performance)
        /// </summary>
        private void ReadFieldValue(ParsedFieldViewModel field)
        {
            if (Stream == null || field == null)
                return;

            try
            {
                // Use buffered reader if available (much faster for sequential reads)
                byte[] buffer;
                if (_bufferedReader != null)
                {
                    buffer = _bufferedReader.ReadBytes(field.Offset, field.Length);
                    if (buffer == null || buffer.Length != field.Length)
                        return; // Read failed
                }
                else
                {
                    // Fallback to direct stream read
                    buffer = new byte[field.Length];
                    Stream.Position = field.Offset;
                    int bytesRead = Stream.Read(buffer, 0, field.Length);
                    if (bytesRead != field.Length)
                        return; // Read failed
                }

                if (buffer.Length == field.Length)
                {
                    // Use FieldValueReader to parse the value
                    bool bigEndian = FieldValueReader.ShouldUseBigEndian(_detectedFormat?.FormatName);

                    // Per-field endianness override (Phase 4.2)
                    if (field.BlockDefinition?.Endianness != null)
                    {
                        bigEndian = field.BlockDefinition.Endianness
                            .Equals("big", System.StringComparison.OrdinalIgnoreCase);
                    }

                    field.RawValue = _fieldValueReader.ReadValue(buffer, 0, field.Length, field.ValueType, bigEndian);

                    // Validate the value if validation rules exist
                    if (field.BlockDefinition?.ValidationRules != null)
                    {
                        // Standard validation (range, enum, pattern)
                        var validationResult = _fieldValidator.Validate(field.RawValue, field.BlockDefinition.ValidationRules);
                        field.IsValid = validationResult.IsValid;
                        if (!validationResult.IsValid)
                        {
                            field.ValidationMessage = validationResult.Message;
                        }

                        // Checksum validation (requires full file data)
                        if (field.IsValid && field.BlockDefinition.ValidationRules.Checksum != null)
                        {
                            try
                            {
                                // Read full file data for checksum validation
                                long currentPos = Stream.Position;
                                long fileLength = Stream.Length;
                                int maxDataLength = (int)Math.Min(fileLength, 10 * 1024 * 1024); // Limit to 10MB for safety
                                byte[] fileData = new byte[maxDataLength];
                                Stream.Position = 0;
                                int bytesReadTotal = Stream.Read(fileData, 0, maxDataLength);
                                Stream.Position = currentPos; // Restore position

                                if (bytesReadTotal > 0)
                                {
                                    // Resize array if we read less
                                    if (bytesReadTotal < maxDataLength)
                                    {
                                        Array.Resize(ref fileData, bytesReadTotal);
                                    }

                                    var checksumResult = _fieldValidator.ValidateChecksum(fileData, field.BlockDefinition.ValidationRules.Checksum);
                                    if (!checksumResult.IsValid)
                                    {
                                        field.IsValid = false;
                                        field.ValidationMessage = checksumResult.Message;
                                    }
                                }
                            }
                            catch (Exception ex)
                            {
                                field.IsValid = false;
                                field.ValidationMessage = $"Checksum validation error: {ex.Message}";
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error reading field value: {ex.Message}");
                field.IsValid = false;
                field.ValidationMessage = ex.Message;
            }
        }

        /// <summary>
        /// Format a field's raw value using the current formatter (with caching)
        /// </summary>
        private void FormatFieldValue(ParsedFieldViewModel field)
        {
            if (field?.RawValue == null || _currentFormatter == null)
                return;

            try
            {
                // Try to get from cache first
                string formatterType = _currentFormatter.DisplayName;
                if (_formattedValueCache.TryGet(field.Offset, field.Length, field.ValueType, formatterType, field.RawValue, out string cachedValue))
                {
                    field.FormattedValue = cachedValue;
                    return;
                }

                // Cache miss - format the value
                string formattedValue;
                if (_currentFormatter.Supports(field.ValueType))
                {
                    formattedValue = _currentFormatter.Format(field.RawValue, field.ValueType, field.Length);
                }
                else
                {
                    // Fallback to hex formatter
                    var hexFormatter = new HexValueFormatter();
                    formattedValue = hexFormatter.Format(field.RawValue, field.ValueType, field.Length);
                }

                // Store in cache and update field
                _formattedValueCache.Set(field.Offset, field.Length, field.ValueType, formatterType, field.RawValue, formattedValue);
                field.FormattedValue = formattedValue;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error formatting field value: {ex.Message}");
                field.FormattedValue = "<format error>";
            }
        }

        #endregion

        #region Public Methods

        /// <summary>
        /// Connecte un ParsedFieldsPanel externe à cet éditeur.
        /// Les événements sont câblés automatiquement via la DP.
        /// </summary>
        public void ConnectParsedFieldsPanel(IParsedFieldsPanel panel)
            => ParsedFieldsPanel = panel;

        /// <summary>
        /// Déconnecte le ParsedFieldsPanel actuel.
        /// </summary>
        public void DisconnectParsedFieldsPanel()
            => ParsedFieldsPanel = null;

        /// <summary>
        /// Refresh parsed fields (public API)
        /// </summary>
        public void RefreshParsedFields()
        {
            ParseFieldsAsync();
        }

        /// <summary>
        /// Clear format detection state (called when file is closed)
        /// Resets detected format, parsed fields, and variable context
        /// </summary>
        internal void ClearFormatDetectionState()
        {
            try
            {
                // NOTE: Don't clear CustomBackgroundBlocks here - AutoDetectAndApplyFormat()
                // will clear them before applying new ones. Clearing here interferes with
                // automatic detection at startup.

                // Clear detected format
                _detectedFormat = null;
                _detectionVariables = null; // Clear function execution results
                _detectionCandidates = null; // Clear candidates for format selector

                // Clear format candidates dropdown
                if (ParsedFieldsPanel?.FormatInfo != null)
                {
                    ParsedFieldsPanel.FormatInfo.Candidates = null;
                }

                // Clear variable context (don't set to null - just clear contents to preserve object)
                _variableContext?.Clear();
                // Recreate expression evaluator for clean state
                if (_variableContext != null)
                {
                    _expressionEvaluator = new ExpressionEvaluator(_variableContext);
                }

                // Clear formatted value cache
                _formattedValueCache?.Clear();

                // Clear buffered reader
                _bufferedReader?.Dispose();
                _bufferedReader = null;

                // Clear parsed fields panel
                if (ParsedFieldsPanel != null)
                {
                    ParsedFieldsPanel.Clear();
                }

                // Reset performance tracking
                _parsedFieldCount = 0;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[HexEditor] Error clearing format detection state: {ex.Message}");
            }
        }

        #endregion
    }
}
