// ==========================================================
// Project: WpfHexEditor.Core
// File: FieldParsingEngine.cs
// Author: Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude (Anthropic)
// Created: 2026-03-26
// Description:
//     Recursive block traversal engine for whfmt format definitions.
//     Creates ParsedFieldViewModel instances from BlockDefinition trees.
//     Extracted from HexEditor.ParsedFieldsIntegration.ParseBlocks() for reuse.
//
// Architecture Notes:
//     Reads bytes via IBinaryDataSource (through BufferedDataSourceReader).
//     No HexEditor dependency. Uses FieldValueReader (Core) for type decoding.
// ==========================================================

using System;
using System.Collections.Generic;
using WpfHexEditor.Core.FormatDetection;
using WpfHexEditor.Core.Interfaces;
using WpfHexEditor.Core.ViewModels;

namespace WpfHexEditor.Core.Services.FormatParsing
{
    /// <summary>
    /// Recursive block traversal engine for whfmt field parsing.
    /// </summary>
    internal sealed class FieldParsingEngine
    {
        private const int MaxFieldsLimit = 500;
        private const int DepthLimit = 10;

        private readonly IBinaryDataSource _source;
        private readonly BufferedDataSourceReader _reader;
        private readonly VariableContext _variableContext;
        private readonly ExpressionEvaluator _expressionEvaluator;
        private readonly FieldValueReader _fieldValueReader;
        private readonly FieldValidator _fieldValidator;
        private readonly ChecksumValidator _checksumValidator;
        private readonly FieldFormattingService _formattingService;
        private readonly string _formatName;

        private int _parsedFieldCount;

        public FieldParsingEngine(
            IBinaryDataSource source,
            BufferedDataSourceReader reader,
            VariableContext variableContext,
            ExpressionEvaluator expressionEvaluator,
            FieldFormattingService formattingService,
            string formatName)
        {
            _source = source;
            _reader = reader;
            _variableContext = variableContext;
            _expressionEvaluator = expressionEvaluator;
            _fieldValueReader = new FieldValueReader();
            _fieldValidator = new FieldValidator();
            _checksumValidator = new ChecksumValidator();
            _formattingService = formattingService;
            _formatName = formatName;
            _parsedFieldCount = 0;
        }

        /// <summary>Number of fields parsed so far.</summary>
        public int ParsedFieldCount => _parsedFieldCount;

        /// <summary>
        /// Recursively parse blocks and their nested children.
        /// Adds ParsedFieldViewModel instances to <paramref name="fields"/>.
        /// </summary>
        public void ParseBlocks(
            List<BlockDefinition> blocks,
            int depth,
            IList<ParsedFieldViewModel> fields)
        {
            if (blocks == null || depth > DepthLimit)
                return;

            if (_parsedFieldCount >= MaxFieldsLimit)
                return;

            foreach (var block in blocks)
            {
                try
                {
                    if (block.Type == "metadata")
                    {
                        ParseMetadataBlock(block, depth, fields);
                        continue;
                    }

                    if (block.Type == "conditional")
                    {
                        ParseConditionalBlock(block, depth, fields);
                        continue;
                    }

                    if (block.Type == "loop" && block.Body != null)
                    {
                        ParseLoopBlock(block, depth, fields);
                        continue;
                    }

                    if (block.Type?.ToLowerInvariant() == "computefromvariables")
                    {
                        ParseComputeBlock(block);
                        continue;
                    }

                    ParseRegularBlock(block, depth, fields);
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Error parsing block {block.Name}: {ex.Message}");
                }
            }
        }

        private void ParseMetadataBlock(BlockDefinition block, int depth, IList<ParsedFieldViewModel> fields)
        {
            if (string.IsNullOrWhiteSpace(block.Variable)) return;

            var value = _variableContext?.GetVariable(block.Variable);
            if (value == null) return;

            var metadataField = new ParsedFieldViewModel
            {
                Name = block.Name,
                Offset = -1,
                Length = 0,
                ValueType = "metadata",
                RawValue = value,
                FormattedValue = value.ToString(),
                Description = block.Description ?? "",
                Color = "#E3F2FD",
                IsValid = true,
                FieldIcon = "\uE946",
                IndentLevel = depth,
                GroupName = "Computed Values"
            };

            fields.Add(metadataField);
            _parsedFieldCount++;
        }

        private void ParseConditionalBlock(BlockDefinition block, int depth, IList<ParsedFieldViewModel> fields)
        {
            if (ConditionEvaluator.Evaluate(block.Condition, _source, _variableContext))
            {
                if (block.Then != null)
                    ParseBlocks(block.Then, depth + 1, fields);
            }
            else
            {
                if (block.Else != null)
                    ParseBlocks(block.Else, depth + 1, fields);
            }
        }

