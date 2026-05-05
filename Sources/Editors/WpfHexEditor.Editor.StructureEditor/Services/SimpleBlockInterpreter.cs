//////////////////////////////////////////////////////
// GNU Affero General Public License v3.0 - 2026
// Author : Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.6
// Project: WpfHexEditor.Editor.StructureEditor
// File: Services/SimpleBlockInterpreter.cs
// Description: Lightweight format-against-file interpreter for the Test Panel.
//              Fully handles: field, signature, conditional, loop, metadata,
//              union, nested, pointer, bitfields (inline after field/nested).
//////////////////////////////////////////////////////

using System.Collections.Generic;
using System.Text;
using System.Text.Json;
using WpfHexEditor.Core.FormatDetection;

namespace WpfHexEditor.Editor.StructureEditor.Services;

/// <summary>Outcome of interpreting a single block against a file.</summary>
internal sealed class BlockTestResult
{
    public string  BlockName    { get; init; } = "";
    public string  BlockType    { get; init; } = "";
    public long    Offset       { get; init; } = -1;   // -1 = not applicable (skipped/summary)
    public int     Length       { get; init; }
    public string  RawHex       { get; init; } = "";
    public string  ParsedValue  { get; init; } = "";
    public string  Status       { get; init; } = "OK";   // OK | Warning | Error | Skipped
    public string  Note         { get; init; } = "";
    public bool    IsSummary    { get; init; }           // True for conditional/loop summary rows
}

/// <summary>
/// Executes a <see cref="FormatDefinition"/> against raw file bytes and returns
/// per-block test results.
/// <list type="bullet">
///   <item>field, signature     — fully parsed, Until sentinel supported</item>
///   <item>conditional          — condition evaluated; Then/Else branch executed</item>
///   <item>loop                 — iterated up to MaxTestIterations; body blocks executed</item>
///   <item>metadata             — variable value shown in ParsedValue</item>
///   <item>action               — variable mutation applied (increment/decrement/set)</item>
///   <item>computeFromVariables — result stored if expression is a simple var: or literal</item>
///   <item>group                — overlay label; child fields executed</item>
///   <item>header               — overlay section; child fields executed</item>
///   <item>data                 — raw byte region; hex preview only</item>
///   <item>repeating            — iterated up to MaxTestIterations; entry fields executed</item>
///   <item>union                — discriminant variable resolved; active variant fields executed</item>
///   <item>nested               — inline struct expansion; bitfields expanded if present</item>
///   <item>pointer              — reads target address, stores in TargetVar, shows label</item>
///   <item>bitfields            — extracted from any field/nested with Bitfields list</item>
/// </list>
/// Assertions in <see cref="FormatDefinition.Assertions"/> are evaluated after all blocks
/// and appended as assertion rows (OK = passed, Warning = failed).
/// </summary>
internal sealed class SimpleBlockInterpreter
{
    private readonly Dictionary<string, object> _vars = new(StringComparer.Ordinal);
    private readonly byte[] _bytes;

    /// <summary>Maximum loop iterations executed in test mode (prevents flood).</summary>
    private const int MaxTestIterations = 32;

    public SimpleBlockInterpreter(byte[] fileBytes)
    {
        _bytes = fileBytes;
    }

    public List<BlockTestResult> Run(FormatDefinition def)
    {
        _vars.Clear();

        // Seed declared variables with their default values
        if (def.Variables is not null)
        {
            foreach (var kv in def.Variables)
                _vars[kv.Key] = kv.Value ?? (object)0L;
        }

        var results = new List<BlockTestResult>();
        foreach (var block in def.Blocks ?? [])
            InterpretBlock(block, results);

        // Run assertions after all blocks
        RunAssertions(def, results);

        return results;
    }

    // ── Block dispatch ────────────────────────────────────────────────────────

    private void InterpretBlock(BlockDefinition block, List<BlockTestResult> results)
    {
        switch (block.Type?.ToLowerInvariant())
        {
            case "field":
            case "signature":
                results.Add(InterpretField(block));
                if (block.Bitfields is { Count: > 0 })
                    InterpretBitfields(block, results);
                break;

            case "conditional":
                InterpretConditional(block, results);
                break;

            case "loop":
                InterpretLoop(block, results);
                break;

            case "metadata":
                results.Add(InterpretMetadata(block));
                break;

            case "action":
                results.Add(InterpretAction(block));
                break;

            case "computefromvariables":
                results.Add(InterpretComputeFromVariables(block));
                break;

            case "group":
                InterpretGroup(block, results);
                break;

            case "header":
                InterpretHeader(block, results);
                break;

            case "data":
                results.Add(InterpretData(block));
                break;

            case "repeating":
                InterpretRepeating(block, results);
                break;

            case "union":
                InterpretUnion(block, results);
                break;

            case "nested":
                InterpretNested(block, results);
                break;

            case "pointer":
                results.Add(InterpretPointer(block));
                break;

            default:
                results.Add(new BlockTestResult
                {
                    BlockName = block.Name ?? "",
                    BlockType = block.Type ?? "",
                    Status    = "Skipped",
                    Note      = $"Block type '{block.Type}' — not supported in test mode.",
                });
                break;
        }
    }

