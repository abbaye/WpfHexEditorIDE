// ==========================================================
// Project: WpfHexEditor.Editor.CodeEditor
// File: Controls/GoToSymbolPopup.cs
// Description:
//     VS-style Ctrl+T "Go to Symbol" palette overlay.
//     Queries ILspClient.WorkspaceSymbolsAsync with 300ms debounce
//     and displays results with MDL2 glyphs, symbol name, container, and file.
//
// Architecture Notes:
//     Self-contained WPF Popup (600×420); no XAML file.
//     PlacementMode.Center on the host CodeEditor.
//     NavigationRequested event carries (filePath, line, column) to the host.
//     Theme tokens: CE_* brushes resolved from Application resources.
// ==========================================================

using System;
using System.Collections.Generic;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using WpfHexEditor.Editor.Core.LSP;

namespace WpfHexEditor.Editor.CodeEditor.Controls;

/// <summary>
/// Floating symbol search palette triggered by Ctrl+T.
/// </summary>
public sealed class GoToSymbolPopup : Popup
{
    // ── Events ─────────────────────────────────────────────────────────────────
    public event EventHandler<GoToSymbolNavigationArgs>? NavigationRequested;

    // ── State ──────────────────────────────────────────────────────────────────
    private ILspClient?               _lspClient;
    private readonly TextBox          _searchBox;
    private readonly ListBox          _resultList;
    private readonly DispatcherTimer  _debounce;
    private CancellationTokenSource?  _cts;

    // ── Constructor ────────────────────────────────────────────────────────────
    public GoToSymbolPopup()
    {
        StaysOpen          = false;
        AllowsTransparency = true;
        Placement          = PlacementMode.Center;

        _debounce = new DispatcherTimer(DispatcherPriority.Input)
        {
            Interval = TimeSpan.FromMilliseconds(300)
        };
        _debounce.Tick += OnDebounce;

        Child = BuildUI(out _searchBox, out _resultList);
    }

    // ── Public API ─────────────────────────────────────────────────────────────

    public void SetLspClient(ILspClient? client) => _lspClient = client;

    public void Show(FrameworkElement placementTarget)
    {
        PlacementTarget = placementTarget;
        IsOpen          = true;
        _searchBox.Text = string.Empty;
        _resultList.Items.Clear();
        Dispatcher.BeginInvoke(DispatcherPriority.Input, _searchBox.Focus);
        // Trigger empty query immediately to show recent/all symbols
        ScheduleSearch();
    }

    // ── Build UI ───────────────────────────────────────────────────────────────

    private FrameworkElement BuildUI(out TextBox searchBox, out ListBox resultList)
    {
        var bg     = TryFindTokenBrush("DockBackgroundBrush")     ?? new SolidColorBrush(Color.FromRgb(30, 30, 30));
        var border = TryFindTokenBrush("DockBorderBrush")         ?? new SolidColorBrush(Color.FromRgb(60, 60, 60));
        var fg     = TryFindTokenBrush("DockMenuForegroundBrush") ?? Brushes.WhiteSmoke;

        var root = new Border
        {
            Width           = 600,
            Height          = 420,
            Background      = bg,
            BorderBrush     = border,
            BorderThickness = new Thickness(1),
            CornerRadius    = new CornerRadius(4),
            Effect          = new System.Windows.Media.Effects.DropShadowEffect
            {
                BlurRadius  = 12,
                ShadowDepth = 4,
                Opacity     = 0.5,
            },
        };

        var grid = new Grid();
        grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

        // ── Search box ──
        searchBox = new TextBox
        {
            Margin          = new Thickness(8, 8, 8, 4),
            Padding         = new Thickness(6, 4, 6, 4),
            FontSize        = 13,
            Background      = new SolidColorBrush(Color.FromRgb(50, 50, 50)),
            Foreground      = fg,
            CaretBrush      = fg,
            BorderThickness = new Thickness(1),
            BorderBrush     = border,
        };
        searchBox.TextChanged += (_, _) => ScheduleSearch();
        searchBox.PreviewKeyDown += OnSearchKeyDown;
        Grid.SetRow(searchBox, 0);

        // ── Result list ──
        resultList = new ListBox
        {
            Margin          = new Thickness(4, 0, 4, 4),
            Background      = Brushes.Transparent,
            BorderThickness = new Thickness(0),
            Foreground      = fg,
        };
        ScrollViewer.SetHorizontalScrollBarVisibility(resultList, ScrollBarVisibility.Disabled);
        resultList.PreviewKeyDown += OnListKeyDown;
        resultList.MouseDoubleClick += OnItemActivated;
        resultList.ItemTemplate = BuildItemTemplate(fg);
        Grid.SetRow(resultList, 1);

        // ── Status bar ──
        var status = new TextBlock
        {
            Margin    = new Thickness(12, 2, 12, 6),
            FontSize  = 10,
            Opacity   = 0.55,
            Foreground = fg,
            Text      = "↑↓ navigate   Enter confirm   Esc close",
        };
        Grid.SetRow(status, 2);

        grid.Children.Add(searchBox);
        grid.Children.Add(resultList);
        grid.Children.Add(status);
        root.Child = grid;
        return root;
    }

