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
using WpfHexEditor.Editor.StructureEditor.Properties;
using WpfHexEditor.Editor.StructureEditor.ViewModels;

namespace WpfHexEditor.Editor.StructureEditor.Dialogs;

public sealed partial class AddBlockDialog : Window
{
    private static Dictionary<string, string> TypeHints => new()
    {
        ["field"]               = StructureEditorResources.StructureEditor_TypeHintField,
        ["signature"]           = StructureEditorResources.StructureEditor_TypeHintSignature,
        ["metadata"]            = StructureEditorResources.StructureEditor_TypeHintMetadata,
        ["conditional"]         = StructureEditorResources.StructureEditor_TypeHintConditional,
        ["loop"]                = StructureEditorResources.StructureEditor_TypeHintLoop,
        ["action"]              = StructureEditorResources.StructureEditor_TypeHintAction,
        ["computeFromVariables"]= StructureEditorResources.StructureEditor_TypeHintCompute,
        ["repeating"]           = StructureEditorResources.StructureEditor_TypeHintRepeating,
        ["union"]               = StructureEditorResources.StructureEditor_TypeHintUnion,
        ["nested"]              = StructureEditorResources.StructureEditor_TypeHintNested,
        ["pointer"]             = StructureEditorResources.StructureEditor_TypeHintPointer,
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
