//////////////////////////////////////////////
// GNU Affero General Public License v3.0 - 2026
// Author : Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.6
//////////////////////////////////////////////
// Project: WpfHexEditor.Editor.StructureEditor
// File: Controls/BlockEditorTemplateSelector.cs
// Description: DataTemplateSelector dispatching on BlockViewModel.BlockType.
//////////////////////////////////////////////

using System.Windows;
using System.Windows.Controls;
using WpfHexEditor.Editor.StructureEditor.ViewModels;

namespace WpfHexEditor.Editor.StructureEditor.Controls;

/// <summary>
/// Selects the appropriate <see cref="DataTemplate"/> based on the block type.
/// Templates are defined in the resource dictionaries merged into StructureEditor.xaml.
/// </summary>
internal sealed class BlockEditorTemplateSelector : DataTemplateSelector
{
    public override DataTemplate? SelectTemplate(object? item, DependencyObject container)
    {
        if (item is not BlockViewModel vm) return null;
        if (container is not FrameworkElement fe) return null;

        var key = vm.BlockType switch
        {
            "field"                => "BlockTemplate_Field",
            "signature"            => "BlockTemplate_Field",   // same form
            "metadata"             => "BlockTemplate_Metadata",
            "conditional"          => "BlockTemplate_Conditional",
            "loop"                 => "BlockTemplate_Loop",
            "action"               => "BlockTemplate_Action",
            "computeFromVariables" => "BlockTemplate_Compute",
            "repeating"            => "BlockTemplate_Repeating",
            "union"                => "BlockTemplate_Union",
            "nested"               => "BlockTemplate_Nested",
            "pointer"              => "BlockTemplate_Pointer",
            "group"                => "BlockTemplate_Group",
            "header"               => "BlockTemplate_Header",
            "data"                 => "BlockTemplate_Data",
            _                      => "BlockTemplate_Field",
        };

        return fe.TryFindResource(key) as DataTemplate;
    }
}
