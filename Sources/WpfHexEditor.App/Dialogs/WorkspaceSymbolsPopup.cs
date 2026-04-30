// ==========================================================
// Project: WpfHexEditor.App
// File: Dialogs/WorkspaceSymbolsPopup.cs
// Author: Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.6
// Created: 2026-03-23
// Description:
//     VS-style "Go to Symbol in Workspace" popup (Ctrl+T).
//     Debounced text input → ILspClient.WorkspaceSymbolsAsync → result list.
//     Enter / double-click → open file at the symbol location.
//
// Architecture Notes:
//     Pattern: Non-modal floating Window (mirrors CommandPaletteWindow).
//     Closes on Deactivated or Esc. Re-uses CP_* theme tokens for visual consistency.
//     ILspClient is injected at open time (nullable = graceful degradation).
// ==========================================================

using System.IO;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Effects;
using System.Windows.Threading;
using WpfHexEditor.App.Properties;
using WpfHexEditor.Editor.Core.LSP;

namespace WpfHexEditor.App.Dialogs;

/// <summary>
/// Floating workspace-symbol search popup (Ctrl+T).
/// Calls <see cref="ILspClient.WorkspaceSymbolsAsync"/> on each keystroke (200ms debounce).
/// </summary>
public sealed class WorkspaceSymbolsPopup : Window
{
    // ── Child controls ────────────────────────────────────────────────────────
    private readonly TextBox  _searchBox;
    private readonly ListBox  _resultsList;
    private readonly TextBlock _statusText;

    // ── State ─────────────────────────────────────────────────────────────────
    private readonly ILspClient?                     _lspClient;
    private readonly Action<string, int, int>?       _navigateTo;   // (filePath, line, col)
    private readonly DispatcherTimer                 _debounce;
    private          CancellationTokenSource?        _cts;
    private          bool                            _closingStarted;

    // ── Glyph helpers ─────────────────────────────────────────────────────────
    private static string KindToGlyph(string? kind) => kind?.ToLowerInvariant() switch
    {
        "method" or "function" or "constructor" => "\uE8A7",
        "class"  or "interface" or "struct"     => "\uE8D7",
        "field"  or "variable"                  => "\uE734",
        "property"                              => "\uE90F",
        "keyword"                               => "\uE8C1",
        "enum"   or "enummember"                => "\uE762",
        "module" or "namespace"                 => "\uE8B7",
        _                                       => "\uE8A5",
    };

    // ── Constructor ───────────────────────────────────────────────────────────

