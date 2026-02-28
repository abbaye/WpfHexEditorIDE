//////////////////////////////////////////////
// Apache 2.0  - 2026
// Author : Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.5
//////////////////////////////////////////////

using System;
using System.ComponentModel;
using System.Windows;
using System.Windows.Media;
using WpfHexEditor.Core.Settings;

namespace WpfHexEditor.HexEditor
{
    /// <summary>
    /// HexEditor partial class - Zoom Support
    /// Contains methods for zoom/scale functionality
    /// </summary>
    public partial class HexEditor
    {
        #region Zoom Support

        /// <summary>
        /// Get or set the zoom scale
        /// Possible Scale: 0.5 to 2.0 (50% to 200%)
        /// </summary>
        [Category("Behavior")]
        [Range(0.5, 2.0, Step = 0.1)]
        public double ZoomScale
        {
            get => (double)GetValue(ZoomScaleProperty);
            set => SetValue(ZoomScaleProperty, value);
        }

        public static readonly DependencyProperty ZoomScaleProperty =
            DependencyProperty.Register(nameof(ZoomScale), typeof(double), typeof(HexEditor),
                new FrameworkPropertyMetadata(1.0, ZoomScale_ChangedCallBack, ZoomScale_CoerceValueCallBack));

        private static object ZoomScale_CoerceValueCallBack(DependencyObject d, object baseValue)
        {
            // Clamp zoom between 0.5 and 2.0
            double value = (double)baseValue;
            if (value < 0.5) return 0.5;
            if (value > 2.0) return 2.0;
            return value;
        }

        private static void ZoomScale_ChangedCallBack(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is HexEditor ctrl && e.NewValue != e.OldValue)
            {
                ctrl.UpdateZoom();
            }
        }

        /// <summary>
        /// Initialize the support of zoom
        /// </summary>
        private void InitialiseZoom()
        {
            if (_scaler != null) return;

            _scaler = new ScaleTransform(ZoomScale, ZoomScale);

            // Apply scale transform to zoomable elements (like V1)
            // Apply to entire header border so all header elements scale together
            if (_headerBorder != null)
                _headerBorder.LayoutTransform = _scaler;
            if (HexViewport != null)
                HexViewport.LayoutTransform = _scaler;
        }

        /// <summary>
        /// Update the zoom to ZoomScale value if AllowZoom is true
        /// </summary>
        private void UpdateZoom()
        {
            if (!AllowZoom) return;

            if (_scaler == null) InitialiseZoom();
            if (_scaler != null)
            {
                _scaler.ScaleX = ZoomScale;
                _scaler.ScaleY = ZoomScale;
            }

            // Update viewport and refresh display
            HexViewport?.InvalidateVisual();
            UpdateVisibleLines();

            // Raise event
            OnZoomScaleChanged(EventArgs.Empty);
        }

        /// <summary>
        /// Reset the zoom to 100%
        /// </summary>
        public void ResetZoom() => ZoomScale = 1.0;

        #endregion
    }
}
