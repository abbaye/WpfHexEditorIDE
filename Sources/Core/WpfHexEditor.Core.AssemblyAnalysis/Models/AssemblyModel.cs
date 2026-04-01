// ==========================================================
// Project: WpfHexEditor.Core.AssemblyAnalysis
// File: Models/AssemblyModel.cs
// Author: Derek Tremblay
// Created: 2026-03-08
// License: GNU Affero General Public License v3.0 (AGPL-3.0)
// Description:
//     Immutable root model representing a loaded PE / .NET assembly.
//     Produced by IAssemblyAnalysisEngine.AnalyzeAsync and consumed
//     by the AssemblyExplorer plugin to build the tree.
//
// Architecture Notes:
//     Pattern: Immutable data model (init-only properties).
//     Managed assemblies carry full type/member trees; native PE stubs
//     carry only section information with IsManaged = false.
//     Moved from plugin to Core so other consumers (tests, other plugins)
//     can reference the model without referencing the WPF plugin.
// ==========================================================

namespace WpfHexEditor.Core.AssemblyAnalysis.Models;

/// <summary>
/// Root model for a parsed PE file (.NET managed or native).
/// All lists are empty (not null) for safe enumeration.
/// </summary>
public sealed class AssemblyModel
{
    /// <summary>Simple assembly name (without extension or culture token).</summary>
    public string Name { get; init; } = string.Empty;

    /// <summary>Absolute path to the PE file on disk.</summary>
    public string FilePath { get; init; } = string.Empty;

    /// <summary>Assembly version from the AssemblyDef metadata row, or null for native PE.</summary>
    public Version? Version { get; init; }

    /// <summary>Culture string from AssemblyDef, or null if neutral / native PE.</summary>
    public string? Culture { get; init; }

    /// <summary>Hex-encoded public key token, or null if unsigned / native PE.</summary>
    public string? PublicKeyToken { get; init; }

    /// <summary>True when the PE contains a .NET metadata section (#~ stream).</summary>
    public bool IsManaged { get; init; }

    /// <summary>
    /// Target framework moniker detected from TargetFrameworkAttribute, e.g. ".NET 8.0",
    /// ".NET Framework 4.8", ".NET Standard 2.0". Null if not detectable.
    /// </summary>
    public string? TargetFramework { get; init; }

    /// <summary>All type definitions grouped by namespace. Empty for native PE.</summary>
    public IReadOnlyList<TypeModel> Types { get; init; } = [];

    /// <summary>Assembly references declared in AssemblyRef metadata table.</summary>
    public IReadOnlyList<AssemblyRef> References { get; init; } = [];

    /// <summary>Managed resources declared in the ManifestResource table.</summary>
    public IReadOnlyList<ResourceEntry> Resources { get; init; } = [];

    /// <summary>Module entries from the Module and ModuleRef tables.</summary>
    public IReadOnlyList<ModuleEntry> Modules { get; init; } = [];

    /// <summary>PE section headers; populated for both managed and native PE.</summary>
    public IReadOnlyList<PeSectionEntry> Sections { get; init; } = [];

    /// <summary>
    /// Type forwarders declared in the ExportedType table (facade / forwarding assemblies).
    /// Empty for assemblies that define their own types and for native PE stubs.
    /// </summary>
    public IReadOnlyList<TypeForwarderEntry> TypeForwarders { get; init; } = [];
}

/// <summary>A single AssemblyRef row from the metadata table.</summary>
public sealed record AssemblyRef(
    string   Name,
    Version? Version,
    string?  PublicKeyToken);

/// <summary>A ManifestResource entry (name + raw PE offset + byte length).</summary>
public sealed record ResourceEntry(
    string Name,
    long   Offset,
    int    Length);

/// <summary>A Module or ModuleRef row entry.</summary>
public sealed record ModuleEntry(
    string Name,
    Guid   Mvid);

/// <summary>A PE section header (works for both managed and native PE).</summary>
public sealed record PeSectionEntry(
    string Name,
    long   VirtualAddress,
    int    VirtualSize,
    long   RawOffset,
    int    RawSize);

/// <summary>
/// A type forwarder entry from the ExportedType metadata table.
/// Present in facade / redirection assemblies (e.g. Microsoft.Win32.Primitives,
/// Microsoft.VisualBasic) that forward their public API to another assembly.
/// </summary>
public sealed record TypeForwarderEntry(
    string Namespace,
    string Name)
{
    /// <summary>Fully qualified type name (Namespace.Name, or just Name when namespace is empty).</summary>
    public string FullName => string.IsNullOrEmpty(Namespace) ? Name : $"{Namespace}.{Name}";
}
