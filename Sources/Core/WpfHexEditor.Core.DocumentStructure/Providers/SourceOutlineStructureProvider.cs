// ==========================================================
// Project: WpfHexEditor.Core.DocumentStructure
// File: Providers/SourceOutlineStructureProvider.cs
// Created: 2026-04-05
// Description:
//     Structure provider using SourceOutlineEngine for .cs and .xaml files.
//     Converts SourceOutlineModel types/members/XamlElements into
//     DocumentStructureNode trees without requiring an LSP server.
//
// Architecture Notes:
//     Priority 500. Used when no LSP server is available for the file.
//     Delegates to ISourceOutlineService which is regex-based (no Roslyn).
// ==========================================================

using WpfHexEditor.Core.ProjectSystem.Languages;
using WpfHexEditor.Core.SourceAnalysis.Models;
using WpfHexEditor.Core.SourceAnalysis.Services;
using WpfHexEditor.SDK.ExtensionPoints.DocumentStructure;

namespace WpfHexEditor.Core.DocumentStructure.Providers;

/// <summary>
/// Regex-based structure provider for .cs and .xaml files (Priority 500).
/// Uses <see cref="ISourceOutlineService"/> to produce the outline.
/// </summary>
public sealed class SourceOutlineStructureProvider : IDocumentStructureProvider
{
    private readonly ISourceOutlineService _outlineService;

    public string DisplayName => "Source Outline";
    public int Priority => 500;

    public SourceOutlineStructureProvider(ISourceOutlineService outlineService)
        => _outlineService = outlineService;

    public bool CanProvide(string? filePath, string? documentType, string? language)
    {
        if (string.IsNullOrEmpty(filePath)) return false;
        // Exclude .xaml — XmlStructureProvider (Priority 300) produces a full hierarchy
        // tree via XDocument; this provider only extracts flat x:Name lists for XAML.
        if (filePath.EndsWith(".xaml", StringComparison.OrdinalIgnoreCase)) return false;
        return _outlineService.CanOutline(filePath);
    }

    public async Task<DocumentStructureResult?> GetStructureAsync(string filePath, CancellationToken ct = default)
    {
        var model = await _outlineService.GetOutlineAsync(filePath, ct).ConfigureAwait(false);
        if (model is null) return null;

        var nodes = model.Kind switch
        {
            SourceFileKind.CSharp => ConvertCSharp(model),
            SourceFileKind.Xaml   => ConvertXaml(model),
            _                     => [],
        };

        if (nodes.Count == 0) return null;

        return new DocumentStructureResult
        {
            Nodes = nodes,
            FilePath = filePath,
            Language = LanguageRegistry.Instance.GetLanguageForFile(filePath)?.Id
                    ?? (model.Kind == SourceFileKind.CSharp ? "csharp" : "xaml"),
        };
    }

    private static IReadOnlyList<DocumentStructureNode> ConvertCSharp(SourceOutlineModel model)
    {
        return model.Types.Select(t => new DocumentStructureNode
        {
            Name = t.Name,
            Kind = MapTypeKind(t.Kind),
            Detail = t.IsStatic ? "static" : t.IsAbstract ? "abstract" : null,
            StartLine = t.LineNumber,
            Children = t.Members.Select(m => new DocumentStructureNode
            {
                Name = m.Name,
                Kind = MapMemberKind(m.Kind),
                Detail = m.ReturnType,
                StartLine = m.LineNumber,
            }).ToList(),
        }).ToList();
    }

    private static IReadOnlyList<DocumentStructureNode> ConvertXaml(SourceOutlineModel model)
    {
        var nodes = new List<DocumentStructureNode>();

        if (!string.IsNullOrEmpty(model.XamlClass))
        {
            nodes.Add(new DocumentStructureNode
            {
                Name = model.XamlClass!,
                Kind = "class",
            });
        }

        foreach (var elem in model.XamlElements)
        {
            nodes.Add(new DocumentStructureNode
            {
                Name = elem.Name,
                Kind = "element",
                Detail = elem.TypeHint,
                StartLine = elem.LineNumber,
            });
        }

        return nodes;
    }

    private static string MapTypeKind(SourceTypeKind kind) => kind switch
    {
        SourceTypeKind.Class        => "class",
        SourceTypeKind.Struct       => "struct",
        SourceTypeKind.Interface    => "interface",
        SourceTypeKind.Enum         => "enum",
        SourceTypeKind.Record       => "record",
        SourceTypeKind.RecordStruct => "struct",
        _                           => "class",
    };

    private static string MapMemberKind(SourceMemberKind kind) => kind switch
    {
        SourceMemberKind.Constructor => "constructor",
        SourceMemberKind.Method      => "method",
        SourceMemberKind.Property    => "property",
        SourceMemberKind.Field       => "field",
        SourceMemberKind.Event       => "event",
        _                            => "method",
    };
}
