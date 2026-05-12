//////////////////////////////////////////////
// GNU Affero General Public License v3.0 - 2026
// Author : Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Opus 4.7 (1M context)
//////////////////////////////////////////////
// Project: WpfHexEditor.Core.Definitions
// File: Metadata/FormatAssertionEvaluator.cs
// Description: F7 — evaluates an entry's assertions[] block against a populated
//              WhfmtVariableStore and returns pass/fail status per assertion.
//              This is the bridge between P4 (expression evaluator) and the IDE
//              (Structure pane / ParsedFields / Format Detail panels) that the
//              P0 audit had flagged as missing: assertions were declared and
//              read, but never evaluated against live binary content.
// Architecture notes:
//              No I/O, no UI dependency — pure function on the model layer.
//              Hosts (HexEditor, ParsedFields) populate the variable store from
//              the binary they have open and call EvaluateAll() to get the result.
//////////////////////////////////////////////

using WpfHexEditor.Core.Contracts;
using WpfHexEditor.Core.Definitions.Models;
using WpfHexEditor.Core.Definitions.Models.Expressions;
using WpfHexEditor.Core.Definitions.Models.Functions;

namespace WpfHexEditor.Core.Definitions.Metadata;

/// <summary>Outcome of a single assertion evaluation.</summary>
public enum AssertionStatus
{
    /// <summary>Not yet evaluated (default / loading state).</summary>
    Pending,
    /// <summary>Expression evaluated to true.</summary>
    Pass,
    /// <summary>Expression evaluated to false; the assertion is violated.</summary>
    Fail,
    /// <summary>Expression failed to parse or evaluate; details in <see cref="AssertionResult.Error"/>.</summary>
    Error,
}

/// <summary>A single evaluated assertion: original rule + status + optional error message.</summary>
public sealed record AssertionResult(AssertionRule Rule, AssertionStatus Status, string? Error);

/// <summary>
/// Evaluates an entry's assertions against a populated <see cref="WhfmtVariableStore"/>.
/// </summary>
public static class FormatAssertionEvaluator
{
    /// <summary>
    /// Evaluates every assertion declared by <paramref name="entry"/> against
    /// <paramref name="store"/>. Returns one <see cref="AssertionResult"/> per rule
    /// preserving the catalog order. Empty input → empty output.
    /// </summary>
    public static IReadOnlyList<AssertionResult> EvaluateAll(
        EmbeddedFormatEntry entry,
        IEmbeddedFormatCatalog catalog,
        WhfmtVariableStore store,
        WhfmtFunctionRegistry? functions = null)
    {
        ArgumentNullException.ThrowIfNull(entry);
        ArgumentNullException.ThrowIfNull(catalog);
        ArgumentNullException.ThrowIfNull(store);

        var rules = entry.GetAssertions(catalog);
        if (rules.Count == 0) return [];

        var evaluator = new WhfmtExpressionEvaluator(store, functions);
        var results   = new List<AssertionResult>(rules.Count);
        foreach (var rule in rules)
            results.Add(EvaluateOne(evaluator, rule));
        return results;
    }

    /// <summary>
    /// Evaluates a single rule with the provided <paramref name="evaluator"/>.
    /// Public for callers that already have an evaluator instance and want to
    /// avoid re-creating one per rule (typical when re-evaluating after the
    /// variable store changes).
    /// </summary>
    public static AssertionResult EvaluateOne(WhfmtExpressionEvaluator evaluator, AssertionRule rule)
    {
        if (string.IsNullOrWhiteSpace(rule.Expression))
            return new AssertionResult(rule, AssertionStatus.Pending, null);

        try
        {
            var ok = evaluator.EvaluateBool(rule.Expression);
            return new AssertionResult(rule, ok ? AssertionStatus.Pass : AssertionStatus.Fail, null);
        }
        catch (WhfmtExpressionException ex)
        {
            return new AssertionResult(rule, AssertionStatus.Error, ex.Message);
        }
        catch (Exception ex)
        {
            return new AssertionResult(rule, AssertionStatus.Error, ex.Message);
        }
    }
}
