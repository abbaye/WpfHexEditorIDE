//////////////////////////////////////////////
// GNU Affero General Public License v3.0 - 2026
// Author : Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.6
//////////////////////////////////////////////

using System.Windows.Controls;
using WpfHexEditor.Editor.StructureEditor.ViewModels;

namespace WpfHexEditor.Editor.StructureEditor.Tabs;

public sealed partial class VariablesTab : UserControl
{
    public VariablesTab() => InitializeComponent();

    private VariablesViewModel? VM => DataContext as VariablesViewModel;

    protected override void OnInitialized(EventArgs e)
    {
        base.OnInitialized(e);
        DataContextChanged += (_, _) =>
        {
            if (DataContext is VariablesViewModel vm)
                VarsGrid.ItemsSource = vm.Items;
        };
    }

    private void OnAddVariable(object sender, System.Windows.RoutedEventArgs e) => VM?.AddItem();
}
