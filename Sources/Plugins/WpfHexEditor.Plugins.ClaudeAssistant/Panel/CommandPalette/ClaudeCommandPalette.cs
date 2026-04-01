// ==========================================================
// Project: WpfHexEditor.Plugins.ClaudeAssistant
// File: ClaudeCommandPalette.cs
// Author: Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Opus 4.6
// Created: 2026-04-01
// License: GNU Affero General Public License v3.0 (AGPL-3.0)
// Description:
//     Code-behind Window for the Claude command palette (Ctrl+Shift+A).
//     Pattern matches CommandPaletteWindow.cs and CustomizeLayoutPopup.cs.
// ==========================================================
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Effects;
using WpfHexEditor.Plugins.ClaudeAssistant.Presets;

namespace WpfHexEditor.Plugins.ClaudeAssistant.Panel.CommandPalette;

public sealed class ClaudeCommandPalette : Window
{
    private readonly List<ClaudeCommandEntry> _allEntries;
    private readonly List<ClaudeCommandEntry> _filteredEntries = [];
    private readonly ListBox _listBox;
    private readonly TextBox _searchBox;
    private bool _closing;

    public ClaudeCommandPalette(
        List<ClaudeCommandEntry> entries,
        Window owner,
        Point? anchor = null)
    {
        _allEntries = entries;

        WindowStyle = WindowStyle.None;
        AllowsTransparency = true;
        Background = Brushes.Transparent;
        ResizeMode = ResizeMode.NoResize;
        ShowInTaskbar = false;
        Width = 480;
        SizeToContent = SizeToContent.Height;
        MaxHeight = 440;

        if (owner is not null)
        {
            Owner = owner;
            WindowStartupLocation = WindowStartupLocation.Manual;

            if (anchor.HasValue)
            {
                // Same positioning as standard Command Palette (Ctrl+Shift+P)
                Left = anchor.Value.X - Width / 2;
                Top = anchor.Value.Y;
            }
            else
            {
                Left = owner.Left + (owner.Width - Width) / 2;
                Top = owner.Top + owner.Height * 0.18;
            }
        }

        // Drop shadow
        var rootBorder = new Border
        {
            CornerRadius = new CornerRadius(6),
            Padding = new Thickness(0),
            Effect = new DropShadowEffect
            {
                Direction = 315,
                ShadowDepth = 6,
                BlurRadius = 18,
                Opacity = 0.55,
                Color = Colors.Black
            }
        };
        rootBorder.SetResourceReference(Border.BackgroundProperty, "DockBackgroundBrush");
        rootBorder.SetResourceReference(Border.BorderBrushProperty, "CA_AccentBrandingBrush");
        rootBorder.BorderThickness = new Thickness(1.5);

        var grid = new Grid();
        grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star), MaxHeight = 360 });

        // Search box
        var searchBorder = new Border
        {
            Padding = new Thickness(12, 10, 12, 8),
            BorderThickness = new Thickness(0, 0, 0, 1)
        };
        searchBorder.SetResourceReference(Border.BorderBrushProperty, "DockBorderBrush");

        var searchGrid = new Grid();
        searchGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        searchGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

        var searchIcon = new TextBlock
        {
            Text = "\uE721",
            FontFamily = new FontFamily("Segoe MDL2 Assets"),
            FontSize = 14,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(0, 0, 8, 0)
        };
        searchIcon.SetResourceReference(TextBlock.ForegroundProperty, "CA_ToolCallForegroundBrush");
        Grid.SetColumn(searchIcon, 0);

        _searchBox = new TextBox
        {
            FontSize = 14,
            BorderThickness = new Thickness(0),
            Background = Brushes.Transparent,
            VerticalAlignment = VerticalAlignment.Center
        };
        _searchBox.SetResourceReference(TextBox.ForegroundProperty, "DockMenuForegroundBrush");
        _searchBox.SetResourceReference(TextBox.CaretBrushProperty, "DockMenuForegroundBrush");
        _searchBox.TextChanged += OnSearchChanged;
        Grid.SetColumn(_searchBox, 1);

        searchGrid.Children.Add(searchIcon);
        searchGrid.Children.Add(_searchBox);
        searchBorder.Child = searchGrid;
        Grid.SetRow(searchBorder, 0);

        // Results list
        _listBox = new ListBox
        {
            BorderThickness = new Thickness(0),
            Background = Brushes.Transparent,
            Padding = new Thickness(4)
        };
        VirtualizingPanel.SetIsVirtualizing(_listBox, true);
        VirtualizingPanel.SetVirtualizationMode(_listBox, VirtualizationMode.Recycling);

        // Item template
        var factory = new FrameworkElementFactory(typeof(StackPanel));
        factory.SetValue(StackPanel.OrientationProperty, Orientation.Horizontal);
        factory.SetValue(StackPanel.MarginProperty, new Thickness(4, 3, 4, 3));

        var iconFactory = new FrameworkElementFactory(typeof(TextBlock));
        iconFactory.SetValue(TextBlock.FontFamilyProperty, new FontFamily("Segoe MDL2 Assets"));
        iconFactory.SetValue(TextBlock.FontSizeProperty, 12.0);
        iconFactory.SetValue(TextBlock.WidthProperty, 20.0);
        iconFactory.SetValue(TextBlock.VerticalAlignmentProperty, VerticalAlignment.Center);
        iconFactory.SetBinding(TextBlock.TextProperty, new System.Windows.Data.Binding("IconGlyph"));
        iconFactory.SetResourceReference(TextBlock.ForegroundProperty, "CA_AccentBrandingBrush");
        factory.AppendChild(iconFactory);

        var nameFactory = new FrameworkElementFactory(typeof(TextBlock));
        nameFactory.SetValue(TextBlock.FontSizeProperty, 12.5);
        nameFactory.SetValue(TextBlock.VerticalAlignmentProperty, VerticalAlignment.Center);
        nameFactory.SetBinding(TextBlock.TextProperty, new System.Windows.Data.Binding("Name"));
        nameFactory.SetResourceReference(TextBlock.ForegroundProperty, "DockMenuForegroundBrush");
        factory.AppendChild(nameFactory);

        _listBox.ItemTemplate = new DataTemplate { VisualTree = factory };

        // Item container style (hover/selection)
        var itemStyle = new Style(typeof(ListBoxItem));
        itemStyle.Setters.Add(new Setter(ListBoxItem.PaddingProperty, new Thickness(4, 2, 4, 2)));
        itemStyle.Setters.Add(new Setter(ListBoxItem.CursorProperty, Cursors.Hand));
        var hoverTrigger = new Trigger { Property = ListBoxItem.IsMouseOverProperty, Value = true };
        hoverTrigger.Setters.Add(new Setter(ListBoxItem.BackgroundProperty, new DynamicResourceExtension("DockTabHoverBrush")));
        itemStyle.Triggers.Add(hoverTrigger);
        var selTrigger = new Trigger { Property = ListBoxItem.IsSelectedProperty, Value = true };
        selTrigger.Setters.Add(new Setter(ListBoxItem.BackgroundProperty, new DynamicResourceExtension("CA_AccentBrandingBrush")));
        itemStyle.Triggers.Add(selTrigger);
        _listBox.ItemContainerStyle = itemStyle;

        _listBox.MouseDoubleClick += OnItemDoubleClick;
        Grid.SetRow(_listBox, 1);

        grid.Children.Add(searchBorder);
        grid.Children.Add(_listBox);
        rootBorder.Child = grid;
        Content = rootBorder;

        // Keyboard
        PreviewKeyDown += OnPreviewKeyDown;
        Deactivated += (_, _) =>
        {
            if (!_closing)
                Dispatcher.BeginInvoke(new Action(SafeClose));
        };
        Loaded += (_, _) => { _searchBox.Focus(); ApplyFilter(""); };
    }

    private void OnSearchChanged(object sender, TextChangedEventArgs e)
        => ApplyFilter(_searchBox.Text);

    private void ApplyFilter(string query)
    {
        _filteredEntries.Clear();
        query = query.Trim();

        if (string.IsNullOrEmpty(query))
            _filteredEntries.AddRange(_allEntries);
        else
            _filteredEntries.AddRange(_allEntries.Where(e =>
                e.Name.Contains(query, StringComparison.OrdinalIgnoreCase)));

        _listBox.ItemsSource = null;
        _listBox.ItemsSource = _filteredEntries;
        if (_filteredEntries.Count > 0)
            _listBox.SelectedIndex = 0;
    }

    private void OnPreviewKeyDown(object sender, KeyEventArgs e)
    {
        switch (e.Key)
        {
            case Key.Escape:
                SafeClose();
                e.Handled = true;
                break;
            case Key.Enter:
                ExecuteSelected();
                e.Handled = true;
                break;
            case Key.Down:
                if (_listBox.SelectedIndex < _filteredEntries.Count - 1)
                    _listBox.SelectedIndex++;
                _listBox.ScrollIntoView(_listBox.SelectedItem);
                e.Handled = true;
                break;
            case Key.Up:
                if (_listBox.SelectedIndex > 0)
                    _listBox.SelectedIndex--;
                _listBox.ScrollIntoView(_listBox.SelectedItem);
                e.Handled = true;
                break;
        }
    }

    private void OnItemDoubleClick(object sender, MouseButtonEventArgs e) => ExecuteSelected();

    private void SafeClose()
    {
        if (_closing) return;
        _closing = true;
        Close();
    }

    private void ExecuteSelected()
    {
        if (_listBox.SelectedItem is ClaudeCommandEntry entry)
        {
            SafeClose();
            entry.Execute();
        }
    }

    /// <summary>Builds the default command catalog for the Claude palette.</summary>
    public static List<ClaudeCommandEntry> BuildDefaultCatalog(
        Action explainSelection,
        Action fixErrors,
        Action refactorSelection,
        Action generateTests,
        Action addDocs,
        Action newTab,
        Action showHistory,
        Action openOptions,
        Action? switchModel = null,
        string? currentModel = null,
        IReadOnlyList<PromptPreset>? presets = null)
    {
        var entries = new List<ClaudeCommandEntry>
        {
            // Model & Account (top of list)
            new("Switch AI Model", currentModel, "\uE8AB", "Model", switchModel ?? (() => { })),
            new("API Key & Provider Settings", null, "\uE8D7", "Model", openOptions),

            // Quick Actions
            new("Explain selected code", "@selection", "\uE946", "Quick Actions", explainSelection),
            new("Fix errors in current file", "@errors", "\uE90F", "Quick Actions", fixErrors),
            new("Refactor with diff preview", "@selection", "\uE70F", "Quick Actions", refactorSelection),
            new("Generate unit tests", "@selection", "\uE9D5", "Quick Actions", generateTests),
            new("Add XML documentation", "@selection", "\uE8A5", "Quick Actions", addDocs),
        };

        if (presets is { Count: > 0 })
        {
            foreach (var p in presets)
                entries.Add(new ClaudeCommandEntry(p.Name, string.Join(", ", p.AutoInjectMentions), p.IconGlyph ?? "\uE945", "Presets", () => { }));
        }

        entries.Add(new("New conversation tab", null, "\uE710", "Actions", newTab));
        entries.Add(new("History", null, "\uE81C", "Actions", showHistory));
        entries.Add(new("Options...", null, "\uE713", "Actions", openOptions));

        return entries;
    }
}
