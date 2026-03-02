//////////////////////////////////////////////
// Apache 2.0  - 2026
// Author : Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.5, Claude Sonnet 4.6
//////////////////////////////////////////////

using System;

namespace WpfHexEditor.Core.Events
{
    /// <summary>
    /// Event arguments for long-running operation completion
    /// </summary>
    public class OperationCompletedEventArgs : EventArgs
    {
        /// <summary>
        /// Whether the operation completed successfully
        /// </summary>
        public bool Success { get; set; }

        /// <summary>
        /// Error message if the operation failed
        /// </summary>
        public string ErrorMessage { get; set; }

        /// <summary>
        /// Whether the operation was cancelled by the user
        /// </summary>
        public bool WasCancelled { get; set; }
    }
}
