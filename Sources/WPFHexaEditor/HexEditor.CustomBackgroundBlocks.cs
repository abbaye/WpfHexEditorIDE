//////////////////////////////////////////////
// Apache 2.0  - 2026
// Author : Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.5
//////////////////////////////////////////////

using System.Collections.Generic;
using System.Linq;
using System.Windows;
using WpfHexaEditor.Core;

namespace WpfHexaEditor
{
    /// <summary>
    /// HexEditor partial class - Custom Background Blocks
    /// Contains methods for managing custom background highlighting
    /// </summary>
    public partial class HexEditor
    {
        #region Custom Background Blocks

        private readonly List<Core.CustomBackgroundBlock> _customBackgroundBlocks = new List<Core.CustomBackgroundBlock>();

        /// <summary>
        /// Enable or disable custom background blocks
        /// </summary>
        public bool AllowCustomBackgroundBlock
        {
            get => (bool)GetValue(AllowCustomBackgroundBlockProperty);
            set => SetValue(AllowCustomBackgroundBlockProperty, value);
        }

        /// <summary>
        /// Get the list of custom background blocks
        /// </summary>
        public List<Core.CustomBackgroundBlock> CustomBackgroundBlockItems => _customBackgroundBlocks;

        #endregion

        #region Public Methods - Custom Background Blocks

        /// <summary>
        /// Add a custom background block
        /// </summary>
        public void AddCustomBackgroundBlock(Core.CustomBackgroundBlock block)
        {
            if (block == null) return;
            _customBackgroundBlocks.Add(block);

            // Sync with HexViewport for rendering
            if (HexViewport != null)
            {
                HexViewport.CustomBackgroundBlocks = _customBackgroundBlocks;
                HexViewport.InvalidateVisual();
            }
        }

        /// <summary>
        /// Remove a custom background block
        /// </summary>
        public void RemoveCustomBackgroundBlock(Core.CustomBackgroundBlock block)
        {
            if (block == null) return;
            _customBackgroundBlocks.Remove(block);

            // Sync with HexViewport for rendering
            if (HexViewport != null)
            {
                HexViewport.CustomBackgroundBlocks = _customBackgroundBlocks;
                HexViewport.InvalidateVisual();
            }
        }

        /// <summary>
        /// Clear all custom background blocks
        /// </summary>
        public void ClearCustomBackgroundBlock()
        {
            _customBackgroundBlocks.Clear();

            // Sync with HexViewport for rendering
            if (HexViewport != null)
            {
                HexViewport.CustomBackgroundBlocks = _customBackgroundBlocks;
                HexViewport.InvalidateVisual();
            }
        }

        /// <summary>
        /// Get custom background block at position
        /// </summary>
        public Core.CustomBackgroundBlock GetCustomBackgroundBlock(long position)
        {
            return _customBackgroundBlocks.FirstOrDefault(b =>
                position >= b.StartOffset && position < b.StopOffset);
        }

        /// <summary>
        /// Get all custom background blocks at position
        /// </summary>
        public IEnumerable<Core.CustomBackgroundBlock> GetCustomBackgroundBlocks(long position)
        {
            return _customBackgroundBlocks.Where(b =>
                position >= b.StartOffset && position < b.StopOffset);
        }

        #endregion
    }
}
