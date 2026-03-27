//////////////////////////////////////////////
// GNU Affero General Public License v3.0 - 2026
// Author : Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Opus 4.6, Claude Sonnet 4.6
//////////////////////////////////////////////

using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;
using WpfHexEditor.Docking.Core.Nodes;

namespace WpfHexEditor.Shell;

/// <summary>
/// VS-style Ctrl+Tab navigator window. Shows two columns: "Active Documents" and
/// "Active Tool Windows", sorted by most-recently-used order.
/// Tab/arrows cycle the selection; releasing Ctrl confirms; Escape cancels.
/// Left/Right arrows cross between columns. Type-ahead filter supported.
/// </summary>
internal sealed class NavigatorWindow : Window
{
    private readonly ListBox _documentsList;
    private readonly ListBox _toolsList;
    private readonly TextBox _searchBox;
    private readonly IReadOnlyList<DockItem> _allDocuments;
    private readonly IReadOnlyList<DockItem> _allTools;
    private DockItem? _selectedItem;
    private bool _confirmed;

    public DockItem? SelectedItem => _confirmed ? _selectedItem : null;

    public NavigatorWindow(
        IReadOnlyList<DockItem> documents,
        IReadOnlyList<DockItem> tools,
        DockItem? currentItem)
    {
        _allDocuments = documents;
        _allTools = tools;

        WindowStyle = WindowStyle.None;
        AllowsTransparency = true;
        ResizeMode = ResizeMode.NoResize;
        ShowInTaskbar = false;
        Topmost = true;
        SizeToContent = SizeToContent.WidthAndHeight;
        MinWidth = 400;
        MaxWidth = 600;
        MaxHeight = 500;

        var bg = new SolidColorBrush(Color.FromRgb(0x2D, 0x2D, 0x30));
        bg.Freeze();
        Background = bg;

        var border = new SolidColorBrush(Color.FromRgb(0x3F, 0x3F, 0x46));
        border.Freeze();
        BorderBrush = border;
        BorderThickness = new Thickness(1);

        var outerPanel = new DockPanel();

        // Search box at top
        _searchBox = new TextBox
        {
            Margin = new Thickness(8, 8, 8, 4),
            Padding = new Thickness(4, 2, 4, 2),
            FontSize = 12,
            Background = new SolidColorBrush(Color.FromRgb(0x33, 0x33, 0x37)),
            Foreground = new SolidColorBrush(Color.FromRgb(0xCC, 0xCC, 0xCC)),
            BorderBrush = new SolidColorBrush(Color.FromRgb(0x50, 0x50, 0x55)),
            BorderThickness = new Thickness(1),
            CaretBrush = Brushes.White
        };
        DockPanel.SetDock(_searchBox, Dock.Top);
        _searchBox.TextChanged += OnSearchTextChanged;
        outerPanel.Children.Add(_searchBox);

        var rootGrid = new Grid();
        rootGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        rootGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

        // Documents column
        var docsPanel = new StackPanel { Margin = new Thickness(8) };
        docsPanel.Children.Add(CreateHeader("Active Documents"));
        _documentsList = CreateListBox(documents);
        docsPanel.Children.Add(_documentsList);
        Grid.SetColumn(docsPanel, 0);
        rootGrid.Children.Add(docsPanel);

        // Tools column
        var toolsPanel = new StackPanel { Margin = new Thickness(8) };
        toolsPanel.Children.Add(CreateHeader("Active Tool Windows"));
        _toolsList = CreateListBox(tools);
        toolsPanel.Children.Add(_toolsList);
        Grid.SetColumn(toolsPanel, 1);
        rootGrid.Children.Add(toolsPanel);

        outerPanel.Children.Add(rootGrid);
        Content = outerPanel;

        // Select the current item or first document
        if (currentItem is not null)
        {
            var docIndex = documents.ToList().IndexOf(currentItem);
            if (docIndex >= 0)
            {
                _documentsList.SelectedIndex = docIndex;
                _documentsList.Focus();
            }
            else
            {
                var toolIndex = tools.ToList().IndexOf(currentItem);
                if (toolIndex >= 0)
                {
                    _toolsList.SelectedIndex = toolIndex;
                    _toolsList.Focus();
                }
            }
        }
        else if (documents.Count > 0)
        {
            _documentsList.SelectedIndex = 0;
            _documentsList.Focus();
        }

        // Cross-list selection: deselect the other list when one is selected
        _documentsList.SelectionChanged += (_, _) =>
        {
            if (_documentsList.SelectedItem is not null)
            {
                _toolsList.SelectedIndex = -1;
                _selectedItem = _documentsList.SelectedItem as DockItem;
            }
        };
        _toolsList.SelectionChanged += (_, _) =>
        {
            if (_toolsList.SelectedItem is not null)
            {
                _documentsList.SelectedIndex = -1;
                _selectedItem = _toolsList.SelectedItem as DockItem;
            }
        };

        PreviewKeyDown += OnPreviewKeyDown;
        PreviewKeyUp += OnPreviewKeyUp;
    }

