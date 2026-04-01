// ==========================================================
// Project: WpfHexEditor.Core
// File: SynalysisGrammar/SynalysisGrammarInterpreter.cs
// Author: Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.6
// Created: 2026-03-16
// Description:
//     Executes a parsed UFWB grammar against a binary buffer, walking the
//     element tree to produce SynalysisParseResult (fields + colour regions).
//
// Architecture Notes:
//     Patterns: Interpreter, Visitor (implicit via element dispatch)
//     Key design decisions:
//     - SymbolTable built as a pre-pass over all structures; resolves "id:X" refs.
//     - Inheritance (extends) flattened per-structure before execution.
//     - InterpreterFrame stack tracks BaseOffset, CurrentOffset, ParentLength,
//       and per-field named values for "prev.FieldName" resolution.
//     - order="variable" handled via greedy try-parse with rollback.
//     - repeatmax="unlimited" bounded by MaxIterations (default 10 000).
//     - Script elements (<script>) are skipped with a warning (Phase-1 deferral).
//     - Depth capped at MaxDepth (50) to prevent infinite recursion.
// ==========================================================

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace WpfHexEditor.Core.SynalysisGrammar;

/// <summary>
/// Executes a <see cref="UfwbRoot"/> grammar against a binary buffer and
/// returns a <see cref="SynalysisParseResult"/>.
/// </summary>
public sealed class SynalysisGrammarInterpreter
{
    // -- Configuration constants -------------------------------------------

    private const int MaxDepth      = 50;
    private const int MaxIterations = 10_000;
    private const int MaxBacktrack  = 64;    // attempts per variable-order pass

    // -- State (reset per Execute call) ------------------------------------

    private byte[]                               _data         = [];
    private long                                 _fileOffset;   // base offset of _data in the full file
    private Dictionary<string, UfwbStructure>    _symbols      = [];
    private readonly List<SynalysisField>         _fields       = [];
    private readonly List<SynalysisColorRegion>   _colorRegions = [];
    private readonly List<string>                 _warnings     = [];
    private bool                                  _hasErrors;
    private int                                   _depth;

    // -- Public API --------------------------------------------------------

    /// <summary>
    /// Executes <paramref name="grammar"/> against <paramref name="data"/> starting
    /// at <paramref name="fileOffset"/> and returns the parse result.
    /// </summary>
    /// <param name="grammar">Parsed UFWB grammar (from <see cref="SynalysisGrammarParser"/>).</param>
    /// <param name="data">Binary buffer. May be a window into a larger file.</param>
    /// <param name="fileOffset">
    /// Absolute file position of <paramref name="data"/>[0].
    /// All output offsets are relative to the file, not the buffer.
    /// </param>
    public SynalysisParseResult Execute(UfwbRoot grammar, byte[] data, long fileOffset = 0)
    {
        ArgumentNullException.ThrowIfNull(grammar);
        ArgumentNullException.ThrowIfNull(data);

        // Reset mutable state for this execution.
        _data         = data;
        _fileOffset   = fileOffset;
        _fields.Clear();
        _colorRegions.Clear();
        _warnings.Clear();
        _hasErrors    = false;
        _depth        = 0;

        // 1. Build symbol table.
        _symbols = BuildSymbolTable(grammar.Grammar);

        // 2. Resolve inheritance chains (mutates copies, leaves originals intact).
        var resolved = BuildResolvedStructures(_symbols);

        // 3. Find root structure.
        var startId = StripIdPrefix(grammar.Grammar.Start);
        if (!resolved.TryGetValue(startId, out var rootStructure))
        {
            _warnings.Add($"[SynalysisInterpreter] Root structure id '{grammar.Grammar.Start}' not found in symbol table.");
            return BuildResult(grammar.Grammar.Name);
        }

        // 4. Execute from root.
        var rootFrame = new InterpreterFrame(0, null, 0);
        ExecuteStructure(rootStructure, resolved, rootFrame);

        return BuildResult(grammar.Grammar.Name);
    }

    // -- Symbol table ------------------------------------------------------

