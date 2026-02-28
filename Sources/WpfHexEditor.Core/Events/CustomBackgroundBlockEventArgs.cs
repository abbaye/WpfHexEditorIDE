//////////////////////////////////////////////
// Apache 2.0  - 2026
// Contributors: Claude Sonnet 4.5
//////////////////////////////////////////////

using System;
using System.Collections.Generic;
using WpfHexEditor.Core;

namespace WpfHexEditor.Core.Events
{
    /// <summary>
    /// Describes the type of change that occurred to custom background blocks
    /// </summary>
    public enum BlockChangeType
    {
        /// <summary>A single block was added</summary>
        Added,

        /// <summary>Multiple blocks were added</summary>
        AddedMultiple,

        /// <summary>A single block was removed</summary>
        Removed,

        /// <summary>Multiple blocks were removed</summary>
        RemovedMultiple,

        /// <summary>All blocks were cleared</summary>
        Cleared
    }

    /// <summary>
    /// Event arguments for CustomBackgroundBlock changes
    /// Following the pattern from OperationProgressEventArgs
    /// </summary>
    public class CustomBackgroundBlockEventArgs : EventArgs
    {
        /// <summary>
        /// Default constructor
        /// </summary>
        public CustomBackgroundBlockEventArgs() { }

        /// <summary>
        /// Constructor for single block operations
        /// </summary>
        /// <param name="changeType">Type of change</param>
        /// <param name="block">The affected block</param>
        /// <param name="totalBlockCount">Total number of blocks after the operation</param>
        public CustomBackgroundBlockEventArgs(BlockChangeType changeType, CustomBackgroundBlock block, int totalBlockCount)
        {
            ChangeType = changeType;
            Block = block;
            AffectedCount = 1;
            TotalBlockCount = totalBlockCount;
        }

        /// <summary>
        /// Constructor for multiple block operations
        /// </summary>
        /// <param name="changeType">Type of change</param>
        /// <param name="blocks">The affected blocks</param>
        /// <param name="totalBlockCount">Total number of blocks after the operation</param>
        public CustomBackgroundBlockEventArgs(BlockChangeType changeType, IReadOnlyList<CustomBackgroundBlock> blocks, int totalBlockCount)
        {
            ChangeType = changeType;
            Blocks = blocks;
            AffectedCount = blocks?.Count ?? 0;
            TotalBlockCount = totalBlockCount;
        }

        /// <summary>
        /// The type of change that occurred
        /// </summary>
        public BlockChangeType ChangeType { get; set; }

        /// <summary>
        /// The block that was added or removed (null for bulk operations)
        /// </summary>
        public CustomBackgroundBlock Block { get; set; }

        /// <summary>
        /// Blocks affected by bulk operations (AddMultiple, RemoveMultiple)
        /// </summary>
        public IReadOnlyList<CustomBackgroundBlock> Blocks { get; set; }

        /// <summary>
        /// Number of blocks affected (useful for Cleared operations)
        /// </summary>
        public int AffectedCount { get; set; }

        /// <summary>
        /// Total number of blocks after operation
        /// </summary>
        public int TotalBlockCount { get; set; }
    }
}
