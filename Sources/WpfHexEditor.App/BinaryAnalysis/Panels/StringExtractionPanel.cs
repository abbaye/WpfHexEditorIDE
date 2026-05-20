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
    private readonly StringExtractionViewModel _vm = new();
    private DataGrid _grid = null!;
    private TblStream? _loadedTbl;
    private bool _disposed;
    private TextBlock? _tblNameLabel;
    private Button? _tblClearBtn;
    private TextBlock? _outdatedBadge;
    private WrapPanel? _statsBar;

    public StringExtractionPanel()
    {
        var root = new Grid();
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        root.SetResourceReference(BackgroundProperty, "TE_Background");

        var toolbarPanel = BuildToolbarWithFilter();
        var grid         = BuildGrid();
        var statsRow     = BuildStatsBar();
        var statusBar    = BuildStatusBar();

        Grid.SetRow(toolbarPanel, 0);
        Grid.SetRow(grid,         1);
        Grid.SetRow(statsRow,     2);
        Grid.SetRow(statusBar,    3);

        root.Children.Add(toolbarPanel);
        root.Children.Add(grid);
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

        var highlightBtn  = MakeToolbarButton("", "StringExtract_TtHighlight");
        highlightBtn.Click += (_, _) => _vm.HighlightRuns(_vm.ResultsView.Cast<StringRun>());
        var clearHlBtn    = MakeToolbarButton("", "StringExtract_TtClearHighlight");
        clearHlBtn.Click += (_, _) => _vm.ClearHighlights();

        // Caret sync toggle
        var syncBtn = new ToggleButton
        {
            Content  = "",
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

        foreach (UIElement el in new UIElement[]
        {
            runBtn, cancelBtn, MakeToolbarSeparator(),
            tblBtn, MakeToolbarSeparator(),
            exportBtn, MakeToolbarSeparator(),
            highlightBtn, clearHlBtn, MakeToolbarSeparator(),
            syncBtn, _outdatedBadge,
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

        // Entropy toggle + threshold slider
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

    private static Button MakeToolbarButton(string glyph, string tooltipKey)
    {
        var btn = new Button
        {
            Content          = glyph,
            Width            = 22,
            Height           = 22,
            Padding          = new Thickness(0),
            Margin           = new Thickness(1, 0, 1, 0),
            BorderThickness  = new Thickness(0),
            Background       = Brushes.Transparent,
            FontFamily       = new FontFamily("Segoe MDL2 Assets"),
            FontSize         = 13,
            VerticalAlignment = VerticalAlignment.Center,
            FocusVisualStyle = null,
        };
        btn.SetResourceReference(StyleProperty,      "PanelIconButtonStyle");
        btn.SetResourceReference(ForegroundProperty, "Panel_ToolbarForegroundBrush");
        btn.SetResourceReference(ToolTipProperty,    tooltipKey);
        return btn;
    }

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

        _tblClearBtn = new Button
        {
            Content = "",
            Width = 16, Height = 16, Padding = new Thickness(0), BorderThickness = new Thickness(0),
            Background = Brushes.Transparent,
            FontFamily = new FontFamily("Segoe MDL2 Assets"),
            FontSize = 9, VerticalAlignment = VerticalAlignment.Center,
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
        _grid.Columns.Add(MakeCol("StringExtract_ColValue",    nameof(StringRun.Value),    0,   null,  "StringExtract_TtValue"));

        // Sync SelectedGridItem in VM when DataGrid selection changes
        _grid.SelectionChanged += (_, _) =>
        {
            if (_grid.SelectedItem is StringRun run)
                _vm.SelectedGridItem = run;
        };

        // Sync DataGrid to VM when VM sets SelectedGridItem
        _vm.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName != nameof(_vm.SelectedGridItem)) return;
            if (_vm.SelectedGridItem is null || ReferenceEquals(_grid.SelectedItem, _vm.SelectedGridItem)) return;
            _grid.SelectedItem = _vm.SelectedGridItem;
            _grid.ScrollIntoView(_vm.SelectedGridItem);
        };

        _grid.ItemsSource       = _vm.ResultsView;
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

        long start = Math.Max(0, run.Offset - n);
        long end   = Math.Min(buf.Length - 1, run.Offset + run.Length + n - 1);

        var encodingColor = EncodingChipColor(run.Encoding);
        var contextColor  = new SolidColorBrush(Color.FromRgb(0x70, 0x70, 0x70));

        // Pre-context bytes (grey)
        for (long i = start; i < run.Offset; i++)
        {
            if (tb.Inlines.Count > 0) tb.Inlines.Add(new System.Windows.Documents.Run(" ") { Foreground = contextColor });
            tb.Inlines.Add(new System.Windows.Documents.Run(buf[i].ToString("X2")) { Foreground = contextColor });
        }

        // Run bytes (encoding color, slightly brighter)
        for (long i = run.Offset; i < run.Offset + run.Length && i <= end; i++)
        {
            if (tb.Inlines.Count > 0) tb.Inlines.Add(new System.Windows.Documents.Run(" ") { Foreground = encodingColor });
            tb.Inlines.Add(new System.Windows.Documents.Run(buf[i].ToString("X2")) { Foreground = encodingColor, FontWeight = FontWeights.SemiBold });
        }

        // Post-context bytes (grey)
        for (long i = run.Offset + run.Length; i <= end; i++)
        {
            if (tb.Inlines.Count > 0) tb.Inlines.Add(new System.Windows.Documents.Run(" ") { Foreground = contextColor });
            tb.Inlines.Add(new System.Windows.Documents.Run(buf[i].ToString("X2")) { Foreground = contextColor });
        }
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
        // headerKey and tooltip are resource keys when present
        if (tooltip is not null)
        {
            var hdr = new TextBlock();
            hdr.SetResourceReference(TextBlock.TextProperty, header);
            hdr.SetResourceReference(ToolTipProperty,        tooltip);
            col.Header = hdr;
        }
        else
        {
            var hdr = new TextBlock();
            hdr.SetResourceReference(TextBlock.TextProperty, header);
            col.Header = hdr;
        }
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
        cm.Items.Add(new Separator());
        cm.Items.Add(MakeMenuItem("Pin / Unpin",      "", () =>
        {
            foreach (var r in _grid.SelectedItems.OfType<StringRun>()) _vm.TogglePin(r);
        }));
        cm.Items.Add(new Separator());
        cm.Items.Add(MakeMenuItem("Highlight Selected", "", () => _vm.HighlightRuns(_grid.SelectedItems.OfType<StringRun>())));
        cm.Items.Add(MakeMenuItem("Highlight All",      "", () => _vm.HighlightRuns(_vm.ResultsView.Cast<StringRun>())));
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

    private static SolidColorBrush EncodingChipColor(StringEncoding enc) => enc switch
    {
        StringEncoding.Tbl or StringEncoding.TblDte or StringEncoding.TblMte => new SolidColorBrush(Color.FromRgb(0x4C, 0xAF, 0x50)),
        StringEncoding.Ascii                                                   => new SolidColorBrush(Color.FromRgb(0x42, 0x8B, 0xCA)),
        StringEncoding.Utf8 or StringEncoding.Utf16Le or StringEncoding.Utf16Be => new SolidColorBrush(Color.FromRgb(0x00, 0xBC, 0xD4)),
        StringEncoding.Ebcdic or StringEncoding.EbcdicNoSpec                   => new SolidColorBrush(Color.FromRgb(0xFF, 0x98, 0x00)),
        StringEncoding.Latin1                                                   => new SolidColorBrush(Color.FromRgb(0xAB, 0x47, 0xBC)),
        _                                                                       => new SolidColorBrush(Color.FromRgb(0x90, 0x90, 0x90)),
    };

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

    public void SetContext(IIDEHostContext context) => _vm.SetContext(context);
    public void OnFileOpened() => _vm.ResultsView.Refresh();

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
