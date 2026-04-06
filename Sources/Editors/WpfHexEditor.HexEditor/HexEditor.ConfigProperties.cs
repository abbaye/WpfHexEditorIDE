//////////////////////////////////////////////
// GNU Affero General Public License v3.0 - 2026
// Author : Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.5, Claude Sonnet 4.6
//////////////////////////////////////////////
// Project: WpfHexEditor.HexEditor
// File: HexEditor.ConfigProperties.cs
// Description: Configuration/behavior dependency property wrappers.
// Architecture notes: Partial class extracted from HexEditor.xaml.cs
//////////////////////////////////////////////

using System.ComponentModel;
using System.IO;
using System.Windows;
using System.Windows.Media;
using WpfHexEditor.Core;
using WpfHexEditor.Core.Bytes;
using WpfHexEditor.Core.CharacterTable;

namespace WpfHexEditor.HexEditor
{
    public partial class HexEditor
    {
        #region Configuration Properties

        /// <summary>
        /// Allow context menu - DependencyProperty
        /// </summary>
        [Category("Behavior")]
        public bool AllowContextMenu
        {
            get => (bool)GetValue(AllowContextMenuProperty);
            set => SetValue(AllowContextMenuProperty, value);
        }

        /// <summary>
        /// Allow zoom - DependencyProperty
        /// </summary>
        [Category("Behavior")]
        public bool AllowZoom
        {
            get => (bool)GetValue(AllowZoomProperty);
            set => SetValue(AllowZoomProperty, value);
        }

        /// <summary>
        /// Gets or sets the mouse wheel scroll speed for vertical scrolling.
        /// Category: Keyboard &amp; mouse
        /// Default: Normal
        /// </summary>
        [Category("Keyboard")]
        public MouseWheelSpeed MouseWheelSpeed
        {
            get => (MouseWheelSpeed)GetValue(MouseWheelSpeedProperty);
            set => SetValue(MouseWheelSpeedProperty, value);
        }

        /// <summary>
        /// Data string display format (Hex/Decimal/Octal/Binary) - DependencyProperty
        /// </summary>
        [Category("Display")]
        public DataVisualType DataStringVisual
        {
            get => (DataVisualType)GetValue(DataStringVisualProperty);
            set => SetValue(DataStringVisualProperty, value);
        }

        /// <summary>
        /// Offset string display format (Hex/Decimal/Octal/Binary) - DependencyProperty
        /// </summary>
        [Category("Display")]
        public DataVisualType OffSetStringVisual
        {
            get => (DataVisualType)GetValue(OffSetStringVisualProperty);
            set => SetValue(OffSetStringVisualProperty, value);
        }

        /// <summary>
        /// Actual offset column width (dynamically calculated based on OffSetStringVisual format)
        /// This property is auto-calculated and should not be displayed in settings panel
        /// </summary>
        [Category("Display")]
        [Browsable(false)]  // Hide from auto-generated settings panel (GridLength not supported)
        public GridLength ActualOffsetWidth
        {
            get => (GridLength)GetValue(ActualOffsetWidthProperty);
            set => SetValue(ActualOffsetWidthProperty, value);
        }

        /// <summary>
        /// Byte order (Lo-Hi / Hi-Lo) - DependencyProperty
        /// </summary>
        [Category("Visual")]
        public ByteOrderType ByteOrder
        {
            get => (ByteOrderType)GetValue(ByteOrderProperty);
            set => SetValue(ByteOrderProperty, value);
        }

        /// <summary>
        /// Byte size display (8/16/32-bit) - DependencyProperty
        /// </summary>
        [Category("Visual")]
        public ByteSizeType ByteSize
        {
            get => (ByteSizeType)GetValue(ByteSizeProperty);
            set => SetValue(ByteSizeProperty, value);
        }

        /// <summary>
        /// Custom text encoding - DependencyProperty
        /// </summary>
        public System.Text.Encoding CustomEncoding
        {
            get => (System.Text.Encoding)GetValue(CustomEncodingProperty);
            set => SetValue(CustomEncodingProperty, value ?? System.Text.Encoding.UTF8);
        }

        /// <summary>
        /// Gets the underlying stream used by the ByteProvider
        /// Read-only property for V1 compatibility
        /// </summary>
        public Stream Stream => _viewModel?.Provider?.Stream;

        /// <summary>
        /// Preload byte strategy - DependencyProperty
        /// </summary>
        [Category("Data")]
        public PreloadByteInEditor PreloadByteInEditorMode
        {
            get => (PreloadByteInEditor)GetValue(PreloadByteInEditorModeProperty);
            set => SetValue(PreloadByteInEditorModeProperty, value);
        }

        // TBL Advanced Features  - DependencyProperties

        /// <summary>
        /// Show ASCII characters in TBL - DependencyProperty
        /// </summary>
        [Category("TBL")]
        public bool ShowTblAscii
        {
            get => (bool)GetValue(ShowTblAsciiProperty);
            set => SetValue(ShowTblAsciiProperty, value);
        }

        /// <summary>
        /// Show DTE (Dual-Title Encoding) in TBL - DependencyProperty
        /// </summary>
        [Category("TBL")]
        public bool ShowTblDte
        {
            get => (bool)GetValue(ShowTblDteProperty);
            set => SetValue(ShowTblDteProperty, value);
        }