    private void OnSearchTextChanged(object sender, TextChangedEventArgs e)
    {
        var filter = _searchBox.Text.Trim();
        RebuildFilteredList(_documentsList, _allDocuments, filter);
        RebuildFilteredList(_toolsList, _allTools, filter);

        // Auto-select first visible item
        if (_documentsList.Items.Count > 0)
        {
            _documentsList.SelectedIndex = 0;
            _selectedItem = _documentsList.Items[0] as DockItem;
        }
        else if (_toolsList.Items.Count > 0)
        {
            _toolsList.SelectedIndex = 0;
            _selectedItem = _toolsList.Items[0] as DockItem;
        }
    }

    private static void RebuildFilteredList(ListBox list, IReadOnlyList<DockItem> source, string filter)
    {
        list.Items.Clear();
        foreach (var item in source)
        {
            if (string.IsNullOrEmpty(filter)
                || item.Title.Contains(filter, StringComparison.OrdinalIgnoreCase)
                || item.ContentId.Contains(filter, StringComparison.OrdinalIgnoreCase))
            {
                list.Items.Add(item);
            }
        }
    }

    private void OnPreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape)
        {
            _confirmed = false;
            Close();
            e.Handled = true;
            return;
        }

        if (e.Key == Key.Enter)
        {
            _confirmed = true;
            Close();
            e.Handled = true;
            return;
        }

        // Left/Right arrows cross between document and tool columns
        if (e.Key == Key.Left)
        {
            if (_toolsList.SelectedIndex >= 0 && _documentsList.Items.Count > 0)
            {
                _documentsList.SelectedIndex = Math.Min(_toolsList.SelectedIndex, _documentsList.Items.Count - 1);
                _documentsList.Focus();
                e.Handled = true;
            }
            return;
        }

        if (e.Key == Key.Right)
        {
            if (_documentsList.SelectedIndex >= 0 && _toolsList.Items.Count > 0)
            {
                _toolsList.SelectedIndex = Math.Min(_documentsList.SelectedIndex, _toolsList.Items.Count - 1);
                _toolsList.Focus();
                e.Handled = true;
            }
            return;
        }

        if (e.Key == Key.Tab)
        {
            var reverse = Keyboard.Modifiers.HasFlag(ModifierKeys.Shift);
            AdvanceSelection(reverse);
            e.Handled = true;
        }

        // Type-ahead: forward printable keys to search box
        if (e.Key >= Key.A && e.Key <= Key.Z && !_searchBox.IsFocused)
        {
            _searchBox.Focus();
            // Let the key pass through to the TextBox
        }
    }

    private void OnPreviewKeyUp(object sender, KeyEventArgs e)
    {
        // When Ctrl is released, confirm the selection (VS-style)
        if (e.Key is Key.LeftCtrl or Key.RightCtrl)
        {
            _confirmed = true;
            Close();
            e.Handled = true;
        }
    }

    private void AdvanceSelection(bool reverse)
    {
        // Get the currently active list
        var activeList = _documentsList.SelectedIndex >= 0 ? _documentsList : _toolsList;
        var otherList = activeList == _documentsList ? _toolsList : _documentsList;

        if (reverse)
        {
            if (activeList.SelectedIndex > 0)
                activeList.SelectedIndex--;
            else if (otherList.Items.Count > 0)
            {
                otherList.SelectedIndex = otherList.Items.Count - 1;
                otherList.Focus();
            }
        }
        else
        {
            if (activeList.SelectedIndex < activeList.Items.Count - 1)
                activeList.SelectedIndex++;
            else if (otherList.Items.Count > 0)
            {
                otherList.SelectedIndex = 0;
                otherList.Focus();
            }
        }
    }

    private static TextBlock CreateHeader(string text) => new()
    {
        Text = text,
        FontWeight = FontWeights.SemiBold,
        Foreground = Brushes.White,
        Margin = new Thickness(0, 0, 0, 4)
    };

    private static ListBox CreateListBox(IReadOnlyList<DockItem> items)
    {
        var lb = new ListBox
        {
            Background = Brushes.Transparent,
            BorderThickness = new Thickness(0),
            Foreground = new SolidColorBrush(Color.FromRgb(0xCC, 0xCC, 0xCC)),
            MaxHeight = 400
        };

        foreach (var item in items)
            lb.Items.Add(item);

        lb.DisplayMemberPath = nameof(DockItem.Title);
        return lb;
    }
}
