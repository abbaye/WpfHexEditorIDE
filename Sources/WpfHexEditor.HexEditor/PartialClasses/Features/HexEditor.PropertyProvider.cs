// ==========================================================
// Project: WpfHexEditor.HexEditor
// File: HexEditor.PropertyProvider.cs
// Author: Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude (Anthropic)
// Created: 2026-03-06
// Description:
//     Partial class implementing IPropertyProvider for the HexEditor.
//     Exposes editor state (file path, size, offset, encoding, format,
//     binary analysis, selection stats) as structured property entries
//     for display in the PropertiesPanel.
//
// Architecture Notes:
//     Implements IPropertyProvider + IDisposable.
//     Subscribes to HexEditor events (FileOpened/Closed, FormatDetected,
//     SelectionChanged) and DP property changes via DependencyPropertyDescriptor
//     to keep the panel in sync without polling. Performs async CRC32/MD5/SHA-1
//     hashing of selected data with cancellation support.
//
// ==========================================================

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;
using WpfHexEditor.Core.Events;
using WpfHexEditor.Core.FormatDetection;
using WpfHexEditor.Core.Models;
using WpfHexEditor.Editor.Core;

namespace WpfHexEditor.HexEditor
{
    // -- Supporting types --------------------------------------------------------

    /// <summary>Byte order used for multi-byte integer/float interpretation.</summary>
    internal enum PropertyByteOrder { LittleEndian, BigEndian }

    /// <summary>Cached CRC32 / MD5 / SHA-1 hashes of the current selection.</summary>
    internal sealed record SelectionHashes(string Crc32, string Md5, string Sha1);

    // ---------------------------------------------------------------------------

    /// <summary>
    /// IPropertyProvider that surfaces the byte(s) at the current HexEditor selection,
    /// document-level settings, format detection info, file metadata, and binary
    /// analysis in the Properties panel.
    /// </summary>
    internal sealed class HexEditorPropertyProvider : IPropertyProvider, IDisposable
    {
        private readonly HexEditor _editor;

        // Endianness for Interpretation group
        private PropertyByteOrder _endianness = PropertyByteOrder.LittleEndian;

        // Format detection cache (populated by FormatDetected event)
        private FormatDefinition? _lastDetectedFormat;
        private double            _lastDetectionMs;

        // Async hash state
        private CancellationTokenSource? _hashCts;
        private SelectionHashes?         _selectionHashes;

        // Deferred one-shot refresh after selection stabilises
        private readonly DispatcherTimer _idleRefreshTimer;
        private long                     _idleSelectionSnapshot = -1;

        // Stored delegate for DependencyPropertyDescriptor unsubscription
        private readonly EventHandler _notifyChange;

        // DependencyPropertyDescriptor refs (needed to call RemoveValueChanged)
        private readonly DependencyPropertyDescriptor _dpIsModified;
        private readonly DependencyPropertyDescriptor _dpReadOnly;
        private readonly DependencyPropertyDescriptor _dpEditMode;
        private readonly DependencyPropertyDescriptor _dpBytesPerLine;
        // Selection DPs: used instead of SelectionChanged event because the event
        // is never raised during keyboard navigation (the anti-recursion guard in
        // OnSelectionStartPropertyChanged short-circuits before firing it).
        private readonly DependencyPropertyDescriptor _dpSelectionStart;
        private readonly DependencyPropertyDescriptor _dpSelectionStop;

