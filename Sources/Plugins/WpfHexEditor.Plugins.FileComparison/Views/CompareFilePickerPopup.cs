// ==========================================================
// Project: WpfHexEditor.Plugins.FileComparison
// File: Views/CompareFilePickerPopup.cs
// Description:
//     VS Code-style floating file picker for "Compare with Another File…".
//     Shows Open Documents + Solution Files (WH and VS .sln) filtered by a
//     live search box.  First quick-access button is "Browse file system…"
//     which falls back to the native OpenFileDialog.
//
// Architecture Notes:
//     Mirrors WpfHexEditor.App/Dialogs/CompareFilePickerWindow (App layer),
//     which cannot be referenced from the plugin ALC.
//     Returns Task<string?> via TaskCompletionSource (promise pattern).
//     Uses DF_* + DockForegroundBrush tokens (all present in Colors.xaml).
//     Opens anchored to the cursor position via GetCursorPos P/Invoke.
// ==========================================================

using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Markup;
using System.Windows.Media;
using System.Windows.Media.Effects;

namespace WpfHexEditor.Plugins.FileComparison.Views;

/// <summary>
/// Floating overlay for picking the second file in a "Compare with Another File…" flow.
/// Populated from Solution Explorer file lists; supports live search and a "Browse" fallback.
/// </summary>
internal sealed class CompareFilePickerPopup : Window
{
    // ── Controls ──────────────────────────────────────────────────────────────
    private readonly TextBox  _searchBox;
    private readonly ListBox  _resultsList;

    // ── State ─────────────────────────────────────────────────────────────────
    private readonly TaskCompletionSource<string?> _tcs = new();
    private readonly IReadOnlyList<string>         _solutionFiles;
    private readonly IReadOnlyList<string>         _openFilePaths;
    private readonly string?                       _solutionBasePath;
    private          bool                          _committed;
    private          bool                          _hasBeenActivated;

    // ── Win32 cursor position ─────────────────────────────────────────────────
    [DllImport("user32.dll")]
    private static extern bool GetCursorPos(out Win32Point pt);

    [StructLayout(LayoutKind.Sequential)]
    private struct Win32Point { public int X; public int Y; }

    // ── Public API ────────────────────────────────────────────────────────────

    /// <summary>
    /// Shows the picker anchored near the cursor and returns the selected path,
    /// or <c>null</c> if cancelled.
    /// </summary>
    public static Task<string?> ShowAsync(
        Window                owner,
        string                leftFilePath,
        IReadOnlyList<string> solutionFiles,
        IReadOnlyList<string> openFilePaths,
        string?               solutionBasePath = null)
    {
        var popup = new CompareFilePickerPopup(owner, leftFilePath, solutionFiles, openFilePaths, solutionBasePath);
        popup.Show();
        return popup._tcs.Task;
    }

    // ── Constructor ───────────────────────────────────────────────────────────

    private CompareFilePickerPopup(
        Window                owner,
        string                leftFilePath,
        IReadOnlyList<string> solutionFiles,
        IReadOnlyList<string> openFilePaths,
        string?               solutionBasePath)
    {
        _solutionFiles    = solutionFiles;
        _openFilePaths    = openFilePaths;
        _solutionBasePath = solutionBasePath is not null
            ? Path.GetDirectoryName(solutionBasePath) // strip .whsln/.sln filename → dir
            : null;

        Owner              = owner;
        WindowStyle        = WindowStyle.None;
        AllowsTransparency = true;
        ResizeMode         = ResizeMode.NoResize;
        ShowInTaskbar      = false;
        Background         = Brushes.Transparent;
        Width              = 500;
        SizeToContent      = SizeToContent.Height;
        Topmost            = false;

        Effect = new DropShadowEffect { BlurRadius = 16, ShadowDepth = 4, Opacity = 0.5 };

        // ── Outer border ──────────────────────────────────────────────────────
        var outerBorder = new Border
        {
            CornerRadius    = new CornerRadius(6),
            BorderThickness = new Thickness(1),
            Padding         = new Thickness(0, 0, 0, 6)
        };
        outerBorder.SetResourceReference(Border.BackgroundProperty,  "DF_PickerBackground");
        outerBorder.SetResourceReference(Border.BorderBrushProperty, "DockSplitterBrush");

        var panel = new StackPanel();
        outerBorder.Child = panel;

        // Title
        var title = new TextBlock
        {
            Text       = $"Compare with \u2014 {Path.GetFileName(leftFilePath)}",
            FontSize   = 11,
            FontWeight = FontWeights.SemiBold,
            Margin     = new Thickness(10, 8, 10, 4),
            Opacity    = 0.7
        };
        title.SetResourceReference(ForegroundProperty, "DockMenuForegroundBrush");
        panel.Children.Add(title);

        // Search box
        _searchBox = new TextBox
        {
            Margin                   = new Thickness(8, 0, 8, 4),
            Height                   = 26,
            FontSize                 = 13,
            BorderThickness          = new Thickness(1),
            Padding                  = new Thickness(6, 3, 6, 3),
            VerticalContentAlignment = VerticalAlignment.Center
        };
        _searchBox.SetResourceReference(BackgroundProperty,   "DockTabBackgroundBrush");
        _searchBox.SetResourceReference(ForegroundProperty,   "DockMenuForegroundBrush");
        _searchBox.SetResourceReference(BorderBrushProperty,  "DockSplitterBrush");
        _searchBox.TextChanged += (_, _) => PopulateList(_searchBox.Text);
        panel.Children.Add(_searchBox);

        // Quick-access: Browse button
        var quickRow  = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(8, 2, 8, 4) };
        var btnBrowse = MakeQuickButton("\uE8B7  Browse file system\u2026", null);
        quickRow.Children.Add(btnBrowse);
        panel.Children.Add(quickRow);

