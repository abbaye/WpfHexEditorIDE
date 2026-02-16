//////////////////////////////////////////////
// Apache 2.0  - 2026
// Author : Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.5
//////////////////////////////////////////////

using System;
using System.Windows;
using System.Windows.Media;
using WpfHexaEditor.Core.Platform.Media;
using WpfHexaEditor.Core.Platform.Rendering;
using WpfHexaEditor.Wpf.Platform.Media;

namespace WpfHexaEditor.Wpf.Platform.Rendering
{
    /// <summary>
    /// WPF implementation of IDrawingContext that wraps System.Windows.Media.DrawingContext.
    /// </summary>
    public class WpfDrawingContext : IDrawingContext
    {
        private readonly DrawingContext _drawingContext;
        private bool _disposed;

        /// <summary>
        /// Gets the underlying WPF DrawingContext.
        /// </summary>
        public DrawingContext NativeContext => _drawingContext;

        public WpfDrawingContext(DrawingContext drawingContext)
        {
            _drawingContext = drawingContext ?? throw new ArgumentNullException(nameof(drawingContext));
        }

        /// <summary>
        /// Draws a rectangle with the specified brush and pen.
        /// </summary>
        public void DrawRectangle(IBrush? brush, IPen? pen, double x, double y, double width, double height)
        {
            var wpfBrush = brush is WpfBrush wb ? wb.NativeBrush : null;
            var wpfPen = pen is WpfPen wp ? wp.NativePen : null;

            var rect = new Rect(x, y, width, height);
            _drawingContext.DrawRectangle(wpfBrush, wpfPen, rect);
        }

        /// <summary>
        /// Draws a line between two points.
        /// </summary>
        public void DrawLine(IPen pen, double x1, double y1, double x2, double y2)
        {
            if (pen is not WpfPen wpfPen)
                throw new ArgumentException("Pen must be a WpfPen", nameof(pen));

            var point1 = new Point(x1, y1);
            var point2 = new Point(x2, y2);
            _drawingContext.DrawLine(wpfPen.NativePen, point1, point2);
        }

        /// <summary>
        /// Draws formatted text at the specified location.
        /// </summary>
        public void DrawText(IFormattedText text, double x, double y)
        {
            if (text is not WpfFormattedText wpfText)
                throw new ArgumentException("Text must be a WpfFormattedText", nameof(text));

            var point = new Point(x, y);
            _drawingContext.DrawText(wpfText.NativeText, point);
        }

        /// <summary>
        /// Draws an ellipse with the specified brush and pen.
        /// </summary>
        public void DrawEllipse(IBrush? brush, IPen? pen, double centerX, double centerY, double radiusX, double radiusY)
        {
            var wpfBrush = brush is WpfBrush wb ? wb.NativeBrush : null;
            var wpfPen = pen is WpfPen wp ? wp.NativePen : null;

            var center = new Point(centerX, centerY);
            _drawingContext.DrawEllipse(wpfBrush, wpfPen, center, radiusX, radiusY);
        }

        /// <summary>
        /// Pushes an opacity value onto the drawing context.
        /// </summary>
        public IDisposable PushOpacity(double opacity)
        {
            _drawingContext.PushOpacity(opacity);
            return new PopDisposable(_drawingContext.Pop);
        }

        /// <summary>
        /// Pushes a clip rectangle onto the drawing context.
        /// </summary>
        public IDisposable PushClip(double x, double y, double width, double height)
        {
            var rect = new Rect(x, y, width, height);
            var geometry = new RectangleGeometry(rect);
            _drawingContext.PushClip(geometry);
            return new PopDisposable(_drawingContext.Pop);
        }

        public void Dispose()
        {
            if (_disposed) return;

            _drawingContext.Close();
            _disposed = true;
        }

        /// <summary>
        /// Helper class for popping drawing context state.
        /// </summary>
        private class PopDisposable : IDisposable
        {
            private readonly Action _popAction;
            private bool _disposed;

            public PopDisposable(Action popAction)
            {
                _popAction = popAction;
            }

            public void Dispose()
            {
                if (_disposed) return;
                _popAction();
                _disposed = true;
            }
        }
    }
}
