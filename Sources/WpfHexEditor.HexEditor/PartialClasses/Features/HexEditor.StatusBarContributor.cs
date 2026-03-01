//////////////////////////////////////////////
// Apache 2.0  - 2026
// Author : Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.6
//////////////////////////////////////////////

using System.Collections.ObjectModel;
using WpfHexEditor.Core;
using WpfHexEditor.Editor.Core;
using EditModeEnum = WpfHexEditor.Core.Models.EditMode;

namespace WpfHexEditor.HexEditor
{
    /// <summary>
    /// HexEditor partial class — IStatusBarContributor implementation.
    /// Exposes interactive status bar items (ByteSize, ByteOrder, EditMode, BytePerLine)
    /// to the host application so it can render a VS Code-style clickable status bar.
    /// Values are kept in sync via <see cref="RefreshStatusBarItemValues"/>,
    /// called by <see cref="RaiseHexStatusChanged"/> on every relevant state change.
    /// </summary>
    public partial class HexEditor : IStatusBarContributor
    {
        // ═══════════════════════════════════════════════════════════════════
        // Fields
        // ═══════════════════════════════════════════════════════════════════

        private ObservableCollection<StatusBarItem>? _statusBarItems;
        private StatusBarItem _sbByteSize    = null!;
        private StatusBarItem _sbByteOrder   = null!;
        private StatusBarItem _sbEditMode    = null!;
        private StatusBarItem _sbBytePerLine = null!;

        // ═══════════════════════════════════════════════════════════════════
        // IStatusBarContributor
        // ═══════════════════════════════════════════════════════════════════

        public ObservableCollection<StatusBarItem> StatusBarItems
            => _statusBarItems ??= BuildStatusBarItems();

        // ═══════════════════════════════════════════════════════════════════
        // Building items (lazy, called once on first access)
        // ═══════════════════════════════════════════════════════════════════

        private ObservableCollection<StatusBarItem> BuildStatusBarItems()
        {
            // ── Byte size ──────────────────────────────────────────────────
            _sbByteSize = new StatusBarItem
            {
                Label   = "Byte size",
                Tooltip = "Click to change byte size"
            };
            _sbByteSize.Choices.Add(new StatusBarChoice
            {
                DisplayName = "8 bit",
                Command     = new HexEditorRelayCommand(_ => ByteSize = ByteSizeType.Bit8)
            });
            _sbByteSize.Choices.Add(new StatusBarChoice
            {
                DisplayName = "16 bit",
                Command     = new HexEditorRelayCommand(_ => ByteSize = ByteSizeType.Bit16)
            });
            _sbByteSize.Choices.Add(new StatusBarChoice
            {
                DisplayName = "32 bit",
                Command     = new HexEditorRelayCommand(_ => ByteSize = ByteSizeType.Bit32)
            });

            // ── Byte order ─────────────────────────────────────────────────
            _sbByteOrder = new StatusBarItem
            {
                Label   = "Byte order",
                Tooltip = "Click to toggle byte order"
            };
            _sbByteOrder.Choices.Add(new StatusBarChoice
            {
                DisplayName = "Lo-Hi",
                Command     = new HexEditorRelayCommand(_ => ByteOrder = ByteOrderType.LoHi)
            });
            _sbByteOrder.Choices.Add(new StatusBarChoice
            {
                DisplayName = "Hi-Lo",
                Command     = new HexEditorRelayCommand(_ => ByteOrder = ByteOrderType.HiLo)
            });

            // ── Edit mode ──────────────────────────────────────────────────
            _sbEditMode = new StatusBarItem
            {
                Label   = "Mode",
                Tooltip = "Click to toggle edit mode"
            };
            _sbEditMode.Choices.Add(new StatusBarChoice
            {
                DisplayName = "Overwrite",
                Command     = new HexEditorRelayCommand(_ => EditMode = EditModeEnum.Overwrite)
            });
            _sbEditMode.Choices.Add(new StatusBarChoice
            {
                DisplayName = "Insert",
                Command     = new HexEditorRelayCommand(_ => EditMode = EditModeEnum.Insert)
            });

            // ── Bytes per line ─────────────────────────────────────────────
            _sbBytePerLine = new StatusBarItem
            {
                Label   = "Bytes/line",
                Tooltip = "Click to change bytes per line"
            };
            foreach (var n in new[] { 8, 16, 24, 32 })
            {
                var capture = n;
                _sbBytePerLine.Choices.Add(new StatusBarChoice
                {
                    DisplayName = capture.ToString(),
                    Command     = new HexEditorRelayCommand(_ => BytePerLine = capture)
                });
            }

            // Initialise values from current DP state
            RefreshStatusBarItemValues();

            return new ObservableCollection<StatusBarItem>
            {
                _sbByteSize,
                _sbByteOrder,
                _sbEditMode,
                _sbBytePerLine
            };
        }

        // ═══════════════════════════════════════════════════════════════════
        // Refresh (called by RaiseHexStatusChanged on every state change)
        // ═══════════════════════════════════════════════════════════════════

        /// <summary>
        /// Syncs all status bar item values and IsActive flags with current DP state.
        /// No-op until the items have been built (i.e. until the host first accesses
        /// <see cref="IStatusBarContributor.StatusBarItems"/>).
        /// </summary>
        internal void RefreshStatusBarItemValues()
        {
            if (_statusBarItems == null) return;

            // Byte size
            var bsLabel = ByteSize switch
            {
                ByteSizeType.Bit8  => "8 bit",
                ByteSizeType.Bit16 => "16 bit",
                ByteSizeType.Bit32 => "32 bit",
                _                  => ByteSize.ToString()
            };
            _sbByteSize.Value = bsLabel;
            foreach (var c in _sbByteSize.Choices) c.IsActive = c.DisplayName == bsLabel;

            // Byte order
            var boLabel = ByteOrder == ByteOrderType.LoHi ? "Lo-Hi" : "Hi-Lo";
            _sbByteOrder.Value = boLabel;
            foreach (var c in _sbByteOrder.Choices) c.IsActive = c.DisplayName == boLabel;

            // Edit mode — read DP into local to avoid property/type name ambiguity
            var currentEditMode = EditMode;
            var emLabel = currentEditMode == EditModeEnum.Insert ? "Insert" : "Overwrite";
            _sbEditMode.Value = emLabel;
            foreach (var c in _sbEditMode.Choices) c.IsActive = c.DisplayName == emLabel;

            // Bytes per line
            var bplLabel = BytePerLine.ToString();
            _sbBytePerLine.Value = bplLabel;
            foreach (var c in _sbBytePerLine.Choices) c.IsActive = c.DisplayName == bplLabel;
        }
    }
}