        // Separator
        var sep = new Border { Height = 1, Margin = new Thickness(0, 2, 0, 2) };
        sep.SetResourceReference(BackgroundProperty, "DockSplitterBrush");
        panel.Children.Add(sep);

        // Results list — replace the entire ListBoxItem ControlTemplate via XAML parsing so that
        // WPF's default chrome (selection rect, FocusVisualStyle dots, SystemColors.HighlightBrush
        // internal triggers) is fully removed.  The Border.Background TemplateBinding picks up the
        // value set by our Style triggers (hover/selected → DF_PickerHighlightBrush).
        const string itemTemplateXaml =
            "<ControlTemplate " +
            "  xmlns='http://schemas.microsoft.com/winfx/2006/xaml/presentation' " +
            "  xmlns:x='http://schemas.microsoft.com/winfx/2006/xaml' " +
            "  TargetType='{x:Type ListBoxItem}'>" +
            "  <Border Background='{TemplateBinding Background}' " +
            "          BorderThickness='0' CornerRadius='3' " +
            "          Padding='{TemplateBinding Padding}'>" +
            "    <ContentPresenter/>" +
            "  </Border>" +
            "</ControlTemplate>";
        var itemTemplate = (ControlTemplate)System.Windows.Markup.XamlReader.Parse(itemTemplateXaml);

        var itemStyle = new Style(typeof(ListBoxItem));
        // OverridesDefaultStyle=true: ignore app/theme ListBoxItem styles entirely.
        // Without this, the NordTheme global ListBoxItem template (which adds a checkbox
        // indicator) wins over ItemContainerStyle.
        itemStyle.Setters.Add(new Setter(OverridesDefaultStyleProperty, true));
        itemStyle.Setters.Add(new Setter(FocusVisualStyleProperty,      null));
        itemStyle.Setters.Add(new Setter(BackgroundProperty,            Brushes.Transparent));
        itemStyle.Setters.Add(new Setter(BorderThicknessProperty,       new Thickness(0)));
        itemStyle.Setters.Add(new Setter(TemplateProperty,              itemTemplate));
        // Set foreground explicitly so text remains readable regardless of theme tokens.
        itemStyle.Setters.Add(new Setter(ForegroundProperty,
            new DynamicResourceExtension("DockMenuForegroundBrush")));

        // Hover → subtle theme row highlight (same token used by Solution Explorer).
        // Selected → full accent brush, so the two states are visually distinct.
        var hoverTrigger    = new Trigger { Property = IsMouseOverProperty, Value = true };
        hoverTrigger.Setters.Add(new Setter(BackgroundProperty,
            new DynamicResourceExtension("SE_HoverBrush")));

        var selectedTrigger = new Trigger { Property = ListBoxItem.IsSelectedProperty, Value = true };
        selectedTrigger.Setters.Add(new Setter(BackgroundProperty,
            new DynamicResourceExtension("DF_PickerHighlightBrush")));

        itemStyle.Triggers.Add(hoverTrigger);
        itemStyle.Triggers.Add(selectedTrigger);

        _resultsList = new ListBox
        {
            MaxHeight            = 320,
            BorderThickness      = new Thickness(0),
            Margin               = new Thickness(4, 0, 4, 0),
            FontSize             = 12,
            ItemContainerStyle   = itemStyle
        };
        ScrollViewer.SetHorizontalScrollBarVisibility(_resultsList, ScrollBarVisibility.Disabled);
        _resultsList.SetResourceReference(BackgroundProperty, "DF_PickerBackground");
        _resultsList.SetResourceReference(ForegroundProperty, "DockMenuForegroundBrush");
        _resultsList.MouseDoubleClick += (_, _) => CommitSelectedItem();
        _resultsList.KeyDown          += OnListKeyDown;
        panel.Children.Add(_resultsList);

