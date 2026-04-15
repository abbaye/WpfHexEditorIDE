//////////////////////////////////////////////
// GNU Affero General Public License v3.0 - 2026
// Author : Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.6
//////////////////////////////////////////////

using System.Text.RegularExpressions;
using System.Windows.Controls;
using System.Windows.Input;
using WpfHexEditor.Editor.StructureEditor.ViewModels;

namespace WpfHexEditor.Editor.StructureEditor.Tabs;

public sealed partial class DetectionTab : UserControl
{
    public DetectionTab() => InitializeComponent();

    private DetectionViewModel? VM => DataContext as DetectionViewModel;

    private void OnAddSignature(object sender, System.Windows.RoutedEventArgs e) => VM?.AddSignature();
    private void OnAddPattern(object sender, System.Windows.RoutedEventArgs e)   => VM?.AddContentPattern();

    private void OnHexOnly(object sender, TextCompositionEventArgs e)
        => e.Handled = !Regex.IsMatch(e.Text, @"^[0-9A-Fa-f]+$");

    private void OnNumericOnly(object sender, TextCompositionEventArgs e)
        => e.Handled = !Regex.IsMatch(e.Text, @"^\d+$");
}
