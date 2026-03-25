// Project      : WpfHexEditorControl
// File         : Dialogs/CompareFilePickerWindow.cs
// Description  : VS Code-style non-modal file picker for Compare Files.
//                Shows open documents + recent comparisons with live search.
// Architecture : Code-behind only (no XAML). Closes on Deactivated/Esc.
//                Returns Task<string?> via TaskCompletionSource (promise pattern).

using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Effects;
using WpfHexEditor.Editor.Core.Documents;
using WpfHexEditor.Core.Options;

namespace WpfHexEditor.App.Dialogs;

/// <summary>
/// Floating overlay for picking a file to compare.
/// Shows currently open documents and recent comparisons filtered by a search box.
/// Returns a <see cref="Task{T}"/> of <see cref="string"/> (file path) that resolves
/// when the user selects a file or cancels (null).
/// </summary>
public sealed class CompareFilePickerWindow : Window
{
    // ── Child controls ────────────────────────────────────────────────────────
    private readonly TextBox   _searchBox;
    private readonly ListBox   _resultsList;
    private readonly TextBlock _titleText;

    // ── State ─────────────────────────────────────────────────────────────────
    private readonly TaskCompletionSource<string?> _tcs = new();
    private readonly IDocumentManager?             _documentManager;
    private readonly IReadOnlyList<string>         _recentFiles;
    private readonly IReadOnlyList<string>         _solutionFiles;
    private readonly string?                       _activeEditorPath;
    private          bool                          _committed;
    private          bool                          _hasBeenActivated;

    /// <summary>
    /// Opens the file picker and returns the selected path, or <c>null</c> if cancelled.
    /// </summary>
    public static Task<string?> ShowAsync(
        Window                 owner,
        string                 promptTitle,
        IDocumentManager?      documentManager  = null,
        IReadOnlyList<string>? recentFiles      = null,
        string?                activeEditorPath = null,
        IReadOnlyList<string>? solutionFiles    = null)
    {
        var popup = new CompareFilePickerWindow(owner, promptTitle, documentManager,
            recentFiles ?? [], activeEditorPath, solutionFiles ?? []);
        popup.Show();
        return popup._tcs.Task;
    }

    // ── Constructor ──────────────────────────────────────────────────────────

    private CompareFilePickerWindow(
        Window                owner,
        string                promptTitle,
        IDocumentManager?     documentManager,
        IReadOnlyList<string> recentFiles,
        string?               activeEditorPath,
        IReadOnlyList<string> solutionFiles)
    {
        _documentManager  = documentManager;
        _recentFiles      = recentFiles;
        _solutionFiles    = solutionFiles;
        _activeEditorPath = activeEditorPath;

        Owner               = owner;
        WindowStyle         = WindowStyle.None;
        AllowsTransparency  = true;
        ResizeMode          = ResizeMode.NoResize;
        ShowInTaskbar       = false;
        Background          = Brushes.Transparent;
        Width               = 500;
        SizeToContent       = SizeToContent.Height;
        Topmost             = false;

        Effect = new DropShadowEffect { BlurRadius = 16, ShadowDepth = 4, Opacity = 0.5 };

        // ── Layout ──────────────────────────────────────────────────────────
        var outerBorder = new Border
        {
            CornerRadius      = new CornerRadius(6),
            BorderThickness   = new Thickness(1),
            Padding           = new Thickness(0, 0, 0, 6)
        };
        outerBorder.SetResourceReference(Border.BackgroundProperty,  "DF_PickerBackground");
        outerBorder.SetResourceReference(Border.BorderBrushProperty, "DockSplitterBrush");

        var panel = new StackPanel();
        outerBorder.Child = panel;

        // Title
        _titleText = new TextBlock
        {
            Text      = promptTitle,
            FontSize  = 11,
            Margin    = new Thickness(10, 8, 10, 4),
            FontWeight= FontWeights.SemiBold,
            Opacity   = 0.7
        };
        _titleText.SetResourceReference(ForegroundProperty, "DockForegroundBrush");
        panel.Children.Add(_titleText);

        // Search box
        _searchBox = new TextBox
        {
            Margin            = new Thickness(8, 0, 8, 4),
            Height            = 26,
            FontSize          = 13,
            BorderThickness   = new Thickness(1),
            Padding           = new Thickness(6, 3, 6, 3),
            VerticalContentAlignment = VerticalAlignment.Center
        };
        _searchBox.SetResourceReference(BackgroundProperty, "DockTabBackgroundBrush");
        _searchBox.SetResourceReference(ForegroundProperty, "DockForegroundBrush");
        _searchBox.SetResourceReference(BorderBrushProperty, "DockSplitterBrush");
        _searchBox.TextChanged += OnSearchChanged;
        panel.Children.Add(_searchBox);

        // Quick access row (Active Editor + Browse)
        var quickRow = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(8, 2, 8, 4) };
        if (!string.IsNullOrEmpty(activeEditorPath))
        {
            var btnActive = MakeQuickButton($"\uE8A5  {Path.GetFileName(activeEditorPath)}", activeEditorPath);
            quickRow.Children.Add(btnActive);
        }
        var btnBrowse = MakeQuickButton("\uE8B7  Browse…", null);
        quickRow.Children.Add(btnBrowse);
        panel.Children.Add(quickRow);

