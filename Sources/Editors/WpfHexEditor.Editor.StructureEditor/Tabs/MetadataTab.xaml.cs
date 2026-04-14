//////////////////////////////////////////////
// GNU Affero General Public License v3.0 - 2026
// Author : Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.6
//////////////////////////////////////////////
// Project: WpfHexEditor.Editor.StructureEditor
// File: Tabs/MetadataTab.xaml.cs
// Description: Code-behind for MetadataTab — thin, all logic in MetadataViewModel.
//////////////////////////////////////////////

using System.Windows.Controls;
using WpfHexEditor.Editor.StructureEditor.ViewModels;

namespace WpfHexEditor.Editor.StructureEditor.Tabs;

public sealed partial class MetadataTab : UserControl
{
    public MetadataTab() => InitializeComponent();

    private MetadataViewModel? VM => DataContext as MetadataViewModel;

    private void OnAddExtension(object sender, System.Windows.RoutedEventArgs e)  => VM?.AddExtension();
    private void OnAddMimeType(object sender, System.Windows.RoutedEventArgs e)   => VM?.AddMimeType();
    private void OnAddSoftware(object sender, System.Windows.RoutedEventArgs e)   => VM?.AddSoftware();
}
