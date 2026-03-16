// ==========================================================
// Project: WpfHexEditor.Plugins.AssemblyExplorer
// File: Services/SkeletonDecompilerBackend.cs
// Author: Derek Tremblay
// Created: 2026-03-16
// Description:
//     BCL-only decompiler backend. Wraps CSharpSkeletonEmitter (structural C# skeletons)
//     and IlTextEmitter (raw IL disassembly). Always available — no NuGet required.
//     Method bodies are emitted as stubs: { /* IL body */ }
//
// Architecture Notes:
//     Pattern: Strategy (implements IDecompilerBackend).
//     This is the fallback used when no richer backend is configured or available.
// ==========================================================

using WpfHexEditor.Core.AssemblyAnalysis.Models;
using WpfHexEditor.Core.AssemblyAnalysis.Services;

namespace WpfHexEditor.Plugins.AssemblyExplorer.Services;

/// <summary>
/// BCL-only decompiler backend.
/// Uses <see cref="CSharpSkeletonEmitter"/> for structural C# output and
/// <see cref="IlTextEmitter"/> for IL disassembly — no external dependencies.
/// </summary>
public sealed class SkeletonDecompilerBackend : IDecompilerBackend
{
    private readonly DecompilerService _service;

    public SkeletonDecompilerBackend(DecompilerService service)
        => _service = service;

    public string Name        => "Skeleton (BCL-only)";
    public bool   IsAvailable => true;
    public DecompilerOptions Options { get; set; } = DecompilerOptions.Default;

    public string DecompileAssembly(AssemblyModel model, string filePath)
        => _service.DecompileAssembly(model);

    public string DecompileType(TypeModel type, string filePath)
        => _service.DecompileType(type);

    public string DecompileMethod(MemberModel member, string filePath)
        => _service.DecompileMethod(member);

    public string GetIlText(MemberModel member, string filePath)
        => _service.GetIlText(member, filePath);
}
