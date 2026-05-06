// WpfHexEditor.Sample.HexEditor — ParsedFieldsPanel.xaml.cs

using System.Windows.Controls;
using WpfHexEditor.Sample.HexEditor.ViewModels;

namespace WpfHexEditor.Sample.HexEditor.Views.Components
{
    public partial class ParsedFieldsPanel : UserControl
    {
        public ParsedFieldsPanelViewModel ViewModel { get; }

        public ParsedFieldsPanel()
        {
            InitializeComponent();
            ViewModel  = new ParsedFieldsPanelViewModel();
            DataContext = ViewModel;
        }
    }
}