    private static Dictionary<string, UfwbStructure> BuildSymbolTable(UfwbGrammar grammar)
    {
        var table = new Dictionary<string, UfwbStructure>(StringComparer.Ordinal);

        foreach (var s in grammar.Structures)
            RegisterStructure(s, table);

        return table;
    }

    private static void RegisterStructure(UfwbStructure s, Dictionary<string, UfwbStructure> table)
    {
        if (!string.IsNullOrEmpty(s.Id))
            table[s.Id] = s;

        // Recursively register inline nested structures.
        foreach (var child in s.Elements.OfType<UfwbStructure>())
            RegisterStructure(child, table);
    }

    // -- Inheritance resolution --------------------------------------------

    /// <summary>
    /// Returns a new dictionary where each structure has its parent's elements
    /// prepended (shallow clone). Detects inheritance cycles.
    /// </summary>
    private static Dictionary<string, UfwbStructure> BuildResolvedStructures(
        Dictionary<string, UfwbStructure> symbols)
    {
        var resolved = new Dictionary<string, UfwbStructure>(StringComparer.Ordinal);

        foreach (var id in symbols.Keys)
            Resolve(id, symbols, resolved, []);

        return resolved;
    }

    private static UfwbStructure Resolve(
        string id,
        Dictionary<string, UfwbStructure> symbols,
        Dictionary<string, UfwbStructure> resolved,
        HashSet<string> visiting)
    {
        if (resolved.TryGetValue(id, out var cached)) return cached;

        if (!symbols.TryGetValue(id, out var original))
            return new UfwbStructure { Id = id, Name = "??" };

        if (!visiting.Add(id))
        {
            // Cycle detected — return as-is without extends to break the loop.
            var stub = CloneStructure(original);
            stub.Extends = string.Empty;
            resolved[id] = stub;
            return stub;
        }

        var clone = CloneStructure(original);

        var parentId = StripIdPrefix(original.Extends);
        if (!string.IsNullOrEmpty(parentId) && symbols.ContainsKey(parentId))
        {
            var parent = Resolve(parentId, symbols, resolved, visiting);

            // Inherit parent defaults (only when clone doesn't override).
            if (string.IsNullOrEmpty(clone.Endian))   clone.Endian   = parent.Endian;
            if (string.IsNullOrEmpty(clone.Encoding)) clone.Encoding = parent.Encoding;
            if (string.IsNullOrEmpty(clone.Signed))   clone.Signed   = parent.Signed;

            // Prepend parent elements before own (unless parent is a "Defaults" struct with no elements).
            if (parent.Elements.Count > 0)
            {
                var merged = new List<UfwbElement>(parent.Elements.Count + clone.Elements.Count);
                merged.AddRange(parent.Elements);
                merged.AddRange(clone.Elements);
                clone.Elements = merged;
            }
        }

        visiting.Remove(id);
        resolved[id] = clone;
        return clone;
    }

    private static UfwbStructure CloneStructure(UfwbStructure s) => new()
    {
        Id            = s.Id,
        Name          = s.Name,
        Extends       = s.Extends,
        Encoding      = s.Encoding,
        Endian        = s.Endian,
        Signed        = s.Signed,
        Length        = s.Length,
        FillColor     = s.FillColor,
        VariableOrder = s.VariableOrder,
        Floating      = s.Floating,
        Description   = s.Description,
        RepeatMin     = s.RepeatMin,
        RepeatMax     = s.RepeatMax,
        Elements      = new List<UfwbElement>(s.Elements),   // shallow copy of elements list
    };

    // -- Structure execution -----------------------------------------------

