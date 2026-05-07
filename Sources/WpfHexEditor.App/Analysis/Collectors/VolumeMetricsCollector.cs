// ==========================================================
// Project: WpfHexEditor.App
// File: Analysis/Collectors/VolumeMetricsCollector.cs
// Description: Collects LOC, type counts, member counts, and DIT per file
//              using Roslyn syntax trees. Stateless — safe for parallel use.
// ==========================================================

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using WpfHexEditor.App.Analysis.Models;

namespace WpfHexEditor.App.Analysis.Collectors;

internal static class VolumeMetricsCollector
{
    internal static FileMetrics Collect(SyntaxTree tree, SemanticModel? model, string projectName)
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

        var types      = root.DescendantNodes().OfType<TypeDeclarationSyntax>().ToList();
        var methods    = root.DescendantNodes().OfType<MethodDeclarationSyntax>().ToList();
        var properties = root.DescendantNodes().OfType<PropertyDeclarationSyntax>().ToList();

        int maxDit = model is null ? 0 : types.Max(t => ComputeDit(t, model));

        return new FileMetrics
        {
            FilePath      = filePath,
            FileName      = Path.GetFileName(filePath),
            ProjectName   = projectName,
            TotalLines    = total,
            CodeLines     = total - blank - comment,
            BlankLines    = blank,
            CommentLines  = comment,
            TypeCount     = types.Count,
            MethodCount   = methods.Count,
            PropertyCount = properties.Count,
            MaxDit        = maxDit,
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
}
