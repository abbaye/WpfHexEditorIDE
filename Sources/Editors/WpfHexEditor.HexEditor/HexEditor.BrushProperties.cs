//////////////////////////////////////////////
// GNU Affero General Public License v3.0 - 2026
// Author : Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.5, Claude Sonnet 4.6
//////////////////////////////////////////////
// Project: WpfHexEditor.HexEditor
// File: HexEditor.BrushProperties.cs
// Description: V1-compatibility Brush wrapper properties (delegates to Color-based DPs).
// Architecture notes: Partial class extracted from HexEditor.xaml.cs
//////////////////////////////////////////////

using System;
using System.ComponentModel;
using System.Windows.Media;

namespace WpfHexEditor.HexEditor
{
    public partial class HexEditor
    {
        #region Brush Properties (Color Wrappers)

        /// <summary>
        /// Selection first color as Brush. Use <see cref="SelectionFirstColor"/> (Color) for V2 code.
        /// </summary>
        /// <remarks>
        /// This property is provided for V1 compatibility. New code should use the Color-based property.
        /// </remarks>
        [Category("Colors.Legacy")]
        [Obsolete("Use SelectionFirstColor (Color property) instead. This Brush wrapper is for V1 compatibility only.", false)]
        public Brush SelectionFirstColorBrush
        {
            get => new SolidColorBrush(SelectionFirstColor);
            set
            {
                if (value is SolidColorBrush solidBrush)
                    SelectionFirstColor = solidBrush.Color;
            }
        }

        /// <summary>
        /// Selection second color as Brush. Use SelectionSecondColor (Color) for V2 code.
        /// </summary>
        [Category("Colors.Legacy")]
        public Brush SelectionSecondColorBrush
        {
            get => new SolidColorBrush(SelectionSecondColor);
            set
            {
                if (value is SolidColorBrush solidBrush)
                    SelectionSecondColor = solidBrush.Color;
            }
        }

        /// <summary>
        /// Modified byte color as Brush. Use ByteModifiedColor (Color) for V2 code.
        /// </summary>
        [Category("Colors.Legacy")]
        public Brush ByteModifiedColorBrush
        {
            get => new SolidColorBrush(ByteModifiedColor);
            set
            {
                if (value is SolidColorBrush solidBrush)
                    ByteModifiedColor = solidBrush.Color;
            }
        }

        /// <summary>
        /// Added byte color as Brush. Use ByteAddedColor (Color) for V2 code.
        /// </summary>
        [Category("Colors.Legacy")]
        public Brush ByteAddedColorBrush
        {
            get => new SolidColorBrush(ByteAddedColor);
            set
            {
                if (value is SolidColorBrush solidBrush)
                    ByteAddedColor = solidBrush.Color;
            }
        }

        /// <summary>
        /// Highlight color as Brush. Use HighLightColor (Color) for V2 code.
        /// </summary>
        [Category("Colors.Legacy")]
        public Brush HighLightColorBrush
        {
            get => new SolidColorBrush(HighLightColor);
            set
            {
                if (value is SolidColorBrush solidBrush)
                    HighLightColor = solidBrush.Color;
            }
        }

        /// <summary>
        /// Mouse over color as Brush. Use MouseOverColor (Color) for V2 code.
        /// </summary>
        [Category("Colors.Legacy")]
        public Brush MouseOverColorBrush
        {
            get => new SolidColorBrush(MouseOverColor);
            set
            {
                if (value is SolidColorBrush solidBrush)
                    MouseOverColor = solidBrush.Color;
            }
        }

        /// <summary>
        /// Foreground second color as Brush. Use ForegroundSecondColor (Color) for V2 code.
        /// </summary>
        [Category("Colors.Legacy")]
        public Brush ForegroundSecondColorBrush
        {
            get => new SolidColorBrush(ForegroundSecondColor);
            set
            {
                if (value is SolidColorBrush solidBrush)
                    ForegroundSecondColor = solidBrush.Color;
            }
        }

        /// <summary>
        /// Offset header foreground color as Brush. Use ForegroundOffSetHeaderColor (Color) for V2 code.
        /// </summary>
        [Category("Colors.Legacy")]
        public Brush ForegroundOffSetHeaderColorBrush
        {
            get => new SolidColorBrush(ForegroundOffSetHeaderColor);
            set
            {
                if (value is SolidColorBrush solidBrush)
                    ForegroundOffSetHeaderColor = solidBrush.Color;
            }
        }

        /// <summary>
        /// Highlighted offset header foreground color as Brush. Use ForegroundHighLightOffSetHeaderColor (Color) for V2 code.
        /// </summary>
        [Category("Colors.Legacy")]
        public Brush ForegroundHighLightOffSetHeaderColorBrush
        {
            get => new SolidColorBrush(ForegroundHighLightOffSetHeaderColor);
            set
            {
                if (value is SolidColorBrush solidBrush)
                    ForegroundHighLightOffSetHeaderColor = solidBrush.Color;
            }
        }

        /// <summary>
        /// Foreground contrast color as Brush. Use ForegroundContrast (Color) for V2 code.
        /// </summary>
        [Category("Colors.Legacy")]
        public Brush ForegroundContrastBrush
        {
            get => new SolidColorBrush(ForegroundContrast);
            set
            {
                if (value is SolidColorBrush solidBrush)
                    ForegroundContrast = solidBrush.Color;
            }
        }

        #endregion
    }
}
