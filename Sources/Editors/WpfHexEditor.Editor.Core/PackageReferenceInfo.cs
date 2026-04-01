// ==========================================================
// Project: WpfHexEditor.Editor.Core
// File: PackageReferenceInfo.cs
// Author: Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.6
// Created: 2026-03-16
// Description:
//     Immutable record representing a single <PackageReference> entry from a .csproj file.
//     Replaces the previous plain string representation to carry version metadata.
//
// Architecture Notes:
//     Pattern: Immutable record (C# 9+) — value semantics, thread-safe by design.
// ==========================================================

namespace WpfHexEditor.Editor.Core;

/// <summary>
/// Represents a single <c>&lt;PackageReference Include="..." Version="..."&gt;</c> entry.
/// </summary>
/// <param name="Id">NuGet package identifier (e.g., <c>"Newtonsoft.Json"</c>).</param>
/// <param name="Version">
/// Version string (e.g., <c>"13.0.3"</c>), or <see langword="null"/> if not specified.
/// </param>
public record PackageReferenceInfo(
    string  Id,
    string? Version);
