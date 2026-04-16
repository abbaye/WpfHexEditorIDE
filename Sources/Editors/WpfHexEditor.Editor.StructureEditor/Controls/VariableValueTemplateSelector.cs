//////////////////////////////////////////////
// Project      : WpfHexEditor.Editor.StructureEditor
// File         : Controls/VariableValueTemplateSelector.cs
// Description  : DataTemplateSelector that picks the correct cell template for the
//                "Initial Value" column of the Variables DataGrid based on VariableItemViewModel.Type.
// Architecture : Stateless selector; templates are injected from XAML resources.
//////////////////////////////////////////////

using System.Windows;
using System.Windows.Controls;
using WpfHexEditor.Editor.StructureEditor.ViewModels;

namespace WpfHexEditor.Editor.StructureEditor.Controls;

internal sealed class VariableValueTemplateSelector : DataTemplateSelector
{
    public DataTemplate? BoolTemplate   { get; set; }
    public DataTemplate? IntTemplate    { get; set; }
    public DataTemplate? FloatTemplate  { get; set; }
    public DataTemplate? HexTemplate    { get; set; }
    public DataTemplate? StringTemplate { get; set; }

    public override DataTemplate? SelectTemplate(object item, DependencyObject container)
    {
        if (item is not VariableItemViewModel vm) return base.SelectTemplate(item, container);

        return vm.Type switch
        {
            "bool"  => BoolTemplate,
            "int"   => IntTemplate,
            "float" => FloatTemplate,
            "hex"   => HexTemplate,
            _       => StringTemplate,
        } ?? base.SelectTemplate(item, container);
    }
}
