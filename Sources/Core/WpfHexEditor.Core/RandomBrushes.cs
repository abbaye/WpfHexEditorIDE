// ==========================================================
// Project: WpfHexEditor.Core
// File: RandomBrushes.cs
// Author: Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude (Anthropic)
// Created: 2026-03-06
// Description:
//     Provides a static collection of named WPF SolidColorBrushes sourced from
//     the Colors class via reflection, enabling random or sequential color
//     assignment for bookmark groups, overlays, and custom background blocks.
//
// Architecture Notes:
//     Uses reflection on System.Windows.Media.Colors to enumerate all named
//     colors. Contains WPF Brush dependencies. Consumed by BookmarkService and
//     CustomBackgroundService for color assignment.
//
// ==========================================================

using System;
using System.Reflection;
using System.Windows.Media;

namespace WpfHexEditor.Core
{
    public static class RandomBrushes
    {
        /// <summary>
        /// Pick a random bruch
        /// </summary>
        public static SolidColorBrush PickBrush()
        {
            var properties = typeof(Brushes).GetProperties();

            return (SolidColorBrush)properties
                [
                    new Random().Next(properties.Length)
                ].GetValue(null, null);
        }
    }
}
