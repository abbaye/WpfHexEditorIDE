// ==========================================================
// Project: WpfHexEditor.Editor.DocumentEditor
// File: Layers/SpellCheckLayer.cs
// Description:
//     DrawingVisual overlay that renders smooth sinusoidal squiggles under
//     misspelled words, matching the VS Code / Word style.
// Architecture:
//     Each squiggle period is drawn as two cubic Bézier arcs (up + down).
//     Amplitude 1.5px, period 6px, 1.2px anti-aliased red pen.
// ==========================================================

using System.Windows;
using System.Windows.Media;
using WpfHexEditor.Core.SpellCheck;

namespace WpfHexEditor.Editor.DocumentEditor.Layers;

internal sealed class SpellCheckLayer : DrawingVisual
{
    private IReadOnlyList<SpellCheckError> _errors = [];

    private static readonly Pen SquigglePen = CreateSquigglePen();

    private static Pen CreateSquigglePen()
    {
        var pen = new Pen(new SolidColorBrush(Color.FromRgb(0xCC, 0x00, 0x00)), 1.0)
        {
            StartLineCap = PenLineCap.Round,
            EndLineCap   = PenLineCap.Round,
            LineJoin     = PenLineJoin.Round,
        };
        pen.Freeze();
        return pen;
    }

    public void SetErrors(IReadOnlyList<SpellCheckError> errors)
    {
        _errors = errors;
        Render();
    }

    public void Clear()
    {
        _errors = [];
        using var dc = RenderOpen();
    }

    private void Render()
    {
        using var dc = RenderOpen();
        foreach (var err in _errors)
            DrawSquiggle(dc, err);
    }

    private static void DrawSquiggle(DrawingContext dc, SpellCheckError err)
    {
        // Baseline: 2px above the bottom of the line so it sits just under the text
        double baseline = err.CanvasY + err.LineHeight - 1.5;
        double x        = err.CanvasX;
        double endX     = err.CanvasX + err.CanvasWidth;

        const double amplitude = 0.9;   // subtle — barely lifts off baseline
        const double period    = 4.0;   // tight wave, ~Word style
        const double half      = period / 2.0;
        const double cp        = half * 0.4;

        var geo = new StreamGeometry();
        using (var ctx = geo.Open())
        {
            ctx.BeginFigure(new Point(x, baseline), false, false);

            bool up = true;
            double px = x;

            while (px < endX)
            {
                double nx  = Math.Min(px + half, endX);
                double dy  = up ? -amplitude : amplitude;
                double mid = (px + nx) / 2.0;

                // Two control points hug the start and end horizontally,
                // pulling the curve into a clean arc.
                ctx.BezierTo(
                    new Point(px + cp,        baseline),
                    new Point(nx - cp,        baseline + dy),
                    new Point(nx,             baseline + dy),
                    true, false);

                // Return arc back to baseline at the next half-period
                double nx2 = Math.Min(nx + half, endX);
                ctx.BezierTo(
                    new Point(nx  + cp,       baseline + dy),
                    new Point(nx2 - cp,       baseline),
                    new Point(nx2,            baseline),
                    true, false);

                px  = nx2;
                up  = !up;

                if (nx2 >= endX) break;
            }
        }
        geo.Freeze();
        dc.DrawGeometry(null, SquigglePen, geo);
    }
}
