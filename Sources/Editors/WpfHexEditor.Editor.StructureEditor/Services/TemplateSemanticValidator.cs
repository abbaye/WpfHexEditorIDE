// ==========================================================
// Project: WpfHexEditor.Editor.StructureEditor
// File: Services/TemplateSemanticValidator.cs
// Description:
//     Runtime semantic validation of a parsed FormatDefinition. Complements
//     the schema-only FormatDefinitionValidator with rules that catch
//     authoring errors only visible after deserialization:
//       - Variable references resolve to a declared variable
//       - Bitfield bit-ranges do not overlap inside a single field
//       - Union variant keys are unique (case-sensitive) and at least one
//         variant exists when a UnionCondition is set
//       - FieldValidationRules min/max numeric coherence
// Architecture: pure C#, no UI, no WPF — easy to unit test.
// ==========================================================

using System.Text.RegularExpressions;
using WpfHexEditor.Core.FormatDetection;
using WpfHexEditor.Editor.Core.Validation;
using WpfHexEditor.Editor.StructureEditor.ViewModels;

namespace WpfHexEditor.Editor.StructureEditor.Services;

/// <summary>Runs semantic checks on a <see cref="FormatDefinition"/>.</summary>
public sealed class TemplateSemanticValidator
{
    private const string Layer = "Semantic";

    private static readonly Regex VarRefRegex = new(@"\bvar:([A-Za-z_]\w*)", RegexOptions.Compiled);

    /// <summary>Validates <paramref name="def"/> and returns one item per finding.</summary>
    public IReadOnlyList<ValidationSummaryItem> Validate(FormatDefinition def)
    {
        var items = new List<ValidationSummaryItem>();
        if (def is null) return items;

        var declared  = CollectDeclaredVariables(def);
        var varTypes  = CollectVariableTypes(def);

        foreach (var block in def.Blocks ?? [])
            ValidateBlock(block, declared, varTypes, items);

        CheckDuplicateBlockNames(def.Blocks ?? [], items);

        return items;
    }

    // ── Per-block checks ──────────────────────────────────────────────────────

    private void ValidateBlock(BlockDefinition b, HashSet<string> declared,
        Dictionary<string, string> varTypes, List<ValidationSummaryItem> items)
    {
        if (b is null) return;

        CheckVariableReferences(b, declared, items);
        CheckBitfields(b, items);
        CheckUnion(b, items);
        CheckValidationRules(b, items);
        CheckStoreAsTypeMismatch(b, varTypes, items);
        CheckLiteralCondition(b, items);

        foreach (var child in EnumerateChildren(b))
            ValidateBlock(child, declared, varTypes, items);
    }

    private static IEnumerable<BlockDefinition> EnumerateChildren(BlockDefinition b)
    {
        if (b.Then   is { } t) foreach (var c in t) yield return c;
        if (b.Else   is { } e) foreach (var c in e) yield return c;
        if (b.Body   is { } y) foreach (var c in y) yield return c;
        if (b.Fields is { } f) foreach (var c in f) yield return c;
        if (b.Variants is { } v)
            foreach (var kv in v)
                if (kv.Value?.Fields is { } vf)
                    foreach (var c in vf) yield return c;
    }

    private static void CheckVariableReferences(BlockDefinition b, HashSet<string> declared, List<ValidationSummaryItem> items)
    {
        foreach (var raw in CollectExpressionStrings(b))
        {
            foreach (Match m in VarRefRegex.Matches(raw))
            {
                var name = m.Groups[1].Value;
                if (!declared.Contains(name))
                    items.Add(new ValidationSummaryItem
                    {
                        Severity         = ValidationSeverity.Error,
                        Message          = $"Block '{b.Name ?? b.Type}' references unknown variable '{name}'.",
                        Layer            = Layer,
                        NavigationTarget = b.Name,
                    });
            }
        }
    }

    private static IEnumerable<string> CollectExpressionStrings(BlockDefinition b)
    {
        foreach (var s in new[] { AsString(b.Offset), AsString(b.OffsetAdd), AsString(b.Length),
                                  AsString(b.Count),  AsString(b.EntrySize), b.Expression,
                                  b.OffsetFrom, b.TargetVar, b.UnionCondition })
            if (!string.IsNullOrEmpty(s)) yield return s!;
    }

