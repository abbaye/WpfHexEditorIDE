// ==========================================================
// Project: WpfHexEditor.App
// File: Analysis/Collectors/VolumeMetricsCollector.cs
// Description: Collects LOC, type counts, member counts, DIT, NOC, comment density,
//              and LCOM4 cohesion per file. Stateless — safe for parallel use.
//              NOC uses a per-compilation cache (BuildNocMap) to avoid the
//              O(types²) walk that the naive per-type lookup would cause.
// ==========================================================

using System.Collections.Concurrent;
using System.IO;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using WpfHexEditor.App.Analysis.Models;

namespace WpfHexEditor.App.Analysis.Collectors;

internal static class VolumeMetricsCollector
{
    /// <summary>
    /// Build the NOC (number-of-children) map once per compilation. Counts how many
    /// types in the compilation directly inherit from each base type. O(T) walk.
    /// </summary>
    internal static IReadOnlyDictionary<INamedTypeSymbol, int> BuildNocMap(Compilation compilation)
    {
        var map = new Dictionary<INamedTypeSymbol, int>(SymbolEqualityComparer.Default);
        foreach (var t in compilation.GlobalNamespace.GetAllTypes())
        {
            if (t.BaseType is null) continue;
            map[t.BaseType] = map.GetValueOrDefault(t.BaseType) + 1;
        }
        return map;
    }

    internal static FileMetrics Collect(SyntaxTree tree, SemanticModel? model, string projectName,
        IReadOnlyDictionary<INamedTypeSymbol, int>? nocMap = null)
    {
        var root     = tree.GetRoot();
        var text     = tree.GetText();
        var filePath = tree.FilePath;

        int total   = text.Lines.Count;
        int blank   = 0;
        int comment = 0;

        foreach (var line in text.Lines)
        {
            var trimmed = line.ToString().Trim();
            if (trimmed.Length == 0)
                blank++;
            else if (trimmed.StartsWith("//") || trimmed.StartsWith("/*") || trimmed.StartsWith("*"))
                comment++;
        }

        int code = total - blank - comment;
        double commentDensity = code > 0 ? Math.Round((double)comment / code * 100.0, 1) : 0;

        var types      = new List<TypeDeclarationSyntax>();
        var methods    = new List<MethodDeclarationSyntax>();
        var properties = new List<PropertyDeclarationSyntax>();

        foreach (var node in root.DescendantNodes())
        {
            switch (node)
            {
                case TypeDeclarationSyntax   t: types.Add(t);      break;
                case MethodDeclarationSyntax m: methods.Add(m);    break;
                case PropertyDeclarationSyntax p: properties.Add(p); break;
            }
        }

        int maxDit  = (model is null || types.Count == 0) ? 0 : types.Max(t => ComputeDit(t, model));
        int maxNoc  = (model is null || types.Count == 0 || nocMap is null) ? 0
                    : types.Max(t => ComputeNoc(t, model, nocMap));
        int maxLcom = types.Count == 0 ? 0 : types.Max(LcomCalculator.Compute);

        return new FileMetrics
        {
            FilePath        = filePath,
            FileName        = Path.GetFileName(filePath),
            ProjectName     = projectName,
            TotalLines      = total,
            CodeLines       = code,
            BlankLines      = blank,
            CommentLines    = comment,
            CommentDensity  = commentDensity,
            TypeCount       = types.Count,
            MethodCount     = methods.Count,
            PropertyCount   = properties.Count,
            MaxDit          = maxDit,
            MaxNoc          = maxNoc,
            MaxLcom         = maxLcom,
        };
    }

    private static int ComputeDit(TypeDeclarationSyntax type, SemanticModel model)
    {
        if (model.GetDeclaredSymbol(type) is not INamedTypeSymbol symbol) return 0;

        int depth = 0;
        var current = symbol.BaseType;
        while (current is not null && current.SpecialType != SpecialType.System_Object)
        {
            depth++;
            current = current.BaseType;
        }
        return depth;
    }

    private static int ComputeNoc(TypeDeclarationSyntax type, SemanticModel model,
        IReadOnlyDictionary<INamedTypeSymbol, int> nocMap)
    {
        if (model.GetDeclaredSymbol(type) is not INamedTypeSymbol symbol) return 0;
        return nocMap.TryGetValue(symbol, out var v) ? v : 0;
    }
}

internal static class NamespaceTypeExtensions
{
    public static IEnumerable<INamedTypeSymbol> GetAllTypes(this INamespaceSymbol ns)
    {
        foreach (var t in ns.GetTypeMembers())
        {
            yield return t;
            foreach (var nested in GetNestedTypes(t)) yield return nested;
        }
        foreach (var sub in ns.GetNamespaceMembers())
            foreach (var t in sub.GetAllTypes()) yield return t;
    }

    private static IEnumerable<INamedTypeSymbol> GetNestedTypes(INamedTypeSymbol type)
    {
        foreach (var t in type.GetTypeMembers())
        {
            yield return t;
            foreach (var n in GetNestedTypes(t)) yield return n;
        }
    }
}