    private static DataTemplate BuildItemTemplate(Brush fg)
    {
        var template = new DataTemplate(typeof(GoToSymbolItem));

        // Root: horizontal StackPanel
        var root = new FrameworkElementFactory(typeof(StackPanel));
        root.SetValue(StackPanel.OrientationProperty, Orientation.Horizontal);
        root.SetValue(FrameworkElement.MarginProperty, new Thickness(4, 2, 4, 2));

        // Glyph
        var glyph = new FrameworkElementFactory(typeof(TextBlock));
        glyph.SetValue(TextBlock.FontFamilyProperty, new FontFamily("Segoe MDL2 Assets"));
        glyph.SetValue(TextBlock.FontSizeProperty, 12.0);
        glyph.SetValue(TextBlock.MarginProperty, new Thickness(0, 0, 6, 0));
        glyph.SetValue(TextBlock.VerticalAlignmentProperty, VerticalAlignment.Center);
        glyph.SetBinding(TextBlock.TextProperty, new System.Windows.Data.Binding("Glyph"));
        glyph.SetBinding(TextBlock.ForegroundProperty, new System.Windows.Data.Binding("GlyphBrush"));

        // Name
        var name = new FrameworkElementFactory(typeof(TextBlock));
        name.SetValue(TextBlock.FontWeightProperty, FontWeights.SemiBold);
        name.SetValue(TextBlock.VerticalAlignmentProperty, VerticalAlignment.Center);
        name.SetBinding(TextBlock.TextProperty, new System.Windows.Data.Binding("Name"));

        // Container
        var container = new FrameworkElementFactory(typeof(TextBlock));
        container.SetValue(TextBlock.MarginProperty, new Thickness(6, 0, 0, 0));
        container.SetValue(TextBlock.OpacityProperty, 0.55);
        container.SetValue(TextBlock.VerticalAlignmentProperty, VerticalAlignment.Center);
        container.SetBinding(TextBlock.TextProperty, new System.Windows.Data.Binding("ContainerText"));

        // File path
        var file = new FrameworkElementFactory(typeof(TextBlock));
        file.SetValue(TextBlock.OpacityProperty, 0.4);
        file.SetValue(TextBlock.FontSizeProperty, 10.0);
        file.SetValue(TextBlock.MarginProperty, new Thickness(8, 0, 0, 0));
        file.SetValue(TextBlock.VerticalAlignmentProperty, VerticalAlignment.Center);
        file.SetBinding(TextBlock.TextProperty, new System.Windows.Data.Binding("ShortFile"));

        root.AppendChild(glyph);
        root.AppendChild(name);
        root.AppendChild(container);
        root.AppendChild(file);

        template.VisualTree = root;
        return template;
    }

    // ── Search logic ───────────────────────────────────────────────────────────

    private void ScheduleSearch()
    {
        _debounce.Stop();
        _debounce.Start();
    }

