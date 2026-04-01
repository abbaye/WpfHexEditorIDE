// ==========================================================
// Project: WpfHexEditor.Editor.ClassDiagram.Core
// File: Validation/DiagramValidator.cs
// Author: Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.6
// Created: 2026-03-19
// Description:
//     Static validator that inspects a DiagramDocument and returns a
//     list of ValidationResult diagnostics.  Detects circular
//     inheritance, duplicate class names, dangling relationship
//     references, and empty type names.
//
// Architecture Notes:
//     Pattern: Validator — pure side-effect-free function returning
//     an immutable result list.
//
//     Circular inheritance detection uses iterative DFS with a
//     visited/recursion-stack set to avoid stack overflow on large
//     graphs (avoids recursive calls).
//
//     All checks are independent and always run regardless of whether
//     earlier checks found issues — callers receive the full picture
//     in one call.
// ==========================================================

using WpfHexEditor.Editor.ClassDiagram.Core.Model;

namespace WpfHexEditor.Editor.ClassDiagram.Core.Validation;

/// <summary>
/// Validates a <see cref="DiagramDocument"/> and returns all diagnostics.
/// </summary>
public static class DiagramValidator
{
    /// <summary>
    /// Validates <paramref name="doc"/> and returns all findings.
    /// The returned list is ordered: Errors first, then Warnings, then Info.
    /// </summary>
    public static IReadOnlyList<ValidationResult> Validate(DiagramDocument doc)
    {
        var results = new List<ValidationResult>();

        CheckEmptyClassNames(doc, results);
        CheckDuplicateClassNames(doc, results);
        CheckDanglingRelationships(doc, results);
        CheckCircularInheritance(doc, results);

        results.Sort(static (a, b) => a.Severity.CompareTo(b.Severity));
        return results.AsReadOnly();
    }

    // -------------------------------------------------------
    // Individual checks
    // -------------------------------------------------------

    private static void CheckEmptyClassNames(
        DiagramDocument doc, List<ValidationResult> results)
    {
        foreach (var node in doc.Classes)
        {
            if (!string.IsNullOrWhiteSpace(node.Name))
                continue;

            results.Add(new ValidationResult(
                ValidationSeverity.Error,
                "A type declaration has an empty or whitespace name.",
                node.Id));
        }
    }

    private static void CheckDuplicateClassNames(
        DiagramDocument doc, List<ValidationResult> results)
    {
        var seen = new HashSet<string>(StringComparer.Ordinal);
        var reported = new HashSet<string>(StringComparer.Ordinal);

        foreach (var node in doc.Classes)
        {
            if (string.IsNullOrWhiteSpace(node.Name))
                continue;

            if (!seen.Add(node.Name) && reported.Add(node.Name))
            {
                results.Add(new ValidationResult(
                    ValidationSeverity.Error,
                    $"Duplicate type name '{node.Name}'. Each type must have a unique name.",
                    node.Id));
            }
        }
    }

    private static void CheckDanglingRelationships(
        DiagramDocument doc, List<ValidationResult> results)
    {
        var knownIds = doc.Classes
            .Select(n => n.Id)
            .ToHashSet(StringComparer.Ordinal);

        foreach (var rel in doc.Relationships)
        {
            if (!knownIds.Contains(rel.SourceId))
            {
                results.Add(new ValidationResult(
                    ValidationSeverity.Error,
                    $"Relationship source '{rel.SourceId}' references a type that does not exist in the diagram.",
                    rel.SourceId));
            }

            if (!knownIds.Contains(rel.TargetId))
            {
                results.Add(new ValidationResult(
                    ValidationSeverity.Error,
                    $"Relationship target '{rel.TargetId}' references a type that does not exist in the diagram.",
                    rel.TargetId));
            }
        }
    }

    private static void CheckCircularInheritance(
        DiagramDocument doc, List<ValidationResult> results)
    {
        // Build inheritance adjacency: child id → parent id
        var parentOf = doc.Relationships
            .Where(r => r.Kind == RelationshipKind.Inheritance)
            .GroupBy(r => r.SourceId, StringComparer.Ordinal)
            .ToDictionary(
                g => g.Key,
                g => g.First().TargetId,
                StringComparer.Ordinal);

        var reportedCycles = new HashSet<string>(StringComparer.Ordinal);

        foreach (var startNode in doc.Classes)
        {
            if (!parentOf.ContainsKey(startNode.Id))
                continue;

            // Walk the inheritance chain from startNode
            var visited = new HashSet<string>(StringComparer.Ordinal);
            var current = startNode.Id;

            while (parentOf.TryGetValue(current, out var parent))
            {
                if (!visited.Add(parent))
                {
                    // We've seen this node before — cycle detected
                    var cycleKey = string.CompareOrdinal(startNode.Id, parent) <= 0
                        ? $"{startNode.Id}|{parent}"
                        : $"{parent}|{startNode.Id}";

                    if (reportedCycles.Add(cycleKey))
                    {
                        results.Add(new ValidationResult(
                            ValidationSeverity.Error,
                            $"Circular inheritance detected involving type '{parent}'.",
                            parent));
                    }

                    break;
                }

                current = parent;
            }
        }
    }
}
