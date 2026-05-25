// ==========================================================
// Project: WpfHexEditor.App
// File: Controls/WelcomePanel.xaml.cs
// Author: Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.6
// Created: 2026-03-06
// Description:
//     Redesigned (#138) VS Start Page-style welcome document.
//     Adds: quick-start template cards, enriched recent file rows
//     (icon + dir + date + pin/remove), search filtering, categorised
//     news feed with filter pills, WelcomeNewsService integration.
//
// Architecture Notes:
//     - Callbacks injected via Configure() for loose coupling with MainWindow
//     - MruService pin/remove exposed via _pinFile / _removeFile delegates
//     - News fetched via WelcomeNewsService (JSON→changelog fallback, 30-min cache)
//     - All dynamic UI built in code-behind (no ItemsControl binding overhead)
// ==========================================================

using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Documents;
using System.Windows.Media;
using System.Windows.Navigation;
using WpfHexEditor.App.Controls.Welcome;
using WpfHexEditor.App.Properties;
using WpfHexEditor.App.Services;

namespace WpfHexEditor.App.Controls;

/// <summary>
/// Redesigned (#138) VS Start Page-style welcome panel.
/// Populate via <see cref="Configure"/> before adding to the visual tree.
/// </summary>
public partial class WelcomePanel : UserControl
{
    // -- Injected callbacks ------------------------------------------------

    private Action?          _onNewFile;
    private Action?          _onOpenFile;
    private Action?          _onOpenProject;
    private Action?          _onOptions;
    private Action<string>?  _openRecentFile;
    private Action<string>?  _openRecentSolution;
    private Action<string>?  _pinFile;
    private Action<string>?  _unpinFile;
    private Action<string>?  _removeFile;
    private Action<string>?  _pinSolution;
    private Action<string>?  _unpinSolution;
    private Action<string>?  _removeSolution;

    // -- Data ---------------------------------------------------------------

    private List<WelcomeRecentFileItem> _allRecentFiles     = [];
    private List<WelcomeRecentFileItem> _allRecentSolutions = [];
    private List<WelcomeTemplateItem>   _templates          = [];
    private List<WelcomeNewsItem>       _allNewsItems       = [];
    private string _activeNewsFilter = "All";

    // -- News service -------------------------------------------------------

    private static readonly WelcomeNewsService _newsService = new();

    // -- Theme brushes for news badges (static, theme-independent) ---------

    private static readonly Brush BrushFeature  = new SolidColorBrush(Color.FromRgb(0x3A, 0x86, 0xFF));
    private static readonly Brush BrushFix      = new SolidColorBrush(Color.FromRgb(0xFF, 0x59, 0x5E));
    private static readonly Brush BrushPerf     = new SolidColorBrush(Color.FromRgb(0xFF, 0xCA, 0x3A));
    private static readonly Brush BrushBreaking = new SolidColorBrush(Color.FromRgb(0xFF, 0x00, 0x6E));

    // Changelog section colours (unchanged from original)
    private static readonly Brush BrushAdded   = new SolidColorBrush(Color.FromArgb(0xFF, 0x4E, 0xC9, 0x6E));
    private static readonly Brush BrushChanged = new SolidColorBrush(Color.FromArgb(0xFF, 0x56, 0x9C, 0xD6));
    private static readonly Brush BrushFixed   = new SolidColorBrush(Color.FromArgb(0xFF, 0xCE, 0x91, 0x78));
    private static readonly Brush BrushRemoved = new SolidColorBrush(Color.FromArgb(0xFF, 0xF4, 0x47, 0x47));
    private static readonly Brush BrushWhatNext = new SolidColorBrush(Color.FromArgb(0xFF, 0xC5, 0x86, 0xC0));

    // -- Constructor -------------------------------------------------------

    public WelcomePanel()
    {
        InitializeComponent();
        Loaded += OnLoaded;
    }

