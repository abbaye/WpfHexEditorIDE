// ==========================================================
// Project: WpfHexEditor.Core.Scripting
// File: ScriptHostAssemblies.cs
// Description:
//     Resolves the set of assemblies available to user scripts.
//     Scripts can use: System.*, WpfHexEditor.SDK, and any other assemblies
//     listed here. Does NOT grant access to internal App assemblies.
// ==========================================================

using System.Reflection;
using Microsoft.CodeAnalysis;

namespace WpfHexEditor.Core.Scripting;

/// <summary>
/// Provides the <see cref="MetadataReference"/> list injected into every user script.
/// Cached after first call.
/// </summary>
internal static class ScriptHostAssemblies
{
    private static IReadOnlyList<MetadataReference>? _cache;
    private static readonly object _lock = new();

    /// <summary>Returns the cached metadata references for script compilation.</summary>
    public static IReadOnlyList<MetadataReference> GetReferences()
    {
        lock (_lock)
        {
            if (_cache is not null) return _cache;
            _cache = BuildReferences();
            return _cache;
        }
    }

    private static IReadOnlyList<MetadataReference> BuildReferences()
    {
        var refs = new List<MetadataReference>();

        // Core .NET assemblies (loaded in current AppDomain)
        var types = new[]
        {
            typeof(object),                              // System.Runtime
            typeof(Console),                             // System.Console
            typeof(Enumerable),                          // System.Linq
            typeof(System.IO.File),                      // System.IO
            typeof(System.Text.StringBuilder),           // System.Text
            typeof(System.Threading.Tasks.Task),         // System.Threading.Tasks
            typeof(System.Collections.Generic.List<>),   // generic collections
            typeof(System.Text.Json.JsonSerializer),     // System.Text.Json
        };

        foreach (var t in types)
            AddIfLoaded(refs, t.Assembly);

        // WpfHexEditor.SDK — public contracts available to scripts
        AddIfLoaded(refs, typeof(WpfHexEditor.SDK.Contracts.Services.IHexEditorService).Assembly);

        // Deduplicate by location
        return refs
            .DistinctBy(r => (r as PortableExecutableReference)?.FilePath)
            .ToList();
    }

    private static void AddIfLoaded(List<MetadataReference> refs, Assembly asm)
    {
        if (string.IsNullOrEmpty(asm.Location)) return;
        refs.Add(MetadataReference.CreateFromFile(asm.Location));
    }
}
