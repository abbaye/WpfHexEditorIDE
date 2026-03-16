// ==========================================================
// Project: WpfHexEditor.Plugins.AssemblyExplorer
// File: Services/DecompilerOptions.cs
// Author: Derek Tremblay
// Created: 2026-03-16
// Description:
//     Immutable snapshot of decompiler settings at the moment a decompile call is made.
//     Decouples the backend from AssemblyExplorerOptions (avoids tight coupling to
//     the singleton and simplifies testing).
//
// Architecture Notes:
//     Pattern: Value Object (record) + Factory.
//     The static factory FromPluginOptions() is the only entry point used at runtime.
// ==========================================================

using WpfHexEditor.Plugins.AssemblyExplorer.Options;

namespace WpfHexEditor.Plugins.AssemblyExplorer.Services;

/// <summary>Quality levels for C# decompilation output.</summary>
public enum DecompilationQuality
{
    /// <summary>BCL-only skeleton emitter — fast, no method bodies.</summary>
    Skeleton,

    /// <summary>ICSharpCode.Decompiler with default settings.</summary>
    Normal,

    /// <summary>ICSharpCode.Decompiler with XML docs, optimised output.</summary>
    Full
}

/// <summary>
/// Immutable snapshot of decompiler settings used for a single decompile call.
/// </summary>
public sealed record DecompilerOptions(
    DecompilationQuality Quality,
    int                  CSharpLanguageVersion,
    bool                 ShowXmlDocs,
    bool                 ShowHiddenMembers)
{
    /// <summary>Builds a <see cref="DecompilerOptions"/> from the persisted plugin options.</summary>
    public static DecompilerOptions FromPluginOptions(AssemblyExplorerOptions opts) => new(
        Quality:              opts.DecompilationQuality,
        CSharpLanguageVersion: opts.CSharpLanguageVersion,
        ShowXmlDocs:          opts.ShowXmlDocs,
        ShowHiddenMembers:    opts.ShowHiddenMembers);

    /// <summary>Default options used when the plugin singleton is not yet loaded.</summary>
    public static DecompilerOptions Default { get; } = new(
        DecompilationQuality.Full, 1200, true, false);
}
