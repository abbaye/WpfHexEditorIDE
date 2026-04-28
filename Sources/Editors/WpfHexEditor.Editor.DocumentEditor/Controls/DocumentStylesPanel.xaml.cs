// ==========================================================
// Project: WpfHexEditor.Editor.DocumentEditor
// File: Controls/DocumentStylesPanel.xaml.cs
// Description:
//     Styles quick-apply panel (Phase 18). Shows 12 built-in paragraph styles.
//     Click fires StyleSelected event with style name; host calls
//     DocumentCanvasRenderer.SetBlockAttribute("style", styleName).
// ==========================================================
using System.Windows.Controls;
using WpfHexEditor.Editor.DocumentEditor.Properties;

namespace WpfHexEditor.Editor.DocumentEditor.Controls;

public partial class DocumentStylesPanel : UserControl
{
    public event EventHandler<string>? StyleSelected;

    private static (string Display, string StyleKey, double FontSize, bool Bold)[] BuildStyles() =>
    [
        (DocumentEditorResources.DocStyles_Normal,        "paragraph", 13, false),
        (DocumentEditorResources.DocStyles_Heading1,      "heading1",  22, true),
        (DocumentEditorResources.DocStyles_Heading2,      "heading2",  18, true),
        (DocumentEditorResources.DocStyles_Heading3,      "heading3",  15, true),
        (DocumentEditorResources.DocStyles_Heading4,      "heading4",  14, true),
        (DocumentEditorResources.DocStyles_Heading5,      "heading5",  13, true),
        (DocumentEditorResources.DocStyles_Heading6,      "heading6",  12, true),
        (DocumentEditorResources.DocStyles_Quote,         "quote",     13, false),
        (DocumentEditorResources.DocStyles_Code,          "code",      12, false),
        (DocumentEditorResources.DocStyles_Caption,       "caption",   11, false),
        (DocumentEditorResources.DocStyles_ListParagraph, "list",      13, false),
        (DocumentEditorResources.DocStyles_IntenseQuote,  "intense",   13, true),
    ];

    public DocumentStylesPanel()
    {
        InitializeComponent();
        foreach (var (display, styleKey, fontSize, bold) in BuildStyles())
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
