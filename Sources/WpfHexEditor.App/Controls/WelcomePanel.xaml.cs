// ==========================================================
// Project: WpfHexEditor.App
// File: WelcomePanel.xaml.cs
// Author: Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.6
// Created: 2026-03-06
// Description:
//     VS Start Page-style welcome document shown on IDE launch.
//     Displays quick actions, recent files/projects, and a live
//     changelog fetched from GitHub (raw content).
//
// Architecture Notes:
//     - Callbacks injected via Configure() for loose coupling with MainWindow
//     - Changelog fetched async from raw.githubusercontent.com via static HttpClient
//     - Falls back to "unavailable" message on network error (no crash)
//     - Uses only existing DynamicResource brush keys (DockBackgroundBrush, etc.)
//     - FlowDocument-free: dynamic StackPanel + TextBlock content
// ==========================================================

using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Reflection;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using System.Windows.Navigation;

namespace WpfHexEditor.App.Controls;

/// <summary>
/// VS Start Page-style welcome document shown on IDE launch.
/// Populate via <see cref="Configure"/> before adding to the visual tree.
/// </summary>
public partial class WelcomePanel : UserControl
{
    // -- Injected callbacks ---------------------------------------------

    private Action?          _onNewFile;
    private Action?          _onOpenFile;
    private Action?          _onOpenProject;
    private Action?          _onOptions;
    private Action<string>?  _openRecentFile;
    private Action<string>?  _openRecentSolution;

    // -- Brushes for changelog categories (static, work on all themes) --

    private static readonly Brush BrushAdded   = new SolidColorBrush(Color.FromArgb(0xFF, 0x4E, 0xC9, 0x6E)); // green
    private static readonly Brush BrushChanged = new SolidColorBrush(Color.FromArgb(0xFF, 0x56, 0x9C, 0xD6)); // blue
    private static readonly Brush BrushFixed   = new SolidColorBrush(Color.FromArgb(0xFF, 0xCE, 0x91, 0x78)); // orange
    private static readonly Brush BrushRemoved = new SolidColorBrush(Color.FromArgb(0xFF, 0xF4, 0x47, 0x47)); // red

    // -- GitHub changelog fetch -----------------------------------------

    private static readonly HttpClient Http = new() { Timeout = TimeSpan.FromSeconds(8) };

    private const string ChangelogUrl =
        "https://raw.githubusercontent.com/abbaye/WpfHexEditorControl/master/CHANGELOG.md";

    // -- Max versions shown in changelog -------------------------------

    private const int MaxVersionsDisplayed = 3;

    // ------------------------------------------------------------------

    public WelcomePanel()
    {
        InitializeComponent();
        Loaded += OnLoaded;
    }

    // -- Public configuration API ---------------------------------------

    /// <summary>
    /// Injects all runtime dependencies (callbacks + MRU lists).
    /// Must be called before the control is added to the visual tree.
    /// </summary>
    public WelcomePanel Configure(
        Action          onNewFile,
        Action          onOpenFile,
        Action          onOpenProject,
        Action          onOptions,
        IReadOnlyList<string> recentFiles,
        IReadOnlyList<string> recentSolutions,
        Action<string>  openRecentFile,
        Action<string>  openRecentSolution)
    {
        _onNewFile           = onNewFile;
        _onOpenFile          = onOpenFile;
        _onOpenProject       = onOpenProject;
        _onOptions           = onOptions;
        _openRecentFile      = openRecentFile;
        _openRecentSolution  = openRecentSolution;

        PopulateRecentFiles(recentFiles);
        PopulateRecentSolutions(recentSolutions);
        return this;
    }

