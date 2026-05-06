// ==========================================================
// Project: WpfHexEditor.Plugins.XamlDesigner
// File: Panels/CodeGenPanel.cs
// Author: Derek Tremblay
// Created: 2026-05-05
// Description:
//     Dockable panel for the XAML Designer's code-behind generation feature.
//     Displays:
//       - Live sync toggle
//       - Named elements list (type, field name, source line)
//       - Event sinks list (element.Event → HandlerName)
//       - Force Regenerate / Preview C# / Open code-behind buttons
//       - Error/status banners
//     Built entirely in code (no XAML) to avoid a circular BAML dependency
//     between the designer and the code generation infrastructure.
//
// Architecture Notes:
//     Follows the VS-Like Panel Pattern used by DesignHistoryPanel:
//       - public ViewModel property for plugin wiring.
//       - Loaded/Unloaded manage event subscriptions.
//       - No business logic in code-behind — everything in ViewModel.
// ==========================================================

using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using WpfHexEditor.Plugins.XamlDesigner.ViewModels;
using WpfHexEditor.SDK.ExtensionPoints.XamlDesigner;

namespace WpfHexEditor.Plugins.XamlDesigner.Panels;

/// <summary>
/// Code-behind generation dockable panel for the XAML Designer.
/// </summary>
public sealed class CodeGenPanel : UserControl
{
    private readonly CodeGenPanelViewModel _vm = new();

    // Controls that need runtime reference.
    private readonly ToggleButton  _toggleSync;
    private readonly TextBlock     _statusLabel;
    private readonly TextBlock     _lastGenLabel;
    private readonly TextBlock     _errorBanner;
    private readonly ListView      _namedElementsList;
    private readonly ListView      _eventSinksList;
    private readonly Button        _btnForceRegen;
    private readonly Button        _btnPreview;

    public CodeGenPanelViewModel ViewModel => _vm;