        /// <summary>
        /// Show MTE (Multi-Title Encoding) in TBL - DependencyProperty (renamed for consistency)
        /// </summary>
        [Category("TBL")]
        public bool ShowTblMte
        {
            get => (bool)GetValue(ShowTblMteProperty);
            set => SetValue(ShowTblMteProperty, value);
        }

        /// <summary>
        /// Show Japanese characters in TBL - DependencyProperty
        /// </summary>
        [Category("TBL")]
        public bool ShowTblJaponais
        {
            get => (bool)GetValue(ShowTblJaponaisProperty);
            set => SetValue(ShowTblJaponaisProperty, value);
        }

        /// <summary>
        /// Show End Block markers in TBL - DependencyProperty
        /// </summary>
        [Category("TBL")]
        public bool ShowTblEndBlock
        {
            get => (bool)GetValue(ShowTblEndBlockProperty);
            set => SetValue(ShowTblEndBlockProperty, value);
        }

        /// <summary>
        /// Show End Line markers in TBL - DependencyProperty
        /// </summary>
        [Category("TBL")]
        public bool ShowTblEndLine
        {
            get => (bool)GetValue(ShowTblEndLineProperty);
            set => SetValue(ShowTblEndLineProperty, value);
        }

        /// <summary>
        /// DTE (Dual-Tile Encoding) color - DependencyProperty
        /// </summary>
        [Category("Colors.CharacterTable")]
        public System.Windows.Media.Color TblDteColor
        {
            get => (System.Windows.Media.Color)GetValue(TblDteColorProperty);
            set => SetValue(TblDteColorProperty, value);
        }

        /// <summary>
        /// MTE (Multi-Title Encoding) color - DependencyProperty
        /// </summary>
        [Category("Colors.CharacterTable")]
        public System.Windows.Media.Color TblMteColor
        {
            get => (System.Windows.Media.Color)GetValue(TblMteColorProperty);
            set => SetValue(TblMteColorProperty, value);
        }

        /// <summary>
        /// End block color for TBL - DependencyProperty
        /// </summary>
        [Category("Colors.CharacterTable")]
        public System.Windows.Media.Color TblEndBlockColor
        {
            get => (System.Windows.Media.Color)GetValue(TblEndBlockColorProperty);
            set => SetValue(TblEndBlockColorProperty, value);
        }

        /// <summary>
        /// End line color for TBL - DependencyProperty
        /// </summary>
        [Category("Colors.CharacterTable")]
        public System.Windows.Media.Color TblEndLineColor
        {
            get => (System.Windows.Media.Color)GetValue(TblEndLineColorProperty);
            set => SetValue(TblEndLineColorProperty, value);
        }

        /// <summary>
        /// ASCII color for TBL - DependencyProperty
        /// </summary>
        [Category("Colors.CharacterTable")]
        public System.Windows.Media.Color TblAsciiColor
        {
            get => (System.Windows.Media.Color)GetValue(TblAsciiColorProperty);
            set => SetValue(TblAsciiColorProperty, value);
        }

        /// <summary>
        /// Japanese characters color for TBL - DependencyProperty
        /// </summary>
        [Category("Colors.CharacterTable")]
        public System.Windows.Media.Color TblJaponaisColor
        {
            get => (System.Windows.Media.Color)GetValue(TblJaponaisColorProperty);
            set => SetValue(TblJaponaisColorProperty, value);
        }

        /// <summary>
        /// 3-byte sequences color for TBL - DependencyProperty
        /// </summary>
        [Category("Colors.CharacterTable")]
        public System.Windows.Media.Color Tbl3ByteColor
        {
            get => (System.Windows.Media.Color)GetValue(Tbl3ByteColorProperty);
            set => SetValue(Tbl3ByteColorProperty, value);
        }

        /// <summary>
        /// 4+ byte sequences color for TBL - DependencyProperty
        /// </summary>
        [Category("Colors.CharacterTable")]
        public System.Windows.Media.Color Tbl4PlusByteColor
        {
            get => (System.Windows.Media.Color)GetValue(Tbl4PlusByteColorProperty);
            set => SetValue(Tbl4PlusByteColorProperty, value);
        }

        /// <summary>
        /// Default color for TBL - DependencyProperty
        /// </summary>
        [Category("Colors.CharacterTable")]
        public System.Windows.Media.Color TblDefaultColor
        {
            get => (System.Windows.Media.Color)GetValue(TblDefaultColorProperty);
            set => SetValue(TblDefaultColorProperty, value);
        }

        /// <summary>
        /// Selection brush for the active panel (Hex or ASCII) - DependencyProperty
        /// </summary>
        [Category("Colors.Selection")]
        public Brush SelectionActiveBrush
        {
            get => (Brush)GetValue(SelectionActiveBrushProperty);
            set => SetValue(SelectionActiveBrushProperty, value);
        }

        /// <summary>
        /// Selection brush for the inactive panel (Hex or ASCII) - DependencyProperty
        /// </summary>
        [Category("Colors.Selection")]
        public Brush SelectionInactiveBrush
        {
            get => (Brush)GetValue(SelectionInactiveBrushProperty);
            set => SetValue(SelectionInactiveBrushProperty, value);
        }

        #endregion
    }
}
