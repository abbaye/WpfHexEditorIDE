// ==========================================================
// Project: WpfHexEditor.Editor.Core
// File: IProjectWithReferences.cs
// Author: Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.6
// Created: 2026-03-16
// Description:
//     Optional interface for IProject implementations that expose typed references
//     (project-to-project references and package/NuGet references) plus language metadata.
//     Currently implemented by VsProject (VS solution loader).
//     The ViewModel layer checks via 'is' cast — keeps IProject clean of VS-specific data.
//
// Architecture Notes:
//     Pattern: Interface segregation (ISP) — keeps IProject minimal; adds VS metadata opt-in.
// ==========================================================

namespace WpfHexEditor.Editor.Core;

/// <summary>
/// Optional interface for projects that expose typed references and language metadata.
/// Implemented by VS-loader projects; detected via <c>is</c> cast in the ViewModel layer.
/// </summary>
public interface IProjectWithReferences
{
    /// <summary>
    /// MSBuild output type: <c>"Exe"</c>, <c>"WinExe"</c>, <c>"Library"</c>, etc.
    /// Used to determine whether a project can be used as a startup project.
    /// </summary>
    string OutputType { get; }

    /// <summary>
    /// Programming language of the project.
    /// Typical values: <c>"C#"</c>, <c>"VB"</c>, <c>"F#"</c>.
    /// </summary>
    string Language { get; }

    /// <summary>
    /// Absolute paths to referenced <c>.csproj</c> / <c>.vbproj</c> files.
    /// </summary>
    IReadOnlyList<string> ProjectReferences { get; }

    /// <summary>
    /// NuGet / package references with optional version metadata.
    /// Populated from <c>&lt;PackageReference Include="..." Version="..."&gt;</c> elements.
    /// </summary>
    IReadOnlyList<PackageReferenceInfo> PackageReferences { get; }

    /// <summary>
    /// Assembly references from <c>&lt;Reference Include="..."&gt;</c> elements.
    /// Includes both BCL / framework assemblies (no HintPath) and explicit DLL refs (with HintPath).
    /// </summary>
    IReadOnlyList<AssemblyReferenceInfo> AssemblyReferences { get; }

    /// <summary>
    /// Roslyn analyzer DLLs from <c>&lt;Analyzer Include="..."&gt;</c> elements.
    /// Displayed in the "Analyzers" sub-folder under References, mirroring Visual Studio.
    /// </summary>
    IReadOnlyList<AnalyzerReferenceInfo> AnalyzerReferences { get; }
}