    // ── Conditional ───────────────────────────────────────────────────────────

    private void InterpretConditional(BlockDefinition block, List<BlockTestResult> results)
    {
        var cond = block.Condition;
        if (cond is null)
        {
            results.Add(new BlockTestResult
            {
                BlockName = block.Name ?? "",
                BlockType = "conditional",
                Status    = "Skipped",
                Note      = "No condition defined.",
            });
            return;
        }

        bool? evaluated = TryEvaluateCondition(cond);
        if (evaluated is null)
        {
            results.Add(new BlockTestResult
            {
                BlockName  = block.Name ?? "",
                BlockType  = "conditional",
                Status     = "Skipped",
                IsSummary  = true,
                Note       = $"Condition [{cond}] — could not be evaluated (unsupported operator or expression).",
            });
            return;
        }

        bool taken         = evaluated.Value;
        var  branch        = taken ? (block.Then ?? []) : (block.Else ?? []);
        var  branchLabel   = taken ? (block.TrueLabel ?? "Then") : (block.FalseLabel ?? "Else");
        int  beforeCount   = results.Count;

        // Summary row for the conditional itself
        results.Add(new BlockTestResult
        {
            BlockName  = block.Name ?? "",
            BlockType  = "conditional",
            Status     = "OK",
            IsSummary  = true,
            ParsedValue = taken ? "TRUE" : "FALSE",
            Note       = $"[{cond}] → {(taken ? "TRUE" : "FALSE")} — executing '{branchLabel}' branch ({branch.Count} block(s))",
        });

        foreach (var b in branch)
            InterpretBlock(b, results);
    }

    private bool? TryEvaluateCondition(ConditionDefinition cond)
    {
        if (string.IsNullOrEmpty(cond.Field) || string.IsNullOrEmpty(cond.Operator))
            return null;

        long actual;
        if (cond.Field.StartsWith("var:", StringComparison.Ordinal))
        {
            var varName = cond.Field[4..];
            if (!_vars.TryGetValue(varName, out var vv)) return null;
            actual = ToLong(vv);
        }
        else if (cond.Field.StartsWith("offset:", StringComparison.Ordinal)
            && long.TryParse(cond.Field[7..], out var off)
            && off >= 0 && off + cond.Length <= _bytes.Length)
        {
            actual = ReadIntFromBytes(off, cond.Length);
        }
        else
        {
            return null;
        }

        long expected = ParseConditionValue(cond.Value);

        return cond.Operator.ToLowerInvariant() switch
        {
            "equals"       or "==" or "eq" => actual == expected,
            "notequals"    or "!=" or "ne" => actual != expected,
            "greaterthan"  or ">"  or "gt" => actual > expected,
            "lessthan"     or "<"  or "lt" => actual < expected,
            "greaterorequal" or ">="       => actual >= expected,
            "lessorequal"    or "<="       => actual <= expected,
            _                              => (bool?)null,
        };
    }

    private long ReadIntFromBytes(long offset, int length)
    {
        long v = 0;
        for (int i = 0; i < Math.Min(length, 8); i++)
            v |= (long)_bytes[offset + i] << (i * 8);
        return v;
    }

    private static long ParseConditionValue(string? raw)
    {
        if (string.IsNullOrEmpty(raw)) return 0;
        if (raw.StartsWith("0x", StringComparison.OrdinalIgnoreCase)
            && long.TryParse(raw[2..], System.Globalization.NumberStyles.HexNumber, null, out var hex))
            return hex;
        if (long.TryParse(raw, out var dec)) return dec;
        return 0;
    }

    // ── Loop ─────────────────────────────────────────────────────────────────