    private void ExecuteStructure(
        UfwbStructure structure,
        Dictionary<string, UfwbStructure> resolved,
        InterpreterFrame parentFrame,
        int indentLevel = 0,
        string groupName = "")
    {
        if (++_depth > MaxDepth)
        {
            _warnings.Add("[SynalysisInterpreter] Maximum recursion depth exceeded.");
            --_depth;
            return;
        }

        // Resolve this structure's byte length to constrain children.
        long? structLength = ResolveLength(structure.Length, parentFrame);

        var frame = new InterpreterFrame(
            parentFrame.CurrentOffset,
            structLength,
            indentLevel);

        if (structure.VariableOrder)
            ExecuteVariableOrderStructure(structure, resolved, frame, indentLevel, groupName);
        else
            ExecuteFixedOrderStructure(structure, resolved, frame, indentLevel, groupName);

        // Advance parent frame past this structure.
        parentFrame.CurrentOffset = frame.CurrentOffset;

        // If a fixed length was declared advance to its end regardless of actual bytes consumed.
        if (structLength.HasValue)
        {
            var expectedEnd = frame.BaseOffset + structLength.Value;
            if (parentFrame.CurrentOffset < expectedEnd)
                parentFrame.CurrentOffset = expectedEnd;
        }

        --_depth;
    }

    private void ExecuteFixedOrderStructure(
        UfwbStructure structure,
        Dictionary<string, UfwbStructure> resolved,
        InterpreterFrame frame,
        int indentLevel,
        string groupName)
    {
        foreach (var element in structure.Elements)
            ExecuteElement(element, resolved, frame, indentLevel, groupName);
    }

    /// <summary>
    /// Handles <c>order="variable"</c>: tries each structref in any order,
    /// repeats until no more matches or the parent length is exhausted.
    /// </summary>
    private void ExecuteVariableOrderStructure(
        UfwbStructure structure,
        Dictionary<string, UfwbStructure> resolved,
        InterpreterFrame frame,
        int indentLevel,
        string groupName)
    {
        int iterations = 0;
        int backtrackBudget = MaxBacktrack;

        while (iterations < MaxIterations && backtrackBudget > 0)
        {
            if (IsFrameExhausted(frame)) break;

            bool matched = false;
            foreach (var element in structure.Elements)
            {
                if (element is not UfwbStructRef sref) continue;
                if (!TryExecuteStructRef(sref, resolved, frame, indentLevel, groupName))
                    continue;
                matched  = true;
                ++iterations;
                break;
            }

            if (!matched) --backtrackBudget;
        }
    }

    // -- Element dispatch --------------------------------------------------

    private void ExecuteElement(
        UfwbElement element,
        Dictionary<string, UfwbStructure> resolved,
        InterpreterFrame frame,
        int indentLevel,
        string groupName)
    {
        if (IsFrameExhausted(frame)) return;

        switch (element)
        {
            case UfwbNumber num:
                ExecuteNumber(num, frame, indentLevel, groupName);
                break;

            case UfwbBinary bin:
                ExecuteBinary(bin, frame, indentLevel, groupName);
                break;

            case UfwbString str:
                ExecuteString(str, frame, indentLevel, groupName);
                break;

            case UfwbStructRef sref:
                for (int i = 0, max = RepeatMax(sref); i < max || max == -1; i++)
                {
                    if (i >= sref.RepeatMin && (IsFrameExhausted(frame) || i >= MaxIterations))
                        break;
                    if (!TryExecuteStructRef(sref, resolved, frame, indentLevel, groupName))
                        break;
                }
                break;

            case UfwbStructure inline:
                for (int i = 0, max = RepeatMax(inline); i < max || max == -1; i++)
                {
                    if (i >= inline.RepeatMin && (IsFrameExhausted(frame) || i >= MaxIterations))
                        break;
                    var childGroup = string.IsNullOrEmpty(inline.Name) ? groupName : inline.Name;
                    ExecuteStructure(inline, resolved, frame, indentLevel + 1, childGroup);
                }
                break;
        }
    }

    // -- Number ------------------------------------------------------------

