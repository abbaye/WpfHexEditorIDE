//////////////////////////////////////////////
// Apache 2.0  - 2026
// Author : Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.5
//////////////////////////////////////////////

using System;
using System.ComponentModel;
using System.Linq;
using System.Windows;
using WpfHexaEditor.Core;
using WpfHexaEditor.Models;

namespace WpfHexaEditor
{
    /// <summary>
    /// HexEditor partial class - Compatibility Layer Properties
    /// Contains V1 backward compatibility properties for missing features and state management
    /// </summary>
    public partial class HexEditor
    {
        #region Missing V1 Properties - Display/UI

        /// <summary>
        /// Legacy V1 property - Use ByteToolTipDisplayMode for new code
        /// </summary>
        public static readonly DependencyProperty ShowByteToolTipProperty =
            DependencyProperty.Register(nameof(ShowByteToolTip), typeof(bool),
                typeof(HexEditor), new PropertyMetadata(false, OnShowByteToolTipChanged));

        [System.ComponentModel.Category("Display")]
        [System.ComponentModel.Browsable(false)] // Hidden from UI - superseded by ByteToolTipDisplayMode
        [System.ComponentModel.Description("Legacy property - Use ByteToolTipDisplayMode instead")]
        [Obsolete("Use ByteToolTipDisplayMode instead")]
        public bool ShowByteToolTip
        {
            get => (bool)GetValue(ShowByteToolTipProperty);
            set => SetValue(ShowByteToolTipProperty, value);
        }

