// ==========================================================
// Project: WpfHexEditor.HexEditor
// File: HexEditor.BatchOperations.cs
// Author: Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude (Anthropic)
// Created: 2026-03-06
// Description:
//     Partial class containing batch operation methods for the HexEditor.
//     Provides BeginBatch/EndBatch API to group multiple byte edits into a
//     single undo-able operation, improving performance for bulk modifications.
//
// Architecture Notes:
//     Batch mode suppresses intermediate UI refreshes and changeset commits.
//     EndBatch flushes the accumulated changes as a single atomic changeset.
//
// ==========================================================

namespace WpfHexEditor.HexEditor
{
    /// <summary>
    /// HexEditor partial class - Batch Operations
    /// Contains methods for optimizing multiple edits with batch mode
    /// </summary>
    public partial class HexEditor
    {
        #region Public Methods - Batch Operations

        /// <summary>
        /// Begin batch operation mode for improved performance with multiple edits
        /// </summary>
        /// <remarks>
        /// When making many sequential modifications, wrapping them in BeginBatch/EndBatch
        /// significantly improves performance by:
        /// - Deferring UI updates until EndBatch
        /// - Grouping multiple edits into a single undo entry
        /// - Reducing memory allocations
        /// - Minimizing event notifications
        ///
        /// Example usage:
        /// <code>
        /// editor.BeginBatch();
        /// try
        /// {
        ///     for (int i = 0; i &lt; 1000; i++)
        ///         editor.ModifyByte(0xFF, i);
        /// }
        /// finally
        /// {
        ///     editor.EndBatch();
        /// }
        /// </code>
        ///
        /// Important:
        /// - Always call EndBatch() after BeginBatch(), preferably in a finally block
        /// - Batch operations can be nested (internal counter tracks depth)
        /// - UI updates are deferred until the outermost EndBatch() completes
        /// </remarks>
        public void BeginBatch()
        {
            if (_viewModel?.Provider != null)
            {
                _viewModel.Provider.BeginBatch();
            }
        }

        /// <summary>
        /// End batch operation mode and apply all deferred changes
        /// </summary>
        /// <remarks>
        /// This method:
        /// - Applies all deferred cache invalidations
        /// - Triggers change notifications
        /// - Updates the viewport display
        ///
        /// Must be called after BeginBatch() to complete the batch operation.
        ///
        /// See BeginBatch() for usage example.
        /// </remarks>
        public void EndBatch()
        {
            if (_viewModel?.Provider != null)
            {
                _viewModel.Provider.EndBatch();

                // Trigger UI update after batch completes
                UpdateVisibleLines();
            }
        }

        #endregion
    }
}
