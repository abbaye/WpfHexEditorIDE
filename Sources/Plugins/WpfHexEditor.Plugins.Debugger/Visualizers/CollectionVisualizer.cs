// ==========================================================
// Project: WpfHexEditor.Plugins.Debugger
// File: Visualizers/CollectionVisualizer.cs
// Description: Debug visualizer that renders IEnumerable / array / List values
//              as a scrollable DataGrid with index + value columns.
// Architecture: IDebugVisualizer implementation — no external MVVM framework.
// ==========================================================

using System.Windows;
using System.Windows.Controls;
using WpfHexEditor.SDK.Contracts;

namespace WpfHexEditor.Plugins.Debugger.Visualizers;

/// <summary>
/// Renders collection-type variables (arrays, List, IEnumerable) as an indexed DataGrid.
/// Activated when the type name contains collection-like keywords.
/// </summary>
internal sealed class CollectionVisualizer : IDebugVisualizer
{
    public string DisplayName => "Collection Visualizer";

    public bool CanVisualize(DebugVariableContext context)
    {
        var t = context.TypeName;
        return t.EndsWith("[]", StringComparison.Ordinal)
            || t.Contains("List<",       StringComparison.Ordinal)
            || t.Contains("IEnumerable", StringComparison.Ordinal)
            || t.Contains("Array",       StringComparison.Ordinal)
            || t.Contains("Collection",  StringComparison.Ordinal);
    }

    public FrameworkElement CreateView(DebugVariableContext context)
    {
        var grid = new DataGrid
        {
            AutoGenerateColumns   = false,
            IsReadOnly            = true,
            CanUserAddRows        = false,
            CanUserDeleteRows     = false,
            HeadersVisibility     = DataGridHeadersVisibility.Column,
            AlternatingRowBackground = System.Windows.Media.Brushes.Transparent,
        };

        grid.Columns.Add(new DataGridTextColumn { Header = "#",     Binding = new System.Windows.Data.Binding("Index") });
        grid.Columns.Add(new DataGridTextColumn { Header = "Value", Binding = new System.Windows.Data.Binding("Value"), Width = new DataGridLength(1, DataGridLengthUnitType.Star) });

        var items = ParseItems(context.RawValue);
        grid.ItemsSource = items;

        var border = new Border { Padding = new Thickness(4), Child = grid };
        return border;
    }

    private static List<IndexedItem> ParseItems(string rawValue)
    {
        var result = new List<IndexedItem>();
        // DAP typically returns array values as "{ 1, 2, 3 }" or "[1, 2, 3]"
        var inner = rawValue.Trim().TrimStart('{', '[').TrimEnd('}', ']').Trim();
        if (string.IsNullOrEmpty(inner)) return result;

        var parts = inner.Split(',');
        for (int i = 0; i < parts.Length; i++)
            result.Add(new IndexedItem(i, parts[i].Trim()));

        return result;
    }

    private sealed record IndexedItem(int Index, string Value);
}
