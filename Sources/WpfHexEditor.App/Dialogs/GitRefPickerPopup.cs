// Project      : WpfHexEditorControl
// File         : Dialogs/GitRefPickerPopup.cs
// Description  : Non-modal floating window for picking a git branch or recent commit.
//                Loads refs asynchronously via GitDiffService; shows a spinner while
//                loading.  Returns Task<string?> via TaskCompletionSource (same pattern
//                as CompareFilePickerWindow).
// Architecture : Code-behind only (no XAML). Closes on Deactivated/Esc.

using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Effects;
using WpfHexEditor.Core.Diff.Services;

namespace WpfHexEditor.App.Dialogs;

/// <summary>
/// Async floating picker for git branches / recent commits.
/// The caller supplies the repo root and the display mode
/// (<see cref="GitRefMode.Branches"/> or <see cref="GitRefMode.Commits"/>).
/// </summary>
public sealed class GitRefPickerPopup : Window
{
    public enum GitRefMode { Branches, Commits }

    // ── Child controls ────────────────────────────────────────────────────────
    private readonly TextBlock _titleText;
    private readonly TextBox   _searchBox;
    private readonly ListBox   _resultsList;
    private readonly TextBlock _spinnerText;

    // ── State ─────────────────────────────────────────────────────────────────
    private readonly TaskCompletionSource<string?> _tcs = new();
    private readonly string         _repoRoot;
    private readonly GitRefMode     _mode;
    private readonly GitDiffService _git = new();
    private          bool           _committed;
    private          List<string>   _allRefs = [];

    // ── Factory ───────────────────────────────────────────────────────────────

    /// <summary>Shows the picker and returns the selected ref, or <c>null</c> if cancelled.</summary>
    public static Task<string?> ShowAsync(
        Window      owner,
        string      repoRoot,
        GitRefMode  mode     = GitRefMode.Branches)
    {
        var popup = new GitRefPickerPopup(owner, repoRoot, mode);
        popup.Show();
        return popup._tcs.Task;
    }

    // ── Constructor ───────────────────────────────────────────────────────────

    private GitRefPickerPopup(Window owner, string repoRoot, GitRefMode mode)
    {
        _repoRoot = repoRoot;
        _mode     = mode;

        Owner              = owner;
        WindowStyle        = WindowStyle.None;
        AllowsTransparency = true;
        ResizeMode         = ResizeMode.NoResize;
        ShowInTaskbar      = false;
        Background         = Brushes.Transparent;
        Width              = 480;
        SizeToContent      = SizeToContent.Height;
        Topmost            = false;

        Effect = new DropShadowEffect { BlurRadius = 16, ShadowDepth = 4, Opacity = 0.5 };

        // ── Layout ────────────────────────────────────────────────────────────
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

        // Title bar
        var titleLabel = mode == GitRefMode.Branches ? "Select Branch" : "Select Commit";
        _titleText = new TextBlock
        {
            Text      = titleLabel,
            FontSize  = 11,
            Margin    = new Thickness(10, 8, 10, 4),
            FontWeight = FontWeights.SemiBold,
            Opacity   = 0.7
        };
        _titleText.SetResourceReference(ForegroundProperty, "DockForegroundBrush");
        panel.Children.Add(_titleText);

        // Spinner (visible while loading)
        _spinnerText = new TextBlock
        {
            Text            = "\uE768  Loading…",
            FontSize        = 12,
            Margin          = new Thickness(10, 4, 10, 4),
            Opacity         = 0.6,
            FontFamily      = new FontFamily("Segoe MDL2 Assets, Segoe UI"),
            Visibility      = Visibility.Visible
        };
        _spinnerText.SetResourceReference(ForegroundProperty, "DockForegroundBrush");
        panel.Children.Add(_spinnerText);

        // Search box
        _searchBox = new TextBox
        {
            Margin            = new Thickness(8, 4, 8, 4),
            Height            = 26,
            FontSize          = 13,
            BorderThickness   = new Thickness(1),
            Padding           = new Thickness(6, 3, 6, 3),
            VerticalContentAlignment = VerticalAlignment.Center,
            Visibility        = Visibility.Collapsed   // hidden until refs are loaded
        };
        _searchBox.SetResourceReference(BackgroundProperty,   "DockTabBackgroundBrush");
        _searchBox.SetResourceReference(ForegroundProperty,   "DockForegroundBrush");
        _searchBox.SetResourceReference(BorderBrushProperty,  "DockSplitterBrush");
        _searchBox.TextChanged += (_, _) => FilterList(_searchBox.Text);
        panel.Children.Add(_searchBox);

        // Separator
        var sep = new Border { Height = 1, Margin = new Thickness(0, 2, 0, 2) };
        sep.SetResourceReference(BackgroundProperty, "DockSplitterBrush");
        panel.Children.Add(sep);

        // Results list
        _resultsList = new ListBox
        {
            MaxHeight         = 300,
            BorderThickness   = new Thickness(0),
            Margin            = new Thickness(4, 0, 4, 0),
            FontSize          = 12,
            Visibility        = Visibility.Collapsed,
        };
        ScrollViewer.SetHorizontalScrollBarVisibility(_resultsList, ScrollBarVisibility.Disabled);
        _resultsList.SetResourceReference(BackgroundProperty,  "DF_PickerBackground");
        _resultsList.SetResourceReference(ForegroundProperty,  "DockForegroundBrush");
        _resultsList.MouseDoubleClick += (_, _) => CommitSelectedItem();
        _resultsList.KeyDown += OnListKeyDown;
        panel.Children.Add(_resultsList);

        Content = outerBorder;

        // Position + auto-load
        Loaded += async (_, _) =>
        {
            var ownerCentre = owner.Left + owner.ActualWidth / 2;
            Left = ownerCentre - Width / 2;
            Top  = owner.Top + 80;
            await LoadRefsAsync();
        };

        Deactivated += (_, _) => Cancel();
        KeyDown     += (_, e) => { if (e.Key == Key.Escape) Cancel(); };
    }

