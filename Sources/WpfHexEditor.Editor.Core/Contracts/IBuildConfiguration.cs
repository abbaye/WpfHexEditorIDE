// ==========================================================
// Project: WpfHexEditor.Editor.Core
// File: Contracts/IBuildConfiguration.cs
// Author: Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.6
// Created: 2026-03-16
// Description:
//     Represents a named build configuration (Debug, Release, or custom).
//     Used by IBuildSystem and ConfigurationManager.
// ==========================================================

namespace WpfHexEditor.Editor.Core;

/// <summary>
/// A named build configuration (Debug / Release / custom).
/// </summary>
public interface IBuildConfiguration
{
    /// <summary>Configuration name, e.g. <c>"Debug"</c> or <c>"Release"</c>.</summary>
    string Name { get; }

    /// <summary>Target platform, e.g. <c>"AnyCPU"</c>, <c>"x64"</c>, <c>"x86"</c>.</summary>
    string Platform { get; }

    /// <summary>Output directory path relative to the project root, e.g. <c>"bin\Debug\net8.0\"</c>.</summary>
    string OutputPath { get; }

    /// <summary>Whether to enable compiler optimizations.</summary>
    bool Optimize { get; }

    /// <summary>Semicolon-separated preprocessor symbol definitions, e.g. <c>"DEBUG;TRACE"</c>.</summary>
    string DefineConstants { get; }
}