    // -- Lifecycle ------------------------------------------------------

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        Loaded -= OnLoaded; // prevent Click-handler accumulation on docking re-attach
        BindActionButtons();
        SetVersionText();
        LoadChangelogAsync();
    }

    // -- Action buttons -------------------------------------------------

    private void BindActionButtons()
    {
        NewFileButton.Click     += (_, _) => _onNewFile?.Invoke();
        OpenFileButton.Click    += (_, _) => _onOpenFile?.Invoke();
        OpenProjectButton.Click += (_, _) => _onOpenProject?.Invoke();
        OptionsButton.Click     += (_, _) => _onOptions?.Invoke();
        GitHubButton.Click      += (_, _) => OpenUrl("https://github.com/abbaye/WpfHexEditorControl");
        IssueButton.Click       += (_, _) => OpenUrl("https://github.com/abbaye/WpfHexEditorControl/issues");
    }

    private static void OpenUrl(string url)
    {
        try { Process.Start(new ProcessStartInfo(url) { UseShellExecute = true }); }
        catch { /* silently ignore if browser unavailable */ }
    }

    // -- Version display ------------------------------------------------

    private void SetVersionText()
    {
        var version = Assembly.GetEntryAssembly()?.GetName().Version;
        VersionText.Text = version is not null
            ? $"Version {version.Major}.{version.Minor}.{version.Build}"
            : "Development build";
    }

    // -- Recent files ---------------------------------------------------

    private void PopulateRecentFiles(IReadOnlyList<string> paths)
    {
        var existing = paths.Where(File.Exists).ToList();
        if (existing.Count == 0)
        {
            NoRecentText.Visibility = Visibility.Visible;
            return;
        }

        foreach (var path in existing)
            RecentFilesPanel.Children.Add(BuildRecentButton(path, _openRecentFile));
    }

    private void PopulateRecentSolutions(IReadOnlyList<string> paths)
    {
        var existing = paths.Where(File.Exists).ToList();
        if (existing.Count == 0)
        {
            NoRecentSolutionsText.Visibility = Visibility.Visible;
            return;
        }

        foreach (var path in existing)
            RecentSolutionsPanel.Children.Add(BuildRecentButton(path, _openRecentSolution));
    }

    private Button BuildRecentButton(string path, Action<string>? callback)
    {
        var btn = new Button
        {
            Style   = FindResource("WelcomeRecentButton") as Style,
            ToolTip = path
        };

        var sp = new StackPanel { Orientation = Orientation.Horizontal };

        // Folder icon for .whsln / .whproj, file icon for others
        var isSolution = path.EndsWith(".whsln", StringComparison.OrdinalIgnoreCase)
                      || path.EndsWith(".whproj", StringComparison.OrdinalIgnoreCase);

        var iconBlock = new TextBlock
        {
            Text        = isSolution ? "\uE8B7" : "\uE8A5",
            FontFamily  = new FontFamily("Segoe MDL2 Assets"),
            FontSize    = 13,
            VerticalAlignment = VerticalAlignment.Center,
            Margin      = new Thickness(0, 0, 8, 0),
        };
        iconBlock.SetResourceReference(TextBlock.ForegroundProperty, "DockTabActiveBrush");
        sp.Children.Add(iconBlock);

        sp.Children.Add(new TextBlock
        {
            Text              = Path.GetFileName(path),
            VerticalAlignment = VerticalAlignment.Center,
            TextTrimming      = TextTrimming.CharacterEllipsis
        });

        btn.Content  = sp;
        btn.Click   += (_, _) => callback?.Invoke(path);
        return btn;
    }

    // -- Changelog fetch + parse ----------------------------------------

    /// <summary>
    /// Fetches CHANGELOG.md from GitHub and populates the changelog panel.
    /// Shows a loading placeholder while in-flight; degrades gracefully on network error.
    /// </summary>
    private async void LoadChangelogAsync()
    {
        ChangelogLoadingText.Visibility = Visibility.Visible;
        try
        {
            var content  = await Http.GetStringAsync(ChangelogUrl).ConfigureAwait(true);
            var versions = ParseChangelog(content.Split('\n'));

            ChangelogLoadingText.Visibility = Visibility.Collapsed;

            if (versions.Count == 0)
            {
                NoChangelogText.Visibility = Visibility.Visible;
                return;
            }

            foreach (var version in versions)
                ChangelogPanel.Children.Add(BuildVersionBlock(version));
        }
        catch
        {
            ChangelogLoadingText.Visibility = Visibility.Collapsed;
            NoChangelogText.Text            = "Changelog unavailable — check your connection.";
            NoChangelogText.Visibility      = Visibility.Visible;
        }
    }

    // -- Changelog model ------------------------------------------------

    private sealed record ChangelogVersion(string Label, string Date, string Title,
                                           List<ChangelogSection> Sections);
    private sealed record ChangelogSection(string Title, List<string> Entries);

    // -- Parser ---------------------------------------------------------

    // Matches: ## [Unreleased] — 2026-03 — IDE & Project System
    //      or: ## [2.7.0] — 2026-02 — Title
    private static readonly Regex VersionHeader =
        new(@"^##\s+\[([^\]]+)\](?:\s+[—-]+\s+(\S+))?(?:\s+[—-]+\s+(.+))?$",
            RegexOptions.Compiled);

    // Matches: ### ✨ Added — Category   or  ### 🔧 Changed
    private static readonly Regex SectionHeader =
        new(@"^###\s+(.+)$", RegexOptions.Compiled);

    // Matches: - **item** — description   or  - Plain text
    private static readonly Regex EntryLine =
        new(@"^-\s+(.+)$", RegexOptions.Compiled);

    private static List<ChangelogVersion> ParseChangelog(string[] lines)
    {
        var versions = new List<ChangelogVersion>();
        ChangelogVersion?  current = null;
        ChangelogSection?  section = null;

        foreach (var raw in lines)
        {
            var line = raw.TrimEnd();

            // Version header
            var vm = VersionHeader.Match(line);
            if (vm.Success)
            {
                if (versions.Count >= MaxVersionsDisplayed) break;

                section = null;
                current = new ChangelogVersion(
                    Label:    vm.Groups[1].Value,
                    Date:     vm.Groups[2].Value,
                    Title:    vm.Groups[3].Value,
                    Sections: []);
                versions.Add(current);
                continue;
            }

            if (current is null) continue;

            // Sub-section header
            var sm = SectionHeader.Match(line);
            if (sm.Success)
            {
                section = new ChangelogSection(sm.Groups[1].Value, []);
                current.Sections.Add(section);
                continue;
            }

            // Entry
            var em = EntryLine.Match(line);
            if (em.Success && section is not null)
            {
                // Strip markdown bold (**...**) for cleaner display
                var text = Regex.Replace(em.Groups[1].Value, @"\*\*(.+?)\*\*", "$1");
                section.Entries.Add(text);
            }
        }

        return versions;
    }

    // -- Changelog WPF rendering ----------------------------------------

    private UIElement BuildVersionBlock(ChangelogVersion version)
    {
        var container = new StackPanel { Margin = new Thickness(0, 0, 0, 24) };

        // Version badge row
        var headerRow = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Margin      = new Thickness(0, 0, 0, 10)
        };

        var badge = new Border
        {
            CornerRadius = new CornerRadius(3),
            Padding      = new Thickness(7, 2, 7, 2),
            Margin       = new Thickness(0, 0, 10, 0)
        };
        badge.SetResourceReference(Border.BackgroundProperty, "DockTabActiveBrush");
        badge.Child = new TextBlock
        {
            Text       = version.Label,
            FontSize   = 11,
            FontWeight = FontWeights.SemiBold,
            Foreground = Brushes.White
        };
        headerRow.Children.Add(badge);

        if (!string.IsNullOrEmpty(version.Date))
        {
            var dateBlock = new TextBlock
            {
                Text              = version.Date,
                FontSize          = 12,
                VerticalAlignment = VerticalAlignment.Center,
                Margin            = new Thickness(0, 0, 8, 0),
                Opacity           = 0.7
            };
            dateBlock.SetResourceReference(TextBlock.ForegroundProperty, "DockTabTextBrush");
            headerRow.Children.Add(dateBlock);
        }

        if (!string.IsNullOrEmpty(version.Title))
        {
            var titleBlock = new TextBlock
            {
                Text              = "— " + version.Title,
                FontSize          = 12,
                VerticalAlignment = VerticalAlignment.Center,
                Opacity           = 0.85
            };
            titleBlock.SetResourceReference(TextBlock.ForegroundProperty, "DockMenuForegroundBrush");
            headerRow.Children.Add(titleBlock);
        }

        container.Children.Add(headerRow);

        // Sections
        foreach (var sec in version.Sections)
        {
            var secColor = ResolveSectionColor(sec.Title);
            container.Children.Add(BuildSectionBlock(sec, secColor));
        }

        return container;
    }

    private UIElement BuildSectionBlock(ChangelogSection section, Brush sectionColor)
    {
        var sp = new StackPanel { Margin = new Thickness(0, 0, 0, 8) };

        sp.Children.Add(new TextBlock
        {
            Text       = section.Title,
            FontSize   = 11,
            FontWeight = FontWeights.SemiBold,
            Foreground = sectionColor,
            Margin     = new Thickness(0, 0, 0, 3)
        });

        foreach (var entry in section.Entries)
        {
            var entryBlock = new TextBlock
            {
                Text         = "· " + entry,
                FontSize     = 12,
                Margin       = new Thickness(10, 0, 0, 2),
                TextWrapping = TextWrapping.Wrap,
                Opacity      = 0.9
            };
            entryBlock.SetResourceReference(TextBlock.ForegroundProperty, "DockMenuForegroundBrush");
            sp.Children.Add(entryBlock);
        }

        return sp;
    }

    private static Brush ResolveSectionColor(string sectionTitle)
    {
        var t = sectionTitle.ToUpperInvariant();
        if (t.Contains("ADDED") || t.Contains("✨")) return BrushAdded;
        if (t.Contains("CHANGED") || t.Contains("🔧")) return BrushChanged;
        if (t.Contains("FIXED") || t.Contains("🐛")) return BrushFixed;
        if (t.Contains("REMOVED") || t.Contains("🗑")) return BrushRemoved;
        return BrushChanged; // default: blue
    }

    // -- Hyperlink navigation -------------------------------------------

    private void OnHyperlinkNavigate(object sender, RequestNavigateEventArgs e)
    {
        try
        {
            Process.Start(new ProcessStartInfo(e.Uri.AbsoluteUri) { UseShellExecute = true });
        }
        catch { /* silently ignore if browser unavailable */ }
        e.Handled = true;
    }
}
