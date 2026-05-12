// ==========================================================
// Project: WpfHexEditor.App
// File: Analysis/UI/Options/CodeAnalysisOptionsPage.cs
// Description: IDE options page for Code Analysis — 3 sections:
//              General, Thresholds, Rules.
//              Code-behind-only UserControl implementing IOptionsPage.
//              Registered via OptionsPageRegistry.RegisterDynamic.
// ==========================================================

using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using WpfHexEditor.App.Analysis.Models;
using WpfHexEditor.App.Analysis.Services;
using WpfHexEditor.Core.Options;

namespace WpfHexEditor.App.Analysis.UI.Options;

public sealed class CodeAnalysisOptionsPage : UserControl, IOptionsPage
{
    public event EventHandler? Changed;

    private readonly CodeAnalysisOptionsService _service;
    private bool _loading;

    // General
    private readonly CheckBox  _runOnOpen;
    private readonly CheckBox  _runOnBuild;
    private readonly CheckBox  _showBadge;
    private readonly CheckBox  _pushToPanel;
    private readonly CheckBox  _includeGenerated;
    private readonly ComboBox  _retention;
    private readonly ComboBox  _verbosity;

    // Thresholds
    private readonly TextBox _ccWarn;   private readonly TextBox _ccError;
    private readonly TextBox _cogWarn;  private readonly TextBox _cogError;
    private readonly TextBox _mLocWarn; private readonly TextBox _mLocError;
    private readonly TextBox _fLocWarn; private readonly TextBox _fLocError;
    private readonly TextBox _dupTok;
    private readonly TextBox _dupWarn;  private readonly TextBox _dupError;
    private readonly TextBox _parWarn;  private readonly TextBox _parError;
    private readonly TextBox _ditWarn;  private readonly TextBox _ditError;

    // Rules — store ruleId (identifier) not the config object
    private readonly List<(string ruleId, CheckBox chk, ComboBox sev)> _ruleRows = [];

