// ==========================================================
// Project: WpfHexEditor.Plugins.Debugger
// File: Dialogs/BreakpointConditionDialog.xaml.cs
// Description:
//     VS-style "Breakpoint Settings" dialog.  Exposes Conditions (conditional
//     expression, hit count, filter), Actions (log message + continue), and
//     Options (disable-once-hit, depends-on).
//     Accessible from the Breakpoint Explorer panel and from the CodeEditor
//     gutter right-click → compact popup → Settings button.
// Architecture:
//     Inherits ThemedDialog; XAML handles static structure, code-behind
//     manages dynamic condition rows (max 2, like VS).
//     All state is written to Result on Close — no OK/Cancel pair (VS pattern).
// ==========================================================

using System.IO;
using System.Windows;
using System.Windows.Controls;
using WpfHexEditor.Core.Debugger.Models;
using WpfHexEditor.Editor.Core.Views;

namespace WpfHexEditor.Plugins.Debugger.Dialogs;

/// <summary>
/// VS2022-style "Breakpoint Settings" dialog for editing the full condition,
/// action, and options of a single breakpoint.
/// </summary>
public partial class BreakpointConditionDialog : ThemedDialog
{
    // ── Condition rows (max 2) ────────────────────────────────────────────────

    private readonly List<ConditionRow> _condRows = [];
    private const int MaxConditionRows = 2;

    // ── Original state for Reset ──────────────────────────────────────────────

    private BreakpointLocation?                _originalBp;
    private IReadOnlyList<BreakpointLocation>? _allBps;

    // ── Log message placeholder ───────────────────────────────────────────────

    private const string LogPlaceholder = "Example: $FUNCTION : value of x = {x}";
    private bool _logBoxHasRealText;

    // ── Output ────────────────────────────────────────────────────────────────

    /// <summary>
    /// Settings captured when the user clicks Close.
    /// <c>null</c> when the dialog was closed via the title-bar [×] without clicking Close.
    /// </summary>
    public BreakpointSettings? Result { get; private set; }

    private bool _closedByButton;

    // ── Constructor ───────────────────────────────────────────────────────────

    public BreakpointConditionDialog()
    {
        InitializeComponent();
        InitLogPlaceholder();
        Closed += (_, _) => { if (!_closedByButton) Result = null; };
    }

    // ── Factory ───────────────────────────────────────────────────────────────

    /// <summary>
    /// Opens the dialog populated with <paramref name="bp"/>'s current settings.
    /// Returns the captured <see cref="BreakpointSettings"/> or <c>null</c> if the
    /// user dismissed via the title-bar close button without clicking Close.
    /// </summary>
    public static BreakpointSettings? Show(
        Window                            owner,
        BreakpointLocation                bp,
        IReadOnlyList<BreakpointLocation> allBps)
    {
        var dlg = new BreakpointConditionDialog { Owner = owner };
        dlg.Populate(bp, allBps);
        dlg.ShowDialog();
        return dlg.Result;
    }

    // ── Populate ──────────────────────────────────────────────────────────────