    // -- Public configuration API ------------------------------------------

    public WelcomePanel Configure(
        Action          onNewFile,
        Action          onOpenFile,
        Action          onOpenProject,
        Action          onOptions,
        IReadOnlyList<string> recentFiles,
        IReadOnlyList<string> recentSolutions,
        Action<string>  openRecentFile,
        Action<string>  openRecentSolution,
        IReadOnlySet<string>? pinnedFiles     = null,
        IReadOnlySet<string>? pinnedSolutions = null,
        Action<string>? pinFile               = null,
        Action<string>? unpinFile             = null,
        Action<string>? removeFile            = null,
        Action<string>? pinSolution           = null,
        Action<string>? unpinSolution         = null,
        Action<string>? removeSolution        = null)
    {
        _onNewFile           = onNewFile;
        _onOpenFile          = onOpenFile;
        _onOpenProject       = onOpenProject;
        _onOptions           = onOptions;
        _openRecentFile      = openRecentFile;
        _openRecentSolution  = openRecentSolution;
        _pinFile             = pinFile;
        _unpinFile           = unpinFile;
        _removeFile          = removeFile;
        _pinSolution         = pinSolution;
        _unpinSolution       = unpinSolution;
        _removeSolution      = removeSolution;

        _allRecentFiles     = BuildRecentItems(recentFiles,     pinnedFiles     ?? new HashSet<string>(), isFile: true);
        _allRecentSolutions = BuildRecentItems(recentSolutions, pinnedSolutions ?? new HashSet<string>(), isFile: false);
        _templates          = BuildTemplates(onNewFile, onOpenFile, onOpenProject);
        return this;
    }

