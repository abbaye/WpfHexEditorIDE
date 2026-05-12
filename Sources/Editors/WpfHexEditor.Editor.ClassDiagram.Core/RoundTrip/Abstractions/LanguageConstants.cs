// ==========================================================
// Project: WpfHexEditor.Editor.ClassDiagram.Core
// File: RoundTrip/Abstractions/LanguageConstants.cs
// Author: Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Opus 4.7
// Created: 2026-05-11
// Description:
//     Canonical identifiers for built-in round-trip languages and
//     their file extensions. Lifted in /simplify pass after Phase A
//     to stop sprinkling raw "csharp"/"vb"/".cs"/".vb" literals.
// ==========================================================

namespace WpfHexEditor.Editor.ClassDiagram.Core.RoundTrip.Abstractions;

/// <summary>Built-in <see cref="ILanguageRoundTripEditor.LanguageId"/> values.</summary>
public static class LanguageIds
{
    public const string CSharp      = "csharp";
    public const string VisualBasic = "vb";
}

/// <summary>Built-in source file extensions paired with <see cref="LanguageIds"/>.</summary>
public static class LanguageFileExtensions
{
    public const string CSharp      = ".cs";
    public const string VisualBasic = ".vb";
}
