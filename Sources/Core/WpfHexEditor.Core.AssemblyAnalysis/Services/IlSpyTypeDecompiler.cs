// ==========================================================
// Project: WpfHexEditor.Core.AssemblyAnalysis
// File: Services/IlSpyTypeDecompiler.cs
// Author: Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.6
// Created: 2026-03-23
// Description:
//     Thin stateless wrapper around ICSharpCode.Decompiler for single-type
//     decompilation.  Produces full C# source with method bodies — unlike the
//     reflection-based CSharpSkeletonEmitter which emits stub signatures only.
//
// Architecture Notes:
//     Pattern: Static service / Facade
//     - Thread-safe: CSharpDecompiler is created per-call (not shared state).
//     - ThrowOnAssemblyResolveErrors=false so BCL facades that redirect to
//       System.Private.CoreLib do not abort decompilation.
//     - Returns the full module + assembly attributes when qualifiedTypeName is
//       null or unresolvable (useful as a "show all" fallback).
// ==========================================================

using System.Threading;
using System.Threading.Tasks;
using ICSharpCode.Decompiler;
using ICSharpCode.Decompiler.CSharp;
using ICSharpCode.Decompiler.TypeSystem;

namespace WpfHexEditor.Core.AssemblyAnalysis.Services;

/// <summary>
/// Decompiles a managed assembly DLL to C# source using ILSpy/ICSharpCode.Decompiler.
/// </summary>
public static class IlSpyTypeDecompiler
{
    private static readonly DecompilerSettings s_settings = new()
    {
        ThrowOnAssemblyResolveErrors = false,
        ShowXmlDocumentation         = true,
    };

    /// <summary>
    /// Decompiles a single type from <paramref name="dllPath"/> to C# source.
    /// When <paramref name="qualifiedTypeName"/> is null or the type is not found
    /// the method falls back to decompiling the module + assembly attributes.
    /// </summary>
    /// <param name="dllPath">Absolute path to the managed DLL.</param>
    /// <param name="qualifiedTypeName">
    /// Fully-qualified CLR type name (e.g. <c>System.Collections.Concurrent.ConcurrentDictionary`2</c>).
    /// Pass null or empty to decompile the entire module overview.
    /// </param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Decompiled C# source as a string.</returns>
    public static Task<string> DecompileTypeAsync(
        string            dllPath,
        string?           qualifiedTypeName,
        CancellationToken ct = default)
        => Task.Run(() =>
        {
            ct.ThrowIfCancellationRequested();
            var dc = new CSharpDecompiler(dllPath, s_settings);

            if (!string.IsNullOrEmpty(qualifiedTypeName))
            {
                try
                {
                    return dc.DecompileTypeAsString(new FullTypeName(qualifiedTypeName));
                }
                catch
                {
                    // Type not found or generic arity mismatch — fall through to module overview.
                }
            }

            return dc.DecompileModuleAndAssemblyAttributesToString();
        }, ct);
}
