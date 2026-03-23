//////////////////////////////////////////////
// Project      : WpfHexEditor.App
// File         : CommandPaletteWindow.cs
// Description  : VS Code-style Command Palette overlay (Ctrl+Shift+P).
//                Code-behind only — no XAML; all layout built programmatically.
// Architecture : Non-modal Window (Show, not ShowDialog). Closes on Deactivated.
//                Uses CP_* resource tokens from the active theme.
//////////////////////////////////////////////

using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Effects;
using WpfHexEditor.App.Models;
using WpfHexEditor.App.Services;

namespace WpfHexEditor.App.Dialogs;

/// <summary>
/// Floating command palette overlay. Opened non-modally; closes when deactivated or Esc pressed.
/// </summary>
public sealed class CommandPaletteWindow : Window
{
    private readonly CommandPaletteService _service;
    private readonly Window _owner;
    private readonly Point? _anchor;
    private readonly TextBox _searchBox;
    private readonly ListBox _resultsList;
    private bool _closingStarted;

    public CommandPaletteWindow(CommandPaletteService service, Window owner, Point? anchor = null)
    {
        _service = service;
        _owner   = owner;
        _anchor  = anchor;

        // Window chrome
        WindowStyle          = WindowStyle.None;
        AllowsTransparency   = true;
        Background           = Brushes.Transparent;
        ResizeMode           = ResizeMode.NoResize;
        ShowInTaskbar        = false;
        Topmost              = true;
        Width                = 580;
        SizeToContent        = SizeToContent.Height;
        MaxHeight            = 440;

        // ─── Root border ───────────────────────────────────────────────────────────
        var root = new Border
        {
            CornerRadius = new CornerRadius(6),
            Effect       = new DropShadowEffect
            {
                Direction   = 315,
                ShadowDepth = 6,
                BlurRadius  = 18,
                Opacity     = 0.55,
                Color       = Colors.Black
            }
        };
        root.SetResourceReference(Border.BackgroundProperty,  "CP_BackgroundBrush");
        root.SetResourceReference(Border.BorderBrushProperty, "CP_BorderBrush");
        root.BorderThickness = new Thickness(1);
        Content = root;

        // ─── Layout grid ───────────────────────────────────────────────────────────
        var grid = new Grid();
        grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        root.Child = grid;

        // ─── Search row ────────────────────────────────────────────────────────────
        var inputBorder = new Border
        {
            Padding          = new Thickness(8, 6, 8, 6),
            BorderThickness  = new Thickness(0, 0, 0, 1)
        };
        inputBorder.SetResourceReference(Border.BackgroundProperty,   "CP_InputBackgroundBrush");
        inputBorder.SetResourceReference(Border.BorderBrushProperty,  "CP_BorderBrush");

        var searchDock = new DockPanel { LastChildFill = true };

        var searchIcon = new TextBlock
        {
            Text             = "\uE721",
            FontFamily       = new FontFamily("Segoe MDL2 Assets"),
            FontSize         = 14,
            VerticalAlignment = VerticalAlignment.Center,
            Margin           = new Thickness(0, 0, 8, 0)
        };
        searchIcon.SetResourceReference(TextBlock.ForegroundProperty, "CP_SecondaryTextBrush");
        DockPanel.SetDock(searchIcon, Dock.Left);
        searchDock.Children.Add(searchIcon);

        _searchBox = new TextBox
        {
            FontSize        = 14,
            BorderThickness = new Thickness(0),
            Background      = Brushes.Transparent,
            VerticalContentAlignment = VerticalAlignment.Center
        };
        _searchBox.SetResourceReference(TextBox.ForegroundProperty,         "CP_TextBrush");
        _searchBox.SetResourceReference(TextBox.CaretBrushProperty,         "CP_TextBrush");
        _searchBox.SetResourceReference(TextBox.SelectionBrushProperty,     "CP_HighlightBrush");
        _searchBox.TextChanged += OnSearchTextChanged;
        searchDock.Children.Add(_searchBox);

        inputBorder.Child = searchDock;
        Grid.SetRow(inputBorder, 0);
        grid.Children.Add(inputBorder);

        // ─── Results row ───────────────────────────────────────────────────────────
        _resultsList = new ListBox
        {
            BorderThickness            = new Thickness(0),
            HorizontalContentAlignment = HorizontalAlignment.Stretch
        };
        ScrollViewer.SetHorizontalScrollBarVisibility(_resultsList, ScrollBarVisibility.Disabled);
        VirtualizingPanel.SetIsVirtualizing(_resultsList, true);
        VirtualizingPanel.SetVirtualizationMode(_resultsList, VirtualizationMode.Recycling);
        _resultsList.SetResourceReference(ListBox.BackgroundProperty, "CP_BackgroundBrush");
        _resultsList.SetResourceReference(ListBox.ForegroundProperty, "CP_TextBrush");
        _resultsList.MouseDoubleClick += (_, _) => ExecuteSelected();

        // Item template
        _resultsList.ItemTemplate = BuildItemTemplate();

        // Item container style — highlight selected row
        var itemStyle = new Style(typeof(ListBoxItem));
        itemStyle.Setters.Add(new Setter(ListBoxItem.PaddingProperty, new Thickness(12, 5, 12, 5)));
        itemStyle.Setters.Add(new Setter(ListBoxItem.BackgroundProperty, Brushes.Transparent));
        var hoverTrigger = new Trigger { Property = ListBoxItem.IsMouseOverProperty, Value = true };
        hoverTrigger.Setters.Add(new Setter(ListBoxItem.BackgroundProperty,
            new DynamicResourceExtension("CP_HoverBrush")));
        itemStyle.Triggers.Add(hoverTrigger);
        var selectTrigger = new Trigger { Property = ListBoxItem.IsSelectedProperty, Value = true };
        selectTrigger.Setters.Add(new Setter(ListBoxItem.BackgroundProperty,
            new DynamicResourceExtension("CP_HighlightBrush")));
        itemStyle.Triggers.Add(selectTrigger);
        _resultsList.ItemContainerStyle = itemStyle;

        Grid.SetRow(_resultsList, 1);
        grid.Children.Add(_resultsList);

        // ─── Keyboard handling ─────────────────────────────────────────────────────
        _searchBox.PreviewKeyDown += OnSearchBoxKeyDown;
        Deactivated += (_, _) => { if (!_closingStarted) Close(); };

        Loaded += OnLoaded;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        if (_anchor.HasValue)
        {
            // Flush below the launcher button, centered on it
            Left = _anchor.Value.X - ActualWidth / 2;
            Top  = _anchor.Value.Y;
        }
        else
        {
            // Fallback (keyboard shortcut path): centered, ~18% from top of owner
            Left = _owner.Left + (_owner.Width - ActualWidth) / 2;
            Top  = _owner.Top  + _owner.Height * 0.18;
        }

        RefreshResults(string.Empty);
        _searchBox.Focus();
    }