    private void Populate(BreakpointLocation bp, IReadOnlyList<BreakpointLocation> allBps)
    {
        _originalBp = bp;
        _allBps     = allBps;
        LocationText.Text = $"{Path.GetFileName(bp.FilePath)} : line {bp.Line}, char {bp.Column}";

        // ── Conditions ───────────────────────────────────────────────────────
        bool hasCondition = bp.ConditionKind != BpConditionKind.None
                         || !string.IsNullOrEmpty(bp.Condition);
        ConditionsCheck.IsChecked = hasCondition;

        if (hasCondition)
        {
            var row = AddConditionRow(isFirst: true);
            // Prefer explicit ConditionKind; fall back to ConditionalExpression when Condition string present
            var kind = bp.ConditionKind != BpConditionKind.None
                ? bp.ConditionKind
                : BpConditionKind.ConditionalExpression;
            row.Load(kind, bp.ConditionMode, bp.HitCountOp,
                bp.Condition.Length > 0 ? bp.Condition : null,
                bp.HitCountTarget, bp.FilterExpr);
        }

        // ── Actions ──────────────────────────────────────────────────────────
        ActionsCheck.IsChecked  = bp.HasAction;
        ContinueCheck.IsChecked = bp.ContinueExecution;
        if (bp.HasAction && !string.IsNullOrEmpty(bp.LogMessage))
        {
            LogMessageBox.Text      = bp.LogMessage;
            _logBoxHasRealText      = true;
            SetLogBoxRealStyle();
        }

        // ── Options ──────────────────────────────────────────────────────────
        DisableOnceCheck.IsChecked = bp.DisableOnceHit;

        // Populate depends-on combo with all other BPs
        foreach (var other in allBps.Where(b => !(
            string.Equals(b.FilePath, bp.FilePath, StringComparison.OrdinalIgnoreCase) &&
            b.Line == bp.Line)))
        {
            var key = $"{Path.GetFileName(other.FilePath)}:{other.Line}";
            DependsOnCombo.Items.Add(new ComboBoxItem
            {
                Content = key,
                Tag     = $"{other.FilePath}:{other.Line}",
            });
        }

        if (!string.IsNullOrEmpty(bp.DependsOnBpKey))
        {
            DependsOnCheck.IsChecked = true;
            // Select matching item
            foreach (ComboBoxItem item in DependsOnCombo.Items)
            {
                if (item.Tag?.ToString() == bp.DependsOnBpKey)
                {
                    DependsOnCombo.SelectedItem = item;
                    break;
                }
            }
        }

        UpdateConditionSectionVisibility();
        UpdateActionSectionVisibility();
    }

    // ── Condition row management ──────────────────────────────────────────────

    private ConditionRow AddConditionRow(bool isFirst)
    {
        var row = new ConditionRow(showCancelButton: !isFirst) { Margin = new Thickness(0, 0, 0, 6) };

        if (!isFirst)
        {
            row.RemoveRequested += () =>
            {
                _condRows.Remove(row);
                ConditionRowsPanel.Children.Remove(row);
                UpdateAddConditionLinkVisibility();
            };
        }

        _condRows.Add(row);
        ConditionRowsPanel.Children.Add(row);
        UpdateAddConditionLinkVisibility();
        return row;
    }

    private void UpdateAddConditionLinkVisibility()
        => AddConditionLink.Visibility =
            (_condRows.Count < MaxConditionRows && ConditionRowsPanel.Visibility == Visibility.Visible)
            ? Visibility.Visible
            : Visibility.Collapsed;

    private void UpdateConditionSectionVisibility()
    {
        bool show = ConditionsCheck.IsChecked == true;
        ConditionRowsPanel.Visibility = show ? Visibility.Visible : Visibility.Collapsed;
        UpdateAddConditionLinkVisibility();

        if (show && _condRows.Count == 0)
            AddConditionRow(isFirst: true);
    }

    private void UpdateActionSectionVisibility()
        => ActionsContent.Visibility = ActionsCheck.IsChecked == true
            ? Visibility.Visible : Visibility.Collapsed;

    // ── Event handlers ────────────────────────────────────────────────────────

    private void OnConditionsCheckedChanged(object sender, RoutedEventArgs e)
        => UpdateConditionSectionVisibility();

    private void OnActionsCheckedChanged(object sender, RoutedEventArgs e)
        => UpdateActionSectionVisibility();

    private void OnAddConditionClicked(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        if (_condRows.Count < MaxConditionRows)
            AddConditionRow(isFirst: false);
    }

    private void OnDependsOnCheckedChanged(object sender, RoutedEventArgs e)
        => DependsOnCombo.IsEnabled = DependsOnCheck.IsChecked == true;

    private void OnResetClicked(object sender, RoutedEventArgs e)
    {
        if (_originalBp is null || _allBps is null) return;
        _condRows.Clear();
        ConditionRowsPanel.Children.Clear();
        DependsOnCombo.Items.Clear();
        _logBoxHasRealText = false;
        InitLogPlaceholder();
        Populate(_originalBp, _allBps);
    }

