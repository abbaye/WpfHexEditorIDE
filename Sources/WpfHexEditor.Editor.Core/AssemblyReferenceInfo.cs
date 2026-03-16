// ==========================================================
// Project: WpfHexEditor.Editor.Core
// File: AssemblyReferenceInfo.cs
// Author: Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.6
// Created: 2026-03-16
// Description:
//     Immutable record representing a single <Reference> entry from a .csproj file.
//     Used by IProjectWithReferences to expose assembly references to the UI layer.
//
// Architecture Notes:
//     Pattern: Immutable record (C# 9+) — value semantics, thread-safe by design.
//     IsFrameworkRef is derived (HintPath == null): BCL/framework assemblies have no HintPath.
// ==========================================================

namespace WpfHexEditor.Editor.Core;

/// <summary>
/// Represents a single <c>&lt;Reference Include="..."&gt;</c> assembly reference in a project file.
/// </summary>
/// <param name="Name">
/// The simple assembly name (e.g., <c>"PresentationCore"</c>, <c>"Newtonsoft.Json"</c>).
/// The full identity string (version/culture/token) is stripped.
/// </param>
/// <param name="HintPath">
/// Absolute path to the referenced DLL, or <see langword="null"/> for BCL / framework assemblies
/// that resolve via the GAC or SDK reference assemblies.
/// </param>
/// <param name="Version">
/// Version string extracted from the <c>Include</c> attribute or a child <c>&lt;Version&gt;</c>
/// element, or <see langword="null"/> when not specified.
/// </param>
/// <param name="IsFrameworkRef">
/// <see langword="true"/> when <paramref name="HintPath"/> is <see langword="null"/>, meaning
/// this is a BCL / framework assembly that needs no explicit path.
/// </param>
public record AssemblyReferenceInfo(
    string  Name,
    string? HintPath,
    string? Version,
    bool    IsFrameworkRef);
