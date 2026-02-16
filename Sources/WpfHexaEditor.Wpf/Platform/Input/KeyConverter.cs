//////////////////////////////////////////////
// Apache 2.0  - 2026
// Author : Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.5
//////////////////////////////////////////////

using System.Windows.Input;
using WpfHexaEditor.Core.Platform.Input;

namespace WpfHexaEditor.Wpf.Platform.Input
{
    /// <summary>
    /// Converts between WPF Key and PlatformKey.
    /// </summary>
    public static class KeyConverter
    {
        /// <summary>
        /// Converts a WPF Key to PlatformKey.
        /// </summary>
        public static PlatformKey ToPlatformKey(Key key)
        {
            return key switch
            {
                // Letters
                Key.A => PlatformKey.A, Key.B => PlatformKey.B, Key.C => PlatformKey.C,
                Key.D => PlatformKey.D, Key.E => PlatformKey.E, Key.F => PlatformKey.F,
                Key.G => PlatformKey.G, Key.H => PlatformKey.H, Key.I => PlatformKey.I,
                Key.J => PlatformKey.J, Key.K => PlatformKey.K, Key.L => PlatformKey.L,
                Key.M => PlatformKey.M, Key.N => PlatformKey.N, Key.O => PlatformKey.O,
                Key.P => PlatformKey.P, Key.Q => PlatformKey.Q, Key.R => PlatformKey.R,
                Key.S => PlatformKey.S, Key.T => PlatformKey.T, Key.U => PlatformKey.U,
                Key.V => PlatformKey.V, Key.W => PlatformKey.W, Key.X => PlatformKey.X,
                Key.Y => PlatformKey.Y, Key.Z => PlatformKey.Z,

                // Numbers
                Key.D0 => PlatformKey.D0, Key.D1 => PlatformKey.D1, Key.D2 => PlatformKey.D2,
                Key.D3 => PlatformKey.D3, Key.D4 => PlatformKey.D4, Key.D5 => PlatformKey.D5,
                Key.D6 => PlatformKey.D6, Key.D7 => PlatformKey.D7, Key.D8 => PlatformKey.D8,
                Key.D9 => PlatformKey.D9,

                // Function keys
                Key.F1 => PlatformKey.F1, Key.F2 => PlatformKey.F2, Key.F3 => PlatformKey.F3,
                Key.F4 => PlatformKey.F4, Key.F5 => PlatformKey.F5, Key.F6 => PlatformKey.F6,
                Key.F7 => PlatformKey.F7, Key.F8 => PlatformKey.F8, Key.F9 => PlatformKey.F9,
                Key.F10 => PlatformKey.F10, Key.F11 => PlatformKey.F11, Key.F12 => PlatformKey.F12,

                // Navigation
                Key.Left => PlatformKey.Left, Key.Right => PlatformKey.Right,
                Key.Up => PlatformKey.Up, Key.Down => PlatformKey.Down,
                Key.Home => PlatformKey.Home, Key.End => PlatformKey.End,
                Key.PageUp => PlatformKey.PageUp, Key.PageDown => PlatformKey.PageDown,

                // Editing (Key.Enter and Key.Return are the same in WPF)
                Key.Enter => PlatformKey.Enter,
                Key.Back => PlatformKey.Back,
                Key.Delete => PlatformKey.Delete,
                Key.Tab => PlatformKey.Tab,
                Key.Space => PlatformKey.Space,
                Key.Escape => PlatformKey.Escape,

                // Modifiers
                Key.LeftShift => PlatformKey.LeftShift, Key.RightShift => PlatformKey.RightShift,
                Key.LeftCtrl => PlatformKey.LeftCtrl, Key.RightCtrl => PlatformKey.RightCtrl,
                Key.LeftAlt => PlatformKey.LeftAlt, Key.RightAlt => PlatformKey.RightAlt,

                // Numpad
                Key.NumPad0 => PlatformKey.NumPad0, Key.NumPad1 => PlatformKey.NumPad1,
                Key.NumPad2 => PlatformKey.NumPad2, Key.NumPad3 => PlatformKey.NumPad3,
                Key.NumPad4 => PlatformKey.NumPad4, Key.NumPad5 => PlatformKey.NumPad5,
                Key.NumPad6 => PlatformKey.NumPad6, Key.NumPad7 => PlatformKey.NumPad7,
                Key.NumPad8 => PlatformKey.NumPad8, Key.NumPad9 => PlatformKey.NumPad9,

                // Special
                Key.Insert => PlatformKey.Insert, Key.Apps => PlatformKey.Apps,
                Key.Scroll => PlatformKey.Scroll, Key.Pause => PlatformKey.Pause,
                Key.CapsLock => PlatformKey.CapsLock, Key.NumLock => PlatformKey.NumLock,

                _ => PlatformKey.None
            };
        }

        /// <summary>
        /// Converts WPF ModifierKeys to PlatformModifierKeys.
        /// </summary>
        public static PlatformModifierKeys ToPlatformModifiers(ModifierKeys modifiers)
        {
            var result = PlatformModifierKeys.None;

            if (modifiers.HasFlag(ModifierKeys.Alt))
                result |= PlatformModifierKeys.Alt;
            if (modifiers.HasFlag(ModifierKeys.Control))
                result |= PlatformModifierKeys.Control;
            if (modifiers.HasFlag(ModifierKeys.Shift))
                result |= PlatformModifierKeys.Shift;
            if (modifiers.HasFlag(ModifierKeys.Windows))
                result |= PlatformModifierKeys.Windows;

            return result;
        }
    }
}
