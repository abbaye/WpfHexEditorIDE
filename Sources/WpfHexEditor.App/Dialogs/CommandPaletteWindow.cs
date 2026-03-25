// ==========================================================
// Project: WpfHexEditor.App
// File: Dialogs/CommandPaletteWindow.cs
// Author: Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.6
// Description:
//     VS Code / JetBrains Rider-style Command Palette overlay (Ctrl+Shift+P).
//     Code-behind only — no XAML. Supports:
//       • 3-tier fuzzy search with per-character match highlight
//       • Mode prefixes: > (commands) @ (LSP symbols) : (go to line) # (open files) ? (help)
//       • Tab to cycle modes; Ctrl+Backspace to clear prefix
//       • Description display: None / Tooltip / BottomPanel (from Options)
//       • Category group headers when query is empty
//       • Async search with spinner for LSP symbol mode
//       • Extended keyboard: PageUp/Down (5 entries), Ctrl+Enter (no close)
//       • Reads all display preferences from CommandPaletteSettings
//
// Architecture Notes:
//     Non-modal Window (Show, not ShowDialog). Closes on Deactivated.
//     Uses CP_* resource tokens from the active theme.
// ==========================================================

using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Effects;
using System.Windows.Threading;
using System.IO;
using WpfHexEditor.App.Models;
using WpfHexEditor.App.Services;
using WpfHexEditor.Editor.Core;
using WpfHexEditor.Editor.Core.Documents;
using WpfHexEditor.Editor.Core.LSP;
using WpfHexEditor.Core.Options;

namespace WpfHexEditor.App.Dialogs;

/// <summary>
/// Floating command palette overlay. Opened non-modally; closes when deactivated or Esc pressed.
/// </summary>
public sealed class CommandPaletteWindow : Window
{
    // ── Private types ─────────────────────────────────────────────────────────

    /// <summary>Carries file + line info for a content-grep (% mode) result.</summary>
    private sealed record GrepMatch(string FilePath, int LineNumber, int MatchStart, int MatchLength);

    // ── Dependencies ─────────────────────────────────────────────────────────

    private readonly CommandPaletteService  _service;
    private readonly CommandPaletteSettings _settings;
    private readonly Window                 _owner;
    private readonly Point?                 _anchor;
    private readonly ILspClient?            _lspClient;
    private readonly IDocumentManager?      _documentManager;
    private readonly Action<int>?           _goToLine;          // callback: jump to 1-based line in active editor
    private readonly ISolutionManager?      _solutionManager;
    private readonly Action<string, int>?   _openAndNavigate;   // (filePath, lineNumber 0-based)

    // ── Child controls ───────────────────────────────────────────────────────

    private readonly TextBox       _searchBox;
    private readonly ListBox       _resultsList;
    private readonly Border        _modeTagBorder;
    private readonly TextBlock     _modeTagText;
    private readonly Border        _descPanel;
    private readonly TextBlock     _descText;
    private readonly TextBlock     _spinnerText;

    // ── State ────────────────────────────────────────────────────────────────

    private string _currentMode   = "";         // "" | ">" | "@" | ":" | "#" | "?"
    private bool   _closingStarted;
    private CancellationTokenSource _searchCts  = new();
    private readonly string? _initialQuery;

    // Mode cycle order (Tab key)
    private static readonly string[] ModeCycle = { "", "@", "#", "%", ":", ">" };

    // ─────────────────────────────────────────────────────────────────────────

