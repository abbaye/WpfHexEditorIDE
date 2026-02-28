//////////////////////////////////////////////
// Apache 2.0  - 2016-2026
// Author : Derek Tremblay (derektremblay666@gmail.com)
//////////////////////////////////////////////

using System.Windows.Input;

namespace WpfHexEditor.HexBox.Core
{
    /// <summary>
    /// Minimal key validation utilities for the HexBox control.
    /// Extracted subset of WpfHexaEditor.Core.KeyValidator.
    /// </summary>
    internal static class HexKeyValidator
    {
        /// <summary>
        /// Returns true if the key is a valid hexadecimal digit (0-9, A-F, numpad).
        /// </summary>
        public static bool IsHexKey(Key key) =>
            key == Key.A || key == Key.B || key == Key.C ||
            key == Key.D || key == Key.E || key == Key.F ||
            IsNumericKey(key);

        private static bool IsNumericKey(Key key) =>
            key == Key.D0 || key == Key.D1 || key == Key.D2 || key == Key.D3 || key == Key.D4 ||
            key == Key.D5 || key == Key.D6 || key == Key.D7 || key == Key.D8 || key == Key.D9 ||
            key == Key.NumPad0 || key == Key.NumPad1 || key == Key.NumPad2 || key == Key.NumPad3 ||
            key == Key.NumPad4 || key == Key.NumPad5 || key == Key.NumPad6 || key == Key.NumPad7 ||
            key == Key.NumPad8 || key == Key.NumPad9;

        public static bool IsBackspaceKey(Key key) => key == Key.Back;

        public static bool IsDeleteKey(Key key) => key == Key.Delete;

        public static bool IsArrowKey(Key key) =>
            key == Key.Up || key == Key.Down || key == Key.Left || key == Key.Right;

        public static bool IsTabKey(Key key) => key == Key.Tab;

        public static bool IsEnterKey(Key key) => key == Key.Enter;
    }
}
