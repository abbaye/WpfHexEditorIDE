// ==========================================================
// Project: WpfHexEditor.Plugins.UnitTesting
// File: Views/UnitTestingPanel.xaml.cs
// Author: Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.6
// Created: 2026-03-23
// Updated: 2026-03-24 (ADR-UT-12 — context-sensitive detail pane)
// Description:
//     Code-behind for the Unit Testing Panel.
//     Toolbar: VS-style pill filter/counter buttons + search box.
//     Test tree: Project → Class → TestMethod (3 levels).
//     Detail pane: ContentControl + implicit DataTemplates — project/class show group
//     summary, leaf shows test detail with Run button.
//     ApplyDetailLayout() switches ContentArea between TopBottom and LeftRight.
// ==========================================================

using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using WpfHexEditor.Plugins.UnitTesting.Options;
using WpfHexEditor.Plugins.UnitTesting.Properties;
using WpfHexEditor.Plugins.UnitTesting.ViewModels;

namespace WpfHexEditor.Plugins.UnitTesting.Views;

/// <summary>
/// Dockable Unit Testing Panel.
/// </summary>
public partial class UnitTestingPanel : UserControl
{
    public event EventHandler?                                       RunAllRequested;
    public event EventHandler?                                       RefreshProjectsRequested;
    public event EventHandler?                                       StopRequested;
    public event EventHandler<string?>?                              RunFailedRequested;
    public event EventHandler<TestResultRow?>?                       RunThisTestRequested;
    public event EventHandler<TestResultRow?>?                       GoToSourceRequested;
    public event EventHandler<(TestResultRow Row, string EditorId)>? OpenSourceWithEditorRequested;

    private UnitTestingViewModel Vm => (UnitTestingViewModel)DataContext;