        private static void OnShowByteToolTipChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is HexEditor editor && editor.HexViewport != null)
            {
                bool enabled = (bool)e.NewValue;

                // Map bool to enum
                var mode = enabled ? ByteToolTipDisplayMode.Everywhere : ByteToolTipDisplayMode.None;

                // Update HexViewport
                editor.HexViewport.ByteToolTipDisplayMode = mode;

                // Sync ByteToolTipDisplayMode DP (without triggering callback)
                if ((ByteToolTipDisplayMode)editor.GetValue(ByteToolTipDisplayModeProperty) != mode)
                {
                    editor.SetValue(ByteToolTipDisplayModeProperty, mode);
                }
            }
        }

        // ===== NEW PROPERTY 1: Display Mode =====

        /// <summary>
        /// Identifies the ByteToolTipDisplayMode dependency property
        /// </summary>
        public static readonly DependencyProperty ByteToolTipDisplayModeProperty =
            DependencyProperty.Register(nameof(ByteToolTipDisplayMode), typeof(ByteToolTipDisplayMode),
                typeof(HexEditor), new PropertyMetadata(ByteToolTipDisplayMode.OnCustomBackgroundBlocks, OnByteToolTipDisplayModeChanged));

        /// <summary>
        /// Controls where byte tooltips are displayed (None, OnCustomBackgroundBlocks, Everywhere)
        /// </summary>
        [System.ComponentModel.Category("Tooltip")]
        [System.ComponentModel.Description("Controls where byte tooltips are displayed")]
        public ByteToolTipDisplayMode ByteToolTipDisplayMode
        {
            get => (ByteToolTipDisplayMode)GetValue(ByteToolTipDisplayModeProperty);
            set => SetValue(ByteToolTipDisplayModeProperty, value);
        }

        private static void OnByteToolTipDisplayModeChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is HexEditor editor && e.NewValue is ByteToolTipDisplayMode mode)
            {
                // Update HexViewport
                if (editor.HexViewport != null)
                {
                    editor.HexViewport.ByteToolTipDisplayMode = mode;
                }

                // Sync legacy ShowByteToolTip property (without triggering callback)
                bool newBoolValue = (mode != ByteToolTipDisplayMode.None);
                if (editor.ShowByteToolTip != newBoolValue)
                {
                    editor.SetValue(ShowByteToolTipProperty, newBoolValue);
                }
            }
        }

        // ===== NEW PROPERTY 2: Detail Level =====

        /// <summary>
        /// Identifies the ByteToolTipDetailLevel dependency property
        /// </summary>
        public static readonly DependencyProperty ByteToolTipDetailLevelProperty =
            DependencyProperty.Register(nameof(ByteToolTipDetailLevel), typeof(ByteToolTipDetailLevel),
                typeof(HexEditor), new PropertyMetadata(ByteToolTipDetailLevel.Standard, OnByteToolTipDetailLevelChanged));

        /// <summary>
        /// Controls the level of detail in byte tooltips (Basic, Standard, Detailed)
        /// </summary>
        [System.ComponentModel.Category("Tooltip")]
        [System.ComponentModel.Description("Controls the level of detail shown in byte tooltips")]
        public ByteToolTipDetailLevel ByteToolTipDetailLevel
        {
            get => (ByteToolTipDetailLevel)GetValue(ByteToolTipDetailLevelProperty);
            set => SetValue(ByteToolTipDetailLevelProperty, value);
        }

        private static void OnByteToolTipDetailLevelChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is HexEditor editor && e.NewValue is ByteToolTipDetailLevel level)
            {
                // Update HexViewport
                if (editor.HexViewport != null)
                {
                    editor.HexViewport.ByteToolTipDetailLevel = level;
                }
            }
        }

        /// <summary>
        /// Default clipboard copy/paste mode  - DependencyProperty
        /// </summary>
        public CopyPasteMode DefaultCopyToClipboardMode
        {
            get => (CopyPasteMode)GetValue(DefaultCopyToClipboardModeProperty);
            set => SetValue(DefaultCopyToClipboardModeProperty, value);
        }

        #endregion
        #region Missing V1 Properties - Editing/Insert Mode

        /// <summary>
        /// Allow insert at any position 
        /// In V2, insert mode is always allowed via EditMode property
        /// </summary>
        public bool CanInsertAnywhere
        {
            get => EditMode == EditMode.Insert;
            set
            {
                if (value)
                {
                    EditMode = EditMode.Insert;

                    // ByteProvider V2 always supports insertion anywhere - no flag needed
                    if (_viewModel?.Provider != null)
                    {
                    }
                }
                // Note: Setting false doesn't force Overwrite to allow other modes
            }
        }

        /// <summary>
        /// Visual caret mode for insert/overwrite indication  - DependencyProperty
        /// </summary>
        public CaretMode VisualCaretMode
        {
            get => (CaretMode)GetValue(VisualCaretModeProperty);
            set => SetValue(VisualCaretModeProperty, value);
        }

        /// <summary>
        /// Byte shift left amount  - DependencyProperty
        /// Used for adjusting byte position display offset
        /// </summary>
        [Category("Visual")]
        public long ByteShiftLeft
        {
            get => (long)GetValue(ByteShiftLeftProperty);
            set => SetValue(ByteShiftLeftProperty, value);
        }

        #endregion
        #region Missing V1 Properties - Auto-Highlight

        /// <summary>
        /// Auto-highlight bytes that match the selected byte  - DependencyProperty
        /// </summary>
        public bool AllowAutoHighLightSelectionByte
        {
            get => (bool)GetValue(AllowAutoHighLightSelectionByteProperty);
            set => SetValue(AllowAutoHighLightSelectionByteProperty, value);
        }

        /// <summary>
        /// Auto-highlight brush color for bytes matching selected byte  - DependencyProperty
        /// </summary>
        public System.Windows.Media.Color AutoHighLiteSelectionByteBrush
        {
            get => (System.Windows.Media.Color)GetValue(AutoHighLiteSelectionByteBrushProperty);
            set => SetValue(AutoHighLiteSelectionByteBrushProperty, value);
        }

        /// <summary>
        /// Auto-select all same bytes when double-clicking a byte  - DependencyProperty
        /// </summary>
        public bool AllowAutoSelectSameByteAtDoubleClick
        {
            get => (bool)GetValue(AllowAutoSelectSameByteAtDoubleClickProperty);
            set => SetValue(AllowAutoSelectSameByteAtDoubleClickProperty, value);
        }

        /// <summary>
        /// Enable or disable navigation when clicking on scroll markers (default: enabled) - DependencyProperty
        /// </summary>
        public bool AllowMarkerClickNavigation
        {
            get => (bool)GetValue(AllowMarkerClickNavigationProperty);
            set => SetValue(AllowMarkerClickNavigationProperty, value);
        }

        #endregion
        #region Missing V1 Properties - Count/Statistics

        /// <summary>
        /// Enable byte counting feature  - DependencyProperty
        /// </summary>
        public bool AllowByteCount
        {
            get => (bool)GetValue(AllowByteCountProperty);
            set => SetValue(AllowByteCountProperty, value);
        }

        #endregion
        #region Missing V1 Properties - File Drop/Drag

        /// <summary>
        /// Confirm before dropping a file to load it  - DependencyProperty
        /// </summary>
        public bool FileDroppingConfirmation
        {
            get => (bool)GetValue(FileDroppingConfirmationProperty);
            set => SetValue(FileDroppingConfirmationProperty, value);
        }

        /// <summary>
        /// Allow text drag-drop operations  - DependencyProperty
        /// </summary>
        public bool AllowTextDrop
        {
            get => (bool)GetValue(AllowTextDropProperty);
            set => SetValue(AllowTextDropProperty, value);
        }

        /// <summary>
        /// Allow file drag-drop operations  - DependencyProperty
        /// Note: AllowDrop must also be true for this to work
        /// </summary>
        public bool AllowFileDrop
        {
            get => (bool)GetValue(AllowFileDropProperty);
            set => SetValue(AllowFileDropProperty, value);
        }

        #endregion
        #region Missing V1 Properties - Extend/Append

        /// <summary>
        /// Allow extending file at end  - DependencyProperty
        /// </summary>
        public bool AllowExtend
        {
            get => (bool)GetValue(AllowExtendProperty);
            set => SetValue(AllowExtendProperty, value);
        }

        /// <summary>
        /// Confirm before appending bytes  - DependencyProperty
        /// </summary>
        public bool AppendNeedConfirmation
        {
            get => (bool)GetValue(AppendNeedConfirmationProperty);
            set => SetValue(AppendNeedConfirmationProperty, value);
        }

        #endregion
        #region Missing V1 Properties - Delete Byte

        /// <summary>
        /// Allow byte deletion  - DependencyProperty
        /// </summary>
        public bool AllowDeleteByte
        {
            get => (bool)GetValue(AllowDeleteByteProperty);
            set => SetValue(AllowDeleteByteProperty, value);
        }

        #endregion
        #region Missing V1 Properties - State

        private System.Xml.Linq.XDocument _currentStateDocument;

        /// <summary>
        /// Current editor state as XDocument for persistence 
        /// Get: Returns current state as XML document
        /// Set: Restores state from XML document
        /// </summary>
        public System.Xml.Linq.XDocument CurrentState
        {
            get
            {
                // Generate current state as XDocument
                var doc = new System.Xml.Linq.XDocument(
                    new System.Xml.Linq.XElement("HexEditorState",
                        new System.Xml.Linq.XElement("FileName", FileName ?? string.Empty),
                        new System.Xml.Linq.XElement("Position", Position),
                        new System.Xml.Linq.XElement("SelectionStart", SelectionStart),
                        new System.Xml.Linq.XElement("SelectionStop", SelectionStop),
                        new System.Xml.Linq.XElement("FontSize", FontSize),
                        new System.Xml.Linq.XElement("BytePerLine", BytePerLine),
                        new System.Xml.Linq.XElement("ReadOnlyMode", ReadOnlyMode),
                        new System.Xml.Linq.XElement("Bookmarks",
                            _bookmarks.Select(b => new System.Xml.Linq.XElement("Bookmark", b))
                        )
                    )
                );
                return doc;
            }
            set
            {
                if (value == null) return;

                var root = value.Root;
                if (root?.Name != "HexEditorState") return;

                // Restore basic properties
                var fontSize = root.Element("FontSize")?.Value;
                if (fontSize != null && double.TryParse(fontSize, out double fs))
                    FontSize = fs;

                var bytesPerLine = root.Element("BytePerLine")?.Value;
                if (bytesPerLine != null && int.TryParse(bytesPerLine, out int bpl))
                    BytePerLine = bpl;

                var readOnlyMode = root.Element("ReadOnlyMode")?.Value;
                if (readOnlyMode != null && bool.TryParse(readOnlyMode, out bool rom))
                    ReadOnlyMode = rom;

                // Restore position and selection
                var position = root.Element("Position")?.Value;
                if (position != null && long.TryParse(position, out long pos))
                    SetPosition(pos);

                var selStart = root.Element("SelectionStart")?.Value;
                var selStop = root.Element("SelectionStop")?.Value;
                if (selStart != null && long.TryParse(selStart, out long start) &&
                    selStop != null && long.TryParse(selStop, out long stop))
                {
                    SelectionStart = start;
                    SelectionStop = stop;
                }

                // Restore bookmarks
                var bookmarks = root.Element("Bookmarks")?.Elements("Bookmark");
                if (bookmarks != null)
                {
                    ClearAllBookmarks();
                    foreach (var bookmark in bookmarks)
                    {
                        if (long.TryParse(bookmark.Value, out long bookmarkPos))
                            SetBookmark(bookmarkPos);
                    }
                }

                _currentStateDocument = value;
            }
        }

        #endregion
    }
}
