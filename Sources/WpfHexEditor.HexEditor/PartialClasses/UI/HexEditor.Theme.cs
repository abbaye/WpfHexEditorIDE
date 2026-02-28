//////////////////////////////////////////////
// Apache 2.0  - 2026
// Author : Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Opus 4.6
//////////////////////////////////////////////

using System.Windows;
using System.Windows.Media;

namespace WpfHexEditor.HexEditor
{
    public partial class HexEditor
    {
        /// <summary>
        /// Applies theme colors by reading HexEditor_* keys from Application.Current.Resources.
        /// Call this after a theme change to update all HexEditor colors.
        ///
        /// Expected resource keys (all of type Color):
        ///   HexEditor_BackgroundColor, HexEditor_HeaderBackgroundColor,
        ///   HexEditor_HeaderForegroundColor, HexEditor_ColumnSeparatorColor,
        ///   HexEditor_ForegroundFirstColor, HexEditor_ForegroundSecondColor,
        ///   HexEditor_ForegroundOffSetHeaderColor, HexEditor_ForegroundHighLightOffSetHeaderColor,
        ///   HexEditor_ForegroundContrast,
        ///   HexEditor_SelectionFirstColor, HexEditor_SelectionSecondColor,
        ///   HexEditor_ByteModifiedColor, HexEditor_ByteAddedColor,
        ///   HexEditor_HighLightColor, HexEditor_MouseOverColor,
        ///   HexEditor_TblDteColor, HexEditor_TblMteColor,
        ///   HexEditor_TblEndBlockColor, HexEditor_TblEndLineColor,
        ///   HexEditor_StatusBarBackgroundColor, HexEditor_StatusBarForegroundColor,
        ///   HexEditor_ScrollBarBackgroundColor, HexEditor_ScrollBarThumbColor
        /// </summary>
        public void ApplyThemeFromResources()
        {
            var app = Application.Current;
            if (app == null) return;

            // Background colors
            BackgroundColor = GetThemeColor(app, "HexEditor_BackgroundColor", BackgroundColor);
            HeaderBackgroundColor = GetThemeColor(app, "HexEditor_HeaderBackgroundColor", HeaderBackgroundColor);
            HeaderForegroundColor = GetThemeColor(app, "HexEditor_HeaderForegroundColor", HeaderForegroundColor);
            ColumnSeparatorColor = GetThemeColor(app, "HexEditor_ColumnSeparatorColor", ColumnSeparatorColor);

            // StatusBar colors
            StatusBarBackgroundColor = GetThemeColor(app, "HexEditor_StatusBarBackgroundColor", StatusBarBackgroundColor);
            StatusBarForegroundColor = GetThemeColor(app, "HexEditor_StatusBarForegroundColor", StatusBarForegroundColor);

            // ScrollBar colors
            ScrollBarBackgroundColor = GetThemeColor(app, "HexEditor_ScrollBarBackgroundColor", ScrollBarBackgroundColor);
            ScrollBarThumbColor = GetThemeColor(app, "HexEditor_ScrollBarThumbColor", ScrollBarThumbColor);

            // Foreground colors
            ForegroundFirstColor = GetThemeColor(app, "HexEditor_ForegroundFirstColor", ForegroundFirstColor);
            ForegroundSecondColor = GetThemeColor(app, "HexEditor_ForegroundSecondColor", ForegroundSecondColor);
            AsciiForegroundColor = GetThemeColor(app, "HexEditor_AsciiForegroundColor", AsciiForegroundColor);
            ForegroundOffSetHeaderColor = GetThemeColor(app, "HexEditor_ForegroundOffSetHeaderColor", ForegroundOffSetHeaderColor);
            ForegroundHighLightOffSetHeaderColor = GetThemeColor(app, "HexEditor_ForegroundHighLightOffSetHeaderColor", ForegroundHighLightOffSetHeaderColor);
            ForegroundContrast = GetThemeColor(app, "HexEditor_ForegroundContrast", ForegroundContrast);

            // Selection colors
            SelectionFirstColor = GetThemeColor(app, "HexEditor_SelectionFirstColor", SelectionFirstColor);
            SelectionSecondColor = GetThemeColor(app, "HexEditor_SelectionSecondColor", SelectionSecondColor);

            // Byte state colors
            ByteModifiedColor = GetThemeColor(app, "HexEditor_ByteModifiedColor", ByteModifiedColor);
            ByteAddedColor = GetThemeColor(app, "HexEditor_ByteAddedColor", ByteAddedColor);
            HighLightColor = GetThemeColor(app, "HexEditor_HighLightColor", HighLightColor);
            MouseOverColor = GetThemeColor(app, "HexEditor_MouseOverColor", MouseOverColor);

            // TBL colors
            TblDteColor = GetThemeColor(app, "HexEditor_TblDteColor", TblDteColor);
            TblMteColor = GetThemeColor(app, "HexEditor_TblMteColor", TblMteColor);
            TblEndBlockColor = GetThemeColor(app, "HexEditor_TblEndBlockColor", TblEndBlockColor);
            TblEndLineColor = GetThemeColor(app, "HexEditor_TblEndLineColor", TblEndLineColor);
        }

        private static Color GetThemeColor(Application app, string key, Color fallback)
        {
            try
            {
                var resource = app.FindResource(key);
                if (resource is Color color)
                    return color;
            }
            catch
            {
                // Resource not found — use fallback
            }

            return fallback;
        }
    }
}
