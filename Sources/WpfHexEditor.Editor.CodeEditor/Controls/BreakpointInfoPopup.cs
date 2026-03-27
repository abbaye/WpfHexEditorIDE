// ==========================================================
// Project: WpfHexEditor.Editor.CodeEditor
// File: Controls/BreakpointInfoPopup.cs
// Description:
//     VS-style popup shown when the user right-clicks the breakpoint gutter on
//     a line that has an active breakpoint. Displays the file/line location,
//     an editable condition text-box, an enable/disable toggle, and a delete button.
// Architecture:
//     Derives from Popup (StaysOpen=true, AllowsTransparency=true).
//     Grace-timer pattern identical to EndBlockHintPopup (200 ms).
//     All colours via SetResourceReference (ET_* tokens — no hardcoded values).
//     Communicates back to the App layer through IBreakpointSource — no direct
//     dependency on IDebuggerService or SDK types.
// ==========================================================

using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Effects;
using System.Windows.Threading;

namespace WpfHexEditor.Editor.CodeEditor.Controls;

/// <summary>
/// Compact VS-style popup for inspecting and editing a breakpoint.
/// Shown by <see cref="CodeEditor"/> in response to <see cref="BreakpointGutterControl.RightClickRequested"/>.
/// </summary>
internal sealed class BreakpointInfoPopup : Popup
{
    // ── Grace-timer ───────────────────────────────────────────────────────────

    private readonly DispatcherTimer _graceTimer;
    private bool _mouseInsidePopup;

    // ── Visual tree ───────────────────────────────────────────────────────────

    private readonly TextBlock _locationText;    // "file.cs : 42"
    private readonly TextBox   _conditionBox;    // editable condition expression
    private readonly Button    _enableBtn;        // "Disable" / "Enable"
    private readonly Button    _deleteBtn;
    private readonly Button    _saveBtn;

    // ── State ─────────────────────────────────────────────────────────────────

    private IBreakpointSource? _source;
    private string             _filePath = string.Empty;
    private int                _line;

    // ── Constructor ───────────────────────────────────────────────────────────

    internal BreakpointInfoPopup()
    {
        StaysOpen          = true;
        AllowsTransparency = true;
        Placement          = PlacementMode.Relative;

        _graceTimer          = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(300) };
        _graceTimer.Tick    += (_, _) => { _graceTimer.Stop(); IsOpen = false; };

        // ── Header row ────────────────────────────────────────────────────────
        var bpDot = new TextBlock
        {
            Text              = "\uEA39",   // Segoe MDL2 "Bug" glyph — red circle visual
            FontFamily        = new FontFamily("Segoe MDL2 Assets"),
            FontSize          = 11,
            VerticalAlignment = VerticalAlignment.Center,
            Margin            = new Thickness(0, 0, 5, 0),
        };
        bpDot.SetResourceReference(TextBlock.ForegroundProperty, "ET_AccentBrush");

        var titleText = new TextBlock
        {
            Text              = "Breakpoint",
            FontWeight        = FontWeights.SemiBold,
            FontSize          = 12,
            VerticalAlignment = VerticalAlignment.Center,
        };
        titleText.SetResourceReference(TextBlock.ForegroundProperty, "ET_HeaderForeground");

        _locationText = new TextBlock
        {
            FontSize          = 11,
            VerticalAlignment = VerticalAlignment.Center,
            Margin            = new Thickness(6, 0, 0, 0),
        };
        _locationText.SetResourceReference(TextBlock.ForegroundProperty, "ET_MetaForeground");

        var closeBtn = new TextBlock
        {
            Text              = "\uE711",
            FontFamily        = new FontFamily("Segoe MDL2 Assets"),
            FontSize          = 10,
            Cursor            = Cursors.Hand,
            VerticalAlignment = VerticalAlignment.Center,
            Margin            = new Thickness(8, 0, 0, 0),
        };
        closeBtn.SetResourceReference(TextBlock.ForegroundProperty, "ET_MetaForeground");
        closeBtn.MouseLeftButtonUp += (_, _) => IsOpen = false;

