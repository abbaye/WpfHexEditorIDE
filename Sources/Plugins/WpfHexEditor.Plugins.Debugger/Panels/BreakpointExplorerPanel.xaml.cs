// ==========================================================
// Project: WpfHexEditor.Plugins.Debugger
// File: Panels/BreakpointExplorerPanel.xaml.cs
// Description:
//     Code-behind for the VS-style Breakpoint Explorer panel.
//     Delegates all logic to BreakpointExplorerViewModel.
//     Uses ToolbarOverflowManager for dynamic toolbar overflow.
//     Detail panel (splitter + detail border) supports Right/Bottom/Hidden layouts.
// ==========================================================

using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Threading;
using WpfHexEditor.Plugins.Debugger.ViewModels;
using WpfHexEditor.SDK.Contracts.Services;
using WpfHexEditor.SDK.UI;

namespace WpfHexEditor.Plugins.Debugger.Panels;

public partial class BreakpointExplorerPanel : UserControl
{
    private BreakpointExplorerViewModel? Vm => DataContext as BreakpointExplorerViewModel;
    private ToolbarOverflowManager? _overflowManager;
    private IUIControlFactory? _uiFactory;

    internal IUIControlFactory? UIFactory
    {
        set
        {
            _uiFactory = value;
            // If already loaded (panel was attached before factory arrived), wire up immediately.
            if (IsLoaded)
                Dispatcher.InvokeAsync(() => { EnsurePreviewControl(); UpdatePreview(); },
                    DispatcherPriority.Loaded);
        }
    }

    private void EnsurePreviewControl()
    {
        if (_uiFactory is null || CodePreviewBorder is null || _codePreviewControl is not null) return;
        _codePreviewControl = _uiFactory.CreateSyntaxPreview(contextLines: 2, fontSize: 12, highlightFocusLine: true);
        _codePreviewControl.HorizontalAlignment = HorizontalAlignment.Stretch;
        CodePreviewBorder.Child = _codePreviewControl;
    }

    private FrameworkElement? _codePreviewControl;

    public BreakpointExplorerPanel()
    {
        InitializeComponent();

        _overflowManager = new ToolbarOverflowManager(
            toolbarContainer:      ToolbarBorder,
            alwaysVisiblePanel:    ToolbarRightPanel,
            overflowButton:        OverflowButton,
            overflowMenu:          OverflowMenu,
            groupsInCollapseOrder: [TbgGroupBy, TbgImportExport, TbgActions, TbgLayout],
            leftFixedElements:     [ToolbarLeftPanel]);

        Dispatcher.InvokeAsync(_overflowManager.CaptureNaturalWidths, DispatcherPriority.Loaded);

        DataContextChanged += OnDataContextChanged;
        Loaded += (_, _) =>
        {
            EnsurePreviewControl();
            UpdatePreview();
            ApplyLayout();
            ApplyDetailVisibility();
        };
    }

    // ── DataContext wiring ────────────────────────────────────────────────────

    private BreakpointExplorerViewModel? _subscribedVm;

    private void OnDataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        if (_subscribedVm is not null)
            _subscribedVm.PropertyChanged -= OnVmPropertyChanged;

        _subscribedVm = Vm;

        if (_subscribedVm is not null)
            _subscribedVm.PropertyChanged += OnVmPropertyChanged;

