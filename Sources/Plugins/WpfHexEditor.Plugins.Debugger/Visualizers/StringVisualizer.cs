// ==========================================================
// Project: WpfHexEditor.Plugins.Debugger
// File: Visualizers/StringVisualizer.cs
// Description: Debug visualizer for string values — shows full text with
//              word-wrap, character count, and escape-character highlighting.
// Architecture: IDebugVisualizer implementation.
// ==========================================================

using System.Windows;
using System.Windows.Controls;
using WpfHexEditor.SDK.Contracts;

namespace WpfHexEditor.Plugins.Debugger.Visualizers;

internal sealed class StringVisualizer : IDebugVisualizer
{
    public string DisplayName => "String Visualizer";

    public bool CanVisualize(DebugVariableContext context)
        => string.Equals(context.TypeName, "string", StringComparison.OrdinalIgnoreCase)
        || string.Equals(context.TypeName, "System.String", StringComparison.Ordinal);

    public FrameworkElement CreateView(DebugVariableContext context)
    {
        var raw = context.RawValue.Trim().Trim('"');

        var stack = new StackPanel { Margin = new Thickness(8) };

        var lengthText = new TextBlock
        {
            Text      = $"Length: {raw.Length} characters",
            FontSize  = 11,
            Margin    = new Thickness(0, 0, 0, 6),
        };
        lengthText.SetResourceReference(TextBlock.ForegroundProperty, "DockMenuForegroundBrush");

        var textBox = new TextBox
        {
            Text             = raw,
            IsReadOnly       = true,
            TextWrapping     = TextWrapping.Wrap,
            AcceptsReturn    = true,
            FontFamily       = new System.Windows.Media.FontFamily("Consolas"),
            FontSize         = 12,
            MinHeight        = 80,
            MaxHeight        = 300,
            VerticalScrollBarVisibility   = ScrollBarVisibility.Auto,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
        };
        textBox.SetResourceReference(TextBox.BackgroundProperty,   "DockMenuBackgroundBrush");
        textBox.SetResourceReference(TextBox.ForegroundProperty,   "DockMenuForegroundBrush");
        textBox.SetResourceReference(TextBox.BorderBrushProperty,  "DockBorderBrush");

        stack.Children.Add(lengthText);
        stack.Children.Add(textBox);

        return new Border
        {
            Padding = new Thickness(4),
            MinWidth = 360,
            Child = stack,
        };
    }
}