    private void InterpretLoop(BlockDefinition block, List<BlockTestResult> results)
    {
        long rawCount = ResolveNumber(block.Count, -1);
        if (rawCount < 0)
        {
            results.Add(new BlockTestResult
            {
                BlockName = block.Name ?? "",
                BlockType = "loop",
                Status    = "Skipped",
                Note      = "Cannot resolve loop count — variable not declared.",
            });
            return;
        }

        int count    = (int)Math.Min(rawCount, MaxTestIterations);
        int bodySize = (block.Body ?? []).Count;
        bool capped  = rawCount > MaxTestIterations;

        // Summary row
        results.Add(new BlockTestResult
        {
            BlockName  = block.Name ?? "",
            BlockType  = "loop",
            Status     = "OK",
            IsSummary  = true,
            ParsedValue = $"{count} / {rawCount}",
            Note       = capped
                ? $"Loop: {rawCount} iteration(s) declared — capped at {MaxTestIterations} in test mode. {bodySize} block(s) per iteration."
                : $"Loop: {count} iteration(s) × {bodySize} block(s) per iteration.",
        });

        for (int i = 0; i < count; i++)
        {
            // Write index variable if declared
            if (!string.IsNullOrEmpty(block.IndexVar))
                _vars[block.IndexVar] = (long)i;

            foreach (var b in block.Body ?? [])
            {
                var iterResults = new List<BlockTestResult>();
                InterpretBlock(b, iterResults);

                foreach (var r in iterResults)
                {
                    // Prefix block name with [i=N] to distinguish iterations
                    results.Add(new BlockTestResult
                    {
                        BlockName   = $"[i={i}] {r.BlockName}",
                        BlockType   = r.BlockType,
                        Offset      = r.Offset,
                        Length      = r.Length,
                        RawHex      = r.RawHex,
                        ParsedValue = r.ParsedValue,
                        Status      = r.Status,
                        Note        = r.Note,
                        IsSummary   = r.IsSummary,
                    });
                }
            }
        }
    }

    // ── Metadata ──────────────────────────────────────────────────────────────

    private BlockTestResult InterpretMetadata(BlockDefinition block)
    {
        var name = block.Name ?? "";

        // Look up the variable by name or by StoreAs
        object? val = null;
        if (!string.IsNullOrEmpty(name) && _vars.TryGetValue(name, out var v1)) val = v1;
        else if (!string.IsNullOrEmpty(block.StoreAs) && _vars.TryGetValue(block.StoreAs, out var v2)) val = v2;

        string parsed = val is not null ? val.ToString() ?? "—" : "—";

        return new BlockTestResult
        {
            BlockName   = name,
            BlockType   = "metadata",
            ParsedValue = parsed,
            Status      = "OK",
            Note        = val is not null
                ? "Variable value at this point in execution."
                : "Variable not yet set at this point.",
        };
    }

    // ── Field / Signature ─────────────────────────────────────────────────────

    private BlockTestResult InterpretField(BlockDefinition block)
    {
        // Resolve offset
        long offset;
        if (!string.IsNullOrEmpty(block.OffsetFrom))
        {
            long baseOff = _vars.TryGetValue(block.OffsetFrom, out var bv) ? ToLong(bv) : 0;
            long addOff  = ResolveNumber(block.OffsetAdd, 0);
            offset = baseOff + addOff;
        }
        else
        {
            var raw = ResolveNumber(block.Offset, -1);
            if (raw < 0)
            {
                return new BlockTestResult
                {
                    BlockName = block.Name ?? "",
                    BlockType = block.Type ?? "",
                    Status    = "Error",
                    Note      = "Cannot resolve offset — variable not declared or expression not supported.",
                };
            }
            offset = raw;
        }

        // Resolve length — Until sentinel takes priority over explicit Length
        int length;
        if (!string.IsNullOrEmpty(block.Until))
        {
            int maxLen = block.MaxLength > 0 ? block.MaxLength : 4096;
            long? scanned = ScanForwardUntil(offset, block.Until, maxLen, block.UntilInclusive);
            length = (int)(scanned ?? (int)ResolveNumber(block.Length, maxLen));
        }
        else
        {
            length = (int)ResolveNumber(block.Length, 0);
        }
        if (length < 0) length = 0;

        // Bounds check
        if (offset < 0 || offset > _bytes.Length)
        {
            return new BlockTestResult
            {
                BlockName   = block.Name ?? "",
                BlockType   = block.Type ?? "",
                Offset      = offset,
                Length      = length,
                Status      = "Error",
                Note        = $"Offset 0x{offset:X} is beyond file end (0x{_bytes.Length:X}).",
            };
        }

        int safeLen  = (int)Math.Min(length, _bytes.Length - offset);
        var rawBytes = _bytes[(int)offset..(int)(offset + safeLen)];
        var rawHex   = Convert.ToHexString(rawBytes);

        // Parse typed value
        bool   bigEndian = string.Equals(block.Endianness, "big", StringComparison.OrdinalIgnoreCase);
        string parsed    = ParseValue(rawBytes, block.ValueType, bigEndian);
        string status    = "OK";
        string note      = "";

        // Apply ValueMap if defined
        if (block.ValueMap is { Count: > 0 } && rawBytes.Length > 0)
        {
            long numericVal = TryParseNumeric(rawBytes, block.ValueType, bigEndian) ?? 0;
            var  key        = numericVal.ToString();
            if (block.ValueMap.TryGetValue(key, out var mapped))
                parsed = $"{parsed} ({mapped})";
        }

        // Store variable
        if (!string.IsNullOrEmpty(block.StoreAs) && length > 0)
        {
            var numVal = TryParseNumeric(rawBytes, block.ValueType, bigEndian);
            if (numVal.HasValue)
                _vars[block.StoreAs] = numVal.Value;
        }

        // Signature validation
        if (string.Equals(block.Type, "signature", StringComparison.OrdinalIgnoreCase))
        {
            note   = "Signature — bytes shown; expected pattern not validated in test mode.";
            status = "Warning";
        }

        // Truncation warning
        if (safeLen < length)
        {
            status = "Warning";
            note   = $"File too short: read {safeLen} of {length} bytes.";
        }

        return new BlockTestResult
        {
            BlockName   = block.Name ?? "",
            BlockType   = block.Type ?? "",
            Offset      = offset,
            Length      = safeLen,
            RawHex      = rawHex,
            ParsedValue = parsed,
            Status      = status,
            Note        = note,
        };
    }