        public HexEditorPropertyProvider(HexEditor editor)
        {
            _editor = editor;
            _notifyChange = (_, _) => PropertiesChanged?.Invoke(this, EventArgs.Empty);

            _editor.FileOpened     += _notifyChange;
            _editor.FileClosed     += OnFileClosed;
            _editor.FormatDetected += OnFormatDetected;

            _dpIsModified   = DependencyPropertyDescriptor.FromProperty(HexEditor.IsModifiedProperty,   typeof(HexEditor));
            _dpReadOnly     = DependencyPropertyDescriptor.FromProperty(HexEditor.ReadOnlyModeProperty, typeof(HexEditor));
            _dpEditMode     = DependencyPropertyDescriptor.FromProperty(HexEditor.EditModeProperty,     typeof(HexEditor));
            _dpBytesPerLine = DependencyPropertyDescriptor.FromProperty(HexEditor.BytePerLineProperty,  typeof(HexEditor));
            // Watch selection DPs directly: the SelectionChanged event is never raised
            // during keyboard navigation because OnSelectionStartPropertyChanged has an
            // anti-recursion guard that returns before firing the event.
            // DependencyPropertyDescriptor.ValueChanged fires independently of that guard.
            _dpSelectionStart = DependencyPropertyDescriptor.FromProperty(HexEditor.SelectionStartProperty, typeof(HexEditor));
            _dpSelectionStop  = DependencyPropertyDescriptor.FromProperty(HexEditor.SelectionStopProperty,  typeof(HexEditor));

            _dpIsModified    .AddValueChanged(_editor, _notifyChange);
            _dpReadOnly      .AddValueChanged(_editor, _notifyChange);
            _dpEditMode      .AddValueChanged(_editor, _notifyChange);
            _dpBytesPerLine  .AddValueChanged(_editor, _notifyChange);
            _dpSelectionStart.AddValueChanged(_editor, OnSelectionChanged);
            _dpSelectionStop .AddValueChanged(_editor, OnSelectionChanged);

            // One-shot timer: fires once after 400 ms of selection inactivity.
            _idleRefreshTimer = new DispatcherTimer(DispatcherPriority.Background, editor.Dispatcher)
            {
                Interval = TimeSpan.FromMilliseconds(400)
            };
            _idleRefreshTimer.Tick += OnIdleRefreshTick;
        }

        // -- IDisposable ----------------------------------------------------------

        public void Dispose()
        {
            _editor.FileOpened     -= _notifyChange;
            _editor.FileClosed     -= OnFileClosed;
            _editor.FormatDetected -= OnFormatDetected;

            _dpIsModified    .RemoveValueChanged(_editor, _notifyChange);
            _dpReadOnly      .RemoveValueChanged(_editor, _notifyChange);
            _dpEditMode      .RemoveValueChanged(_editor, _notifyChange);
            _dpBytesPerLine  .RemoveValueChanged(_editor, _notifyChange);
            _dpSelectionStart.RemoveValueChanged(_editor, OnSelectionChanged);
            _dpSelectionStop .RemoveValueChanged(_editor, OnSelectionChanged);

            _idleRefreshTimer.Stop();
            _idleRefreshTimer.Tick -= OnIdleRefreshTick;

            _hashCts?.Cancel();
            _hashCts?.Dispose();
        }

        // -- IPropertyProvider ----------------------------------------------------

        public string ContextLabel
        {
            get
            {
                var name = _editor.FileName?.Length > 0
                    ? Path.GetFileName(_editor.FileName)
                    : "Untitled";
                var sel = _editor.SelectionStart;
                return sel >= 0
                    ? $"{name} — Byte at 0x{sel:X8}"
                    : name;
            }
        }

        public event EventHandler? PropertiesChanged;

        public IReadOnlyList<PropertyGroup> GetProperties()
        {
            var groups = new List<PropertyGroup?>();
            var sel    = _editor.SelectionStart;
            var len    = _editor.SelectionLength;

            groups.Add(BuildPositionGroup(sel, len));
            groups.Add(BuildValueGroup(sel));
            groups.Add(BuildInterpretationGroup(sel));
            groups.Add(BuildSelectionGroup(sel, len));
            groups.Add(BuildSelectionAnalysisGroup(sel, len));
            groups.Add(BuildDocumentGroup());
            groups.Add(BuildFileMetadataGroup());
            groups.Add(BuildFormatGroup());
            groups.Add(BuildBinaryAnalysisGroup());

            return groups.Where(g => g != null).Cast<PropertyGroup>().ToList();
        }

        // -- Group builders -------------------------------------------------------

