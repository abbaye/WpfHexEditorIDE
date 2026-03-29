// ==========================================================
// Project: WpfHexEditor.Core
// File: WithMethodExtention.cs
// Author: Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude (Anthropic)
// Created: 2026-03-06
// Description:
//     Extension method providing a C#-style emulation of the VB.NET "With"
//     statement, enabling concise object initialization and fluent configuration
//     chains without introducing local variables.
//
// Architecture Notes:
//     Single generic extension method — no state, no WPF dependencies.
//     Used in configuration and builder patterns across the codebase.
//
// ==========================================================

using System;

namespace WpfHexEditor.Core.Extensions
{
    public static class WithMethodExtention
    {
        /// <summary>
        /// C# like of the very good VB With statement
        /// </summary>
        public static void With<T>(this T obj, Action<T> act) => act(obj);
    }
}