    // ── Async loading ─────────────────────────────────────────────────────────

    private async Task LoadRefsAsync()
    {
        try
        {
            if (_mode == GitRefMode.Branches)
            {
                _allRefs = [.. await _git.GetBranchesAsync(_repoRoot)];
            }
            else
            {
                var commits = await _git.GetRecentCommitsAsync(_repoRoot, count: 30);
                _allRefs = commits.Select(c => $"{c.ShortHash}  {c.Message}  ({c.Author})").ToList();
            }
        }
        catch
        {
            _allRefs = [];
        }

        // Hide spinner, show controls
        _spinnerText.Visibility  = Visibility.Collapsed;
        _searchBox.Visibility    = Visibility.Visible;
        _resultsList.Visibility  = Visibility.Visible;

        FilterList(string.Empty);
        _searchBox.Focus();
    }

    // ── Filtering ─────────────────────────────────────────────────────────────

    private void FilterList(string filter)
    {
        _resultsList.Items.Clear();
        foreach (var r in _allRefs
            .Where(x => string.IsNullOrEmpty(filter) ||
                        x.IndexOf(filter, StringComparison.OrdinalIgnoreCase) >= 0)
            .Take(40))
        {
            var item = BuildRefItem(r);
            _resultsList.Items.Add(item);
        }
    }

    // ── Item builder ──────────────────────────────────────────────────────────

    private ListBoxItem BuildRefItem(string label)
    {
        var tb = new TextBlock
        {
            Text         = label,
            FontSize     = 12,
            TextTrimming = TextTrimming.CharacterEllipsis
        };
        tb.SetResourceReference(ForegroundProperty, "DockForegroundBrush");

        var item = new ListBoxItem
        {
            Content = tb,
            Padding = new Thickness(8, 3, 8, 3),
            Tag     = _mode == GitRefMode.Branches
                        ? label                       // branch name is the full tag
                        : label.Split(' ')[0]         // extract short hash
        };
        item.MouseEnter += (_, _) => item.SetResourceReference(BackgroundProperty, "DF_PickerHighlightBrush");
        item.MouseLeave += (_, _) => item.Background = Brushes.Transparent;
        return item;
    }

    // ── Event handlers ────────────────────────────────────────────────────────

    private void OnListKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter) CommitSelectedItem();
    }

    // ── Actions ───────────────────────────────────────────────────────────────

    private void CommitSelectedItem()
    {
        if (_resultsList.SelectedItem is ListBoxItem { Tag: string refName })
            Commit(refName);
    }

    private void Commit(string refName)
    {
        if (_committed) return;
        _committed = true;
        Close();
        _tcs.TrySetResult(refName);
    }

    private void Cancel()
    {
        if (_committed) return;
        _committed = true;
        Close();
        _tcs.TrySetResult(null);
    }
}