        // Separator
        var sep = new Border { Height = 1, Margin = new Thickness(0, 2, 0, 2) };
        sep.SetResourceReference(BackgroundProperty, "DockSplitterBrush");
        panel.Children.Add(sep);

        // Results list
        _resultsList = new ListBox
        {
            MaxHeight         = 320,
            BorderThickness   = new Thickness(0),
            Margin            = new Thickness(4, 0, 4, 0),
            FontSize          = 12,
        };
        ScrollViewer.SetHorizontalScrollBarVisibility(_resultsList, ScrollBarVisibility.Disabled);
        _resultsList.SetResourceReference(BackgroundProperty, "DF_PickerBackground");
        _resultsList.SetResourceReference(ForegroundProperty, "DockForegroundBrush");
        _resultsList.MouseDoubleClick += (_, _) => CommitSelectedItem();
        _resultsList.KeyDown += OnListKeyDown;
        panel.Children.Add(_resultsList);

        Content = outerBorder;

        // Position below owner centre
        Loaded += (_, _) =>
        {
            var ownerCentre = owner.Left + owner.ActualWidth / 2;
            Left = ownerCentre - Width / 2;
            Top  = owner.Top   + 80;
            PopulateList("");
        };
        // Guard: only cancel on Deactivated AFTER the window has received focus at least once.
        // Without this, WPF fires Deactivated immediately after Show() if another window holds
        // focus (e.g., the Solution Explorer context menu), closing the picker before it's seen.
        Activated   += (_, _) => { _hasBeenActivated = true; _searchBox.Focus(); };
        Deactivated += (_, _) => { if (_hasBeenActivated) Cancel(); };
        KeyDown     += (_, e) => { if (e.Key == Key.Escape) Cancel(); };
    }

    // ── Population ───────────────────────────────────────────────────────────

    private void PopulateList(string filter)
    {
        _resultsList.Items.Clear();

        var allPaths = new List<(string Path, string Group)>();

        // Open documents
        var openDocs = _documentManager?.OpenDocuments ?? [];
        foreach (var doc in openDocs)
        {
            if (!string.IsNullOrEmpty(doc.FilePath))
                allPaths.Add((doc.FilePath, "Open Documents"));
        }

        // Solution files (deduplicated against open docs)
        var openDocPaths = new HashSet<string>(
            allPaths.Select(x => x.Path), StringComparer.OrdinalIgnoreCase);
        foreach (var path in _solutionFiles)
            if (!string.IsNullOrEmpty(path) && !openDocPaths.Contains(path))
                allPaths.Add((path, "Solution Files"));

        // Recent comparisons
        foreach (var recent in _recentFiles)
            if (!string.IsNullOrEmpty(recent))
                allPaths.Add((recent, "Recent Files"));

        string? lastGroup = null;
        foreach (var (path, group) in allPaths
            .Where(x => string.IsNullOrEmpty(filter) ||
                        x.Path.IndexOf(filter, StringComparison.OrdinalIgnoreCase) >= 0)
            .Take(50))
        {
            if (group != lastGroup)
            {
                _resultsList.Items.Add(BuildGroupHeader(group));
                lastGroup = group;
            }
            var subtitle = group == "Solution Files" ? Path.GetDirectoryName(path) : null;
            _resultsList.Items.Add(BuildFileItem(path, subtitle));
        }
    }

    // ── Item builders ────────────────────────────────────────────────────────

    private ListBoxItem BuildGroupHeader(string title)
    {
        var tb = new TextBlock { Text = title, FontSize = 10, FontWeight = FontWeights.Bold, Opacity = 0.6 };
        tb.SetResourceReference(ForegroundProperty, "DockForegroundBrush");
        return new ListBoxItem
        {
            Content    = tb,
            IsEnabled  = false,
            Padding    = new Thickness(8, 4, 4, 2),
            Focusable  = false
        };
    }

    private ListBoxItem BuildFileItem(string filePath, string? subtitle = null)
    {
        var grid = new Grid();
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

        var icon = new TextBlock { Text = "\uE8A5", FontFamily = new FontFamily("Segoe MDL2 Assets"),
            FontSize = 12, VerticalAlignment = VerticalAlignment.Top, Margin = new Thickness(0, 2, 6, 0), Opacity = 0.7 };
        icon.SetResourceReference(ForegroundProperty, "DockForegroundBrush");
        Grid.SetColumn(icon, 0);
        grid.Children.Add(icon);

        var namePanel = new StackPanel { Orientation = Orientation.Vertical };

        var nameText = new TextBlock
        {
            Text         = Path.GetFileName(filePath),
            FontSize     = 12,
            TextTrimming = TextTrimming.CharacterEllipsis
        };
        nameText.SetResourceReference(ForegroundProperty, "DockForegroundBrush");
        namePanel.Children.Add(nameText);

        if (!string.IsNullOrEmpty(subtitle))
        {
            var subText = new TextBlock
            {
                Text         = subtitle,
                FontSize     = 10,
                Opacity      = 0.5,
                TextTrimming = TextTrimming.CharacterEllipsis
            };
            subText.SetResourceReference(ForegroundProperty, "DockForegroundBrush");
            namePanel.Children.Add(subText);
        }

        Grid.SetColumn(namePanel, 1);
        grid.Children.Add(namePanel);

        var item = new ListBoxItem
        {
            Content = grid,
            Padding = subtitle is null ? new Thickness(8, 3, 8, 3) : new Thickness(8, 2, 8, 2),
            Tag     = filePath,
            ToolTip = filePath
        };

        item.MouseEnter  += (_, _) => item.SetResourceReference(BackgroundProperty, "DF_PickerHighlightBrush");
        item.MouseLeave  += (_, _) => item.Background = Brushes.Transparent;
        return item;
    }

    private Button MakeQuickButton(string text, string? path)
    {
        var btn = new Button
        {
            Content     = text,
            Tag         = path,
            Margin      = new Thickness(0, 0, 6, 0),
            Padding     = new Thickness(6, 2, 6, 2),
            FontSize    = 11,
            BorderThickness = new Thickness(1),
            Cursor      = Cursors.Hand
        };
        btn.SetResourceReference(BackgroundProperty, "DockTabBackgroundBrush");
        btn.SetResourceReference(ForegroundProperty, "DockForegroundBrush");
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

    private void OnSearchChanged(object sender, TextChangedEventArgs e)
        => PopulateList(_searchBox.Text);

    private void OnListKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter) CommitSelectedItem();
    }

    // ── Actions ──────────────────────────────────────────────────────────────

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
        var dlg = new Microsoft.Win32.OpenFileDialog { Title = "Select file to compare" };
        if (dlg.ShowDialog(this) == true)
            Commit(dlg.FileName);
    }
}