    private static string? AsString(object? o) => o?.ToString();

    private static void CheckBitfields(BlockDefinition b, List<ValidationSummaryItem> items)
    {
        if (b.Bitfields is null || b.Bitfields.Count == 0) return;

        var occupied = new bool[64];
        foreach (var bf in b.Bitfields)
        {
            if (bf is null || string.IsNullOrEmpty(bf.Bits)) continue;
            if (!TryParseBitRange(bf.Bits, out var lo, out var hi))
            {
                items.Add(new ValidationSummaryItem
                {
                    Severity = ValidationSeverity.Warning,
                    Message  = $"Bitfield '{bf.Name}' has unparseable bit-range '{bf.Bits}'.",
                    Layer    = Layer, NavigationTarget = b.Name,
                });
                continue;
            }
            for (var i = lo; i <= hi && i < 64; i++)
            {
                if (occupied[i])
                {
                    items.Add(new ValidationSummaryItem
                    {
                        Severity = ValidationSeverity.Error,
                        Message  = $"Bitfield '{bf.Name}' overlaps another bitfield at bit {i} in block '{b.Name}'.",
                        Layer    = Layer, NavigationTarget = b.Name,
                    });
                    break;
                }
                occupied[i] = true;
            }
        }
    }

    private static bool TryParseBitRange(string bits, out int lo, out int hi)
    {
        lo = hi = -1;
        var parts = bits.Split('-', 2);
        if (parts.Length == 1 && int.TryParse(parts[0], out var single)) { lo = hi = single; return true; }
        if (parts.Length == 2 && int.TryParse(parts[0], out var a) && int.TryParse(parts[1], out var b))
        {
            lo = Math.Min(a, b); hi = Math.Max(a, b); return true;
        }
        return false;
    }

    private static void CheckUnion(BlockDefinition b, List<ValidationSummaryItem> items)
    {
        if (!string.Equals(b.Type, "union", StringComparison.OrdinalIgnoreCase)) return;

        if (string.IsNullOrEmpty(b.UnionCondition))
            items.Add(new ValidationSummaryItem
            {
                Severity = ValidationSeverity.Error,
                Message  = $"Union block '{b.Name}' is missing 'unionCondition'.",
                Layer    = Layer, NavigationTarget = b.Name,
            });

        if (b.Variants is null || b.Variants.Count == 0)
            items.Add(new ValidationSummaryItem
            {
                Severity = ValidationSeverity.Error,
                Message  = $"Union block '{b.Name}' has no variants.",
                Layer    = Layer, NavigationTarget = b.Name,
            });
    }

    private static void CheckValidationRules(BlockDefinition b, List<ValidationSummaryItem> items)
    {
        var v = b.ValidationRules;
        if (v is null) return;

        if (TryAsDouble(v.MinValue, out var min) && TryAsDouble(v.MaxValue, out var max) && min > max)
            items.Add(new ValidationSummaryItem
            {
                Severity = ValidationSeverity.Error,
                Message  = $"Block '{b.Name}': validation min ({min}) is greater than max ({max}).",
                Layer    = Layer, NavigationTarget = b.Name,
            });
    }

    private static bool TryAsDouble(object? o, out double d)
    {
        switch (o)
        {
            case null:    d = 0; return false;
            case double x: d = x; return true;
            case float f:  d = f; return true;
            case int i:    d = i; return true;
            case long l:   d = l; return true;
            default:
                return double.TryParse(o.ToString(),
                    System.Globalization.NumberStyles.Float,
                    System.Globalization.CultureInfo.InvariantCulture, out d);
        }
    }

    // ── New Phase 2 checks ───────────────────────────────────────────────────

    // Numeric types: storeAs into a var declared 'bool' or 'hex' could be mismatched.
    private static readonly HashSet<string> NumericValueTypes =
        new(StringComparer.OrdinalIgnoreCase)
        { "uint8","uint16","uint32","uint64","int8","int16","int32","int64","float","double" };

