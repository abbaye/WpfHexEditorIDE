// ==========================================================
// Project: WpfHexEditor.Editor.XamlDesigner
// File: ShapeDrawingService.cs
// Description:
//     Produces minimal XAML markup snippets for shapes drawn on the design
//     canvas. Invoked by DesignCanvas.CommitDrawnShape() after the user
//     completes a draw drag. The generated snippet is injected into the live
//     XAML document via DesignToXamlSyncService.
//
// Architecture Notes:
//     Pure service — stateless, no WPF rendering dependency.
//     One method per shape type; all return a complete element string
//     with Canvas.Left / Canvas.Top / Width / Height attributes.
//     Uses InvariantCulture for all double→string conversions so the
//     generated XAML is locale-independent.
// ==========================================================

using System.Globalization;
using System.Windows;

namespace WpfHexEditor.Editor.XamlDesigner.Services;

/// <summary>
/// Identifies which shape tool is currently active on the design canvas.
/// </summary>
public enum DrawingTool
{
    None,
    Rectangle,
    Ellipse,
    Line,
}

/// <summary>
/// Generates XAML markup for shapes drawn by the user on the design canvas.
/// </summary>
public sealed class ShapeDrawingService
{
    private static string D(double v) => v.ToString("F0", CultureInfo.InvariantCulture);

    /// <summary>
    /// Produces a XAML element string for the given tool and bounding rectangle.
    /// </summary>
    /// <param name="tool">The active drawing tool.</param>
    /// <param name="bounds">Canvas-relative bounding rect of the drawn shape.</param>
    /// <param name="fill">Fill attribute value (e.g. "#407ACC" or "Transparent").</param>
    /// <param name="stroke">Stroke attribute value (e.g. "#007ACC").</param>
    /// <param name="strokeThickness">Stroke thickness in device-independent pixels.</param>
    /// <returns>XAML element string, or null when tool is None.</returns>
    public string? GenerateXaml(
        DrawingTool tool,
        Rect        bounds,
        string      fill            = "Transparent",
        string      stroke          = "#FF007ACC",
        double      strokeThickness = 1.5)
    {
        if (tool == DrawingTool.None) return null;

        double x = bounds.X;
        double y = bounds.Y;
        double w = Math.Max(1, bounds.Width);
        double h = Math.Max(1, bounds.Height);

        string st = strokeThickness.ToString("F1", CultureInfo.InvariantCulture);

        return tool switch
        {
            DrawingTool.Rectangle =>
                $"<Rectangle Canvas.Left=\"{D(x)}\" Canvas.Top=\"{D(y)}\" " +
                $"Width=\"{D(w)}\" Height=\"{D(h)}\" " +
                $"Fill=\"{fill}\" Stroke=\"{stroke}\" StrokeThickness=\"{st}\"/>",

            DrawingTool.Ellipse =>
                $"<Ellipse Canvas.Left=\"{D(x)}\" Canvas.Top=\"{D(y)}\" " +
                $"Width=\"{D(w)}\" Height=\"{D(h)}\" " +
                $"Fill=\"{fill}\" Stroke=\"{stroke}\" StrokeThickness=\"{st}\"/>",

            DrawingTool.Line =>
                $"<Line X1=\"{D(x)}\" Y1=\"{D(y)}\" X2=\"{D(x + w)}\" Y2=\"{D(y + h)}\" " +
                $"Stroke=\"{stroke}\" StrokeThickness=\"{st}\"/>",

            _ => null
        };
    }

    /// <summary>Returns the default display name of the tool.</summary>
    public static string GetToolName(DrawingTool tool) => tool switch
    {
        DrawingTool.Rectangle => "Rectangle",
        DrawingTool.Ellipse   => "Ellipse",
        DrawingTool.Line      => "Line",
        _                     => "None"
    };
}