    private void OnSearchTextChanged(object sender, TextChangedEventArgs e)
        => RefreshResults(_searchBox.Text);

    private void RefreshResults(string query)
    {
        var results = _service.Filter(query);
        _resultsList.ItemsSource = results;
        if (results.Count > 0)
            _resultsList.SelectedIndex = 0;
    }

    private void OnSearchBoxKeyDown(object sender, KeyEventArgs e)
    {
        switch (e.Key)
        {
            case Key.Down:
                MoveSelection(+1);
                e.Handled = true;
                break;
            case Key.Up:
                MoveSelection(-1);
                e.Handled = true;
                break;
            case Key.Enter:
                ExecuteSelected();
                e.Handled = true;
                break;
            case Key.Escape:
                Close();
                e.Handled = true;
                break;
        }
    }

    private void MoveSelection(int delta)
    {
        if (_resultsList.Items.Count == 0) return;
        var next = _resultsList.SelectedIndex + delta;
        _resultsList.SelectedIndex = Math.Clamp(next, 0, _resultsList.Items.Count - 1);
        _resultsList.ScrollIntoView(_resultsList.SelectedItem);
    }

    private void ExecuteSelected()
    {
        if (_resultsList.SelectedItem is not CommandPaletteEntry entry) return;
        if (entry.Command?.CanExecute(entry.CommandParameter) == true)
        {
            _closingStarted = true;
            Close();
            entry.Command.Execute(entry.CommandParameter);
        }
    }

