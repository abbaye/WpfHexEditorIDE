// ==========================================================
// Project: WpfHexEditor.Shell
// File: QuickWindowSearchWindow.cs
// Description:
//     Ctrl+Shift+A Quick Window Search popup. Shows a flat list
//     of ALL panels (including hidden) with instant search filter.
// ==========================================================

using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using WpfHexEditor.Docking.Core.Nodes;

namespace WpfHexEditor.Shell;

/// <summary>
/// Quick window search popup (Ctrl+Shift+A). Shows all known panels
/// in a single-column filterable list. Selecting an item activates it.
/// </summary>
internal sealed class QuickWindowSearchWindow : Window
{
    private readonly TextBox _searchBox;
    private readonly ListBox _resultsList;
    private readonly IReadOnlyList<DockItem> _allItems;
    private DockItem? _selectedItem;
    private bool _confirmed;

    public DockItem? SelectedItem => _confirmed ? _selectedItem : null;

    public QuickWindowSearchWindow(IReadOnlyList<DockItem> allItems)
    {
        _allItems = allItems;

        WindowStyle = WindowStyle.None;
        AllowsTransparency = true;
        ResizeMode = ResizeMode.NoResize;
        ShowInTaskbar = false;
        Topmost = true;
        Width = 400;
        MaxHeight = 450;
        SizeToContent = SizeToContent.Height;

        var bg = new SolidColorBrush(Color.FromRgb(0x2D, 0x2D, 0x30));
        bg.Freeze();
        Background = bg;

        var borderBrush = new SolidColorBrush(Color.FromRgb(0x3F, 0x3F, 0x46));
        borderBrush.Freeze();
        BorderBrush = borderBrush;
        BorderThickness = new Thickness(1);

        var panel = new DockPanel { Margin = new Thickness(8) };

        // Search box
        _searchBox = new TextBox
        {
            Padding = new Thickness(6, 4, 6, 4),
            FontSize = 13,
            Background = new SolidColorBrush(Color.FromRgb(0x33, 0x33, 0x37)),
            Foreground = new SolidColorBrush(Color.FromRgb(0xCC, 0xCC, 0xCC)),
            BorderBrush = new SolidColorBrush(Color.FromRgb(0x00, 0x7A, 0xCC)),
            BorderThickness = new Thickness(1),
            CaretBrush = Brushes.White
        };
        DockPanel.SetDock(_searchBox, Dock.Top);
        _searchBox.TextChanged += OnSearchTextChanged;
        panel.Children.Add(_searchBox);

        // Results list
        _resultsList = new ListBox
        {
            Background = Brushes.Transparent,
            BorderThickness = new Thickness(0),
            Foreground = new SolidColorBrush(Color.FromRgb(0xCC, 0xCC, 0xCC)),
            Margin = new Thickness(0, 6, 0, 0),
            MaxHeight = 350
        };
        _resultsList.DisplayMemberPath = nameof(DockItem.Title);
        _resultsList.SelectionChanged += (_, _) =>
        {
            _selectedItem = _resultsList.SelectedItem as DockItem;
        };
        _resultsList.MouseDoubleClick += (_, _) =>
        {
            if (_selectedItem is not null)
            {
                _confirmed = true;
                Close();
            }
        };
        panel.Children.Add(_resultsList);

        Content = panel;

        // Populate initially
        RebuildFilteredList(string.Empty);
        if (_resultsList.Items.Count > 0)
            _resultsList.SelectedIndex = 0;

        PreviewKeyDown += OnPreviewKeyDown;
        Loaded += (_, _) => _searchBox.Focus();
        Deactivated += (_, _) => { if (IsLoaded) Close(); };
    }

    private void OnSearchTextChanged(object sender, TextChangedEventArgs e)
    {
        RebuildFilteredList(_searchBox.Text.Trim());
        if (_resultsList.Items.Count > 0)
            _resultsList.SelectedIndex = 0;
    }

    private void RebuildFilteredList(string filter)
    {
        _resultsList.Items.Clear();
        foreach (var item in _allItems)
        {
            if (string.IsNullOrEmpty(filter)
                || item.Title.Contains(filter, StringComparison.OrdinalIgnoreCase)
                || item.ContentId.Contains(filter, StringComparison.OrdinalIgnoreCase))
            {
                _resultsList.Items.Add(item);
            }
        }
    }

    private void OnPreviewKeyDown(object sender, KeyEventArgs e)
    {
        switch (e.Key)
        {
            case Key.Escape:
                _confirmed = false;
                Close();
                e.Handled = true;
                break;

            case Key.Enter:
                _confirmed = _selectedItem is not null;
                Close();
                e.Handled = true;
                break;

            case Key.Down:
                if (_resultsList.SelectedIndex < _resultsList.Items.Count - 1)
                    _resultsList.SelectedIndex++;
                e.Handled = true;
                break;

            case Key.Up:
                if (_resultsList.SelectedIndex > 0)
                    _resultsList.SelectedIndex--;
                e.Handled = true;
                break;
        }
    }
}