    public UnitTestingPanel(UnitTestingViewModel vm)
    {
        InitializeComponent();
        DataContext = vm;

        vm.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(UnitTestingViewModel.IsRunning))
                UpdateToolbarState(vm.IsRunning);
        };

        ApplyDetailLayout(UnitTestingOptions.Instance.DetailPaneLayout);
    }

    // ── Toolbar action handlers ───────────────────────────────────────────────

    private void OnRunAllClicked(object sender, RoutedEventArgs e)
        => RunAllRequested?.Invoke(this, EventArgs.Empty);

    private void OnRefreshClicked(object sender, RoutedEventArgs e)
        => RefreshProjectsRequested?.Invoke(this, EventArgs.Empty);

    private void OnRunFailedClicked(object sender, RoutedEventArgs e)
        => RunFailedRequested?.Invoke(this, null);

    private void OnStopClicked(object sender, RoutedEventArgs e)
        => StopRequested?.Invoke(this, EventArgs.Empty);

    private void OnClearClicked(object sender, RoutedEventArgs e)
        => Vm.Reset();

    // ── Filter toggles ────────────────────────────────────────────────────────

    private void OnFilterAll(object sender, RoutedEventArgs e)     => Vm.FilterMode = "All";
    private void OnFilterPassed(object sender, RoutedEventArgs e)  => Vm.FilterMode = "Passed";
    private void OnFilterFailed(object sender, RoutedEventArgs e)  => Vm.FilterMode = "Failed";
    private void OnFilterSkipped(object sender, RoutedEventArgs e) => Vm.FilterMode = "Skipped";

    // ── Search box ────────────────────────────────────────────────────────────

    private void OnSearchTextChanged(object sender, TextChangedEventArgs e)
    {
        Vm.SearchText = SearchBox.Text;
        if (!string.IsNullOrEmpty(SearchBox.Text))
            SearchSuggestionsPopup.IsOpen = false;
    }

    private void OnSearchBoxGotFocus(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrEmpty(SearchBox.Text))
            SearchSuggestionsPopup.IsOpen = true;
    }

    private void OnSearchBoxKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
    {
        if (e.Key != System.Windows.Input.Key.Escape) return;
        SearchSuggestionsPopup.IsOpen = false;
        SearchBox.Clear();
        Vm.SearchText = string.Empty;
    }

    private void OnFilterChipClicked(object sender, RoutedEventArgs e)
    {
        if (sender is not Button btn || btn.Tag is not string prefix) return;
        SearchSuggestionsPopup.IsOpen = false;
        SearchBox.Text       = prefix;
        Vm.SearchText        = prefix;
        SearchBox.Focus();
        SearchBox.CaretIndex = prefix.Length;
    }

    private void OnGoToSourceClicked(object sender, RoutedEventArgs e)
    {
        if (sender is not FrameworkElement { Tag: TestResultRow row } || !row.HasSource) return;

        var bg  = TryFindResource("DockMenuBackgroundBrush");
        var fg  = TryFindResource("DockMenuForegroundBrush");
        var bdr = TryFindResource("DockBorderBrush");

        var menu = new ContextMenu
        {
            Background  = bg  as System.Windows.Media.Brush,
            Foreground  = fg  as System.Windows.Media.Brush,
            BorderBrush = bdr as System.Windows.Media.Brush,
        };

        // Navigate to the exact source line (default, code-editor)
        var lineLabel = row.SourceLine > 0
            ? $"Go to Line {row.SourceLine}  —  {row.ShortClassName}"
            : $"Open  —  {row.ShortClassName}";
        var miNavigate = new MenuItem { Header = lineLabel, FontWeight = FontWeights.SemiBold };
        miNavigate.Click += (_, _) => GoToSourceRequested?.Invoke(this, row);
        menu.Items.Add(miNavigate);

        menu.Items.Add(new Separator());

        // Open with specific editor
        var miCode = new MenuItem { Header = UnitTestingResources.UnitTest_OpenInCodeEditor };
        miCode.Click += (_, _) => OpenSourceWithEditorRequested?.Invoke(this,
            (row, WpfHexEditor.SDK.Contracts.WellKnownEditorIds.CodeEditor));
        menu.Items.Add(miCode);

        var miHex = new MenuItem { Header = UnitTestingResources.UnitTest_OpenInHexEditor };
        miHex.Click += (_, _) => OpenSourceWithEditorRequested?.Invoke(this,
            (row, WpfHexEditor.SDK.Contracts.WellKnownEditorIds.HexEditor));
        menu.Items.Add(miHex);

        menu.PlacementTarget = (UIElement)sender;
        menu.Placement       = System.Windows.Controls.Primitives.PlacementMode.Bottom;
        menu.IsOpen          = true;
        e.Handled            = true;
    }

    private void OnClearSearch(object sender, RoutedEventArgs e)
    {
        SearchBox.Clear();
        Vm.SearchText = string.Empty;
    }

    // ── Tree expand / collapse ────────────────────────────────────────────────

    private void OnCollapseAllClicked(object sender, RoutedEventArgs e)
        => Vm.SetAllExpanded(false);

    private void OnExpandAllClicked(object sender, RoutedEventArgs e)
        => Vm.SetAllExpanded(true);

    // ── Tree selection ────────────────────────────────────────────────────────

    private void OnTreeSelectedItemChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
        => Vm.SelectedNode = e.NewValue;

    // ── Detail pane action buttons ────────────────────────────────────────────

    private void OnDetailRunClicked(object sender, RoutedEventArgs e)
    {
        switch (Vm.SelectedNode)
        {
            case TestResultRow row:
                RunThisTestRequested?.Invoke(this, row);
                break;
            case TestClassNode:
            case TestProjectNode:
                RunAllRequested?.Invoke(this, EventArgs.Empty);
                break;
        }
    }

    // ── Context menu ─────────────────────────────────────────────────────────

    private void OnRunThisTestClicked(object sender, RoutedEventArgs e)
    {
        switch (TestTree.SelectedItem)
        {
            case TestResultRow row:
                RunThisTestRequested?.Invoke(this, row);
                break;
            case TestClassNode:
            case TestProjectNode:
                RunAllRequested?.Invoke(this, EventArgs.Empty);
                break;
        }
    }

    private void OnCopyTestNameClicked(object sender, RoutedEventArgs e)
    {
        if (TestTree.SelectedItem is TestResultRow row)
            Clipboard.SetText(row.Display);
    }

    private void OnCopyStackTraceClicked(object sender, RoutedEventArgs e)
    {
        var trace = Vm.SelectedResult?.StackTrace;
        if (!string.IsNullOrEmpty(trace))
            Clipboard.SetText(trace);
    }

    // ── Settings dropdown ─────────────────────────────────────────────────────

    private void OnSettingsClicked(object sender, RoutedEventArgs e)
    {
        var opts = UnitTestingOptions.Instance;
        MiAutoRun.IsChecked    = opts.AutoRunOnBuild;
        MiAutoExpand.IsChecked = opts.AutoExpandDetailOnFailure;
        MiRatioBar.IsChecked   = opts.ShowRatioBar;

        var menu = SettingsButton.ContextMenu;
        menu.PlacementTarget = SettingsButton;
        menu.Placement       = PlacementMode.Bottom;
        menu.IsOpen          = true;
    }

    private void OnOptionChanged(object sender, RoutedEventArgs e)
    {
        var opts = UnitTestingOptions.Instance;
        opts.AutoRunOnBuild           = MiAutoRun.IsChecked    == true;
        opts.AutoExpandDetailOnFailure = MiAutoExpand.IsChecked == true;
        opts.ShowRatioBar             = MiRatioBar.IsChecked   == true;
        opts.Save();
        Vm.ApplyOptions();
    }

    // ── Layout dropdown ───────────────────────────────────────────────────────

    private void OnLayoutButtonClicked(object sender, RoutedEventArgs e)
    {
        var opts = UnitTestingOptions.Instance;
        MiLayoutTopBottom.IsChecked = opts.DetailPaneLayout == DetailLayout.TopBottom;
        MiLayoutLeftRight.IsChecked = opts.DetailPaneLayout == DetailLayout.LeftRight;

        var menu = LayoutButton.ContextMenu;
        menu.PlacementTarget = LayoutButton;
        menu.Placement       = PlacementMode.Bottom;
        menu.IsOpen          = true;
    }

    private void OnLayoutChanged(object sender, RoutedEventArgs e)
    {
        MiLayoutTopBottom.IsChecked = sender == MiLayoutTopBottom;
        MiLayoutLeftRight.IsChecked = sender == MiLayoutLeftRight;

        var layout = sender == MiLayoutLeftRight
            ? DetailLayout.LeftRight
            : DetailLayout.TopBottom;

        var opts = UnitTestingOptions.Instance;
        opts.DetailPaneLayout = layout;
        opts.Save();
        ApplyDetailLayout(layout);
    }

    private void ApplyDetailLayout(DetailLayout layout)
    {
        ContentArea.RowDefinitions.Clear();
        ContentArea.ColumnDefinitions.Clear();

        if (layout == DetailLayout.LeftRight)
        {
            ContentArea.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            ContentArea.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(4) });
            ContentArea.ColumnDefinitions.Add(new ColumnDefinition
            {
                Width    = new GridLength(260),
                MaxWidth = 420,
                MinWidth = 160,
            });

            Grid.SetRow(TreeArea,        0); Grid.SetColumn(TreeArea,        0);
            Grid.SetRow(ContentSplitter, 0); Grid.SetColumn(ContentSplitter, 1);
            Grid.SetRow(DetailPane,      0); Grid.SetColumn(DetailPane,      2);

            ContentSplitter.Width               = 4;
            ContentSplitter.Height              = double.NaN;
            ContentSplitter.HorizontalAlignment = HorizontalAlignment.Stretch;
            ContentSplitter.VerticalAlignment   = VerticalAlignment.Stretch;
            ContentSplitter.ResizeDirection     = GridResizeDirection.Columns;

            DetailPane.MinHeight       = 0;
            DetailPane.MaxHeight       = double.PositiveInfinity;
            DetailPane.MinWidth        = 160;
            DetailPane.MaxWidth        = 420;
            DetailPane.BorderThickness = new Thickness(1, 0, 0, 0);
        }
        else // TopBottom
        {
            ContentArea.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            ContentArea.RowDefinitions.Add(new RowDefinition { Height = new GridLength(4) });
            ContentArea.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto, MinHeight = 0 });

            Grid.SetRow(TreeArea,        0); Grid.SetColumn(TreeArea,        0);
            Grid.SetRow(ContentSplitter, 1); Grid.SetColumn(ContentSplitter, 0);
            Grid.SetRow(DetailPane,      2); Grid.SetColumn(DetailPane,      0);

            ContentSplitter.Height              = 4;
            ContentSplitter.Width               = double.NaN;
            ContentSplitter.HorizontalAlignment = HorizontalAlignment.Stretch;
            ContentSplitter.VerticalAlignment   = VerticalAlignment.Stretch;
            ContentSplitter.ResizeDirection     = GridResizeDirection.Rows;

            DetailPane.MinHeight       = 100;
            DetailPane.MaxHeight       = 260;
            DetailPane.MinWidth        = 0;
            DetailPane.MaxWidth        = double.PositiveInfinity;
            DetailPane.BorderThickness = new Thickness(0, 1, 0, 0);
        }
    }

    // ── Toolbar state sync ────────────────────────────────────────────────────

    private void UpdateToolbarState(bool isRunning)
    {
        RunButton.IsEnabled  = !isRunning;
        StopButton.IsEnabled =  isRunning;
    }
}
