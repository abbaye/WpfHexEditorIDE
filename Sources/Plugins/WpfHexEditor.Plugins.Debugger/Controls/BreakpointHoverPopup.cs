// ==========================================================
// Project: WpfHexEditor.Plugins.Debugger
// File: Controls/BreakpointHoverPopup.cs
// Description:
//     VS-style hover popup shown when the user hovers a row in the BreakpointsPanel.
//     Identical visual contract to CodeEditor's BreakpointInfoPopup (ET_* tokens,
//     same layout: header / condition box / enable-toggle / delete / save).
// Architecture:
//     Derives from Popup. All colours via SetResourceReference (ET_* tokens).
//     Talks directly to IDebuggerService — no IBreakpointSource dependency.
//     Grace-timer pattern (400 ms) for comfortable list hover interaction.
// ==========================================================

using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Effects;
using System.Windows.Threading;
using WpfHexEditor.Plugins.Debugger.ViewModels;
using WpfHexEditor.SDK.Contracts.Services;

namespace WpfHexEditor.Plugins.Debugger.Controls;

/// <summary>
/// VS-style hover popup for inspecting and editing a breakpoint row in <see cref="Panels.BreakpointsPanel"/>.
/// </summary>
internal sealed class BreakpointHoverPopup : Popup
{
    // ── Grace-timer ───────────────────────────────────────────────────────────

    private readonly DispatcherTimer _graceTimer;
    private bool _mouseInsidePopup;

    // ── Visual tree ───────────────────────────────────────────────────────────

    private readonly TextBlock   _locationText;
    private readonly TextBox     _conditionBox;
    private readonly Button      _enableBtn;
    private readonly Button      _deleteBtn;
    private readonly Button      _saveBtn;
    private readonly StackPanel  _codePreviewStack;
    private readonly Border      _codePreviewBorder;
    private readonly Border      _codePreviewSep;

    // ── State ─────────────────────────────────────────────────────────────────

    private IDebuggerService? _svc;
    private string            _filePath = string.Empty;
    private int               _line;
    private bool              _isEnabled;

    // ── Constructor ───────────────────────────────────────────────────────────

    internal BreakpointHoverPopup()
    {
        StaysOpen          = true;
        AllowsTransparency = true;
        Placement          = PlacementMode.Mouse;

        _graceTimer       = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(400) };
        _graceTimer.Tick += (_, _) => { _graceTimer.Stop(); IsOpen = false; };

        // ── Header ────────────────────────────────────────────────────────────
        var bpDot = new TextBlock
        {
            Text              = "\uEA39",
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
        headerStack.Children.Add(new Border { HorizontalAlignment = HorizontalAlignment.Stretch, MinWidth = 20 });
        headerStack.Children.Add(closeBtn);

        var headerBorder = new Border { Padding = new Thickness(0) };
        headerBorder.SetResourceReference(Border.BackgroundProperty, "ET_HeaderBackground");
        headerBorder.Child = headerStack;

        // ── Separator ─────────────────────────────────────────────────────────
        var sep1 = new Border { Height = 1 };
        sep1.SetResourceReference(Border.BackgroundProperty, "ET_PopupBorderBrush");

        // ── Code preview (populated dynamically in Show()) ────────────────────
        _codePreviewStack = new StackPanel { Margin = new Thickness(0) };
        _codePreviewBorder = new Border
        {
            CornerRadius = new CornerRadius(3),
            Padding      = new Thickness(8, 5, 8, 5),
            Margin       = new Thickness(8, 6, 8, 2),
            Visibility   = Visibility.Collapsed,
            Child        = _codePreviewStack,
        };
        _codePreviewBorder.SetResourceReference(Border.BackgroundProperty, "ET_HeaderBackground");

        _codePreviewSep = new Border { Height = 1, Margin = new Thickness(0, 4, 0, 0), Visibility = Visibility.Collapsed };
        _codePreviewSep.SetResourceReference(Border.BackgroundProperty, "ET_PopupBorderBrush");

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
            FontFamily  = new FontFamily("Consolas"),
            FontSize    = 11,
            MinWidth    = 180,
            MaxWidth    = 400,
            Margin      = new Thickness(0, 6, 10, 6),
            Padding     = new Thickness(4, 2, 4, 2),
            BorderThickness = new Thickness(1),
        };
        _conditionBox.SetResourceReference(TextBox.ForegroundProperty,   "ET_HeaderForeground");
        _conditionBox.SetResourceReference(TextBox.BackgroundProperty,   "ET_HeaderBackground");
        _conditionBox.SetResourceReference(TextBox.BorderBrushProperty,  "ET_PopupBorderBrush");

        var condRow = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
        condRow.Children.Add(condLabel);
        condRow.Children.Add(_conditionBox);

