// ==========================================================
// Project: WpfHexEditor.Editor.Core
// File: AnalyzerReferenceInfo.cs
// Author: Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.6
// Created: 2026-03-16
// Description:
//     Immutable record representing a single <Analyzer Include="..."> entry from a .csproj.
//     Used by IProjectWithReferences to expose Roslyn analyzer DLLs to the UI layer
//     (displayed in the "Analyzers" sub-folder under References, mirroring Visual Studio).
//
// Architecture Notes:
//     Pattern: Immutable record (C# 9+) — value semantics, thread-safe by design.
// ==========================================================

namespace WpfHexEditor.Editor.Core;

/// <summary>
/// Represents a single <c>&lt;Analyzer Include="..."&gt;</c> reference in a project file.
/// </summary>
/// <param name="HintPath">Absolute path to the analyzer DLL.</param>
public record AnalyzerReferenceInfo(string HintPath);
