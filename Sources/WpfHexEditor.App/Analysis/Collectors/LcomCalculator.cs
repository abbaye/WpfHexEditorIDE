// ==========================================================
// Project: WpfHexEditor.App
// File: Analysis/Collectors/LcomCalculator.cs
// Description: LCOM4 — Lack of Cohesion of Methods (Hitz/Montazeri).
//              Builds a graph where nodes = methods and edges = "shares a field"
//              or "calls another method in the same class". LCOM4 is the number
//              of connected components in this graph.
//                  1 → cohesive
//                  2-4 → multiple responsibilities
//                  ≥5 → god class candidate
// Architecture Notes:
//     Stateless. Uses Roslyn syntax only (no SemanticModel needed) — heuristic.
// ==========================================================

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace WpfHexEditor.App.Analysis.Collectors;

internal static class LcomCalculator
{
    internal static int Compute(TypeDeclarationSyntax type)
    {
        var methods = type.Members.OfType<MethodDeclarationSyntax>().ToList();
        if (methods.Count <= 1) return methods.Count;  // 0 or 1 method → trivially cohesive

        var fields = type.Members
            .OfType<FieldDeclarationSyntax>()
            .SelectMany(f => f.Declaration.Variables.Select(v => v.Identifier.Text))
            .ToHashSet(StringComparer.Ordinal);

        var methodNames = methods.Select(m => m.Identifier.Text).ToHashSet(StringComparer.Ordinal);

        // For each method, collect identifiers it touches (fields + sibling methods)
        var touched = new Dictionary<int, HashSet<string>>();
        for (int i = 0; i < methods.Count; i++)
        {
            var ids = methods[i].DescendantNodes().OfType<IdentifierNameSyntax>()
                .Select(n => n.Identifier.Text)
                .Where(n => fields.Contains(n) || methodNames.Contains(n))
                .ToHashSet(StringComparer.Ordinal);
            touched[i] = ids;
        }

        // Union-Find over methods sharing at least one field/sibling-method
        var parent = Enumerable.Range(0, methods.Count).ToArray();
        for (int i = 0; i < methods.Count; i++)
            for (int j = i + 1; j < methods.Count; j++)
                if (touched[i].Overlaps(touched[j]))
                    Union(parent, i, j);

        return parent.Select((_, i) => Find(parent, i)).Distinct().Count();
    }

    private static int Find(int[] p, int x)
    {
        while (p[x] != x) { p[x] = p[p[x]]; x = p[x]; }
        return x;
    }

    private static void Union(int[] p, int a, int b)
    {
        int ra = Find(p, a), rb = Find(p, b);
        if (ra != rb) p[ra] = rb;
    }
}
