//////////////////////////////////////////////
// GNU Affero General Public License v3.0 - 2026
// Author : Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.6
//////////////////////////////////////////////
// Project: WpfHexEditor.Editor.StructureEditor
// File: Dialogs/AddBlockDialog.xaml.cs
// Description: ThemedDialog-style modal for selecting block type + name.
//////////////////////////////////////////////

using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using WpfHexEditor.Editor.StructureEditor.ViewModels;

namespace WpfHexEditor.Editor.StructureEditor.Dialogs;

public sealed partial class AddBlockDialog : Window
{
    private static readonly Dictionary<string, string> TypeHints = new()
    {
        ["field"]               = "A binary field with a fixed or variable offset and length.",
        ["signature"]           = "Magic bytes that identify the format at a specific offset.",
        ["metadata"]            = "Reads a variable-length or symbolic value into a named variable.",
        ["conditional"]         = "Conditionally parses blocks based on a field value or variable.",
        ["loop"]                = "Repeats a block body while a condition is true.",
        ["action"]              = "Modifies a variable (increment, decrement, setVariable).",
        ["computeFromVariables"]= "Evaluates a math expression and stores the result.",
        ["repeating"]           = "Parses a fixed-count array of structured entries.",
        ["union"]               = "Selects a variant block set based on a discriminant variable.",
        ["nested"]              = "Embeds an external struct definition by reference.",
        ["pointer"]             = "Creates a navigation annotation to a pointed-to offset.",
    };

    public string SelectedBlockType { get; private set; } = "field";
    public string BlockName         { get; private set; } = "";

    public AddBlockDialog()
    {
        InitializeComponent();
        TypeCombo.ItemsSource = BlockViewModel.BlockTypes;
        TypeCombo.SelectedIndex = 0;
        NameBox.Focus();
    }

    private void OnTypeChanged(object sender, SelectionChangedEventArgs e)
    {
        var type = TypeCombo.SelectedItem?.ToString() ?? "field";
        HintText.Text = TypeHints.TryGetValue(type, out var hint) ? hint : "";
    }

    private void OnNameKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter) Commit();
    }

    private void OnAdd(object sender, RoutedEventArgs e) => Commit();

    private void Commit()
    {
        SelectedBlockType = TypeCombo.SelectedItem?.ToString() ?? "field";
        BlockName         = NameBox.Text.Trim();
        if (string.IsNullOrEmpty(BlockName))
            BlockName = SelectedBlockType;
        DialogResult = true;
    }
}
