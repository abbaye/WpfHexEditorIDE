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
using System.Windows.Media;
using WpfHexEditor.App.Properties;
using WpfHexEditor.Core.Commands;
using WpfHexEditor.Core.Options;

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

        // Merge DialogStyles locally so KSP_* keys survive ApplyTheme() clearing App resources.
        // DynamicResource references in those styles resolve upward to Application.Resources (theme brushes).
        Resources.MergedDictionaries.Add(new ResourceDictionary
        {
            Source = new Uri(
                "pack://application:,,,/WpfHexEditor.App;component/Themes/DialogStyles.xaml")
        });

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

        // ClearValue so TransferProperty calls row.ClearValue(Background) instead of SetCurrentValue.
        // SetCurrentValue is at WPF precedence level 4 (above style triggers at level 5) — it would
        // prevent the IsSelected trigger from firing.  With UnsetValue, ClearValue is called on each
        // row and the style trigger wins cleanly.
        _grid.ClearValue(DataGrid.RowBackgroundProperty);
        _grid.ClearValue(DataGrid.AlternatingRowBackgroundProperty);

        // Category column
        var catCol = new DataGridTextColumn
        {
            Header  = AppResources.App_Options_KbCategory,
            Binding = new Binding(nameof(ShortcutRow.Category)),
            Width   = new DataGridLength(110),
            IsReadOnly = true,
        };
        catCol.ElementStyle = MakeTextStyle("KSP_CellForeground");
        _grid.Columns.Add(catCol);

        // Command name column
        var nameCol = new DataGridTextColumn
        {
            Header  = AppResources.App_Options_KbCommand,
            Binding = new Binding(nameof(ShortcutRow.Name)),
            Width   = new DataGridLength(1, DataGridLengthUnitType.Star),
            IsReadOnly = true,
        };
        nameCol.ElementStyle = MakeTextStyle("KSP_CellForeground");
        _grid.Columns.Add(nameCol);

        // Default gesture column
        var defCol = new DataGridTextColumn
        {
            Header  = AppResources.App_Options_KbDefault,
            Binding = new Binding(nameof(ShortcutRow.DefaultGesture)),
            Width   = new DataGridLength(130),
            IsReadOnly = true,
        };
        defCol.ElementStyle = MakeTextStyle("KSP_HintForeground");
        _grid.Columns.Add(defCol);

        // Current gesture column (key-capture template)
        var currentGestureCol = new DataGridTemplateColumn
        {
            Header = AppResources.App_Options_KbShortcut,
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

        // Assign CellStyle per column as a local value (more reliable than DataGrid.CellStyle
        // which travels through an unreliable SetCurrentValue propagation path).
        var cellStyle = BuildCellStyle();
        catCol.CellStyle          = cellStyle;
        nameCol.CellStyle         = cellStyle;
        defCol.CellStyle          = cellStyle;
        currentGestureCol.CellStyle = cellStyle;
        resetCol.CellStyle        = cellStyle;

        // Group header rows (category separators)
        _grid.GroupStyle.Add(BuildGroupStyle());

        Grid.SetRow(_grid, 1);

        root.Children.Add(toolbar);
        root.Children.Add(_grid);
        Content = root;
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

    private static Style BuildCellStyle()
    {
        // Minimal template — Border uses TemplateBinding Background exclusively.
        // No InactiveSelectionHighlight trigger: IsSelected=True fires for both
        // focused and unfocused states, keeping the accent brush in both cases.
        var border = new FrameworkElementFactory(typeof(Border));
        border.SetBinding(Border.BackgroundProperty,
            new Binding { RelativeSource = RelativeSource.TemplatedParent,
                          Path = new PropertyPath(Control.BackgroundProperty) });
        border.SetBinding(Border.BorderBrushProperty,
            new Binding { RelativeSource = RelativeSource.TemplatedParent,
                          Path = new PropertyPath(Control.BorderBrushProperty) });
        border.SetBinding(Border.BorderThicknessProperty,
            new Binding { RelativeSource = RelativeSource.TemplatedParent,
                          Path = new PropertyPath(Control.BorderThicknessProperty) });
        border.SetValue(UIElement.SnapsToDevicePixelsProperty, true);

        var cp = new FrameworkElementFactory(typeof(ContentPresenter));
        cp.SetBinding(UIElement.SnapsToDevicePixelsProperty,
            new Binding { RelativeSource = RelativeSource.TemplatedParent,
                          Path = new PropertyPath(UIElement.SnapsToDevicePixelsProperty) });
        border.AppendChild(cp);

        var template = new ControlTemplate(typeof(DataGridCell)) { VisualTree = border };

        var style = new Style(typeof(DataGridCell));
        style.Setters.Add(new Setter(Control.BorderThicknessProperty, new Thickness(0)));
        style.Setters.Add(new Setter(Control.TemplateProperty, template));

        var trigger = new Trigger { Property = DataGridCell.IsSelectedProperty, Value = true };
        trigger.Setters.Add(new Setter(Control.BackgroundProperty,
            new DynamicResourceExtension("DockAccentBrush")));
        trigger.Setters.Add(new Setter(Control.ForegroundProperty, Brushes.White));
        style.Triggers.Add(trigger);

        return style;
    }

    private GroupStyle BuildGroupStyle()
    {
        // Category label — DataContext of GroupItem is CollectionViewGroup,
        // so Binding("Name") resolves to the category string (e.g. "Build").
        var labelFactory = new FrameworkElementFactory(typeof(TextBlock));
        labelFactory.SetBinding(TextBlock.TextProperty, new Binding("Name"));
        labelFactory.SetValue(TextBlock.FontWeightProperty, FontWeights.SemiBold);
        labelFactory.SetValue(TextBlock.FontSizeProperty, 11.0);
        labelFactory.SetValue(TextBlock.PaddingProperty, new Thickness(8, 5, 4, 5));
        labelFactory.SetResourceReference(TextBlock.ForegroundProperty, "KSP_CellForeground");

        var headerBorder = new FrameworkElementFactory(typeof(Border));
        headerBorder.SetResourceReference(Border.BackgroundProperty, "DockMenuBackgroundBrush");
        headerBorder.AppendChild(labelFactory);

        var itemsPresenter = new FrameworkElementFactory(typeof(ItemsPresenter));

        var rootPanel = new FrameworkElementFactory(typeof(StackPanel));
        rootPanel.AppendChild(headerBorder);
        rootPanel.AppendChild(itemsPresenter);

        var template = new ControlTemplate(typeof(GroupItem)) { VisualTree = rootPanel };

        var containerStyle = new Style(typeof(GroupItem));
        containerStyle.Setters.Add(new Setter(Control.TemplateProperty, template));

        return new GroupStyle { ContainerStyle = containerStyle };
    }

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
        // Key-capture cell: user presses the desired key combination rather than typing text.
        // IsReadOnly=true prevents free typing.  Conflict detection scans _rows inline.
        var page    = this;
        var factory = new FrameworkElementFactory(typeof(TextBox));
        factory.SetValue(TextBox.IsReadOnlyProperty,      true);
        factory.SetValue(TextBox.TextProperty,            "Press shortcut\u2026");
        factory.SetValue(TextBox.PaddingProperty,         new Thickness(4, 2, 4, 2));
        factory.SetValue(TextBox.BorderThicknessProperty, new Thickness(1));
        factory.SetResourceReference(TextBox.BackgroundProperty,  "KSP_EditBackground");
        factory.SetResourceReference(TextBox.ForegroundProperty,  "KSP_HintForeground");
        factory.SetResourceReference(TextBox.BorderBrushProperty, "KSP_EditBorder");

        factory.AddHandler(UIElement.PreviewKeyDownEvent, new KeyEventHandler((s, e) =>
        {
            if (s is not TextBox tb) return;
            var key = e.Key == Key.System ? e.SystemKey : e.Key;

            // Ignore standalone modifier keys — don't let them move DataGrid row focus.
            if (key is Key.LeftCtrl  or Key.RightCtrl  or Key.LeftShift or Key.RightShift
                     or Key.LeftAlt  or Key.RightAlt   or Key.LWin      or Key.RWin
                     or Key.None) { e.Handled = true; return; }

            // Escape → pass through so DataGrid's default cancel-edit fires.
            if (key == Key.Escape) return;

            // Tab → pass through for DataGrid cell navigation (not a capturable gesture).
            if (key == Key.Tab) return;

            // Enter with a stashed conflict gesture → confirm override.
            if (key == Key.Return && tb.Tag is string pending
                && tb.DataContext is ShortcutRow confirmRow)
            {
                page.CommitGesture(confirmRow, pending);
                page._grid.CommitEdit(DataGridEditingUnit.Cell, exitEditingMode: true);
                e.Handled = true; return;
            }

            // Plain Enter (no modifiers, no pending conflict) → exit edit without capturing.
            if (key == Key.Return && Keyboard.Modifiers == ModifierKeys.None && tb.Tag is null)
            {
                page._grid.CommitEdit(DataGridEditingUnit.Cell, exitEditingMode: true);
                e.Handled = true; return;
            }

            // All other keys (Del, Backspace, F-keys, arrows, alphanumeric…) fall through to capture.

            // Build gesture string from current modifier state + pressed key.
            var mods  = Keyboard.Modifiers;
            var parts = new System.Collections.Generic.List<string>(4);
            if ((mods & ModifierKeys.Control) != 0) parts.Add("Ctrl");
            if ((mods & ModifierKeys.Shift)   != 0) parts.Add("Shift");
            if ((mods & ModifierKeys.Alt)     != 0) parts.Add("Alt");
            parts.Add(key.ToString());
            var gesture = string.Join("+", parts);

            tb.SetResourceReference(TextBox.ForegroundProperty, "KSP_CellForeground");
            tb.Text = gesture;
            tb.Tag  = null;

            if (tb.DataContext is ShortcutRow row)
            {
                var conflict = page._rows.FirstOrDefault(r =>
                    r.CommandId != row.CommandId &&
                    string.Equals(r.CurrentGesture, gesture, StringComparison.OrdinalIgnoreCase));

                if (conflict != null)
                {
                    tb.BorderBrush = Brushes.IndianRed;
                    tb.ToolTip     = $"\u26a0 Also used by: {conflict.Category} \u203a {conflict.Name}. Enter to override, Esc to cancel.";
                    tb.Tag = gesture;   // stash so Enter-path above can confirm
                    e.Handled = true; return;
                }

                page.CommitGesture(row, gesture);
                page._grid.CommitEdit(DataGridEditingUnit.Cell, exitEditingMode: true);
            }
            e.Handled = true;
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