    // Builds the DataTemplate for each palette row:
    //   [Icon]  [Name]              [Gesture  Category]
    private DataTemplate BuildItemTemplate()
    {
        var factory = new FrameworkElementFactory(typeof(Grid));

        var col0 = new FrameworkElementFactory(typeof(ColumnDefinition));
        col0.SetValue(ColumnDefinition.WidthProperty, GridLength.Auto);
        var col1 = new FrameworkElementFactory(typeof(ColumnDefinition));
        col1.SetValue(ColumnDefinition.WidthProperty, new GridLength(1, GridUnitType.Star));
        var col2 = new FrameworkElementFactory(typeof(ColumnDefinition));
        col2.SetValue(ColumnDefinition.WidthProperty, GridLength.Auto);
        factory.AppendChild(col0);
        factory.AppendChild(col1);
        factory.AppendChild(col2);

        // Icon
        var icon = new FrameworkElementFactory(typeof(TextBlock));
        icon.SetValue(TextBlock.FontFamilyProperty, new FontFamily("Segoe MDL2 Assets"));
        icon.SetValue(TextBlock.FontSizeProperty, 13d);
        icon.SetValue(TextBlock.VerticalAlignmentProperty, VerticalAlignment.Center);
        icon.SetValue(TextBlock.MarginProperty, new Thickness(0, 0, 8, 0));
        icon.SetValue(TextBlock.WidthProperty, 18d);
        icon.SetBinding(TextBlock.TextProperty,
            new System.Windows.Data.Binding(nameof(CommandPaletteEntry.IconGlyph)));
        icon.SetResourceReference(TextBlock.ForegroundProperty, "CP_SecondaryTextBrush");
        icon.SetValue(Grid.ColumnProperty, 0);
        factory.AppendChild(icon);

        // Name
        var name = new FrameworkElementFactory(typeof(TextBlock));
        name.SetValue(TextBlock.FontSizeProperty, 13d);
        name.SetValue(TextBlock.VerticalAlignmentProperty, VerticalAlignment.Center);
        name.SetBinding(TextBlock.TextProperty,
            new System.Windows.Data.Binding(nameof(CommandPaletteEntry.Name)));
        name.SetResourceReference(TextBlock.ForegroundProperty, "CP_TextBrush");
        name.SetValue(Grid.ColumnProperty, 1);
        factory.AppendChild(name);

        // Right stack: gesture + category
        var rightStack = new FrameworkElementFactory(typeof(StackPanel));
        rightStack.SetValue(StackPanel.OrientationProperty, Orientation.Horizontal);
        rightStack.SetValue(Grid.ColumnProperty, 2);

        var gesture = new FrameworkElementFactory(typeof(TextBlock));
        gesture.SetValue(TextBlock.FontSizeProperty, 11d);
        gesture.SetValue(TextBlock.VerticalAlignmentProperty, VerticalAlignment.Center);
        gesture.SetValue(TextBlock.MarginProperty, new Thickness(8, 0, 4, 0));
        gesture.SetBinding(TextBlock.TextProperty,
            new System.Windows.Data.Binding(nameof(CommandPaletteEntry.GestureText)));
        gesture.SetResourceReference(TextBlock.ForegroundProperty, "CP_SecondaryTextBrush");
        rightStack.AppendChild(gesture);

        var category = new FrameworkElementFactory(typeof(TextBlock));
        category.SetValue(TextBlock.FontSizeProperty, 11d);
        category.SetValue(TextBlock.VerticalAlignmentProperty, VerticalAlignment.Center);
        category.SetValue(TextBlock.MarginProperty, new Thickness(0, 0, 2, 0));
        category.SetBinding(TextBlock.TextProperty,
            new System.Windows.Data.Binding(nameof(CommandPaletteEntry.Category)));
        category.SetResourceReference(TextBlock.ForegroundProperty, "CP_SecondaryTextBrush");
        rightStack.AppendChild(category);

        factory.AppendChild(rightStack);

        return new DataTemplate { VisualTree = factory };
    }
}