    /// <param name="lspClient">Active LSP client. May be null (popup shows "LSP not active").</param>
    /// <param name="navigateTo">Callback that opens a file at a given 0-based line/column.</param>
    public WorkspaceSymbolsPopup(ILspClient? lspClient, Action<string, int, int>? navigateTo)
    {
        _lspClient  = lspClient;
        _navigateTo = navigateTo;

        // Window chrome.
        WindowStyle        = WindowStyle.None;
        AllowsTransparency = true;
        Background         = Brushes.Transparent;
        ResizeMode         = ResizeMode.NoResize;
        ShowInTaskbar      = false;
        Topmost            = true;
        Width              = 600;
        SizeToContent      = SizeToContent.Height;
        MaxHeight          = 460;

        // Root border (same shadow/corner as CommandPaletteWindow).
        var root = new Border
        {
            CornerRadius = new CornerRadius(6),
            Effect       = new DropShadowEffect
            {
                Direction   = 315,
                ShadowDepth = 6,
                BlurRadius  = 18,
                Opacity     = 0.55,
                Color       = Colors.Black,
            },
            BorderThickness = new Thickness(1),
        };
        root.SetResourceReference(Border.BackgroundProperty,  "CP_BackgroundBrush");
        root.SetResourceReference(Border.BorderBrushProperty, "CP_BorderBrush");
        Content = root;

        // Grid: header (auto) + results (star) + status (auto).
        var grid = new Grid();
        grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        root.Child = grid;

        // Header (icon + search box).
        var header = new Border { Padding = new Thickness(10, 8, 10, 8), BorderThickness = new Thickness(0, 0, 0, 1) };
        header.SetResourceReference(Border.BackgroundProperty,  "CP_InputBackgroundBrush");
        header.SetResourceReference(Border.BorderBrushProperty, "CP_BorderBrush");
        Grid.SetRow(header, 0);

        var headerGrid = new Grid();
        headerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        headerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        header.Child = headerGrid;

        var iconLbl = new TextBlock
        {
            Text              = "\uE721",
            FontFamily        = new FontFamily("Segoe MDL2 Assets"),
            FontSize          = 16,
            VerticalAlignment = VerticalAlignment.Center,
            Margin            = new Thickness(0, 0, 8, 0),
        };
        iconLbl.SetResourceReference(TextBlock.ForegroundProperty, "CP_TextBrush");
        Grid.SetColumn(iconLbl, 0);

        _searchBox = new TextBox
        {
            FontSize          = 14,
            BorderThickness   = new Thickness(0),
            VerticalAlignment = VerticalAlignment.Center,
            Background        = Brushes.Transparent,
        };
        _searchBox.SetResourceReference(TextBox.ForegroundProperty, "CP_TextBrush");
        _searchBox.SetResourceReference(TextBox.CaretBrushProperty, "CP_TextBrush");
        Grid.SetColumn(_searchBox, 1);

        headerGrid.Children.Add(iconLbl);
        headerGrid.Children.Add(_searchBox);
        grid.Children.Add(header);

        // Results list.
        _resultsList = new ListBox
        {
            BorderThickness = new Thickness(0),
            MaxHeight       = 360,
        };
        _resultsList.SetResourceReference(ListBox.BackgroundProperty,  "CP_BackgroundBrush");
        _resultsList.SetResourceReference(ListBox.ForegroundProperty,  "CP_TextBrush");

        // Item template: [kind glyph] [name] [file:line] (right aligned)
        var tpl = new DataTemplate();
        var itemGrid  = new FrameworkElementFactory(typeof(Grid));

        var c0 = new FrameworkElementFactory(typeof(ColumnDefinition));
        c0.SetValue(ColumnDefinition.WidthProperty, GridLength.Auto);
        var c1 = new FrameworkElementFactory(typeof(ColumnDefinition));
        c1.SetValue(ColumnDefinition.WidthProperty, new GridLength(1, GridUnitType.Star));
        var c2 = new FrameworkElementFactory(typeof(ColumnDefinition));
        c2.SetValue(ColumnDefinition.WidthProperty, GridLength.Auto);
        itemGrid.AppendChild(c0); itemGrid.AppendChild(c1); itemGrid.AppendChild(c2);

        var kindTb = new FrameworkElementFactory(typeof(TextBlock));
        kindTb.SetBinding(TextBlock.TextProperty, new System.Windows.Data.Binding("KindGlyph"));
        kindTb.SetValue(TextBlock.FontFamilyProperty, new FontFamily("Segoe MDL2 Assets"));
        kindTb.SetValue(TextBlock.FontSizeProperty, 13.0);
        kindTb.SetValue(TextBlock.MarginProperty, new Thickness(4, 0, 8, 0));
        kindTb.SetValue(TextBlock.VerticalAlignmentProperty, VerticalAlignment.Center);
        kindTb.SetValue(Grid.ColumnProperty, 0);
        itemGrid.AppendChild(kindTb);

        var nameTb = new FrameworkElementFactory(typeof(TextBlock));
        nameTb.SetBinding(TextBlock.TextProperty, new System.Windows.Data.Binding("Name"));
        nameTb.SetValue(TextBlock.FontWeightProperty, FontWeights.SemiBold);
        nameTb.SetValue(TextBlock.VerticalAlignmentProperty, VerticalAlignment.Center);
        nameTb.SetValue(TextBlock.TextTrimmingProperty, TextTrimming.CharacterEllipsis);
        nameTb.SetValue(Grid.ColumnProperty, 1);
        itemGrid.AppendChild(nameTb);

        var fileTb = new FrameworkElementFactory(typeof(TextBlock));
        fileTb.SetBinding(TextBlock.TextProperty, new System.Windows.Data.Binding("FileLabel"));
        fileTb.SetValue(TextBlock.FontSizeProperty, 10.0);
        fileTb.SetValue(TextBlock.OpacityProperty, 0.65);
        fileTb.SetValue(TextBlock.MarginProperty, new Thickness(8, 0, 4, 0));
        fileTb.SetValue(TextBlock.VerticalAlignmentProperty, VerticalAlignment.Center);
        fileTb.SetValue(Grid.ColumnProperty, 2);
        itemGrid.AppendChild(fileTb);

        tpl.VisualTree = itemGrid;
        _resultsList.ItemTemplate = tpl;

        // Item container style for CP_HoverBrush.
        var itemStyle = new Style(typeof(ListBoxItem));
        itemStyle.Setters.Add(new Setter(BackgroundProperty, Brushes.Transparent));
        itemStyle.Setters.Add(new Setter(PaddingProperty, new Thickness(6, 4, 6, 4)));
        var hoverTrigger = new Trigger { Property = IsMouseOverProperty, Value = true };
        hoverTrigger.Setters.Add(new Setter(BackgroundProperty,
            Application.Current.TryFindResource("CP_HoverBrush") ?? Brushes.DimGray));
        var selTrigger = new Trigger { Property = ListBoxItem.IsSelectedProperty, Value = true };
        selTrigger.Setters.Add(new Setter(BackgroundProperty,
            Application.Current.TryFindResource("CP_HighlightBrush") ?? Brushes.CornflowerBlue));
        itemStyle.Triggers.Add(hoverTrigger);
        itemStyle.Triggers.Add(selTrigger);
        _resultsList.ItemContainerStyle = itemStyle;

        Grid.SetRow(_resultsList, 1);
        grid.Children.Add(_resultsList);

        // Status bar.
        _statusText = new TextBlock
        {
            Padding = new Thickness(10, 4, 10, 6),
            FontSize = 11,
            Text    = _lspClient?.IsInitialized == true ? AppResources.App_Options_LspTypeToSearch : AppResources.App_Options_LspNotActive,
        };
        _statusText.SetResourceReference(TextBlock.ForegroundProperty, "CP_SubTextBrush");
        Grid.SetRow(_statusText, 2);
        grid.Children.Add(_statusText);

        // Debounce timer.
        _debounce = new DispatcherTimer(DispatcherPriority.Input) { Interval = TimeSpan.FromMilliseconds(200) };
        _debounce.Tick += OnDebounce;

        // Events.
        _searchBox.TextChanged    += OnSearchChanged;
        _resultsList.MouseDoubleClick += (_, _) => CommitSelection();
        PreviewKeyDown            += OnKeyDown;
        Deactivated               += (_, _) => SafeClose();
        Loaded                    += (_, _) => _searchBox.Focus();
    }

