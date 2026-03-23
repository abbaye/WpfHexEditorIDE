//////////////////////////////////////////////
// Project      : WpfHexEditor.App
// File         : Options/KeyboardShortcutsPage.cs
// Description  : Options page for viewing and customising keyboard gesture
//                bindings for all registered IDE commands.
// Architecture : Code-behind-only UserControl implementing IOptionsPage.
//                Receives ICommandRegistry + IKeyBindingService via constructor
//                (injected from MainWindow.Loaded — avoids circular project ref).
//                Each override is saved immediately through KeyBindingService
//                which auto-persists to AppSettings.KeyBindingOverrides.
//////////////////////////////////////////////

using System;
using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using WpfHexEditor.Commands;
using WpfHexEditor.Options;

namespace WpfHexEditor.App.Options;

/// <summary>
/// IDE options page — Keyboard Shortcuts.
/// Shows all registered commands in a searchable DataGrid;
/// lets the user override the gesture per command and reset to default.
/// </summary>
public sealed class KeyboardShortcutsPage : UserControl, IOptionsPage
{
    // -----------------------------------------------------------------------
    // Dependencies
    // -----------------------------------------------------------------------

    private readonly ICommandRegistry   _registry;
    private readonly IKeyBindingService _bindings;

    // -----------------------------------------------------------------------
    // UI elements (built programmatically — no XAML file)
    // -----------------------------------------------------------------------

    private readonly TextBox                             _searchBox;
    private readonly DataGrid                           _grid;
    private readonly ObservableCollection<ShortcutRow>  _rows = [];
    private          CollectionViewSource               _viewSource = new();

    // -----------------------------------------------------------------------
    // IOptionsPage
    // -----------------------------------------------------------------------

    public event EventHandler? Changed;

    // -----------------------------------------------------------------------
    // Constructor
    // -----------------------------------------------------------------------

