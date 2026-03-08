// ==========================================================
// Project: WpfHexEditor.Plugins.AssemblyExplorer
// File: Converters/NodeTypeToIconConverter.cs
// Author: Derek Tremblay
// Created: 2026-03-08
// Description:
//     IValueConverter: AssemblyNodeViewModel → string (Segoe MDL2 glyph).
//     Used in TreeView DataTemplates to display node type icons.
// ==========================================================

using System.Globalization;
using System.Windows.Data;
using WpfHexEditor.Plugins.AssemblyExplorer.ViewModels;

namespace WpfHexEditor.Plugins.AssemblyExplorer.Converters;

/// <summary>
/// Converts an <see cref="AssemblyNodeViewModel"/> to its Segoe MDL2 Assets icon glyph.
/// Delegates to each node's <see cref="AssemblyNodeViewModel.IconGlyph"/> property.
/// </summary>
[ValueConversion(typeof(AssemblyNodeViewModel), typeof(string))]
public sealed class NodeTypeToIconConverter : IValueConverter
{
    public static readonly NodeTypeToIconConverter Instance = new();

    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        => value is AssemblyNodeViewModel node ? node.IconGlyph : "\uE8A5";

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