        var condBorder = new Border();
        condBorder.SetResourceReference(Border.BackgroundProperty, "ET_PopupBackground");
        condBorder.Child = condRow;

        // ── Separator ─────────────────────────────────────────────────────────
        var sep2 = new Border { Height = 1 };
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
            Orientation         = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Right,
            Margin              = new Thickness(10, 5, 10, 6),
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
        stack.Children.Add(_codePreviewBorder);
        stack.Children.Add(_codePreviewSep);
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

        outerBorder.MouseEnter += (_, _) => { _mouseInsidePopup = true;  _graceTimer.Stop(); };
        outerBorder.MouseLeave += (_, _) => { _mouseInsidePopup = false; _graceTimer.Start(); };

        PreviewKeyDown += (_, e) =>
        {
            if (e.Key == Key.Escape) { IsOpen = false; e.Handled = true; }
            if (e.Key == Key.Return) { OnSaveClicked(null, null!); e.Handled = true; }
        };

        if (Application.Current is not null)
            Application.Current.Deactivated += OnApplicationDeactivated;
    }

    private void OnApplicationDeactivated(object? sender, EventArgs e)
        => Dispatcher.BeginInvoke(DispatcherPriority.Background, new Action(() => IsOpen = false));

    private void PopulateCodePreview(string filePath, int line1)
    {
        _codePreviewStack.Children.Clear();

        string[] fileLines;
        try { fileLines = File.ReadAllLines(filePath); }
        catch { fileLines = []; }

        if (fileLines.Length == 0 || line1 < 1 || line1 > fileLines.Length)
        {
            _codePreviewBorder.Visibility = Visibility.Collapsed;
            _codePreviewSep.Visibility    = Visibility.Collapsed;
            return;
        }

        int startLine = Math.Max(0, line1 - 2);              // 0-based: 1 line before BP
        int endLine   = Math.Min(fileLines.Length - 1, line1 - 1); // 0-based: the BP line

        for (int i = startLine; i <= endLine; i++)
        {
            var tb = new TextBlock
            {
                Text         = fileLines[i],
                FontFamily   = new FontFamily("Cascadia Code, Consolas, Courier New"),
                FontSize     = 11,
                TextTrimming = TextTrimming.CharacterEllipsis,
                FontWeight   = (i == line1 - 1) ? FontWeights.SemiBold : FontWeights.Normal,
            };
            tb.SetResourceReference(TextBlock.ForegroundProperty, "ET_HeaderForeground");
            _codePreviewStack.Children.Add(tb);
        }

        _codePreviewBorder.Visibility = Visibility.Visible;
        _codePreviewSep.Visibility    = Visibility.Visible;
    }

    // ── Public API ────────────────────────────────────────────────────────────

    /// <summary>Opens the popup anchored at the current mouse position for the given row.</summary>
    internal void Show(BreakpointRow row, IDebuggerService svc)
    {
        _svc      = svc;
        _filePath = row.FilePath;
        _line     = row.Line;
        _isEnabled = row.IsEnabled;

        _graceTimer.Stop();
        _mouseInsidePopup = false;

        _conditionBox.Text = row.Condition ?? string.Empty;
        _enableBtn.Content = row.IsEnabled ? "Disable" : "Enable";
        _locationText.Text = $"  ·  {Path.GetFileName(row.FilePath)} : {row.Line}";

        PopulateCodePreview(row.FilePath, row.Line);

        IsOpen = true;
    }

    /// <summary>Starts the grace-timer when the host list view reports the mouse has left.</summary>
    internal void OnHostMouseLeft()
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
        if (_svc is null) return;
        var cond = _conditionBox.Text.Trim();
        _ = _svc.UpdateBreakpointAsync(_filePath, _line, cond.Length == 0 ? null : cond, _isEnabled);
        IsOpen = false;
    }

    private void OnEnableToggleClicked(object sender, RoutedEventArgs e)
    {
        if (_svc is null) return;
        _isEnabled = !_isEnabled;
        var cond = _conditionBox.Text.Trim();
        _ = _svc.UpdateBreakpointAsync(_filePath, _line, cond.Length == 0 ? null : cond, _isEnabled);
        _enableBtn.Content = _isEnabled ? "Disable" : "Enable";
    }

    private void OnDeleteClicked(object sender, RoutedEventArgs e)
    {
        if (_svc is null) return;
        _ = _svc.ToggleBreakpointAsync(_filePath, _line);
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
        btn.SetResourceReference(Button.ForegroundProperty,  "ET_HeaderForeground");
        btn.SetResourceReference(Button.BackgroundProperty,  "ET_HeaderBackground");
        btn.SetResourceReference(Button.BorderBrushProperty, "ET_PopupBorderBrush");
        return btn;
    }
}