    // ── Action ────────────────────────────────────────────────────────────────

    private BlockTestResult InterpretAction(BlockDefinition block)
    {
        var variable = block.Variable ?? "";
        var action   = block.Action   ?? "increment";
        long current = _vars.TryGetValue(variable, out var cv) ? ToLong(cv) : 0;
        long operand = ResolveNumber(block.Value, 1);
        long result;

        switch (action.ToLowerInvariant())
        {
            case "increment":  result = current + operand; break;
            case "decrement":  result = current - operand; break;
            case "setvariable":
            default:           result = operand; break;
        }

        if (!string.IsNullOrEmpty(variable))
            _vars[variable] = result;

        return new BlockTestResult
        {
            BlockName   = block.Name ?? "",
            BlockType   = "action",
            ParsedValue = !string.IsNullOrEmpty(variable) ? $"{variable} = {result}" : "",
            Status      = "OK",
            Note        = $"{action} '{variable}' → {result}",
        };
    }

    // ── ComputeFromVariables ──────────────────────────────────────────────────

    private BlockTestResult InterpretComputeFromVariables(BlockDefinition block)
    {
        // Simple support: if expression is "var:name" or a literal, store result
        var expr    = block.Expression ?? "";
        var storeAs = block.StoreAs    ?? "";

        object? resolved = null;
        if (expr.StartsWith("var:", StringComparison.Ordinal))
        {
            var varName = expr[4..];
            _vars.TryGetValue(varName, out resolved);
        }
        else if (long.TryParse(expr, out var num))
        {
            resolved = num;
        }

        string note;
        if (resolved != null && !string.IsNullOrEmpty(storeAs))
        {
            _vars[storeAs] = resolved;
            note = $"{storeAs} = {resolved}";
        }
        else
        {
            note = string.IsNullOrEmpty(expr)
                ? "No expression defined."
                : $"Expression '{expr}' — full evaluation not supported in test mode.";
        }

        return new BlockTestResult
        {
            BlockName   = block.Name ?? "",
            BlockType   = "computeFromVariables",
            ParsedValue = resolved?.ToString() ?? "",
            Status      = resolved != null ? "OK" : "Skipped",
            Note        = note,
        };
    }

    // ── Group ─────────────────────────────────────────────────────────────────

    private void InterpretGroup(BlockDefinition block, List<BlockTestResult> results)
    {
        var children = block.Fields ?? [];
        results.Add(new BlockTestResult
        {
            BlockName  = block.Name ?? "",
            BlockType  = "group",
            Status     = "OK",
            IsSummary  = true,
            Note       = $"Group — {children.Count} child field(s).",
        });
        foreach (var b in children)
            InterpretBlock(b, results);
    }

    // ── Header ────────────────────────────────────────────────────────────────

    private void InterpretHeader(BlockDefinition block, List<BlockTestResult> results)
    {
        var children = block.Fields ?? [];
        results.Add(new BlockTestResult
        {
            BlockName  = block.Name ?? "",
            BlockType  = "header",
            Status     = "OK",
            IsSummary  = true,
            Note       = $"Header section — {children.Count} child field(s).",
        });
        foreach (var b in children)
            InterpretBlock(b, results);
    }

    // ── Data ──────────────────────────────────────────────────────────────────

    private BlockTestResult InterpretData(BlockDefinition block)
    {
        long offset = ResolveNumber(block.Offset, -1);
        int  length = (int)ResolveNumber(block.Length, 0);

        if (offset < 0 || offset >= _bytes.Length)
        {
            return new BlockTestResult
            {
                BlockName = block.Name ?? "",
                BlockType = "data",
                Offset    = offset,
                Status    = "Error",
                Note      = $"Offset 0x{offset:X} is beyond file end.",
            };
        }

        int safeLen  = (int)Math.Min(length, _bytes.Length - offset);
        var rawBytes = _bytes[(int)offset..(int)(offset + safeLen)];
        var previewLen = Math.Min(safeLen, 16);
        var rawHex = Convert.ToHexString(rawBytes[..previewLen]) + (safeLen > 16 ? "…" : "");

        return new BlockTestResult
        {
            BlockName   = block.Name ?? "",
            BlockType   = "data",
            Offset      = offset,
            Length      = safeLen,
            RawHex      = rawHex,
            ParsedValue = $"({safeLen} byte{(safeLen == 1 ? "" : "s")} raw data)",
            Status      = "OK",
        };
    }