    internal CodeAnalysisOptionsPage(CodeAnalysisOptionsService service)
    {
        _service = service;

        Resources.MergedDictionaries.Add(new ResourceDictionary
        {
            Source = new Uri("pack://application:,,,/WpfHexEditor.App;component/Themes/DialogStyles.xaml")
        });

        Padding = new Thickness(16);

        var scroll = new ScrollViewer
        {
            VerticalScrollBarVisibility   = ScrollBarVisibility.Auto,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
        };
        Content = scroll;

        var root = new StackPanel();
        scroll.Content = root;

        // ── General ─────────────────────────────────────────────────────────
        root.Children.Add(SectionHeader("GENERAL"));

        _runOnOpen       = Chk("Run analysis automatically on solution open");
        _runOnBuild      = Chk("Run analysis automatically on build");
        _showBadge       = Chk("Show score badge in status bar");
        _pushToPanel     = Chk("Push diagnostics to Error Panel");
        _includeGenerated = Chk("Include generated files (*.g.cs, *.Designer.cs)");

        root.Children.Add(_runOnOpen);
        root.Children.Add(_runOnBuild);
        root.Children.Add(_showBadge);
        root.Children.Add(_pushToPanel);
        root.Children.Add(_includeGenerated);

        root.Children.Add(LabeledCombo("Snapshot retention:", out _retention,
            "7 days", "14 days", "30 days", "60 days", "90 days"));
        root.Children.Add(LabeledCombo("Output verbosity:", out _verbosity,
            "Silent", "Normal", "Verbose"));

        // ── Thresholds ───────────────────────────────────────────────────────
        root.Children.Add(SectionHeader("THRESHOLDS"));

        root.Children.Add(ThresholdRow("Cyclomatic Complexity:",   out _ccWarn,   out _ccError));
        root.Children.Add(ThresholdRow("Cognitive Complexity:",    out _cogWarn,  out _cogError));
        root.Children.Add(ThresholdRow("Method LOC:",              out _mLocWarn, out _mLocError));
        root.Children.Add(ThresholdRow("File LOC:",                out _fLocWarn, out _fLocError));
        root.Children.Add(SingleRow("Duplication min tokens:",    out _dupTok));
        root.Children.Add(ThresholdRow("Duplication %:",           out _dupWarn,  out _dupError));
        root.Children.Add(ThresholdRow("Max parameters:",          out _parWarn,  out _parError));
        root.Children.Add(ThresholdRow("Inheritance depth (DIT):", out _ditWarn,  out _ditError));

        // ── Rules ────────────────────────────────────────────────────────────
        root.Children.Add(SectionHeader("RULES"));

        var filterBox = new TextBox
        {
            Margin              = new Thickness(0, 4, 0, 8),
            ToolTip             = "Filter rules…",
            HorizontalAlignment = HorizontalAlignment.Stretch,
        };
        filterBox.TextChanged += (_, _) => FilterRules(filterBox.Text);
        root.Children.Add(filterBox);

        var rulesPanel = new StackPanel { Tag = "RulesPanel" };
        root.Children.Add(rulesPanel);

        // Phase 11 — group rules by RuleCategory inside Expanders so the user
        // can scan / bulk-toggle a whole category quickly.
        foreach (var group in CodeAnalysisOptions.DefaultRules().GroupBy(r => r.Category))
        {
            var categoryPanel = new StackPanel();
            var expander = new Expander
            {
                Header     = CategoryHeaderFor(group.Key, group.Count()),
                IsExpanded = true,
                Margin     = new Thickness(0, 4, 0, 4),
                Content    = categoryPanel,
            };
            // WPF's default Expander header ignores inherited Foreground — set explicitly
            // so the label follows the dock theme instead of going black on dark surfaces.
            expander.SetResourceReference(Control.ForegroundProperty, "DockMenuForegroundBrush");
            expander.SetResourceReference(Control.BorderBrushProperty, "DockBorderBrush");
            TextElement.SetForeground(expander, (Brush)Application.Current.FindResource("DockMenuForegroundBrush"));

            foreach (var rule in group)
                categoryPanel.Children.Add(BuildRuleRow(rule));

            rulesPanel.Children.Add(expander);
        }

        // ── Reset button ─────────────────────────────────────────────────────
        var reset = new Button
        {
            Content             = "Restore Defaults",
            HorizontalAlignment = HorizontalAlignment.Left,
            Margin              = new Thickness(0, 16, 0, 0),
            Padding             = new Thickness(12, 4, 12, 4),
        };
        reset.Click += (_, _) => ApplyToUi(new CodeAnalysisOptions());
        root.Children.Add(reset);

        // Wire change events
        foreach (var ctrl in new Control[] { _runOnOpen, _runOnBuild, _showBadge, _pushToPanel, _includeGenerated })
            if (ctrl is CheckBox cb)
            {
                cb.Checked   += OnChanged;
                cb.Unchecked += OnChanged;
            }
        _retention.SelectionChanged += (_, _) => { if (!_loading) Changed?.Invoke(this, EventArgs.Empty); };
        _verbosity.SelectionChanged += (_, _) => { if (!_loading) Changed?.Invoke(this, EventArgs.Empty); };

        foreach (var tb in new[] { _ccWarn, _ccError, _cogWarn, _cogError, _mLocWarn, _mLocError,
                                    _fLocWarn, _fLocError, _dupTok, _dupWarn, _dupError,
                                    _parWarn, _parError, _ditWarn, _ditError })
            tb.TextChanged += (_, _) => { if (!_loading) Changed?.Invoke(this, EventArgs.Empty); };
    }

    // ── IOptionsPage ─────────────────────────────────────────────────────────

    public void Load(AppSettings _settings)
    {
        _service.Load();
        ApplyToUi(_service.Options);
    }

    public void Flush(AppSettings _settings)
    {
        var o = _service.Options;
        o.RunOnSolutionOpen     = _runOnOpen.IsChecked      == true;
        o.RunOnBuild            = _runOnBuild.IsChecked     == true;
        o.ShowStatusBarBadge    = _showBadge.IsChecked      == true;
        o.PushToErrorPanel      = _pushToPanel.IsChecked    == true;
        o.IncludeGeneratedFiles = _includeGenerated.IsChecked == true;
        o.SnapshotRetentionDays = ParseRetention(_retention.SelectedItem?.ToString() ?? "30 days");
        o.OutputVerbosity       = _verbosity.SelectedItem?.ToString() ?? "Normal";

        o.CcWarning       = Int("10", _ccWarn);    o.CcError       = Int("20", _ccError);
        o.CognitiveWarning = Int("15", _cogWarn);  o.CognitiveError = Int("30", _cogError);
        o.MethodLocWarning = Int("25", _mLocWarn); o.MethodLocError = Int("50", _mLocError);
        o.FileLocWarning   = Int("300", _fLocWarn); o.FileLocError  = Int("600", _fLocError);
        o.DupMinTokens     = Int("50", _dupTok);
        o.DupPercentWarning = Int("5", _dupWarn);  o.DupPercentError = Int("15", _dupError);
        o.MaxParamsWarning  = Int("5", _parWarn);  o.MaxParamsError  = Int("8", _parError);
        o.DitWarning        = Int("4", _ditWarn);  o.DitError        = Int("7", _ditError);

        foreach (var (ruleId, chk, sev) in _ruleRows)
        {
            var target = o.GetRule(ruleId);
            if (target is null) continue;
            target.Severity = chk.IsChecked == true
                ? Enum.Parse<RuleSeverity>(sev.SelectedItem?.ToString() ?? "Warning", ignoreCase: true)
                : RuleSeverity.Disabled;
        }

        _service.Save();
    }