        private PropertyGroup BuildPositionGroup(long sel, long len)
        {
            var bytePerLine = _editor.BytePerLine;
            return new PropertyGroup
            {
                Name = "Position",
                Entries = new List<PropertyEntry>
                {
                    new() { Name = "Offset (hex)", Value = sel >= 0 ? $"0x{sel:X8}" : "—",
                            Type = PropertyEntryType.Hex,     Description = "Hex address of the selected byte." },
                    new() { Name = "Offset (dec)", Value = sel >= 0 ? sel.ToString() : "—",
                            Type = PropertyEntryType.Integer, Description = "Decimal address of the selected byte." },
                    new() { Name = "Line",   Value = sel >= 0 ? (sel / bytePerLine + 1).ToString() : "—",
                            Description = "Line number (1-based) of the selected byte." },
                    new() { Name = "Column", Value = sel >= 0 ? (sel % bytePerLine + 1).ToString() : "—",
                            Description = "Column number (1-based) of the selected byte." },
                }
            };
        }

        private PropertyGroup? BuildValueGroup(long sel)
        {
            if (sel < 0 || !TryReadByte(_editor, sel, out var b)) return null;

            return new PropertyGroup
            {
                Name = "Value",
                Entries = new List<PropertyEntry>
                {
                    new() { Name = "Hex",     Value = $"{b:X2}",                                   Description = "Byte value in hexadecimal." },
                    new() { Name = "Decimal", Value = b.ToString(),                                 Description = "Byte value in decimal." },
                    new() { Name = "Binary",  Value = Convert.ToString(b, 2).PadLeft(8, '0'),      Description = "Byte value in binary." },
                    new() { Name = "Octal",   Value = Convert.ToString(b, 8),                      Description = "Byte value in octal." },
                    new() { Name = "ASCII",   Value = b >= 0x20 && b < 0x7F ? ((char)b).ToString() : ".", Description = "ASCII representation." },
                }
            };
        }

        private PropertyGroup? BuildInterpretationGroup(long sel)
        {
            if (sel < 0 || !TryReadBytes(_editor, sel, 8, out var raw)) return null;

            var byteOrderValues = new List<object> { PropertyByteOrder.LittleEndian, PropertyByteOrder.BigEndian };
            var entries = new List<PropertyEntry>
            {
                new()
                {
                    Name          = "Endianness",
                    Value         = _endianness,
                    Type          = PropertyEntryType.Enum,
                    IsReadOnly    = false,
                    AllowedValues = byteOrderValues,
                    OnValueChanged = v => { if (v is PropertyByteOrder bo) { _endianness = bo; PropertiesChanged?.Invoke(this, EventArgs.Empty); } },
                    Description   = "Byte order for multi-byte integer and float interpretation.",
                },
                new() { Name = "Int8",  Value = ((sbyte)raw[0]).ToString(), Description = "Signed 8-bit integer." },
                new() { Name = "UInt8", Value = raw[0].ToString(),          Description = "Unsigned 8-bit integer." },
            };

            AppendMultiByteEntries(entries, raw, _endianness);
            return new PropertyGroup { Name = "Interpretation", Entries = entries };
        }

        private static void AppendMultiByteEntries(List<PropertyEntry> entries, byte[] raw, PropertyByteOrder order)
        {
            if (raw.Length >= 2)
            {
                var b2 = Slice(raw, 0, 2, order);
                entries.Add(new() { Name = "Int16",  Value = BitConverter.ToInt16 (b2, 0).ToString(), Description = $"Signed 16-bit ({order})." });
                entries.Add(new() { Name = "UInt16", Value = BitConverter.ToUInt16(b2, 0).ToString(), Description = $"Unsigned 16-bit ({order})." });
            }
            if (raw.Length >= 4)
            {
                var b4 = Slice(raw, 0, 4, order);
                entries.Add(new() { Name = "Int32",   Value = BitConverter.ToInt32 (b4, 0).ToString(),    Description = $"Signed 32-bit ({order})." });
                entries.Add(new() { Name = "UInt32",  Value = BitConverter.ToUInt32(b4, 0).ToString(),    Description = $"Unsigned 32-bit ({order})." });
                entries.Add(new() { Name = "Float32", Value = BitConverter.ToSingle(b4, 0).ToString("G6"), Description = $"32-bit float ({order})." });
            }
            if (raw.Length >= 8)
            {
                var b8 = Slice(raw, 0, 8, order);
                entries.Add(new() { Name = "Int64",   Value = BitConverter.ToInt64 (b8, 0).ToString(),    Description = $"Signed 64-bit ({order})." });
                entries.Add(new() { Name = "Float64", Value = BitConverter.ToDouble(b8, 0).ToString("G9"), Description = $"64-bit float ({order})." });
            }
        }

