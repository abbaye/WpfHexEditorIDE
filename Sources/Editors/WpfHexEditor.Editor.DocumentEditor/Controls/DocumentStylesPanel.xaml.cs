// ==========================================================
// Project: WpfHexEditor.Editor.DocumentEditor
// File: Controls/DocumentStylesPanel.xaml.cs
// Description:
//     Styles quick-apply panel (Phase 18). Shows 12 built-in paragraph styles.
//     Click fires StyleSelected event with style name; host calls
//     DocumentCanvasRenderer.SetBlockAttribute("style", styleName).
// ==========================================================
using System.Windows.Controls;

namespace WpfHexEditor.Editor.DocumentEditor.Controls;

public partial class DocumentStylesPanel : UserControl
{
    public event EventHandler<string>? StyleSelected;

    private static readonly (string Display, string StyleKey, double FontSize, bool Bold)[] Styles =
    [
        ("Normal",         "paragraph", 13, false),
        ("Heading 1",      "heading1",  22, true),
        ("Heading 2",      "heading2",  18, true),
        ("Heading 3",      "heading3",  15, true),
        ("Heading 4",      "heading4",  14, true),
        ("Heading 5",      "heading5",  13, true),
        ("Heading 6",      "heading6",  12, true),
        ("Quote",          "quote",     13, false),
        ("Code",           "code",      12, false),
        ("Caption",        "caption",   11, false),
        ("List Paragraph", "list",      13, false),
        ("Intense Quote",  "intense",   13, true),
    ];

    public DocumentStylesPanel()
    {
        InitializeComponent();
        foreach (var (display, styleKey, fontSize, bold) in Styles)
        {
            var item = new ListBoxItem
            {
                Content    = display,
                Tag        = styleKey,
                FontSize   = fontSize,
                FontWeight = bold ? System.Windows.FontWeights.Bold : System.Windows.FontWeights.Normal,
                Padding    = new System.Windows.Thickness(8, 4, 8, 4),
            };
            PART_StyleList.Items.Add(item);
        }
    }

    private void OnStyleSelected(object sender, SelectionChangedEventArgs e)
    {
        if (PART_StyleList.SelectedItem is not ListBoxItem item) return;
        StyleSelected?.Invoke(this, item.Tag?.ToString() ?? "paragraph");
        PART_StyleList.SelectedIndex = -1;
    }
}