    private void ExecuteNumber(
        UfwbNumber num,
        InterpreterFrame frame,
        int indentLevel,
        string groupName)
    {
        var byteLen = ResolveLengthOrDefault(num.Length, frame, 4);
        if (byteLen <= 0 || !CanRead(frame.CurrentOffset, byteLen)) return;

        var bufOffset = frame.CurrentOffset;
        var rawBytes  = ReadBytes(bufOffset, byteLen);

        // Default endianness is big-endian for UFWB (most formats are big-endian).
        var isBig = true;  // resolved further down if structure context provides endian

        long rawValue = ReadInt(rawBytes, isBig);

        var valueDisplay = FormatNumberValue(rawValue, byteLen, num.Display, num.Signed);

        // MustMatch validation.
        var isValid = true;
        if (num.MustMatch && num.FixedValues?.Values.Count > 0)
        {
            isValid = num.FixedValues.Values.Any(fv => ParseFixedValue(fv.Value) == rawValue);
            if (!isValid) _hasErrors = true;
        }

        // Named value lookup (replace raw value with enum name when available).
        if (num.FixedValues?.Values.Count > 0)
        {
            var match = num.FixedValues.Values.FirstOrDefault(fv => ParseFixedValue(fv.Value) == rawValue);
            if (match is not null && !string.IsNullOrEmpty(match.Name))
                valueDisplay = $"{match.Name} ({valueDisplay})";
        }

        var fileOff = _fileOffset + bufOffset;
        EmitField(num, fileOff, byteLen, valueDisplay, num.FillColor, indentLevel, groupName, isValid);
        frame.FieldValues[num.Name] = rawValue;
        frame.CurrentOffset += byteLen;
    }

    // -- Binary ------------------------------------------------------------

    private void ExecuteBinary(
        UfwbBinary bin,
        InterpreterFrame frame,
        int indentLevel,
        string groupName)
    {
        var byteLen = ResolveBinaryLength(bin.Length, frame);
        if (byteLen <= 0 || !CanRead(frame.CurrentOffset, byteLen)) return;

        var bufOffset = frame.CurrentOffset;
        var rawBytes  = ReadBytes(bufOffset, byteLen);
        var valueDisplay = BytesToHex(rawBytes, maxBytes: 16);

        // MustMatch validation.
        var isValid = true;
        if (bin.MustMatch && bin.FixedValues?.Values.Count > 0)
        {
            var expectedHex = bin.FixedValues.Values.First().Value
                                 .Replace(" ", "").Replace("0x", "").Replace("0X", "");
            var actualHex   = BitConverter.ToString(rawBytes).Replace("-", "");
            isValid = actualHex.Equals(expectedHex, StringComparison.OrdinalIgnoreCase);
            if (!isValid) _hasErrors = true;
        }

        var fileOff = _fileOffset + bufOffset;
        EmitField(bin, fileOff, byteLen, valueDisplay, bin.FillColor, indentLevel, groupName, isValid);
        frame.CurrentOffset += byteLen;
    }

    // -- String ------------------------------------------------------------

    private void ExecuteString(
        UfwbString str,
        InterpreterFrame frame,
        int indentLevel,
        string groupName)
    {
        var bufOffset = frame.CurrentOffset;
        int byteLen;
        string text;

        if (str.Type == "zero-terminated")
        {
            (text, byteLen) = ReadZeroTerminatedString(bufOffset);
        }
        else
        {
            byteLen = ResolveLengthOrDefault(str.Length, frame, 1);
            if (!CanRead(bufOffset, byteLen)) return;
            text = Encoding.UTF8.GetString(ReadBytes(bufOffset, byteLen)).TrimEnd('\0');
        }

        var fileOff = _fileOffset + bufOffset;
        EmitField(str, fileOff, byteLen, text, fillColor: string.Empty, indentLevel, groupName, isValid: true);
        frame.FieldValues[str.Name] = byteLen;
        frame.CurrentOffset += byteLen;
    }

    // -- StructRef ---------------------------------------------------------