        private PropertyGroup? BuildSelectionGroup(long sel, long len)
        {
            if (len <= 1) return null;
            return new PropertyGroup
            {
                Name = "Selection",
                Entries = new List<PropertyEntry>
                {
                    new() { Name = "Start",  Value = $"0x{sel:X8}",           Description = "Start offset of selection." },
                    new() { Name = "End",    Value = $"0x{sel + len - 1:X8}", Description = "End offset of selection." },
                    new() { Name = "Length", Value = len.ToString(),            Description = "Number of selected bytes." },
                }
            };
        }

        private PropertyGroup? BuildSelectionAnalysisGroup(long sel, long len)
        {
            if (len <= 1 || len > 65536 || !TryReadBytes(_editor, sel, (int)len, out var data))
                return null;

            var (entropy, nullPct, asciiPct, uniqueCount, _) = AnalyzeSample(data);
            var hashes = _selectionHashes;

            var entries = new List<PropertyEntry>
            {
                new() { Name = "Entropy",    Value = $"{entropy:F2} / 8.0",   Description = "Shannon entropy of the selected bytes (0=ordered, 8=random)." },
                new() { Name = "Null bytes", Value = $"{nullPct:F1}%",         Description = "Null byte percentage in selection." },
                new() { Name = "Printable",  Value = $"{asciiPct:F1}%",        Description = "Printable ASCII (0x20–0x7E) percentage in selection." },
                new() { Name = "Unique",     Value = $"{uniqueCount} / 256",   Description = "Distinct byte values in selection." },
                new() { Name = "CRC32",      Value = hashes?.Crc32 ?? "Computing…", Description = "CRC-32 checksum of selected bytes." },
                new() { Name = "MD5",        Value = hashes?.Md5   ?? "Computing…", Description = "MD5 hash of selected bytes." },
                new() { Name = "SHA-1",      Value = hashes?.Sha1  ?? "Computing…", Description = "SHA-1 hash of selected bytes." },
            };

            return new PropertyGroup { Name = "Selection Analysis", Entries = entries };
        }

