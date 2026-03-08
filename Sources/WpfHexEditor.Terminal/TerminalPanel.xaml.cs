// ==========================================================
// Project: WpfHexEditor.Terminal
// File: TerminalPanel.xaml.cs
// Author: Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.6
// Created: 2026-03-06
// Description:
//     Code-behind for the VS-Like Terminal docking panel.
//     Drives the RichTextBox output area: colored lines, clickable hyperlinks,
//     auto-scroll with smart user-scroll detection, find bar, tab completion,
//     word wrap, font zoom, timestamps, pause/resume, and copy-on-select.
//
// Architecture Notes:
//     - Output is rendered into a FlowDocument (Paragraph per line, Run per segment).
//     - URLs in output are auto-detected with a Regex and wrapped in Hyperlink elements.
//     - CollectionChanged events from ViewModel.OutputLines drive incremental RichTextBox updates.
//     - A _suppressAutoScrollPause flag prevents the programmatic ScrollToEnd call from
//       triggering the "user scrolled up → pause auto-scroll" logic.
//     - FindNext / FindPrev traverse FlowDocument using TextPointer.FindText.
// ==========================================================

using System.Collections.Specialized;
using System.ComponentModel;
using System.Diagnostics;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using WpfHexEditor.SDK.UI;

namespace WpfHexEditor.Terminal;

/// <summary>
/// VS-Like dockable Terminal panel.
/// Hosts toolbar, collapsible find bar, RichTextBox output, and command input row.
/// </summary>
public sealed partial class TerminalPanel : UserControl
{
    // -- URL detection ------------------------------------------------------------

    private static readonly Regex UrlRegex = new(
        @"https?://[^\s""'<>]+|ftp://[^\s""'<>]+",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    // -- Toolbar overflow ---------------------------------------------------------

    private ToolbarOverflowManager _overflowManager = null!;

    // -- Auto-scroll state --------------------------------------------------------

    /// <summary>
    /// Set to true before programmatic ScrollToEnd calls so that
    /// the ScrollChanged handler does not mistakenly pause auto-scroll.
    /// </summary>
    private bool _suppressAutoScrollPause;

    // -- Current search position --------------------------------------------------

    private TextPointer? _lastFindPointer;

    // -- DataContext / ViewModel --------------------------------------------------

    public TerminalPanelViewModel? ViewModel => DataContext as TerminalPanelViewModel;

    private TerminalPanelViewModel? _vm;

    // -- Constructor --------------------------------------------------------------

    public TerminalPanel()
    {
        InitializeComponent();
        SetResourceReference(ForegroundProperty, "DockMenuForegroundBrush");

        DataContextChanged += OnDataContextChanged;

        // Auto-focus input box when the panel becomes visible.
        IsVisibleChanged += (_, e) =>
        {
            if ((bool)e.NewValue)
                Dispatcher.InvokeAsync(() => InputBox.Focus(), DispatcherPriority.Input);
        };

        Loaded += (_, _) =>
        {
            _overflowManager = new ToolbarOverflowManager(
                toolbarContainer:      ToolbarBorder,
                alwaysVisiblePanel:    ToolbarRightPanel,
                overflowButton:        ToolbarOverflowButton,
                overflowMenu:          OverflowContextMenu,
                groupsInCollapseOrder: new FrameworkElement[]
                {
                    TbgScrollNav,    // [0] first to collapse
                    TbgTextOptions,  // [1]
                    TbgOutputControl,// [2]
                    TbgFont,         // [3] last to collapse
                });
            Dispatcher.InvokeAsync(_overflowManager.CaptureNaturalWidths, DispatcherPriority.Loaded);
        };

        Unloaded += OnUnloaded;
    }

    // -- Lifecycle ----------------------------------------------------------------

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        DetachViewModel(_vm);
        if (DataContext is IDisposable d) d.Dispose();
        Unloaded -= OnUnloaded;
    }

    // -- DataContext wiring -------------------------------------------------------

