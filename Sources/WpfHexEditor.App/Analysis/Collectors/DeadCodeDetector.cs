// ==========================================================
// Project: WpfHexEditor.App
// File: Analysis/Collectors/DeadCodeDetector.cs
// Description: Detects private/internal symbols that are never referenced
//              within the compilation. Uses Roslyn symbol finders via a
//              two-pass approach: declare all symbols, then subtract referenced.
//              Stateless — safe for parallel use.
// ==========================================================

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using WpfHexEditor.App.Analysis.Models;

namespace WpfHexEditor.App.Analysis.Collectors;

internal static class DeadCodeDetector
{
    internal static IReadOnlyList<DeadSymbol> Detect(Compilation compilation)
    {
        // Pass 1: collect all private/internal declared symbols
        var declared = new Dictionary<ISymbol, (string file, int line, DeadSymbolKind kind, bool isInternal)>(
            SymbolEqualityComparer.Default);

        foreach (var tree in compilation.SyntaxTrees)
        {
            var model = compilation.GetSemanticModel(tree);
            foreach (var node in tree.GetRoot().DescendantNodes())
            {
                var symbol = model.GetDeclaredSymbol(node);
                if (symbol is null) continue;
                if (!IsDeadCandidate(symbol, out var kind, out var isInternal)) continue;

                var loc = symbol.Locations.FirstOrDefault(l => l.IsInSource);
                if (loc is null) continue;

                declared[symbol] = (
                    loc.SourceTree?.FilePath ?? string.Empty,
                    loc.GetLineSpan().StartLinePosition.Line + 1,
                    kind,
                    isInternal);
            }
        }

        // Pass 2: find all referenced symbols and remove them from declared
        var referenced = new HashSet<ISymbol>(SymbolEqualityComparer.Default);
        foreach (var tree in compilation.SyntaxTrees)
        {
            var model = compilation.GetSemanticModel(tree);
            foreach (var node in tree.GetRoot().DescendantNodes())
            {
                var info = model.GetSymbolInfo(node);
                if (info.Symbol is not null)
                    referenced.Add(info.Symbol.OriginalDefinition);
            }
        }

        var results = new List<DeadSymbol>();
        foreach (var (symbol, (file, line, kind, isInternal)) in declared)
        {
            if (referenced.Contains(symbol)) continue;
            results.Add(new DeadSymbol
            {
                Name        = symbol.Name,
                Kind        = kind,
                FilePath    = file,
                Line        = line,
                ProjectName = compilation.AssemblyName ?? string.Empty,
                IsInternal  = isInternal,
            });
        }

        return results;
    }

    private static bool IsDeadCandidate(ISymbol symbol, out DeadSymbolKind kind, out bool isInternal)
    {
        kind       = DeadSymbolKind.Field;
        isInternal = symbol.DeclaredAccessibility == Accessibility.Internal;

        bool isPrivateOrInternal = symbol.DeclaredAccessibility
            is Accessibility.Private or Accessibility.Internal;

        if (!isPrivateOrInternal) return false;

        // Skip compiler-generated, partial, and entry-point symbols
        if (symbol.IsImplicitlyDeclared) return false;
        if (symbol is IMethodSymbol m && (m.IsAbstract || m.IsVirtual || m.IsOverride || m.IsStatic && m.Name == "Main"))
            return false;

        kind = symbol switch
        {
            INamedTypeSymbol t when t.TypeKind == TypeKind.Class     => DeadSymbolKind.Class,
            INamedTypeSymbol t when t.TypeKind == TypeKind.Struct    => DeadSymbolKind.Struct,
            INamedTypeSymbol t when t.TypeKind == TypeKind.Interface => DeadSymbolKind.Interface,
            IMethodSymbol                                             => DeadSymbolKind.Method,
            IFieldSymbol                                              => DeadSymbolKind.Field,
            IPropertySymbol                                           => DeadSymbolKind.Property,
            IParameterSymbol                                          => DeadSymbolKind.Parameter,
            ILocalSymbol                                              => DeadSymbolKind.Variable,
            _                                                         => (DeadSymbolKind)(-1),
        };

        return (int)kind >= 0;
    }
}