    // ── Private ──────────────────────────────────────────────────────────────

    private void ApplyToUi(CodeAnalysisOptions o)
    {
        _loading = true;
        try
        {
            _runOnOpen.IsChecked      = o.RunOnSolutionOpen;
            _runOnBuild.IsChecked     = o.RunOnBuild;
            _showBadge.IsChecked      = o.ShowStatusBarBadge;
            _pushToPanel.IsChecked    = o.PushToErrorPanel;
            _includeGenerated.IsChecked = o.IncludeGeneratedFiles;
            _retention.SelectedItem   = $"{o.SnapshotRetentionDays} days";
            _verbosity.SelectedItem   = o.OutputVerbosity;

            _ccWarn.Text   = o.CcWarning.ToString();   _ccError.Text   = o.CcError.ToString();
            _cogWarn.Text  = o.CognitiveWarning.ToString(); _cogError.Text  = o.CognitiveError.ToString();
            _mLocWarn.Text = o.MethodLocWarning.ToString(); _mLocError.Text = o.MethodLocError.ToString();
            _fLocWarn.Text = o.FileLocWarning.ToString();   _fLocError.Text = o.FileLocError.ToString();
            _dupTok.Text   = o.DupMinTokens.ToString();
            _dupWarn.Text  = o.DupPercentWarning.ToString(); _dupError.Text  = o.DupPercentError.ToString();
            _parWarn.Text  = o.MaxParamsWarning.ToString();  _parError.Text  = o.MaxParamsError.ToString();
            _ditWarn.Text  = o.DitWarning.ToString();        _ditError.Text  = o.DitError.ToString();

            foreach (var (ruleId, chk, sev) in _ruleRows)
            {
                var cfg      = o.GetRule(ruleId);
                var severity = cfg?.Severity ?? RuleSeverity.Warning;
                chk.IsChecked    = severity != RuleSeverity.Disabled;
                sev.SelectedItem = severity == RuleSeverity.Disabled ? "Info" : severity.ToString();
            }
        }
        finally { _loading = false; }
    }

    private void FilterRules(string text)
    {
        // Find the rules panel
        if (Content is not ScrollViewer sv || sv.Content is not StackPanel root) return;
        StackPanel? rulesPanel = null;
        foreach (UIElement child in root.Children)
            if (child is StackPanel sp && "RulesPanel".Equals(sp.Tag)) { rulesPanel = sp; break; }
        if (rulesPanel is null) return;

        // Children are Expanders (Phase 11). Walk Expander.Content (StackPanel of rows)
        // and apply the filter per-row; collapse the Expander when every row is hidden.
        foreach (UIElement child in rulesPanel.Children)
        {
            if (child is not Expander exp || exp.Content is not StackPanel rows) continue;
            int visibleInGroup = 0;
            foreach (UIElement rowEl in rows.Children)
            {
                if (rowEl is not DockPanel row || row.Children.Count == 0 || row.Children[1] is not CheckBox cb) continue;
                bool show = string.IsNullOrWhiteSpace(text)
                         || cb.Content?.ToString()?.Contains(text, StringComparison.OrdinalIgnoreCase) == true;
                row.Visibility = show ? Visibility.Visible : Visibility.Collapsed;
                if (show) visibleInGroup++;
            }
            exp.Visibility = visibleInGroup > 0 ? Visibility.Visible : Visibility.Collapsed;
        }
    }

    private void OnChanged(object sender, RoutedEventArgs e)
    {
        if (!_loading) Changed?.Invoke(this, EventArgs.Empty);
    }

    // ── Builders ─────────────────────────────────────────────────────────────

