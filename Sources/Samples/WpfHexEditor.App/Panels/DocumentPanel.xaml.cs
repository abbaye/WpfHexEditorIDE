using System.Windows.Controls;

namespace WpfHexEditor.App.Panels;

public partial class DocumentPanel : UserControl
{
    public DocumentPanel(string title)
    {
        InitializeComponent();
        TitleText.Text = title;
    }
}
