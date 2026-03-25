// ==========================================================
// Project: WpfHexEditor.Plugins.AssemblyExplorer
// File: Services/DecompilerService.cs
// Author: Derek Tremblay
// Created: 2026-03-08
// License: GNU Affero General Public License v3.0 (AGPL-3.0)
// Description:
//     Decompiler service implementation using the BCL-only Core emitters:
//       - CSharpSkeletonEmitter -> "Code" tab (C# structural skeleton)
//       - IlTextEmitter         -> "IL"   tab (raw IL disassembly, methods only)
//     Full decompilation (control-flow reconstruction, expressions) is
//     outside the BCL-only scope; method bodies are left as stubs.
//
// Architecture Notes:
//     Pattern: Facade - wraps Core emitters behind a plugin-facing API.
//     GetIlText opens a PEReader on-demand (file path required) because
//     IlTextEmitter needs a live PEReader to read the IL body bytes.
//     The PEReader is disposed after each call - no long-lived handles.
// ==========================================================

using System.IO;
using System.Reflection.Metadata;
using System.Reflection.Metadata.Ecma335;
using System.Reflection.PortableExecutable;
using WpfHexEditor.Core.AssemblyAnalysis.Models;
using WpfHexEditor.Core.AssemblyAnalysis.Services;
using IAssemblyAnalysisEngine = WpfHexEditor.Core.AssemblyAnalysis.Services.IAssemblyAnalysisEngine;

namespace WpfHexEditor.Plugins.AssemblyExplorer.Services;

/// <summary>
/// Provides decompiled text for the detail pane tabs.
/// Uses only BCL-based Core emitters — no external NuGet dependencies.
/// </summary>
public sealed class DecompilerService
{
    private readonly IAssemblyAnalysisEngine _engine;
    private readonly CSharpSkeletonEmitter   _csharp = new();
    private readonly VbNetSkeletonEmitter    _vbnet  = new();
    private readonly IlTextEmitter           _il     = new();

    public DecompilerService(IAssemblyAnalysisEngine engine)
        => _engine = engine;

    // ── Code tab ──────────────────────────────────────────────────────────────

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

    // ── VB.NET skeleton tab (BCL-only) ────────────────────────────────────────

    /// <summary>Returns the VB.NET skeleton for an assembly (AssemblyInfo.vb style).</summary>
    public string DecompileAssemblyVB(AssemblyModel assembly)
        => _vbnet.EmitAssemblyInfo(assembly);

    /// <summary>Returns the VB.NET structural skeleton for a type.</summary>
    public string DecompileTypeVB(TypeModel type)
        => _vbnet.EmitType(type);

    /// <summary>Returns the VB.NET signature stub for a single member.</summary>
    public string DecompileMethodVB(MemberModel member)
        => _vbnet.EmitMethod(member);

    // ── IL tab ────────────────────────────────────────────────────────────────

    /// <summary>
    /// Opens the PE file on demand and returns the IL disassembly for the given method member.
    /// Returns an empty string for non-method members or when no body is present.
    /// Returns an error comment when the file cannot be read.
    /// </summary>
    public string GetIlText(MemberModel member, string filePath)
    {
        if (member.Kind != MemberKind.Method || member.MetadataToken == 0)
            return string.Empty;

        if (string.IsNullOrEmpty(filePath) || !File.Exists(filePath))
            return "// PE file not available — cannot read IL.";

        try
        {
            // FileShare.ReadWrite allows concurrent access when HexEditor holds the file open.
            using var stream = new FileStream(
                filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            using var pe = new PEReader(stream);

            var mdReader  = pe.GetMetadataReader();
            var handle    = (MethodDefinitionHandle)MetadataTokens.EntityHandle(member.MetadataToken);
            var methodDef = mdReader.GetMethodDefinition(handle);

            var ilText = _il.EmitMethod(methodDef, mdReader, pe);
            // RVA=0 has multiple legitimate causes: abstract/extern/interface, CLR-
            // generated delegate stubs (Invoke/BeginInvoke/EndInvoke), or reference
            // assemblies that ship signatures only (no implementation).
            return string.IsNullOrEmpty(ilText) ? string.Empty : ilText;
        }
        catch (Exception ex)
        {
            return $"// Error reading IL: {ex.Message}";
        }
    }
}