    private static void CheckStoreAsTypeMismatch(BlockDefinition b,
        Dictionary<string, string> varTypes, List<ValidationSummaryItem> items)
    {
        if (string.IsNullOrEmpty(b.StoreAs) || string.IsNullOrEmpty(b.ValueType)) return;
        if (!varTypes.TryGetValue(b.StoreAs!, out var declaredType)) return;

        bool fieldIsNumeric = NumericValueTypes.Contains(b.ValueType);
        bool varIsBool      = string.Equals(declaredType, "bool", StringComparison.OrdinalIgnoreCase);

        if (fieldIsNumeric && varIsBool)
            items.Add(new ValidationSummaryItem
            {
                Severity         = ValidationSeverity.Warning,
                Message          = $"Block '{b.Name}' stores numeric type '{b.ValueType}' into bool variable '{b.StoreAs}'.",
                Layer            = Layer,
                NavigationTarget = b.Name,
            });
    }

    private static void CheckLiteralCondition(BlockDefinition b, List<ValidationSummaryItem> items)
    {
        if (!string.Equals(b.Type, "conditional", StringComparison.OrdinalIgnoreCase)) return;
        var cond = b.Condition?.ToString()?.Trim();
        if (string.IsNullOrEmpty(cond)) return;

        if (string.Equals(cond, "true", StringComparison.OrdinalIgnoreCase))
            items.Add(new ValidationSummaryItem
            {
                Severity         = ValidationSeverity.Warning,
                Message          = $"Conditional block '{b.Name}' has a literal 'true' condition — Else branch is unreachable.",
                Layer            = Layer,
                NavigationTarget = b.Name,
            });
        else if (string.Equals(cond, "false", StringComparison.OrdinalIgnoreCase))
            items.Add(new ValidationSummaryItem
            {
                Severity         = ValidationSeverity.Warning,
                Message          = $"Conditional block '{b.Name}' has a literal 'false' condition — Then branch is unreachable.",
                Layer            = Layer,
                NavigationTarget = b.Name,
            });
    }

    private static void CheckDuplicateBlockNames(IEnumerable<BlockDefinition> blocks,
        List<ValidationSummaryItem> items)
    {
        var seen   = new HashSet<string>(StringComparer.Ordinal);
        var dupes  = new HashSet<string>(StringComparer.Ordinal);
        foreach (var b in FlattenAll(blocks))
        {
            if (string.IsNullOrEmpty(b.Name)) continue;
            if (!seen.Add(b.Name!) && dupes.Add(b.Name!))
                items.Add(new ValidationSummaryItem
                {
                    Severity         = ValidationSeverity.Warning,
                    Message          = $"Block name '{b.Name}' appears more than once. Duplicate names can cause variable-store collisions.",
                    Layer            = Layer,
                    NavigationTarget = b.Name,
                });
        }
    }

    private static IEnumerable<BlockDefinition> FlattenAll(IEnumerable<BlockDefinition> blocks)
    {
        foreach (var b in blocks)
        {
            if (b is null) continue;
            yield return b;
            foreach (var child in EnumerateChildren(b))
                foreach (var sub in FlattenAll([child]))
                    yield return sub;
        }
    }

    // ── Variable discovery ────────────────────────────────────────────────────

    private static Dictionary<string, string> CollectVariableTypes(FormatDefinition def)
    {
        // FormatDefinition.Variables stores initial values (object), not type strings.
        // The dedicated type field lives only in the editor VM — not in the runtime JSON.
        // Return empty map; type-mismatch checks would need an extended schema to be reliable.
        return new Dictionary<string, string>(StringComparer.Ordinal);
    }

    private static HashSet<string> CollectDeclaredVariables(FormatDefinition def)
    {
        var set = new HashSet<string>(StringComparer.Ordinal);

        if (def.Variables is { } vars)
            foreach (var key in vars.Keys) set.Add(key);

        foreach (var block in def.Blocks ?? [])
            WalkAndCollect(block, set);

        return set;
    }

    private static void WalkAndCollect(BlockDefinition b, HashSet<string> set)
    {
        if (b is null) return;
        if (!string.IsNullOrEmpty(b.StoreAs))            set.Add(b.StoreAs!);
        if (!string.IsNullOrEmpty(b.MappedValueStoreAs)) set.Add(b.MappedValueStoreAs!);
        if (!string.IsNullOrEmpty(b.IndexVar))           set.Add(b.IndexVar!);

        foreach (var child in EnumerateChildren(b))
            WalkAndCollect(child, set);
    }
}