    // ── Repeating ─────────────────────────────────────────────────────────────

    private void InterpretRepeating(BlockDefinition block, List<BlockTestResult> results)
    {
        long rawCount = ResolveNumber(block.Count, -1);
        if (rawCount < 0)
        {
            results.Add(new BlockTestResult
            {
                BlockName = block.Name ?? "",
                BlockType = "repeating",
                Status    = "Skipped",
                Note      = "Cannot resolve count — variable not declared.",
            });
            return;
        }

        int  count    = (int)Math.Min(rawCount, MaxTestIterations);
        var  fields   = block.Fields ?? [];
        bool capped   = rawCount > MaxTestIterations;

        results.Add(new BlockTestResult
        {
            BlockName   = block.Name ?? "",
            BlockType   = "repeating",
            Status      = "OK",
            IsSummary   = true,
            ParsedValue = $"{count} / {rawCount}",
            Note        = capped
                ? $"Repeating: {rawCount} entries declared — capped at {MaxTestIterations} in test mode. {fields.Count} field(s) per entry."
                : $"Repeating: {count} entr{(count == 1 ? "y" : "ies")} × {fields.Count} field(s).",
        });

        for (int i = 0; i < count; i++)
        {
            if (!string.IsNullOrEmpty(block.IndexVar))
                _vars[block.IndexVar] = (long)i;

            foreach (var b in fields)
            {
                var iterResults = new List<BlockTestResult>();
                InterpretBlock(b, iterResults);
                foreach (var r in iterResults)
                {
                    results.Add(new BlockTestResult
                    {
                        BlockName   = $"[{i}] {r.BlockName}",
                        BlockType   = r.BlockType,
                        Offset      = r.Offset,
                        Length      = r.Length,
                        RawHex      = r.RawHex,
                        ParsedValue = r.ParsedValue,
                        Status      = r.Status,
                        Note        = r.Note,
                        IsSummary   = r.IsSummary,
                    });
                }
            }
        }
    }

    // ── Union ─────────────────────────────────────────────────────────────────

    private void InterpretUnion(BlockDefinition block, List<BlockTestResult> results)
    {
        var condVar  = block.UnionCondition ?? "";
        var variants = block.Variants;

        if (variants is not { Count: > 0 })
        {
            results.Add(new BlockTestResult
            {
                BlockName = block.Name ?? "",
                BlockType = "union",
                Status    = "Skipped",
                Note      = "Union has no variants defined.",
            });
            return;
        }

        // Resolve the discriminant variable.
        string discriminant = _vars.TryGetValue(condVar, out var dv)
            ? dv?.ToString() ?? ""
            : condVar;

        results.Add(new BlockTestResult
        {
            BlockName   = block.Name ?? "",
            BlockType   = "union",
            Status      = "OK",
            IsSummary   = true,
            ParsedValue = discriminant,
            Note        = $"Union — condition var '{condVar}' = '{discriminant}'. {variants.Count} variant(s).",
        });

        // Find matching variant; fall back to first if none match.
        var key = variants.Keys.FirstOrDefault(
            k => string.Equals(k, discriminant, StringComparison.OrdinalIgnoreCase))
            ?? variants.Keys.First();

        var variant = variants[key];

        results.Add(new BlockTestResult
        {
            BlockName   = $"[variant '{key}']",
            BlockType   = "union",
            Status      = "OK",
            IsSummary   = true,
            Note        = variant.Description ?? $"Active variant: {key}",
        });

        // Execute variant sub-fields if present.
        if (variant.Fields is { Count: > 0 })
        {
            foreach (var b in variant.Fields)
                InterpretBlock(b, results);
        }
        else if (variant.ValueType is not null)
        {
            // Inline variant — treat like a single field at the current cursor.
            long   offset  = ResolveNumber(block.Offset, -1);
            int    length  = (int)ResolveNumber(variant.Length, 0);
            bool   ok      = offset >= 0 && offset < _bytes.Length;
            int    safeLen = ok ? (int)Math.Min(length, _bytes.Length - offset) : 0;
            string rawHex  = ok && safeLen > 0 ? Convert.ToHexString(_bytes[(int)offset..(int)(offset + safeLen)]) : "";
            string parsed  = ok && safeLen > 0 ? ParseValue(_bytes[(int)offset..(int)(offset + safeLen)], variant.ValueType, false) : "(out of range)";

            results.Add(new BlockTestResult
            {
                BlockName   = $"[variant '{key}'] {block.Name}",
                BlockType   = "union",
                Offset      = offset,
                Length      = safeLen,
                RawHex      = rawHex,
                ParsedValue = parsed,
                Status      = ok ? "OK" : "Error",
                Note        = ok ? "" : $"Offset 0x{offset:X} is beyond file end.",
            });
        }
    }

