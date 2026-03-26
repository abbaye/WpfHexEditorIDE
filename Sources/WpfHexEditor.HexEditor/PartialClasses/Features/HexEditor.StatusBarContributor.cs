// ==========================================================
// Project: WpfHexEditor.HexEditor
// File: HexEditor.StatusBarContributor.cs
// Author: Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude (Anthropic)
// Created: 2026-03-06
// Description:
//     Partial class implementing IStatusBarContributor for the HexEditor.
//     Provides status bar items (current offset, selection size, edit mode,
//     encoding, file size) to the IDE's status bar infrastructure.
//
// Architecture Notes:
//     Implements IStatusBarContributor from WpfHexEditor.Editor.Core.
//     Status items refreshed on selection change, mode change, and file events.
//
// ==========================================================

using System;
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
    public partial class HexEditor : IStatusBarContributor, IRefreshTimeReporter
    {
        // ═══════════════════════════════════════════════════════════════════
        // Fields
        // ═══════════════════════════════════════════════════════════════════

        private ObservableCollection<StatusBarItem>? _statusBarItems;
        private StatusBarItem _sbFileSize       = null!;
        private StatusBarItem _sbByteSize       = null!;
        private StatusBarItem _sbByteOrder      = null!;
        private StatusBarItem _sbEditMode       = null!;
        private StatusBarItem _sbBytePerLine    = null!;
        private StatusBarItem _sbOffsetVisual   = null!;
        private StatusBarItem _sbDataVisual     = null!;
        private StatusBarItem _sbByteGrouping   = null!;
        private StatusBarItem _sbCopyMode       = null!;
        private StatusBarItem _sbRefreshTime    = null!;

        // ═══════════════════════════════════════════════════════════════════
        // IStatusBarContributor
        // ═══════════════════════════════════════════════════════════════════

        public ObservableCollection<StatusBarItem> StatusBarItems
            => _statusBarItems ??= BuildStatusBarItems();

        /// <summary>
        /// Gets the refresh time status bar item. This is NOT included in StatusBarItems
        /// because it is displayed separately on the right side of the IDE status bar.
        /// Accessing this property ensures the status bar items are initialized.
        /// </summary>
        public StatusBarItem? RefreshTimeStatusBarItem
        {
            get
            {
                // Ensure items are built so _sbRefreshTime is initialized
                if (_statusBarItems == null)
                    _ = StatusBarItems;
                return _sbRefreshTime;
            }
        }

        // ═══════════════════════════════════════════════════════════════════
        // Building items (lazy, called once on first access)
        // ═══════════════════════════════════════════════════════════════════

        private ObservableCollection<StatusBarItem> BuildStatusBarItems()
        {
            // -- File size (read-only, display only — no Choices) -----------
            _sbFileSize = new StatusBarItem
            {
                Label   = "Size",
                Tooltip = "Current file size (virtual length including pending insertions)"
            };

            // -- Byte size --------------------------------------------------
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

            // -- Byte order -------------------------------------------------
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

            // -- Edit mode --------------------------------------------------
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

            // -- Bytes per line ---------------------------------------------
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

            // -- Offset display format ------------------------------------------
            _sbOffsetVisual = new StatusBarItem
            {
                Label   = "Offset",
                Tooltip = "Click to change offset display format"
            };
            _sbOffsetVisual.Choices.Add(new StatusBarChoice
            {
                DisplayName = "Hex",
                Command     = new HexEditorRelayCommand(_ => OffSetStringVisual = DataVisualType.Hexadecimal)
            });
            _sbOffsetVisual.Choices.Add(new StatusBarChoice
            {
                DisplayName = "Dec",
                Command     = new HexEditorRelayCommand(_ => OffSetStringVisual = DataVisualType.Decimal)
            });
            _sbOffsetVisual.Choices.Add(new StatusBarChoice
            {
                DisplayName = "Bin",
                Command     = new HexEditorRelayCommand(_ => OffSetStringVisual = DataVisualType.Binary)
            });

            // -- Data display format --------------------------------------------
            _sbDataVisual = new StatusBarItem
            {
                Label   = "Data",
                Tooltip = "Click to change byte data display format"
            };
            _sbDataVisual.Choices.Add(new StatusBarChoice
            {
                DisplayName = "Hex",
                Command     = new HexEditorRelayCommand(_ => DataStringVisual = DataVisualType.Hexadecimal)
            });
            _sbDataVisual.Choices.Add(new StatusBarChoice
            {
                DisplayName = "Dec",
                Command     = new HexEditorRelayCommand(_ => DataStringVisual = DataVisualType.Decimal)
            });
            _sbDataVisual.Choices.Add(new StatusBarChoice
            {
                DisplayName = "Bin",
                Command     = new HexEditorRelayCommand(_ => DataStringVisual = DataVisualType.Binary)
            });

            // -- Byte grouping --------------------------------------------------
            _sbByteGrouping = new StatusBarItem
            {
                Label   = "Grouping",
                Tooltip = "Click to change byte grouping"
            };
            _sbByteGrouping.Choices.Add(new StatusBarChoice
            {
                DisplayName = "2B",
                Command     = new HexEditorRelayCommand(_ => ByteGrouping = ByteSpacerGroup.TwoByte)
            });
            _sbByteGrouping.Choices.Add(new StatusBarChoice
            {
                DisplayName = "4B",
                Command     = new HexEditorRelayCommand(_ => ByteGrouping = ByteSpacerGroup.FourByte)
            });
            _sbByteGrouping.Choices.Add(new StatusBarChoice
            {
                DisplayName = "6B",
                Command     = new HexEditorRelayCommand(_ => ByteGrouping = ByteSpacerGroup.SixByte)
            });
            _sbByteGrouping.Choices.Add(new StatusBarChoice
            {
                DisplayName = "8B",
                Command     = new HexEditorRelayCommand(_ => ByteGrouping = ByteSpacerGroup.EightByte)
            });

            // -- Copy-to-clipboard format ---------------------------------------
            _sbCopyMode = new StatusBarItem
            {
                Label   = "Copy as",
                Tooltip = "Click to change the default copy format"
            };
            foreach (CopyPasteMode mode in (CopyPasteMode[])Enum.GetValues(typeof(CopyPasteMode)))
            {
                var capture = mode;
                _sbCopyMode.Choices.Add(new StatusBarChoice
                {
                    DisplayName = capture.ToString(),
                    Command     = new HexEditorRelayCommand(_ => DefaultCopyToClipboardMode = capture)
                });
            }

            // -- Refresh time (display-only, updated by HexViewport) -----------
            // Note: This is exposed as a separate field (_sbRefreshTime) but NOT included
            // in the returned collection, because MainWindow displays it separately on the
            // right side of the status bar, next to the plugin count.
            _sbRefreshTime = new StatusBarItem
            {
                Label   = "Refresh",
                Tooltip = "Viewport refresh time in milliseconds",
                Value   = "—"
            };

            // Initialise values from current DP state
            RefreshStatusBarItemValues();

            return new ObservableCollection<StatusBarItem>
            {
                _sbFileSize,
                _sbByteSize,
                _sbByteOrder,
                _sbEditMode,
                _sbBytePerLine,
                _sbOffsetVisual,
                _sbDataVisual,
                _sbByteGrouping,
                _sbCopyMode
                // _sbRefreshTime is NOT included here - displayed separately on the right
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
        /// <inheritdoc cref="IStatusBarContributor.RefreshStatusBarItems"/>
        void IStatusBarContributor.RefreshStatusBarItems() => RefreshStatusBarItemValues();

        internal void RefreshStatusBarItemValues()
        {
            if (_statusBarItems == null) return;

            // File size (display-only — no Choices)
            _sbFileSize.Value = _viewModel != null
                ? FormatFileSize(_viewModel.VirtualLength)
                : "-";

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

            // Offset display format
            var offLabel = OffSetStringVisual switch
            {
                DataVisualType.Decimal     => "Dec",
                DataVisualType.Binary      => "Bin",
                _                          => "Hex"
            };
            _sbOffsetVisual.Value = offLabel;
            foreach (var c in _sbOffsetVisual.Choices) c.IsActive = c.DisplayName == offLabel;

            // Data display format
            var dataLabel = DataStringVisual switch
            {
                DataVisualType.Decimal => "Dec",
                DataVisualType.Binary  => "Bin",
                _                      => "Hex"
            };
            _sbDataVisual.Value = dataLabel;
            foreach (var c in _sbDataVisual.Choices) c.IsActive = c.DisplayName == dataLabel;

            // Byte grouping
            var grpLabel = ByteGrouping switch
            {
                ByteSpacerGroup.TwoByte   => "2B",
                ByteSpacerGroup.SixByte   => "6B",
                ByteSpacerGroup.EightByte => "8B",
                _                         => "4B"
            };
            _sbByteGrouping.Value = grpLabel;
            foreach (var c in _sbByteGrouping.Choices) c.IsActive = c.DisplayName == grpLabel;

            // Copy-to-clipboard format
            var copyLabel = DefaultCopyToClipboardMode.ToString();
            _sbCopyMode.Value = copyLabel;
            foreach (var c in _sbCopyMode.Choices) c.IsActive = c.DisplayName == copyLabel;
        }
    }
}