    private void OnDataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        DetachViewModel(_vm);
        _vm = e.NewValue as TerminalPanelViewModel;
        AttachViewModel(_vm);
        RebuildOutput();
    }

    private void AttachViewModel(TerminalPanelViewModel? vm)
    {
        if (vm is null) return;
        ((INotifyCollectionChanged)vm.OutputLines).CollectionChanged += OnOutputLinesChanged;
        vm.PropertyChanged += OnViewModelPropertyChanged;
    }

    private void DetachViewModel(TerminalPanelViewModel? vm)
    {
        if (vm is null) return;
        ((INotifyCollectionChanged)vm.OutputLines).CollectionChanged -= OnOutputLinesChanged;
        vm.PropertyChanged -= OnViewModelPropertyChanged;
    }

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        switch (e.PropertyName)
        {
            case nameof(TerminalPanelViewModel.IsWordWrap):
                UpdateWordWrap();
                break;
            case nameof(TerminalPanelViewModel.ShowTimestamps):
                RebuildOutput();
                break;
        }
    }

    // -- CollectionChanged → RichTextBox ------------------------------------------

    private void OnOutputLinesChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        switch (e.Action)
        {
            case NotifyCollectionChangedAction.Add:
                if (e.NewItems is not null)
                    foreach (TerminalOutputLine line in e.NewItems)
                        AppendLineToRtb(line);
                break;

            case NotifyCollectionChangedAction.Remove:
                // Line-limit trim: remove the first paragraph (FIFO).
                if (OutputDoc.Blocks.FirstBlock is Paragraph first)
                    OutputDoc.Blocks.Remove(first);
                break;

            case NotifyCollectionChangedAction.Reset:
                OutputDoc.Blocks.Clear();
                _lastFindPointer = null;
                break;
        }
    }

    // -- Append a single line to the RichTextBox ----------------------------------

    private void AppendLineToRtb(TerminalOutputLine line)
    {
        var para = BuildParagraph(line);
        OutputDoc.Blocks.Add(para);

        if (_vm?.IsAutoScrollEnabled == true)
            ScrollLastIntoView(para);
    }

    /// <summary>
    /// Defers BringIntoView until after the layout pass so the paragraph
    /// has been measured and its position is known.
    /// </summary>
    private void ScrollLastIntoView(Paragraph para)
    {
        Dispatcher.InvokeAsync(() =>
        {
            _suppressAutoScrollPause = true;
            para.BringIntoView();
            _suppressAutoScrollPause = false;
        }, DispatcherPriority.Background);
    }

    /// <summary>
    /// Builds a <see cref="Paragraph"/> for a single output line.
    /// Text is split on URL boundaries so each URL becomes a clickable <see cref="Hyperlink"/>.
    /// An optional timestamp prefix is prepended when ShowTimestamps is enabled.
    /// </summary>
    private Paragraph BuildParagraph(TerminalOutputLine line)
    {
        var para = new Paragraph { Margin = new Thickness(0) };
        var fg = BrushForKind(line.Kind);

        if (_vm?.ShowTimestamps == true)
        {
            para.Inlines.Add(new Run($"[{line.Timestamp:HH:mm:ss}] ")
            {
                Foreground = (Brush)FindResource("Panel_ToolbarForegroundBrush"),
                FontSize = _vm?.OutputFontSize ?? 12
            });
        }

        var text = line.Text;
        var matches = UrlRegex.Matches(text);
        int cursor = 0;

        foreach (Match m in matches)
        {
            // Text before the URL
            if (m.Index > cursor)
            {
                para.Inlines.Add(new Run(text[cursor..m.Index]) { Foreground = fg });
            }

            // Hyperlink for the URL
            var uri = TryParseUri(m.Value);
            if (uri is not null)
            {
                var link = new Hyperlink(new Run(m.Value))
                {
                    NavigateUri = uri,
                    Foreground  = Brushes.DodgerBlue,
                    TextDecorations = TextDecorations.Underline
                };
                link.RequestNavigate += OnHyperlinkNavigate;
                para.Inlines.Add(link);
            }
            else
            {
                para.Inlines.Add(new Run(m.Value) { Foreground = fg });
            }

            cursor = m.Index + m.Length;
        }

        // Remaining text after last URL
        if (cursor < text.Length)
        {
            para.Inlines.Add(new Run(text[cursor..]) { Foreground = fg });
        }

        // Empty line — at least one run so the paragraph has a defined height.
        if (!para.Inlines.Any())
        {
            para.Inlines.Add(new Run(string.Empty) { Foreground = fg });
        }

        return para;
    }

    private static Uri? TryParseUri(string raw)
    {
        try { return new Uri(raw); }
        catch { return null; }
    }

    private static void OnHyperlinkNavigate(object sender, System.Windows.Navigation.RequestNavigateEventArgs e)
    {
        try { Process.Start(new ProcessStartInfo(e.Uri.AbsoluteUri) { UseShellExecute = true }); }
        catch { /* silently ignore — hyperlink best-effort */ }
        e.Handled = true;
    }

    // -- Rebuild entire output (used when ShowTimestamps toggles) -----------------

    private void RebuildOutput()
    {
        OutputDoc.Blocks.Clear();
        _lastFindPointer = null;
        if (_vm is null) return;

        foreach (var line in _vm.OutputLines)
            OutputDoc.Blocks.Add(BuildParagraph(line));

        if (_vm.IsAutoScrollEnabled && OutputDoc.Blocks.LastBlock is Paragraph last)
            ScrollLastIntoView(last);

        UpdateWordWrap();
    }

    // -- Word wrap ----------------------------------------------------------------

    private void UpdateWordWrap()
    {
        OutputDoc.PageWidth = _vm?.IsWordWrap == true
            ? double.NaN           // auto-wrap
            : double.MaxValue;     // no wrap (horizontal scroll)
    }

    // -- Brush mapping ------------------------------------------------------------

    private Brush BrushForKind(TerminalOutputKind kind)
    {
        try
        {
            return kind switch
            {
                TerminalOutputKind.Error   => (Brush)FindResource("ErrorForegroundBrush"),
                TerminalOutputKind.Warning => (Brush)FindResource("WarningForegroundBrush"),
                TerminalOutputKind.Info    => (Brush)FindResource("DockTabActiveTextBrush"),
                _                          => (Brush)FindResource("DockMenuForegroundBrush")
            };
        }
        catch
        {
            // Fallback colours when theme resource is not available.
            return kind switch
            {
                TerminalOutputKind.Error   => new SolidColorBrush(Color.FromRgb(0xEF, 0x44, 0x44)),
                TerminalOutputKind.Warning => new SolidColorBrush(Color.FromRgb(0xF5, 0x9E, 0x0B)),
                TerminalOutputKind.Info    => new SolidColorBrush(Color.FromRgb(0x60, 0xA5, 0xFA)),
                _                          => Brushes.White
            };
        }
    }

    // -- Auto-scroll / scroll events ----------------------------------------------

    private void OnOutputScrollChanged(object sender, ScrollChangedEventArgs e)
    {
        if (_suppressAutoScrollPause || _vm is null) return;

        // Use ScrollChangedEventArgs directly — sender is the RichTextBox, not the ScrollViewer.
        var scrollableHeight = e.ExtentHeight - e.ViewportHeight;

        if (e.VerticalChange < 0)
        {
            // User scrolled up → pause auto-scroll.
            _vm.IsAutoScrollEnabled = false;
        }
        else if (e.VerticalOffset >= scrollableHeight - 1)
        {
            // User scrolled back to bottom → resume auto-scroll.
            _vm.IsAutoScrollEnabled = true;
        }
    }

    // -- Toolbar button handlers --------------------------------------------------

    private void OnShellSelectorClick(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.ContextMenu is { } menu)
        {
            menu.PlacementTarget = btn;
            menu.Placement = System.Windows.Controls.Primitives.PlacementMode.Bottom;
            menu.IsOpen = true;
        }
    }

    private void OnScrollToTop(object sender, RoutedEventArgs e)
        => OutputRtb.ScrollToHome();

    private void OnScrollToBottom(object sender, RoutedEventArgs e)
    {
        if (OutputDoc.Blocks.LastBlock is Paragraph last)
            ScrollLastIntoView(last);
    }

    // -- Input box key handling ---------------------------------------------------

    private void OnInputKeyDown(object sender, KeyEventArgs e)
    {
        switch (e.Key)
        {
            case Key.Enter:
                ViewModel?.RunCommand.Execute(null);
                e.Handled = true;
                break;

            case Key.Tab:
                ViewModel?.CycleCompletion();
                // Move caret to end of text after completion.
                if (sender is TextBox tb)
                    tb.CaretIndex = tb.Text.Length;
                e.Handled = true;
                break;

            case Key.Up:
                ViewModel?.NavigateHistoryUp();
                e.Handled = true;
                break;

            case Key.Down:
                ViewModel?.NavigateHistoryDown();
                e.Handled = true;
                break;

            case Key.Escape:
                ViewModel?.CancelCommand.Execute(null);
                e.Handled = true;
                break;
        }
    }

    // -- Output box key handling --------------------------------------------------

    private void OnOutputKeyDown(object sender, KeyEventArgs e)
    {
        if (e.KeyboardDevice.Modifiers == ModifierKeys.Control)
        {
            switch (e.Key)
            {
                case Key.F:
                    ViewModel?.ToggleFindCommand.Execute(null);
                    if (_vm?.IsFindVisible == true)
                        Dispatcher.InvokeAsync(() => FindBox.Focus(), DispatcherPriority.Input);
                    e.Handled = true;
                    return;

                case Key.Add:
                case Key.OemPlus:
                    ViewModel?.IncreaseFontCommand.Execute(null);
                    e.Handled = true;
                    return;

                case Key.Subtract:
                case Key.OemMinus:
                    ViewModel?.DecreaseFontCommand.Execute(null);
                    e.Handled = true;
                    return;
            }
        }
    }

    // -- Copy on select -----------------------------------------------------------

    private void OnOutputSelectionChanged(object sender, RoutedEventArgs e)
    {
        if (_vm?.CopyOnSelect != true) return;
        var selection = OutputRtb.Selection;
        if (selection.IsEmpty) return;

        try { Clipboard.SetText(selection.Text); }
        catch { /* clipboard may be locked */ }
    }

    // -- Find bar -----------------------------------------------------------------

    private void OnCloseFindBar(object sender, RoutedEventArgs e)
    {
        if (_vm is not null) _vm.IsFindVisible = false;
    }

    private void OnFindKeyDown(object sender, KeyEventArgs e)
    {
        switch (e.Key)
        {
            case Key.Enter:
                if (e.KeyboardDevice.Modifiers == ModifierKeys.Shift)
                    FindPrev();
                else
                    FindNext();
                e.Handled = true;
                break;

            case Key.F3:
                if (e.KeyboardDevice.Modifiers == ModifierKeys.Shift)
                    FindPrev();
                else
                    FindNext();
                e.Handled = true;
                break;

            case Key.Escape:
                if (_vm is not null) _vm.IsFindVisible = false;
                e.Handled = true;
                break;
        }
    }

    private void OnFindNext(object sender, RoutedEventArgs e) => FindNext();
    private void OnFindPrev(object sender, RoutedEventArgs e) => FindPrev();

    private void FindNext()
    {
        var term = _vm?.FindText;
        if (string.IsNullOrEmpty(term)) return;

        var start = _lastFindPointer ?? OutputDoc.ContentStart;
        var found = FindTextForward(start, term);

        if (found is null)
        {
            // Wrap around from document start.
            found = FindTextForward(OutputDoc.ContentStart, term);
        }

        if (found is not null)
        {
            HighlightFound(found, term.Length);
            if (_vm is not null) _vm.FindStatusLabel = string.Empty;
        }
        else
        {
            if (_vm is not null) _vm.FindStatusLabel = "Not found";
        }
    }

    private void FindPrev()
    {
        var term = _vm?.FindText;
        if (string.IsNullOrEmpty(term)) return;

        var end = _lastFindPointer ?? OutputDoc.ContentEnd;
        var found = FindTextBackward(end, term);

        if (found is null)
        {
            // Wrap around from document end.
            found = FindTextBackward(OutputDoc.ContentEnd, term);
        }

        if (found is not null)
        {
            HighlightFound(found, term.Length);
            if (_vm is not null) _vm.FindStatusLabel = string.Empty;
        }
        else
        {
            if (_vm is not null) _vm.FindStatusLabel = "Not found";
        }
    }

    private TextPointer? FindTextForward(TextPointer start, string term)
    {
        var pointer = start;
        while (pointer is not null)
        {
            if (pointer.GetPointerContext(LogicalDirection.Forward) == TextPointerContext.Text)
            {
                var text = pointer.GetTextInRun(LogicalDirection.Forward);
                var idx  = text.IndexOf(term, StringComparison.OrdinalIgnoreCase);
                if (idx >= 0)
                {
                    _lastFindPointer = pointer.GetPositionAtOffset(idx + term.Length);
                    return pointer.GetPositionAtOffset(idx);
                }
            }
            pointer = pointer.GetNextContextPosition(LogicalDirection.Forward);
        }
        return null;
    }

    private TextPointer? FindTextBackward(TextPointer end, string term)
    {
        // Collect all text runs and their start pointers, then search backwards.
        var runs = new List<(TextPointer start, string text)>();
        var ptr  = OutputDoc.ContentStart;

        while (ptr is not null && ptr.CompareTo(end) < 0)
        {
            if (ptr.GetPointerContext(LogicalDirection.Forward) == TextPointerContext.Text)
            {
                var text = ptr.GetTextInRun(LogicalDirection.Forward);
                if (!string.IsNullOrEmpty(text))
                    runs.Add((ptr, text));
            }
            ptr = ptr.GetNextContextPosition(LogicalDirection.Forward);
        }

        for (int r = runs.Count - 1; r >= 0; r--)
        {
            var (runStart, runText) = runs[r];
            var idx = runText.LastIndexOf(term, StringComparison.OrdinalIgnoreCase);
            if (idx >= 0)
            {
                var found = runStart.GetPositionAtOffset(idx);
                _lastFindPointer = found;
                return found;
            }
        }

        return null;
    }

    private void HighlightFound(TextPointer start, int length)
    {
        var end = start.GetPositionAtOffset(length);
        if (end is null) return;

        OutputRtb.Selection.Select(start, end);
        start.Paragraph?.BringIntoView();
    }

    // ── Toolbar overflow ─────────────────────────────────────────────────────

    private void OnToolbarSizeChanged(object sender, SizeChangedEventArgs e)
    {
        if (e.WidthChanged) _overflowManager?.Update();
    }

    private void OnOverflowButtonClick(object sender, RoutedEventArgs e)
    {
        OverflowContextMenu.PlacementTarget = ToolbarOverflowButton;
        OverflowContextMenu.Placement       = System.Windows.Controls.Primitives.PlacementMode.Bottom;
        OverflowContextMenu.IsOpen          = true;
    }

    private void OnOverflowMenuOpened(object sender, RoutedEventArgs e)
    {
        OvfAutoScroll.IsChecked   = _vm?.IsAutoScrollEnabled == true;
        OvfPauseOutput.IsChecked  = _vm?.IsOutputPaused      == true;
        OvfTimestamps.IsChecked   = _vm?.ShowTimestamps       == true;
        OvfWordWrap.IsChecked     = _vm?.IsWordWrap           == true;
        OvfCopyOnSelect.IsChecked = _vm?.CopyOnSelect         == true;
        _overflowManager?.SyncMenuVisibility();
    }

    private void OvfAutoScroll_Click(object sender, RoutedEventArgs e)
    {
        if (_vm is not null) _vm.IsAutoScrollEnabled = !_vm.IsAutoScrollEnabled;
    }

    private void OvfPauseOutput_Click(object sender, RoutedEventArgs e)
    {
        if (_vm is not null) _vm.IsOutputPaused = !_vm.IsOutputPaused;
    }

    private void OvfTimestamps_Click(object sender, RoutedEventArgs e)
    {
        if (_vm is not null) _vm.ShowTimestamps = !_vm.ShowTimestamps;
    }

    private void OvfWordWrap_Click(object sender, RoutedEventArgs e)
    {
        if (_vm is not null) _vm.IsWordWrap = !_vm.IsWordWrap;
    }

    private void OvfCopyOnSelect_Click(object sender, RoutedEventArgs e)
    {
        if (_vm is not null) _vm.CopyOnSelect = !_vm.CopyOnSelect;
    }

    private void OvfFontIncrease_Click(object sender, RoutedEventArgs e)
        => _vm?.IncreaseFontCommand.Execute(null);

    private void OvfFontDecrease_Click(object sender, RoutedEventArgs e)
        => _vm?.DecreaseFontCommand.Execute(null);
}
