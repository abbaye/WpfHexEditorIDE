// ==========================================================
// Project: WpfHexEditor.Core
// File: ByteDifferenceEventArgs.cs
// Author: Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude (Anthropic)
// Created: 2026-03-06
// Description:
//     Custom EventArgs carrying a ByteDifference payload for events raised
//     during file comparison operations, allowing subscribers to receive
//     source, destination, and offset information.
//
// Architecture Notes:
//     Located in Events; mirrored in Events/.
//     Pure event data container — no WPF dependencies.
//
// ==========================================================

using System;
using WpfHexEditor.Core.Bytes;

namespace WpfHexEditor.Core.Events
{
    /// <summary>
    /// Custom event arguments used for pass somes informations to delegate
    /// </summary>
    public class ByteDifferenceEventArgs : EventArgs
    {
        public ByteDifferenceEventArgs() { }

        public ByteDifferenceEventArgs(ByteDifference byteDifference) => ByteDiff = byteDifference;

        /// <summary>
        /// ByteDifference to pass in arguments
        /// /// </summary>
        public ByteDifference ByteDiff;
    }
}
