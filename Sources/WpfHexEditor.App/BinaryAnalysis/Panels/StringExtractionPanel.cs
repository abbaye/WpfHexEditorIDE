//////////////////////////////////////////////
// GNU Affero General Public License v3.0 - 2026
// Author : Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.6
//////////////////////////////////////////////

using System.IO;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;
using Microsoft.Win32;
using WpfHexEditor.App.BinaryAnalysis.Services;
using WpfHexEditor.App.BinaryAnalysis.ViewModels;
using WpfHexEditor.Core.CharacterTable;
using WpfHexEditor.SDK.Contracts;

namespace WpfHexEditor.App.BinaryAnalysis.Panels;

/// <summary>#110 String Extraction panel — full IDE theme, overkill redesign.</summary>
public sealed class StringExtractionPanel : UserControl, IDisposable
{
    // Frozen brushes shared across all recycled cells — allocated once at class init.
    private static readonly SolidColorBrush ContextByteBrush = FreezeB(new SolidColorBrush(Color.FromRgb(0x70, 0x70, 0x70)));
    private static SolidColorBrush FreezeB(SolidColorBrush b) { b.Freeze(); return b; }

    // 256-entry lookup avoids one string allocation per byte in the context column hot path.
    private static readonly string[] HexByteLookup =
        Enumerable.Range(0, 256).Select(i => i.ToString("X2")).ToArray();

    private readonly StringExtractionViewModel _vm = new();
    private readonly StringExtractionOptions _options = StringExtractionOptions.Load();
    private DataGrid _grid = null!;
    private TblStream? _loadedTbl;
    private bool _disposed;
    private TextBlock? _tblNameLabel;
    private Button? _tblClearBtn;
    private TextBlock? _outdatedBadge;
    private TextBlock? _capBadge;
    private WrapPanel? _statsBar;
    private readonly StringOffsetHeatmap  _heatmap  = new() { Height = 8, Cursor = System.Windows.Input.Cursors.Hand };
    private readonly StringTimelinePanel  _timeline = new();
    private readonly StringDiffPanel      _diffPanel;