    /// <summary>
    /// Attempts to execute a structref. Returns false when the referenced
    /// structure's mustmatch constraints fail (allows variable-order rollback).
    /// </summary>
    private bool TryExecuteStructRef(
        UfwbStructRef sref,
        Dictionary<string, UfwbStructure> resolved,
        InterpreterFrame frame,
        int indentLevel,
        string groupName)
    {
        var targetId = StripIdPrefix(sref.StructureRef);
        if (!resolved.TryGetValue(targetId, out var target)) return false;

        // Save position for rollback if mustmatch fails.
        var savedOffset     = frame.CurrentOffset;
        var savedFieldCount = _fields.Count;
        var savedRegCount   = _colorRegions.Count;

        var childGroup = string.IsNullOrEmpty(sref.Name) ? groupName : sref.Name;
        ExecuteStructure(target, resolved, frame, indentLevel, childGroup);

        // If mustmatch validation failed, roll back.
        if (_hasErrors && _fields.Count > savedFieldCount)
        {
            // Revert fields / regions added by this failed attempt.
            // Guard against nested rollbacks that may have already removed items past the saved index.
            _fields.RemoveRange(savedFieldCount, _fields.Count - savedFieldCount);
            if (_colorRegions.Count > savedRegCount)
                _colorRegions.RemoveRange(savedRegCount, _colorRegions.Count - savedRegCount);
            frame.CurrentOffset = savedOffset;
            _hasErrors = false;  // reset so the next structref gets a clean try
            return false;
        }

        return true;
    }

    // -- Output helpers ----------------------------------------------------

    private void EmitField(
        UfwbElement element,
        long fileOffset,
        int byteLen,
        string valueDisplay,
        string fillColor,
        int indentLevel,
        string groupName,
        bool isValid)
    {
        var kind = element switch
        {
            UfwbNumber    => SynalysisFieldKind.Number,
            UfwbBinary    => SynalysisFieldKind.Binary,
            UfwbString    => SynalysisFieldKind.String,
            UfwbStructure => SynalysisFieldKind.Structure,
            _             => SynalysisFieldKind.Number,
        };

        _fields.Add(new SynalysisField
        {
            Name         = element.Name,
            Offset       = fileOffset,
            Length       = byteLen,
            ValueDisplay = valueDisplay,
            Color        = fillColor,
            IndentLevel  = indentLevel,
            GroupName    = groupName,
            Description  = element.Description,
            Kind         = kind,
            IsValid      = isValid,
        });

        if (!string.IsNullOrEmpty(fillColor) && byteLen > 0)
        {
            _colorRegions.Add(new SynalysisColorRegion
            {
                Offset      = fileOffset,
                Length      = byteLen,
                Color       = fillColor,
                Description = element.Name,
                Opacity     = 0.3,
            });
        }
    }

    private SynalysisParseResult BuildResult(string grammarName) => new()
    {
        GrammarName       = grammarName,
        Fields            = [.. _fields],
        ColorRegions      = [.. _colorRegions],
        Warnings          = [.. _warnings],
        HasValidationErrors = _hasErrors,
    };

    // -- Length resolution -------------------------------------------------

    /// <summary>
    /// Resolves a length string to a long. Returns null for "remaining"
    /// (caller must interpret from frame context).
    /// </summary>
    private long? ResolveLength(string lengthStr, InterpreterFrame frame)
    {
        if (string.IsNullOrEmpty(lengthStr)) return null;
        if (lengthStr == "remaining")        return RemainingBytes(frame);

        if (lengthStr.StartsWith("prev.", StringComparison.OrdinalIgnoreCase))
        {
            var fieldName = lengthStr[5..];
            return frame.FieldValues.TryGetValue(fieldName, out var v) ? v : null;
        }

        return long.TryParse(lengthStr, out var n) ? n : null;
    }

    private int ResolveLengthOrDefault(string lengthStr, InterpreterFrame frame, int defaultLen)
    {
        var resolved = ResolveLength(lengthStr, frame);
        return resolved.HasValue ? (int)Math.Min(resolved.Value, int.MaxValue) : defaultLen;
    }

    private int ResolveBinaryLength(string lengthStr, InterpreterFrame frame)
    {
        if (lengthStr == "remaining")
            return (int)Math.Max(0, RemainingBytes(frame));

        return ResolveLengthOrDefault(lengthStr, frame, 0);
    }

    private long RemainingBytes(InterpreterFrame frame)
    {
        if (frame.ParentLength.HasValue)
            return Math.Max(0, frame.BaseOffset + frame.ParentLength.Value - frame.CurrentOffset);

        return Math.Max(0, _data.Length - frame.CurrentOffset);
    }

