// GNU Affero General Public License v3.0 - 2026
// Contributors: Claude Sonnet 4.6

using System.Windows;
using WpfHexEditor.App.Controls;
using WpfHexEditor.SDK.Contracts.Services;

namespace WpfHexEditor.App.Services;

/// <summary>
/// App-layer implementation of <see cref="IUIControlFactory"/>.
/// Creates concrete UI controls and exposes them as <see cref="FrameworkElement"/>
/// so plugins never need to reference WpfHexEditor.App directly.
/// </summary>
internal sealed class UIControlFactory : IUIControlFactory
{
    private readonly ISyntaxColoringService? _coloringService;

    public UIControlFactory(ISyntaxColoringService? coloringService)
    {
        _coloringService = coloringService;
    }

    /// <inheritdoc />
    public FrameworkElement CreateSyntaxPreview(int contextLines = 2, double fontSize = 12, bool highlightFocusLine = true)
    {
        return new SyntaxColoredBlock
        {
            ColoringServiceInstance = _coloringService,
            ContextLines            = contextLines,
            FontSizeCode            = fontSize,
            HighlightBreakLine      = highlightFocusLine,
        };
    }

    /// <inheritdoc />
    public void SetPreviewSource(FrameworkElement preview, string? sourceText, string? languageId, int focusLine = -1)
    {
        if (preview is not SyntaxColoredBlock block) return;
        block.SourceText    = sourceText   ?? string.Empty;
        block.LanguageId    = languageId   ?? string.Empty;
        block.FocusLineIndex = focusLine;
    }
}
