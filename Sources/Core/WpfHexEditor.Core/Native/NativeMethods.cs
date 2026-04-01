// ==========================================================
// Project: WpfHexEditor.Core
// File: NativeMethods.cs
// Author: Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude (Anthropic)
// Created: 2026-03-06
// Description:
//     P/Invoke declarations for Windows API methods used internally by the hex
//     editor, specifically GetKeyboardState and ToUnicode for virtual key code
//     to character conversion in KeyValidator.
//
// Architecture Notes:
//     Internal static class following the NativeMethods naming convention for
//     FxCop compliance. Only exposes the minimal Win32 surface required.
//     Platform-specific: Windows only.
//
// ==========================================================

using System.Runtime.InteropServices;
using System.Text;

namespace WpfHexEditor.Core.Native
{
    /// <summary>
    /// Used for key detection
    /// </summary>
    internal static class NativeMethods
    {
        internal enum MapType : uint
        {
            MapvkVkToVsc = 0x0,
            MapvkVscToVk = 0x1,
            MapvkVkToChar = 0x2,
            MapvkVscToVkEx = 0x3,
        }

        [DllImport("user32.dll")]
        internal static extern int ToUnicode(uint wVirtKey,
                                             uint wScanCode,
                                             byte[] lpKeyState,
                                             [Out, MarshalAs(UnmanagedType.LPWStr, SizeParamIndex = 4)] StringBuilder pwszBuff,
                                             int cchBuff,
                                             uint wFlags);

        [DllImport("user32.dll")]
        internal static extern bool GetKeyboardState(byte[] lpKeyState);

        [DllImport("user32.dll")]
        internal static extern uint MapVirtualKey(uint uCode, MapType uMapType);
    }
}