    // -- Lifecycle ---------------------------------------------------------

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        Loaded -= OnLoaded;
        BindActionButtons();
        SetVersionText();
        RenderTemplateCards();
        RenderRecentFiles(_allRecentFiles,     PinnedFilesPanel,     RecentFilesPanel,     NoRecentText);
        RenderRecentFiles(_allRecentSolutions, PinnedSolutionsPanel, RecentSolutionsPanel, NoRecentSolutionsText);
        _ = LoadNewsAsync();
    }

    // -- Version -----------------------------------------------------------

    private void SetVersionText()
    {
        var version = Assembly.GetEntryAssembly()?.GetName().Version;
        VersionText.Text = version is not null
            ? $"Version {version.Major}.{version.Minor}.{version.Build}"
            : AppResources.App_Welcome_VersionDev;
    }

    // -- Action buttons ----------------------------------------------------

    private void BindActionButtons()
    {
        NewFileButton.Click     += (_, _) => _onNewFile?.Invoke();
        OpenFileButton.Click    += (_, _) => _onOpenFile?.Invoke();
        OpenProjectButton.Click += (_, _) => _onOpenProject?.Invoke();
        OptionsButton.Click     += (_, _) => _onOptions?.Invoke();
        GitHubButton.Click      += (_, _) => OpenUrl("https://github.com/abbaye/WpfHexEditorControl");
        WikiButton.Click        += (_, _) => OpenUrl("https://github.com/abbaye/WpfHexEditorIDE/wiki");
        IssueButton.Click       += (_, _) => OpenUrl("https://github.com/abbaye/WpfHexEditorControl/issues");
    }

    // -- Template cards ----------------------------------------------------

    private static List<WelcomeTemplateItem> BuildTemplates(
        Action onNewFile, Action onOpenFile, Action onOpenProject) =>
    [
        new("C# Console App",    "Console application",        "", "Code",     onNewFile),
        new("WPF Application",   "WPF desktop application",    "", "Code",     onNewFile),
        new("Open Hex File",     "Open any binary in hex",     "", "Binary",   onOpenFile),
        new("Binary Analysis",   "Analyse binary structure",   "", "Forensics",onOpenFile),
        new("Class Diagram",     "New UML class diagram",      "", "Design",   onNewFile),
        new("Code Snippet",      "Author a reusable snippet",  "", "Tools",    onNewFile),
        new(".whfmt Template",   "New binary format template", "", "Binary",   onNewFile),
        new("Open Folder",       "Open any folder",            "", "General",  onOpenProject),
    ];

    private void RenderTemplateCards()
    {
        TemplateCardsGrid.Children.Clear();
        foreach (var tmpl in _templates)
            TemplateCardsGrid.Children.Add(BuildTemplateCard(tmpl));
    }

    private UIElement BuildTemplateCard(WelcomeTemplateItem tmpl)
    {
        var btn = new Button
        {
            Style  = FindResource("WelcomeCardButton") as Style,
            Width  = 150,
            Height = 80,
            Margin = new Thickness(0, 0, 10, 10)
        };

        var sp = new StackPanel();

        var iconRow = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 6) };
        var icon = new TextBlock
        {
            Text       = tmpl.IconGlyph,
            FontFamily = new FontFamily("Segoe MDL2 Assets"),
            FontSize   = 18,
            VerticalAlignment = VerticalAlignment.Center,
            Margin     = new Thickness(0, 0, 8, 0)
        };
        icon.SetResourceReference(ForegroundProperty, "DockTabActiveBrush");
        iconRow.Children.Add(icon);
        sp.Children.Add(iconRow);

        var title = new TextBlock
        {
            Text         = tmpl.Title,
            FontSize     = 12,
            FontWeight   = FontWeights.SemiBold,
            TextWrapping = TextWrapping.Wrap
        };
        title.SetResourceReference(ForegroundProperty, "DockMenuForegroundBrush");
        sp.Children.Add(title);

        btn.Content  = sp;
        btn.Click   += (_, _) => tmpl.OnCreate();
        return btn;
    }

    // -- Recent files / solutions ------------------------------------------

    private static List<WelcomeRecentFileItem> BuildRecentItems(
        IReadOnlyList<string> paths,
        IReadOnlySet<string>  pinned,
        bool isFile)
    {
        var items = new List<WelcomeRecentFileItem>(paths.Count);
        foreach (var p in paths)
        {
            var info = new FileInfo(p);
            if (!info.Exists) continue;
            items.Add(new WelcomeRecentFileItem(
                Path:          p,
                FileName:      System.IO.Path.GetFileName(p),
                Directory:     System.IO.Path.GetDirectoryName(p) ?? string.Empty,
                LastAccessed:  info.LastWriteTime,
                FileSizeBytes: info.Length,
                IsPinned:      pinned.Contains(p),
                IsSolution:    !isFile,
                IconGlyph:     ResolveFileIcon(p, isFile)));
        }
        items.Sort((a, b) =>
        {
            var pin = b.IsPinned.CompareTo(a.IsPinned);
            return pin != 0 ? pin : b.LastAccessed.CompareTo(a.LastAccessed);
        });
        return items;
    }

    private static string ResolveFileIcon(string path, bool isFile)
    {
        if (!isFile) return "";
        return System.IO.Path.GetExtension(path).ToLowerInvariant() switch
        {
            ".cs"    or ".vb" or ".fs"  => "",
            ".xaml"                     => "",
            ".sln"   or ".whsln"        => "",
            ".csproj"or ".whproj"       => "",
            ".json"  or ".xml"          => "",
            ".md"                       => "",
            ".whfmt"                    => "",
            _ => ""
        };
    }

    private void RenderRecentFiles(
        List<WelcomeRecentFileItem> items,
        StackPanel pinnedPanel,
        StackPanel regularPanel,
        TextBlock  noItemsText)
    {
        pinnedPanel.Children.Clear();
        regularPanel.Children.Clear();

        var pinned  = items.Where(x => x.IsPinned).ToList();
        var regular = items.Where(x => !x.IsPinned).ToList();

        foreach (var item in pinned)
            pinnedPanel.Children.Add(BuildRecentRow(item));

        foreach (var item in regular)
            regularPanel.Children.Add(BuildRecentRow(item));

        noItemsText.Visibility = (pinned.Count + regular.Count) == 0
            ? Visibility.Visible : Visibility.Collapsed;
    }

    private UIElement BuildRecentRow(WelcomeRecentFileItem item)
    {
        var outer = new Grid { Margin = new Thickness(0, 0, 0, 2) };
        outer.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        outer.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        outer.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        // Main click button
        var btn = new Button { Style = FindResource("WelcomeRecentRowButton") as Style, ToolTip = item.Path };

        var contentSp = new StackPanel { Orientation = Orientation.Horizontal };

        var iconBlock = new TextBlock
        {
            Text       = item.IconGlyph,
            FontFamily = new FontFamily("Segoe MDL2 Assets"),
            FontSize   = 14,
            VerticalAlignment = VerticalAlignment.Center,
            Margin     = new Thickness(0, 0, 8, 0)
        };
        iconBlock.SetResourceReference(ForegroundProperty, "DockTabActiveBrush");
        contentSp.Children.Add(iconBlock);

        var textStack = new StackPanel();
        var nameBlock = new TextBlock
        {
            Text         = item.FileName,
            FontSize     = 12,
            FontWeight   = FontWeights.SemiBold,
            TextTrimming = TextTrimming.CharacterEllipsis,
            MaxWidth     = 180
        };
        nameBlock.SetResourceReference(ForegroundProperty, "DockMenuForegroundBrush");
        textStack.Children.Add(nameBlock);

        var dirBlock = new TextBlock
        {
            Text         = item.Directory,
            FontSize     = 11,
            TextTrimming = TextTrimming.CharacterEllipsis,
            MaxWidth     = 180,
            Opacity      = 0.6
        };
        dirBlock.SetResourceReference(ForegroundProperty, "DockTabTextBrush");
        textStack.Children.Add(dirBlock);

        var dateBlock = new TextBlock
        {
            Text    = FormatRelativeDate(item.LastAccessed),
            FontSize = 10,
            Opacity  = 0.5
        };
        dateBlock.SetResourceReference(ForegroundProperty, "DockTabTextBrush");
        textStack.Children.Add(dateBlock);

        contentSp.Children.Add(textStack);
        btn.Content = contentSp;

        var capturedPath = item.Path;
        if (item.IsSolution)
            btn.Click += (_, _) => _openRecentSolution?.Invoke(capturedPath);
        else
            btn.Click += (_, _) => _openRecentFile?.Invoke(capturedPath);

        Grid.SetColumn(btn, 0);
        outer.Children.Add(btn);

        // Pin button
        var pinBtn = new Button
        {
            Style   = FindResource("WelcomeIconButton") as Style,
            ToolTip = item.IsPinned
                ? FindResource("APP_UnpinFile") as string ?? "Unpin"
                : FindResource("APP_PinFile") as string ?? "Pin",
            Margin  = new Thickness(2, 0, 0, 0)
        };
        var pinIcon = new TextBlock
        {
            Text       = item.IsPinned ? "" : "",
            FontFamily = new FontFamily("Segoe MDL2 Assets"),
            FontSize   = 11
        };
        pinIcon.SetResourceReference(ForegroundProperty, item.IsPinned ? "WS_PinActiveColorBrush" : "DockTabTextBrush");
        pinBtn.Content = pinIcon;
        pinBtn.Click += (_, _) =>
        {
            if (item.IsSolution)
            {
                if (item.IsPinned) _unpinSolution?.Invoke(capturedPath);
                else               _pinSolution?.Invoke(capturedPath);
            }
            else
            {
                if (item.IsPinned) _unpinFile?.Invoke(capturedPath);
                else               _pinFile?.Invoke(capturedPath);
            }
        };
        Grid.SetColumn(pinBtn, 1);
        outer.Children.Add(pinBtn);

        // Remove button
        var removeBtn = new Button
        {
            Style   = FindResource("WelcomeIconButton") as Style,
            ToolTip = FindResource("APP_RemoveRecent") as string ?? "Remove",
            Margin  = new Thickness(2, 0, 0, 0)
        };
        var removeIcon = new TextBlock
        {
            Text       = "",
            FontFamily = new FontFamily("Segoe MDL2 Assets"),
            FontSize   = 11
        };
        removeIcon.SetResourceReference(ForegroundProperty, "DockTabTextBrush");
        removeBtn.Content = removeIcon;
        removeBtn.Click += (_, _) =>
        {
            if (item.IsSolution) _removeSolution?.Invoke(capturedPath);
            else                _removeFile?.Invoke(capturedPath);
            (outer.Parent as Panel)?.Children.Remove(outer);
        };
        Grid.SetColumn(removeBtn, 2);
        outer.Children.Add(removeBtn);

        return outer;
    }

    // -- Recent search filter ----------------------------------------------

    private void OnRecentSearchChanged(object sender, TextChangedEventArgs e)
    {
        var query = RecentSearchBox.Text.Trim();
        FilterRecentPanel(PinnedFilesPanel, _allRecentFiles.Where(x => x.IsPinned), query);
        FilterRecentPanel(RecentFilesPanel, _allRecentFiles.Where(x => !x.IsPinned), query);
        FilterRecentPanel(PinnedSolutionsPanel, _allRecentSolutions.Where(x => x.IsPinned), query);
        FilterRecentPanel(RecentSolutionsPanel, _allRecentSolutions.Where(x => !x.IsPinned), query);
    }

    private void FilterRecentPanel(StackPanel panel, IEnumerable<WelcomeRecentFileItem> source, string query)
    {
        panel.Children.Clear();
        var filtered = string.IsNullOrEmpty(query)
            ? source
            : source.Where(x => x.FileName.Contains(query, StringComparison.OrdinalIgnoreCase)
                             || x.Directory.Contains(query, StringComparison.OrdinalIgnoreCase));
        foreach (var item in filtered)
            panel.Children.Add(BuildRecentRow(item));
    }

    // -- News feed ---------------------------------------------------------

    private async Task LoadNewsAsync()
    {
        ChangelogLoadingText.Visibility = Visibility.Visible;
        try
        {
            _allNewsItems = (await _newsService.GetNewsAsync()).ToList();
            ChangelogLoadingText.Visibility = Visibility.Collapsed;

            if (_allNewsItems.Count == 0)
            {
                NoChangelogText.Visibility = Visibility.Visible;
                return;
            }

            RenderNews(_activeNewsFilter);
        }
        catch
        {
            ChangelogLoadingText.Visibility = Visibility.Collapsed;
            NoChangelogText.Text            = AppResources.App_Welcome_ChangelogUnavailable;
            NoChangelogText.Visibility      = Visibility.Visible;
        }
    }

    private void RenderNews(string filter)
    {
        NewsPanel.Children.Clear();
        var items = filter == "All"
            ? _allNewsItems
            : _allNewsItems.Where(x => x.Category == filter).ToList();

        if (items.Count == 0)
        {
            NoChangelogText.Visibility = Visibility.Visible;
            return;
        }

        NoChangelogText.Visibility = Visibility.Collapsed;
        foreach (var item in items)
            NewsPanel.Children.Add(BuildNewsRow(item));
    }

    private UIElement BuildNewsRow(WelcomeNewsItem item)
    {
        var sp = new StackPanel { Margin = new Thickness(0, 0, 0, 10) };

        var headerRow = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 3) };

        // Category badge
        var badge = new Border
        {
            CornerRadius = new CornerRadius(3),
            Padding      = new Thickness(6, 2, 6, 2),
            Margin       = new Thickness(0, 0, 8, 0),
            Background   = ResolveBadgeBrush(item.Category)
        };
        badge.Child = new TextBlock
        {
            Text       = item.Category,
            FontSize   = 10,
            FontWeight = FontWeights.SemiBold,
            Foreground = Brushes.White
        };
        headerRow.Children.Add(badge);

        // Date
        var dateBlock = new TextBlock
        {
            Text              = FormatRelativeDate(item.Date),
            FontSize          = 11,
            VerticalAlignment = VerticalAlignment.Center,
            Opacity           = 0.6
        };
        dateBlock.SetResourceReference(ForegroundProperty, "DockTabTextBrush");
        headerRow.Children.Add(dateBlock);

        sp.Children.Add(headerRow);

        // Title
        if (string.IsNullOrEmpty(item.Url))
        {
            var title = new TextBlock
            {
                Text         = item.Title,
                FontSize     = 12,
                TextWrapping = TextWrapping.Wrap
            };
            title.SetResourceReference(ForegroundProperty, "DockMenuForegroundBrush");
            sp.Children.Add(title);
        }
        else
        {
            var titleBlock = new TextBlock { FontSize = 12, TextWrapping = TextWrapping.Wrap };
            var link = new Hyperlink(new Run(item.Title))
            {
                NavigateUri = new Uri(item.Url)
            };
            link.SetResourceReference(Hyperlink.ForegroundProperty, "DockTabActiveBrush");
            link.RequestNavigate += OnHyperlinkNavigate;
            titleBlock.Inlines.Add(link);
            sp.Children.Add(titleBlock);
        }

        // Summary
        if (!string.IsNullOrEmpty(item.Summary))
        {
            var summary = new TextBlock
            {
                Text         = item.Summary,
                FontSize     = 11,
                Opacity      = 0.7,
                TextWrapping = TextWrapping.Wrap,
                Margin       = new Thickness(0, 2, 0, 0)
            };
            summary.SetResourceReference(ForegroundProperty, "DockMenuForegroundBrush");
            sp.Children.Add(summary);
        }

        return sp;
    }

    // -- News filter pills -------------------------------------------------

    private void OnNewsFilterClicked(object sender, RoutedEventArgs e)
    {
        if (sender is not ToggleButton clicked) return;

        // Uncheck all, check only clicked
        foreach (var tb in new[] { FilterAll, FilterFeature, FilterFix, FilterPerf })
            tb.IsChecked = tb == clicked;

        _activeNewsFilter = clicked.Tag as string ?? "All";
        RenderNews(_activeNewsFilter);
    }

    // -- Helpers -----------------------------------------------------------

    private static Brush ResolveBadgeBrush(string category) => category switch
    {
        "Fix"      => BrushFix,
        "Perf"     => BrushPerf,
        "Breaking" => BrushBreaking,
        _          => BrushFeature
    };

    private static string FormatRelativeDate(DateTime dt)
    {
        var diff = DateTime.Now - dt;
        if (diff.TotalMinutes < 1)   return "just now";
        if (diff.TotalHours   < 1)   return $"{(int)diff.TotalMinutes}m ago";
        if (diff.TotalDays    < 1)   return $"{(int)diff.TotalHours}h ago";
        if (diff.TotalDays    < 7)   return $"{(int)diff.TotalDays}d ago";
        if (diff.TotalDays    < 365) return dt.ToString("MMM d");
        return dt.ToString("MMM d, yyyy");
    }

    private static void OpenUrl(string url)
    {
        try { Process.Start(new ProcessStartInfo(url) { UseShellExecute = true }); }
        catch { }
    }

    private void OnHyperlinkNavigate(object sender, RequestNavigateEventArgs e)
    {
        try { Process.Start(new ProcessStartInfo(e.Uri.AbsoluteUri) { UseShellExecute = true }); }
        catch { }
        e.Handled = true;
    }
}
