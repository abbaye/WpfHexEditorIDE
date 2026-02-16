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
    /// WPF implementation of IBrush that wraps System.Windows.Media.Brush.
    /// </summary>
    public class WpfBrush : IBrush
    {
        /// <summary>
        /// The underlying WPF brush.
        /// </summary>
        public Brush NativeBrush { get; }

        /// <summary>
        /// Gets the platform-specific brush object.
        /// </summary>
        public object PlatformBrush => NativeBrush;

        public WpfBrush(Brush brush)
        {
            NativeBrush = brush ?? throw new ArgumentNullException(nameof(brush));
        }

        /// <summary>
        /// Creates a WpfBrush from a PlatformColor.
        /// </summary>
        public static WpfBrush FromColor(PlatformColor color)
        {
            var wpfColor = Color.FromArgb(color.A, color.R, color.G, color.B);
            return new WpfBrush(new SolidColorBrush(wpfColor));
        }

        /// <summary>
        /// Converts a WPF Brush to IBrush.
        /// </summary>
        public static implicit operator WpfBrush(Brush brush)
        {
            return new WpfBrush(brush);
        }

        /// <summary>
        /// Converts IBrush to WPF Brush.
        /// </summary>
        public static implicit operator Brush(WpfBrush wpfBrush)
        {
            return wpfBrush.NativeBrush;
        }
    }
}