    // ── Keyboard handling ─────────────────────────────────────────────────────

    private void OnKeyDown(object sender, KeyEventArgs e)
    {
        switch (e.Key)
        {
            case Key.Escape:
                SafeClose();
                e.Handled = true;
                break;
            case Key.Enter:
                CommitSelection();
                e.Handled = true;
                break;
            case Key.Down:
                if (_resultsList.SelectedIndex < _resultsList.Items.Count - 1)
                    _resultsList.SelectedIndex++;
                _resultsList.ScrollIntoView(_resultsList.SelectedItem);
                e.Handled = true;
                break;
            case Key.Up:
                if (_resultsList.SelectedIndex > 0)
                    _resultsList.SelectedIndex--;
                _resultsList.ScrollIntoView(_resultsList.SelectedItem);
                e.Handled = true;
                break;
        }
    }

    // ── Search ────────────────────────────────────────────────────────────────

    private void OnSearchChanged(object sender, TextChangedEventArgs e)
    {
        _debounce.Stop();
        _debounce.Start();
    }

    private async void OnDebounce(object? sender, EventArgs e)
    {
        _debounce.Stop();
        var query = _searchBox.Text.Trim();

        if (_lspClient?.IsInitialized != true)
        {
            _statusText.Text = AppResources.App_Options_LspNotActive;
            return;
        }

        if (query.Length < 1)
        {
            _resultsList.ItemsSource = null;
            _statusText.Text = AppResources.App_Options_LspTypeToSearch;
            return;
        }

        _statusText.Text = AppResources.App_Options_LspSearching;
        _cts?.Cancel();
        _cts = new CancellationTokenSource(TimeSpan.FromSeconds(3));

        try
        {
            var symbols = await _lspClient.WorkspaceSymbolsAsync(query, _cts.Token)
                .ConfigureAwait(true);

            var items = symbols.Select(s => new SymbolItem(s)).ToList();
            _resultsList.ItemsSource = items;
            _statusText.Text = items.Count == 0 ? "No symbols found" : $"{items.Count} symbol(s)";
            if (items.Count > 0) _resultsList.SelectedIndex = 0;
        }
        catch (OperationCanceledException) { }
        catch (Exception ex)
        {
            _statusText.Text = $"Error: {ex.Message}";
        }
    }

    // ── Commit ────────────────────────────────────────────────────────────────

    private void CommitSelection()
    {
        if (_resultsList.SelectedItem is not SymbolItem item) return;
        _navigateTo?.Invoke(item.Symbol.Uri, item.Symbol.StartLine, item.Symbol.StartColumn);
        SafeClose();
    }

    private void SafeClose()
    {
        if (_closingStarted) return;
        _closingStarted = true;
        _cts?.Cancel();
        _debounce.Stop();
        Close();
    }

    // ── Display model ─────────────────────────────────────────────────────────

    private sealed class SymbolItem
    {
        public LspWorkspaceSymbol Symbol { get; }
        public string KindGlyph  => KindToGlyph(Symbol.Kind);
        public string Name       => string.IsNullOrEmpty(Symbol.ContainerName)
            ? Symbol.Name
            : $"{Symbol.ContainerName}.{Symbol.Name}";
        public string FileLabel  => $"{Path.GetFileName(Symbol.Uri)}:{Symbol.StartLine + 1}";

        public SymbolItem(LspWorkspaceSymbol s) => Symbol = s;
    }
}
