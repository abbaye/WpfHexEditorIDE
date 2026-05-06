// ==========================================================
// Project: WpfHexEditor.Editor.DocumentEditor
// File: Layers/SpellCheckLayer.cs
// Description:
//     DrawingVisual overlay that renders red squiggles under misspelled words.
//     Follows the same architecture as LspInlayHintsLayer: hosted in the
//     DocumentCanvasRenderer visual tree, re-rendered on SetErrors().
// Architecture:
//     Squiggle = zigzag line: amplitude 2px, period 4px, 1px red pen.
//     Errors are in canvas-space coordinates supplied by SpellCheckService.
// ==========================================================

using System.Windows;
using System.Windows.Media;
using WpfHexEditor.Editor.Core.SpellCheck;

namespace WpfHexEditor.Editor.DocumentEditor.Layers;

internal sealed class SpellCheckLayer : DrawingVisual
{
    private IReadOnlyList<SpellCheckError> _errors = [];

    private static readonly Pen SquigglePen = CreateSquigglePen();

    private static Pen CreateSquigglePen()
    {
        var pen = new Pen(Brushes.Red, 1.0);
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
        // empty — clears the visual
    }

    private void Render()
    {
        using var dc = RenderOpen();
        foreach (var err in _errors)
            DrawSquiggle(dc, err);
    }

    private static void DrawSquiggle(DrawingContext dc, SpellCheckError err)
    {
        double y       = err.CanvasY + err.LineHeight - 1.5;
        double x       = err.CanvasX;
        double endX    = err.CanvasX + err.CanvasWidth;
        const double amplitude = 2.0;
        const double period    = 4.0;

        var geo = new StreamGeometry();
        using (var ctx = geo.Open())
        {
            ctx.BeginFigure(new Point(x, y), false, false);
            bool up = true;
            for (double cx = x + period / 2; cx <= endX + period; cx += period / 2)
            {
                double px = Math.Min(cx, endX);
                ctx.LineTo(new Point(px, up ? y - amplitude : y + amplitude), true, false);
                up = !up;
            }
        }
        geo.Freeze();
        dc.DrawGeometry(null, SquigglePen, geo);
    }
}
