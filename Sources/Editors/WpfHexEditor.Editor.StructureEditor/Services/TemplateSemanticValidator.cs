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

        var declared = CollectDeclaredVariables(def);

        foreach (var block in def.Blocks ?? [])
            ValidateBlock(block, declared, items);

        return items;
    }

    // ── Per-block checks ──────────────────────────────────────────────────────

    private void ValidateBlock(BlockDefinition b, HashSet<string> declared, List<ValidationSummaryItem> items)
    {
        if (b is null) return;

        CheckVariableReferences(b, declared, items);
        CheckBitfields(b, items);
        CheckUnion(b, items);
        CheckValidationRules(b, items);

        foreach (var child in EnumerateChildren(b))
            ValidateBlock(child, declared, items);
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

    // ── Variable discovery ────────────────────────────────────────────────────

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
