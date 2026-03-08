// ==========================================================
// Project: WpfHexEditor.Plugins.AssemblyExplorer
// File: Services/DecompilerService.cs
// Author: Derek Tremblay
// Created: 2026-03-08
// License: GNU Affero General Public License v3.0 (AGPL-3.0)
// Description:
//     Decompiler service implementation using the BCL-only Core emitters:
//       - CSharpSkeletonEmitter -> "Code" tab (C# structural skeleton)
//     Full decompilation (control-flow reconstruction, expressions) is
//     outside the BCL-only scope; method bodies are left as stubs.
//
// Architecture Notes:
//     Pattern: Facade - wraps Core emitters behind a plugin-facing API.
// ==========================================================

using WpfHexEditor.Core.AssemblyAnalysis.Models;
using WpfHexEditor.Core.AssemblyAnalysis.Services;
using IAssemblyAnalysisEngine = WpfHexEditor.Core.AssemblyAnalysis.Services.IAssemblyAnalysisEngine;

namespace WpfHexEditor.Plugins.AssemblyExplorer.Services;

/// <summary>
/// Provides decompiled text for the detail pane tabs.
/// Uses only BCL-based Core emitters - no external NuGet dependencies.
/// </summary>
public sealed class DecompilerService
{
    private readonly IAssemblyAnalysisEngine _engine;
    private readonly CSharpSkeletonEmitter   _csharp = new();

    public DecompilerService(IAssemblyAnalysisEngine engine)
        => _engine = engine;

    // Public API

    /// <summary>Returns the C# skeleton for an assembly (AssemblyInfo.cs style).</summary>
    public string DecompileAssembly(AssemblyModel assembly)
        => _csharp.EmitAssemblyInfo(assembly);

    /// <summary>Returns the C# structural skeleton for a type.</summary>
    public string DecompileType(TypeModel type)
        => _csharp.EmitType(type);

    /// <summary>Returns the C# signature stub for a single member.</summary>
    public string DecompileMethod(MemberModel member)
        => _csharp.EmitMethod(member);

    /// <summary>Returns a placeholder for node kinds with no decompilation support.</summary>
    public string GetStubText(string nodeDisplayName)
        => $"// {nodeDisplayName}\n\n// No decompilation available for this node type.";
}
