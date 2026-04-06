// Project: WpfHexEditor.Core.Options
// File: Preview/FormattingRuleTooltip.cs
// Created: 2026-04-04
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using WpfHexEditor.Core.ProjectSystem.Languages;
namespace WpfHexEditor.Core.Options.Preview;
internal sealed class FormattingRuleTooltip : UserControl
{
    private readonly string      _ruleId;
    private readonly RichTextBox _beforeBox;
    private readonly RichTextBox _afterBox;
    private readonly TextBlock   _noSampleLabel;
    public FormattingRuleTooltip(string ruleId, string ruleDisplayName)
    {
        _ruleId = ruleId;
        var root = new Border { Padding = new Thickness(10), CornerRadius = new CornerRadius(4), MinWidth = 340, BorderThickness = new Thickness(1) };
        root.SetResourceReference(Border.BackgroundProperty,  "DockPanelBackgroundBrush");
        root.SetResourceReference(Border.BorderBrushProperty, "DockBorderBrush");
        var stack = new StackPanel();
        var header = new TextBlock { Text = ruleDisplayName, FontWeight = FontWeights.SemiBold, FontSize = 12, Margin = new Thickness(0, 0, 0, 6) };
        header.SetResourceReference(TextBlock.ForegroundProperty, "DockMenuForegroundBrush");
        stack.Children.Add(header);
        var sep = new Separator { Margin = new Thickness(0, 0, 0, 8) };
        sep.SetResourceReference(Separator.BackgroundProperty, "DockBorderBrush");
        stack.Children.Add(sep);
        var grid = new Grid();
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(8) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        var lblBefore = MakeLabel("Before"); Grid.SetColumn(lblBefore, 0);
        var lblAfter  = MakeLabel("After");  Grid.SetColumn(lblAfter,  2);
        grid.Children.Add(lblBefore);
        grid.Children.Add(lblAfter);
        _beforeBox = MakeCodeBox(); Grid.SetRow(_beforeBox, 1); Grid.SetColumn(_beforeBox, 0);
        _afterBox  = MakeCodeBox(); Grid.SetRow(_afterBox,  1); Grid.SetColumn(_afterBox,  2);
        grid.Children.Add(_beforeBox);
        grid.Children.Add(_afterBox);
        stack.Children.Add(grid);
        _noSampleLabel = new TextBlock { Text = "No preview sample available for this language.", FontStyle = FontStyles.Italic, FontSize = 11, Opacity = 0.6, Visibility = Visibility.Collapsed };
        _noSampleLabel.SetResourceReference(TextBlock.ForegroundProperty, "DockMenuForegroundBrush");
        stack.Children.Add(_noSampleLabel);
        root.Child = stack;
        Content    = root;
        Background = Brushes.Transparent;
    }
    public void Refresh(LanguageDefinition? langDef, IPreviewColorizer colorizer)
    {
        if (langDef is null || !langDef.PreviewSamples.TryGetValue(_ruleId, out var sample))
        {
            _beforeBox.Visibility     = Visibility.Collapsed;
            _afterBox.Visibility      = Visibility.Collapsed;
            _noSampleLabel.Visibility = Visibility.Visible;
            return;
        }
        _beforeBox.Visibility     = Visibility.Visible;
        _afterBox.Visibility      = Visibility.Visible;
        _noSampleLabel.Visibility = Visibility.Collapsed;
        Render(_beforeBox, sample.Before, langDef.Id, colorizer);
        Render(_afterBox,  sample.After,  langDef.Id, colorizer);
    }
    private static TextBlock MakeLabel(string text)
    {
        var tb = new TextBlock { Text = text, FontSize = 10, FontWeight = FontWeights.Bold, Margin = new Thickness(0, 0, 0, 2) };
        tb.SetResourceReference(TextBlock.ForegroundProperty, "CP_SecondaryTextBrush");
        return tb;
    }
    private static RichTextBox MakeCodeBox()
    {
        var box = new RichTextBox
        {
            IsReadOnly                    = true,
            BorderThickness               = new Thickness(0),
            Padding                       = new Thickness(6, 4, 6, 4),
            MinHeight                     = 30,
            MinWidth                      = 150,
            FontFamily                    = new FontFamily("Consolas, Courier New"),
            FontSize                      = 12,
            VerticalScrollBarVisibility   = ScrollBarVisibility.Disabled,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
            IsDocumentEnabled             = false,
            Focusable                     = false,
            Background                    = Brushes.Transparent,
        };
        box.SetResourceReference(RichTextBox.ForegroundProperty, "DockMenuForegroundBrush");
        return box;
    }
    private static void Render(RichTextBox box, string code, string langId, IPreviewColorizer colorizer)
    {
        var doc = new FlowDocument { PagePadding = new Thickness(0), ColumnWidth = double.PositiveInfinity, PageWidth = 10_000 };
        doc.SetResourceReference(FlowDocument.ForegroundProperty, "DockMenuForegroundBrush");
        var rawLines = code.Split(new[]{"\n"}, StringSplitOptions.None);
        var spans    = colorizer.ColorizeLines(rawLines, langId);
        for (int i = 0; i < rawLines.Length; i++)
        {
            var para      = new Paragraph { Margin = new Thickness(0), LineHeight = 16 };
            var lineSpans = i < spans.Count ? spans[i] : (IReadOnlyList<PreviewSpan>)Array.Empty<PreviewSpan>();
            if (lineSpans.Count == 0) { para.Inlines.Add(new Run(rawLines[i])); }
            else { foreach (var s in lineSpans) { var r = new Run(s.Text){ Foreground=s.Foreground }; if(s.IsBold) r.FontWeight=FontWeights.Bold; if(s.IsItalic) r.FontStyle=FontStyles.Italic; para.Inlines.Add(r); } }
            doc.Blocks.Add(para);
        }
        box.Document = doc;
    }
}