        var headerStack = new StackPanel
        {
            Orientation       = Orientation.Horizontal,
            Margin            = new Thickness(10, 7, 10, 5),
            VerticalAlignment = VerticalAlignment.Center,
        };
        headerStack.Children.Add(bpDot);
        headerStack.Children.Add(titleText);
        headerStack.Children.Add(_locationText);
        // Filler
        headerStack.Children.Add(new Border { HorizontalAlignment = HorizontalAlignment.Stretch, MinWidth = 20 });
        headerStack.Children.Add(closeBtn);

        var headerBorder = new Border { Padding = new Thickness(0) };
        headerBorder.SetResourceReference(Border.BackgroundProperty, "ET_HeaderBackground");
        headerBorder.Child = headerStack;

        // ── Separator ─────────────────────────────────────────────────────────
        var sep1 = new Border { Height = 1, Margin = new Thickness(0) };
        sep1.SetResourceReference(Border.BackgroundProperty, "ET_PopupBorderBrush");

        // ── Condition row ─────────────────────────────────────────────────────
        var condLabel = new TextBlock
        {
            Text              = "Condition:",
            FontSize          = 11,
            VerticalAlignment = VerticalAlignment.Center,
            Margin            = new Thickness(10, 0, 6, 0),
            MinWidth          = 65,
        };
        condLabel.SetResourceReference(TextBlock.ForegroundProperty, "ET_MetaForeground");

        _conditionBox = new TextBox
        {
            FontSize    = 11,
            MinWidth    = 180,
            MaxWidth    = 400,
            Margin      = new Thickness(0, 6, 10, 6),
            Padding     = new Thickness(4, 2, 4, 2),
            BorderThickness = new Thickness(1),
        };
        _conditionBox.SetResourceReference(TextBox.ForegroundProperty, "ET_HeaderForeground");
        _conditionBox.SetResourceReference(TextBox.BackgroundProperty, "ET_HeaderBackground");
        _conditionBox.SetResourceReference(TextBox.BorderBrushProperty, "ET_PopupBorderBrush");

        var condRow = new StackPanel
        {
            Orientation       = Orientation.Horizontal,
            VerticalAlignment = VerticalAlignment.Center,
        };
        condRow.Children.Add(condLabel);
        condRow.Children.Add(_conditionBox);

        var condBorder = new Border();
        condBorder.SetResourceReference(Border.BackgroundProperty, "ET_PopupBackground");
        condBorder.Child = condRow;

        // ── Separator ─────────────────────────────────────────────────────────
        var sep2 = new Border { Height = 1, Margin = new Thickness(0) };
        sep2.SetResourceReference(Border.BackgroundProperty, "ET_PopupBorderBrush");

        // ── Action row ────────────────────────────────────────────────────────
        _enableBtn = MakeActionButton("Disable");
        _enableBtn.Click += OnEnableToggleClicked;

        _deleteBtn = MakeActionButton("Delete");
        _deleteBtn.SetResourceReference(Button.ForegroundProperty, "ET_AccentBrush");
        _deleteBtn.Click += OnDeleteClicked;

        _saveBtn = MakeActionButton("Save");
        _saveBtn.Click += OnSaveClicked;

        var actionRow = new StackPanel
        {
            Orientation       = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Right,
            Margin            = new Thickness(10, 5, 10, 6),
        };
        actionRow.Children.Add(_enableBtn);
        actionRow.Children.Add(_deleteBtn);
        actionRow.Children.Add(_saveBtn);

        var actionBorder = new Border();
        actionBorder.SetResourceReference(Border.BackgroundProperty, "ET_PopupBackground");
        actionBorder.Child = actionRow;

        // ── Outer stack ───────────────────────────────────────────────────────
        var stack = new StackPanel { Orientation = Orientation.Vertical };
        stack.Children.Add(headerBorder);
        stack.Children.Add(sep1);
        stack.Children.Add(condBorder);
        stack.Children.Add(sep2);
        stack.Children.Add(actionBorder);

        // ── Outer border ─────────────────────────────────────────────────────
        var outerBorder = new Border
        {
            CornerRadius    = new CornerRadius(4),
            BorderThickness = new Thickness(1),
            MinWidth        = 280,
            MaxWidth        = 520,
            Child           = stack,
            Effect = new DropShadowEffect
            {
                BlurRadius  = 8,
                ShadowDepth = 2,
                Opacity     = 0.35,
                Color       = Colors.Black,
            },
        };
        outerBorder.SetResourceReference(Border.BackgroundProperty,  "ET_PopupBackground");
        outerBorder.SetResourceReference(Border.BorderBrushProperty, "ET_PopupBorderBrush");