    public StringExtractionPanel()
    {
        _diffPanel = new StringDiffPanel(_vm);

        var root = new Grid();
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        root.SetResourceReference(BackgroundProperty, "TE_Background");

        var toolbarPanel = BuildToolbarWithFilter();
        var tabCtrl      = BuildTabControl();
        var statsRow     = BuildStatsBar();
        var statusBar    = BuildStatusBar();

        _heatmap.Attach(_vm);
        _heatmap.NavigateToOffset = offset =>
        {
            var nearest = _vm.FindNearestRun(offset);
            if (nearest is not null)
            {
                _vm.SelectedGridItem = nearest;
                _vm.NavigateToOffset(nearest);
            }
        };

        _timeline.Attach(_vm, run =>
        {
            _vm.SelectedGridItem = run;
            _vm.NavigateToOffset(run);
        });

        Grid.SetRow(toolbarPanel, 0);
        Grid.SetRow(_heatmap,     1);
        Grid.SetRow(tabCtrl,      2);
        Grid.SetRow(statsRow,     3);
        Grid.SetRow(statusBar,    4);

        root.Children.Add(toolbarPanel);
        root.Children.Add(_heatmap);
        root.Children.Add(tabCtrl);
        root.Children.Add(statsRow);
        root.Children.Add(statusBar);

        Content = root;
        Focusable = true;
        KeyDown += OnKeyDown;

        _vm.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(_vm.EncodingCounts)) RefreshStatsBar();
            if (e.PropertyName == nameof(_vm.IsOutdated))     UpdateOutdatedBadge();
        };
    }

    // ── Tab control ──────────────────────────────────────────────────────────

    private TabControl BuildTabControl()
    {
        var tc = new TabControl { BorderThickness = new Thickness(0), Padding = new Thickness(0) };
        tc.SetResourceReference(BackgroundProperty, "TE_Background");
        tc.ItemContainerStyle = MakePanelTabItemStyle();

        var resultsTab = new TabItem();
        resultsTab.SetResourceReference(HeaderedContentControl.HeaderProperty, "StringExtract_TabResults");
        resultsTab.Content = BuildGrid();

        var timelineTab = new TabItem();
        timelineTab.SetResourceReference(HeaderedContentControl.HeaderProperty, "StringExtract_TabTimeline");
        timelineTab.SetResourceReference(ToolTipProperty, "StringExtract_TtTimeline");
        timelineTab.Content = _timeline;

        var diffTab = new TabItem();
        diffTab.SetResourceReference(HeaderedContentControl.HeaderProperty, "StringExtract_TabDiff");
        diffTab.SetResourceReference(ToolTipProperty, "StringExtract_TtDiff");
        diffTab.Content = _diffPanel;

        tc.Items.Add(resultsTab);
        tc.Items.Add(timelineTab);
        tc.Items.Add(diffTab);
        return tc;
    }

    private static Style MakePanelTabItemStyle()
    {
        var style = new Style(typeof(TabItem));
        style.Setters.Add(new Setter(Control.BackgroundProperty,      new DynamicResourceExtension("TE_Background")));
        style.Setters.Add(new Setter(Control.ForegroundProperty,      new DynamicResourceExtension("Panel_ToolbarForegroundBrush")));
        style.Setters.Add(new Setter(Control.BorderThicknessProperty, new Thickness(0)));
        style.Setters.Add(new Setter(Control.PaddingProperty,         new Thickness(10, 3, 10, 3)));
        style.Setters.Add(new Setter(Control.FontSizeProperty,        11d));
        style.Setters.Add(new Setter(Control.FocusVisualStyleProperty, null));

        var tpl = new ControlTemplate(typeof(TabItem));
        var border = new FrameworkElementFactory(typeof(Border));
        border.Name = "PART_Border";
        border.SetValue(Border.BackgroundProperty,    new TemplateBindingExtension(Control.BackgroundProperty));
        border.SetValue(Border.PaddingProperty,       new TemplateBindingExtension(Control.PaddingProperty));
        var cp = new FrameworkElementFactory(typeof(ContentPresenter));
        cp.SetValue(ContentPresenter.ContentSourceProperty, "Header");
        cp.SetValue(FrameworkElement.VerticalAlignmentProperty,   VerticalAlignment.Center);
        cp.SetValue(FrameworkElement.HorizontalAlignmentProperty, HorizontalAlignment.Center);
        border.AppendChild(cp);
        tpl.VisualTree = border;

        var selectedTrigger = new Trigger { Property = TabItem.IsSelectedProperty, Value = true };
        selectedTrigger.Setters.Add(new Setter(Border.BorderBrushProperty,     new DynamicResourceExtension("DockTabActiveBrush"), "PART_Border"));
        selectedTrigger.Setters.Add(new Setter(Border.BorderThicknessProperty, new Thickness(0, 0, 0, 2),                         "PART_Border"));
        selectedTrigger.Setters.Add(new Setter(Control.ForegroundProperty,     new DynamicResourceExtension("TE_Foreground")));
        selectedTrigger.Setters.Add(new Setter(Control.FontWeightProperty,     FontWeights.SemiBold));

        var hoverTrigger = new MultiTrigger();
        hoverTrigger.Conditions.Add(new Condition(TabItem.IsSelectedProperty,  false));
        hoverTrigger.Conditions.Add(new Condition(UIElement.IsMouseOverProperty, true));
        hoverTrigger.Setters.Add(new Setter(Control.BackgroundProperty, new DynamicResourceExtension("Panel_ItemHoverBrush")));

        tpl.Triggers.Add(selectedTrigger);
        tpl.Triggers.Add(hoverTrigger);

        style.Setters.Add(new Setter(Control.TemplateProperty, tpl));
        return style;
    }

    // ── Toolbar ───────────────────────────────────────────────────────────────

    private UIElement BuildToolbarWithFilter()
    {
        var outer = new Border { BorderThickness = new Thickness(0, 0, 0, 1) };
        outer.SetResourceReference(Border.BackgroundProperty,  "Panel_ToolbarBrush");
        outer.SetResourceReference(Border.BorderBrushProperty, "Panel_ToolbarBorderBrush");

        var stack = new StackPanel();

        // Row 1: actions left + search right
        var btnRow = new DockPanel { LastChildFill = false, Height = 26, Margin = new Thickness(4, 0, 4, 0) };

        var regexToggle = BuildRegexToggle();
        DockPanel.SetDock(regexToggle, Dock.Right);
        btnRow.Children.Add(regexToggle);

        var searchBox = BuildSearchBox();
        DockPanel.SetDock(searchBox, Dock.Right);
        btnRow.Children.Add(searchBox);

        var runBtn       = MakeToolbarButton("", "StringExtract_TtRun");
        runBtn.Click    += async (_, _) => await _vm.RunAsync();
        var cancelBtn    = MakeToolbarButton("", "StringExtract_TtCancel");
        cancelBtn.Click += (_, _) => _vm.Cancel();
        var tblBtn       = MakeToolbarButton("", "StringExtract_TtLoadTbl");
        tblBtn.Click    += OnLoadTbl;
        var exportBtn    = MakeToolbarButton("", "StringExtract_TtExport");
        exportBtn.Click += (_, _) => OnExport(exportAll: true);
        var wrapBtn         = MakeToolbarToggle(0xE751, "StringExtract_TtWordWrap",      nameof(_vm.WordWrap));
        var groupBtn        = MakeToolbarToggle(0xF168, "StringExtract_TtGroupByEncoding", nameof(_vm.GroupByEncoding));
        var autoRescanBtn   = MakeToolbarToggle(0xE72C, "StringExtract_TtAutoRescan",    nameof(_vm.AutoRescan));

        var clusterBtn = MakeToolbarButton(0xE8FD, "StringExtract_TtCluster");
        clusterBtn.Click += async (_, _) => await _vm.ClusterAsync();

        var showClustersBtn = MakeToolbarToggle(0xE71C, "StringExtract_TtShowClusters", nameof(_vm.ShowOnlyClusters));



        var highlightBtn  = MakeToolbarButton("", "StringExtract_TtHighlight");
        highlightBtn.Click += (_, _) => _vm.HighlightRuns(_vm.DisplayList);
        var clearHlBtn    = MakeToolbarButton("", "StringExtract_TtClearHighlight");
        clearHlBtn.Click += (_, _) => _vm.ClearHighlights();

        // Caret sync toggle
        var syncBtn = new ToggleButton
        {
            Content  = char.ConvertFromUtf32(0xE8AB),
            ToolTip  = null,
            Height   = 20, Width = 22,
            Padding  = new Thickness(0),
            BorderThickness = new Thickness(0),
            FontFamily = new FontFamily("Segoe MDL2 Assets"),
            FontSize   = 11,
            IsChecked  = _vm.SyncCaretToGrid,
            FocusVisualStyle = null,
        };
        syncBtn.SetResourceReference(ForegroundProperty, "Panel_ToolbarForegroundBrush");
        syncBtn.SetResourceReference(BackgroundProperty, "Panel_ToolbarBrush");
        syncBtn.SetResourceReference(ToolTipProperty,   "StringExtract_TtSyncCaret");
        syncBtn.SetBinding(ToggleButton.IsCheckedProperty, new Binding(nameof(_vm.SyncCaretToGrid)) { Source = _vm, Mode = BindingMode.TwoWay });

        _outdatedBadge = new TextBlock
        {
            FontSize = 10,
            FontWeight = FontWeights.Bold,
            Foreground = Brushes.OrangeRed,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(6, 0, 0, 0),
            Visibility = Visibility.Collapsed,
        };
        _outdatedBadge.SetResourceReference(TextBlock.TextProperty,   "StringExtract_TtOutdatedBadge");
        _outdatedBadge.SetResourceReference(ToolTipProperty,          "StringExtract_TtOutdated");

        _capBadge = new TextBlock
        {
            Text = "⚠ display capped at 100 000", FontSize = 10, FontWeight = FontWeights.Bold,
            Foreground = Brushes.Orange, VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(6, 0, 0, 0), Visibility = Visibility.Collapsed,
            ToolTip = "The file produced more than 100 000 strings. Results are truncated. Use filters to narrow down.",
        };
        _vm.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(_vm.IsDisplayCapped))
                _capBadge.Visibility = _vm.IsDisplayCapped ? Visibility.Visible : Visibility.Collapsed;
        };

        var kindFilterBtn    = BuildKindFilterButton();
        var snapshotBtn      = MakeToolbarButton(0xE722, "StringExtract_TtSnapshot");
        snapshotBtn.Click   += (_, _) => _vm.TakeSnapshot();
        var loadSnapshotBtn  = BuildSnapshotDropdown();

        foreach (UIElement el in new UIElement[]
        {
            runBtn, cancelBtn, MakeToolbarSeparator(),
            tblBtn, MakeToolbarSeparator(),
            exportBtn, MakeToolbarSeparator(),
            wrapBtn, groupBtn, autoRescanBtn, MakeToolbarSeparator(),
            clusterBtn, showClustersBtn, kindFilterBtn, MakeToolbarSeparator(),
            snapshotBtn, loadSnapshotBtn, MakeToolbarSeparator(),
            highlightBtn, clearHlBtn, MakeToolbarSeparator(),
            syncBtn, _outdatedBadge, _capBadge,
        })
        {
            DockPanel.SetDock(el, Dock.Left);
            btnRow.Children.Add(el);
        }

        // Row 2: file + encodings + min length + TBL indicator
        var filterRow = new WrapPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(4, 2, 4, 2) };

        var fileCombo = new ComboBox { Width = 160, Height = 20, FontSize = 11, Margin = new Thickness(0, 0, 8, 0) };
        fileCombo.SetResourceReference(ForegroundProperty, "TE_Foreground");
        fileCombo.SetResourceReference(ToolTipProperty,    "StringExtract_TtFile");
        fileCombo.SetBinding(ItemsControl.ItemsSourceProperty, new Binding(nameof(_vm.OpenedFiles)) { Source = _vm });
        fileCombo.SetBinding(Selector.SelectedItemProperty,   new Binding(nameof(_vm.SelectedFile)) { Source = _vm, Mode = BindingMode.TwoWay });
        fileCombo.DisplayMemberPath = nameof(OpenedFileItem.DisplayName);
        fileCombo.DropDownOpened += (_, _) => _vm.RefreshOpenedFiles();

        var minBox = new TextBox
        {
            Width = 32, Height = 20, FontSize = 11, TextAlignment = TextAlignment.Center,
            VerticalContentAlignment = VerticalAlignment.Center,
            Text    = _vm.MinLength.ToString(),
        };
        minBox.SetResourceReference(BackgroundProperty, "TE_Background");
        minBox.SetResourceReference(ForegroundProperty, "TE_Foreground");
        minBox.SetResourceReference(ToolTipProperty,    "StringExtract_TtMinLength");
        minBox.LostFocus += (_, _) =>
        {
            if (int.TryParse(minBox.Text, out int v)) _vm.MinLength = v;
            minBox.Text = _vm.MinLength.ToString();
        };
        minBox.KeyDown += (_, e) => { if (e.Key == Key.Enter) Keyboard.ClearFocus(); };

        filterRow.Children.Add(MakeLabelFromKey("StringExtract_LabelFile"));
        filterRow.Children.Add(fileCombo);
        filterRow.Children.Add(MakeLabelFromKey("StringExtract_LabelEncodings"));
        filterRow.Children.Add(BuildEncodingDropdown());
        filterRow.Children.Add(MakeLabelFromKey("StringExtract_LabelMin"));
        filterRow.Children.Add(minBox);
        filterRow.Children.Add(BuildTblIndicator());

        // Row 3: advanced filters (MinUniqueChars + Regex + Offset range + Entropy)
        var advRow = BuildAdvancedFilterRow();

        stack.Children.Add(btnRow);
        stack.Children.Add(filterRow);
        stack.Children.Add(advRow);
        outer.Child = stack;
        return outer;
    }

    private UIElement BuildAdvancedFilterRow()
    {
        var row = new WrapPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(4, 0, 4, 4) };

        // Min unique chars spinner
        var uniqueCharsUpDown = BuildIntSpinner(
            getValue: () => _vm.MinUniqueChars,
            setValue: v => _vm.MinUniqueChars = v,
            min: 1, max: 20, width: 36,
            tooltip: "StringExtract_TtMinUniq");
        row.Children.Add(MakeLabelFromKey("StringExtract_LabelUniq"));
        row.Children.Add(uniqueCharsUpDown);

        // Offset range
        row.Children.Add(MakeLabelFromKey("StringExtract_LabelOffset"));
        var fromBox = BuildHexBox(
            getValue: () => _vm.RangeFrom,
            setValue: v => _vm.RangeFrom = v,
            tooltip: "StringExtract_TtOffsetFrom");
        row.Children.Add(fromBox);
        row.Children.Add(MakeLabel("–"));
        var toBox = BuildHexBox(
            getValue: () => _vm.RangeTo == long.MaxValue ? 0 : _vm.RangeTo,
            setValue: v => _vm.RangeTo = v == 0 ? long.MaxValue : v,
            tooltip: "StringExtract_TtOffsetTo");
        row.Children.Add(toBox);

        row.Children.Add(new System.Windows.Shapes.Rectangle { Width = 6, Height = 1, Fill = Brushes.Transparent });

        var entropyChk = new CheckBox
        {
            FontSize = 10,
            VerticalAlignment = VerticalAlignment.Center,
        };
        entropyChk.SetResourceReference(ForegroundProperty, "TE_Foreground");
        entropyChk.SetResourceReference(ContentProperty,    "StringExtract_ExclHighEntropy");
        entropyChk.SetResourceReference(ToolTipProperty,    "StringExtract_TtExclEntropy");
        entropyChk.SetBinding(CheckBox.IsCheckedProperty, new Binding(nameof(_vm.ExcludeHighEntropy)) { Source = _vm, Mode = BindingMode.TwoWay });

        var thresholdSlider = new Slider
        {
            Minimum = 0.1, Maximum = 1.0, TickFrequency = 0.05,
            IsSnapToTickEnabled = true,
            Width = 80, Height = 18,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(4, 0, 2, 0),
        };
        thresholdSlider.SetBinding(RangeBase.ValueProperty, new Binding(nameof(_vm.EntropyThreshold)) { Source = _vm, Mode = BindingMode.TwoWay });
        thresholdSlider.SetBinding(UIElement.IsEnabledProperty, new Binding(nameof(_vm.ExcludeHighEntropy)) { Source = _vm });

        var thresholdLabel = new TextBlock { FontSize = 10, VerticalAlignment = VerticalAlignment.Center };
        thresholdLabel.SetResourceReference(ForegroundProperty, "TE_Foreground");
        thresholdLabel.SetBinding(TextBlock.TextProperty, new Binding(nameof(_vm.EntropyThreshold)) { Source = _vm, StringFormat = "F2" });

        row.Children.Add(entropyChk);
        row.Children.Add(thresholdSlider);
        row.Children.Add(thresholdLabel);

        row.Children.Add(new System.Windows.Shapes.Rectangle { Width = 6, Height = 1, Fill = Brushes.Transparent });

        var readabilitySlider = new Slider
        {
            Minimum = 0.0, Maximum = 1.0, TickFrequency = 0.05,
            IsSnapToTickEnabled = true,
            Width = 80, Height = 18,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(4, 0, 2, 0),
        };
        readabilitySlider.SetBinding(RangeBase.ValueProperty, new Binding(nameof(_vm.MinReadability)) { Source = _vm, Mode = BindingMode.TwoWay });

        var readabilityLabel = new TextBlock { FontSize = 10, VerticalAlignment = VerticalAlignment.Center };
        readabilityLabel.SetResourceReference(ForegroundProperty, "TE_Foreground");
        readabilityLabel.SetBinding(TextBlock.TextProperty, new Binding(nameof(_vm.MinReadability)) { Source = _vm, StringFormat = "F2" });

        row.Children.Add(MakeLabelFromKey("StringExtract_LabelReadability"));
        row.Children.Add(readabilitySlider);
        row.Children.Add(readabilityLabel);

        row.Children.Add(new System.Windows.Shapes.Rectangle { Width = 6, Height = 1, Fill = Brushes.Transparent });

        var printableChk = new CheckBox { FontSize = 10, VerticalAlignment = VerticalAlignment.Center };
        printableChk.SetResourceReference(ForegroundProperty, "TE_Foreground");
        printableChk.SetResourceReference(ContentProperty,    "StringExtract_PrintableOnly");
        printableChk.SetResourceReference(ToolTipProperty,    "StringExtract_TtPrintableOnly");
        printableChk.SetBinding(CheckBox.IsCheckedProperty, new Binding(nameof(_vm.PrintableOnly)) { Source = _vm, Mode = BindingMode.TwoWay });
        row.Children.Add(printableChk);

        return row;
    }

    private UIElement BuildSearchBox()
    {
        var box = new TextBox
        {
            Width   = 140,
            Tag     = "Search strings…",
            Margin  = new Thickness(4, 0, 0, 0),
            VerticalAlignment = VerticalAlignment.Center,
        };
        box.SetResourceReference(StyleProperty,   "PanelSearchBoxStyle");
        box.SetResourceReference(ToolTipProperty, "StringExtract_TtSearch");
        box.SetBinding(TextBox.TextProperty, new Binding(nameof(_vm.Filter))
        {
            Source = _vm, Mode = BindingMode.TwoWay, UpdateSourceTrigger = UpdateSourceTrigger.PropertyChanged
        });
        return box;
    }

    /// <summary>Regex toggle chip — sits left of the search box, lights up blue when active.</summary>
    private UIElement BuildRegexToggle()
    {
        // Outer border acts as the chip — color changes via trigger on IsChecked
        var chip = new Border
        {
            CornerRadius    = new CornerRadius(3),
            BorderThickness = new Thickness(1),
            Padding         = new Thickness(6, 0, 6, 0),
            Height          = 20,
            Margin          = new Thickness(2, 0, 0, 0),
            Cursor          = Cursors.Hand,
            VerticalAlignment = VerticalAlignment.Center,
        };
        chip.SetResourceReference(Border.BorderBrushProperty, "Panel_ToolbarBorderBrush");
        chip.SetResourceReference(Border.BackgroundProperty,  "TE_Background");
        chip.SetResourceReference(ToolTipProperty,            "StringExtract_TtRegex");

        var label = new TextBlock
        {
            Text = ".*",
            FontSize = 11,
            FontWeight = FontWeights.SemiBold,
            VerticalAlignment = VerticalAlignment.Center,
        };
        label.SetResourceReference(ForegroundProperty, "TE_Foreground");
        chip.Child = label;

        // Toggle on click
        chip.MouseLeftButtonUp += (_, _) => _vm.UseRegexFilter = !_vm.UseRegexFilter;

        // React to VM state: highlight chip blue when regex is ON
        void UpdateChipState()
        {
            if (_vm.UseRegexFilter)
            {
                chip.Background = new SolidColorBrush(Color.FromRgb(0x00, 0x7A, 0xCC));
                label.Foreground = Brushes.White;
            }
            else
            {
                chip.ClearValue(Border.BackgroundProperty);
                chip.SetResourceReference(Border.BackgroundProperty, "TE_Background");
                label.ClearValue(ForegroundProperty);
                label.SetResourceReference(ForegroundProperty, "TE_Foreground");
            }
        }

        _vm.PropertyChanged += (_, e) => { if (e.PropertyName == nameof(_vm.UseRegexFilter)) UpdateChipState(); };
        UpdateChipState();
        return chip;
    }

    private static UIElement BuildIntSpinner(Func<int> getValue, Action<int> setValue, int min, int max, double width, string tooltip)
    {
        var panel = new StackPanel { Orientation = Orientation.Horizontal };

        var txt = new TextBox
        {
            Width = width, Height = 18, FontSize = 10,
            TextAlignment = TextAlignment.Center,
            VerticalContentAlignment = VerticalAlignment.Center,
            Text = getValue().ToString(),
        };
        txt.SetResourceReference(ToolTipProperty, tooltip);

        void Commit()
        {
            if (int.TryParse(txt.Text, out int v)) setValue(Math.Clamp(v, min, max));
            txt.Text = getValue().ToString();
        }

        txt.LostFocus += (_, _) => Commit();
        txt.KeyDown   += (_, e) => { if (e.Key == Key.Enter) Commit(); };

        var up = new RepeatButton { Content = "▲", Width = 14, Height = 9, FontSize = 7, Padding = new Thickness(0), BorderThickness = new Thickness(1) };
        var dn = new RepeatButton { Content = "▼", Width = 14, Height = 9, FontSize = 7, Padding = new Thickness(0), BorderThickness = new Thickness(1) };

        up.Click += (_, _) => { setValue(Math.Clamp(getValue() + 1, min, max)); txt.Text = getValue().ToString(); };
        dn.Click += (_, _) => { setValue(Math.Clamp(getValue() - 1, min, max)); txt.Text = getValue().ToString(); };

        var btnStack = new StackPanel { Orientation = Orientation.Vertical };
        btnStack.Children.Add(up);
        btnStack.Children.Add(dn);

        panel.Children.Add(txt);
        panel.Children.Add(btnStack);
        return panel;
    }

    private static UIElement BuildHexBox(Func<long> getValue, Action<long> setValue, string tooltip)
    {
        var txt = new TextBox
        {
            Width = 70, Height = 18, FontSize = 10,
            TextAlignment = TextAlignment.Center,
            VerticalContentAlignment = VerticalAlignment.Center,
            Text = $"{getValue():X}",
        };
        txt.SetResourceReference(ToolTipProperty, tooltip);

        void Commit()
        {
            var raw = txt.Text.Replace("0x", "").Replace("0X", "");
            if (long.TryParse(raw, System.Globalization.NumberStyles.HexNumber, null, out long v))
                setValue(v);
            txt.Text = $"{getValue():X}";
        }

        txt.LostFocus += (_, _) => Commit();
        txt.KeyDown   += (_, e) => { if (e.Key == Key.Enter) Commit(); };
        return txt;
    }

    private static Button MakeToolbarButton(int glyphCp, string tooltipKey) =>
        MakeToolbarButton(char.ConvertFromUtf32(glyphCp), tooltipKey);

    private static Button MakeToolbarButton(string glyph, string tooltipKey)
    {
        // Content must be a TextBlock with its own FontFamily — PanelIconButtonStyle's
        // ControlTemplate does not inherit FontFamily from the Button itself.
        var icon = new TextBlock { Text = glyph, FontFamily = Mdl2, FontSize = 13 };
        icon.SetResourceReference(TextBlock.ForegroundProperty, "Panel_ToolbarForegroundBrush");

        var btn = new Button
        {
            Content           = icon,
            Width             = 22,
            Height            = 22,
            Padding           = new Thickness(0),
            Margin            = new Thickness(1, 0, 1, 0),
            BorderThickness   = new Thickness(0),
            Background        = Brushes.Transparent,
            VerticalAlignment = VerticalAlignment.Center,
            FocusVisualStyle  = null,
        };
        btn.SetResourceReference(StyleProperty,   "PanelIconButtonStyle");
        btn.SetResourceReference(ToolTipProperty, tooltipKey);
        return btn;
    }

    private ToggleButton MakeToolbarToggle(int glyphCp, string tooltipKey, string vmProperty) =>
        MakeToolbarToggle(char.ConvertFromUtf32(glyphCp), tooltipKey, vmProperty);

    private ToggleButton MakeToolbarToggle(string glyph, string tooltipKey, string vmProperty)
    {
        var icon = new TextBlock { Text = glyph, FontFamily = Mdl2, FontSize = 11 };
        icon.SetResourceReference(TextBlock.ForegroundProperty, "Panel_ToolbarForegroundBrush");

        var btn = new ToggleButton
        {
            Content         = icon,
            Height          = 20, Width = 22,
            Padding         = new Thickness(0),
            BorderThickness = new Thickness(0),
            FocusVisualStyle = null,
        };
        btn.SetResourceReference(ForegroundProperty, "Panel_ToolbarForegroundBrush");
        btn.SetResourceReference(BackgroundProperty, "Panel_ToolbarBrush");
        btn.SetResourceReference(ToolTipProperty,    tooltipKey);
        btn.SetBinding(ToggleButton.IsCheckedProperty, new Binding(vmProperty) { Source = _vm, Mode = BindingMode.TwoWay });
        return btn;
    }

    private static readonly FontFamily Mdl2 = new("Segoe MDL2 Assets");

    private static System.Windows.Shapes.Rectangle MakeToolbarSeparator()
    {
        var sep = new System.Windows.Shapes.Rectangle { Width = 1, Height = 16, Margin = new Thickness(4, 0, 4, 0) };
        sep.SetResourceReference(System.Windows.Shapes.Shape.FillProperty, "Panel_ToolbarBorderBrush");
        return sep;
    }

    private TextBlock MakeLabel(string text)
    {
        var tb = new TextBlock
        {
            Text = text, VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(0, 0, 4, 0), FontSize = 11,
        };
        tb.SetResourceReference(ForegroundProperty, "Panel_ToolbarForegroundBrush");
        return tb;
    }

    private TextBlock MakeLabelFromKey(string resourceKey)
    {
        var tb = new TextBlock
        {
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(0, 0, 4, 0), FontSize = 11,
        };
        tb.SetResourceReference(TextBlock.TextProperty, resourceKey);
        tb.SetResourceReference(ForegroundProperty,     "Panel_ToolbarForegroundBrush");
        return tb;
    }

    // ── Kind filter dropdown ──────────────────────────────────────────────────

    private UIElement BuildKindFilterButton()
    {
        var popup = new Popup { StaysOpen = false, AllowsTransparency = true, Placement = PlacementMode.Bottom };
        var listBorder = new Border { BorderThickness = new Thickness(1), Padding = new Thickness(2) };
        listBorder.SetResourceReference(BackgroundProperty,  "TE_Background");
        listBorder.SetResourceReference(BorderBrushProperty, "Panel_ToolbarBorderBrush");

        var listPanel = new StackPanel();
        var checkBoxes = new List<CheckBox>();

        var allKinds = Enum.GetValues<StringKind>().Where(k => k != StringKind.None).ToArray();
        foreach (var kind in allKinds)
        {
            var chk = new CheckBox
            {
                Content   = kind.ToString(),
                Tag       = kind,
                IsChecked = _vm.IsKindActive(kind),
                Padding   = new Thickness(4, 1, 8, 1),
                FontSize  = 11,
            };
            chk.SetResourceReference(ForegroundProperty, "TE_Foreground");
            chk.Click += (_, _) =>
            {
                _vm.ToggleKind(kind);
                foreach (var c in checkBoxes)
                    if (c.Tag is StringKind k) c.IsChecked = _vm.IsKindActive(k);
            };
            checkBoxes.Add(chk);
            listPanel.Children.Add(chk);
        }

        // Clear-all entry
        var clearItem = new Button
        {
            Content = "Clear all", FontSize = 10,
            Padding = new Thickness(4, 2, 4, 2), Margin = new Thickness(2, 4, 2, 0),
            HorizontalAlignment = HorizontalAlignment.Stretch,
        };
        clearItem.SetResourceReference(ForegroundProperty, "TE_Foreground");
        clearItem.Click += (_, _) =>
        {
            foreach (var kind in allKinds.Where(k => _vm.IsKindActive(k))) _vm.ToggleKind(kind);
            foreach (var c in checkBoxes) c.IsChecked = false;
        };
        listPanel.Children.Add(clearItem);

        listBorder.Child = listPanel;
        popup.Child      = listBorder;

        var btn = MakeToolbarButton(0xE71C, "StringExtract_TtKindFilter");
        btn.Click += (_, _) => { popup.PlacementTarget = btn; popup.IsOpen = !popup.IsOpen; };

        // Highlight button blue when any kind filter is active
        _vm.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName != nameof(_vm.ActiveKinds)) return;
            if (_vm.ActiveKinds.Count > 0)
                btn.Foreground = new SolidColorBrush(Color.FromRgb(0x00, 0x7A, 0xCC));
            else
                btn.SetResourceReference(ForegroundProperty, "Panel_ToolbarForegroundBrush");
        };

        return btn;
    }

    // ── Snapshot dropdown ─────────────────────────────────────────────────────

    private UIElement BuildSnapshotDropdown()
    {
        var popup = new Popup { StaysOpen = false, AllowsTransparency = true, Placement = PlacementMode.Bottom };
        var outerBorder = new Border { BorderThickness = new Thickness(1), Padding = new Thickness(2), MinWidth = 260 };
        outerBorder.SetResourceReference(BackgroundProperty,  "TE_Background");
        outerBorder.SetResourceReference(BorderBrushProperty, "Panel_ToolbarBorderBrush");

        var listBox = new ListBox { BorderThickness = new Thickness(0), FontSize = 11 };
        listBox.SetResourceReference(BackgroundProperty, "TE_Background");
        listBox.SetResourceReference(ForegroundProperty, "TE_Foreground");
        listBox.SetBinding(ItemsControl.ItemsSourceProperty, new Binding(nameof(_vm.Snapshots)) { Source = _vm });
        listBox.DisplayMemberPath = nameof(StringExtractionViewModel.ScanSnapshot.DisplayName);
        listBox.MouseDoubleClick += (_, _) =>
        {
            if (listBox.SelectedItem is StringExtractionViewModel.ScanSnapshot snap)
            {
                _vm.RestoreSnapshot(snap);
                popup.IsOpen = false;
            }
        };

        var emptyHint = new TextBlock { FontSize = 10, Margin = new Thickness(4), FontStyle = FontStyles.Italic };
        emptyHint.SetResourceReference(ForegroundProperty,     "Panel_ToolbarForegroundBrush");
        emptyHint.SetResourceReference(TextBlock.TextProperty, "StringExtract_NoSnapshots");

        void SyncHint()
            => emptyHint.Visibility = _vm.Snapshots.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        _vm.Snapshots.CollectionChanged += (_, _) => SyncHint();
        SyncHint();

        var inner = new StackPanel();
        inner.Children.Add(emptyHint);
        inner.Children.Add(listBox);
        outerBorder.Child = inner;
        popup.Child       = outerBorder;

        var btn = MakeToolbarButton(0xE8B7, "StringExtract_TtLoadSnapshot");
        btn.Click += (_, _) => { popup.PlacementTarget = btn; popup.IsOpen = !popup.IsOpen; };
        return btn;
    }

    // ── Encoding dropdown ─────────────────────────────────────────────────────

    private UIElement BuildEncodingDropdown()
    {
        var summaryText = new TextBlock { FontSize = 11, VerticalAlignment = VerticalAlignment.Center, TextTrimming = TextTrimming.CharacterEllipsis };
        summaryText.SetResourceReference(ForegroundProperty, "TE_Foreground");

        void RefreshSummary() =>
            summaryText.Text = _vm.ActiveEncodings.Count == 0
                ? "(none)"
                : string.Join(", ", _vm.ActiveEncodings.Select(EncodingLabel));

        var popup = new Popup { StaysOpen = false, AllowsTransparency = true, Placement = PlacementMode.Bottom };
        var listBorder = new Border { BorderThickness = new Thickness(1), Padding = new Thickness(2) };
        listBorder.SetResourceReference(BackgroundProperty,  "TE_Background");
        listBorder.SetResourceReference(BorderBrushProperty, "Panel_ToolbarBorderBrush");

        var listPanel = new StackPanel();

        (string label, StringEncoding? enc)[] items =
        [
            ("── Built-in ──",    null),
            ("ASCII",             StringEncoding.Ascii),
            ("EBCDIC + Special",  StringEncoding.Ebcdic),
            ("EBCDIC (no spec)",  StringEncoding.EbcdicNoSpec),
            ("── Encodings ──",   null),
            ("UTF-8",             StringEncoding.Utf8),
            ("UTF-16 LE",         StringEncoding.Utf16Le),
            ("UTF-16 BE",         StringEncoding.Utf16Be),
            ("Latin-1",           StringEncoding.Latin1),
            ("── TBL (loaded) ──", null),
            ("TBL — Single byte",  StringEncoding.Tbl),
            ("TBL — DTE (2 bytes)",StringEncoding.TblDte),
            ("TBL — MTE (3-8 bytes)", StringEncoding.TblMte),
        ];

        var checkBoxes = new List<CheckBox>();
        foreach (var (label, enc) in items)
        {
            if (enc is null)
            {
                var hdr = new TextBlock
                {
                    Text = label, FontSize = 10, FontWeight = FontWeights.SemiBold,
                    Margin = new Thickness(4, 2, 4, 0), IsEnabled = false,
                };
                hdr.SetResourceReference(ForegroundProperty, "Panel_ToolbarForegroundBrush");
                listPanel.Children.Add(hdr);
            }
            else
            {
                var chk = new CheckBox
                {
                    Content = label, Tag = enc,
                    IsChecked = _vm.IsEncodingActive(enc.Value),
                    Padding = new Thickness(4, 1, 8, 1), FontSize = 11,
                };
                chk.SetResourceReference(ForegroundProperty, "TE_Foreground");
                chk.Click += (_, _) =>
                {
                    _vm.ToggleEncoding(enc.Value);
                    foreach (var c in checkBoxes)
                        if (c.Tag is StringEncoding e2) c.IsChecked = _vm.IsEncodingActive(e2);
                    RefreshSummary();
                };
                checkBoxes.Add(chk);
                listPanel.Children.Add(chk);
            }
        }

        // Sync checkboxes when ActiveEncodings changes externally (e.g. TBL load/clear)
        void SyncCheckboxes()
        {
            foreach (var c in checkBoxes)
                if (c.Tag is StringEncoding e2) c.IsChecked = _vm.IsEncodingActive(e2);
            RefreshSummary();
        }
        _vm.PropertyChanged += (_, e) => { if (e.PropertyName == nameof(_vm.ActiveEncodings)) SyncCheckboxes(); };

        listBorder.Child = listPanel;
        popup.Child      = listBorder;

        var btn = new Button
        {
            Height = 20, MinWidth = 130, Padding = new Thickness(6, 0, 6, 0),
            BorderThickness = new Thickness(1),
            HorizontalContentAlignment = HorizontalAlignment.Left,
            VerticalContentAlignment   = VerticalAlignment.Center,
            FontSize = 11, Margin = new Thickness(0, 0, 8, 0),
            FocusVisualStyle = null,
        };
        btn.SetResourceReference(BackgroundProperty,  "TE_Background");
        btn.SetResourceReference(ForegroundProperty,  "TE_Foreground");
        btn.SetResourceReference(BorderBrushProperty, "Panel_ToolbarBorderBrush");
        btn.Content = summaryText;
        btn.Click  += (_, _) => { popup.PlacementTarget = btn; popup.IsOpen = !popup.IsOpen; };

        RefreshSummary();
        return btn;
    }

    // ── TBL indicator ─────────────────────────────────────────────────────────

    private UIElement BuildTblIndicator()
    {
        var panel = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(8, 0, 0, 0) };

        _tblNameLabel = new TextBlock { FontSize = 11, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 4, 0), Visibility = Visibility.Collapsed };
        _tblNameLabel.SetResourceReference(ForegroundProperty, "Panel_ToolbarForegroundBrush");

        var tblClearIcon = new TextBlock { Text = "", FontFamily = Mdl2, FontSize = 9 };
        tblClearIcon.SetResourceReference(TextBlock.ForegroundProperty, "Panel_ToolbarForegroundBrush");
        _tblClearBtn = new Button
        {
            Content = tblClearIcon,
            Width = 16, Height = 16, Padding = new Thickness(0), BorderThickness = new Thickness(0),
            Background = Brushes.Transparent,
            VerticalAlignment = VerticalAlignment.Center,
            Visibility = Visibility.Collapsed, FocusVisualStyle = null,
        };
        _tblClearBtn.SetResourceReference(ForegroundProperty, "Panel_ToolbarForegroundBrush");
        _tblClearBtn.SetResourceReference(ToolTipProperty,    "StringExtract_TtRemoveTbl");
        _tblClearBtn.Click += (_, _) => ClearTbl();

        panel.Children.Add(MakeLabelFromKey("StringExtract_LabelTbl"));
        panel.Children.Add(_tblNameLabel);
        panel.Children.Add(_tblClearBtn);
        return panel;
    }

    private void UpdateTblIndicator(string? path)
    {
        if (_tblNameLabel is null || _tblClearBtn is null) return;
        if (path is null)
        {
            _tblNameLabel.Text       = string.Empty;
            _tblNameLabel.Visibility = Visibility.Collapsed;
            _tblClearBtn.Visibility  = Visibility.Collapsed;
        }
        else
        {
            _tblNameLabel.Text       = Path.GetFileName(path);
            _tblNameLabel.ToolTip    = path;
            _tblNameLabel.Visibility = Visibility.Visible;
            _tblClearBtn.Visibility  = Visibility.Visible;
        }
    }

    private void ClearTbl()
    {
        _loadedTbl?.Dispose();
        _loadedTbl = null;
        _vm.SetTblTable(null);
        UpdateTblIndicator(null);
        _options.LastTblFilePath = null;
        _options.Save();
    }

    private static string EncodingLabel(StringEncoding enc) => enc switch
    {
        StringEncoding.Ascii        => "ASCII",
        StringEncoding.Utf16Le      => "UTF-16 LE",
        StringEncoding.Utf16Be      => "UTF-16 BE",
        StringEncoding.Utf8         => "UTF-8",
        StringEncoding.Latin1       => "Latin-1",
        StringEncoding.Ebcdic       => "EBCDIC+",
        StringEncoding.EbcdicNoSpec => "EBCDIC",
        StringEncoding.Tbl          => "TBL",
        StringEncoding.TblDte       => "TBL-DTE",
        StringEncoding.TblMte       => "TBL-MTE",
        _                           => enc.ToString(),
    };

    // ── DataGrid ──────────────────────────────────────────────────────────────

    private UIElement BuildGrid()
    {
        _grid = new DataGrid
        {
            AutoGenerateColumns      = false,
            CanUserAddRows           = false,
            CanUserDeleteRows        = false,
            IsReadOnly               = true,
            SelectionMode            = DataGridSelectionMode.Extended,
            EnableRowVirtualization  = true,
            GridLinesVisibility      = DataGridGridLinesVisibility.Horizontal,
            HeadersVisibility        = DataGridHeadersVisibility.Column,
            BorderThickness          = new Thickness(0),
        };
        VirtualizingPanel.SetIsVirtualizing(_grid, true);
        VirtualizingPanel.SetVirtualizationMode(_grid, VirtualizationMode.Recycling);
        VirtualizingPanel.SetScrollUnit(_grid, ScrollUnit.Item);

        _grid.SetResourceReference(BackgroundProperty,                        "TE_Background");
        _grid.SetResourceReference(ForegroundProperty,                        "TE_Foreground");
        _grid.SetResourceReference(DataGrid.RowBackgroundProperty,            "TE_Background");
        _grid.SetResourceReference(DataGrid.AlternatingRowBackgroundProperty, "Panel_ToolbarBrush");

        var headerStyle = new Style(typeof(DataGridColumnHeader));
        headerStyle.Setters.Add(new Setter(BackgroundProperty,      new DynamicResourceExtension("Panel_ToolbarBrush")));
        headerStyle.Setters.Add(new Setter(ForegroundProperty,      new DynamicResourceExtension("Panel_ToolbarForegroundBrush")));
        headerStyle.Setters.Add(new Setter(BorderBrushProperty,     new DynamicResourceExtension("Panel_ToolbarBorderBrush")));
        headerStyle.Setters.Add(new Setter(BorderThicknessProperty, new Thickness(0, 0, 1, 1)));
        headerStyle.Setters.Add(new Setter(PaddingProperty,         new Thickness(6, 3, 6, 3)));
        headerStyle.Setters.Add(new Setter(FontSizeProperty,        11d));
        headerStyle.Setters.Add(new Setter(SnapsToDevicePixelsProperty, true));
        _grid.ColumnHeaderStyle = headerStyle;

        var rowStyle = new Style(typeof(DataGridRow));
        rowStyle.Setters.Add(new Setter(ForegroundProperty, new DynamicResourceExtension("TE_Foreground")));
        _grid.RowStyle = rowStyle;

        _grid.Columns.Add(MakePinColumn());
        _grid.Columns.Add(MakeCol("StringExtract_ColOffset",   nameof(StringRun.Offset),   80,  "X8",  "StringExtract_TtOffset"));
        _grid.Columns.Add(MakeCol("StringExtract_ColLength",   nameof(StringRun.Length),   55,  null,  "StringExtract_TtLength"));
        _grid.Columns.Add(MakeCol("StringExtract_ColEncoding", nameof(StringRun.Encoding), 85,  null,  "StringExtract_TtEncoding"));
        _grid.Columns.Add(MakeCol("StringExtract_ColBytes",    nameof(StringRun.RawHex),   160, null,  "StringExtract_TtBytes"));
        _grid.Columns.Add(MakeDuplicateColumn());
        _grid.Columns.Add(MakeContextBytesColumn());
        _grid.Columns.Add(MakeKindColumn());
        _grid.Columns.Add(MakeValueColumn());
        var scoreCol   = MakeScoreColumn();
        var clusterCol = MakeClusterColumn();
        _grid.Columns.Add(scoreCol);
        _grid.Columns.Add(clusterCol);
        _grid.ColumnHeaderStyle.Setters.Add(new EventSetter(UIElement.MouseRightButtonUpEvent,
            new MouseButtonEventHandler((_, e2) =>
            {
                if (scoreCol.Visibility == Visibility.Collapsed)
                    scoreCol.Visibility = Visibility.Visible;
                else if (clusterCol.Visibility == Visibility.Collapsed)
                    clusterCol.Visibility = Visibility.Visible;
                else
                {
                    scoreCol.Visibility   = Visibility.Collapsed;
                    clusterCol.Visibility = Visibility.Collapsed;
                }
            })));

        // Sync SelectedGridItem in VM when DataGrid selection changes
        _grid.SelectionChanged += (_, _) =>
        {
            if (_grid.SelectedItem is StringRun run)
                _vm.SelectedGridItem = run;
        };

        // Sync DataGrid to VM when VM sets SelectedGridItem or refreshes DisplayList
        _vm.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(_vm.DisplayList))
            {
                _grid.ItemsSource = null;
                _grid.ItemsSource = _vm.DisplayList;
            }
            else if (e.PropertyName == nameof(_vm.SelectedGridItem))
            {
                if (_vm.SelectedGridItem is null || ReferenceEquals(_grid.SelectedItem, _vm.SelectedGridItem)) return;
                _grid.SelectedItem = _vm.SelectedGridItem;
                _grid.ScrollIntoView(_vm.SelectedGridItem);
            }
        };

        _grid.ItemsSource       = _vm.DisplayList;
        _grid.MouseDoubleClick += (_, _) => NavigateSelected();
        _grid.ContextMenu       = BuildContextMenu();

        return _grid;
    }

    private DataGridTemplateColumn MakePinColumn()
    {
        var col = new DataGridTemplateColumn { Header = "", Width = 22, CanUserSort = false };

        var cellTemplate = new DataTemplate();
        var btnFactory = new FrameworkElementFactory(typeof(Button));
        btnFactory.SetValue(Button.ContentProperty, "");
        btnFactory.SetValue(Button.FontFamilyProperty, new FontFamily("Segoe MDL2 Assets"));
        btnFactory.SetValue(Button.FontSizeProperty, 10d);
        btnFactory.SetValue(Button.WidthProperty, 18d);
        btnFactory.SetValue(Button.HeightProperty, 18d);
        btnFactory.SetValue(Button.PaddingProperty, new Thickness(0));
        btnFactory.SetValue(Button.BorderThicknessProperty, new Thickness(0));
        btnFactory.SetValue(Button.BackgroundProperty, Brushes.Transparent);
        btnFactory.SetValue(Button.FocusVisualStyleProperty, null as Style);
        btnFactory.SetResourceReference(Button.ToolTipProperty, "StringExtract_TtPin");
        btnFactory.AddHandler(Button.ClickEvent, new RoutedEventHandler((s, _) =>
        {
            if (s is FrameworkElement fe && fe.DataContext is StringRun r)
                _vm.TogglePin(r);
        }));
        cellTemplate.VisualTree = btnFactory;
        col.CellTemplate = cellTemplate;
        return col;
    }

    private DataGridTemplateColumn MakeDuplicateColumn()
    {
        var dupHdr = new TextBlock();
        dupHdr.SetResourceReference(TextBlock.TextProperty, "StringExtract_ColDup");
        dupHdr.SetResourceReference(ToolTipProperty,        "StringExtract_TtDup");
        var col = new DataGridTemplateColumn
        {
            Header = dupHdr,
            Width  = new DataGridLength(40),
            CanUserSort = false,
        };
        var cellTemplate = new DataTemplate();
        var tbFactory = new FrameworkElementFactory(typeof(TextBlock));
        tbFactory.SetValue(TextBlock.TextAlignmentProperty, TextAlignment.Center);
        tbFactory.SetValue(TextBlock.FontSizeProperty, 10d);
        tbFactory.SetValue(TextBlock.VerticalAlignmentProperty, VerticalAlignment.Center);
        tbFactory.AddHandler(FrameworkElement.LoadedEvent, new RoutedEventHandler((s, _) =>
        {
            if (s is not TextBlock tb) return;
            void Refresh(object? _, DependencyPropertyChangedEventArgs __)
            {
                int c = tb.DataContext is StringRun r ? _vm.GetDuplicateCount(r) : 0;
                tb.Text       = c > 1 ? c.ToString() : string.Empty;
                tb.Foreground = c > 1 ? Brushes.OrangeRed : Brushes.Transparent;
            }
            tb.DataContextChanged += Refresh;
            tb.Unloaded += (_, _) => tb.DataContextChanged -= Refresh;
            Refresh(null, default);
        }));
        cellTemplate.VisualTree = tbFactory;
        col.CellTemplate = cellTemplate;
        return col;
    }

    private static readonly IReadOnlyDictionary<StringKind, (SolidColorBrush bg, string label)> KindBadges =
        new Dictionary<StringKind, (SolidColorBrush, string)>
        {
            [StringKind.Email]       = (FreezeB(new SolidColorBrush(Color.FromRgb(0x2E, 0x7D, 0x32))), "EMAIL"),
            [StringKind.Url]         = (FreezeB(new SolidColorBrush(Color.FromRgb(0x01, 0x57, 0x9B))), "URL"),
            [StringKind.PathWin]     = (FreezeB(new SolidColorBrush(Color.FromRgb(0x4A, 0x14, 0x8C))), "PATH"),
            [StringKind.PathUnix]    = (FreezeB(new SolidColorBrush(Color.FromRgb(0x4A, 0x14, 0x8C))), "PATH"),
            [StringKind.Guid]        = (FreezeB(new SolidColorBrush(Color.FromRgb(0xBF, 0x36, 0x0C))), "GUID"),
            [StringKind.RegistryKey] = (FreezeB(new SolidColorBrush(Color.FromRgb(0x78, 0x09, 0x16))), "REG"),
            [StringKind.Version]     = (FreezeB(new SolidColorBrush(Color.FromRgb(0x00, 0x60, 0x64))), "VER"),
            [StringKind.IpV4]        = (FreezeB(new SolidColorBrush(Color.FromRgb(0xE6, 0x51, 0x00))), "IPv4"),
            [StringKind.IpV6]        = (FreezeB(new SolidColorBrush(Color.FromRgb(0xE6, 0x51, 0x00))), "IPv6"),
            [StringKind.HexHash]     = (FreezeB(new SolidColorBrush(Color.FromRgb(0x37, 0x47, 0x4F))), "HASH"),
        };

    private DataGridTemplateColumn MakeKindColumn()
    {
        var hdr = new TextBlock();
        hdr.SetResourceReference(TextBlock.TextProperty, "StringExtract_ColKind");
        hdr.SetResourceReference(ToolTipProperty,        "StringExtract_TtKind");
        var col = new DataGridTemplateColumn
        {
            Header = hdr,
            Width  = new DataGridLength(52),
            CanUserSort = false,
        };
        var cellTemplate = new DataTemplate();
        var borderFactory = new FrameworkElementFactory(typeof(Border));
        borderFactory.SetValue(Border.CornerRadiusProperty, new CornerRadius(3));
        borderFactory.SetValue(Border.PaddingProperty,      new Thickness(4, 1, 4, 1));
        borderFactory.SetValue(Border.MarginProperty,       new Thickness(2, 1, 2, 1));
        borderFactory.SetValue(Border.HorizontalAlignmentProperty, HorizontalAlignment.Left);
        borderFactory.SetValue(Border.VerticalAlignmentProperty,   VerticalAlignment.Center);

        var tbFactory = new FrameworkElementFactory(typeof(TextBlock));
        tbFactory.SetValue(TextBlock.FontSizeProperty,   9d);
        tbFactory.SetValue(TextBlock.FontWeightProperty, FontWeights.SemiBold);
        tbFactory.SetValue(TextBlock.ForegroundProperty, Brushes.White);
        borderFactory.AppendChild(tbFactory);

        borderFactory.AddHandler(FrameworkElement.LoadedEvent, new RoutedEventHandler((s, _) =>
        {
            if (s is not Border b) return;
            var tb = (TextBlock)b.Child;

            void Refresh(object? _, DependencyPropertyChangedEventArgs __)
            {
                if (b.DataContext is StringRun r && r.Kind != StringKind.None && KindBadges.TryGetValue(r.Kind, out var badge))
                {
                    b.Background = badge.bg;
                    tb.Text      = badge.label;
                    b.Visibility = Visibility.Visible;
                }
                else
                {
                    b.Visibility = Visibility.Collapsed;
                }
            }
            b.DataContextChanged += Refresh;
            b.Unloaded += (_, _) => b.DataContextChanged -= Refresh;
            Refresh(null, default);
        }));

        cellTemplate.VisualTree = borderFactory;
        col.CellTemplate = cellTemplate;
        return col;
    }

    private DataGridTemplateColumn MakeValueColumn()
    {
        var hdr = new TextBlock();
        hdr.SetResourceReference(TextBlock.TextProperty, "StringExtract_ColValue");
        hdr.SetResourceReference(ToolTipProperty,        "StringExtract_TtValue");

        var col = new DataGridTemplateColumn
        {
            Header = hdr,
            Width  = DataGridLength.Auto,
        };

        var cellTemplate = new DataTemplate();
        var tbFactory = new FrameworkElementFactory(typeof(TextBlock));
        tbFactory.SetValue(TextBlock.FontSizeProperty, 11d);
        tbFactory.SetValue(TextBlock.VerticalAlignmentProperty, VerticalAlignment.Top);
        tbFactory.SetBinding(TextBlock.TextProperty, new Binding(nameof(StringRun.Value)));
        tbFactory.SetBinding(TextBlock.TextWrappingProperty, new Binding(nameof(_vm.WordWrap))
        {
            Source = _vm,
            Converter = new BoolToTextWrappingConverter(),
        });
        cellTemplate.VisualTree = tbFactory;
        col.CellTemplate = cellTemplate;
        return col;
    }

    private DataGridTemplateColumn MakeClusterColumn()
    {
        var hdr = new TextBlock();
        hdr.SetResourceReference(TextBlock.TextProperty, "StringExtract_ColCluster");
        hdr.SetResourceReference(ToolTipProperty,        "StringExtract_TtCluster");
        var col = new DataGridTemplateColumn
        {
            Header     = hdr,
            Width      = new DataGridLength(40),
            Visibility = Visibility.Collapsed,
            CanUserSort = false,
        };
        var cellTemplate = new DataTemplate();
        var tbFactory = new FrameworkElementFactory(typeof(TextBlock));
        tbFactory.SetValue(TextBlock.TextAlignmentProperty, TextAlignment.Center);
        tbFactory.SetValue(TextBlock.FontSizeProperty, 10d);
        tbFactory.SetValue(TextBlock.VerticalAlignmentProperty, VerticalAlignment.Center);
        tbFactory.AddHandler(FrameworkElement.LoadedEvent, new RoutedEventHandler((s, _) =>
        {
            if (s is not TextBlock tb) return;
            void Refresh(object? _, DependencyPropertyChangedEventArgs __)
            {
                int id = tb.DataContext is StringRun r ? _vm.GetClusterId(r) : 0;
                tb.Text       = id > 0 ? $"C{id}" : string.Empty;
                tb.Foreground = id > 0 ? Brushes.CornflowerBlue : Brushes.Transparent;
            }
            tb.DataContextChanged += Refresh;
            tb.Unloaded += (_, _) => tb.DataContextChanged -= Refresh;
            Refresh(null, default);
        }));
        cellTemplate.VisualTree = tbFactory;
        col.CellTemplate = cellTemplate;
        return col;
    }

    private static DataGridTextColumn MakeScoreColumn()
    {
        var col = MakeCol("StringExtract_ColScore", nameof(StringRun.ReadabilityScore), 55, "F2", "StringExtract_TtScore");
        col.Visibility = Visibility.Collapsed;
        return col;
    }

    private DataGridTemplateColumn MakeContextBytesColumn()
    {
        var hdrTb = new TextBlock();
        hdrTb.SetResourceReference(TextBlock.TextProperty, "StringExtract_ColContext");
        hdrTb.SetResourceReference(ToolTipProperty,        "StringExtract_TtContext");
        var col = new DataGridTemplateColumn
        {
            Header = hdrTb,
            Width  = new DataGridLength(180),
            CanUserSort = false,
        };

        var cellTemplate = new DataTemplate();
        var tbFactory = new FrameworkElementFactory(typeof(TextBlock));
        tbFactory.SetValue(TextBlock.FontSizeProperty, 9d);
        tbFactory.SetValue(TextBlock.FontFamilyProperty, new FontFamily("Consolas, Courier New"));
        tbFactory.SetValue(TextBlock.VerticalAlignmentProperty, VerticalAlignment.Center);
        tbFactory.AddHandler(FrameworkElement.LoadedEvent, new RoutedEventHandler((s, _) =>
        {
            if (s is not TextBlock tb) return;
            void Refresh(object? _, DependencyPropertyChangedEventArgs __)
            {
                tb.Inlines.Clear();
                if (tb.DataContext is StringRun r) BuildContextInlines(tb, r, 4);
            }
            tb.DataContextChanged += Refresh;
            tb.Unloaded += (_, _) => tb.DataContextChanged -= Refresh;
            Refresh(null, default);
        }));
        cellTemplate.VisualTree = tbFactory;
        col.CellTemplate = cellTemplate;
        return col;
    }

    private void BuildContextInlines(TextBlock tb, StringRun run, int n)
    {
        var buf = _vm.LastBuffer;
        if (buf is null) return;

        long start        = Math.Max(0, run.Offset - n);
        long end          = Math.Min(buf.Length - 1, run.Offset + run.Length + n - 1);
        var  encodingColor = EncodingChipColor(run.Encoding);
        bool first        = true;

        void AppendByte(byte b, SolidColorBrush color, FontWeight weight)
        {
            if (!first) tb.Inlines.Add(new System.Windows.Documents.Run(" ") { Foreground = color });
            tb.Inlines.Add(new System.Windows.Documents.Run(HexByteLookup[b]) { Foreground = color, FontWeight = weight });
            first = false;
        }

        for (long i = start; i < run.Offset; i++)
            AppendByte(buf[i], ContextByteBrush, FontWeights.Normal);

        for (long i = run.Offset; i < run.Offset + run.Length && i <= end; i++)
            AppendByte(buf[i], encodingColor, FontWeights.SemiBold);

        for (long i = run.Offset + run.Length; i <= end; i++)
            AppendByte(buf[i], ContextByteBrush, FontWeights.Normal);
    }

    private static DataGridTextColumn MakeCol(string header, string path, double width, string? format = null, string? tooltip = null)
    {
        var binding = new Binding(path);
        if (format is not null) binding.StringFormat = $"{{0:{format}}}";
        var col = new DataGridTextColumn
        {
            Binding = binding,
            Width   = width > 0 ? new DataGridLength(width) : DataGridLength.Auto,
        };
        var hdr = new TextBlock();
        hdr.SetResourceReference(TextBlock.TextProperty, header);
        if (tooltip is not null) hdr.SetResourceReference(ToolTipProperty, tooltip);
        col.Header = hdr;
        return col;
    }

    // ── Context menu ──────────────────────────────────────────────────────────

    private ContextMenu BuildContextMenu()
    {
        var cm = new ContextMenu();
        cm.SetResourceReference(BackgroundProperty, "TE_Background");
        cm.SetResourceReference(ForegroundProperty, "TE_Foreground");

        cm.Items.Add(MakeMenuItem("Go to Offset",     "", () => NavigateSelected()));
        cm.Items.Add(new Separator());
        cm.Items.Add(MakeMenuItem("Copy Value",       "", () => CopyField(r => r.Value)));
        cm.Items.Add(MakeMenuItem("Copy Offset",      "", () => CopyField(r => $"0x{r.Offset:X8}")));
        cm.Items.Add(MakeMenuItem("Copy Row (TSV)",   "", () => CopyField(r => $"0x{r.Offset:X8}\t{r.Length}\t{r.Encoding}\t{r.Value}")));
        cm.Items.Add(MakeMenuItem("Copy as C Array",  "", () => CopyCArray()));
        cm.Items.Add(MakeMenuItem("Copy as JSON",     "", () => CopyAsJson()));
        cm.Items.Add(new Separator());
        cm.Items.Add(MakeMenuItem("Pin / Unpin",      "", () =>
        {
            foreach (var r in _grid.SelectedItems.OfType<StringRun>()) _vm.TogglePin(r);
        }));
        cm.Items.Add(new Separator());
        cm.Items.Add(MakeMenuItem("Highlight Selected", "", () => _vm.HighlightRuns(_grid.SelectedItems.OfType<StringRun>())));
        cm.Items.Add(MakeMenuItem("Highlight All",      "", () => _vm.HighlightRuns(_vm.DisplayList)));
        cm.Items.Add(MakeMenuItem("Clear Highlights",   "", () => _vm.ClearHighlights()));
        cm.Items.Add(new Separator());
        cm.Items.Add(MakeMenuItem("Export Selected…", "", () => OnExport(exportAll: false)));
        cm.Items.Add(MakeMenuItem("Export All…",      "", () => OnExport(exportAll: true)));
        return cm;
    }

    private MenuItem MakeMenuItem(string header, string glyph, Action action)
    {
        var icon = new TextBlock
        {
            Text = glyph,
            FontFamily = new FontFamily("Segoe MDL2 Assets"),
            FontSize = 12, Width = 16,
        };
        icon.SetResourceReference(ForegroundProperty, "Panel_ToolbarForegroundBrush");

        var item = new MenuItem { Header = header, Icon = icon };
        item.SetResourceReference(ForegroundProperty, "TE_Foreground");
        item.Click += (_, _) => action();
        return item;
    }

    private void CopyCArray()
    {
        var runs = _grid.SelectedItems.OfType<StringRun>().ToList();
        if (runs.Count == 0) return;
        var sb = new StringBuilder();
        foreach (var run in runs)
        {
            var hex  = run.RawHex.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            var name = $"str_{run.Offset:X8}";
            sb.Append($"static const uint8_t {name}[] = {{ ");
            sb.Append(string.Join(", ", hex.Select(h => $"0x{h}")));
            sb.AppendLine(" };");
        }
        Clipboard.SetText(sb.ToString());
    }

    private void CopyAsJson()
    {
        var runs = _grid.SelectedItems.OfType<StringRun>().ToList();
        if (runs.Count == 0) return;
        var sb = new StringBuilder("[");
        for (int i = 0; i < runs.Count; i++)
        {
            var r = runs[i];
            if (i > 0) sb.Append(',');
            sb.Append($"{{\"offset\":\"0x{r.Offset:X8}\",\"length\":{r.Length},\"encoding\":\"{r.Encoding}\",\"value\":{System.Text.Json.JsonSerializer.Serialize(r.Value)}}}");
        }
        sb.Append(']');
        Clipboard.SetText(sb.ToString());
    }

    // ── Stats bar ─────────────────────────────────────────────────────────────

    private UIElement BuildStatsBar()
    {
        var border = new Border { BorderThickness = new Thickness(0, 0, 0, 1), Padding = new Thickness(4, 2, 4, 2) };
        border.SetResourceReference(Border.BackgroundProperty,  "Panel_ToolbarBrush");
        border.SetResourceReference(Border.BorderBrushProperty, "Panel_ToolbarBorderBrush");

        _statsBar = new WrapPanel { Orientation = Orientation.Horizontal };
        border.Child = _statsBar;
        return border;
    }

    private void RefreshStatsBar()
    {
        if (_statsBar is null) return;
        _statsBar.Children.Clear();

        foreach (var kvp in _vm.EncodingCounts.OrderByDescending(x => x.Value))
        {
            var chip = new Border
            {
                Margin          = new Thickness(0, 0, 4, 0),
                Padding         = new Thickness(6, 1, 6, 1),
                CornerRadius    = new CornerRadius(8),
                Background      = EncodingChipColor(kvp.Key),
            };
            var txt = new TextBlock
            {
                Text = $"{EncodingLabel(kvp.Key)}: {kvp.Value}",
                FontSize = 10, Foreground = Brushes.White,
            };
            chip.Child = txt;
            _statsBar.Children.Add(chip);
        }
    }

    private static SolidColorBrush EncodingChipColor(StringEncoding enc) =>
        EncodingPalette.Brushes.TryGetValue(enc, out var b) ? b : EncodingPalette.FallbackBrush;

    // ── Status bar ────────────────────────────────────────────────────────────

    private UIElement BuildStatusBar()
    {
        var bar = new Border
        {
            Padding = new Thickness(6, 2, 6, 2),
            BorderThickness = new Thickness(0, 1, 0, 0),
            Height = 20,
        };
        bar.SetResourceReference(Border.BackgroundProperty,  "Panel_ToolbarBrush");
        bar.SetResourceReference(Border.BorderBrushProperty, "Panel_ToolbarBorderBrush");

        var row = new DockPanel { LastChildFill = true };

        var progress = new System.Windows.Controls.ProgressBar
        {
            Width = 100, Height = 10, IsIndeterminate = true,
            Margin = new Thickness(8, 0, 0, 0), VerticalAlignment = VerticalAlignment.Center,
        };
        progress.SetBinding(UIElement.VisibilityProperty, new Binding(nameof(_vm.IsBusy))
        {
            Source = _vm, Converter = new BooleanToVisibilityConverter(),
        });
        DockPanel.SetDock(progress, Dock.Right);

        var statusTxt = new TextBlock { FontSize = 11, VerticalAlignment = VerticalAlignment.Center };
        statusTxt.SetResourceReference(ForegroundProperty, "Panel_ToolbarForegroundBrush");
        statusTxt.SetBinding(TextBlock.TextProperty, new Binding(nameof(_vm.StatusText)) { Source = _vm });

        row.Children.Add(progress);
        row.Children.Add(statusTxt);
        bar.Child = row;
        return bar;
    }

    private void UpdateOutdatedBadge()
    {
        if (_outdatedBadge is null) return;
        _outdatedBadge.Visibility = _vm.IsOutdated ? Visibility.Visible : Visibility.Collapsed;
    }

    // ── Public API ────────────────────────────────────────────────────────────

    public void SetContext(IIDEHostContext context)
    {
        _vm.SetContext(context);
        RestoreLastTbl();
    }

    private void RestoreLastTbl()
    {
        var path = _options.LastTblFilePath;
        if (path is null || !File.Exists(path)) return;
        _loadedTbl?.Dispose();
        _loadedTbl = new TblStream(path);
        _vm.SetTblTable(new TblDecodeTableAdapter(_loadedTbl));
        UpdateTblIndicator(path);
    }
    public void OnFileOpened()
    {
        _vm.RefreshOpenedFiles();
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _loadedTbl?.Dispose();
        _loadedTbl = null;
        _vm.Dispose();
    }

    // ── Actions ───────────────────────────────────────────────────────────────

    private void NavigateSelected()
    {
        if (_grid.SelectedItem is StringRun run)
            _vm.NavigateToOffset(run);
    }

    private void CopyField(Func<StringRun, string> selector)
    {
        var text = string.Join(Environment.NewLine, _grid.SelectedItems.OfType<StringRun>().Select(selector));
        if (!string.IsNullOrEmpty(text)) Clipboard.SetText(text);
    }

    private void OnKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.F5)
        {
            _ = _vm.RunAsync(); e.Handled = true;
        }
        else if (e.Key == Key.F3 && Keyboard.Modifiers == ModifierKeys.Shift)
        {
            _vm.NavigateHighlightPrev(); e.Handled = true;
        }
        else if (e.Key == Key.F3)
        {
            _vm.NavigateHighlightNext(); e.Handled = true;
        }
        else if (e.Key == Key.Enter && !_vm.IsBusy)
        {
            NavigateSelected(); e.Handled = true;
        }
        else if (e.Key == Key.C && Keyboard.Modifiers == ModifierKeys.Control)
        {
            CopyField(r => r.Value); e.Handled = true;
        }
    }

    private void OnLoadTbl(object sender, RoutedEventArgs e)
    {
        var dlg = new OpenFileDialog
        {
            Title = "Load TBL file",
            Filter = "TBL Files (*.tbl;*.tblx)|*.tbl;*.tblx|All Files (*.*)|*.*",
            DefaultExt = ".tbl",
        };
        if (dlg.ShowDialog() != true) return;

        _loadedTbl?.Dispose();
        _loadedTbl = new TblStream(dlg.FileName);
        _vm.SetTblTable(new TblDecodeTableAdapter(_loadedTbl));
        _vm.PublishLoadTbl(dlg.FileName);
        UpdateTblIndicator(dlg.FileName);
        _options.LastTblFilePath = dlg.FileName;
        _options.Save();
    }

    private void OnExport(bool exportAll)
    {
        var runs = (exportAll ? _vm.GetAllRuns() : _grid.SelectedItems.OfType<StringRun>()).ToList();
        if (runs.Count == 0) return;

        var dlg = new SaveFileDialog
        {
            Title      = exportAll ? "Export All Strings" : "Export Selected Strings",
            Filter     = string.Join("|", StringExtractionExporters.All.Select(x => x.FileFilter)),
            DefaultExt = ".txt",
            FileName   = "strings_export",
        };
        if (dlg.ShowDialog() != true) return;

        var ext      = Path.GetExtension(dlg.FileName);
        var exporter = StringExtractionExporters.All.FirstOrDefault(x => x.DefaultExt.Equals(ext, StringComparison.OrdinalIgnoreCase))
                    ?? StringExtractionExporters.All[0];

        _ = exporter.ExportAsync(runs, dlg.FileName);
    }
}

file sealed class BoolToTextWrappingConverter : System.Windows.Data.IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        => value is true ? TextWrapping.Wrap : TextWrapping.NoWrap;

    public object ConvertBack(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        => throw new NotSupportedException();
}