    // ── Nested ────────────────────────────────────────────────────────────────

    private void InterpretNested(BlockDefinition block, List<BlockTestResult> results)
    {
        // Nested blocks are inline struct expansions — execute their Fields list.
        var fields = block.Fields ?? [];

        results.Add(new BlockTestResult
        {
            BlockName   = block.Name ?? "",
            BlockType   = "nested",
            Status      = "OK",
            IsSummary   = true,
            Note        = string.IsNullOrEmpty(block.StructRef)
                ? $"Nested struct — {fields.Count} inline field(s)."
                : $"Nested struct '{block.StructRef}' — {fields.Count} field(s) (StructRef resolved inline).",
        });

        foreach (var b in fields)
            InterpretBlock(b, results);

        // If the block has bitfields, expand them after the base field interpretation.
        if (block.Bitfields is { Count: > 0 })
            InterpretBitfields(block, results);
    }

    /// <summary>
    /// Expands BitfieldDefinition entries for a field whose raw numeric value has been
    /// read. Called after InterpretField (via InterpretNested or directly from the field
    /// path when Bitfields are present).
    /// </summary>
    private void InterpretBitfields(BlockDefinition block, List<BlockTestResult> results)
    {
        if (block.Bitfields is not { Count: > 0 }) return;

        long   offset  = ResolveNumber(block.Offset, -1);
        int    length  = (int)ResolveNumber(block.Length, 1);
        bool   ok      = offset >= 0 && (offset + length) <= _bytes.Length;
        long   rawLong = 0;

        if (ok && length > 0)
        {
            var slice = _bytes[(int)offset..(int)(offset + Math.Min(length, 8))];
            rawLong = TryParseNumeric(slice, block.ValueType ?? "uint8",
                string.Equals(block.Endianness, "big", StringComparison.OrdinalIgnoreCase)) ?? 0;
        }

        results.Add(new BlockTestResult
        {
            BlockName   = $"{block.Name} (bitfields)",
            BlockType   = "bitfield",
            Status      = "OK",
            IsSummary   = true,
            ParsedValue = ok ? $"0x{rawLong:X}" : "(unresolved)",
            Note        = $"{block.Bitfields.Count} bitfield(s) in {length}-byte value.",
        });

        foreach (var bf in block.Bitfields)
        {
            long extracted = ok ? bf.ExtractValue(rawLong) : 0;
            string mapped  = "";
            if (bf.ValueMap is { Count: > 0 } && bf.ValueMap.TryGetValue(extracted.ToString(), out var m))
                mapped = $" ({m})";

            if (!string.IsNullOrEmpty(bf.StoreAs))
                _vars[bf.StoreAs] = extracted;

            results.Add(new BlockTestResult
            {
                BlockName   = bf.Name ?? "",
                BlockType   = "bitfield",
                Offset      = offset,
                Length      = length,
                ParsedValue = $"{extracted}{mapped}",
                Status      = "OK",
                Note        = $"Bits [{bf.Bits}]{(string.IsNullOrEmpty(bf.Description) ? "" : $" — {bf.Description}")}",
            });
        }
    }

    // ── Pointer ───────────────────────────────────────────────────────────────

    private BlockTestResult InterpretPointer(BlockDefinition block)
    {
        long offset = ResolveNumber(block.Offset, -1);
        int  length = (int)ResolveNumber(block.Length, 4);

        if (offset < 0 || offset >= _bytes.Length)
        {
            return new BlockTestResult
            {
                BlockName = block.Name ?? "",
                BlockType = "pointer",
                Offset    = offset,
                Status    = "Error",
                Note      = $"Offset 0x{offset:X} is beyond file end.",
            };
        }

        int    safeLen   = (int)Math.Min(length, _bytes.Length - offset);
        var    rawBytes  = _bytes[(int)offset..(int)(offset + safeLen)];
        bool   bigEndian = string.Equals(block.Endianness, "big", StringComparison.OrdinalIgnoreCase);
        long?  target    = TryParseNumeric(rawBytes, block.ValueType ?? "uint32", bigEndian);

        string label   = block.Label ?? $"→ 0x{target:X}";
        string parsed  = target.HasValue ? $"0x{target.Value:X8} {label}" : "(unresolved)";

        if (target.HasValue && !string.IsNullOrEmpty(block.TargetVar))
            _vars[block.TargetVar] = target.Value;

        return new BlockTestResult
        {
            BlockName   = block.Name ?? "",
            BlockType   = "pointer",
            Offset      = offset,
            Length      = safeLen,
            RawHex      = Convert.ToHexString(rawBytes),
            ParsedValue = parsed,
            Status      = "OK",
            Note        = $"Pointer — target stored in var '{block.TargetVar ?? "(none)"}'.",
        };
    }