        Child = outerBorder;

        // Mouse tracking — keep popup open while cursor is inside it.
        outerBorder.MouseEnter += (_, _) => { _mouseInsidePopup = true;  _graceTimer.Stop(); };
        outerBorder.MouseLeave += (_, _) => { _mouseInsidePopup = false; _graceTimer.Start(); };

        PreviewKeyDown += (_, e) =>
        {
            if (e.Key == Key.Escape)  { IsOpen = false; e.Handled = true; }
            if (e.Key == Key.Return)  { OnSaveClicked(null, null!); e.Handled = true; }
        };

        if (Application.Current is not null)
            Application.Current.Deactivated += OnApplicationDeactivated;
    }

    private void OnApplicationDeactivated(object? sender, EventArgs e)
        => Dispatcher.BeginInvoke(DispatcherPriority.Background, new Action(() => IsOpen = false));

    // ── Public API ────────────────────────────────────────────────────────────

    /// <summary>
    /// Opens the popup positioned relative to the breakpoint gutter for the given file/line.
    /// </summary>
    internal void Show(
        FrameworkElement   host,
        IBreakpointSource  source,
        string             filePath,
        int                line1Based,
        Point              gutterOffset,
        double             lineHeight = 0)
    {
        _source   = source;
        _filePath = filePath;
        _line     = line1Based;

        _graceTimer.Stop();
        _mouseInsidePopup = false;

        // Populate fields from current BP state.
        var info = source.GetBreakpoint(filePath, line1Based);
        _conditionBox.Text   = info?.Condition ?? string.Empty;
        _enableBtn.Content   = info?.IsEnabled == true ? "Disable" : "Enable";

        _locationText.Text   = $"  ·  {Path.GetFileName(filePath)} : {line1Based}";

        PlacementTarget  = host;
        HorizontalOffset = gutterOffset.X;
        // Position popup 2 line heights above the mouse/click position.
        VerticalOffset   = lineHeight > 0 ? gutterOffset.Y - 2 * lineHeight : gutterOffset.Y;
        IsOpen           = true;

        // Move focus into the condition box for quick editing.
        Dispatcher.BeginInvoke(DispatcherPriority.Input,
            new Action(() => { _conditionBox.Focus(); _conditionBox.SelectAll(); }));
    }

    /// <summary>Starts the grace-timer when the CodeEditor reports the mouse has left.</summary>
    internal void OnEditorMouseLeft()
    {
        if (!IsOpen || _mouseInsidePopup) return;
        _graceTimer.Stop();
        _graceTimer.Start();
    }

    internal void Dispose()
    {
        if (Application.Current is not null)
            Application.Current.Deactivated -= OnApplicationDeactivated;
    }

    // ── Button handlers ───────────────────────────────────────────────────────

    private void OnSaveClicked(object? sender, RoutedEventArgs e)
    {
        if (_source is null) return;
        var cond = _conditionBox.Text.Trim();
        _source.SetCondition(_filePath, _line, cond.Length == 0 ? null : cond);
        IsOpen = false;
    }

    private void OnEnableToggleClicked(object sender, RoutedEventArgs e)
    {
        if (_source is null) return;
        var info = _source.GetBreakpoint(_filePath, _line);
        if (info is null) return;

        bool newEnabled = !info.IsEnabled;
        _source.SetEnabled(_filePath, _line, newEnabled);
        _enableBtn.Content = newEnabled ? "Disable" : "Enable";
    }

    private void OnDeleteClicked(object sender, RoutedEventArgs e)
    {
        _source?.Delete(_filePath, _line);
        IsOpen = false;
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static Button MakeActionButton(string label)
    {
        var btn = new Button
        {
            Content         = label,
            FontSize        = 11,
            Padding         = new Thickness(10, 3, 10, 3),
            Margin          = new Thickness(0, 0, 6, 0),
            BorderThickness = new Thickness(1),
        };
        btn.SetResourceReference(Button.ForegroundProperty,   "ET_HeaderForeground");
        btn.SetResourceReference(Button.BackgroundProperty,   "ET_HeaderBackground");
        btn.SetResourceReference(Button.BorderBrushProperty,  "ET_PopupBorderBrush");
        return btn;
    }
}
