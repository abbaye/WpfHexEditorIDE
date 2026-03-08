// ==========================================================
// Project: WpfHexEditor.Plugins.AssemblyExplorer
// File: Services/DecompilerService.cs
// Author: Derek Tremblay
// Created: 2026-03-08
// Description:
//     Stub decompiler service that returns placeholder C# text.
//     Full decompilation via ILSpy engine (IDecompiler from
//     WpfHexEditor.Decompiler.Core) is planned for a future phase.
//     No ProjectReference to Decompiler.Core is added in this stub.
//
// Architecture Notes:
//     Pattern: Strategy stub — the interface expected by future phases
//     is expressed through method signatures so the ViewModel layer
//     can call this service without change when a real engine is plugged in.
// ==========================================================

using WpfHexEditor.Plugins.AssemblyExplorer.Models;

namespace WpfHexEditor.Plugins.AssemblyExplorer.Services;

/// <summary>
/// Returns placeholder decompiled text for assembly nodes.
/// All methods are synchronous stubs — a real decompiler backend
/// will replace these with async calls to an ILSpy/dnSpy engine.
/// </summary>
public sealed class DecompilerService
{
    private const string NotLoaded =
        "// Decompiler not yet loaded.\n// Full decompilation is planned for a future release.";

    // ── Public API ────────────────────────────────────────────────────────────

    /// <summary>Returns stub C# text for an assembly root node.</summary>
    public string DecompileAssembly(AssemblyModel assembly)
        => $"// Assembly: {assembly.Name} v{assembly.Version}\n"
         + $"// File: {assembly.FilePath}\n"
         + (assembly.IsManaged
               ? $"// Types: {assembly.Types.Count}  |  References: {assembly.References.Count}\n\n{NotLoaded}"
               : "// Native PE — no managed metadata.\n\n" + NotLoaded);

    /// <summary>Returns stub C# text for a type node.</summary>
    public string DecompileType(TypeModel type)
        => $"// Type: {type.FullName}\n"
         + $"// Token: 0x{type.MetadataToken:X8}\n\n"
         + NotLoaded;

    /// <summary>Returns stub C# text for a method node.</summary>
    public string DecompileMethod(MemberModel method)
        => $"// Method: {method.Name}\n"
         + $"// Token: 0x{method.MetadataToken:X8}\n\n"
         + NotLoaded;

    /// <summary>Returns stub text for any other node kind.</summary>
    public string GetStubText(string nodeDisplayName)
        => $"// {nodeDisplayName}\n\n{NotLoaded}";
}