    private void OnCloseClicked(object sender, RoutedEventArgs e)
    {
        _closedByButton = true;
        Result          = BuildResult();
        Close();
    }

    private void OnLogHintClicked(object sender, RoutedEventArgs e)
    {
        MessageBox.Show(
            "Supported interpolations:\n" +
            "  $FUNCTION  — method or property name\n" +
            "  $ADDRESS   — instruction address\n" +
            "  $CALLER    — caller frame name\n" +
            "  {expr}     — value of any expression, e.g. {x + 1}\n\n" +
            "Newlines: use \\n\n" +
            "Curly braces: use {{ and }}",
            "Log Message Format",
            MessageBoxButton.OK,
            MessageBoxImage.Information);
    }

    // ── Log message placeholder ───────────────────────────────────────────────

    private void InitLogPlaceholder()
    {
        LogMessageBox.Text       = LogPlaceholder;
        LogMessageBox.Foreground = System.Windows.Media.Brushes.Gray;
        _logBoxHasRealText       = false;
    }

    private void OnLogBoxGotFocus(object sender, RoutedEventArgs e)
    {
        if (!_logBoxHasRealText)
        {
            LogMessageBox.Text = string.Empty;
            SetLogBoxRealStyle();
        }
    }

    private void OnLogBoxLostFocus(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(LogMessageBox.Text))
        {
            _logBoxHasRealText = false;
            InitLogPlaceholder();
        }
        else
        {
            _logBoxHasRealText = true;
        }
    }

    private void SetLogBoxRealStyle()
        => LogMessageBox.SetResourceReference(
            System.Windows.Controls.TextBox.ForegroundProperty, "DockMenuForegroundBrush");

    // ── Build result ──────────────────────────────────────────────────────────

    private BreakpointSettings BuildResult()
    {
        // ── Condition ────────────────────────────────────────────────────────
        var condKind   = BpConditionKind.None;
        string? condExpr = null;
        var condMode   = BpConditionMode.IsTrue;
        var hitOp      = BpHitCountOp.Equal;
        int hitTarget  = 1;
        string? filter = null;

        if (ConditionsCheck.IsChecked == true && _condRows.Count > 0)
        {
            var primary = _condRows[0];
            condKind  = primary.ConditionKind;
            condMode  = primary.ConditionMode;
            hitOp     = primary.HitCountOp;
            hitTarget = primary.HitCountTarget;

            condExpr = condKind == BpConditionKind.ConditionalExpression
                ? (primary.ConditionExpr.Length > 0 ? primary.ConditionExpr : null)
                : null;

            filter = condKind == BpConditionKind.Filter
                ? (primary.FilterExpr.Length > 0 ? primary.FilterExpr : null)
                : null;
        }

        // ── Action ───────────────────────────────────────────────────────────
        bool hasAction = ActionsCheck.IsChecked == true;
        string? logMsg = hasAction && _logBoxHasRealText
            ? LogMessageBox.Text.Trim()
            : null;
        bool continueExec = ContinueCheck.IsChecked != false;

        // ── Options ──────────────────────────────────────────────────────────
        bool disableOnce = DisableOnceCheck.IsChecked == true;
        string? dependsOn = null;
        if (DependsOnCheck.IsChecked == true && DependsOnCombo.SelectedItem is ComboBoxItem item)
            dependsOn = item.Tag?.ToString();

        return new BreakpointSettings(
            ConditionKind:     condKind,
            ConditionExpr:     condExpr,
            ConditionMode:     condMode,
            HitCountOp:        hitOp,
            HitCountTarget:    hitTarget,
            FilterExpr:        filter,
            HasAction:         hasAction && logMsg is not null,
            LogMessage:        logMsg,
            ContinueExecution: continueExec,
            DisableOnceHit:    disableOnce,
            DependsOnBpKey:    dependsOn
        );
    }
}
