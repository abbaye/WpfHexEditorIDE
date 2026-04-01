// ==========================================================
// Project: WpfHexEditor.Core
// File: DoubleExtension.cs
// Author: Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude (Anthropic)
// Created: 2026-03-06
// Description:
//     Extension method for the double type providing a concise Round() helper
//     that defaults to 2 decimal places, used throughout the hex editor for
//     display value formatting and layout calculations.
//
// Architecture Notes:
//     Minimal static extension — single method wrapping Math.Round.
//     No WPF dependencies.
//
// ==========================================================

using System;

namespace WpfHexEditor.Core.Extensions
{
    public static class DoubleExtension
    {
        public static double Round(this double s, int digit = 2) => Math.Round(s, digit);
    }
}
