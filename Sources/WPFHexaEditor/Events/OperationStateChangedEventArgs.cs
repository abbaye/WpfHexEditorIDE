//////////////////////////////////////////////
// Apache 2.0  - 2026
// Author : Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.5
//////////////////////////////////////////////

using System;

namespace WpfHexaEditor.Events
{
    /// <summary>
    /// Event arguments for operation state changes
    /// Raised when a long-running async operation starts or completes
    /// </summary>
    public class OperationStateChangedEventArgs : EventArgs
    {
        /// <summary>
        /// Whether a long-running operation is currently active
        /// </summary>
        public bool IsActive { get; }

        /// <summary>
        /// Creates new OperationStateChangedEventArgs
        /// </summary>
        /// <param name="isActive">Whether an operation is active</param>
        public OperationStateChangedEventArgs(bool isActive)
        {
            IsActive = isActive;
        }
    }
}