    public CodeGenPanel()
    {
        DataContext = _vm;
        Background  = new SolidColorBrush(Color.FromRgb(0x1E, 0x1E, 0x1E));

        // ── Root layout ───────────────────────────────────────────────────────
        var root = new DockPanel { LastChildFill = true };

        // ── Header bar ────────────────────────────────────────────────────────
        var header = new StackPanel
        {
            Orientation = Orientation.Vertical,
            Margin      = new Thickness(4, 4, 4, 0)
        };
        DockPanel.SetDock(header, Dock.Top);

        // Sync toggle row
        var syncRow = new DockPanel { Margin = new Thickness(0, 0, 0, 4) };

        _toggleSync = new ToggleButton
        {
            Content    = "Live sync enabled",
            IsChecked  = true,
            Margin     = new Thickness(0, 0, 8, 0),
            Padding    = new Thickness(6, 2, 6, 2),
            FontSize   = 11
        };
        _toggleSync.SetResourceReference(ForegroundProperty, "ToolWindowText");
        _toggleSync.Checked   += (_, _) => _vm.IsEnabled = true;
        _toggleSync.Unchecked += (_, _) => _vm.IsEnabled = false;
        DockPanel.SetDock(_toggleSync, Dock.Left);
        syncRow.Children.Add(_toggleSync);

        _lastGenLabel = new TextBlock
        {
            FontSize            = 10,
            VerticalAlignment   = VerticalAlignment.Center,
            Opacity             = 0.6
        };
        _lastGenLabel.SetResourceReference(ForegroundProperty, "ToolWindowText");
        _lastGenLabel.SetBinding(TextBlock.TextProperty,
            new System.Windows.Data.Binding(nameof(CodeGenPanelViewModel.LastGenTime)));
        syncRow.Children.Add(_lastGenLabel);

        header.Children.Add(syncRow);

        // Status label
        _statusLabel = new TextBlock
        {
            FontSize   = 11,
            FontWeight = FontWeights.SemiBold,
            Margin     = new Thickness(0, 0, 0, 4)
        };
        _statusLabel.SetResourceReference(ForegroundProperty, "ToolWindowText");
        _statusLabel.SetBinding(TextBlock.TextProperty,
            new System.Windows.Data.Binding(nameof(CodeGenPanelViewModel.StatusText)));
        header.Children.Add(_statusLabel);

        // Error banner (hidden when no error)
        _errorBanner = new TextBlock
        {
            FontSize   = 11,
            Foreground = Brushes.OrangeRed,
            Margin     = new Thickness(0, 0, 0, 4),
            TextWrapping = TextWrapping.Wrap
        };
        _errorBanner.SetBinding(TextBlock.TextProperty,
            new System.Windows.Data.Binding(nameof(CodeGenPanelViewModel.ErrorText)));
        _errorBanner.SetBinding(VisibilityProperty,
            new System.Windows.Data.Binding(nameof(CodeGenPanelViewModel.HasError))
            {
                Converter = new BoolToVisibilityConverter()
            });
        header.Children.Add(_errorBanner);

        root.Children.Add(header);

        // ── Button bar (bottom) ───────────────────────────────────────────────
        var btnBar = new WrapPanel
        {
            Margin      = new Thickness(4, 4, 4, 4),
            Orientation = Orientation.Horizontal
        };
        DockPanel.SetDock(btnBar, Dock.Bottom);

        _btnForceRegen = MakeButton("Force Regenerate",
            nameof(CodeGenPanelViewModel.ForceRegenerateCommand));
        _btnPreview    = MakeButton("Preview C#…",
            nameof(CodeGenPanelViewModel.GeneratePreviewCommand));
        var btnOpenFile = new Button
        {
            Content = "Open code-behind",
            Padding = new Thickness(6, 2, 6, 2),
            Margin  = new Thickness(0, 0, 4, 0),
            FontSize = 11
        };
        btnOpenFile.Click += (_, _) => _vm.ForceRegenerateCommand.Execute(null);

        btnBar.Children.Add(_btnForceRegen);
        btnBar.Children.Add(_btnPreview);
        btnBar.Children.Add(btnOpenFile);

        root.Children.Add(btnBar);

        // ── Main content: named elements + event sinks ────────────────────────
        var content = new Grid();
        content.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        content.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        content.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });

        // Named elements section.
        var namedSection = BuildSection(
            "Named Elements",
            nameof(CodeGenPanelViewModel.NamedElementsCountLabel),
            out _namedElementsList,
            isEventList: false);
        Grid.SetRow(namedSection, 0);
        content.Children.Add(namedSection);

        // Separator
        var sep = new GridSplitter
        {
            Height             = 1,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment   = VerticalAlignment.Center,
            ResizeBehavior      = GridResizeBehavior.PreviousAndNext,
            Margin              = new Thickness(0, 2, 0, 2),
            Opacity             = 0.3
        };
        sep.SetResourceReference(BackgroundProperty, "ToolWindowText");
        Grid.SetRow(sep, 1);
        content.Children.Add(sep);

        // Event sinks section.
        var sinkSection = BuildSection(
            "Event Sinks",
            nameof(CodeGenPanelViewModel.EventSinksCountLabel),
            out _eventSinksList,
            isEventList: true);
        Grid.SetRow(sinkSection, 2);
        content.Children.Add(sinkSection);

        root.Children.Add(content);
        Content = root;

        Loaded   += OnLoaded;
        Unloaded += OnUnloaded;
    }

    // ── Public API ────────────────────────────────────────────────────────────

    /// <summary>
    /// Wires the panel to the active document's code generation service.
    /// Called by the plugin on each XAML document focus switch.
    /// </summary>
    public void Attach(ICodeBehindGeneratorService service)
        => _vm.Attach(service);

    /// <summary>Detaches from the current document.</summary>
    public void Detach()
        => _vm.Detach();

    // ── Lifecycle ─────────────────────────────────────────────────────────────

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        _vm.PreviewRequested -= OnPreviewRequested;
        _vm.PreviewRequested += OnPreviewRequested;

        // Bind list ItemsSource now (DataContext is set in ctor, but INPC needed).
        _namedElementsList.ItemsSource = _vm.NamedElements;
        _eventSinksList.ItemsSource    = _vm.EventSinks;
    }

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        _vm.PreviewRequested -= OnPreviewRequested;
    }

    private void OnPreviewRequested(object? sender, string preview)
    {
        // Show a floating preview window with the generated C# text.
        var win = new Window
        {
            Title  = "Generated Code-Behind Preview",
            Width  = 600,
            Height = 500,
            Owner  = Window.GetWindow(this)
        };
        var tb = new TextBox
        {
            Text              = preview,
            IsReadOnly        = true,
            FontFamily        = new FontFamily("Consolas, Courier New"),
            FontSize          = 12,
            AcceptsReturn     = true,
            VerticalScrollBarVisibility   = ScrollBarVisibility.Auto,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Auto
        };
        tb.SetResourceReference(ForegroundProperty, "ToolWindowText");
        tb.SetResourceReference(BackgroundProperty, "ToolWindowBackground");
        win.Content = tb;
        win.ShowDialog();
    }

    // ── Build helpers ─────────────────────────────────────────────────────────

    private static Grid BuildSection(
        string   title,
        string   countBindingPath,
        out ListView list,
        bool isEventList)
    {
        var grid = new Grid();
        grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });

        // Section header
        var headerPanel = new DockPanel { Margin = new Thickness(4, 4, 4, 2) };

        var titleLabel = new TextBlock
        {
            Text       = title,
            FontWeight = FontWeights.SemiBold,
            FontSize   = 11
        };
        titleLabel.SetResourceReference(ForegroundProperty, "ToolWindowText");
        DockPanel.SetDock(titleLabel, Dock.Left);
        headerPanel.Children.Add(titleLabel);

        var countLabel = new TextBlock
        {
            FontSize  = 10,
            Opacity   = 0.6,
            Margin    = new Thickness(8, 0, 0, 0),
            VerticalAlignment = VerticalAlignment.Center
        };
        countLabel.SetResourceReference(ForegroundProperty, "ToolWindowText");
        countLabel.SetBinding(TextBlock.TextProperty,
            new System.Windows.Data.Binding(countBindingPath));
        headerPanel.Children.Add(countLabel);

        Grid.SetRow(headerPanel, 0);
        grid.Children.Add(headerPanel);

        // List view
        list = new ListView
        {
            BorderThickness = new Thickness(0),
            Margin          = new Thickness(4, 0, 4, 0),
            FontSize        = 11
        };
        list.SetResourceReference(ForegroundProperty, "ToolWindowText");
        list.SetResourceReference(BackgroundProperty, "ToolWindowBackground");

        if (!isEventList)
        {
            // Named elements: type + name + line columns
            var gv = new GridView();
            gv.Columns.Add(new GridViewColumn
            {
                Header           = "Type",
                DisplayMemberBinding = new System.Windows.Data.Binding(nameof(CodeGenNamedElementItem.WpfTypeName)),
                Width            = 80
            });
            gv.Columns.Add(new GridViewColumn
            {
                Header           = "Field Name",
                DisplayMemberBinding = new System.Windows.Data.Binding(nameof(CodeGenNamedElementItem.Name)),
                Width            = 120
            });
            gv.Columns.Add(new GridViewColumn
            {
                Header           = "Line",
                DisplayMemberBinding = new System.Windows.Data.Binding(nameof(CodeGenNamedElementItem.LineLabel)),
                Width            = 55
            });
            list.View = gv;
        }
        else
        {
            // Event sinks: single label column
            var gv = new GridView();
            gv.Columns.Add(new GridViewColumn
            {
                Header           = "Sink",
                DisplayMemberBinding = new System.Windows.Data.Binding(nameof(CodeGenEventSinkItem.Label)),
                Width            = 260
            });
            list.View = gv;
        }

        Grid.SetRow(list, 1);
        grid.Children.Add(list);
        return grid;
    }

    private Button MakeButton(string label, string commandPath)
    {
        var btn = new Button
        {
            Content = label,
            Padding = new Thickness(6, 2, 6, 2),
            Margin  = new Thickness(0, 0, 4, 0),
            FontSize = 11
        };
        btn.SetBinding(Button.CommandProperty,
            new System.Windows.Data.Binding(commandPath));
        return btn;
    }

    // ── Inner converter ───────────────────────────────────────────────────────

    private sealed class BoolToVisibilityConverter : System.Windows.Data.IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
            => value is true ? Visibility.Visible : Visibility.Collapsed;

        public object ConvertBack(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
            => throw new NotSupportedException();
    }
}
