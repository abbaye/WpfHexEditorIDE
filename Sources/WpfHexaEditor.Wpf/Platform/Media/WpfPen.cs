//////////////////////////////////////////////
// Apache 2.0  - 2026
// Author : Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.5
//////////////////////////////////////////////

using System.Windows.Media;
using WpfHexaEditor.Core.Platform.Media;

namespace WpfHexaEditor.Wpf.Platform.Media
{
    /// <summary>
    /// WPF implementation of IPen that wraps System.Windows.Media.Pen.
    /// </summary>
    public class WpfPen : IPen
    {
        /// <summary>
        /// The underlying WPF pen.
        /// </summary>
        public Pen NativePen { get; }

        /// <summary>
        /// Gets the platform-specific pen object.
        /// </summary>
        public object PlatformPen => NativePen;

        /// <summary>
        /// Gets or sets the brush used to draw the pen.
        /// </summary>
        public IBrush? Brush
        {
            get => NativePen.Brush != null ? new WpfBrush(NativePen.Brush) : null;
            set
            {
                if (value is WpfBrush wpfBrush)
                    NativePen.Brush = wpfBrush.NativeBrush;
            }
        }

        /// <summary>
        /// Gets or sets the thickness of the pen.
        /// </summary>
        public double Thickness
        {
            get => NativePen.Thickness;
            set => NativePen.Thickness = value;
        }

        public WpfPen(Pen pen)
        {
            NativePen = pen ?? throw new ArgumentNullException(nameof(pen));
        }

        /// <summary>
        /// Creates a new WpfPen from a brush and thickness.
        /// </summary>
        public static WpfPen Create(IBrush brush, double thickness)
        {
            if (brush is WpfBrush wpfBrush)
            {
                return new WpfPen(new Pen(wpfBrush.NativeBrush, thickness));
            }
            throw new ArgumentException("Brush must be a WpfBrush", nameof(brush));
        }

        /// <summary>
        /// Converts a WPF Pen to IPen.
        /// </summary>
        public static implicit operator WpfPen(Pen pen)
        {
            return new WpfPen(pen);
        }

        /// <summary>
        /// Converts IPen to WPF Pen.
        /// </summary>
        public static implicit operator Pen(WpfPen wpfPen)
        {
            return wpfPen.NativePen;
        }
    }
}
