// ==========================================================
// Project: WpfHexEditor.Editor.ClassDiagram.Core
// File: CodeGen/Abstractions/LanguageIds.cs
// Author: Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Opus 4.7
// Created: 2026-05-08
// Description:
//     Canonical language identifiers used by ILanguageGenerator
//     implementations and by callers to resolve them through
//     CodeGenLanguageRegistry.
// ==========================================================

namespace WpfHexEditor.Editor.ClassDiagram.Core.CodeGen.Abstractions;

/// <summary>Canonical language identifiers for built-in code generators.</summary>
public static class LanguageIds
{
    /// <summary>Identifier for the C# generator.</summary>
    public const string CSharp = "csharp";

    /// <summary>Identifier for the Visual Basic generator (reserved for a future implementation).</summary>
    public const string VisualBasic = "vb";

    /// <summary>Identifier for the TypeScript generator (reserved for a future implementation).</summary>
    public const string TypeScript = "typescript";
}