        Content = outerBorder;

        // Populate list as soon as the window is loaded.
        Loaded += (_, _) => PopulateList("");

        // Position AFTER content is rendered so ActualHeight is valid (SizeToContent=Height
        // computes the height lazily — it is 0 during Loaded).
        // Capture cursor now; it won't have moved since the context-menu click.
        GetCursorPos(out var cursorAtCreate);
        ContentRendered += (_, _) =>
        {
            var src    = PresentationSource.FromVisual(this);
            var scaleX = src?.CompositionTarget?.TransformFromDevice.M11 ?? 1.0;
            var scaleY = src?.CompositionTarget?.TransformFromDevice.M22 ?? 1.0;
            var dipX   = cursorAtCreate.X * scaleX;
            var dipY   = cursorAtCreate.Y * scaleY;
            var screen = SystemParameters.WorkArea;
            Left = Math.Min(dipX + 4, screen.Right  - Width        - 8);
            Top  = Math.Min(dipY + 4, screen.Bottom - ActualHeight - 8);
        };

        // _hasBeenActivated guard: Deactivated fires immediately after Show() if another window
        // (e.g. the Solution Explorer context menu) currently has focus — without the guard the
        // picker would close before the user ever sees it.
        Activated   += (_, _) => { _hasBeenActivated = true; _searchBox.Focus(); };
        Deactivated += (_, _) => { if (_hasBeenActivated) Cancel(); };
        KeyDown     += (_, e) => { if (e.Key == Key.Escape) Cancel(); };
    }

    // ── List population ───────────────────────────────────────────────────────

    private void PopulateList(string filter)
    {
        _resultsList.Items.Clear();

        // Build a flat list of (path, group) pairs — open docs first, then solution files.
        var items = new List<(string Path, string Group)>();

        foreach (var path in _openFilePaths)
            if (!string.IsNullOrEmpty(path))
                items.Add((path, "Open Documents"));

        var openSet = new HashSet<string>(
            _openFilePaths.Where(p => !string.IsNullOrEmpty(p)),
            StringComparer.OrdinalIgnoreCase);

        foreach (var path in _solutionFiles)
            if (!string.IsNullOrEmpty(path) && !openSet.Contains(path))
                items.Add((path, "Solution Files"));

        // Apply filter
        if (filter.Length > 0)
            items = items
                .Where(x => x.Path.IndexOf(filter, StringComparison.OrdinalIgnoreCase) >= 0)
                .ToList();

        string? lastGroup = null;
        foreach (var (path, group) in items.Take(50))
        {
            if (group != lastGroup)
            {
                _resultsList.Items.Add(BuildGroupHeader(group));
                lastGroup = group;
            }
            var subtitle = MakeRelativePath(path);
            _resultsList.Items.Add(BuildFileItem(path, subtitle));
        }
    }

    // ── Item builders ─────────────────────────────────────────────────────────

    /// <summary>
    /// Returns a short relative path for the directory containing <paramref name="filePath"/>,
    /// without the filename.  Uses <c>_solutionBasePath</c> as anchor when available;
    /// falls back to just the immediate parent folder name.
    /// </summary>
    private string? MakeRelativePath(string filePath)
    {
        var dir = Path.GetDirectoryName(filePath);
        if (string.IsNullOrEmpty(dir)) return null;

        if (_solutionBasePath is not null)
        {
            try
            {
                var rel = Path.GetRelativePath(_solutionBasePath, dir);
                // Normalise separators to forward-slash for a compact look
                return rel.Replace('\\', '/').TrimEnd('/');
            }
            catch { /* fall through */ }
        }

        // No solution base — show just the parent folder name
        return Path.GetFileName(dir);
    }

    private ListBoxItem BuildGroupHeader(string text)
    {
        var tb = new TextBlock { Text = text, FontSize = 10, FontWeight = FontWeights.Bold, Opacity = 0.6 };
        tb.SetResourceReference(ForegroundProperty, "DockMenuForegroundBrush");
        return new ListBoxItem
        {
            Content         = tb,
            IsEnabled       = false,
            Focusable       = false,
            Padding         = new Thickness(8, 4, 4, 2),
            // Keep group headers visually neutral — override style triggers.
            Background      = Brushes.Transparent,
            BorderThickness = new Thickness(0)
        };
    }

    private ListBoxItem BuildFileItem(string filePath, string? subtitle)
    {
        var grid = new Grid();
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

        var icon = new TextBlock
        {
            Text              = "\uE8A5",
            FontFamily        = new FontFamily("Segoe MDL2 Assets"),
            FontSize          = 12,
            VerticalAlignment = VerticalAlignment.Top,
            Margin            = new Thickness(0, 2, 6, 0),
            Opacity           = 0.7
        };
        icon.SetResourceReference(ForegroundProperty, "DockMenuForegroundBrush");
        Grid.SetColumn(icon, 0);
        grid.Children.Add(icon);

        var namePanel = new StackPanel { Orientation = Orientation.Vertical };

        var nameText = new TextBlock
        {
            Text         = Path.GetFileName(filePath),
            FontSize     = 12,
            TextTrimming = TextTrimming.CharacterEllipsis
        };
        nameText.SetResourceReference(ForegroundProperty, "DockMenuForegroundBrush");
        namePanel.Children.Add(nameText);

        if (!string.IsNullOrEmpty(subtitle))
        {
            var subText = new TextBlock
            {
                Text         = subtitle,
                FontSize     = 10,
                FontStyle    = FontStyles.Italic,
                Opacity      = 0.55,
                TextTrimming = TextTrimming.CharacterEllipsis
            };
            subText.SetResourceReference(ForegroundProperty, "DockMenuForegroundBrush");
            namePanel.Children.Add(subText);
        }

        Grid.SetColumn(namePanel, 1);
        grid.Children.Add(namePanel);

        // Hover/selection highlight is handled by ItemContainerStyle triggers — no per-item
        // MouseEnter/MouseLeave handlers needed.
        return new ListBoxItem
        {
            Content = grid,
            Padding = subtitle is null ? new Thickness(8, 3, 8, 3) : new Thickness(8, 2, 8, 2),
            Tag     = filePath,
            ToolTip = filePath
        };
    }

    private Button MakeQuickButton(string text, string? path)
    {
        const string btnTemplateXaml =
            "<ControlTemplate " +
            "  xmlns='http://schemas.microsoft.com/winfx/2006/xaml/presentation' " +
            "  xmlns:x='http://schemas.microsoft.com/winfx/2006/xaml' " +
            "  TargetType='{x:Type Button}'>" +
            "  <Border Background='{TemplateBinding Background}' " +
            "          BorderBrush='{TemplateBinding BorderBrush}' " +
            "          BorderThickness='{TemplateBinding BorderThickness}' " +
            "          CornerRadius='3' Padding='{TemplateBinding Padding}'>" +
            "    <ContentPresenter HorizontalAlignment='Left' VerticalAlignment='Center'/>" +
            "  </Border>" +
            "</ControlTemplate>";
        var btnTemplate = (ControlTemplate)XamlReader.Parse(btnTemplateXaml);

        var btnStyle = new Style(typeof(Button));
        btnStyle.Setters.Add(new Setter(OverridesDefaultStyleProperty, true));
        btnStyle.Setters.Add(new Setter(FocusVisualStyleProperty,      null));
        btnStyle.Setters.Add(new Setter(TemplateProperty,              btnTemplate));
        var hoverTrigger = new Trigger { Property = IsMouseOverProperty, Value = true };
        hoverTrigger.Setters.Add(new Setter(BackgroundProperty,
            new DynamicResourceExtension("SE_HoverBrush")));
        btnStyle.Triggers.Add(hoverTrigger);

        var btn = new Button
        {
            Content         = text,
            Tag             = path,
            Margin          = new Thickness(0, 0, 6, 0),
            Padding         = new Thickness(6, 2, 6, 2),
            FontSize        = 11,
            BorderThickness = new Thickness(1),
            Cursor          = Cursors.Hand,
            Style           = btnStyle
        };
        btn.SetResourceReference(BackgroundProperty,  "DockTabBackgroundBrush");
        btn.SetResourceReference(ForegroundProperty,  "DockMenuForegroundBrush");
        btn.SetResourceReference(BorderBrushProperty, "DockSplitterBrush");
        btn.Click += (_, _) =>
        {
            if (path is not null)
                Commit(path);
            else
                BrowseForFile();
        };
        return btn;
    }

    // ── Event handlers ────────────────────────────────────────────────────────

    private void OnListKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter) CommitSelectedItem();
    }

    // ── Actions ───────────────────────────────────────────────────────────────

    private void CommitSelectedItem()
    {
        if (_resultsList.SelectedItem is ListBoxItem { Tag: string path })
            Commit(path);
    }

    private void Commit(string path)
    {
        if (_committed) return;
        _committed = true;
        Close();
        _tcs.TrySetResult(path);
    }

    private void Cancel()
    {
        if (_committed) return;
        _committed = true;
        Close();
        _tcs.TrySetResult(null);
    }

    private void BrowseForFile()
    {
        var dlg = new Microsoft.Win32.OpenFileDialog
        {
            Title  = "Select file to compare",
            Filter = "All files (*.*)|*.*"
        };
        if (dlg.ShowDialog(this) == true)
            Commit(dlg.FileName);
    }
}