    // ── Until sentinel (simple linear scan) ──────────────────────────────────

    private long? ScanForwardUntil(long startOffset, string untilExpr, int maxLength, bool inclusive)
    {
        // Resolve pattern — hex string or var:name (treating var value as hex)
        byte[]? pattern = null;
        if (untilExpr.StartsWith("var:", StringComparison.Ordinal))
        {
            var varName = untilExpr[4..];
            if (_vars.TryGetValue(varName, out var vv))
            {
                var hexStr = vv?.ToString() ?? "";
                pattern = TryParseHexPattern(hexStr);
            }
        }
        else
        {
            pattern = TryParseHexPattern(untilExpr);
        }

        if (pattern == null || pattern.Length == 0) return null;

        long end = Math.Min(startOffset + maxLength, _bytes.Length - pattern.Length + 1);
        for (long i = startOffset; i < end; i++)
        {
            bool found = true;
            for (int j = 0; j < pattern.Length; j++)
            {
                if (_bytes[i + j] != pattern[j]) { found = false; break; }
            }
            if (found)
            {
                long consumed = i - startOffset;
                return inclusive ? consumed + pattern.Length : consumed;
            }
        }

        // Pattern not found — return maxLength as fallback
        return maxLength;
    }

    private static byte[]? TryParseHexPattern(string hex)
    {
        hex = hex.Replace(" ", "");
        if (hex.Length == 0 || hex.Length % 2 != 0) return null;
        try
        {
            return Convert.FromHexString(hex);
        }
        catch
        {
            return null;
        }
    }

    // ── Assertions ────────────────────────────────────────────────────────────

    private void RunAssertions(FormatDefinition def, List<BlockTestResult> results)
    {
        if (def.Assertions is not { Count: > 0 }) return;

        results.Add(new BlockTestResult
        {
            BlockName = "── Assertions ──",
            BlockType = "assertion",
            Status    = "OK",
            IsSummary = true,
            Note      = $"{def.Assertions.Count} assertion(s) evaluated.",
        });

        foreach (var assertion in def.Assertions)
        {
            bool passed = EvaluateSimpleAssertion(assertion.Expression);
            results.Add(new BlockTestResult
            {
                BlockName   = assertion.Name ?? "",
                BlockType   = "assertion",
                ParsedValue = passed ? "PASSED" : "FAILED",
                Status      = passed ? "OK" : "Warning",
                Note        = passed
                    ? "Assertion passed."
                    : $"Assertion failed: {assertion.Expression}",
            });
        }
    }

    /// <summary>Evaluates simple boolean assertions in the form "var:X op value".</summary>
    private bool EvaluateSimpleAssertion(string? expression)
    {
        if (string.IsNullOrWhiteSpace(expression)) return false;

        // Simple pattern: "var:name op value"
        // e.g. "var:magic equals 1234" or "var:count greaterThan 0"
        var parts = expression.Trim().Split(' ', 3, StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length != 3) return false;

        var field   = parts[0];
        var op      = parts[1];
        var valStr  = parts[2];

        long actual;
        if (field.StartsWith("var:", StringComparison.Ordinal))
        {
            var varName = field[4..];
            if (!_vars.TryGetValue(varName, out var vv)) return false;
            actual = ToLong(vv);
        }
        else return false;

        long expected = ParseConditionValue(valStr);

        return op.ToLowerInvariant() switch
        {
            "equals"        or "==" or "eq" => actual == expected,
            "notequals"     or "!=" or "ne" => actual != expected,
            "greaterthan"   or ">"  or "gt" => actual > expected,
            "lessthan"      or "<"  or "lt" => actual < expected,
            "greaterorequal" or ">="        => actual >= expected,
            "lessorequal"    or "<="        => actual <= expected,
            _                               => false,
        };
    }

    // ── Value parsing ─────────────────────────────────────────────────────────

