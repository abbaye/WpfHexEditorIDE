// ==========================================================
// Project: WpfHexEditor.HexEditor
// File: HexEditor.PropertyProvider.cs
// Author: Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude (Anthropic)
// Created: 2026-03-06
// Description:
//     Partial class implementing IPropertyProvider for the HexEditor.
//     Exposes editor state (file path, size, offset, encoding, format) as
//     structured property entries for display in the PropertiesPanel.
//
// Architecture Notes:
//     Implements IPropertyProvider interface from WpfHexEditor.Editor.Core.
//     Returns property groups refreshed on file open, selection change, and format detection.
//
// ==========================================================

using System;
using System.Collections.Generic;
using System.Text;
using WpfHexEditor.Core.Models;
using WpfHexEditor.Editor.Core;

namespace WpfHexEditor.HexEditor
{
    /// <summary>
    /// IPropertyProvider that surfaces the byte(s) at the current HexEditor selection
    /// plus document-level settings in the Properties panel.
    /// </summary>
    internal sealed class HexEditorPropertyProvider : IPropertyProvider
    {
        private readonly HexEditor _editor;

        public HexEditorPropertyProvider(HexEditor editor)
        {
            _editor = editor;
            // Refresh the panel whenever the selection changes
            _editor.SelectionChanged += (_, _) => PropertiesChanged?.Invoke(this, EventArgs.Empty);
        }

        public string ContextLabel
        {
            get
            {
                var name = _editor.FileName?.Length > 0
                    ? System.IO.Path.GetFileName(_editor.FileName)
                    : "Untitled";
                var sel  = _editor.SelectionStart;
                return sel >= 0
                    ? $"{name} — Byte at 0x{sel:X8}"
                    : name;
            }
        }

        public event EventHandler? PropertiesChanged;

        public IReadOnlyList<PropertyGroup> GetProperties()
        {
            var groups = new List<PropertyGroup>();
            var sel    = _editor.SelectionStart;
            var len    = _editor.SelectionLength;

            // -- Position -----------------------------------------------------
            groups.Add(new PropertyGroup
            {
                Name    = "Position",
                Entries = new List<PropertyEntry>
                {
                    new() { Name = "Offset (hex)", Value = sel >= 0 ? $"0x{sel:X8}" : "—",
                            Type = PropertyEntryType.Hex,   Description = "Hex address of the selected byte." },
                    new() { Name = "Offset (dec)", Value = sel >= 0 ? sel.ToString() : "—",
                            Type = PropertyEntryType.Integer, Description = "Decimal address of the selected byte." },
                    new() { Name = "Line",   Value = sel >= 0 ? (sel / _editor.BytePerLine + 1).ToString() : "—",
                            Description = "Line number (1-based) of the selected byte." },
                    new() { Name = "Column", Value = sel >= 0 ? (sel % _editor.BytePerLine + 1).ToString() : "—",
                            Description = "Column number (1-based) of the selected byte." },
                }
            });

            // -- Value (single byte) -------------------------------------------
            if (sel >= 0 && _editor.FileName?.Length > 0 && TryReadByte(_editor, sel, out var b))
            {
                groups.Add(new PropertyGroup
                {
                    Name    = "Value",
                    Entries = new List<PropertyEntry>
                    {
                        new() { Name = "Hex",     Value = $"{b:X2}",           Description = "Byte value in hexadecimal." },
                        new() { Name = "Decimal", Value = b.ToString(),          Description = "Byte value in decimal." },
                        new() { Name = "Binary",  Value = Convert.ToString(b, 2).PadLeft(8, '0'), Description = "Byte value in binary." },
                        new() { Name = "Octal",   Value = Convert.ToString(b, 8), Description = "Byte value in octal." },
                        new() { Name = "ASCII",   Value = b >= 0x20 && b < 0x7F ? ((char)b).ToString() : ".", Description = "ASCII representation." },
                    }
                });

                // -- Interpretation ---------------------------------------------
                if (TryReadBytes(_editor, sel, 8, out var raw))
                {
                    var interp = new List<PropertyEntry>
                    {
                        new() { Name = "Int8",      Value = ((sbyte)raw[0]).ToString(),               Description = "Signed 8-bit integer." },
                        new() { Name = "UInt8",     Value = raw[0].ToString(),                         Description = "Unsigned 8-bit integer." },
                    };
                    if (raw.Length >= 2)
                    {
                        interp.Add(new() { Name = "Int16 LE",  Value = BitConverter.ToInt16 (raw, 0).ToString(), Description = "Signed 16-bit (little-endian)." });
                        interp.Add(new() { Name = "UInt16 LE", Value = BitConverter.ToUInt16(raw, 0).ToString(), Description = "Unsigned 16-bit (little-endian)." });
                    }
                    if (raw.Length >= 4)
                    {
                        interp.Add(new() { Name = "Int32 LE",   Value = BitConverter.ToInt32 (raw, 0).ToString(), Description = "Signed 32-bit (little-endian)." });
                        interp.Add(new() { Name = "UInt32 LE",  Value = BitConverter.ToUInt32(raw, 0).ToString(), Description = "Unsigned 32-bit (little-endian)." });
                        interp.Add(new() { Name = "Float32 LE", Value = BitConverter.ToSingle(raw, 0).ToString("G6"), Description = "32-bit float (little-endian)." });
                    }
                    if (raw.Length >= 8)
                    {
                        interp.Add(new() { Name = "Int64 LE",   Value = BitConverter.ToInt64 (raw, 0).ToString(), Description = "Signed 64-bit (little-endian)." });
                        interp.Add(new() { Name = "Float64 LE", Value = BitConverter.ToDouble(raw, 0).ToString("G9"), Description = "64-bit float (little-endian)." });
                    }

                    groups.Add(new PropertyGroup { Name = "Interpretation", Entries = interp });
                }
            }

            // -- Selection -----------------------------------------------------
            if (len > 1)
            {
                groups.Add(new PropertyGroup
                {
                    Name = "Selection",
                    Entries = new List<PropertyEntry>
                    {
                        new() { Name = "Start",  Value = $"0x{sel:X8}",       Description = "Start offset of selection." },
                        new() { Name = "End",    Value = $"0x{sel + len - 1:X8}", Description = "End offset of selection." },
                        new() { Name = "Length", Value = len.ToString(),        Description = "Number of selected bytes." },
                    }
                });
            }

            // -- Document ------------------------------------------------------
            var editModeValues = new List<object> { EditMode.Insert, EditMode.Overwrite };
            groups.Add(new PropertyGroup
            {
                Name = "Document",
                Entries = new List<PropertyEntry>
                {
                    new()
                    {
                        Name     = "Edit mode",
                        Value    = _editor.EditMode.ToString(),
                        Type     = PropertyEntryType.Enum,
                        IsReadOnly  = false,
                        AllowedValues = editModeValues,
                        OnValueChanged = v => { if (v is EditMode em) _editor.EditMode = em; },
                        Description = "Insert: bytes are inserted at cursor. Overwrite: bytes are replaced.",
                    },
                    new()
                    {
                        Name     = "Bytes / line",
                        Value    = _editor.BytePerLine,
                        Type     = PropertyEntryType.Integer,
                        IsReadOnly  = false,
                        OnValueChanged = v => { if (int.TryParse(v?.ToString(), out var n) && n > 0) _editor.BytePerLine = n; },
                        Description = "Number of bytes displayed per row.",
                    },
                    new()
                    {
                        Name     = "Encoding",
                        Value    = _editor.CustomEncoding?.WebName ?? "UTF-8",
                        Description = "Text encoding used for the ASCII/text area.",
                    },
                    new()
                    {
                        Name  = "File size",
                        Value = _editor.FileName?.Length > 0 && System.IO.File.Exists(_editor.FileName)
                                ? FormatFileSize(new System.IO.FileInfo(_editor.FileName).Length)
                                : "—",
                        Description = "Size of the file on disk.",
                    },
                    new()
                    {
                        Name  = "File path",
                        Value = _editor.FileName ?? "—",
                        Type  = PropertyEntryType.FilePath,
                        Description = "Full path to the file.",
                    },
                }
            });

            return groups;
        }

