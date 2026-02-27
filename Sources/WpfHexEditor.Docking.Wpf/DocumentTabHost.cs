//////////////////////////////////////////////
// Apache 2.0  - 2026
// Author : Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.5
//////////////////////////////////////////////

using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using WpfHexEditor.Docking.Core.Nodes;

namespace WpfHexEditor.Docking.Wpf;

/// <summary>
/// WPF projection of <see cref="DocumentHostNode"/>: specialized tab host for documents.
/// Visually distinct from tool panel tabs (different background, tab style).
/// </summary>
public class DocumentTabHost : DockTabControl
{
    public DocumentTabHost()
    {
        // Visual distinction for document host
        BorderBrush = new SolidColorBrush(Color.FromRgb(0, 122, 204)); // VS blue
        BorderThickness = new Thickness(0, 2, 0, 0);
    }

    /// <summary>
    /// Shows a placeholder when no documents are open.
    /// </summary>
    public void ShowEmptyPlaceholder()
    {
        Items.Clear();
        var placeholder = new TextBlock
        {
            Text = "Open a document to begin",
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            Foreground = Brushes.Gray,
            FontSize = 14
        };

        // Wrap in a tab to maintain visual consistency
        Items.Add(new TabItem
        {
            Header = "Start",
            Content = placeholder,
            IsEnabled = false
        });
    }
}
