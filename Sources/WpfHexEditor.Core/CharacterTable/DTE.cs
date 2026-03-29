// ==========================================================
// Project: WpfHexEditor.Core
// File: DTE.cs
// Author: Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude (Anthropic)
// Created: 2026-03-06
// Description:
//     Represents a single DTE (Dual/Multiple Title Encoding) entry from a TBL file,
//     mapping a hex byte sequence to a character string for ROM hacking character
//     table translation. Includes validation and type classification.
//
// Architecture Notes:
//     Sealed class for DTE entries used in TBLStream. Validates entry format via
//     regex. DteType enum distinguishes ASCII, Japanese, DTE, MTE, EndLine, EndBlock.
//     No WPF dependencies — pure domain model.
//
// ==========================================================

using System;
using System.Text.RegularExpressions;

namespace WpfHexEditor.Core.CharacterTable
{
    /// <summary>
    /// Object representing a DTE with validation and metadata
    /// </summary>
    public sealed class Dte
    {
        /// <summary>
        /// DTE name
        /// </summary>
        private string _entry;

        #region Constructeurs

        /// <summary>
        /// Main constructor
        /// </summary>
        public Dte()
        {
            _entry = string.Empty;
            Type = DteType.Invalid;
            Value = string.Empty;
            IsValid = true;
        }

        /// <summary>
        /// Constructor to add an entry and a value
        /// </summary>
        /// <param name="entry">DTE name</param>
        /// <param name="value">DTE value</param>
        public Dte(string entry, string value)
        {
            _entry = entry?.ToUpperInvariant() ?? string.Empty;
            Value = value ?? string.Empty;
            Type = DetectTypeFromEntry(_entry, value);
            ValidateEntry();
        }

        /// <summary>
        /// Constructor to add an entry, a value and a description
        /// </summary>
        /// <param name="entry">DTE name</param>
        /// <param name="value">DTE value</param>
        /// <param name="type">DTE type</param>
        public Dte(string entry, string value, DteType type)
        {
            _entry = entry?.ToUpperInvariant() ?? string.Empty;
            Value = value ?? string.Empty;
            Type = type;
            ValidateEntry();
        }

        #endregion Constructeurs

        #region Type Detection

        /// <summary>
        /// Detect DTE type based on entry hex string and value
        /// </summary>
        private static DteType DetectTypeFromEntry(string entry, string value)
        {
            if (string.IsNullOrEmpty(entry))
                return DteType.Invalid;

            // Check for special types
            if (entry.StartsWith("/"))
                return DteType.EndBlock;
            if (entry.StartsWith("*"))
                return DteType.EndLine;

            // Detect based on hex entry length
            int hexLength = entry.Length;

            // Must be even number of hex chars (each byte = 2 hex chars)
            if (hexLength % 2 != 0)
                return DteType.Invalid;

            // Must be valid hex characters
            if (!System.Text.RegularExpressions.Regex.IsMatch(entry, "^[0-9A-Fa-f]+$"))
                return DteType.Invalid;

            // Detect type by byte count
            switch (hexLength)
            {
                case 2:
                    // 1 byte = ASCII
                    return DteType.Ascii;
                case 4:
                    // 2 bytes = DTE (Dual Title Encoding)
                    return DteType.DualTitleEncoding;
                case >= 6 when hexLength <= 16:
                    // 3-8 bytes = MTE (Multiple Title Encoding)
                    return DteType.MultipleTitleEncoding;
                default:
                    return DteType.Invalid;
            }
        }

        #endregion Type Detection

        #region Validation

