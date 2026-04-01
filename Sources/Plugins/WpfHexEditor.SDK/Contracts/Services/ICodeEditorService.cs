//////////////////////////////////////////////
// GNU Affero General Public License v3.0 - 2026
// Author : Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.6
//////////////////////////////////////////////

namespace WpfHexEditor.SDK.Contracts.Services;

/// <summary>
/// Provides access to the active CodeEditor for plugins.
/// Requires <c>AccessCodeEditor</c> permission.
/// </summary>
public interface ICodeEditorService
{
    /// <summary>Gets whether a CodeEditor tab is currently active.</summary>
    bool IsActive { get; }

    /// <summary>Gets the language identifier of the active document (e.g. "json", "csharp").</summary>
    string? CurrentLanguage { get; }

    /// <summary>Gets the file path of the active code document, or null.</summary>
    string? CurrentFilePath { get; }

    /// <summary>Gets the full text content of the active code document.</summary>
    string? GetContent();

    /// <summary>Gets the text of the current selection, or empty string if no selection.</summary>
    string GetSelectedText();

    /// <summary>Gets the current caret line (1-based).</summary>
    int CaretLine { get; }

    /// <summary>Gets the current caret column (1-based).</summary>
    int CaretColumn { get; }

    /// <summary>
    /// Raised when the active code document changes (new file opened, tab switched).
    /// Raised on the UI thread.
    /// </summary>
    event EventHandler DocumentChanged;
}
