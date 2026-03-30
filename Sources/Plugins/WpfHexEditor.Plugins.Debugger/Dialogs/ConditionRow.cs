// ==========================================================
// Project: WpfHexEditor.Plugins.Debugger
// File: Dialogs/ConditionRow.cs
// Description:
//     A single condition row UI control used inside BreakpointConditionDialog.
//     Builds its visual tree entirely in code-behind (no XAML).
//     Exposes ConditionKind, ConditionMode, HitCountOp, Expression,
//     HitCountTarget, FilterExpr, and a RemoveRequested event.
// Architecture:
//     Pure WPF code-behind; all colors via SetResourceReference (DockMenu* tokens).
//     Hosted directly in BreakpointConditionDialog's conditions StackPanel.
// ==========================================================

using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using WpfHexEditor.Core.Debugger.Models;

namespace WpfHexEditor.Plugins.Debugger.Dialogs;

/// <summary>
/// A single condition row inside <see cref="BreakpointConditionDialog"/>.
/// Switches its secondary controls based on <see cref="ConditionKind"/>.
/// </summary>
internal sealed class ConditionRow : Border
{
    // ── Events ────────────────────────────────────────────────────────────────

    /// <summary>Fired when the user clicks the "Cancel" (×) link to remove this row.</summary>
    internal event Action? RemoveRequested;

    // ── Controls ──────────────────────────────────────────────────────────────

    private readonly ComboBox _kindCombo;
    private readonly ComboBox _modeCombo;       // IsTrue / WhenChanged
    private readonly ComboBox _hitOpCombo;      // = / >= / multiple of
    private readonly TextBox  _exprBox;         // conditional expression
    private readonly TextBox  _hitCountBox;     // hit count integer
    private readonly TextBox  _filterBox;       // filter expression
    private readonly TextBlock _cancelLink;
    private readonly StackPanel _secondaryPanel;

    // ── Public properties ─────────────────────────────────────────────────────

    internal BpConditionKind ConditionKind =>
        _kindCombo.SelectedIndex switch
        {
            1 => BpConditionKind.ConditionalExpression,
            2 => BpConditionKind.HitCount,
            3 => BpConditionKind.Filter,
            _ => BpConditionKind.None,
        };

    internal BpConditionMode ConditionMode =>
        _modeCombo.SelectedIndex == 1 ? BpConditionMode.WhenChanged : BpConditionMode.IsTrue;

    internal BpHitCountOp HitCountOp =>
        _hitOpCombo.SelectedIndex switch
        {
            1 => BpHitCountOp.GreaterOrEqual,
            2 => BpHitCountOp.MultipleOf,
            _ => BpHitCountOp.Equal,
        };

    internal string ConditionExpr   => _exprBox.Text.Trim();
    internal string FilterExpr      => _filterBox.Text.Trim();
    internal int    HitCountTarget  =>
        int.TryParse(_hitCountBox.Text, out int n) && n > 0 ? n : 1;

    // ── Constructor ───────────────────────────────────────────────────────────

