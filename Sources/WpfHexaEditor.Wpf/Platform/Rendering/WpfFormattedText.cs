//////////////////////////////////////////////
// Apache 2.0  - 2026
// Author : Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.5
//////////////////////////////////////////////

using System.Globalization;
using System.Windows;
using System.Windows.Media;
using WpfHexaEditor.Core.Platform.Media;
using WpfHexaEditor.Core.Platform.Rendering;
using WpfHexaEditor.Wpf.Platform.Media;

namespace WpfHexaEditor.Wpf.Platform.Rendering
{
    /// <summary>
    /// WPF implementation of IFormattedText that wraps System.Windows.Media.FormattedText.
    /// </summary>
    public class WpfFormattedText : IFormattedText
    {
        private readonly FormattedText _formattedText;

        /// <summary>
        /// Gets the text content.
        /// </summary>
        public string Text => _formattedText.Text;

        /// <summary>
        /// Gets or sets the foreground brush.
        /// </summary>
        public IBrush? Foreground
        {
            get => null; // WPF FormattedText doesn't expose foreground brush getter
            set
            {
                if (value is WpfBrush wpfBrush)
                    _formattedText.SetForegroundBrush(wpfBrush.NativeBrush);
            }
        }

        /// <summary>
        /// Gets the width of the formatted text.
        /// </summary>
        public double Width => _formattedText.Width;

        /// <summary>
        /// Gets the height of the formatted text.
        /// </summary>
        public double Height => _formattedText.Height;

        /// <summary>
        /// Gets or sets the maximum text width before wrapping.
        /// </summary>
        public double MaxTextWidth
        {
            get => _formattedText.MaxTextWidth;
            set => _formattedText.MaxTextWidth = value;
        }

        /// <summary>
        /// Gets or sets the maximum text height.
        /// </summary>
        public double MaxTextHeight
        {
            get => _formattedText.MaxTextHeight;
            set => _formattedText.MaxTextHeight = value;
        }

        /// <summary>
        /// Gets the underlying platform-specific formatted text object.
        /// </summary>
        public object PlatformText => _formattedText;

        public WpfFormattedText(FormattedText formattedText)
        {
            _formattedText = formattedText ?? throw new ArgumentNullException(nameof(formattedText));
        }

        /// <summary>
        /// Creates a new WpfFormattedText with the specified parameters.
        /// </summary>
        public static WpfFormattedText Create(
            string text,
            Typeface typeface,
            double fontSize,
            Brush foreground)
        {
#pragma warning disable CS0618 // FormattedText constructor is deprecated but still widely used
            var formattedText = new FormattedText(
                text,
                CultureInfo.CurrentCulture,
                FlowDirection.LeftToRight,
                typeface,
                fontSize,
                foreground,
                VisualTreeHelper.GetDpi(Application.Current.MainWindow).PixelsPerDip);
#pragma warning restore CS0618

            return new WpfFormattedText(formattedText);
        }

        /// <summary>
        /// Gets the underlying WPF FormattedText.
        /// </summary>
        public FormattedText NativeText => _formattedText;
    }
}