    public CommandPaletteWindow(
        CommandPaletteService   service,
        CommandPaletteSettings  settings,
        Window                  owner,
        Point?                  anchor           = null,
        ILspClient?             lspClient        = null,
        IDocumentManager?       documentManager  = null,
        Action<int>?            goToLine         = null,
        ISolutionManager?       solutionManager  = null,
        Action<string, int>?    openAndNavigate  = null,
        string?                 initialQuery     = null)
    {
        _service          = service;
        _settings         = settings;
        _owner            = owner;
        _anchor           = anchor;
        _lspClient        = lspClient;
        _documentManager  = documentManager;
        _goToLine         = goToLine;
        _solutionManager  = solutionManager;
        _openAndNavigate  = openAndNavigate;
        _initialQuery     = initialQuery;

        // Window chrome
        WindowStyle        = WindowStyle.None;
        AllowsTransparency = true;
        Background         = Brushes.Transparent;
        ResizeMode         = ResizeMode.NoResize;
        ShowInTaskbar      = false;
        Topmost            = true;
        Width              = Math.Clamp(settings.WindowWidth, 400, 900);
        SizeToContent      = SizeToContent.Height;
        MaxHeight          = 520;

        // ─── Root border ──────────────────────────────────────────────────────
        var root = new Border
        {
            CornerRadius = new CornerRadius(6),
            Effect = new DropShadowEffect
            {
                Direction   = 315,
                ShadowDepth = 6,
                BlurRadius  = 18,
                Opacity     = 0.55,
                Color       = Colors.Black
            }
        };
        root.SetResourceReference(Border.BackgroundProperty,  "CP_BackgroundBrush");
        root.SetResourceReference(Border.BorderBrushProperty, "CP_BorderBrush");
        root.BorderThickness = new Thickness(1);
        Content = root;

        // ─── Outer grid: search row | results | desc panel ────────────────────
        var outerGrid = new Grid();
        outerGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });           // 0: search
        outerGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) }); // 1: results
        outerGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });           // 2: desc panel
        root.Child = outerGrid;

        // ─── Search row ───────────────────────────────────────────────────────
        var inputBorder = new Border
        {
            Padding         = new Thickness(8, 6, 8, 6),
            BorderThickness = new Thickness(0, 0, 0, 1)
        };
        inputBorder.SetResourceReference(Border.BackgroundProperty,  "CP_InputBackgroundBrush");
        inputBorder.SetResourceReference(Border.BorderBrushProperty, "CP_BorderBrush");

        var searchDock = new DockPanel { LastChildFill = true };

        // Search icon (left)
        var searchIcon = new TextBlock
        {
            Text              = "\uE721",
            FontFamily        = new FontFamily("Segoe MDL2 Assets"),
            FontSize          = 14,
            VerticalAlignment = VerticalAlignment.Center,
            Margin            = new Thickness(0, 0, 6, 0)
        };
        searchIcon.SetResourceReference(TextBlock.ForegroundProperty, "CP_SecondaryTextBrush");
        DockPanel.SetDock(searchIcon, Dock.Left);
        searchDock.Children.Add(searchIcon);

        // Mode tag badge (left, hidden when no mode)
        _modeTagText = new TextBlock
        {
            VerticalAlignment = VerticalAlignment.Center,
            FontSize          = 11,
            FontWeight        = FontWeights.SemiBold,
            Margin            = new Thickness(0, 0, 4, 0)
        };
        _modeTagText.SetResourceReference(TextBlock.ForegroundProperty, "CP_HighlightBrush");
        _modeTagBorder = new Border
        {
            CornerRadius    = new CornerRadius(3),
            Padding         = new Thickness(4, 1, 4, 1),
            Margin          = new Thickness(0, 0, 6, 0),
            Visibility      = Visibility.Collapsed,
            Child           = _modeTagText
        };
        _modeTagBorder.SetResourceReference(Border.BackgroundProperty, "CP_GroupHeaderBrush");
        DockPanel.SetDock(_modeTagBorder, Dock.Left);
        searchDock.Children.Add(_modeTagBorder);

        // Spinner (right, async modes)
        _spinnerText = new TextBlock
        {
            Text              = "\uE72C",     // MDL2 "Sync" glyph used as spinner placeholder
            FontFamily        = new FontFamily("Segoe MDL2 Assets"),
            FontSize          = 12,
            VerticalAlignment = VerticalAlignment.Center,
            Margin            = new Thickness(0, 0, 6, 0),
            Visibility        = Visibility.Collapsed
        };
        _spinnerText.SetResourceReference(TextBlock.ForegroundProperty, "CP_SecondaryTextBrush");
        DockPanel.SetDock(_spinnerText, Dock.Right);
        searchDock.Children.Add(_spinnerText);

        // Search TextBox
        _searchBox = new TextBox
        {
            FontSize                 = 14,
            BorderThickness          = new Thickness(0),
            Background               = Brushes.Transparent,
            VerticalContentAlignment = VerticalAlignment.Center
        };
        _searchBox.SetResourceReference(TextBox.ForegroundProperty,     "CP_TextBrush");
        _searchBox.SetResourceReference(TextBox.CaretBrushProperty,     "CP_TextBrush");
        _searchBox.SetResourceReference(TextBox.SelectionBrushProperty, "CP_HighlightBrush");
        _searchBox.TextChanged    += OnSearchTextChanged;
        _searchBox.PreviewKeyDown += OnSearchBoxKeyDown;
        searchDock.Children.Add(_searchBox);

        inputBorder.Child = searchDock;
        Grid.SetRow(inputBorder, 0);
        outerGrid.Children.Add(inputBorder);

        // ─── Results list ─────────────────────────────────────────────────────
        _resultsList = new ListBox
        {
            BorderThickness            = new Thickness(0),
            HorizontalContentAlignment = HorizontalAlignment.Stretch
        };
        ScrollViewer.SetHorizontalScrollBarVisibility(_resultsList, ScrollBarVisibility.Disabled);
        VirtualizingPanel.SetIsVirtualizing(_resultsList, true);
        VirtualizingPanel.SetVirtualizationMode(_resultsList, VirtualizationMode.Recycling);
        _resultsList.SetResourceReference(ListBox.BackgroundProperty, "CP_BackgroundBrush");
        _resultsList.SetResourceReference(ListBox.ForegroundProperty, "CP_TextBrush");
        _resultsList.MouseDoubleClick  += (_, _) => ExecuteSelected(closeAfter: true);
        _resultsList.SelectionChanged  += OnResultsSelectionChanged;
        _resultsList.ItemContainerStyle = BuildItemContainerStyle();
        _resultsList.ItemTemplateSelector = new PaletteItemTemplateSelector(this);
        Grid.SetRow(_resultsList, 1);
        outerGrid.Children.Add(_resultsList);

        // ─── Description bottom panel ─────────────────────────────────────────
        _descText = new TextBlock
        {
            FontSize    = 12,
            TextWrapping = TextWrapping.Wrap,
            MaxHeight    = 60,
            Margin       = new Thickness(12, 6, 12, 6)
        };
        _descText.SetResourceReference(TextBlock.ForegroundProperty, "CP_SecondaryTextBrush");

        _descPanel = new Border
        {
            BorderThickness = new Thickness(0, 1, 0, 0),
            Visibility      = Visibility.Collapsed,
            Child           = _descText
        };
        _descPanel.SetResourceReference(Border.BackgroundProperty,   "CP_DescPanelBrush");
        _descPanel.SetResourceReference(Border.BorderBrushProperty,  "CP_BorderBrush");
        Grid.SetRow(_descPanel, 2);
        outerGrid.Children.Add(_descPanel);

        // ─── Window events ────────────────────────────────────────────────────
        Deactivated += (_, _) =>
        {
            if (_closingStarted) return;
            _closingStarted = true;
            _searchCts.Cancel();   // stop async ops before UI tears down
            Close();
        };
        Loaded      += OnLoaded;

        // Apply default mode prefix from settings
        if (!string.IsNullOrEmpty(settings.DefaultMode))
            ApplyModePrefix(settings.DefaultMode);
    }

    // ── Closing ──────────────────────────────────────────────────────────────
    // OnClosing fires BEFORE Deactivated — set the flag here so the Deactivated
    // handler sees it and skips the redundant Close() call.

    protected override void OnClosing(System.ComponentModel.CancelEventArgs e)
    {
        _closingStarted = true;
        _searchCts.Cancel();
        base.OnClosing(e);
    }

    // ── Loaded ───────────────────────────────────────────────────────────────

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        if (_anchor.HasValue)
        {
            Left = _anchor.Value.X - ActualWidth / 2;
            Top  = _anchor.Value.Y;
        }
        else
        {
            Left = _owner.Left + (_owner.Width - ActualWidth) / 2;
            Top  = _owner.Top  + _owner.Height * 0.18;
        }

        if (!string.IsNullOrEmpty(_initialQuery))
        {
            _searchBox.Text = _initialQuery;
            _searchBox.CaretIndex = _initialQuery.Length;
        }
        _ = RefreshAsync(_searchBox.Text);
        _searchBox.Focus();
    }

    // ── Text changed ─────────────────────────────────────────────────────────

    private void OnSearchTextChanged(object sender, TextChangedEventArgs e)
    {
        var raw = _searchBox.Text;
        var (mode, query) = ParseInput(raw);
        if (mode != _currentMode)
            UpdateModeTag(mode);
        _currentMode = mode;

        if (_settings.SearchDebounceMs <= 0)
            _ = RefreshAsync(query);
        else
        {
            // Simple debounce via dispatcher delay
            _searchCts.Cancel();
            _searchCts = new CancellationTokenSource();
            var ct = _searchCts.Token;
            Dispatcher.InvokeAsync(async () =>
            {
                await Task.Delay(_settings.SearchDebounceMs, ct).ContinueWith(_ => { }, CancellationToken.None);
                if (!ct.IsCancellationRequested)
                    await RefreshAsync(query);
            }, DispatcherPriority.Normal, ct);
        }
    }

    // ── Refresh ───────────────────────────────────────────────────────────────

    private async Task RefreshAsync(string query)
    {
        _searchCts.Cancel();
        _searchCts = new CancellationTokenSource();
        var ct = _searchCts.Token;

        switch (_currentMode)
        {
            case "@":
                await RefreshSymbolsAsync(query, ct);
                break;
            case ":":
                RefreshLineMode(query);
                break;
            case "#":
                RefreshFilesMode(query);
                break;
            case "%":
                await RefreshContentSearchAsync(query, ct);
                break;
            case "?":
                RefreshHelpMode();
                break;
            default: // "" or ">"
                RefreshCommandsMode(query);
                break;
        }
    }

    private void RefreshCommandsMode(string query)
    {
        var results = _service.Filter(query, _settings);
        IEnumerable<CommandPaletteEntry> items = results;

        if (_settings.ShowCategoryHeaders && string.IsNullOrEmpty(query))
            items = InsertGroupHeaders(results);

        BindResults(items.ToList());
    }

    private async Task RefreshSymbolsAsync(string query, CancellationToken ct)
    {
        if (_lspClient is null || string.IsNullOrWhiteSpace(query))
        {
            BindResults(new List<CommandPaletteEntry>());
            return;
        }

        if (!_closingStarted) _spinnerText.Visibility = Visibility.Visible;
        try
        {
            var symbols = await _lspClient.WorkspaceSymbolsAsync(query, ct);
            if (ct.IsCancellationRequested) return;

            var entries = symbols.Select(s => new CommandPaletteEntry(
                Name:        s.Name,
                Category:    s.ContainerName ?? s.Kind,
                GestureText: null,
                IconGlyph:   SymbolKindGlyph(s.Kind),
                Command:     null,   // navigation handled via special execute path
                Description: $"{System.IO.Path.GetFileName(s.Uri)}  line {s.StartLine + 1}")).ToList();

            BindResults(entries);
        }
        catch (OperationCanceledException) { }
        finally
        {
            if (!_closingStarted) _spinnerText.Visibility = Visibility.Collapsed;
        }
    }

    private void RefreshLineMode(string query)
    {
        if (int.TryParse(query.Trim(), out var line) && line > 0)
        {
            BindResults(new List<CommandPaletteEntry>
            {
                new(Name: $"Go to line {line}",
                    Category: "Navigation",
                    GestureText: null,
                    IconGlyph: "\uE700",
                    Command: null,
                    Description: $"Jump to line {line} in the active editor")
            });
        }
        else
        {
            BindResults(new List<CommandPaletteEntry>());
        }
    }

    private void RefreshFilesMode(string query)
    {
        // Build set of already-open file paths for badge
        var openPaths = _documentManager?.OpenDocuments
            .Where(d => d.FilePath != null)
            .Select(d => d.FilePath!)
            .ToHashSet(StringComparer.OrdinalIgnoreCase) ?? [];

        // Enumerate all solution items; fall back to open tabs if no solution
        var allItems = _solutionManager?.CurrentSolution?.Projects
            .SelectMany(p => p.Items.Select(i => (ProjectName: p.Name, Item: i)))
            .Where(x => x.Item.AbsolutePath != null)
            .ToList() ?? [];

        if (allItems.Count == 0)
        {
            // Fallback: open documents only (no solution loaded)
            var docs = _documentManager?.OpenDocuments ?? Array.Empty<DocumentModel>();
            BindResults(docs
                .Where(d => d.FilePath != null)
                .Select(d => new CommandPaletteEntry(
                    Name:             Path.GetFileName(d.FilePath!),
                    Category:         "Open Files",
                    GestureText:      null,
                    IconGlyph:        "\uE8A5",
                    Command:          null,
                    Description:      d.FilePath,
                    CommandParameter: d.FilePath))   // use path for _openAndNavigate
                .ToList());
            return;
        }

        var q      = query.Trim();
        var scored = new List<(CommandPaletteEntry E, int Score, bool IsOpen)>();

        foreach (var (projectName, item) in allItems)
        {
            var filename = Path.GetFileName(item.AbsolutePath);
            var isOpen   = openPaths.Contains(item.AbsolutePath);
            int score; int[] indices;

            if (string.IsNullOrEmpty(q))
            {
                score   = isOpen ? 1 : 0;
                indices = Array.Empty<int>();
            }
            else
            {
                (score, indices) = ScoreFilename(filename, q);
                if (score < 0) continue;
            }

            scored.Add((new CommandPaletteEntry(
                Name:             filename,
                Category:         projectName,
                GestureText:      isOpen ? "● Open" : null,
                IconGlyph:        "\uE8A5",
                Command:          null,
                Description:      item.AbsolutePath,
                MatchIndices:     indices,
                CommandParameter: item.AbsolutePath),   // full path for _openAndNavigate
                score, isOpen));
        }

        var results = scored
            .OrderByDescending(x => x.IsOpen ? 10_000 : 0)
            .ThenByDescending(x => x.Score)
            .Take(_settings.MaxResults)
            .Select(x => x.E)
            .ToList();

        BindResults(results);
    }

    // Known binary extensions — skip without reading
    private static readonly HashSet<string> BinaryExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".exe", ".dll", ".pdb", ".obj", ".lib", ".a",
        ".png", ".jpg", ".jpeg", ".gif", ".bmp", ".ico", ".tiff", ".webp",
        ".mp3", ".wav", ".ogg", ".flac",
        ".zip", ".7z", ".tar", ".gz", ".rar",
        ".bin", ".dat", ".db", ".sqlite", ".nupkg",
        ".pdf", ".doc", ".docx", ".xls", ".xlsx",
    };

    private async Task RefreshContentSearchAsync(string query, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(query) || query.Length < 2)
        {
            BindResults(new List<CommandPaletteEntry>());
            return;
        }

        static bool IsTextItem(ProjectItemType t) =>
            t is not (ProjectItemType.Binary or ProjectItemType.Image
                     or ProjectItemType.Audio or ProjectItemType.Tile);

        var items = _solutionManager?.CurrentSolution?.Projects
            .SelectMany(p => p.Items)
            .Where(i => i.AbsolutePath != null && IsTextItem(i.ItemType))
            .ToList() ?? [];

        if (!_closingStarted) _spinnerText.Visibility = Visibility.Visible;
        var bag = new System.Collections.Concurrent.ConcurrentBag<CommandPaletteEntry>();

        try
        {
            await Parallel.ForEachAsync(
                items,
                new ParallelOptions { MaxDegreeOfParallelism = 4, CancellationToken = ct },
                async (item, innerCt) =>
                {
                    if (_closingStarted || bag.Count >= _settings.MaxGrepResults) return;
                    var path = item.AbsolutePath!;
                    var ext  = Path.GetExtension(path) ?? "";
                    if (!File.Exists(path)) return;
                    if (BinaryExtensions.Contains(ext)) return;
                    if (new FileInfo(path).Length > _settings.MaxGrepFileSizeBytes) return;

                    var filename = Path.GetFileName(path);
                    try
                    {
                        using var sr = new StreamReader(path, detectEncodingFromByteOrderMarks: true);
                        string? rawLine; int lineIdx = 0;
                        while ((rawLine = await sr.ReadLineAsync(innerCt)) is not null
                               && bag.Count < _settings.MaxGrepResults)
                        {
                            innerCt.ThrowIfCancellationRequested();
                            var matchIdx = rawLine.IndexOf(query, StringComparison.OrdinalIgnoreCase);
                            if (matchIdx >= 0)
                            {
                                var (snippet, adjIdx) = TrimSnippet(rawLine, matchIdx, 72);
                                bag.Add(new CommandPaletteEntry(
                                    Name:             snippet,
                                    Category:         $"{filename}:{lineIdx + 1}",
                                    GestureText:      null,
                                    IconGlyph:        "\uE721",
                                    Command:          null,
                                    Description:      path,
                                    MatchIndices:     Enumerable.Range(adjIdx, Math.Min(query.Length, snippet.Length - adjIdx)).ToArray(),
                                    CommandParameter: new GrepMatch(path, lineIdx, matchIdx, query.Length)));
                            }
                            lineIdx++;
                        }
                    }
                    catch { /* skip unreadable / binary-detected-at-runtime */ }
                });
        }
        catch (OperationCanceledException) { return; }
        finally { if (!_closingStarted) _spinnerText.Visibility = Visibility.Collapsed; }

        if (!_closingStarted)
            BindResults(bag.OrderBy(e => e.Category).Take(_settings.MaxGrepResults).ToList());
    }

    /// <summary>
    /// 3-tier fuzzy scoring on a filename (same tiers as CommandPaletteService.ScoreEntry).
    /// Returns (score, matchIndices); score &lt; 0 means no match.
    /// </summary>
    private static (int Score, int[] Indices) ScoreFilename(string name, string needle)
    {
        if (string.IsNullOrEmpty(name) || string.IsNullOrEmpty(needle))
            return (-1, Array.Empty<int>());

        // Tier 1: prefix
        if (name.StartsWith(needle, StringComparison.OrdinalIgnoreCase))
            return (1000 - name.Length, Enumerable.Range(0, needle.Length).ToArray());

        // Tier 2: substring
        var idx = name.IndexOf(needle, StringComparison.OrdinalIgnoreCase);
        if (idx >= 0)
            return (500 - idx, Enumerable.Range(idx, needle.Length).ToArray());

        // Tier 3: subsequence
        var positions = new int[needle.Length];
        var hi = 0; var ni = 0; var gaps = 0; var last = -1;
        while (ni < needle.Length && hi < name.Length)
        {
            if (char.ToUpperInvariant(name[hi]) == char.ToUpperInvariant(needle[ni]))
            {
                if (last >= 0) gaps += hi - last - 1;
                positions[ni] = hi;
                last = hi; ni++;
            }
            hi++;
        }
        return ni == needle.Length ? (100 - gaps, positions) : (-1, Array.Empty<int>());
    }

    /// <summary>
    /// Trims a source line to at most <paramref name="maxLen"/> chars centered
    /// around the match, prefixing "…" when the start is truncated.
    /// Returns the trimmed text and the adjusted match-start index within that text.
    /// </summary>
    private static (string Text, int AdjIdx) TrimSnippet(string line, int matchIdx, int maxLen)
    {
        var trimmed  = line.TrimStart();
        var trimDiff = line.Length - trimmed.Length;
        matchIdx     = Math.Max(0, matchIdx - trimDiff);
        if (trimmed.Length <= maxLen) return (trimmed, matchIdx);
        var start  = Math.Max(0, matchIdx - maxLen / 3);
        var prefix = start > 0 ? "…" : "";
        var text   = prefix + trimmed.Substring(start, Math.Min(maxLen, trimmed.Length - start));
        return (text, matchIdx - start + prefix.Length);
    }

    private void RefreshHelpMode()
    {
        var help = new List<CommandPaletteEntry>
        {
            new("(vide) ou >", "Modes",  null, "\uE721", null, Description: "Recherche dans toutes les commandes"),
            new("@",           "Modes",  null, "\uE8D2", null, Description: "Go to Symbol — recherche LSP workspace symbols"),
            new(":",           "Modes",  null, "\uE700", null, Description: "Go to Line — saisir un numéro de ligne"),
            new("#",           "Modes",  null, "\uE8A5", null, Description: "Fichiers solution — fuzzy search sur nom de fichier"),
            new("%",           "Modes",  null, "\uE721", null, Description: "Grep — recherche dans le contenu des fichiers solution"),
            new("?",           "Modes",  null, "\uE897", null, Description: "Aide — affiche cette liste de modes"),
        };
        BindResults(help);
    }

    private void BindResults(List<CommandPaletteEntry> results)
    {
        if (_closingStarted) return;
        _resultsList.ItemsSource = results;
        // Skip group headers when selecting first item
        var first = results.FirstOrDefault(e => !e.IsGroupHeader);
        if (first is not null)
            _resultsList.SelectedItem = first;
    }

    // ── Group headers ─────────────────────────────────────────────────────────

    private static IEnumerable<CommandPaletteEntry> InsertGroupHeaders(
        IReadOnlyList<CommandPaletteEntry> entries)
    {
        string? lastCategory = null;
        foreach (var e in entries)
        {
            if (e.IsRecent && lastCategory != "⟳ Récents")
            {
                lastCategory = "⟳ Récents";
                yield return new CommandPaletteEntry("⟳ Récents", "", null, null, null, IsGroupHeader: true);
            }
            else if (!e.IsRecent && e.Category != lastCategory)
            {
                lastCategory = e.Category;
                yield return new CommandPaletteEntry(e.Category, "", null, null, null, IsGroupHeader: true);
            }
            yield return e;
        }
    }

    // ── Selection changed → description panel ────────────────────────────────

    private void OnResultsSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_settings.DescriptionMode != CpDescriptionMode.BottomPanel) return;
        if (_resultsList.SelectedItem is not CommandPaletteEntry entry || entry.IsGroupHeader)
        {
            _descPanel.Visibility = Visibility.Collapsed;
            return;
        }

        var desc = entry.Description;
        if (string.IsNullOrEmpty(desc))
        {
            _descPanel.Visibility = Visibility.Collapsed;
        }
        else
        {
            _descText.Text        = desc;
            _descPanel.Visibility = Visibility.Visible;
        }
    }

    // ── Keyboard ──────────────────────────────────────────────────────────────

    private void OnSearchBoxKeyDown(object sender, KeyEventArgs e)
    {
        switch (e.Key)
        {
            case Key.Down:
                MoveSelection(+1);
                e.Handled = true;
                break;

            case Key.Up:
                MoveSelection(-1);
                e.Handled = true;
                break;

            case Key.PageDown:
                MoveSelection(+5);
                e.Handled = true;
                break;

            case Key.PageUp:
                MoveSelection(-5);
                e.Handled = true;
                break;

            case Key.Enter when (Keyboard.Modifiers & ModifierKeys.Control) != 0:
                ExecuteSelected(closeAfter: false);
                e.Handled = true;
                break;

            case Key.Enter:
                ExecuteSelected(closeAfter: true);
                e.Handled = true;
                break;

            case Key.Escape:
                Close();
                e.Handled = true;
                break;

            case Key.Tab:
                CycleModePrefix();
                e.Handled = true;
                break;

            case Key.Back when (Keyboard.Modifiers & ModifierKeys.Control) != 0:
                ClearModePrefix();
                e.Handled = true;
                break;
        }
    }

    private void MoveSelection(int delta)
    {
        if (_resultsList.Items.Count == 0) return;
        var items = _resultsList.Items.Cast<CommandPaletteEntry>().ToList();
        var idx   = _resultsList.SelectedIndex;
        var next  = Math.Clamp(idx + delta, 0, items.Count - 1);
        // Skip group headers
        while (next > 0 && next < items.Count - 1 && items[next].IsGroupHeader)
            next += delta > 0 ? 1 : -1;
        if (!items[next].IsGroupHeader)
        {
            _resultsList.SelectedIndex = next;
            _resultsList.ScrollIntoView(_resultsList.SelectedItem);
        }
    }

    // ── Mode prefix helpers ───────────────────────────────────────────────────

    private static (string Mode, string Query) ParseInput(string raw)
    {
        if (string.IsNullOrEmpty(raw)) return ("", "");
        return raw[0] switch
        {
            '>' => (">",  raw.Length > 1 ? raw[1..].TrimStart() : ""),
            '@' => ("@",  raw.Length > 1 ? raw[1..].TrimStart() : ""),
            ':' => (":",  raw.Length > 1 ? raw[1..].TrimStart() : ""),
            '#' => ("#",  raw.Length > 1 ? raw[1..].TrimStart() : ""),
            '%' => ("%",  raw.Length > 1 ? raw[1..].TrimStart() : ""),
            '?' => ("?",  ""),
            _   => ("",   raw),
        };
    }

    private void CycleModePrefix()
    {
        var idx  = Array.IndexOf(ModeCycle, _currentMode);
        var next = ModeCycle[(idx + 1) % ModeCycle.Length];
        ApplyModePrefix(next);
    }

    private void ClearModePrefix()
    {
        var (mode, _) = ParseInput(_searchBox.Text);
        if (!string.IsNullOrEmpty(mode))
        {
            _searchBox.Text = _searchBox.Text.Length > 1 ? _searchBox.Text[1..].TrimStart() : "";
            _searchBox.CaretIndex = _searchBox.Text.Length;
        }
    }

    private void ApplyModePrefix(string mode)
    {
        var currentQuery = _searchBox.Text;
        var (existingMode, existingQuery) = ParseInput(currentQuery);

        var newText = mode switch
        {
            "" or ">" when mode == "" => existingQuery,
            _ => mode + (string.IsNullOrEmpty(existingQuery) ? " " : " " + existingQuery)
        };
        _searchBox.TextChanged -= OnSearchTextChanged;
        _searchBox.Text         = newText;
        _searchBox.CaretIndex   = newText.Length;
        _searchBox.TextChanged += OnSearchTextChanged;

        UpdateModeTag(mode);
        _currentMode = mode;
        _ = RefreshAsync(existingQuery);
    }

    private void UpdateModeTag(string mode)
    {
        if (string.IsNullOrEmpty(mode) || mode == ">")
        {
            _modeTagBorder.Visibility = Visibility.Collapsed;
            return;
        }

        _modeTagText.Text         = mode switch
        {
            "@" => "@ Symbols",
            ":" => ": Line",
            "#" => "# Files",
            "%" => "% Grep",
            "?" => "? Help",
            _   => mode
        };
        _modeTagBorder.Visibility = Visibility.Visible;
    }

    // ── Execution ─────────────────────────────────────────────────────────────

    private void ExecuteSelected(bool closeAfter)
    {
        if (_resultsList.SelectedItem is not CommandPaletteEntry entry) return;
        if (entry.IsGroupHeader) return;

        // Mode `:` — go to line
        if (_currentMode == ":" && entry.Name.StartsWith("Go to line ", StringComparison.Ordinal))
        {
            var lineStr = entry.Name["Go to line ".Length..];
            if (int.TryParse(lineStr, out var line))
                _goToLine?.Invoke(line);
            if (closeAfter) { _closingStarted = true; Close(); }
            return;
        }

        // Mode `#` — open or activate project file (CommandParameter holds full path)
        if (_currentMode == "#" && entry.CommandParameter is string filePath)
        {
            _openAndNavigate?.Invoke(filePath, 0);
            if (closeAfter) { _closingStarted = true; Close(); }
            return;
        }

        // Mode `%` — open file and navigate to matched line (CommandParameter holds GrepMatch)
        if (entry.CommandParameter is GrepMatch gm)
        {
            _openAndNavigate?.Invoke(gm.FilePath, gm.LineNumber);
            if (closeAfter) { _closingStarted = true; Close(); }
            return;
        }

        // Normal command execution
        if (entry.Command?.CanExecute(entry.CommandParameter) == true)
        {
            _service.RecordExecution(entry.Name, _settings);
            if (closeAfter) { _closingStarted = true; Close(); }
            entry.Command.Execute(entry.CommandParameter);
        }
    }

    // ── Item container style ──────────────────────────────────────────────────

    private static Style BuildItemContainerStyle()
    {
        var style = new Style(typeof(ListBoxItem));
        style.Setters.Add(new Setter(ListBoxItem.PaddingProperty,    new Thickness(12, 5, 12, 5)));
        style.Setters.Add(new Setter(ListBoxItem.BackgroundProperty, Brushes.Transparent));

        var hoverTrigger = new Trigger { Property = ListBoxItem.IsMouseOverProperty, Value = true };
        hoverTrigger.Setters.Add(new Setter(ListBoxItem.BackgroundProperty,
            new DynamicResourceExtension("CP_HoverBrush")));
        style.Triggers.Add(hoverTrigger);

        var selectTrigger = new Trigger { Property = ListBoxItem.IsSelectedProperty, Value = true };
        selectTrigger.Setters.Add(new Setter(ListBoxItem.BackgroundProperty,
            new DynamicResourceExtension("CP_HighlightBrush")));
        style.Triggers.Add(selectTrigger);

        return style;
    }

    // ── Item template selector ────────────────────────────────────────────────

    /// <summary>Selects group-header vs regular-item template.</summary>
    private sealed class PaletteItemTemplateSelector : DataTemplateSelector
    {
        private readonly CommandPaletteWindow _host;
        private DataTemplate? _groupTemplate;
        private DataTemplate? _itemTemplate;

        public PaletteItemTemplateSelector(CommandPaletteWindow host) => _host = host;

        public override DataTemplate? SelectTemplate(object item, DependencyObject container)
        {
            if (item is CommandPaletteEntry { IsGroupHeader: true })
                return _groupTemplate ??= _host.BuildGroupHeaderTemplate();
            return _itemTemplate ??= _host.BuildItemTemplate();
        }
    }

    // ── Group-header template ─────────────────────────────────────────────────

    private DataTemplate BuildGroupHeaderTemplate()
    {
        var factory = new FrameworkElementFactory(typeof(Border));
        factory.SetResourceReference(Border.BackgroundProperty, "CP_GroupHeaderBrush");
        factory.SetValue(Border.PaddingProperty, new Thickness(12, 3, 12, 3));

        var text = new FrameworkElementFactory(typeof(TextBlock));
        text.SetBinding(TextBlock.TextProperty,
            new System.Windows.Data.Binding(nameof(CommandPaletteEntry.Name)));
        text.SetValue(TextBlock.FontSizeProperty, 10d);
        text.SetValue(TextBlock.FontWeightProperty, FontWeights.SemiBold);
        text.SetResourceReference(TextBlock.ForegroundProperty, "CP_SecondaryTextBrush");
        factory.AppendChild(text);

        return new DataTemplate { VisualTree = factory };
    }

    // ── Regular item template ─────────────────────────────────────────────────

    internal DataTemplate BuildItemTemplate()
    {
        var factory = new FrameworkElementFactory(typeof(Grid));

        var col0 = new FrameworkElementFactory(typeof(ColumnDefinition));
        col0.SetValue(ColumnDefinition.WidthProperty, GridLength.Auto);
        var col1 = new FrameworkElementFactory(typeof(ColumnDefinition));
        col1.SetValue(ColumnDefinition.WidthProperty, new GridLength(1, GridUnitType.Star));
        var col2 = new FrameworkElementFactory(typeof(ColumnDefinition));
        col2.SetValue(ColumnDefinition.WidthProperty, GridLength.Auto);
        factory.AppendChild(col0);
        factory.AppendChild(col1);
        factory.AppendChild(col2);

        // Icon (only if ShowIconGlyphs)
        if (_settings.ShowIconGlyphs)
        {
            var icon = new FrameworkElementFactory(typeof(TextBlock));
            icon.SetValue(TextBlock.FontFamilyProperty,        new FontFamily("Segoe MDL2 Assets"));
            icon.SetValue(TextBlock.FontSizeProperty,          13d);
            icon.SetValue(TextBlock.VerticalAlignmentProperty, VerticalAlignment.Center);
            icon.SetValue(TextBlock.MarginProperty,            new Thickness(0, 0, 8, 0));
            icon.SetValue(TextBlock.WidthProperty,             18d);
            icon.SetBinding(TextBlock.TextProperty,
                new System.Windows.Data.Binding(nameof(CommandPaletteEntry.IconGlyph)));
            icon.SetResourceReference(TextBlock.ForegroundProperty, "CP_SecondaryTextBrush");
            icon.SetValue(Grid.ColumnProperty, 0);
            factory.AppendChild(icon);
        }

        // Name — use ContentControl with BuildHighlightedText via converter
        var nameContent = new FrameworkElementFactory(typeof(ContentControl));
        nameContent.SetValue(Grid.ColumnProperty, 1);
        nameContent.SetValue(ContentControl.VerticalContentAlignmentProperty, VerticalAlignment.Center);
        // We use a custom binding converter to produce the highlighted TextBlock
        var nameBinding = new System.Windows.Data.Binding(".")
        {
            Converter = new NameHighlightConverter(this)
        };
        nameContent.SetBinding(ContentControl.ContentProperty, nameBinding);
        factory.AppendChild(nameContent);

        // Right stack: gesture + category
        if (_settings.ShowGestureHints)
        {
            var rightStack = new FrameworkElementFactory(typeof(StackPanel));
            rightStack.SetValue(StackPanel.OrientationProperty, Orientation.Horizontal);
            rightStack.SetValue(Grid.ColumnProperty, 2);

            var gesture = new FrameworkElementFactory(typeof(TextBlock));
            gesture.SetValue(TextBlock.FontSizeProperty,          11d);
            gesture.SetValue(TextBlock.VerticalAlignmentProperty, VerticalAlignment.Center);
            gesture.SetValue(TextBlock.MarginProperty,            new Thickness(8, 0, 4, 0));
            gesture.SetBinding(TextBlock.TextProperty,
                new System.Windows.Data.Binding(nameof(CommandPaletteEntry.GestureText)));
            gesture.SetResourceReference(TextBlock.ForegroundProperty, "CP_SecondaryTextBrush");
            rightStack.AppendChild(gesture);

            var category = new FrameworkElementFactory(typeof(TextBlock));
            category.SetValue(TextBlock.FontSizeProperty,          11d);
            category.SetValue(TextBlock.VerticalAlignmentProperty, VerticalAlignment.Center);
            category.SetValue(TextBlock.MarginProperty,            new Thickness(0, 0, 2, 0));
            category.SetBinding(TextBlock.TextProperty,
                new System.Windows.Data.Binding(nameof(CommandPaletteEntry.Category)));
            category.SetResourceReference(TextBlock.ForegroundProperty, "CP_SecondaryTextBrush");
            rightStack.AppendChild(category);

            factory.AppendChild(rightStack);
        }

        var template = new DataTemplate { VisualTree = factory };

        // Tooltip for description (mode Tooltip only)
        if (_settings.DescriptionMode == CpDescriptionMode.Tooltip)
        {
            template.Triggers.Add(BuildDescriptionTooltipTrigger());
        }

        return template;
    }

    private static DataTrigger BuildDescriptionTooltipTrigger()
    {
        // We rely on ToolTip being set inline in BuildHighlightedText / NameHighlightConverter
        // This trigger is a no-op placeholder (tooltip is set on the ContentControl directly).
        return new DataTrigger
        {
            Binding = new System.Windows.Data.Binding(nameof(CommandPaletteEntry.Description))
            { TargetNullValue = null },
            Value   = null
        };
    }

    // ── Highlight text builder ────────────────────────────────────────────────

    internal TextBlock BuildHighlightedText(CommandPaletteEntry entry)
    {
        var tb = new TextBlock
        {
            VerticalAlignment = VerticalAlignment.Center,
            FontSize          = 13
        };
        tb.SetResourceReference(TextBlock.ForegroundProperty, "CP_TextBrush");

        var name    = entry.Name;
        var indices = entry.MatchIndices;

        if (!_settings.HighlightMatchChars || indices is null || indices.Length == 0)
        {
            tb.Text = name;
        }
        else
        {
            var set = new HashSet<int>(indices);
            for (var i = 0; i < name.Length; i++)
            {
                var run = new Run(name[i].ToString());
                if (set.Contains(i))
                {
                    run.FontWeight = FontWeights.Bold;
                    run.SetResourceReference(TextElement.ForegroundProperty, "CP_MatchHighlightBrush");
                }
                tb.Inlines.Add(run);
            }
        }

        // Recent indicator
        if (entry.IsRecent)
        {
            tb.Inlines.Add(new Run(" ⟳") { FontSize = 10 });
        }

        // Tooltip
        if (_settings.DescriptionMode == CpDescriptionMode.Tooltip
            && !string.IsNullOrEmpty(entry.Description))
        {
            ToolTipService.SetToolTip(tb, entry.Description);
            ToolTipService.SetInitialShowDelay(tb, 400);
        }

        return tb;
    }

    // ── Name highlight converter ──────────────────────────────────────────────

    private sealed class NameHighlightConverter : System.Windows.Data.IValueConverter
    {
        private readonly CommandPaletteWindow _host;
        public NameHighlightConverter(CommandPaletteWindow host) => _host = host;

        public object Convert(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
            => value is CommandPaletteEntry entry
               ? _host.BuildHighlightedText(entry)
               : new TextBlock { Text = value?.ToString() ?? "" };

        public object ConvertBack(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
            => throw new NotSupportedException();
    }

    // ── Symbol kind → MDL2 glyph ─────────────────────────────────────────────

    private static string SymbolKindGlyph(string kind) => kind.ToLowerInvariant() switch
    {
        "class"         => "\uE8D2",
        "interface"     => "\uE8D2",
        "enum"          => "\uEA40",
        "function"
        or "method"     => "\uE74A",
        "variable"
        or "field"      => "\uE70F",
        "namespace"
        or "module"     => "\uE8F4",
        _               => "\uE8A5"
    };
}