        /// <summary>
        /// Validate entry format (called in constructor)
        /// </summary>
        private void ValidateEntry()
        {
            // Skip validation for special types
            if (Type == DteType.EndBlock || Type == DteType.EndLine)
            {
                IsValid = true;
                return;
            }

            // Validate hex entry
            if (string.IsNullOrWhiteSpace(_entry))
            {
                IsValid = false;
                ValidationError = "Entry cannot be empty";
                return;
            }

            // Must be even length (2, 4, 6, 8, ..., 16)
            if (_entry.Length % 2 != 0)
            {
                IsValid = false;
                ValidationError = "Hex entry must have even number of characters";
                return;
            }

            // Length limits: 2-16 chars (1-8 bytes)
            if (_entry.Length < 2 || _entry.Length > 16)
            {
                IsValid = false;
                ValidationError = $"Hex entry length must be 2-16 characters (1-8 bytes), got {_entry.Length}";
                return;
            }

            // All chars must be hex digits (0-9, A-F)
            if (!Regex.IsMatch(_entry, "^[0-9A-Fa-f]+$"))
            {
                IsValid = false;
                ValidationError = "Hex entry must contain only hex digits (0-9, A-F)";
                return;
            }

            IsValid = true;
            ValidationError = null;
        }

        #endregion Validation

        #region Properties

        /// <summary>
        /// DTE name
        /// </summary>
        public string Entry
        {
            set => _entry = value is not null
                ? value.ToUpperInvariant()
                : string.Empty;
            get => _entry;
        }

        /// <summary>
        /// DTE value
        /// </summary>
        public string Value { get; }

        /// <summary>
        /// DTE type
        /// </summary>
        public DteType Type { get; }

        /// <summary>
        /// Validation state
        /// </summary>
        public bool IsValid { get; set; } = true;

        /// <summary>
        /// Validation error message (null if valid)
        /// </summary>
        public string ValidationError { get; set; }

        /// <summary>
        /// Parameters for control codes (for future use with $ format)
        /// </summary>
        public string Parameters { get; set; }

        /// <summary>
        /// Inline comment (for future use)
        /// </summary>
        public string Comment { get; set; }

        #endregion Properties

        #region Methods

        /// <summary>
        /// This function returns the DTE in the form: [Entry]=[Value]
        /// </summary>
        /// <returns>Returns the DTE in the form: [Entry]=[Value]</returns>
        public override string ToString() => Type != DteType.EndBlock && Type != DteType.EndLine
            ? _entry + "=" + Value
            : _entry;

        #endregion Methods

        #region Static Methods

        public static DteType TypeDte(Dte dteValue)
        {
            if (dteValue == null) return DteType.Invalid;

            try
            {
                switch (dteValue._entry.Length)
                {
                    case 2:
                        return dteValue.Value.Length == 2 ? DteType.Ascii : DteType.DualTitleEncoding;
                    case >= 4 when dteValue._entry.Length % 2 == 0 && dteValue._entry.Length <= 16:
                        return DteType.MultipleTitleEncoding;  // 2-8 bytes (4-16 hex chars)
                }
            }
            catch (IndexOutOfRangeException)
            {
                switch (dteValue._entry)
                {
                    case @"/":
                        return DteType.EndBlock;

                    case @"*":
                        return DteType.EndLine;
                        //case @"\":
                }
            }
            catch (ArgumentOutOfRangeException)
            {
                //Due to an entry that has 2 = in a row... EX:  XX==
                return DteType.DualTitleEncoding;
            }

            return DteType.Invalid;
        }

        public static DteType TypeDte(string dteValue)
        {
            if (dteValue == null) return DteType.Invalid;

            try
            {
                if (dteValue == Properties.Resources.EndTagString)
                    return DteType.EndBlock; //<end>

                if (dteValue == Properties.Resources.LineTagString)
                    return DteType.EndLine; //<ln>

                switch (dteValue.Length)
                {
                    case 1:
                        return DteType.Ascii;
                    case 2:
                        return DteType.DualTitleEncoding;
                }

                if (dteValue.Length > 2)
                    return DteType.MultipleTitleEncoding;
            }
            catch (ArgumentOutOfRangeException)
            {
                //Due to an entry that has 2 = in a row... EX:  XX==
                return DteType.DualTitleEncoding;
            }

            return DteType.Invalid;
        }

        #endregion Static Methods

    }
}
