//////////////////////////////////////////////
// GNU Affero General Public License v3.0 - 2026
// Author : Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.5, Claude Sonnet 4.6
//////////////////////////////////////////////
// Project: WpfHexEditor.HexEditor
// File: HexEditor.ColorProperties.cs
// Description: Color dependency properties with change callbacks that sync to HexViewport.
// Architecture notes: Partial class extracted from HexEditor.xaml.cs
//////////////////////////////////////////////

using System.ComponentModel;
using System.Windows;
using System.Windows.Media;

namespace WpfHexEditor.HexEditor
{
    public partial class HexEditor
    {
        #region Color Properties

        /// <summary>
        /// First selection gradient color
        /// </summary>
        [Category("Colors.Selection")]
        public Color SelectionFirstColor
        {
            get => (Color)GetValue(SelectionFirstColorProperty);
            set => SetValue(SelectionFirstColorProperty, value);
        }

        public static readonly DependencyProperty SelectionFirstColorProperty =
            DependencyProperty.Register(nameof(SelectionFirstColor), typeof(Color), typeof(HexEditor),
                new PropertyMetadata(Color.FromArgb(102, 0, 120, 212), OnSelectionFirstColorChanged));

        private static void OnSelectionFirstColorChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is HexEditor editor)
            {
                var color = (Color)e.NewValue;
                editor.Resources["SelectionBrush"] = new SolidColorBrush(color) { Opacity = 0.4 };

                // Update HexViewport color
                if (editor.HexViewport != null)
                {
                    editor.HexViewport.SelectionColor = color;

                    // CRITICAL: Also update SelectionActiveBrush which is used in rendering
                    editor.SelectionActiveBrush = new SolidColorBrush(color);
                    editor.HexViewport.SelectionActiveBrush = editor.SelectionActiveBrush;
                }
            }
        }

        /// <summary>
        /// Second selection gradient color
        /// </summary>
        [Category("Colors.Selection")]
        public Color SelectionSecondColor
        {
            get => (Color)GetValue(SelectionSecondColorProperty);
            set => SetValue(SelectionSecondColorProperty, value);
        }

        public static readonly DependencyProperty SelectionSecondColorProperty =
            DependencyProperty.Register(nameof(SelectionSecondColor), typeof(Color), typeof(HexEditor),
                new PropertyMetadata(Color.FromArgb(102, 0, 120, 212), OnSelectionSecondColorChanged));

        private static void OnSelectionSecondColorChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is HexEditor editor && editor.HexViewport != null)
            {
                var color = (Color)e.NewValue;

                // SelectionSecondColor updates the inactive selection brush
                editor.SelectionInactiveBrush = new SolidColorBrush(color);
                editor.HexViewport.SelectionInactiveBrush = editor.SelectionInactiveBrush;
                editor.HexViewport.SelectionColor = color;
            }
        }

        /// <summary>
        /// Color for modified bytes
        /// </summary>
        [Category("Colors.ByteStates")]
        public Color ByteModifiedColor
        {
            get => (Color)GetValue(ByteModifiedColorProperty);
            set => SetValue(ByteModifiedColorProperty, value);
        }

        public static readonly DependencyProperty ByteModifiedColorProperty =
            DependencyProperty.Register(nameof(ByteModifiedColor), typeof(Color), typeof(HexEditor),
                new PropertyMetadata(Color.FromRgb(255, 165, 0), OnByteModifiedColorChanged));

        private static void OnByteModifiedColorChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is HexEditor editor)
            {
                var color = (Color)e.NewValue;
                editor.Resources["ModifiedBrush"] = new SolidColorBrush(color);

                // Update HexViewport color
                if (editor.HexViewport != null)
                    editor.HexViewport.ModifiedByteColor = color;
            }
        }

        /// <summary>
        /// Color for added bytes
        /// </summary>
        [Category("Colors.ByteStates")]
        public Color ByteAddedColor
        {
            get => (Color)GetValue(ByteAddedColorProperty);
            set => SetValue(ByteAddedColorProperty, value);
        }

        public static readonly DependencyProperty ByteAddedColorProperty =
            DependencyProperty.Register(nameof(ByteAddedColor), typeof(Color), typeof(HexEditor),
                new PropertyMetadata(Color.FromRgb(76, 175, 80), OnByteAddedColorChanged));

        private static void OnByteAddedColorChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is HexEditor editor)
            {
                var color = (Color)e.NewValue;
                editor.Resources["AddedBrush"] = new SolidColorBrush(color);

                // Update HexViewport color
                if (editor.HexViewport != null)
                    editor.HexViewport.AddedByteColor = color;
            }
        }

        /// <summary>
        /// Color for highlighted bytes
        /// </summary>
        [Category("Colors.ByteStates")]
        public Color HighLightColor
        {
            get => (Color)GetValue(HighLightColorProperty);
            set => SetValue(HighLightColorProperty, value);
        }

        public static readonly DependencyProperty HighLightColorProperty =
            DependencyProperty.Register(nameof(HighLightColor), typeof(Color), typeof(HexEditor),
                new PropertyMetadata(Colors.Gold, OnHighLightColorChanged));

        private static void OnHighLightColorChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is HexEditor editor && editor.HexViewport != null)
            {
                var color = (Color)e.NewValue;
                editor.HexViewport.HighlightColor = color;
            }
        }

        /// <summary>
        /// Mouse over color
        /// </summary>
        [Category("Colors.ByteStates")]
        public Color MouseOverColor
        {
            get => (Color)GetValue(MouseOverColorProperty);
            set => SetValue(MouseOverColorProperty, value);
        }

        public static readonly DependencyProperty MouseOverColorProperty =
            DependencyProperty.Register(nameof(MouseOverColor), typeof(Color), typeof(HexEditor),
                new PropertyMetadata(Color.FromArgb(0x50, 100, 150, 255), OnMouseOverColorChanged)); // Light Blue semi-transparent (31% opacity)

        private static void OnMouseOverColorChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is HexEditor editor && e.NewValue is Color color)
            {
                editor.Resources["ByteHoverBrush"] = new SolidColorBrush(color);

                // Update HexViewport hover brush for V2
                if (editor.HexViewport != null)
                {
                    editor.HexViewport.MouseHoverBrush = new SolidColorBrush(color);
                    editor.HexViewport.InvalidateVisual();
                }
            }
        }

        /// <summary>
        /// Foreground color for normal bytes (even columns: 00, 02, 04...)
        /// </summary>
        [Category("Colors.Foreground")]
        public Color ForegroundFirstColor
        {
            get => (Color)GetValue(ForegroundFirstColorProperty);
            set => SetValue(ForegroundFirstColorProperty, value);
        }

        public static readonly DependencyProperty ForegroundFirstColorProperty =
            DependencyProperty.Register(nameof(ForegroundFirstColor), typeof(Color), typeof(HexEditor),
                new PropertyMetadata(Colors.Black, OnForegroundFirstColorChanged));

        private static void OnForegroundFirstColorChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is HexEditor editor)
            {
                var color = (Color)e.NewValue;
                editor.Resources["ByteForegroundBrush"] = new SolidColorBrush(color);

                // Update HexViewport color
                if (editor.HexViewport != null)
                    editor.HexViewport.NormalByteColor = color;
            }
        }

        /// <summary>
        /// Foreground color for alternate bytes (odd columns: 01, 03, 05...)
        /// </summary>
        [Category("Colors.Foreground")]
        public Color ForegroundSecondColor
        {
            get => (Color)GetValue(ForegroundSecondColorProperty);
            set => SetValue(ForegroundSecondColorProperty, value);
        }

        public static readonly DependencyProperty ForegroundSecondColorProperty =
            DependencyProperty.Register(nameof(ForegroundSecondColor), typeof(Color), typeof(HexEditor),
                new PropertyMetadata(Colors.Blue, OnForegroundSecondColorChanged));

        private static void OnForegroundSecondColorChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is HexEditor editor)
            {
                var color = (Color)e.NewValue;
                editor.Resources["AlternateByteForegroundBrush"] = new SolidColorBrush(color);

                // Update HexViewport color
                if (editor.HexViewport != null)
                    editor.HexViewport.AlternateByteColor = color;
            }
        }

        /// <summary>
        /// Foreground color for offset header
        /// </summary>
        [Category("Colors.Foreground")]
        public Color ForegroundOffSetHeaderColor
        {
            get => (Color)GetValue(ForegroundOffSetHeaderColorProperty);
            set => SetValue(ForegroundOffSetHeaderColorProperty, value);
        }

        public static readonly DependencyProperty ForegroundOffSetHeaderColorProperty =
            DependencyProperty.Register(nameof(ForegroundOffSetHeaderColor), typeof(Color), typeof(HexEditor),
                new PropertyMetadata(Color.FromRgb(117, 117, 117), OnForegroundOffSetHeaderColorChanged));

        private static void OnForegroundOffSetHeaderColorChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is HexEditor editor)
            {
                var color = (Color)e.NewValue;
                editor.Resources["OffsetBrush"] = new SolidColorBrush(color);

                // Update HexViewport offset foreground color
                if (editor.HexViewport != null)
                    editor.HexViewport.OffsetForegroundColor = color;
            }
        }

        /// <summary>
        /// Foreground highlight offset header color
        /// </summary>
        [Category("Colors.Foreground")]
        public Color ForegroundHighLightOffSetHeaderColor
        {
            get => (Color)GetValue(ForegroundHighLightOffSetHeaderColorProperty);
            set => SetValue(ForegroundHighLightOffSetHeaderColorProperty, value);
        }

        public static readonly DependencyProperty ForegroundHighLightOffSetHeaderColorProperty =
            DependencyProperty.Register(nameof(ForegroundHighLightOffSetHeaderColor), typeof(Color), typeof(HexEditor),
                new PropertyMetadata(Colors.DarkBlue, OnForegroundHighLightOffSetHeaderColorChanged));

        private static void OnForegroundHighLightOffSetHeaderColorChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is HexEditor editor && editor.HexViewport != null)
            {
                // This color is used for highlighted offset headers
                // Currently not directly used in HexViewport rendering, but keep for future use
                editor.HexViewport.InvalidateVisual();
            }
        }

        /// <summary>
        /// Foreground contrast color
        /// </summary>
        [Category("Colors.Foreground")]
        public Color ForegroundContrast
        {
            get => (Color)GetValue(ForegroundContrastProperty);
            set => SetValue(ForegroundContrastProperty, value);
        }

        public static readonly DependencyProperty ForegroundContrastProperty =
            DependencyProperty.Register(nameof(ForegroundContrast), typeof(Color), typeof(HexEditor),
                new PropertyMetadata(Colors.Black, OnForegroundContrastChanged));

        private static void OnForegroundContrastChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is HexEditor editor && editor.HexViewport != null)
            {
                // This color is used for high contrast foreground
                // Currently not directly used in HexViewport rendering, but keep for future use
                editor.HexViewport.InvalidateVisual();
            }
        }

        /// <summary>
        /// Background color for the hex editor content area.
        /// </summary>
        [Category("Colors.Background")]
        public Color BackgroundColor
        {
            get => (Color)GetValue(BackgroundColorProperty);
            set => SetValue(BackgroundColorProperty, value);
        }

        public static readonly DependencyProperty BackgroundColorProperty =
            DependencyProperty.Register(nameof(BackgroundColor), typeof(Color), typeof(HexEditor),
                new PropertyMetadata(Colors.White, OnBackgroundColorChanged));

        private static void OnBackgroundColorChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is HexEditor editor)
            {
                var color = (Color)e.NewValue;
                editor.Background = new SolidColorBrush(color);

                if (editor.HexViewport != null)
                    editor.HexViewport.BackgroundColor = color;
            }
        }

        /// <summary>
        /// Background color for the column header area.
        /// </summary>
        [Category("Colors.Background")]
        public Color HeaderBackgroundColor
        {
            get => (Color)GetValue(HeaderBackgroundColorProperty);
            set => SetValue(HeaderBackgroundColorProperty, value);
        }

        public static readonly DependencyProperty HeaderBackgroundColorProperty =
            DependencyProperty.Register(nameof(HeaderBackgroundColor), typeof(Color), typeof(HexEditor),
                new PropertyMetadata(Color.FromRgb(0xF5, 0xF5, 0xF5), OnHeaderBackgroundColorChanged));

        private static void OnHeaderBackgroundColorChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is HexEditor editor)
            {
                var color = (Color)e.NewValue;
                editor.Resources["HeaderBrush"] = new SolidColorBrush(color);
            }
        }

        /// <summary>
        /// Foreground color for the column header text.
        /// </summary>
        [Category("Colors.Background")]
        public Color HeaderForegroundColor
        {
            get => (Color)GetValue(HeaderForegroundColorProperty);
            set => SetValue(HeaderForegroundColorProperty, value);
        }

        public static readonly DependencyProperty HeaderForegroundColorProperty =
            DependencyProperty.Register(nameof(HeaderForegroundColor), typeof(Color), typeof(HexEditor),
                new PropertyMetadata(Color.FromRgb(0x42, 0x42, 0x42), OnHeaderForegroundColorChanged));

        private static void OnHeaderForegroundColorChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is HexEditor editor)
            {
                var color = (Color)e.NewValue;
                editor.Resources["HeaderTextBrush"] = new SolidColorBrush(color);
            }
        }

        /// <summary>
        /// Color for column separator lines and header border.
        /// </summary>
        [Category("Colors.Background")]
        public Color ColumnSeparatorColor
        {
            get => (Color)GetValue(ColumnSeparatorColorProperty);
            set => SetValue(ColumnSeparatorColorProperty, value);
        }

        public static readonly DependencyProperty ColumnSeparatorColorProperty =
            DependencyProperty.Register(nameof(ColumnSeparatorColor), typeof(Color), typeof(HexEditor),
                new PropertyMetadata(Color.FromRgb(0xD0, 0xD0, 0xD0), OnColumnSeparatorColorChanged));

        private static void OnColumnSeparatorColorChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is HexEditor editor)
            {
                var color = (Color)e.NewValue;
                editor.Resources["ColumnSeparatorBrush"] = new SolidColorBrush(color);

                if (editor.HexViewport != null)
                    editor.HexViewport.SeparatorColor = color;
            }
        }

        /// <summary>
        /// Background color for the status bar.
        /// </summary>
        [Category("Colors.Background")]
        public Color StatusBarBackgroundColor
        {
            get => (Color)GetValue(StatusBarBackgroundColorProperty);
            set => SetValue(StatusBarBackgroundColorProperty, value);
        }

        public static readonly DependencyProperty StatusBarBackgroundColorProperty =
            DependencyProperty.Register(nameof(StatusBarBackgroundColor), typeof(Color), typeof(HexEditor),
                new PropertyMetadata(Color.FromRgb(0xF5, 0xF5, 0xF5), OnStatusBarBackgroundColorChanged));

        private static void OnStatusBarBackgroundColorChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is HexEditor editor)
            {
                var color = (Color)e.NewValue;
                editor.Resources["StatusBarBrush"] = new SolidColorBrush(color);
            }
        }

        /// <summary>
        /// Foreground color for the status bar text.
        /// </summary>
        [Category("Colors.Background")]
        public Color StatusBarForegroundColor
        {
            get => (Color)GetValue(StatusBarForegroundColorProperty);
            set => SetValue(StatusBarForegroundColorProperty, value);
        }

        public static readonly DependencyProperty StatusBarForegroundColorProperty =
            DependencyProperty.Register(nameof(StatusBarForegroundColor), typeof(Color), typeof(HexEditor),
                new PropertyMetadata(Color.FromRgb(0x33, 0x33, 0x33), OnStatusBarForegroundColorChanged));

        private static void OnStatusBarForegroundColorChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is HexEditor editor)
            {
                var color = (Color)e.NewValue;
                editor.Resources["StatusBarTextBrush"] = new SolidColorBrush(color);
            }
        }

        /// <summary>
        /// Background color for the scrollbar track.
        /// </summary>
        [Category("Colors.Background")]
        public Color ScrollBarBackgroundColor
        {
            get => (Color)GetValue(ScrollBarBackgroundColorProperty);
            set => SetValue(ScrollBarBackgroundColorProperty, value);
        }

        public static readonly DependencyProperty ScrollBarBackgroundColorProperty =
            DependencyProperty.Register(nameof(ScrollBarBackgroundColor), typeof(Color), typeof(HexEditor),
                new PropertyMetadata(Color.FromRgb(0xF0, 0xF0, 0xF0), OnScrollBarBackgroundColorChanged));

        private static void OnScrollBarBackgroundColorChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is HexEditor editor)
            {
                var color = (Color)e.NewValue;
                editor.Resources["ScrollBarBrush"] = new SolidColorBrush(color);
            }
        }

        /// <summary>
        /// Color for the scrollbar thumb (draggable handle).
        /// </summary>
        [Category("Colors.Background")]
        public Color ScrollBarThumbColor
        {
            get => (Color)GetValue(ScrollBarThumbColorProperty);
            set => SetValue(ScrollBarThumbColorProperty, value);
        }

        public static readonly DependencyProperty ScrollBarThumbColorProperty =
            DependencyProperty.Register(nameof(ScrollBarThumbColor), typeof(Color), typeof(HexEditor),
                new PropertyMetadata(Color.FromRgb(0xCD, 0xCD, 0xCD), OnScrollBarThumbColorChanged));

        private static void OnScrollBarThumbColorChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is HexEditor editor)
            {
                var color = (Color)e.NewValue;
                editor.Resources["ScrollBarThumbBrush"] = new SolidColorBrush(color);
            }
        }

        /// <summary>
        /// Foreground color for the ASCII panel text.
        /// </summary>
        [Category("Colors.Foreground")]
        public Color AsciiForegroundColor
        {
            get => (Color)GetValue(AsciiForegroundColorProperty);
            set => SetValue(AsciiForegroundColorProperty, value);
        }

        public static readonly DependencyProperty AsciiForegroundColorProperty =
            DependencyProperty.Register(nameof(AsciiForegroundColor), typeof(Color), typeof(HexEditor),
                new PropertyMetadata(Color.FromRgb(0x42, 0x42, 0x42), OnAsciiForegroundColorChanged));

        private static void OnAsciiForegroundColorChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is HexEditor editor)
            {
                var color = (Color)e.NewValue;
                if (editor.HexViewport != null)
                    editor.HexViewport.AsciiForegroundColor = color;
            }
        }

        #endregion
    }
}
