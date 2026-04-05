// GNU Affero General Public License v3.0 - 2026
// Contributors: Claude Sonnet 4.6

using System.Windows;

namespace WpfHexEditor.SDK.Contracts.Services;

/// <summary>
/// Factory that creates and drives shared UI controls from the App layer.
/// Plugins never reference WpfHexEditor.App directly — all interaction goes through this interface.
/// </summary>
public interface IUIControlFactory
{
    /// <summary>
    /// Creates a syntax-highlighted source code preview control.
    /// Use <see cref="SetPreviewSource"/> to feed it with file/line data.
    /// </summary>
    FrameworkElement CreateSyntaxPreview(int contextLines = 2, double fontSize = 12, bool highlightFocusLine = true);

    /// <summary>
    /// Feeds pre-extracted source text into a preview control created by <see cref="CreateSyntaxPreview"/>.
    /// The caller is responsible for reading the file and detecting the language —
    /// the control itself performs no I/O.
    /// </summary>
    /// <param name="preview">The element returned by <see cref="CreateSyntaxPreview"/>.</param>
    /// <param name="sourceText">Source lines to display (pre-extracted, \n-separated).</param>
    /// <param name="languageId">Language identifier for syntax coloring (e.g. "csharp").</param>
    /// <param name="focusLine">0-based index of the line to highlight within <paramref name="sourceText"/>.</param>
    void SetPreviewSource(FrameworkElement preview, string? sourceText, string? languageId, int focusLine = -1);
}