    private async void OnDebounce(object? sender, EventArgs e)
    {
        _debounce.Stop();

        if (_lspClient?.IsInitialized != true)
        {
            _resultList.Items.Clear();
            return;
        }

        _cts?.Cancel();
        _cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));

        var query = _searchBox.Text ?? string.Empty;

        try
        {
            var symbols = await _lspClient.WorkspaceSymbolsAsync(query, _cts.Token)
                                           .ConfigureAwait(true);

            _resultList.Items.Clear();
            foreach (var sym in symbols)
                _resultList.Items.Add(new GoToSymbolItem(sym));

            if (_resultList.Items.Count > 0)
                _resultList.SelectedIndex = 0;
        }
        catch (OperationCanceledException) { }
        catch { _resultList.Items.Clear(); }
    }

    // ── Keyboard navigation ────────────────────────────────────────────────────

    private void OnSearchKeyDown(object sender, KeyEventArgs e)
    {
        switch (e.Key)
        {
            case Key.Down:
                if (_resultList.Items.Count > 0)
                {
                    _resultList.SelectedIndex = Math.Min(
                        _resultList.SelectedIndex + 1, _resultList.Items.Count - 1);
                    (_resultList.ItemContainerGenerator.ContainerFromIndex(
                        _resultList.SelectedIndex) as ListBoxItem)?.BringIntoView();
                }
                e.Handled = true;
                break;

            case Key.Up:
                if (_resultList.Items.Count > 0)
                {
                    _resultList.SelectedIndex = Math.Max(_resultList.SelectedIndex - 1, 0);
                    (_resultList.ItemContainerGenerator.ContainerFromIndex(
                        _resultList.SelectedIndex) as ListBoxItem)?.BringIntoView();
                }
                e.Handled = true;
                break;

            case Key.Enter:
                ActivateSelected();
                e.Handled = true;
                break;

            case Key.Escape:
                IsOpen = false;
                e.Handled = true;
                break;
        }
    }

    private void OnListKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            ActivateSelected();
            e.Handled = true;
        }
        else if (e.Key == Key.Escape)
        {
            IsOpen = false;
            e.Handled = true;
        }
    }

    private void OnItemActivated(object sender, MouseButtonEventArgs e)
        => ActivateSelected();

    private void ActivateSelected()
    {
        if (_resultList.SelectedItem is not GoToSymbolItem item) return;
        IsOpen = false;
        var filePath = item.UriToLocalPath();
        NavigationRequested?.Invoke(this,
            new GoToSymbolNavigationArgs(filePath, item.Symbol.StartLine, item.Symbol.StartColumn));
    }

    private static Brush? TryFindTokenBrush(string key)
        => Application.Current?.TryFindResource(key) as Brush;
}

// ── Helper view-model ──────────────────────────────────────────────────────────

internal sealed class GoToSymbolItem
{
    public LspWorkspaceSymbol Symbol { get; }

    public string Name          => Symbol.Name;
    public string ContainerText => Symbol.ContainerName is { Length: > 0 } c ? $"({c})" : string.Empty;
    public string ShortFile     => System.IO.Path.GetFileName(UriToPath(Symbol.Uri ?? string.Empty));
    public string Glyph         => KindToGlyph(Symbol.Kind ?? string.Empty);
    public Brush  GlyphBrush    => KindToBrush(Symbol.Kind ?? string.Empty);

    public GoToSymbolItem(LspWorkspaceSymbol symbol) => Symbol = symbol;

    public string UriToLocalPath() => UriToPath(Symbol.Uri ?? string.Empty);

    private static string UriToPath(string uri)
    {
        if (uri.StartsWith("file:///", StringComparison.OrdinalIgnoreCase))
        {
            var path = uri[8..].Replace('/', System.IO.Path.DirectorySeparatorChar);
            return Uri.UnescapeDataString(path);
        }
        return uri;
    }

    private static string KindToGlyph(string kind) => kind.ToLowerInvariant() switch
    {
        "method" or "function" or "constructor" => "\uE8A5",
        "enum" or "enummember"                  => "\uE8D7",
        "struct"                                => "\uE8D7",
        "interface"                             => "\uE8D4",
        "namespace" or "module"                 => "\uE8A1",
        "class" or "object"                     => "\uE8B1",
        "field" or "variable" or "constant"     => "\uE8E3",
        _                                       => "\uE8A5",
    };

    private static Brush KindToBrush(string kind) => kind.ToLowerInvariant() switch
    {
        "method" or "function" or "constructor"
            => new SolidColorBrush(Color.FromRgb(220, 220, 170)),
        "class" or "struct" or "object"
            => new SolidColorBrush(Color.FromRgb(78, 201, 176)),
        "interface"
            => new SolidColorBrush(Color.FromRgb(184, 215, 163)),
        "enum" or "enummember"
            => new SolidColorBrush(Color.FromRgb(180, 180, 220)),
        _ => new SolidColorBrush(Color.FromRgb(156, 220, 254)),
    };
}

/// <summary>Event args for <see cref="GoToSymbolPopup.NavigationRequested"/>.</summary>
public sealed class GoToSymbolNavigationArgs : EventArgs
{
    public string? FilePath { get; }
    public int     Line     { get; }
    public int     Column   { get; }

    public GoToSymbolNavigationArgs(string? filePath, int line, int column)
    {
        FilePath = filePath;
        Line     = line;
        Column   = column;
    }
}