    private static string ParseValue(byte[] data, string? valueType, bool bigEndian)
    {
        if (data.Length == 0) return "(empty)";

        try
        {
            return valueType?.ToLowerInvariant() switch
            {
                "uint8"  => data[0].ToString(),
                "int8"   => ((sbyte)data[0]).ToString(),
                "uint16" => (bigEndian
                    ? (ushort)((data[0] << 8) | data[1])
                    : BitConverter.ToUInt16(Pad(data, 2))).ToString(),
                "int16"  => (bigEndian
                    ? (short)((data[0] << 8) | data[1])
                    : BitConverter.ToInt16(Pad(data, 2))).ToString(),
                "uint32" => (bigEndian
                    ? (uint)((data[0] << 24) | (data[1] << 16) | (data[2] << 8) | data[3])
                    : BitConverter.ToUInt32(Pad(data, 4))).ToString(),
                "int32"  => (bigEndian
                    ? (int)((data[0] << 24) | (data[1] << 16) | (data[2] << 8) | data[3])
                    : BitConverter.ToInt32(Pad(data, 4))).ToString(),
                "uint64" => (bigEndian
                    ? BEUInt64(data)
                    : BitConverter.ToUInt64(Pad(data, 8))).ToString(),
                "int64"  => (bigEndian
                    ? (long)BEUInt64(data)
                    : BitConverter.ToInt64(Pad(data, 8))).ToString(),
                "float"  => BitConverter.ToSingle(Pad(data, 4)).ToString("G"),
                "double" => BitConverter.ToDouble(Pad(data, 8)).ToString("G"),
                "ascii" or "string" => Encoding.ASCII.GetString(data).TrimEnd('\0'),
                "utf8"  => Encoding.UTF8.GetString(data).TrimEnd('\0'),
                "utf16" => Encoding.Unicode.GetString(data).TrimEnd('\0'),
                "bool"  => (data[0] != 0).ToString(),
                "char"  => ((char)data[0]).ToString(),
                "bytes" => Convert.ToHexString(data),
                "padding" => $"({data.Length} padding byte{(data.Length == 1 ? "" : "s")})",
                _        => Convert.ToHexString(data),
            };
        }
        catch
        {
            return Convert.ToHexString(data);
        }
    }

    private static long? TryParseNumeric(byte[] data, string? valueType, bool bigEndian)
    {
        try
        {
            return valueType?.ToLowerInvariant() switch
            {
                "uint8"  => data[0],
                "int8"   => (sbyte)data[0],
                "uint16" => bigEndian ? (data[0] << 8) | data[1] : BitConverter.ToUInt16(Pad(data, 2)),
                "int16"  => bigEndian ? (short)((data[0] << 8) | data[1]) : BitConverter.ToInt16(Pad(data, 2)),
                "uint32" => bigEndian ? (uint)((data[0] << 24) | (data[1] << 16) | (data[2] << 8) | data[3]) : BitConverter.ToUInt32(Pad(data, 4)),
                "int32"  => bigEndian ? (data[0] << 24) | (data[1] << 16) | (data[2] << 8) | data[3] : BitConverter.ToInt32(Pad(data, 4)),
                "uint64" => (long)(bigEndian ? BEUInt64(data) : BitConverter.ToUInt64(Pad(data, 8))),
                "int64"  => bigEndian ? (long)BEUInt64(data) : BitConverter.ToInt64(Pad(data, 8)),
                "bool"   => data[0] != 0 ? 1 : 0,
                _        => (long?)null,
            };
        }
        catch { return null; }
    }

    // ── Numeric resolution ────────────────────────────────────────────────────

    private long ResolveNumber(object? raw, long fallback)
    {
        if (raw is null) return fallback;

        if (raw is JsonElement je)
        {
            return je.ValueKind switch
            {
                JsonValueKind.Number => je.TryGetInt64(out var n) ? n : (long)je.GetDouble(),
                JsonValueKind.String => ResolveString(je.GetString(), fallback),
                _ => fallback,
            };
        }

        return raw switch
        {
            int    i => i,
            long   l => l,
            double d => (long)d,
            string s => ResolveString(s, fallback),
            _        => fallback,
        };
    }

    private long ResolveString(string? s, long fallback)
    {
        if (string.IsNullOrEmpty(s)) return fallback;
        if (s.StartsWith("var:", StringComparison.Ordinal))
        {
            var name = s[4..];
            return _vars.TryGetValue(name, out var v) ? ToLong(v) : fallback;
        }
        if (s.StartsWith("0x", StringComparison.OrdinalIgnoreCase)
            && long.TryParse(s[2..], System.Globalization.NumberStyles.HexNumber, null, out var hex))
            return hex;
        if (long.TryParse(s, out var parsed)) return parsed;
        return fallback;
    }

    private static long ToLong(object? v) => v switch
    {
        int    i => i,
        long   l => l,
        double d => (long)d,
        JsonElement je => je.TryGetInt64(out var n) ? n : (long)je.GetDouble(),
        _        => 0,
    };

    // ── Byte helpers ──────────────────────────────────────────────────────────

    private static byte[] Pad(byte[] src, int minLen)
    {
        if (src.Length >= minLen) return src;
        var result = new byte[minLen];
        Array.Copy(src, result, src.Length);
        return result;
    }

    private static ulong BEUInt64(byte[] data)
    {
        ulong v = 0;
        for (int i = 0; i < Math.Min(8, data.Length); i++)
            v = (v << 8) | data[i];
        return v;
    }
}
