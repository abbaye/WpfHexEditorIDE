// ==========================================================
// Project: WpfHexEditor.Core.Roslyn
// File: Providers/RoslynSourceOutlineStructureProvider.cs
// Description:
//     IDocumentStructureProvider (Priority 700) backed by the Roslyn
//     semantic model. Takes precedence over the regex-based
//     SourceOutlineStructureProvider (Priority 500) when a Roslyn workspace
//     has loaded the document. Falls back silently otherwise.
// ==========================================================

using WpfHexEditor.Core.Roslyn.Services;
using WpfHexEditor.Core.SourceAnalysis.Models;
using WpfHexEditor.SDK.ExtensionPoints.DocumentStructure;

namespace WpfHexEditor.Core.Roslyn.Providers;

/// <summary>
/// Document-structure provider sourced from Roslyn semantic analysis.
/// Priority 700 — overrides the regex-based provider (500) when available.
/// </summary>
public sealed class RoslynSourceOutlineStructureProvider : IDocumentStructureProvider
{
    private readonly RoslynSourceOutlineService _service;

    public string DisplayName => "Roslyn Source Outline";
    public int    Priority    => 700;

    public RoslynSourceOutlineStructureProvider(RoslynSourceOutlineService service)
    {
        _service = service;
    }

    public bool CanProvide(string? filePath, string? documentType, string? language)
    {
        if (string.IsNullOrEmpty(filePath)) return false;
        if (!_service.CanOutline(filePath)) return false;
        // The host falls back to the regex provider when no Roslyn document
        // is available; we still claim CanProvide here and return null from
        // GetStructureAsync in that case.
        return true;
    }

    public async Task<DocumentStructureResult?> GetStructureAsync(string filePath, CancellationToken ct = default)
    {
        var model = await _service.GetOutlineAsync(filePath, ct).ConfigureAwait(false);
        if (model is null || model.Types.Count == 0) return null;

        var nodes = model.Types.Select(t => new DocumentStructureNode
        {
            Name      = t.Name,
            Kind      = MapTypeKind(t.Kind),
            Detail    = t.IsStatic ? "static" : t.IsAbstract ? "abstract" : null,
            StartLine = t.LineNumber,
            Children  = t.Members.Select(m => new DocumentStructureNode
            {
                Name      = m.Name,
                Kind      = MapMemberKind(m.Kind),
                Detail    = m.ReturnType,
                StartLine = m.LineNumber,
            }).ToList(),
        }).ToList();

        return new DocumentStructureResult
        {
            Nodes    = nodes,
            FilePath = filePath,
            Language = filePath.EndsWith(".vb", StringComparison.OrdinalIgnoreCase) ? "vbnet" : "csharp",
        };
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
