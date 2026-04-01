// ==========================================================
// Project: WpfHexEditor.Editor.Core
// File: Contracts/ISolutionLoader.cs
// Author: Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.6
// Created: 2026-03-16
// Description:
//     Contract for pluggable solution file loaders.
//     Implementations: SolutionLoader.VS (.sln/.csproj) and
//     SolutionLoader.WH (.whsln/.whproj).
// ==========================================================

namespace WpfHexEditor.Editor.Core;

/// <summary>
/// Loads a solution file and converts it to an <see cref="ISolution"/> in-memory model.
/// Register implementations via the IDE's loader registry.
/// </summary>
public interface ISolutionLoader
{
    /// <summary>Human-readable name of this loader (e.g. <c>"Visual Studio"</c>).</summary>
    string LoaderName { get; }

    /// <summary>
    /// File extensions this loader handles, without the leading dot
    /// (e.g. <c>["sln", "csproj", "vbproj"]</c>).
    /// Used to build the Open-Solution-or-Project file-dialog filter dynamically.
    /// </summary>
    IReadOnlyList<string> SupportedExtensions { get; }

    /// <summary>
    /// Returns <c>true</c> if this loader can load the file at <paramref name="filePath"/>.
    /// Called before <see cref="LoadAsync"/> — should be fast (check extension only).
    /// </summary>
    bool CanLoad(string filePath);

    /// <summary>
    /// Loads the solution at <paramref name="filePath"/> and returns an <see cref="ISolution"/>.
    /// </summary>
    Task<ISolution> LoadAsync(string filePath, CancellationToken ct = default);
}
