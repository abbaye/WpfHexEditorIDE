// ==========================================================
// Project: WpfHexEditor.Core
// File: FieldFormattingService.cs
// Author: Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude (Anthropic)
// Created: 2026-03-26
// Description:
//     Manages field value formatting with caching.
//     Supports switching between hex, decimal, mixed, and string formatters.
//     Extracted from HexEditor.ParsedFieldsIntegration.cs for reuse.
//
// Architecture Notes:
//     Owns the IFieldValueFormatter instance and FormattedValueCache.
//     Thread-safe for read-only access after construction.
// ==========================================================

using System;
using WpfHexEditor.Core.Formatters;
using WpfHexEditor.Core.Interfaces;
using WpfHexEditor.Core.ViewModels;

namespace WpfHexEditor.Core.Services.FormatParsing
{
    /// <summary>
    /// Manages field value formatting with caching.
    /// </summary>
    internal sealed class FieldFormattingService
    {
        private IFieldValueFormatter _currentFormatter;
        private readonly FormattedValueCache _cache = new FormattedValueCache();

        public FieldFormattingService()
        {
            _currentFormatter = new MixedValueFormatter();
        }

        /// <summary>Current formatter display name (for cache keying).</summary>
        public string CurrentFormatterType => _currentFormatter.DisplayName;

        /// <summary>
        /// Switch the active formatter. Clears the cache.
        /// </summary>
        public void SetFormatter(string formatterType)
        {
            _currentFormatter = formatterType switch
            {
                "hex" => new HexValueFormatter(),
                "decimal" => new DecimalValueFormatter(),
                "string" => new StringValueFormatter(),
                "mixed" => new MixedValueFormatter(),
                _ => _currentFormatter
            };
            _cache.Clear();
        }

        /// <summary>
        /// Format a field's raw value using the current formatter (with caching).
        /// </summary>
        public void FormatFieldValue(ParsedFieldViewModel field)
        {
            if (field?.RawValue == null || _currentFormatter == null)
                return;

            try
            {
                string formatterType = _currentFormatter.DisplayName;
                if (_cache.TryGet(field.Offset, field.Length, field.ValueType, formatterType, field.RawValue, out string cachedValue))
                {
                    field.FormattedValue = cachedValue;
                    return;
                }

                string formattedValue;
                if (_currentFormatter.Supports(field.ValueType))
                {
                    formattedValue = _currentFormatter.Format(field.RawValue, field.ValueType, field.Length);
                }
                else
                {
                    var hexFormatter = new HexValueFormatter();
                    formattedValue = hexFormatter.Format(field.RawValue, field.ValueType, field.Length);
                }

                _cache.Set(field.Offset, field.Length, field.ValueType, formatterType, field.RawValue, formattedValue);
                field.FormattedValue = formattedValue;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error formatting field value: {ex.Message}");
                field.FormattedValue = "<format error>";
            }
        }

        /// <summary>Clear the formatting cache.</summary>
        public void ClearCache() => _cache.Clear();
    }
}
