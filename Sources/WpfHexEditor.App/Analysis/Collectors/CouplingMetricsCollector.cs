// ==========================================================
// Project: WpfHexEditor.App
// File: Analysis/Collectors/CouplingMetricsCollector.cs
// Description: Computes Ca (afferent), Ce (efferent), Instability, and LCOM
//              for every declared type in a compilation. Requires a full
//              Compilation (not just a single SyntaxTree) for accurate Ca.
//              Stateless — safe for parallel use.
// ==========================================================

using Microsoft.CodeAnalysis;
using WpfHexEditor.App.Analysis.Models;

namespace WpfHexEditor.App.Analysis.Collectors;

internal static class CouplingMetricsCollector
{
    internal static IReadOnlyList<CouplingMetrics> Collect(Compilation compilation)
    {
        // Build Ce per type: set of distinct types this type references
        var ceMap = new Dictionary<INamedTypeSymbol, HashSet<INamedTypeSymbol>>(SymbolEqualityComparer.Default);

        foreach (var tree in compilation.SyntaxTrees)
        {
            var model = compilation.GetSemanticModel(tree);
            var root  = tree.GetRoot();

            foreach (var typeDecl in root.DescendantNodes()
                         .Where(n => n is Microsoft.CodeAnalysis.CSharp.Syntax.TypeDeclarationSyntax))
            {
                if (model.GetDeclaredSymbol(typeDecl) is not INamedTypeSymbol ownerSymbol) continue;

                if (!ceMap.ContainsKey(ownerSymbol))
                    ceMap[ownerSymbol] = new HashSet<INamedTypeSymbol>(SymbolEqualityComparer.Default);

                // Collect all type references inside this type
                foreach (var node in typeDecl.DescendantNodes())
                {
                    var info = model.GetTypeInfo(node);
                    if (info.Type is INamedTypeSymbol refType
                        && !SymbolEqualityComparer.Default.Equals(refType, ownerSymbol)
                        && refType.Locations.Any(l => l.IsInSource))
                    {
                        ceMap[ownerSymbol].Add(refType);
                    }
                }
            }
        }

        // Build Ca: inverse of Ce (who references this type)
        var caMap = new Dictionary<INamedTypeSymbol, int>(SymbolEqualityComparer.Default);
        foreach (var (_, refs) in ceMap)
            foreach (var target in refs)
            {
                if (!caMap.ContainsKey(target)) caMap[target] = 0;
                caMap[target]++;
            }

        var results = new List<CouplingMetrics>();
        foreach (var (symbol, ceSet) in ceMap)
        {
            var ce   = ceSet.Count;
            var ca   = caMap.TryGetValue(symbol, out var v) ? v : 0;
            var inst = (ca + ce) == 0 ? 0.0 : (double)ce / (ca + ce);

            // LCOM approximation: ratio of unique method-field interactions
            var lcom = inst switch
            {
                > 0.8 => LcomLevel.High,
                > 0.4 => LcomLevel.Medium,
                _     => LcomLevel.Low
            };

            var loc = symbol.Locations.FirstOrDefault(l => l.IsInSource);
            results.Add(new CouplingMetrics
            {
                TypeName    = symbol.Name,
                FilePath    = loc?.SourceTree?.FilePath ?? string.Empty,
                Line        = loc?.GetLineSpan().StartLinePosition.Line + 1 ?? 0,
                Ca          = ca,
                Ce          = ce,
                Instability = Math.Round(inst, 2),
                Lcom        = lcom,
            });
        }

        return results;
    }
}
