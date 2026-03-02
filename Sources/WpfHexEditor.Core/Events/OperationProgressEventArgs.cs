//////////////////////////////////////////////
// Apache 2.0  - 2026
// Author : Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.5, Claude Sonnet 4.6
//////////////////////////////////////////////

using System;

namespace WpfHexEditor.Core.Events
{
    /// <summary>
    /// Event arguments for long-running operation progress updates
    /// </summary>
    public class OperationProgressEventArgs : EventArgs
    {
        /// <summary>
        /// Title of the operation being performed
        /// </summary>
        public string OperationTitle { get; set; }

        /// <summary>
        /// Current status message describing what's happening
        /// </summary>
        public string StatusMessage { get; set; }

        /// <summary>
        /// Progress percentage (0-100)
        /// </summary>
        public int ProgressPercentage { get; set; }

        /// <summary>
        /// Whether the progress bar should be indeterminate (no percentage)
        /// </summary>
        public bool IsIndeterminate { get; set; }

        /// <summary>
        /// Whether the operation can be cancelled by the user
        /// </summary>
        public bool CanCancel { get; set; }
    }
}
