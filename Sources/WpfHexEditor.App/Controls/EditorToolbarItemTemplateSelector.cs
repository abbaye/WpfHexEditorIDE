// GNU Affero General Public License v3.0 - 2026
// Contributors: Claude Sonnet 4.6

using System.Windows;
using System.Windows.Controls;
using WpfHexEditor.Editor.Core;

namespace WpfHexEditor.App.Controls;

/// <summary>
/// Selects the correct DataTemplate for an <see cref="EditorToolbarItem"/>
/// in the dynamic context pod: separator, dropdown-button, or plain icon-button.
/// </summary>
public sealed class EditorToolbarItemTemplateSelector : DataTemplateSelector
{
    public DataTemplate? SeparatorTemplate { get; set; }
    public DataTemplate? DropdownTemplate  { get; set; }
    public DataTemplate? ToggleTemplate    { get; set; }
    public DataTemplate? ButtonTemplate    { get; set; }

    public override DataTemplate? SelectTemplate(object item, DependencyObject container)
    {
        if (item is not EditorToolbarItem ti) return null;
        if (ti.IsSeparator)              return SeparatorTemplate;
        if (ti.DropdownItems?.Count > 0) return DropdownTemplate;
        if (ti.IsToggle)                 return ToggleTemplate;
        return ButtonTemplate;
    }
}