        ApplyLayout();
        ApplyDetailVisibility();
        UpdatePreview();
        if (LayoutCombo is not null)
            LayoutCombo.SelectedIndex = (int)(Vm?.DetailLayout ?? DetailPanelLayout.Right);
    }

    private void OnVmPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(BreakpointExplorerViewModel.SelectedBreakpoint))
        {
            ApplyDetailVisibility();
            UpdatePreview();
        }
    }

    private void UpdatePreview()
    {
        if (_codePreviewControl is null || _uiFactory is null) return;
        var bp = Vm?.SelectedBreakpoint;
        if (bp is null)
        {
            _uiFactory.SetPreviewSource(_codePreviewControl, null, null);
            return;
        }
        int focusIndex = Math.Min(2, Math.Max(0, bp.Line - 1));
        _uiFactory.SetPreviewSource(_codePreviewControl, bp.SourcePreview, bp.SourceLanguageId, focusIndex);
    }

    /// <summary>Shows or hides the splitter + detail columns based on selection state.</summary>
    private void ApplyDetailVisibility()
    {
        var hasSelection = Vm?.SelectedBreakpoint is not null;
        var layout = Vm?.DetailLayout ?? DetailPanelLayout.Right;

        if (layout == DetailPanelLayout.Hidden || !hasSelection)
        {
            SplitterCol.Width  = new GridLength(0);
            DetailCol.Width    = new GridLength(0);
            SplitterRow.Height = new GridLength(0);
            DetailRow.Height   = new GridLength(0);
        }
        else
        {
            ApplyLayout(); // restore widths for current layout
        }

        // Show the code preview only when the source file exists on disk.
        if (CodePreviewBorder is not null)
        {
            var fp = Vm?.SelectedBreakpoint?.FilePath;
            CodePreviewBorder.Visibility =
                !string.IsNullOrEmpty(fp) && System.IO.File.Exists(fp)
                    ? Visibility.Visible
                    : Visibility.Collapsed;
        }
    }

    // ── Layout management ──────────────────────────────────────────────────────

    /// <summary>
    /// Positions DetailSplitter and DetailBorder inside ContentGrid
    /// based on Vm.DetailLayout (Right / Bottom / Hidden).
    /// Manipulates RowDefinitions / ColumnDefinitions and Grid attached properties.
    /// </summary>
    private void ApplyLayout()
    {
        var layout = Vm?.DetailLayout ?? DetailPanelLayout.Right;

        switch (layout)
        {
            case DetailPanelLayout.Right:
                // Columns: list | 5px splitter | 220 detail
                MainCol.Width     = new GridLength(1, GridUnitType.Star);
                SplitterCol.Width = new GridLength(5);
                DetailCol.Width   = new GridLength(350);
                // Rows: single row for everything
                MainRow.Height     = new GridLength(1, GridUnitType.Star);
                SplitterRow.Height = new GridLength(0);
                DetailRow.Height   = new GridLength(0);

                // Splitter: vertical (col=1)
                Grid.SetRow(DetailSplitter, 0);        Grid.SetRowSpan(DetailSplitter, 1);
                Grid.SetColumn(DetailSplitter, 1);     Grid.SetColumnSpan(DetailSplitter, 1);
                DetailSplitter.Width  = 5;
                DetailSplitter.Height = double.NaN;
                DetailSplitter.HorizontalAlignment = HorizontalAlignment.Stretch;
                DetailSplitter.VerticalAlignment   = VerticalAlignment.Stretch;

                // Detail panel: col=2
                Grid.SetRow(DetailBorder, 0);          Grid.SetRowSpan(DetailBorder, 1);
                Grid.SetColumn(DetailBorder, 2);       Grid.SetColumnSpan(DetailBorder, 1);
                DetailBorder.BorderThickness = new Thickness(1, 0, 0, 0);
                break;

            case DetailPanelLayout.Bottom:
                // Columns: full width
                MainCol.Width     = new GridLength(1, GridUnitType.Star);
                SplitterCol.Width = new GridLength(0);
                DetailCol.Width   = new GridLength(0);
                // Rows: list | 5px splitter | 180 detail
                MainRow.Height     = new GridLength(1, GridUnitType.Star);
                SplitterRow.Height = new GridLength(5);
                DetailRow.Height   = new GridLength(180);

                // List: span all columns
                Grid.SetColumnSpan(FlatList, 3);
                Grid.SetColumnSpan(GroupedTree, 3);

                // Splitter: horizontal (row=1)
                Grid.SetRow(DetailSplitter, 1);        Grid.SetRowSpan(DetailSplitter, 1);
                Grid.SetColumn(DetailSplitter, 0);     Grid.SetColumnSpan(DetailSplitter, 3);
                DetailSplitter.Width  = double.NaN;
                DetailSplitter.Height = 5;
                DetailSplitter.HorizontalAlignment = HorizontalAlignment.Stretch;
                DetailSplitter.VerticalAlignment   = VerticalAlignment.Stretch;

                // Detail panel: row=2, span all cols
                Grid.SetRow(DetailBorder, 2);          Grid.SetRowSpan(DetailBorder, 1);
                Grid.SetColumn(DetailBorder, 0);       Grid.SetColumnSpan(DetailBorder, 3);
                DetailBorder.BorderThickness = new Thickness(0, 1, 0, 0);
                break;

            case DetailPanelLayout.Hidden:
                // Hide splitter and detail, list takes full space
                SplitterCol.Width  = new GridLength(0);
                DetailCol.Width    = new GridLength(0);
                SplitterRow.Height = new GridLength(0);
                DetailRow.Height   = new GridLength(0);
                MainRow.Height     = new GridLength(1, GridUnitType.Star);
                MainCol.Width      = new GridLength(1, GridUnitType.Star);

                Grid.SetColumnSpan(FlatList, 3);
                Grid.SetColumnSpan(GroupedTree, 3);
                break;
        }

        // Reset list column span for Right layout
        if (layout == DetailPanelLayout.Right)
        {
            Grid.SetColumnSpan(FlatList, 1);
            Grid.SetColumnSpan(GroupedTree, 1);
        }
    }

    // ── Toolbar overflow ──────────────────────────────────────────────────

    private void OnToolbarSizeChanged(object sender, SizeChangedEventArgs e)
    {
        if (e.WidthChanged) _overflowManager?.Update();
    }

    private void OnOverflowButtonClick(object sender, RoutedEventArgs e)
    {
        OverflowMenu.PlacementTarget = OverflowButton;
        OverflowMenu.Placement       = PlacementMode.Bottom;
        OverflowMenu.IsOpen          = true;
    }

    private void OnOverflowMenuOpened(object sender, RoutedEventArgs e)
    {
        _overflowManager?.SyncMenuVisibility();
    }

    // ── Overflow menu handlers ────────────────────────────────────────────

    private void OnOvfEnableAll(object sender, RoutedEventArgs e)  => Vm?.EnableAllCommand.Execute(null);
    private void OnOvfDisableAll(object sender, RoutedEventArgs e) => Vm?.DisableAllCommand.Execute(null);
    private void OnOvfDeleteAll(object sender, RoutedEventArgs e)  => Vm?.DeleteAllCommand.Execute(null);

    private void OnOvfGroupNone(object sender, RoutedEventArgs e)    { GroupByCombo.SelectedIndex = 0; }
    private void OnOvfGroupFile(object sender, RoutedEventArgs e)    { GroupByCombo.SelectedIndex = 1; }
    private void OnOvfGroupType(object sender, RoutedEventArgs e)    { GroupByCombo.SelectedIndex = 2; }
    private void OnOvfGroupEnabled(object sender, RoutedEventArgs e) { GroupByCombo.SelectedIndex = 3; }
    private void OnOvfGroupProject(object sender, RoutedEventArgs e) { GroupByCombo.SelectedIndex = 4; }

    // ── Layout ComboBox ───────────────────────────────────────────────────

    private void OnLayoutComboChanged(object sender, SelectionChangedEventArgs e)
    {
        if (Vm is null || LayoutCombo.SelectedIndex < 0) return;
        SetLayout((DetailPanelLayout)LayoutCombo.SelectedIndex);
    }

    private void OnOvfLayoutRight(object sender, RoutedEventArgs e)  => SetLayoutCombo(DetailPanelLayout.Right);
    private void OnOvfLayoutBottom(object sender, RoutedEventArgs e) => SetLayoutCombo(DetailPanelLayout.Bottom);
    private void OnOvfLayoutHidden(object sender, RoutedEventArgs e) => SetLayoutCombo(DetailPanelLayout.Hidden);

    private void SetLayoutCombo(DetailPanelLayout layout)
    {
        LayoutCombo.SelectedIndex = (int)layout; // triggers OnLayoutComboChanged
    }

    private void SetLayout(DetailPanelLayout layout)
    {
        if (Vm is null) return;
        Vm.DetailLayout = layout;
        ApplyLayout();
        ApplyDetailVisibility();
    }

    // ── Group-by ComboBox ─────────────────────────────────────────────────

    private void OnGroupByChanged(object sender, SelectionChangedEventArgs e)
    {
        if (Vm is null || GroupByCombo.SelectedIndex < 0) return;
        Vm.GroupBy = (GroupByMode)GroupByCombo.SelectedIndex;
    }

    // ── Flat list double-click → edit condition ───────────────────────────

    private void OnRowDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (Vm?.SelectedBreakpoint is not null)
            Vm.EditConditionCommand.Execute(Vm.SelectedBreakpoint);
    }

    // ── Tree selection → sync SelectedBreakpoint ─────────────────────────

    private void OnTreeSelectionChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
    {
        if (Vm is null) return;
        // Only sync leaf nodes (BreakpointRowEx), not group headers
        if (e.NewValue is BreakpointRowEx row)
            Vm.SelectedBreakpoint = row;
    }

    // ── Tree item double-click → edit condition ───────────────────────────

    private void OnTreeItemDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (e.ClickCount == 2 && sender is FrameworkElement fe && fe.DataContext is BreakpointRowEx row)
        {
            Vm?.EditConditionCommand.Execute(row);
            e.Handled = true;
        }
    }

    // ── Edit condition ────────────────────────────────────────────────────

    private void OnEditCondition(object sender, RoutedEventArgs e)
    {
        var row = GetSelectedRow();
        if (row is not null) Vm?.EditConditionCommand.Execute(row);
    }

    // ── CheckBox enable/disable ───────────────────────────────────────────

    private void OnCheckBoxClicked(object sender, RoutedEventArgs e)
    {
        if (sender is CheckBox cb && cb.Tag is BreakpointRowEx row)
            Vm?.ToggleEnabledCommand.Execute(row);
    }

    // ── Context menu handlers ─────────────────────────────────────────────

    private void OnGoToSource(object sender, RoutedEventArgs e)
    {
        var row = GetSelectedRow();
        if (row is not null) Vm?.GoToSourceCommand.Execute(row);
    }

    private void OnToggleEnabled(object sender, RoutedEventArgs e)
    {
        var row = GetSelectedRow();
        if (row is not null) Vm?.ToggleEnabledCommand.Execute(row);
    }

    private void OnCopyLocation(object sender, RoutedEventArgs e)
    {
        var row = GetSelectedRow();
        if (row is not null) Vm?.CopyLocationCommand.Execute(row);
    }

    private void OnDeleteSelected(object sender, RoutedEventArgs e)
    {
        var row = GetSelectedRow();
        if (row is not null) Vm?.DeleteCommand.Execute(row);
    }

    // ── VS XML Import / Export ────────────────────────────────────────────

    private void OnImportBreakpoints(object sender, RoutedEventArgs e) => Vm?.ImportCommand.Execute(null);
    private void OnExportBreakpoints(object sender, RoutedEventArgs e) => Vm?.ExportCommand.Execute(null);

    // ── Detail panel action buttons ───────────────────────────────────────

    private void OnDetailGoToSource(object sender, RoutedEventArgs e)
    {
        var row = Vm?.SelectedBreakpoint;
        if (row is not null) Vm?.GoToSourceCommand.Execute(row);
    }

    private void OnDetailEditCondition(object sender, RoutedEventArgs e)
    {
        var row = Vm?.SelectedBreakpoint;
        if (row is not null) Vm?.EditConditionCommand.Execute(row);
    }

    private void OnDetailDelete(object sender, RoutedEventArgs e)
    {
        var row = Vm?.SelectedBreakpoint;
        if (row is not null) Vm?.DeleteCommand.Execute(row);
    }

    private void OnDetailToggleEnabled(object sender, RoutedEventArgs e)
    {
        var row = Vm?.SelectedBreakpoint;
        if (row is not null) Vm?.ToggleEnabledCommand.Execute(row);
    }

    // ─────────────────────────────────────────────────────────────────────

    // ── Tree context menu ────────────────────────────────────────────────

    private void OnTreeContextMenuOpened(object sender, RoutedEventArgs e)
    {
        var selected = GroupedTree.SelectedItem;
        var isRow   = selected is BreakpointRowEx;
        var isGroup = selected is BreakpointGroupNode;

        // Row-specific items
        TreeMenuGoToSource.Visibility     = isRow ? Visibility.Visible : Visibility.Collapsed;
        TreeSep1.Visibility               = isRow ? Visibility.Visible : Visibility.Collapsed;
        TreeMenuEditCondition.Visibility   = isRow ? Visibility.Visible : Visibility.Collapsed;
        TreeSep2.Visibility               = isRow ? Visibility.Visible : Visibility.Collapsed;
        TreeMenuToggleEnabled.Visibility   = isRow ? Visibility.Visible : Visibility.Collapsed;
        TreeMenuCopyLocation.Visibility    = isRow ? Visibility.Visible : Visibility.Collapsed;
        TreeSep3.Visibility               = isRow ? Visibility.Visible : Visibility.Collapsed;
        TreeMenuDelete.Visibility          = isRow ? Visibility.Visible : Visibility.Collapsed;

        // Group-specific items
        TreeSepGroup.Visibility            = isGroup ? Visibility.Visible : Visibility.Collapsed;
        TreeMenuGroupEnableAll.Visibility  = isGroup ? Visibility.Visible : Visibility.Collapsed;
        TreeMenuGroupDisableAll.Visibility = isGroup ? Visibility.Visible : Visibility.Collapsed;
        TreeMenuGroupDeleteAll.Visibility  = isGroup ? Visibility.Visible : Visibility.Collapsed;
    }

    private void OnGroupEnableAll(object sender, RoutedEventArgs e)
    {
        if (GroupedTree.SelectedItem is BreakpointGroupNode group)
            foreach (var row in group.Children.ToList())
                if (!row.IsEnabled) Vm?.ToggleEnabledCommand.Execute(row);
    }

    private void OnGroupDisableAll(object sender, RoutedEventArgs e)
    {
        if (GroupedTree.SelectedItem is BreakpointGroupNode group)
            foreach (var row in group.Children.ToList())
                if (row.IsEnabled) Vm?.ToggleEnabledCommand.Execute(row);
    }

    private void OnGroupDeleteAll(object sender, RoutedEventArgs e)
    {
        if (GroupedTree.SelectedItem is BreakpointGroupNode group)
            foreach (var row in group.Children.ToList())
                Vm?.DeleteCommand.Execute(row);
    }

    // ─────────────────────────────────────────────────────────────────────

    private BreakpointRowEx? GetSelectedRow() =>
        Vm?.SelectedBreakpoint ?? FlatList.SelectedItem as BreakpointRowEx;
}
