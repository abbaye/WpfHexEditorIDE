// ==========================================================
// Project: WpfHexEditor.Editor.DocumentEditor
// File: Controls/DocumentStatusBar.xaml.cs
// Description:
//     Bottom status strip for the DocumentEditor. Shows format chip,
//     page count, word count, forensic alert badge, zoom slider,
//     and view mode indicator.
// Architecture: Dependency Properties driven by DocumentEditorHost.
// ==========================================================

using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using WpfHexEditor.Editor.DocumentEditor.Core.Model;
using WpfHexEditor.Editor.DocumentEditor.Properties;

namespace WpfHexEditor.Editor.DocumentEditor.Controls;

/// <summary>
/// Status bar displayed at the bottom of the document editor.
/// All data properties are Dependency Properties updated by <see cref="DocumentEditorHost"/>.
/// </summary>
public partial class DocumentStatusBar : UserControl
{
    // ── Dependency Properties ────────────────────────────────────────────────

    public static readonly DependencyProperty FormatNameProperty =
        DependencyProperty.Register(nameof(FormatName), typeof(string), typeof(DocumentStatusBar),
            new PropertyMetadata(DocumentEditorResources.DocEditorHost_MetaEmptyValue));

    public static readonly DependencyProperty FormatTooltipProperty =
        DependencyProperty.Register(nameof(FormatTooltip), typeof(string), typeof(DocumentStatusBar),
            new PropertyMetadata(string.Empty));

    public static readonly DependencyProperty PageTextProperty =
        DependencyProperty.Register(nameof(PageText), typeof(string), typeof(DocumentStatusBar),
            new PropertyMetadata(DocumentEditorResources.DocStatusBar_DefaultPage));

    public static readonly DependencyProperty WordCountTextProperty =
        DependencyProperty.Register(nameof(WordCountText), typeof(string), typeof(DocumentStatusBar),
            new PropertyMetadata(DocumentEditorResources.DocStatusBar_DefaultWordCount));

    public static readonly DependencyProperty ForensicAlertCountProperty =
        DependencyProperty.Register(nameof(ForensicAlertCount), typeof(int), typeof(DocumentStatusBar),
            new PropertyMetadata(0, OnForensicCountChanged));

    public static readonly DependencyProperty HasForensicAlertsProperty =
        DependencyProperty.Register(nameof(HasForensicAlerts), typeof(bool), typeof(DocumentStatusBar),
            new PropertyMetadata(false));

    public static readonly DependencyProperty ForensicBadgeBrushProperty =
        DependencyProperty.Register(nameof(ForensicBadgeBrush), typeof(Brush), typeof(DocumentStatusBar),
            new PropertyMetadata(Brushes.DarkRed));

    public static readonly DependencyProperty ZoomPercentProperty =
        DependencyProperty.Register(nameof(ZoomPercent), typeof(double), typeof(DocumentStatusBar),
            new PropertyMetadata(100.0, OnZoomPercentChanged));

    public static readonly DependencyProperty ZoomTextProperty =
        DependencyProperty.Register(nameof(ZoomText), typeof(string), typeof(DocumentStatusBar),
            new PropertyMetadata(DocumentEditorResources.DocStatusBar_DefaultZoom));

    public static readonly DependencyProperty ViewModeTextProperty =
        DependencyProperty.Register(nameof(ViewModeText), typeof(string), typeof(DocumentStatusBar),
            new PropertyMetadata(DocumentEditorResources.DocStatusBar_DefaultViewMode));

    // ── Events ────────────────────────────────────────────────────────────────

    /// <summary>Raised when the user clicks the forensic alert badge.</summary>
    public event EventHandler? ForensicBadgeClicked;

    /// <summary>Raised when the zoom slider value changes (value = percent 50–200).</summary>
    public event EventHandler<double>? ZoomChanged;

    // ── Constructor ──────────────────────────────────────────────────────────

    public DocumentStatusBar()
    {
        InitializeComponent();
    }

    // ── Properties ──────────────────────────────────────────────────────────

    public string FormatName
    {
        get => (string)GetValue(FormatNameProperty);
        set => SetValue(FormatNameProperty, value);
    }

    public string FormatTooltip
    {
        get => (string)GetValue(FormatTooltipProperty);
        set => SetValue(FormatTooltipProperty, value);
    }