    private static TextBlock SectionHeader(string text) => new()
    {
        Text       = text,
        FontWeight = FontWeights.SemiBold,
        Margin     = new Thickness(0, 16, 0, 6),
    };

    private static CheckBox Chk(string label) => new()
    {
        Content = label,
        Margin  = new Thickness(0, 3, 0, 3),
    };

    private static FrameworkElement LabeledCombo(string label, out ComboBox combo, params string[] items)
    {
        var row  = new DockPanel { Margin = new Thickness(0, 4, 0, 4) };
        var lbl  = new TextBlock { Text = label, Width = 180, VerticalAlignment = VerticalAlignment.Center };
        combo    = new ComboBox { Width = 120, HorizontalAlignment = HorizontalAlignment.Left };
        foreach (var i in items) combo.Items.Add(i);
        if (items.Length > 0) combo.SelectedIndex = 0;
        DockPanel.SetDock(lbl, Dock.Left);
        row.Children.Add(lbl);
        row.Children.Add(combo);
        return row;
    }

    private static FrameworkElement ThresholdRow(string label, out TextBox warn, out TextBox error)
    {
        var row  = new DockPanel { Margin = new Thickness(0, 3, 0, 3) };
        var lbl  = new TextBlock { Text = label, Width = 200, VerticalAlignment = VerticalAlignment.Center };
        var wLbl = new TextBlock { Text = " warn ", VerticalAlignment = VerticalAlignment.Center };
        var eLbl = new TextBlock { Text = " error ", VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(8,0,0,0) };
        warn     = new TextBox { Width = 50 };
        error    = new TextBox { Width = 50 };
        DockPanel.SetDock(lbl, Dock.Left);
        row.Children.Add(lbl);
        row.Children.Add(wLbl);
        row.Children.Add(warn);
        row.Children.Add(eLbl);
        row.Children.Add(error);
        return row;
    }

    private static FrameworkElement SingleRow(string label, out TextBox box)
    {
        var row = new DockPanel { Margin = new Thickness(0, 3, 0, 3) };
        var lbl = new TextBlock { Text = label, Width = 200, VerticalAlignment = VerticalAlignment.Center };
        box     = new TextBox { Width = 50 };
        DockPanel.SetDock(lbl, Dock.Left);
        row.Children.Add(lbl);
        row.Children.Add(box);
        return row;
    }

    private static int Int(string fallback, TextBox tb)
        => int.TryParse(tb.Text, out var v) ? v : int.Parse(fallback);

    private static int ParseRetention(string s)
        => int.TryParse(s.Split(' ')[0], out var v) ? v : 30;

    private FrameworkElement BuildRuleRow(RuleConfiguration rule)
    {
        var row = new DockPanel { Margin = new Thickness(8, 2, 0, 2) };

        var sev = new ComboBox { Width = 90, Margin = new Thickness(8, 0, 0, 0) };
        sev.Items.Add("Disabled");
        sev.Items.Add("Info");
        sev.Items.Add("Warning");
        sev.Items.Add("Error");
        DockPanel.SetDock(sev, Dock.Right);
        sev.SelectionChanged += (_, _) => { if (!_loading) Changed?.Invoke(this, EventArgs.Empty); };

        var chk = new CheckBox
        {
            Content           = $"{rule.RuleId}  {rule.Description}",
            VerticalAlignment = VerticalAlignment.Center,
        };
        chk.Checked   += (_, _) => { if (!_loading) Changed?.Invoke(this, EventArgs.Empty); };
        chk.Unchecked += (_, _) =>
        {
            if (!_loading) { sev.SelectedItem = "Disabled"; Changed?.Invoke(this, EventArgs.Empty); }
        };

        row.Children.Add(sev);
        row.Children.Add(chk);

        _ruleRows.Add((rule.RuleId, chk, sev));
        return row;
    }

    private static string CategoryHeaderFor(RuleCategory category, int count) => category switch
    {
        RuleCategory.Complexity   => $"Complexity ({count})",
        RuleCategory.DeadCode     => $"Dead code ({count})",
        RuleCategory.Duplication  => $"Duplication ({count})",
        RuleCategory.Conventions  => $"Conventions ({count})",
        RuleCategory.Architecture => $"Architecture ({count})",
        RuleCategory.Project      => $"Project ({count})",
        RuleCategory.AsyncCode    => $"Async ({count})",
        RuleCategory.Linq         => $"LINQ ({count})",
        _                         => $"Other ({count})",
    };
}