        private PropertyGroup BuildDocumentGroup()
        {
            var editModeValues = new List<object> { EditMode.Insert, EditMode.Overwrite };
            return new PropertyGroup
            {
                Name = "Document",
                Entries = new List<PropertyEntry>
                {
                    new()
                    {
                        Name          = "Edit mode",
                        Value         = _editor.EditMode,   // keep as enum so SelectedItem matches AllowedValues
                        Type          = PropertyEntryType.Enum,
                        IsReadOnly    = false,
                        AllowedValues = editModeValues,
                        OnValueChanged = v => { if (v is EditMode em) _editor.EditMode = em; },
                        Description   = "Insert: bytes are inserted at cursor. Overwrite: bytes are replaced.",
                    },
                    new()
                    {
                        Name          = "Bytes / line",
                        Value         = _editor.BytePerLine,
                        Type          = PropertyEntryType.Integer,
                        IsReadOnly    = false,
                        OnValueChanged = v => { if (int.TryParse(v?.ToString(), out var n) && n > 0) _editor.BytePerLine = n; },
                        Description   = "Number of bytes displayed per row.",
                    },
                    new()
                    {
                        Name       = "Read-only",
                        Value      = _editor.ReadOnlyMode,
                        Type       = PropertyEntryType.Boolean,
                        IsReadOnly = false,
                        OnValueChanged = v => { if (v is bool ro) _editor.ReadOnlyMode = ro; },
                        Description = "When enabled, all editing operations are blocked.",
                    },
                    new()
                    {
                        Name       = "Modified",
                        Value      = _editor.IsModified,
                        Type       = PropertyEntryType.Boolean,
                        IsReadOnly = true,
                        Description = "True if the document has unsaved changes.",
                    },
                    new()
                    {
                        Name  = "Encoding",
                        Value = _editor.CustomEncoding?.WebName ?? "UTF-8",
                        Description = "Text encoding used for the ASCII/text area.",
                    },
                    new()
                    {
                        Name  = "File size",
                        Value = GetFileSizeDisplay(),
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
            };
        }

        private PropertyGroup? BuildFileMetadataGroup()
        {
            var path = _editor.FileName;
            if (string.IsNullOrEmpty(path) || !File.Exists(path)) return null;

            var fi = new FileInfo(path);
            return new PropertyGroup
            {
                Name = "File Metadata",
                Entries = new List<PropertyEntry>
                {
                    new() { Name = "Created",    Value = fi.CreationTime.ToString("yyyy-MM-dd HH:mm:ss"),   Description = "File creation timestamp." },
                    new() { Name = "Modified",   Value = fi.LastWriteTime.ToString("yyyy-MM-dd HH:mm:ss"),  Description = "Last write timestamp." },
                    new() { Name = "Accessed",   Value = fi.LastAccessTime.ToString("yyyy-MM-dd HH:mm:ss"), Description = "Last access timestamp." },
                    new() { Name = "Attributes", Value = fi.Attributes.ToString(),                          Description = "File system attributes (ReadOnly, Hidden, System, etc.)." },
                }
            };
        }

        private PropertyGroup? BuildFormatGroup()
        {
            if (_lastDetectedFormat == null) return null;
            var f = _lastDetectedFormat;
            return new PropertyGroup
            {
                Name = "Format",
                Entries = new List<PropertyEntry>
                {
                    new() { Name = "Name",       Value = f.FormatName ?? "—",                               Description = "Detected file format name." },
                    new() { Name = "Category",   Value = f.Category   ?? "—",                               Description = "Format category (Archives, Images, etc.)." },
                    new() { Name = "MIME",        Value = f.MimeTypes?.FirstOrDefault() ?? "—",             Description = "Primary MIME type of the detected format." },
                    new() { Name = "Extensions", Value = f.Extensions?.Any() == true ? string.Join(", ", f.Extensions) : "—",
                            Description = "File extensions associated with this format." },
                    new() { Name = "Detection",  Value = $"{_lastDetectionMs:F1} ms",                       Description = "Time taken for format detection." },
                }
            };
        }

        private PropertyGroup? BuildBinaryAnalysisGroup()
        {
            var path = _editor.FileName;
            if (string.IsNullOrEmpty(path) || !File.Exists(path)) return null;
            if (!TryReadSample(_editor, 65536, out var sample)) return null;

            var (entropy, nullPct, asciiPct, uniqueCount, dataType) = AnalyzeSample(sample);
            return new PropertyGroup
            {
                Name = "Binary Analysis",
                Entries = new List<PropertyEntry>
                {
                    new() { Name = "Entropy",      Value = $"{entropy:F2} / 8.0", Description = "Shannon entropy of the file sample (0=structured, 8=random)." },
                    new() { Name = "Null bytes",   Value = $"{nullPct:F1}%",       Description = "Percentage of zero bytes in the sample." },
                    new() { Name = "Printable",    Value = $"{asciiPct:F1}%",      Description = "Printable ASCII (0x20–0x7E) percentage in sample." },
                    new() { Name = "Unique bytes", Value = $"{uniqueCount} / 256", Description = "Number of distinct byte values in the sample." },
                    new() { Name = "Data type",    Value = dataType,               Description = "Content classification inferred from byte distribution." },
                }
            };
        }

        // -- Async hash -----------------------------------------------------------

        private async void BeginSelectionHashAsync(long offset, long length)
        {
            _hashCts?.Cancel();
            _hashCts?.Dispose();
            _hashCts = new CancellationTokenSource();
            var token = _hashCts.Token;
            var path  = _editor.FileName;
            if (string.IsNullOrEmpty(path)) return;

            try
            {
                var hashes = await Task.Run(() => ComputeSelectionHashes(path, offset, length, token), token);
                if (!token.IsCancellationRequested)
                {
                    _selectionHashes = hashes;
                    PropertiesChanged?.Invoke(this, EventArgs.Empty);
                }
            }
            catch (OperationCanceledException) { /* selection changed before completion */ }
        }

        private static SelectionHashes ComputeSelectionHashes(string path, long offset, long length, CancellationToken ct)
        {
            var cap  = (int)Math.Min(length, 16L * 1024 * 1024);
            var data = new byte[cap];
            using var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            fs.Seek(offset, SeekOrigin.Begin);
            _ = fs.Read(data, 0, cap);
            ct.ThrowIfCancellationRequested();

            return new SelectionHashes(
                Crc32: ComputeCrc32String(data),
                Md5:   Convert.ToHexString(MD5.HashData(data)).ToLowerInvariant(),
                Sha1:  Convert.ToHexString(SHA1.HashData(data)).ToLowerInvariant()
            );
        }

        private static readonly uint[] CrcTable = BuildCrc32Table();

        private static uint[] BuildCrc32Table()
        {
            var table = new uint[256];
            for (uint i = 0; i < 256; i++)
            {
                var crc = i;
                for (var j = 0; j < 8; j++)
                    crc = (crc & 1) != 0 ? (crc >> 1) ^ 0xEDB88320u : crc >> 1;
                table[i] = crc;
            }
            return table;
        }

        private static string ComputeCrc32String(byte[] data)
        {
            var crc = 0xFFFFFFFFu;
            foreach (var b in data)
                crc = (crc >> 8) ^ CrcTable[(crc ^ b) & 0xFF];
            return $"{crc ^ 0xFFFFFFFFu:X8}";
        }

        // -- Event handlers -------------------------------------------------------

        private void OnSelectionChanged(object? sender, EventArgs e)
        {
            _selectionHashes = null;

            // Do NOT fire PropertiesChanged here: GetProperties() opens FileStreams,
            // reads bytes and computes entropy synchronously on the UI thread.
            // Calling it on every keypress blocks WPF's message loop, causing sluggish
            // navigation and scroll-jump artefacts from batched input events.
            // Instead, restart the idle timer: the panel refreshes exactly once after
            // the user stops navigating (400 ms of inactivity).
            _idleSelectionSnapshot = _editor.SelectionStart;
            _idleRefreshTimer.Stop();
            _idleRefreshTimer.Start();
        }

        private void OnIdleRefreshTick(object? sender, EventArgs e)
        {
            // One-shot: stop immediately so the timer does not repeat.
            _idleRefreshTimer.Stop();

            if (_editor.SelectionStart != _idleSelectionSnapshot)
                return; // Position moved again before the timer fired — skip.

            // Position has been stable for 400 ms: start async hash if needed,
            // then refresh the panel once.
            var sel = _editor.SelectionStart;
            var len = _editor.SelectionLength;
            if (len > 1 && len <= 16L * 1024 * 1024 && !string.IsNullOrEmpty(_editor.FileName))
                BeginSelectionHashAsync(sel, len);

            PropertiesChanged?.Invoke(this, EventArgs.Empty);
        }

        private void OnFileClosed(object? sender, EventArgs e)
        {
            _lastDetectedFormat = null;
            _lastDetectionMs    = 0;
            _selectionHashes    = null;
            _hashCts?.Cancel();
            PropertiesChanged?.Invoke(this, EventArgs.Empty);
        }

        private void OnFormatDetected(object? sender, FormatDetectedEventArgs e)
        {
            if (e.Success)
            {
                _lastDetectedFormat = e.Format;
                _lastDetectionMs    = e.DetectionTimeMs;
            }
            PropertiesChanged?.Invoke(this, EventArgs.Empty);
        }

        // -- Static helpers -------------------------------------------------------

        private static bool TryReadByte(HexEditor editor, long offset, out byte result)
        {
            result = 0;
            try
            {
                if (string.IsNullOrEmpty(editor.FileName) || !File.Exists(editor.FileName)) return false;
                using var fs = new FileStream(editor.FileName, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
                if (offset >= fs.Length) return false;
                fs.Seek(offset, SeekOrigin.Begin);
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
                if (string.IsNullOrEmpty(editor.FileName) || !File.Exists(editor.FileName)) return false;
                using var fs = new FileStream(editor.FileName, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
                if (offset >= fs.Length) return false;
                var available = (int)Math.Min(count, fs.Length - offset);
                result = new byte[available];
                fs.Seek(offset, SeekOrigin.Begin);
                _ = fs.Read(result, 0, available);
                return true;
            }
            catch { return false; }
        }

        private static bool TryReadSample(HexEditor editor, int maxBytes, out byte[] sample)
        {
            sample = [];
            try
            {
                if (string.IsNullOrEmpty(editor.FileName) || !File.Exists(editor.FileName)) return false;
                using var fs = new FileStream(editor.FileName, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
                var size = (int)Math.Min(maxBytes, fs.Length);
                sample = new byte[size];
                _ = fs.Read(sample, 0, size);
                return size > 0;
            }
            catch { return false; }
        }

        private static (double entropy, double nullPct, double asciiPct, int uniqueCount, string dataType)
            AnalyzeSample(byte[] data)
        {
            if (data.Length == 0) return (0, 0, 0, 0, "Empty");

            var histogram = new int[256];
            foreach (var b in data)
                histogram[b]++;

            double entropy    = 0;
            int    nullCount  = histogram[0];
            int    asciiCount = 0;
            int    uniqueCount = 0;
            double total      = data.Length;

            for (var i = 0; i < 256; i++)
            {
                if (histogram[i] == 0) continue;
                uniqueCount++;
                var p = histogram[i] / total;
                entropy -= p * Math.Log(p, 2);
                if (i >= 0x20 && i < 0x7F) asciiCount += histogram[i];
            }

            var dataType = entropy > 7.5 ? "Encrypted / Compressed"
                         : entropy > 5.0 ? "Binary"
                         : entropy < 2.0 ? "Text / Structured"
                         : "Mixed Binary";

            return (entropy, nullCount * 100.0 / total, asciiCount * 100.0 / total, uniqueCount, dataType);
        }

        private static byte[] Slice(byte[] src, int offset, int count, PropertyByteOrder order)
        {
            var slice = src.Skip(offset).Take(count).ToArray();
            if (order == PropertyByteOrder.BigEndian)
                Array.Reverse(slice);
            return slice;
        }

        private string GetFileSizeDisplay()
        {
            var path = _editor.FileName;
            if (string.IsNullOrEmpty(path) || !File.Exists(path)) return "—";
            var len = new FileInfo(path).Length;
            if (len < 1024)        return $"{len} B";
            if (len < 1024 * 1024) return $"{len / 1024.0:F1} KB";
            return $"{len / (1024.0 * 1024):F2} MB";
        }
    }

    // ---------------------------------------------------------------------------

    /// <summary>
    /// Makes HexEditor implement <see cref="IPropertyProviderSource"/> so the host
    /// can retrieve its property provider without depending on HexEditor internals.
    /// </summary>
    public partial class HexEditor : IPropertyProviderSource
    {
        private HexEditorPropertyProvider? _propertyProvider;

        /// <inheritdoc />
        public IPropertyProvider? GetPropertyProvider()
        {
            if (_propertyProvider != null) return _propertyProvider;
            _propertyProvider = new HexEditorPropertyProvider(this);
            Unloaded += OnProviderHostUnloaded;
            return _propertyProvider;
        }

        private void OnProviderHostUnloaded(object sender, RoutedEventArgs e)
        {
            Unloaded -= OnProviderHostUnloaded;
            (_propertyProvider as IDisposable)?.Dispose();
            _propertyProvider = null;
        }
    }
}