    internal ConditionRow(bool showCancelButton)
    {
        Padding = new Thickness(0, 4, 0, 4);

        // ── Kind combo ─────────────────────────────────────────────────────
        _kindCombo = MakeComboBox(140,
            "Conditional Expression",
            "Conditional Expression",
            "Hit Count",
            "Filter");
        _kindCombo.SelectedIndex = 1; // default to ConditionalExpression
        _kindCombo.SelectionChanged += OnKindChanged;

        // ── Mode combo (ConditionalExpression) ─────────────────────────────
        _modeCombo = MakeComboBox(110, "Is true", "Is true", "When changed");
        _modeCombo.SelectedIndex = 0;

        // ── Hit-count operator combo ────────────────────────────────────────
        _hitOpCombo = MakeComboBox(130,
            "= (equals)",
            "= (equals)",
            ">= (at least)",
            "Is a multiple of");
        _hitOpCombo.SelectedIndex = 0;

        // ── Expression / count / filter boxes ──────────────────────────────
        _exprBox = MakeTextBox("Example: x == 5", minWidth: 260);
        _hitCountBox = MakeTextBox("1", minWidth: 60, maxWidth: 80);
        _hitCountBox.Text = "1";
        _filterBox = MakeTextBox("MachineName=server, ProcessId=1234", minWidth: 260);

        // ── Cancel link ────────────────────────────────────────────────────
        _cancelLink = new TextBlock
        {
            Text              = "Cancel",
            FontSize          = 11,
            TextDecorations   = TextDecorations.Underline,
            Cursor            = System.Windows.Input.Cursors.Hand,
            VerticalAlignment = VerticalAlignment.Center,
            Margin            = new Thickness(8, 0, 0, 0),
            Visibility        = showCancelButton ? Visibility.Visible : Visibility.Collapsed,
        };
        _cancelLink.SetResourceReference(TextBlock.ForegroundProperty, "ET_AccentBrush");
        _cancelLink.MouseLeftButtonUp += (_, _) => RemoveRequested?.Invoke();

        // ── Secondary panel (mode/op + expression) ──────────────────────────
        _secondaryPanel = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 6, 0, 0) };

        // ── Outer layout ────────────────────────────────────────────────────
        var row1 = new StackPanel { Orientation = Orientation.Horizontal };
        row1.Children.Add(_kindCombo);
        row1.Children.Add(_cancelLink);

        var outer = new StackPanel { Orientation = Orientation.Vertical };
        outer.Children.Add(row1);
        outer.Children.Add(_secondaryPanel);

        Child = outer;

        // initial state
        RebuildSecondary();
    }

    // ── Populate / load ───────────────────────────────────────────────────────

    internal void Load(BpConditionKind kind, BpConditionMode mode, BpHitCountOp op,
        string? expr, int hitTarget, string? filter)
    {
        _kindCombo.SelectedIndex = kind switch
        {
            BpConditionKind.ConditionalExpression => 1,
            BpConditionKind.HitCount              => 2,
            BpConditionKind.Filter                => 3,
            _                                     => 1,
        };
        _modeCombo.SelectedIndex  = mode == BpConditionMode.WhenChanged ? 1 : 0;
        _hitOpCombo.SelectedIndex = op switch
        {
            BpHitCountOp.GreaterOrEqual => 1,
            BpHitCountOp.MultipleOf     => 2,
            _                           => 0,
        };
        _exprBox.Text      = expr    ?? string.Empty;
        _hitCountBox.Text  = hitTarget > 0 ? hitTarget.ToString() : "1";
        _filterBox.Text    = filter  ?? string.Empty;
    }

    // ── Kind switch ───────────────────────────────────────────────────────────

    private void OnKindChanged(object sender, SelectionChangedEventArgs e) => RebuildSecondary();

    private void RebuildSecondary()
    {
        _secondaryPanel.Children.Clear();

        switch (ConditionKind)
        {
            case BpConditionKind.ConditionalExpression:
                _secondaryPanel.Children.Add(_modeCombo);
                _secondaryPanel.Children.Add(_exprBox);
                break;

            case BpConditionKind.HitCount:
                _secondaryPanel.Children.Add(_hitOpCombo);
                _secondaryPanel.Children.Add(_hitCountBox);
                break;

            case BpConditionKind.Filter:
                _secondaryPanel.Children.Add(_filterBox);
                break;
        }
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static ComboBox MakeComboBox(double width, string placeholder, params string[] items)
    {
        var cb = new ComboBox
        {
            Width  = width,
            Height = 22,
            FontSize = 11,
            VerticalContentAlignment = VerticalAlignment.Center,
            Margin = new Thickness(0, 0, 6, 0),
        };
        cb.SetResourceReference(Control.ForegroundProperty,   "DockMenuForegroundBrush");
        cb.SetResourceReference(Control.BackgroundProperty,   "DockMenuBackgroundBrush");
        cb.SetResourceReference(Control.BorderBrushProperty,  "DockBorderBrush");
        foreach (var item in items)
            cb.Items.Add(new ComboBoxItem { Content = item, FontSize = 11 });
        return cb;
    }

    private static TextBox MakeTextBox(string placeholder, double minWidth = 180, double maxWidth = 400)
    {
        var tb = new TextBox
        {
            MinWidth         = minWidth,
            MaxWidth         = maxWidth,
            FontSize         = 11,
            Padding          = new Thickness(4, 2, 4, 2),
            BorderThickness  = new Thickness(1),
            Tag              = placeholder, // used by placeholder adorner if any
            VerticalContentAlignment = VerticalAlignment.Center,
            Margin           = new Thickness(0, 0, 6, 0),
        };
        tb.SetResourceReference(Control.ForegroundProperty,  "DockMenuForegroundBrush");
        tb.SetResourceReference(Control.BackgroundProperty,  "DockMenuBackgroundBrush");
        tb.SetResourceReference(Control.BorderBrushProperty, "DockBorderBrush");

        // Placeholder text via GotFocus/LostFocus (lightweight pattern)
        tb.GotFocus  += (_, _) => { if (tb.Text == placeholder) { tb.Text = string.Empty; tb.SetResourceReference(Control.ForegroundProperty, "DockMenuForegroundBrush"); } };
        tb.LostFocus += (_, _) =>
        {
            if (string.IsNullOrEmpty(tb.Text))
            {
                tb.Text = placeholder;
                tb.Foreground = new SolidColorBrush(Color.FromArgb(0x80, 0x80, 0x80, 0x80));
            }
        };
        tb.Text = placeholder;
        tb.Foreground = new SolidColorBrush(Color.FromArgb(0x80, 0x80, 0x80, 0x80));

        return tb;
    }

    /// <summary>Returns raw text; if it equals placeholder returns empty.</summary>
    private static string CleanText(TextBox tb, string placeholder)
        => tb.Text == placeholder ? string.Empty : tb.Text.Trim();
}
