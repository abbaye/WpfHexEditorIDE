//////////////////////////////////////////////
// Apache 2.0  - 2026
// Contributors: Claude Sonnet 4.5
//////////////////////////////////////////////

using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using WpfHexaEditor.Core;
using WpfHexaEditor.Core.FormatDetection;
using WpfHexaEditor.Formatters;
using WpfHexaEditor.Interfaces;
using WpfHexaEditor.ViewModels;
using WpfHexaEditor.Views.Panels;

namespace WpfHexaEditor
{
    public partial class HexEditor
    {
        #region Dependency Properties

        /// <summary>
        /// Visibility of the parsed fields panel
        /// </summary>
        public static readonly DependencyProperty ParsedFieldsPanelVisibilityProperty =
            DependencyProperty.Register(
                nameof(ParsedFieldsPanelVisibility),
                typeof(Visibility),
                typeof(HexEditor),
                new PropertyMetadata(Visibility.Visible, OnParsedFieldsPanelVisibilityChanged));

        public Visibility ParsedFieldsPanelVisibility
        {
            get => (Visibility)GetValue(ParsedFieldsPanelVisibilityProperty);
            set => SetValue(ParsedFieldsPanelVisibilityProperty, value);
        }

        private static void OnParsedFieldsPanelVisibilityChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is HexEditor editor && e.NewValue is Visibility visibility)
            {
                editor.UpdateParsedFieldsPanelState(visibility);
            }
        }

        #endregion

        #region Fields

        private IFieldValueFormatter _currentFormatter;
        private readonly FieldValueReader _fieldValueReader = new FieldValueReader();
        private FormatDefinition _detectedFormat;
        private VariableContext _variableContext;
        private ExpressionEvaluator _expressionEvaluator;

        #endregion

        #region Initialization

        /// <summary>
        /// Initialize parsed fields panel integration
        /// Called from HexEditor constructor
        /// </summary>
        private void InitializeParsedFieldsPanel()
        {
            // Set default formatter
            _currentFormatter = new HexValueFormatter();

            // Initialize variable context and expression evaluator
            _variableContext = new VariableContext();
            _expressionEvaluator = new ExpressionEvaluator(_variableContext);

            // Wire up events
            if (ParsedFieldsPanel != null)
            {
                ParsedFieldsPanel.FieldSelected += ParsedFieldsPanel_FieldSelected;
                ParsedFieldsPanel.RefreshRequested += ParsedFieldsPanel_RefreshRequested;
                ParsedFieldsPanel.FormatterChanged += ParsedFieldsPanel_FormatterChanged;
            }
        }

        private void UpdateParsedFieldsPanelState(Visibility visibility)
        {
            // Additional logic when panel visibility changes
            if (visibility == Visibility.Visible && ParsedFieldsPanel != null)
            {
                // Refresh parsed fields if file is open
                if (Stream != null && _detectedFormat != null)
                {
                    ParseFieldsAsync();
                }
            }
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
                // Set selection in hex editor to match the field
                // SetPosition automatically scrolls to make position visible
                SetPosition(field.Offset, 1);
                SelectionStart = field.Offset;
                SelectionStop = field.Offset + field.Length - 1;

                // Optionally highlight with a custom background block
                if (field.Color != null)
                {
                    // The CustomBackground system will handle visual highlighting
                    RefreshView();
                }
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
                "mixed" => new HexValueFormatter(), // TODO: Implement mixed formatter
                _ => _currentFormatter
            };

            // Re-format all existing fields
            if (ParsedFieldsPanel?.ParsedFields != null)
            {
                foreach (var field in ParsedFieldsPanel.ParsedFields)
                {
                    FormatFieldValue(field);
                }
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
                    // Clear existing fields and variables
                    ParsedFieldsPanel.ParsedFields.Clear();
                    _variableContext?.Clear();

                    // Load format-defined variables
                    if (_detectedFormat.Variables != null)
                    {
                        foreach (var kvp in _detectedFormat.Variables)
                        {
                            _variableContext?.SetVariable(kvp.Key, kvp.Value);
                        }
                    }

                    // Update format info
                    ParsedFieldsPanel.FormatInfo.IsDetected = true;
                    ParsedFieldsPanel.FormatInfo.Name = _detectedFormat.FormatName;
                    ParsedFieldsPanel.FormatInfo.Description = _detectedFormat.Description;

                    // Parse all blocks from the format definition
                    if (_detectedFormat.Blocks != null)
                    {
                        ParseBlocks(_detectedFormat.Blocks, 0);
                    }
                });
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error parsing fields: {ex.Message}");
            }
        }

        /// <summary>
        /// Recursively parse blocks and their nested children
        /// </summary>
        private void ParseBlocks(System.Collections.Generic.List<BlockDefinition> blocks, int depth)
        {
            if (blocks == null || depth > 10) // Prevent infinite recursion
                return;

            foreach (var block in blocks)
            {
                try
                {
                    // Handle conditional blocks
                    if (block.Type == "conditional")
                    {
                        if (EvaluateCondition(block.Condition))
                        {
                            if (block.Then != null)
                                ParseBlocks(block.Then, depth + 1);
                        }
                        else
                        {
                            if (block.Else != null)
                                ParseBlocks(block.Else, depth + 1);
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

                            ParseBlocks(block.Body, depth + 1);
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

                    // Create field view model
                    var fieldVm = ParsedFieldViewModel.FromBlockDefinition(block, offset, length);

                    // Read and format value
                    ReadFieldValue(fieldVm);
                    FormatFieldValue(fieldVm);

                    // Store value as variable if specified
                    if (!string.IsNullOrWhiteSpace(block.StoreAs) && fieldVm.RawValue != null)
                    {
                        _variableContext?.SetVariable(block.StoreAs, fieldVm.RawValue);
                    }

                    // Add to panel (skip hidden fields)
                    if (block.Hidden != true)
                    {
                        ParsedFieldsPanel.ParsedFields.Add(fieldVm);
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
                string strLength when strLength.StartsWith("var:") =>
                    (int)(_variableContext?.GetVariableAsLong(strLength.Substring(4)) ?? 0),
                string strLength when strLength.StartsWith("calc:") =>
                    (int)(_expressionEvaluator?.Evaluate(strLength.Substring(5)) ?? 0),
                _ => 1
            };
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
        /// Evaluate a conditional expression (string format)
        /// Supports: ==, !=, <, >, <=, >=
        /// Example: "width > 0", "type == 1", "var:flags != 0"
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
        /// Read the raw value from the file for a given field
        /// </summary>
        private void ReadFieldValue(ParsedFieldViewModel field)
        {
            if (Stream == null || field == null)
                return;

            try
            {
                // Read bytes from stream
                var buffer = new byte[field.Length];
                Stream.Position = field.Offset;
                int bytesRead = Stream.Read(buffer, 0, field.Length);

                if (bytesRead == field.Length)
                {
                    // Use FieldValueReader to parse the value
                    bool bigEndian = FieldValueReader.ShouldUseBigEndian(_detectedFormat?.FormatName);
                    field.RawValue = _fieldValueReader.ReadValue(buffer, 0, field.Length, field.ValueType, bigEndian);
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
        /// Format a field's raw value using the current formatter
        /// </summary>
        private void FormatFieldValue(ParsedFieldViewModel field)
        {
            if (field?.RawValue == null || _currentFormatter == null)
                return;

            try
            {
                if (_currentFormatter.Supports(field.ValueType))
                {
                    field.FormattedValue = _currentFormatter.Format(field.RawValue, field.ValueType, field.Length);
                }
                else
                {
                    // Fallback to hex formatter
                    var hexFormatter = new HexValueFormatter();
                    field.FormattedValue = hexFormatter.Format(field.RawValue, field.ValueType, field.Length);
                }
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
        /// Toggle the visibility of the parsed fields panel
        /// </summary>
        public void ToggleParsedFieldsPanel()
        {
            ParsedFieldsPanelVisibility = ParsedFieldsPanelVisibility == Visibility.Visible
                ? Visibility.Collapsed
                : Visibility.Visible;
        }

        /// <summary>
        /// Refresh parsed fields (public API)
        /// </summary>
        public void RefreshParsedFields()
        {
            ParseFieldsAsync();
        }

        #endregion
    }
}
