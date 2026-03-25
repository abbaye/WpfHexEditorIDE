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
//     Multi-tab session support: re-subscribes OutputLines when ActiveSession changes.
//
// Architecture Notes:
//     - Output is rendered into a FlowDocument (Paragraph per line, Run per segment).
//     - URLs in output are auto-detected with a Regex and wrapped in Hyperlink elements.
//     - CollectionChanged events from the *active session's* OutputLines drive incremental RichTextBox updates.
//     - When ActiveSession changes (tab switch), UnsubscribeOutputLines() + SubscribeToActiveSessionOutput()
//       + RebuildOutput() fully rewire the output area.
//     - A _suppressAutoScrollPause flag prevents the programmatic ScrollToEnd call from
//       triggering the "user scrolled up → pause auto-scroll" logic.
//     - FindNext / FindPrev traverse FlowDocument using TextPointer.FindText.
//     - ToolbarOverflowManager manages 5 groups: TbgScrollNav[0], TbgTextOptions[1],
//       TbgOutputControl[2], TbgFont[3], TbgMacro[4].
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
/// Hosts toolbar, session tab strip, collapsible find bar, RichTextBox output, and command input row.
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

    // -- Find service -------------------------------------------------------------

    private RichTextBoxFindService _findService = null!;

    // -- Session output subscription ----------------------------------------------

    /// <summary>
    /// The OutputLines collection currently subscribed via CollectionChanged.
    /// Tracked separately so we can unsubscribe cleanly on session switch.
    /// </summary>
    private INotifyCollectionChanged? _subscribedOutputLines;

    // -- DataContext / ViewModel --------------------------------------------------

    public TerminalPanelViewModel? ViewModel => DataContext as TerminalPanelViewModel;

    private TerminalPanelViewModel? _vm;

    // -- Constructor --------------------------------------------------------------

    public TerminalPanel()
    {
        InitializeComponent();
        _findService = new RichTextBoxFindService(OutputDoc, OutputRtb);
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
                    TbgFont,         // [3]
                    TbgMacro,        // [4] last to collapse
                },
                leftFixedElements: new FrameworkElement[] { ToolbarLeftFixedPanel });
            Dispatcher.InvokeAsync(_overflowManager.CaptureNaturalWidths, DispatcherPriority.Loaded);
        };

        Unloaded += OnUnloaded;
        Loaded   += OnLoaded;
    }

    // -- Lifecycle ----------------------------------------------------------------

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        // Only detach event subscriptions — do NOT dispose and do NOT remove this handler.
        // The docking system fires Unloaded on every visual-tree rebuild, not only on permanent close.
        // Disposal is handled by OnDataContextChanged when the DataContext is replaced.
        DetachViewModel(_vm);
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        // Re-attach after a dock visual-tree rebuild (Unloaded fired but ViewModel was NOT disposed).
        if (_vm is null) return;
        AttachViewModel(_vm);
        SubscribeToActiveSessionOutput();
        RebuildOutput();
    }

    // -- DataContext wiring -------------------------------------------------------

    private void OnDataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        DetachViewModel(_vm);
        if (e.OldValue is IDisposable old) old.Dispose(); // Dispose old VM when DC is replaced (permanent close)
        _vm = e.NewValue as TerminalPanelViewModel;
        AttachViewModel(_vm);
        SubscribeToActiveSessionOutput();
        RebuildOutput();
    }

    private void AttachViewModel(TerminalPanelViewModel? vm)
    {
        if (vm is null) return;
        vm.PropertyChanged -= OnViewModelPropertyChanged; // guard: prevent duplicate on repeated Loaded
        vm.PropertyChanged += OnViewModelPropertyChanged;
    }

    private void DetachViewModel(TerminalPanelViewModel? vm)
    {
        if (vm is null) return;
        vm.PropertyChanged -= OnViewModelPropertyChanged;
        UnsubscribeOutputLines();
    }

    // -- Session output subscription management -----------------------------------

    /// <summary>
    /// Subscribes to the active session's OutputLines CollectionChanged.
    /// Unsubscribes from any previously-subscribed collection first.
    /// </summary>
    private void SubscribeToActiveSessionOutput()
    {
        UnsubscribeOutputLines();

        if (_vm?.OutputLines is INotifyCollectionChanged newCollection)
        {
            _subscribedOutputLines = newCollection;
            _subscribedOutputLines.CollectionChanged += OnOutputLinesChanged;
        }
    }

    private void UnsubscribeOutputLines()
    {
        if (_subscribedOutputLines is not null)
        {
            _subscribedOutputLines.CollectionChanged -= OnOutputLinesChanged;
            _subscribedOutputLines = null;
        }
    }

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        switch (e.PropertyName)
        {
            case nameof(TerminalPanelViewModel.OutputLines):
                // Active session changed — rewire collection subscription and rebuild output.
                SubscribeToActiveSessionOutput();
                RebuildOutput();
                break;

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
                _findService.Reset();
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

    // -- Rebuild entire output (used when ShowTimestamps toggles or session switches) ----

    private void RebuildOutput()
    {
        OutputDoc.Blocks.Clear();
        _findService.Reset();
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

    // -- Session tab strip handlers -----------------------------------------------

    /// <summary>
    /// Handles tab selection change from the session ListBox tab strip.
    /// The VM's ActiveSession binding handles the actual switch; this handler focuses the input.
    /// </summary>
    private void OnSessionTabSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        Dispatcher.InvokeAsync(() => InputBox.Focus(), DispatcherPriority.Input);
    }

    /// <summary>
    /// Handles the close "×" button on a session tab.
    /// The Tag of the button is the ShellSessionViewModel to close.
    /// </summary>
    private void OnCloseSessionTabClick(object sender, RoutedEventArgs e)
    {
        if (sender is Button { Tag: ShellSessionViewModel session })
            _vm?.CloseSessionCommand.Execute(session.Session.Id);

        e.Handled = true; // prevent tab selection change on close click
    }

    /// <summary>
    /// Click handler for the "+" new-session button.
    /// Opens the shell-type selection context menu below the button.
    /// The context menu items use direct Command bindings, so no additional
    /// dispatch logic is needed here.
    /// </summary>
    private void OnNewSessionMenuClick(object sender, RoutedEventArgs e)
    {
        if (sender is not Button btn || btn.ContextMenu is not { } menu) return;

        menu.PlacementTarget = btn;
        menu.Placement       = System.Windows.Controls.Primitives.PlacementMode.Bottom;
        menu.IsOpen          = true;
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

    // -- "+" new session button ---------------------------------------------------

    private void OnNewSessionButtonClick(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.ContextMenu is { } menu)
        {
            menu.PlacementTarget = btn;
            menu.Placement = System.Windows.Controls.Primitives.PlacementMode.Bottom;
            menu.IsOpen = true;
        }
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
        if (_vm is not null)
            _vm.FindStatusLabel = _findService.FindNext(term) ? string.Empty : "Not found";
    }

    private void FindPrev()
    {
        var term = _vm?.FindText;
        if (string.IsNullOrEmpty(term)) return;
        if (_vm is not null)
            _vm.FindStatusLabel = _findService.FindPrev(term) ? string.Empty : "Not found";
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
        OvfRecord.IsChecked       = _vm?.IsRecording          == true;
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

    // -- Macro overflow handlers --------------------------------------------------

    private void OvfRecord_Click(object sender, RoutedEventArgs e)
        => _vm?.StartRecordingCommand.Execute(null);

    private void OvfStop_Click(object sender, RoutedEventArgs e)
        => _vm?.StopRecordingCommand.Execute(null);

    private void OvfReplay_Click(object sender, RoutedEventArgs e)
        => _vm?.ReplayMacroCommand.Execute(null);

    private void OvfSaveMacro_Click(object sender, RoutedEventArgs e)
        => _vm?.SaveMacroCommand.Execute(null);
}