    private bool IsFrameExhausted(InterpreterFrame frame)
    {
        if (frame.CurrentOffset >= _data.Length) return true;
        if (frame.ParentLength.HasValue && frame.CurrentOffset >= frame.BaseOffset + frame.ParentLength.Value)
            return true;
        return false;
    }

    // -- Binary data helpers -----------------------------------------------

    private bool CanRead(long offset, int length)
        => offset >= 0 && offset + length <= _data.Length;

    private byte[] ReadBytes(long offset, int length)
    {
        if (!CanRead(offset, length)) return [];
        var buf = new byte[length];
        Array.Copy(_data, offset, buf, 0, length);
        return buf;
    }

    private static long ReadInt(byte[] bytes, bool bigEndian)
    {
        if (bytes.Length == 0) return 0;

        // Always work with up to 8 bytes.
        Span<byte> padded = stackalloc byte[8];
        var src = bytes.AsSpan(0, Math.Min(bytes.Length, 8));

        if (bigEndian)
            src.CopyTo(padded[(8 - src.Length)..]);
        else
            src.CopyTo(padded);

        if (bigEndian)
            padded.Reverse();

        return BitConverter.ToInt64(padded);
    }

    private (string text, int byteLen) ReadZeroTerminatedString(long offset)
    {
        var end = offset;
        while (end < _data.Length && _data[end] != 0) end++;
        var len  = (int)(end - offset);
        var text = len > 0 ? Encoding.UTF8.GetString(_data, (int)offset, len) : string.Empty;
        return (text, len + 1); // include null terminator in consumed length
    }

    private static string BytesToHex(byte[] data, int maxBytes = 16)
    {
        var take = Math.Min(data.Length, maxBytes);
        var hex  = BitConverter.ToString(data, 0, take).Replace("-", " ");
        return data.Length > maxBytes ? hex + " ..." : hex;
    }

    // -- Formatting --------------------------------------------------------

    private static string FormatNumberValue(long value, int byteLen, string display, string signed)
    {
        var isUnsigned = signed != "yes";
        var unsignedValue = isUnsigned ? (ulong)(value & ((1L << (byteLen * 8)) - 1)) : (ulong)value;

        return display switch
        {
            "hex"     => $"0x{unsignedValue:X}",
            "binary"  => Convert.ToString((long)unsignedValue, 2).PadLeft(byteLen * 8, '0'),
            _         => isUnsigned ? unsignedValue.ToString() : value.ToString(),
        };
    }

    private static long ParseFixedValue(string valueStr)
    {
        if (string.IsNullOrEmpty(valueStr)) return 0;

        var s = valueStr.Trim();
        if (s.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
        {
            if (long.TryParse(s[2..], System.Globalization.NumberStyles.HexNumber, null, out var hex))
                return hex;
        }

        if (long.TryParse(s, out var dec)) return dec;
        return 0;
    }

    // -- Repeat helpers ----------------------------------------------------

    private static int RepeatMax(UfwbStructRef sref) => sref.RepeatMax == -1 ? MaxIterations : sref.RepeatMax;
    private static int RepeatMax(UfwbStructure  s)   => s.RepeatMax   == -1 ? MaxIterations : s.RepeatMax;

    // -- Utility -----------------------------------------------------------

    private static string StripIdPrefix(string id)
    {
        if (string.IsNullOrEmpty(id)) return string.Empty;
        return id.StartsWith("id:", StringComparison.OrdinalIgnoreCase) ? id[3..] : id;
    }

    // -- Frame -------------------------------------------------------------

    private sealed class InterpreterFrame
    {
        public long BaseOffset    { get; }
        public long? ParentLength { get; }
        public int  IndentLevel   { get; }
        public long CurrentOffset { get; set; }

        /// <summary>Named field values parsed so far in this frame (for "prev.X" resolution).</summary>
        public Dictionary<string, long> FieldValues { get; } = new(StringComparer.Ordinal);

        public InterpreterFrame(long baseOffset, long? parentLength, int indentLevel)
        {
            BaseOffset    = baseOffset;
            ParentLength  = parentLength;
            IndentLevel   = indentLevel;
            CurrentOffset = baseOffset;
        }
    }
}
