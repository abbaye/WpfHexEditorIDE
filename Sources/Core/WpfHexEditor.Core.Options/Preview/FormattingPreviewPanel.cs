// Project: WpfHexEditor.Core.Options
// File: Preview/FormattingPreviewPanel.cs
// Created: 2026-04-04
// Description: Live preview panel for the Formatting options page.
//   Shows a language-appropriate snippet, formatted with current settings,
//   colorised via IPreviewColorizer. No CodeEditor / SDK dependency.
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using WpfHexEditor.Core.ProjectSystem.Languages;
namespace WpfHexEditor.Core.Options.Preview;
/// <summary>
/// Preview panel: live colorised code snippet that reflects current formatting options.
/// Embed in a two-column layout on the Formatting options page.
/// </summary>
public sealed class FormattingPreviewPanel : UserControl
{
    // ── Fields ────────────────────────────────────────────────────────────
    private readonly RichTextBox  _codeBox;
    private readonly TextBlock    _noPreviewLabel;
    private IPreviewColorizer?    _colorizer;
    private IPreviewFormatter?    _formatter;
    private LanguageDefinition?   _currentLang;
    private FormattingOverrides?  _overrides;
    // ── Construction ──────────────────────────────────────────────────────
    public FormattingPreviewPanel()
    {
        var root = new Border
        {
            CornerRadius    = new CornerRadius(4),
            BorderThickness = new Thickness(1),
            Padding         = new Thickness(0),
        };
        root.SetResourceReference(Border.BackgroundProperty,  "TE_Background");
        root.SetResourceReference(Border.BorderBrushProperty, "DockBorderBrush");
        var outerStack = new DockPanel { LastChildFill = true };
        // ── Toolbar (label only — language selection is owned by the page) ─────────
        var toolbar = new Border
        {
            Padding         = new Thickness(6, 4, 6, 4),
            BorderThickness = new Thickness(0, 0, 0, 1),
        };
        toolbar.SetResourceReference(Border.BackgroundProperty,  "DockPanelBackgroundBrush");
        toolbar.SetResourceReference(Border.BorderBrushProperty, "DockBorderBrush");
        DockPanel.SetDock(toolbar, Dock.Top);
        var lbl = new TextBlock { Text = "Preview", FontWeight = FontWeights.SemiBold, FontSize = 11, VerticalAlignment = VerticalAlignment.Center };
        lbl.SetResourceReference(TextBlock.ForegroundProperty, "DockMenuForegroundBrush");
        toolbar.Child = lbl;
        outerStack.Children.Add(toolbar);
        // ── Code box ─────────────────────────────────────────────────────
        _codeBox = new RichTextBox
        {
            IsReadOnly                    = true,
            BorderThickness               = new Thickness(0),
            Padding                       = new Thickness(8),
            FontFamily                    = new FontFamily("Consolas, Courier New"),
            FontSize                      = 12,
            VerticalScrollBarVisibility   = ScrollBarVisibility.Auto,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Auto,
            IsDocumentEnabled             = false,
            Focusable                     = false,
            Background                    = Brushes.Transparent,
        };
        _codeBox.SetResourceReference(RichTextBox.ForegroundProperty, "DockMenuForegroundBrush");
        _noPreviewLabel = new TextBlock
        {
            Text              = "No preview available for this language.",
            FontStyle         = FontStyles.Italic,
            FontSize          = 11,
            Opacity           = 0.5,
            Margin            = new Thickness(8),
            VerticalAlignment = VerticalAlignment.Center,
            Visibility        = Visibility.Collapsed,
        };
        _noPreviewLabel.SetResourceReference(TextBlock.ForegroundProperty, "DockMenuForegroundBrush");
        var codeContainer = new Grid();
        codeContainer.Children.Add(_codeBox);
        codeContainer.Children.Add(_noPreviewLabel);
        outerStack.Children.Add(codeContainer);
        root.Child = outerStack;
        Content    = root;
    }
    // ── Public API ────────────────────────────────────────────────────────
    /// <summary>
    /// Initialises the panel with available languages and the colorizer.
    /// Call once from the options page after Load().
    /// </summary>
    public void Initialize(IPreviewColorizer colorizer, IPreviewFormatter? formatter = null)
    {
        _colorizer = colorizer;
        _formatter = formatter;
    }
    /// <summary>
    /// Refreshes the preview to reflect current checkbox state.
    /// Call from each checkbox Changed handler on the options page.
    /// </summary>
    public void Refresh(FormattingOverrides? overrides = null)
    {
        _overrides = overrides;
        RenderPreview();
    }

    /// <summary>
    /// Returns the ID of the currently selected language, or null if none is selected.
    /// Used by the tooltip wiring to refresh Before/After samples for the right language.
    /// </summary>
    public string? SelectedLanguageId => _currentLang?.Id;

    /// <summary>
    /// Switches the active preview language without re-rendering immediately.
    /// Call <see cref="Refresh"/> afterwards to update the view.
    /// </summary>
    public void SelectLanguage(LanguageDefinition lang) => _currentLang = lang;

    // ── Private ───────────────────────────────────────────────────────────
    private void RenderPreview()
    {
        if (_colorizer is null || _currentLang is null)
            return;
        var snippet = _currentLang.PreviewSnippet;
        if (string.IsNullOrWhiteSpace(snippet))
        {
            // Fallback: concatenate all Before samples
            snippet = string.Join("\n", _currentLang.PreviewSamples.Values.Select(s => s.Before));
        }
        if (string.IsNullOrWhiteSpace(snippet))
        {
            _codeBox.Visibility        = Visibility.Collapsed;
            _noPreviewLabel.Visibility = Visibility.Visible;
            return;
        }
        _codeBox.Visibility        = Visibility.Visible;
        _noPreviewLabel.Visibility = Visibility.Collapsed;
        // Apply the structural formatter so the preview reflects the language rules + any user overrides.
        if (_formatter is not null)
        {
            var rules = _currentLang.FormattingRules;
            if (_overrides is not null)
                rules = rules?.WithOverrides(_overrides) ?? new FormattingRules();
            snippet = _formatter.Format(snippet, rules);
        }
        var rawLines = snippet.Split(new[]{"\n"}, StringSplitOptions.None);
        var spans    = _colorizer.ColorizeLines(rawLines, _currentLang.Id);
        var doc = new FlowDocument { PagePadding = new Thickness(0) };
        doc.SetResourceReference(FlowDocument.ForegroundProperty, "DockMenuForegroundBrush");
        for (int i = 0; i < rawLines.Length; i++)
        {
            var para      = new Paragraph { Margin = new Thickness(0), LineHeight = 16 };
            var lineSpans = i < spans.Count ? spans[i] : (IReadOnlyList<PreviewSpan>)Array.Empty<PreviewSpan>();
            if (lineSpans.Count == 0) { para.Inlines.Add(new Run(rawLines[i])); }
            else { foreach (var s in lineSpans) { var r = new Run(s.Text){ Foreground=s.Foreground }; if(s.IsBold) r.FontWeight=FontWeights.Bold; if(s.IsItalic) r.FontStyle=FontStyles.Italic; para.Inlines.Add(r); } }
            doc.Blocks.Add(para);
        }
        _codeBox.Document = doc;
    }
}
