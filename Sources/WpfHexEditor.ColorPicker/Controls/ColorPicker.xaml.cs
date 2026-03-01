//////////////////////////////////////////////
// Apache 2.0  - 2026
// Author : Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.6
//////////////////////////////////////////////

using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using WpfHexEditor.ColorPicker.Helpers;

namespace WpfHexEditor.ColorPicker.Controls
{
    /// <summary>
    /// Advanced ColorPicker control with HSV selector, RGB sliders, hex input, and color palettes.
    ///
    /// <para><b>Theme Integration:</b></para>
    /// <para>
    /// This control uses the following DynamicResources for theming:
    /// - BorderBrush: Border color for control frame
    /// - SurfaceElevatedBrush: Background for hex display section
    /// - ForegroundBrush: Text color for hex display
    ///
    /// If your application doesn't define these resources, built-in fallback values are used.
    /// To customize colors, define these brushes in your App.xaml or theme dictionary.
    /// </para>
    /// </summary>
    public partial class ColorPicker : UserControl
    {
        private bool _isDraggingHsv;
        private DateTime _lastHsvUpdate = DateTime.MinValue;
        private Color _originalColor; // Color before popup opens

        #region Dependency Properties

        /// <summary>
        /// The currently selected color (ARGB)
        /// </summary>
        public Color SelectedColor
        {
            get => (Color)GetValue(SelectedColorProperty);
            set => SetValue(SelectedColorProperty, value);
        }

        public static readonly DependencyProperty SelectedColorProperty =
            DependencyProperty.Register(
                nameof(SelectedColor),
                typeof(Color),
                typeof(ColorPicker),
                new FrameworkPropertyMetadata(
                    Color.FromRgb(64, 64, 255),
                    FrameworkPropertyMetadataOptions.BindsTwoWayByDefault,
                    OnSelectedColorChanged));