        // -- Helpers -----------------------------------------------------------

        private static bool TryReadByte(HexEditor editor, long offset, out byte result)
        {
            result = 0;
            try
            {
                if (string.IsNullOrEmpty(editor.FileName) || !System.IO.File.Exists(editor.FileName))
                    return false;
                using var fs = new System.IO.FileStream(editor.FileName, System.IO.FileMode.Open,
                    System.IO.FileAccess.Read, System.IO.FileShare.ReadWrite);
                if (offset >= fs.Length) return false;
                fs.Seek(offset, System.IO.SeekOrigin.Begin);
                var b = fs.ReadByte();
                if (b < 0) return false;
                result = (byte)b;
                return true;
            }
            catch { return false; }
        }

        private static bool TryReadBytes(HexEditor editor, long offset, int count, out byte[] result)
        {
            result = [];
            try
            {
                if (string.IsNullOrEmpty(editor.FileName) || !System.IO.File.Exists(editor.FileName))
                    return false;
                using var fs = new System.IO.FileStream(editor.FileName, System.IO.FileMode.Open,
                    System.IO.FileAccess.Read, System.IO.FileShare.ReadWrite);
                if (offset >= fs.Length) return false;
                var available = (int)Math.Min(count, fs.Length - offset);
                result = new byte[available];
                fs.Seek(offset, System.IO.SeekOrigin.Begin);
                _ = fs.Read(result, 0, available);
                return true;
            }
            catch { return false; }
        }

        private static string FormatFileSize(long bytes)
        {
            if (bytes < 1024)        return $"{bytes} B";
            if (bytes < 1024 * 1024) return $"{bytes / 1024.0:F1} KB";
            return $"{bytes / (1024.0 * 1024):F2} MB";
        }
    }

    /// <summary>
    /// Makes HexEditor implement <see cref="IPropertyProviderSource"/> so the host
    /// can retrieve its property provider without depending on HexEditor internals.
    /// </summary>
    public partial class HexEditor : IPropertyProviderSource
    {
        private HexEditorPropertyProvider? _propertyProvider;

        /// <inheritdoc />
        public IPropertyProvider? GetPropertyProvider()
            => _propertyProvider ??= new HexEditorPropertyProvider(this);
    }
}
