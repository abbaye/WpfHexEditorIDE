// ==========================================================
// Project: WpfHexEditor.Plugins.AssemblyExplorer
// File: Services/IDecompilerBackend.cs
// Author: Derek Tremblay
// Created: 2026-03-16
// Description:
//     Abstraction for the decompiler backend used by the Assembly Explorer.
//     Allows swapping the BCL-only skeleton emitter for a richer implementation
//     (e.g. ICSharpCode.Decompiler / ILSpy) without changing the consuming ViewModel.
//
// Architecture Notes:
//     Pattern: Strategy — multiple implementations behind a common interface.
//     Implementations: SkeletonDecompilerBackend (always available, BCL-only),
//                       IlSpyDecompilerBackend (optional NuGet, richer C# output).
// ==========================================================

using WpfHexEditor.Core.AssemblyAnalysis.Models;

namespace WpfHexEditor.Plugins.AssemblyExplorer.Services;

/// <summary>
/// Contract for a decompiler backend used by the Assembly Explorer detail pane
/// and "Open in Code Editor" feature.
/// </summary>
public interface IDecompilerBackend
{
    /// <summary>Human-readable name displayed in the Options page backend selector.</summary>
    string Name { get; }

    /// <summary>
    /// True when this backend always produces C# regardless of <see cref="DecompilerOptions.TargetLanguageId"/>.
    /// When true, <c>AssemblyDetailViewModel</c> applies a post-decompile language transform via
    /// <c>IDecompilationLanguage.TransformFromCSharpAsync</c> for non-CSharp target languages.
    /// False when the backend handles language routing internally (e.g. <see cref="SkeletonDecompilerBackend"/>).
    /// </summary>
    bool OutputIsCSharpOnly { get; }

    /// <summary>
    /// True when this backend is usable in the current environment
    /// (e.g. required NuGet package is present / loaded successfully).
    /// </summary>
    bool IsAvailable { get; }

    /// <summary>
    /// Active decompiler options. Set after construction to propagate changes from
    /// the Options page without rebuilding the backend instance.
    /// Implementations must not cache options across calls — read fresh per call.
    /// </summary>
    DecompilerOptions Options { get; set; }

    /// <summary>Returns C# text representing the assembly-level overview.</summary>
    string DecompileAssembly(AssemblyModel model, string filePath);

    /// <summary>Returns C# text for the given type definition.</summary>
    string DecompileType(TypeModel type, string filePath);

    /// <summary>Returns C# text for a single method, field, property, or event.</summary>
    string DecompileMethod(MemberModel member, string filePath);

    /// <summary>
    /// Returns raw IL disassembly text for a method member, or an empty string
    /// for non-method members / members with no IL body.
    /// </summary>
    string GetIlText(MemberModel member, string filePath);
}