    public KeyboardShortcutsPage(ICommandRegistry registry, IKeyBindingService bindings)
    {
        _registry = registry;
        _bindings = bindings;

        // Root layout
        var root = new Grid();
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });

        // ── Toolbar row ────────────────────────────────────────────────────
        var toolbar = new Grid { Margin = new Thickness(0, 0, 0, 6) };
        toolbar.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        toolbar.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        _searchBox = new TextBox
        {
            Margin = new Thickness(0, 0, 8, 0),
            VerticalContentAlignment = VerticalAlignment.Center,
        };
        _searchBox.SetResourceReference(TextBox.StyleProperty, "KSP_SearchBoxStyle");
        _searchBox.TextChanged += OnSearchChanged;
        Grid.SetColumn(_searchBox, 0);

        var resetAllBtn = new Button { Content = "↺  Reset All", Padding = new Thickness(8, 4, 8, 4) };
        resetAllBtn.SetResourceReference(Button.StyleProperty, "KSP_ResetButtonStyle");
        resetAllBtn.Click += OnResetAll;
        Grid.SetColumn(resetAllBtn, 1);

        toolbar.Children.Add(_searchBox);
        toolbar.Children.Add(resetAllBtn);
        Grid.SetRow(toolbar, 0);

        // ── DataGrid ───────────────────────────────────────────────────────
        _grid = new DataGrid
        {
            AutoGenerateColumns    = false,
            IsReadOnly             = false,
            CanUserAddRows         = false,
            CanUserDeleteRows      = false,
            CanUserResizeRows      = false,
            SelectionMode          = DataGridSelectionMode.Single,
            HeadersVisibility      = DataGridHeadersVisibility.Column,
            GridLinesVisibility    = DataGridGridLinesVisibility.Horizontal,
            RowHeaderWidth         = 0,
            Margin                 = new Thickness(0),
        };
        _grid.SetResourceReference(DataGrid.StyleProperty,     "KSP_DataGridStyle");
        _grid.SetResourceReference(DataGrid.RowStyleProperty,  "KSP_DataGridRowStyle");

        // Category column
        var catCol = new DataGridTextColumn
        {
            Header  = "Category",
            Binding = new Binding(nameof(ShortcutRow.Category)),
            Width   = new DataGridLength(110),
            IsReadOnly = true,
        };
        catCol.ElementStyle = MakeTextStyle("KSP_CellForeground");
        _grid.Columns.Add(catCol);

        // Command name column
        var nameCol = new DataGridTextColumn
        {
            Header  = "Command",
            Binding = new Binding(nameof(ShortcutRow.Name)),
            Width   = new DataGridLength(1, DataGridLengthUnitType.Star),
            IsReadOnly = true,
        };
        nameCol.ElementStyle = MakeTextStyle("KSP_CellForeground");
        _grid.Columns.Add(nameCol);

        // Default gesture column
        var defCol = new DataGridTextColumn
        {
            Header  = "Default",
            Binding = new Binding(nameof(ShortcutRow.DefaultGesture)),
            Width   = new DataGridLength(130),
            IsReadOnly = true,
        };
        defCol.ElementStyle = MakeTextStyle("KSP_HintForeground");
        _grid.Columns.Add(defCol);

        // Current gesture column (editable TextBox template)
        var currentGestureCol = new DataGridTemplateColumn
        {
            Header = "Shortcut",
            Width  = new DataGridLength(150),
        };
        currentGestureCol.CellTemplate        = BuildGestureDisplayTemplate();
        currentGestureCol.CellEditingTemplate  = BuildGestureEditTemplate();
        _grid.Columns.Add(currentGestureCol);

        // Reset button column
        var resetCol = new DataGridTemplateColumn
        {
            Header = "",
            Width  = new DataGridLength(36),
        };
        resetCol.CellTemplate = BuildResetButtonTemplate();
        _grid.Columns.Add(resetCol);

        Grid.SetRow(_grid, 1);

        // Mouse-wheel scroll — wire after template is applied so DG_ScrollViewer exists
        _grid.Loaded += OnGridLoaded;

        root.Children.Add(toolbar);
        root.Children.Add(_grid);
        Content = root;
    }

    private void OnGridLoaded(object sender, RoutedEventArgs e)
    {
        if (_grid.Template?.FindName("DG_ScrollViewer", _grid) is ScrollViewer sv)
            _grid.PreviewMouseWheel += (_, we) =>
            {
                sv.ScrollToVerticalOffset(sv.VerticalOffset - we.Delta / 3.0);
                we.Handled = true;
            };
    }

    // -----------------------------------------------------------------------
    // IOptionsPage — Load / Flush
    // -----------------------------------------------------------------------

    public void Load(AppSettings _)
    {
        _rows.Clear();
        foreach (var cmd in _registry.GetAll())
        {
            _rows.Add(new ShortcutRow(cmd.Id, cmd.Name, cmd.Category,
                                       cmd.DefaultGesture,
                                       _bindings.ResolveGesture(cmd.Id)));
        }

        _viewSource = new CollectionViewSource { Source = _rows };
        _viewSource.GroupDescriptions.Add(new PropertyGroupDescription(nameof(ShortcutRow.Category)));
        _grid.ItemsSource = _viewSource.View;
    }

    /// <remarks>
    /// Gestures are saved immediately via <see cref="IKeyBindingService.SetOverride"/>
    /// as the user confirms each edit — nothing left to flush.
    /// </remarks>
    public void Flush(AppSettings _) { }

    // -----------------------------------------------------------------------
    // Event handlers
    // -----------------------------------------------------------------------

    private void OnSearchChanged(object sender, TextChangedEventArgs e)
    {
        var q = _searchBox.Text.Trim();
        _viewSource.View.Filter = string.IsNullOrEmpty(q)
            ? null
            : (obj => obj is ShortcutRow r &&
               (r.Name.Contains(q, StringComparison.OrdinalIgnoreCase) ||
                r.Category.Contains(q, StringComparison.OrdinalIgnoreCase)));
    }

    private void OnResetAll(object sender, RoutedEventArgs e)
    {
        _bindings.ResetAll();
        foreach (var row in _rows)
            row.CurrentGesture = row.DefaultGesture;
        Changed?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>Called by the Reset (↩) button in each row.</summary>
    internal void ResetRow(ShortcutRow row)
    {
        _bindings.ResetOverride(row.CommandId);
        row.CurrentGesture = row.DefaultGesture;
        Changed?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>Called when the user confirms an inline gesture edit.</summary>
    internal void CommitGesture(ShortcutRow row, string? gesture)
    {
        var norm = string.IsNullOrWhiteSpace(gesture) ? null : gesture.Trim();
        _bindings.SetOverride(row.CommandId, norm);
        row.CurrentGesture = _bindings.ResolveGesture(row.CommandId);
        Changed?.Invoke(this, EventArgs.Empty);
    }

    // -----------------------------------------------------------------------
    // Template helpers
    // -----------------------------------------------------------------------

    private DataTemplate BuildGestureDisplayTemplate()
    {
        var factory = new FrameworkElementFactory(typeof(TextBlock));
        factory.SetBinding(TextBlock.TextProperty,
            new Binding(nameof(ShortcutRow.CurrentGesture)));
        factory.SetValue(TextBlock.MarginProperty, new Thickness(4, 0, 4, 0));
        factory.SetValue(TextBlock.VerticalAlignmentProperty, VerticalAlignment.Center);
        factory.SetResourceReference(TextBlock.ForegroundProperty, "KSP_CellForeground");
        return new DataTemplate { VisualTree = factory };
    }

    private DataTemplate BuildGestureEditTemplate()
    {
        var factory = new FrameworkElementFactory(typeof(TextBox));
        factory.SetBinding(TextBox.TextProperty,
            new Binding(nameof(ShortcutRow.CurrentGesture)) { UpdateSourceTrigger = UpdateSourceTrigger.LostFocus });
        factory.SetValue(TextBox.PaddingProperty, new Thickness(4, 2, 4, 2));
        factory.SetValue(TextBox.BorderThicknessProperty, new Thickness(1));
        factory.SetResourceReference(TextBox.BackgroundProperty,  "KSP_EditBackground");
        factory.SetResourceReference(TextBox.ForegroundProperty,  "KSP_CellForeground");
        factory.SetResourceReference(TextBox.BorderBrushProperty, "KSP_EditBorder");

        // Commit when Enter is pressed
        factory.AddHandler(UIElement.KeyDownEvent, new KeyEventHandler((s, e) =>
        {
            if (e.Key != Key.Return) return;
            if (s is TextBox tb && tb.DataContext is ShortcutRow row)
            {
                CommitGesture(row, tb.Text);
                _grid.CommitEdit(DataGridEditingUnit.Cell, exitEditingMode: true);
            }
        }));

        return new DataTemplate { VisualTree = factory };
    }

    private DataTemplate BuildResetButtonTemplate()
    {
        // Store reference so lambda can call ResetRow
        var page = this;
        var factory = new FrameworkElementFactory(typeof(Button));
        factory.SetValue(Button.ContentProperty, "↩");
        factory.SetValue(Button.PaddingProperty, new Thickness(4, 2, 4, 2));
        factory.SetValue(Button.ToolTipProperty, "Reset to default");
        factory.SetResourceReference(Button.StyleProperty, "KSP_ResetButtonStyle");
        factory.AddHandler(Button.ClickEvent, new RoutedEventHandler((s, _) =>
        {
            if (s is Button btn && btn.DataContext is ShortcutRow row)
                page.ResetRow(row);
        }));
        return new DataTemplate { VisualTree = factory };
    }

    private static Style MakeTextStyle(string foregroundKey)
    {
        var style = new Style(typeof(TextBlock));
        style.Setters.Add(new Setter(TextBlock.VerticalAlignmentProperty, VerticalAlignment.Center));
        style.Setters.Add(new Setter(TextBlock.MarginProperty, new Thickness(4, 0, 4, 0)));
        // Foreground via resource reference (can't use SetResourceReference in Style.Setters directly,
        // but DynamicResourceExtension works for Setter.Value).
        style.Setters.Add(new Setter(TextBlock.ForegroundProperty,
            new DynamicResourceExtension(foregroundKey)));
        return style;
    }
}

// -----------------------------------------------------------------------
// Row model
// -----------------------------------------------------------------------

/// <summary>Observable row model for the keyboard shortcuts DataGrid.</summary>
public sealed class ShortcutRow : System.ComponentModel.INotifyPropertyChanged
{
    private string? _currentGesture;

    public string  CommandId       { get; }
    public string  Name            { get; }
    public string  Category        { get; }
    public string? DefaultGesture  { get; }

    public string? CurrentGesture
    {
        get => _currentGesture;
        set { _currentGesture = value; PropertyChanged?.Invoke(this, new System.ComponentModel.PropertyChangedEventArgs(nameof(CurrentGesture))); }
    }

    public event System.ComponentModel.PropertyChangedEventHandler? PropertyChanged;

    public ShortcutRow(string commandId, string name, string category,
                       string? defaultGesture, string? currentGesture)
    {
        CommandId      = commandId;
        Name           = name;
        Category       = category;
        DefaultGesture = defaultGesture;
        _currentGesture = currentGesture;
    }
}
