// ==========================================================
// Project: WpfHexEditor.Editor.ResxEditor
// File: Controls/ResxCellTemplateSelector.cs
// Description:
//     DataTemplateSelector that returns the correct DataTemplate
//     for the Value column based on the entry's ResxEntryType.
//     Templates are looked up as StaticResources from the editor's
//     resource dictionary.
// ==========================================================

using System.Windows;
using System.Windows.Controls;
using WpfHexEditor.Editor.ResxEditor.Models;
using WpfHexEditor.Editor.ResxEditor.ViewModels;

namespace WpfHexEditor.Editor.ResxEditor.Controls;

/// <summary>Selects the Value cell template based on <see cref="ResxEntryType"/>.</summary>
public sealed class ResxCellTemplateSelector : DataTemplateSelector
{
    public DataTemplate? StringTemplate  { get; set; }
    public DataTemplate? ImageTemplate   { get; set; }
    public DataTemplate? BinaryTemplate  { get; set; }
    public DataTemplate? FileRefTemplate { get; set; }
    public DataTemplate? OtherTemplate   { get; set; }

    public override DataTemplate? SelectTemplate(object item, DependencyObject container)
    {
        if (item is not ResxEntryViewModel vm)
            return StringTemplate;

        return vm.EntryType switch
        {
            ResxEntryType.Image   => ImageTemplate   ?? StringTemplate,
            ResxEntryType.Binary  => BinaryTemplate  ?? StringTemplate,
            ResxEntryType.FileRef => FileRefTemplate ?? StringTemplate,
            ResxEntryType.Other   => OtherTemplate   ?? StringTemplate,
            _                     => StringTemplate
        };
    }
}
