// ==========================================================
// Project: WpfHexEditor.Plugins.AssemblyExplorer
// File: Services/IlSpyDecompilerBackend.cs
// Author: Derek Tremblay
// Created: 2026-03-16
// Updated: 2026-03-16 — Phase 1: replaced fragile reflection loading with a
//                        hard NuGet dependency on ICSharpCode.Decompiler 9.x.
//                        DecompileMethod now uses EntityHandle (correct API).
// Description:
//     Full C# decompiler backend powered by ICSharpCode.Decompiler (ILSpy engine).
//     Produces real method bodies, async/await, LINQ, lambdas, and pattern matching.
//     Falls back to SkeletonDecompilerBackend on any error.
//
// Architecture Notes:
//     Pattern: Strategy — implements IDecompilerBackend.
//     ICSharpCode.Decompiler is a hard NuGet dependency (Version 9.*).
//     The BCL-only SkeletonDecompilerBackend remains as an always-available fallback.
//     DecompilerSettings are built fresh per call (stateless) to avoid cross-call
//     state leakage. CSharpDecompiler is cheap to construct (PE loaded in memory).
// ==========================================================

using ICSharpCode.Decompiler;
using ICSharpCode.Decompiler.CSharp;
using ICSharpCode.Decompiler.TypeSystem;
using System.Runtime.CompilerServices;
using SrmEntityHandle = System.Reflection.Metadata.EntityHandle;
using WpfHexEditor.Core.AssemblyAnalysis.Models;

namespace WpfHexEditor.Plugins.AssemblyExplorer.Services;

/// <summary>
/// Full C# decompiler backend using ICSharpCode.Decompiler (the ILSpy engine).
/// Produces real method bodies with async/await, LINQ, lambdas, and pattern matching.
/// Always available — hard NuGet dependency.
/// </summary>
public sealed class IlSpyDecompilerBackend : IDecompilerBackend
{
    private readonly SkeletonDecompilerBackend _fallback;

    public IlSpyDecompilerBackend(SkeletonDecompilerBackend fallback)
        => _fallback = fallback;

    public string Name        => "ILSpy (ICSharpCode.Decompiler)";
    public bool   IsAvailable => true;
    public DecompilerOptions Options { get; set; } = DecompilerOptions.Default;

    // ── IDecompilerBackend ────────────────────────────────────────────────────

    public string DecompileAssembly(AssemblyModel model, string filePath)
    {
        if (string.IsNullOrEmpty(filePath)) return _fallback.DecompileAssembly(model, filePath);
        try
        {
            var decompiler = CreateDecompiler(filePath);
            return decompiler.DecompileModuleAndAssemblyAttributesToString();
        }
        catch
        {
            return _fallback.DecompileAssembly(model, filePath);
        }
    }

    public string DecompileType(TypeModel type, string filePath)
    {
        if (string.IsNullOrEmpty(filePath)) return _fallback.DecompileType(type, filePath);
        try
        {
            var decompiler = CreateDecompiler(filePath);
            return decompiler.DecompileTypeAsString(new FullTypeName(type.FullName));
        }
        catch
        {
            return _fallback.DecompileType(type, filePath)
                   + "\n\n// ILSpy decompilation failed — showing skeleton.";
        }
    }

    public string DecompileMethod(MemberModel member, string filePath)
    {
        if (string.IsNullOrEmpty(filePath)) return _fallback.DecompileMethod(member, filePath);
        try
        {
            // Raw metadata token (int) has the same binary layout as EntityHandle (uint vToken).
            var rawToken = member.MetadataToken;
            var handle   = Unsafe.BitCast<int, SrmEntityHandle>(rawToken);
            var decompiler = CreateDecompiler(filePath);
            return decompiler.DecompileAsString(handle);
        }
        catch
        {
            return _fallback.DecompileMethod(member, filePath)
                   + "\n\n// ILSpy decompilation failed — showing skeleton.";
        }
    }

    public string GetIlText(MemberModel member, string filePath)
        => _fallback.GetIlText(member, filePath); // IL disassembly keeps using BCL IlTextEmitter.

    // ── Private helpers ───────────────────────────────────────────────────────

    private CSharpDecompiler CreateDecompiler(string filePath)
        => new(filePath, BuildSettings(Options));

    private static DecompilerSettings BuildSettings(DecompilerOptions opts) => new()
    {
        ThrowOnAssemblyResolveErrors            = false,
        RemoveDeadCode                          = false,
        LoadInMemory                            = true,
        ShowXmlDocumentation                    = opts.ShowXmlDocs,
        // Keep output readable: avoid aggressive optimisations that obscure intent.
        AggressiveScalarReplacementOfAggregates = false,
    };
}
