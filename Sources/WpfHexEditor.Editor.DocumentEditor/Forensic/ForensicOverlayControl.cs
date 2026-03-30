// ==========================================================
// Project: WpfHexEditor.Editor.DocumentEditor
// File: Forensic/ForensicOverlayControl.cs
// Description:
//     WPF Adorner that renders a 16px forensic gutter on the left edge
//     of the DocumentTextPane. Each badge is a colored rectangle mapped
//     to a ForensicAlert — green (OK), yellow (Warning), red (Error).
//     Tooltip shows alert message + suggestion.
// ==========================================================

using System.Windows;
using System.Windows.Documents;
using System.Windows.Media;
using WpfHexEditor.Editor.DocumentEditor.Core.Forensic;
using WpfHexEditor.Editor.DocumentEditor.Core.Model;

namespace WpfHexEditor.Editor.DocumentEditor.Forensic;

/// <summary>
/// Adorner that draws a gutter strip over the <see cref="Controls.DocumentTextPane"/>
/// with per-alert colored badges when forensic mode is active.
/// </summary>
public sealed class ForensicOverlayControl : Adorner
{
    private IReadOnlyList<ForensicAlert> _alerts = [];
    private DocumentModel? _model;

    private static readonly SolidColorBrush OkBrush;
    private static readonly SolidColorBrush WarnBrush;
    private static readonly SolidColorBrush ErrorBrush;
    private static readonly Pen             BorderPen;
    private static readonly Typeface        BadgeTypeface;
    private static readonly double          GutterWidth = 16.0;
    private static readonly double          BadgeSize   = 10.0;
    private static readonly double          BadgeMargin = 3.0;

    static ForensicOverlayControl()
    {
        OkBrush    = new SolidColorBrush(Color.FromArgb(80,  34, 187,  34)); OkBrush.Freeze();
        WarnBrush  = new SolidColorBrush(Color.FromArgb(160, 255, 204,   0)); WarnBrush.Freeze();
        ErrorBrush = new SolidColorBrush(Color.FromArgb(160, 255,  68,  68)); ErrorBrush.Freeze();
        BorderPen  = new Pen(new SolidColorBrush(Colors.Transparent), 0); BorderPen.Freeze();
        BadgeTypeface = new Typeface("Segoe UI");
    }

    public ForensicOverlayControl(UIElement adornedElement) : base(adornedElement)
    {
        IsHitTestVisible = false;
    }

    // ── Public API ───────────────────────────────────────────────────────────

    public void UpdateAlerts(DocumentModel model)
    {
        _model   = model;
        _alerts  = model.ForensicAlerts;
        InvalidateVisual();
    }

    // ── Rendering ────────────────────────────────────────────────────────────

    protected override void OnRender(DrawingContext dc)
    {
        if (_alerts.Count == 0) return;

        double height = AdornedElement.RenderSize.Height;

        // Gutter background (semi-transparent)
        dc.DrawRectangle(
            new SolidColorBrush(Color.FromArgb(40, 20, 20, 20)),
            BorderPen,
            new Rect(0, 0, GutterWidth, height));

        // One badge per alert (up to 50 visible)
        double itemHeight = BadgeSize + BadgeMargin * 2;
        for (int i = 0; i < Math.Min(_alerts.Count, 50); i++)
        {
            var alert  = _alerts[i];
            var brush  = alert.Severity switch
            {
                ForensicSeverity.Error   => ErrorBrush,
                ForensicSeverity.Warning => WarnBrush,
                _                        => OkBrush
            };

            double y = i * itemHeight + BadgeMargin;
            if (y + BadgeSize > height) break;

            dc.DrawEllipse(
                brush,
                BorderPen,
                new Point(GutterWidth / 2, y + BadgeSize / 2),
                BadgeSize / 2,
                BadgeSize / 2);
        }
    }
}
