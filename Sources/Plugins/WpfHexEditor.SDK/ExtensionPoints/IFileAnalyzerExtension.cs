// ==========================================================
// Project: WpfHexEditor.SDK
// File: ExtensionPoints/IFileAnalyzerExtension.cs
// Created: 2026-03-15
// Description:
//     Extension point contract for plugins that analyze opened files.
//     IDE invokes all contributors on file open via IExtensionRegistry.GetExtensions<T>().
//
// Architecture Notes:
//     Pattern: Extension Points — IDE iterates contributors without knowing plugin identities.
//     FileAnalysisResult is a pure data record (no WPF dependency).
// ==========================================================

namespace WpfHexEditor.SDK.ExtensionPoints;

/// <summary>
/// Extension point contract: file analysis.
/// Plugins implementing this are invoked when any file is opened in the IDE.
/// Register in manifest: <c>"extensions": { "FileAnalyzer": "MyPlugin.MyAnalyzerClass" }</c>
/// </summary>
public interface IFileAnalyzerExtension
{
    /// <summary>Unique name for this analyzer shown in output (e.g. "PE Analyzer").</summary>
    string AnalyzerName { get; }

    /// <summary>
    /// Analyzes the file at <paramref name="filePath"/>.
    /// Returns null when the file is not supported by this analyzer.
    /// </summary>
    Task<FileAnalysisResult?> AnalyzeAsync(string filePath, CancellationToken ct = default);
}

/// <summary>Result produced by a <see cref="IFileAnalyzerExtension"/> contributor.</summary>
public sealed record FileAnalysisResult(
    string AnalyzerName,
    string Summary,
    IReadOnlyList<FileAnalysisEntry> Entries);

/// <summary>A single key/value entry in a file analysis result.</summary>
public sealed record FileAnalysisEntry(string Key, string Value);