        private void ParseLoopBlock(BlockDefinition block, int depth, IList<ParsedFieldViewModel> fields)
        {
            int count = FieldValueResolver.ResolveLength(block.Count, _variableContext, _expressionEvaluator);
            for (int i = 0; i < count && i < 1000; i++)
            {
                _variableContext?.SetVariable("i", i);
                _variableContext?.SetVariable("index", i);
                ParseBlocks(block.Body, depth + 1, fields);
            }
        }

        private void ParseComputeBlock(BlockDefinition block)
        {
            if (!string.IsNullOrWhiteSpace(block.Expression) && !string.IsNullOrWhiteSpace(block.StoreAs))
            {
                long result = _expressionEvaluator?.Evaluate(block.Expression) ?? 0;
                _variableContext?.SetVariable(block.StoreAs, result);
            }
        }

        private void ParseRegularBlock(BlockDefinition block, int depth, IList<ParsedFieldViewModel> fields)
        {
            long offset = FieldValueResolver.ResolveOffset(block.Offset, _variableContext, _expressionEvaluator);
            if (offset < 0 || offset >= _source.Length)
                return;

            int length = FieldValueResolver.ResolveLength(block.Length, _variableContext, _expressionEvaluator);
            if (length <= 0 || offset + length > _source.Length)
                return;

            var fieldVm = ParsedFieldViewModel.FromBlockDefinition(block, offset, length, depth);

            fieldVm.GroupName = block.Type?.ToLowerInvariant() switch
            {
                "signature" => "Signature",
                _ => depth > 0 ? "Data Fields" : "Header Fields"
            };

            ReadFieldValue(fieldVm);
            _formattingService.FormatFieldValue(fieldVm);

            // Store value as variable if specified
            if (!string.IsNullOrWhiteSpace(block.StoreAs) && fieldVm.RawValue != null)
                _variableContext?.SetVariable(block.StoreAs, fieldVm.RawValue);

            // Apply field-level valueMap
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

            // Add to fields (skip hidden)
            if (block.Hidden != true)
            {
                fields.Add(fieldVm);
                _parsedFieldCount++;

                // Process bitfield extractions
                if (block.Bitfields != null && fieldVm.RawValue != null)
                    ParseBitfields(block, fieldVm, depth, fields);

                if (_parsedFieldCount >= MaxFieldsLimit)
                    return;
            }
        }

        private void ParseBitfields(BlockDefinition block, ParsedFieldViewModel fieldVm, int depth, IList<ParsedFieldViewModel> fields)
        {
            try
            {
                long rawVal = Convert.ToInt64(fieldVm.RawValue);
                foreach (var bf in block.Bitfields)
                {
                    long bfValue = bf.ExtractValue(rawVal);

                    if (!string.IsNullOrWhiteSpace(bf.StoreAs))
                        _variableContext?.SetVariable(bf.StoreAs, bfValue);

                    string displayValue = bfValue.ToString();
                    if (bf.ValueMap != null && bf.ValueMap.TryGetValue(bfValue.ToString(), out string bfMapped))
                    {
                        displayValue = $"{bfValue} ({bfMapped})";
                        if (!string.IsNullOrWhiteSpace(bf.StoreAs))
                            _variableContext?.SetVariable(bf.StoreAs + "Name", bfMapped);
                    }

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
                        FieldIcon = "\uE71D"
                    };
                    fields.Add(subField);
                    _parsedFieldCount++;
                }
            }
            catch { /* Bitfield extraction failed, continue */ }
        }

        private void ReadFieldValue(ParsedFieldViewModel field)
        {
            if (field == null) return;

            try
            {
                byte[] buffer = _reader.ReadBytes(field.Offset, field.Length);
                if (buffer == null || buffer.Length != field.Length)
                    return;

                bool bigEndian = FieldValueReader.ShouldUseBigEndian(_formatName);

                if (field.BlockDefinition?.Endianness != null)
                {
                    bigEndian = field.BlockDefinition.Endianness
                        .Equals("big", StringComparison.OrdinalIgnoreCase);
                }

                field.RawValue = _fieldValueReader.ReadValue(buffer, 0, field.Length, field.ValueType, bigEndian);

                if (field.BlockDefinition?.ValidationRules != null)
                {
                    var validationResult = _fieldValidator.Validate(field.RawValue, field.BlockDefinition.ValidationRules);
                    field.IsValid = validationResult.IsValid;
                    if (!validationResult.IsValid)
                        field.ValidationMessage = validationResult.Message;

                    if (field.IsValid && field.BlockDefinition.ValidationRules.Checksum != null)
                        ValidateChecksum(field);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error reading field value: {ex.Message}");
                field.IsValid = false;
                field.ValidationMessage = ex.Message;
            }
        }

        private void ValidateChecksum(ParsedFieldViewModel field)
        {
            try
            {
                long fileLength = _source.Length;
                int maxDataLength = (int)Math.Min(fileLength, 10 * 1024 * 1024);
                byte[] fileData = _source.ReadBytes(0, maxDataLength);

                if (fileData != null && fileData.Length > 0)
                {
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
