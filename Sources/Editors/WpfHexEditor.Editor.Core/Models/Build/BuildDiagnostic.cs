// ==========================================================
// Project: WpfHexEditor.Editor.Core
// File: Models/Build/BuildDiagnostic.cs
// Author: Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.6
// Created: 2026-03-16
// Description:
//     A single compiler/build-tool diagnostic (error, warning, or message)
//     produced during a build operation. Consumed by BuildResult and the
//     ErrorList panel via the BuildErrorListAdapter.
// ==========================================================

namespace WpfHexEditor.Editor.Core;

/// <summary>A single diagnostic entry produced by the build system.</summary>
public sealed record BuildDiagnostic(
    /// <summary>Absolute path of the source file, or null for project-level errors.</summary>
    string?            FilePath,
    /// <summary>1-based line number, or null.</summary>
    int?               Line,
    /// <summary>1-based column number, or null.</summary>
    int?               Column,
    /// <summary>Compiler/MSBuild diagnostic code, e.g. <c>"CS0103"</c>.</summary>
    string             Code,
    /// <summary>Human-readable description.</summary>
    string             Message,
    /// <summary>Severity of this diagnostic.</summary>
    DiagnosticSeverity Severity,
    /// <summary>ID of the project that generated this diagnostic.</summary>
    string?            ProjectId  = null,
    /// <summary>Short project name for display.</summary>
    string?            ProjectName = null);