        private static void OnSelectedColorChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is ColorPicker picker && e.NewValue is Color newColor)
            {
                picker.ViewModel.SetColor(newColor);
                picker.UpdateHexDisplay();
                picker.UpdateHsvThumbPosition();
                picker.RaiseColorChanged(newColor);
            }
        }

        /// <summary>
        /// Whether to show the alpha channel slider
        /// </summary>
        public bool ShowAlphaChannel
        {
            get => (bool)GetValue(ShowAlphaChannelProperty);
            set => SetValue(ShowAlphaChannelProperty, value);
        }

        public static readonly DependencyProperty ShowAlphaChannelProperty =
            DependencyProperty.Register(
                nameof(ShowAlphaChannel),
                typeof(bool),
                typeof(ColorPicker),
                new PropertyMetadata(true, OnShowAlphaChannelChanged));

        private static void OnShowAlphaChannelChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is ColorPicker picker)
            {
                picker.AlphaSliderGrid.Visibility = (bool)e.NewValue ? Visibility.Visible : Visibility.Collapsed;
            }
        }

        #endregion

        #region Properties

        /// <summary>
        /// ViewModel for color manipulation
        /// </summary>
        public ColorPickerViewModel ViewModel { get; private set; }

        /// <summary>
        /// Recent colors collection
        /// </summary>
        public ObservableCollection<Color> RecentColors { get; private set; }

        #endregion

        #region Events

        /// <summary>
        /// Raised when the selected color changes
        /// </summary>
        public event EventHandler<Color> ColorChanged;

        private void RaiseColorChanged(Color newColor)
        {
            ColorChanged?.Invoke(this, newColor);
        }

        #endregion

        #region Constructor

        public ColorPicker()
        {
            InitializeComponent();

            // Initialize ViewModel
            ViewModel = new ColorPickerViewModel();
            ViewModel.PropertyChanged += ViewModel_PropertyChanged;
            DataContext = ViewModel;

            // Load recent colors
            RecentColors = new ObservableCollection<Color>(RecentColorManager.LoadRecentColors());
            RecentColorsPanel.ItemsSource = RecentColors;

            // Initialize standard colors palette
            InitializeStandardColors();

            // Set initial color
            ViewModel.SetColor(SelectedColor);
            UpdateHexDisplay();
        }

        #endregion

        #region Standard Colors Palette

        private void InitializeStandardColors()
        {
            var colors = new[]
            {
                // Row 1: Grayscale + Primary colors
                Colors.Transparent, Colors.Black, Colors.DarkGray, Colors.Gray,
                Colors.LightGray, Colors.White, Colors.Maroon, Colors.Red,

                // Row 2: Warm colors
                Colors.Orange, Colors.Yellow, Colors.Olive, Colors.Green,
                Colors.Teal, Colors.Cyan, Colors.Navy, Colors.Blue,

                // Row 3: Semi-transparent overlays (for HexEditor highlights)
                Color.FromArgb(102, 0, 120, 212),   // Blue
                Color.FromArgb(102, 255, 165, 0),   // Orange
                Color.FromArgb(102, 76, 175, 80),   // Green
                Color.FromArgb(102, 244, 67, 54),   // Red
                Color.FromArgb(96, 255, 255, 0),    // Yellow
                Color.FromArgb(102, 156, 39, 176),  // Purple
                Color.FromArgb(102, 0, 188, 212),   // Cyan
                Color.FromArgb(102, 255, 87, 34),   // Deep Orange

                // Row 4: Additional colors
                Colors.Pink, Colors.Brown, Colors.Purple, Colors.DarkCyan,
                Colors.Lime, Colors.Indigo, Colors.Gold, Colors.DarkViolet
            };

            StandardColorsPanel.ItemsSource = colors;
        }

        #endregion

        #region Event Handlers

        private void ViewModel_PropertyChanged(object sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(ViewModel.SelectedColor))
            {
                SelectedColor = ViewModel.SelectedColor;
            }
            else if (e.PropertyName == nameof(ViewModel.Hue))
            {
                UpdateHsvCanvasBackground();
            }
            else if (e.PropertyName == nameof(ViewModel.Saturation) || e.PropertyName == nameof(ViewModel.Value))
            {
                UpdateHsvThumbPosition();
            }
        }

        private void Border_MouseUp(object sender, MouseButtonEventArgs e)
        {
            // Only open on left button
            if (e.ChangedButton != MouseButton.Left)
                return;

            // Save original color
            _originalColor = SelectedColor;
            OriginalColorPreview.Background = new SolidColorBrush(_originalColor);

            // Open popup
            ColorPickerPopup.IsOpen = true;
        }

        private void HsvCanvas_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.LeftButton == MouseButtonState.Pressed)
            {
                _isDraggingHsv = true;
                ((IInputElement)sender).CaptureMouse();
                UpdateColorFromCanvasPosition(e.GetPosition(HsvCanvas));
            }
        }

        private void HsvCanvas_MouseMove(object sender, MouseEventArgs e)
        {
            if (_isDraggingHsv && e.LeftButton == MouseButtonState.Pressed)
            {
                // Throttle updates to 60 FPS (16ms)
                var now = DateTime.Now;
                if ((now - _lastHsvUpdate).TotalMilliseconds < 16)
                    return;

                _lastHsvUpdate = now;
                UpdateColorFromCanvasPosition(e.GetPosition(HsvCanvas));
            }
        }

        private void HsvCanvas_MouseUp(object sender, MouseButtonEventArgs e)
        {
            if (_isDraggingHsv)
            {
                _isDraggingHsv = false;
                ((IInputElement)sender).ReleaseMouseCapture();

                // Add to recent colors when done dragging
                AddToRecentColors(ViewModel.SelectedColor);
            }
        }

        private void PaletteColor_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (sender is FrameworkElement element && element.DataContext is Color color)
            {
                ViewModel.SetColor(color);
                AddToRecentColors(color);
                ColorPickerPopup.IsOpen = false;
            }
        }

        private void RecentColor_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (sender is FrameworkElement element && element.DataContext is Color color)
            {
                ViewModel.SetColor(color);
                ColorPickerPopup.IsOpen = false;
            }
        }

        #endregion

        #region HSV Canvas Manipulation

        private void UpdateColorFromCanvasPosition(Point position)
        {
            if (HsvCanvas.ActualWidth == 0 || HsvCanvas.ActualHeight == 0)
                return;

            // Calculate saturation and value from mouse position
            double saturation = Math.Max(0, Math.Min(1, position.X / HsvCanvas.ActualWidth));
            double value = Math.Max(0, Math.Min(1, 1 - (position.Y / HsvCanvas.ActualHeight)));

            // Update ViewModel (will trigger color update)
            ViewModel.Saturation = saturation;
            ViewModel.Value = value;
        }

        private void UpdateHsvThumbPosition()
        {
            if (HsvCanvas.ActualWidth == 0 || HsvCanvas.ActualHeight == 0)
                return;

            // Calculate position from saturation and value
            double x = ViewModel.Saturation * HsvCanvas.ActualWidth - 6;  // -6 to center thumb
            double y = (1 - ViewModel.Value) * HsvCanvas.ActualHeight - 6;

            Canvas.SetLeft(HsvThumb, Math.Max(0, Math.Min(HsvCanvas.ActualWidth - 12, x)));
            Canvas.SetTop(HsvThumb, Math.Max(0, Math.Min(HsvCanvas.ActualHeight - 12, y)));
        }

        private void UpdateHsvCanvasBackground()
        {
            // Convert hue to RGB for canvas background
            var (r, g, b) = ColorSpaceConverter.HsvToRgb(ViewModel.Hue, 1, 1);
            HsvCanvas.Background = new SolidColorBrush(Color.FromRgb(r, g, b));
        }

        #endregion

        #region Recent Colors Management

        private void AddToRecentColors(Color color)
        {
            var updatedList = RecentColorManager.AddRecentColor(color, RecentColors.ToList());

            // Update ObservableCollection
            RecentColors.Clear();
            foreach (var c in updatedList)
            {
                RecentColors.Add(c);
            }
        }

        #endregion

        #region UI Updates

        private void UpdateHexDisplay()
        {
            var color = SelectedColor;
            HexColorText.Text = $"#{color.A:X2}{color.R:X2}{color.G:X2}{color.B:X2}";
        }

        #endregion
    }
}