    public string PageText
    {
        get => (string)GetValue(PageTextProperty);
        set => SetValue(PageTextProperty, value);
    }

    public string WordCountText
    {
        get => (string)GetValue(WordCountTextProperty);
        set => SetValue(WordCountTextProperty, value);
    }

    public int ForensicAlertCount
    {
        get => (int)GetValue(ForensicAlertCountProperty);
        set => SetValue(ForensicAlertCountProperty, value);
    }

    public bool HasForensicAlerts
    {
        get => (bool)GetValue(HasForensicAlertsProperty);
        private set => SetValue(HasForensicAlertsProperty, value);
    }

    public Brush ForensicBadgeBrush
    {
        get => (Brush)GetValue(ForensicBadgeBrushProperty);
        set => SetValue(ForensicBadgeBrushProperty, value);
    }

    public double ZoomPercent
    {
        get => (double)GetValue(ZoomPercentProperty);
        set => SetValue(ZoomPercentProperty, value);
    }

    public string ZoomText
    {
        get => (string)GetValue(ZoomTextProperty);
        private set => SetValue(ZoomTextProperty, value);
    }

    public string ViewModeText
    {
        get => (string)GetValue(ViewModeTextProperty);
        set => SetValue(ViewModeTextProperty, value);
    }

    // ── Public API ───────────────────────────────────────────────────────────

    /// <summary>Updates all status fields from the loaded model.</summary>
    public void BindModel(DocumentModel model, string formatExtension)
    {
        FormatName    = ResolveFormatName(formatExtension);
        FormatTooltip = string.Format(DocumentEditorResources.DocStatusBar_FormatPattern, FormatName, formatExtension.ToUpperInvariant());

        UpdateWordCount(model);
        UpdatePageCount(model);
        UpdateForensicCount(model);
    }

    public void UpdateWordCount(DocumentModel model)
    {
        int words = model.Blocks
            .Where(b => b.Kind is "paragraph" or "heading" or "run")
            .Sum(b => b.Text.Split(' ', StringSplitOptions.RemoveEmptyEntries).Length);

        WordCountText = string.Format(DocumentEditorResources.DocStatusBar_WordCountPattern, words);
    }

    public void UpdatePageCount(DocumentModel model)
    {
        int sections = model.Blocks.Count(b => b.Kind == "section");
        int pages    = sections > 0 ? sections : Math.Max(1, model.Blocks.Count / 30);
        PageText = string.Format(DocumentEditorResources.DocStatusBar_PagePattern, pages);
    }

    public void UpdateForensicCount(DocumentModel model)
    {
        int errors   = model.ForensicAlerts.Count(a => a.Severity == Core.Forensic.ForensicSeverity.Error);
        int warnings = model.ForensicAlerts.Count(a => a.Severity == Core.Forensic.ForensicSeverity.Warning);

        ForensicAlertCount = errors + warnings;
        HasForensicAlerts  = ForensicAlertCount > 0;

        if (errors > 0)
            ForensicBadgeBrush = TryFindResource("DE_ForensicBadgeErrorBg") as Brush ?? Brushes.DarkRed;
        else if (warnings > 0)
            ForensicBadgeBrush = TryFindResource("DE_ForensicBadgeWarnBg") as Brush ?? Brushes.DarkOrange;
    }

    // ── Helpers ──────────────────────────────────────────────────────────────

    private static string ResolveFormatName(string ext) => ext.ToLowerInvariant().TrimStart('.') switch
    {
        "docx" or "dotx" => "DOCX",
        "odt"  or "ott"  => "ODT",
        "rtf"            => "RTF",
        _                => ext.ToUpperInvariant().TrimStart('.')
    };

    // ── DP change callbacks ──────────────────────────────────────────────────

    private static void OnForensicCountChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is DocumentStatusBar sb)
            sb.HasForensicAlerts = (int)e.NewValue > 0;
    }

    private static void OnZoomPercentChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is DocumentStatusBar sb)
            sb.ZoomText = string.Format(DocumentEditorResources.DocStatusBar_ZoomPattern, (double)e.NewValue);
    }

    // ── Event handlers ───────────────────────────────────────────────────────

    private void OnForensicBadgeClicked(object sender, MouseButtonEventArgs e) =>
        ForensicBadgeClicked?.Invoke(this, EventArgs.Empty);

    private void OnSliderValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e) =>
        ZoomChanged?.Invoke(this, e.NewValue);
}
