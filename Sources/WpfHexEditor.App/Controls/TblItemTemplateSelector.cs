// Apache 2.0 - 2026
// Contributors: Claude Sonnet 4.6

using System.Windows;
using System.Windows.Controls;
using WpfHexEditor.App.Models;

namespace WpfHexEditor.App.Controls;

/// <summary>
/// Selects among Header / Separator / Item data templates for the TBL toolbar ComboBox.
/// </summary>
public sealed class TblItemTemplateSelector : DataTemplateSelector
{
    public DataTemplate? HeaderTemplate    { get; set; }
    public DataTemplate? SeparatorTemplate { get; set; }
    public DataTemplate? ItemTemplate      { get; set; }

    public override DataTemplate? SelectTemplate(object item, DependencyObject container)
    {
        if (item is not TblSelectionItem t) return base.SelectTemplate(item, container);

        return t.Kind switch
        {
            TblSelectionKind.Header    => HeaderTemplate,
            TblSelectionKind.Separator => SeparatorTemplate,
            _                          => ItemTemplate,
        };
    }
}
