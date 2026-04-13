//////////////////////////////////////////////
// GNU Affero General Public License v3.0 - 2026
// Author : Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.5, Claude Sonnet 4.6
//////////////////////////////////////////////
// Project: WpfHexEditor.HexEditor
// File: HexEditor.DependencyProperties.cs
// Description: WPF DependencyProperty declarations and change-callback handlers.
// Architecture notes: Partial class extracted from HexEditor.xaml.cs
//////////////////////////////////////////////

using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using WpfHexEditor.Core;
using WpfHexEditor.Core.Bytes;
using WpfHexEditor.Core.Events;
using WpfHexEditor.Core.Models;
using WpfHexEditor.HexEditor.ViewModels;

namespace WpfHexEditor.HexEditor
{
    public partial class HexEditor
    {
        #region Dependency Properties

        /// <summary>
        /// FileName DependencyProperty for XAML binding
        /// </summary>
        public static readonly DependencyProperty FileNameProperty =
            DependencyProperty.Register(nameof(FileName), typeof(string), typeof(HexEditor),
                new PropertyMetadata(string.Empty, OnFileNamePropertyChanged));

        private static void OnFileNamePropertyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is HexEditor editor && e.NewValue is string path && !string.IsNullOrEmpty(path))
            {
                // CRITICAL: Prevent infinite recursion — only guard the OpenFile call,
                // NOT the title notification below (which must always fire so the tab
                // header reflects the correct name after an async open completes).
                if (!editor._isOpeningFile)
                {
                    // Auto-load file when FileName is set
                    // This enables the pattern: HexEdit.FileName = "file.bin" to automatically open the file
                    try
                    {
                        // Only open if different from current file and file exists
                        if (path != e.OldValue?.ToString() && System.IO.File.Exists(path))
                        {
                            editor.OpenFile(path);
                        }
                    }
                    catch (Exception ex)
                    {
                        editor.StatusText.Text = $"Failed to open file: {ex.Message}";
                    }
                }
            }

            // Notify IDocumentEditor subscribers that the title may have changed.
            // Always fires — including when OpenFileCoreAsync sets FileName after async I/O
            // completes — so the tab header is updated from "Untitled" to the real file name.
            if (d is HexEditor ed)
                ed.RaiseDocumentEditorTitleChanged();
        }

        /// <summary>
        /// IsModified DependencyProperty for XAML binding
        /// </summary>
        public static readonly DependencyProperty IsModifiedProperty =
            DependencyProperty.Register(nameof(IsModified), typeof(bool), typeof(HexEditor),
                new PropertyMetadata(false));

        /// <summary>
        /// Position DependencyProperty for XAML binding
        /// </summary>
        public static readonly DependencyProperty PositionProperty =
            DependencyProperty.Register(nameof(Position), typeof(long), typeof(HexEditor),
                new PropertyMetadata(-1L, OnPositionPropertyChanged));

        private static void OnPositionPropertyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is HexEditor editor && e.NewValue is long position && position >= 0)
            {
                // CRITICAL FIX: Prevent infinite loop - only update ViewModel if value actually changed
                var currentVmStart = editor._viewModel?.SelectionStart.IsValid == true ? editor._viewModel.SelectionStart.Value : -1L;
                if (currentVmStart == position)
                    return; // Already synced, avoid recursion

                editor.SetPosition(position);
            }
        }

        /// <summary>
        /// ReadOnlyMode DependencyProperty for XAML binding
        /// </summary>
        public static readonly DependencyProperty ReadOnlyModeProperty =
            DependencyProperty.Register(nameof(ReadOnlyMode), typeof(bool), typeof(HexEditor),
                new PropertyMetadata(false, OnReadOnlyModePropertyChanged));

        private static void OnReadOnlyModePropertyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is HexEditor editor && e.NewValue is bool readOnly)
            {
                if (editor._viewModel != null)
                {
                    var oldValue = editor._viewModel.ReadOnlyMode;
                    editor._viewModel.ReadOnlyMode = readOnly;

                    // Fire event
                    if (oldValue != readOnly)
                        editor.OnReadOnlyChanged(EventArgs.Empty);
                }
            }
        }

        /// <summary>
        /// SelectionStart DependencyProperty for XAML binding
        /// </summary>
        public static readonly DependencyProperty SelectionStartProperty =
            DependencyProperty.Register(nameof(SelectionStart), typeof(long), typeof(HexEditor),
                new PropertyMetadata(-1L, OnSelectionStartPropertyChanged));

        private static void OnSelectionStartPropertyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is HexEditor editor && e.NewValue is long position)
            {
                if (editor._viewModel != null && position >= 0 && position < editor.VirtualLength)
                {
                    // CRITICAL FIX: Prevent infinite loop - only update ViewModel if value actually changed
                    var currentVmStart = editor._viewModel.SelectionStart.IsValid ? editor._viewModel.SelectionStart.Value : -1L;
                    if (currentVmStart == position)
                        return; // Already synced, avoid recursion

                    var oldStart = e.OldValue is long old ? old : -1;
                    var oldStop = editor.SelectionStop;
                    var oldLength = editor.SelectionLength;

                    var stop = editor._viewModel.SelectionStop.IsValid ? editor._viewModel.SelectionStop : new VirtualPosition(position);
                    editor._viewModel.SetSelectionRange(new VirtualPosition(position), stop);

                    // Fire events
                    if (oldStart != position || oldStop != editor.SelectionStop)
                    {
                        editor.OnSelectionChanged(new HexSelectionChangedEventArgs(position, editor.SelectionStop, editor.SelectionLength));

                        if (oldStart != position)
                            editor.OnSelectionStartChanged(EventArgs.Empty);
                        if (oldLength != editor.SelectionLength)
                            editor.OnSelectionLengthChanged(EventArgs.Empty);
                    }

                    // Update auto-highlight byte value
                    editor.UpdateAutoHighlightByte();
                }
            }
        }

        /// <summary>
        /// SelectionStop DependencyProperty for XAML binding
        /// </summary>
        public static readonly DependencyProperty SelectionStopProperty =
            DependencyProperty.Register(nameof(SelectionStop), typeof(long), typeof(HexEditor),
                new PropertyMetadata(-1L, OnSelectionStopPropertyChanged));

        private static void OnSelectionStopPropertyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is HexEditor editor && e.NewValue is long position)
            {
                if (editor._viewModel != null && position >= 0 && position < editor.VirtualLength)
                {
                    // CRITICAL FIX: Prevent infinite loop - only update ViewModel if value actually changed
                    var currentVmStop = editor._viewModel.SelectionStop.IsValid ? editor._viewModel.SelectionStop.Value : -1L;
                    if (currentVmStop == position)
                        return; // Already synced, avoid recursion

                    var oldStart = editor.SelectionStart;
                    var oldStop = e.OldValue is long old ? old : -1;
                    var oldLength = editor.SelectionLength;

                    var start = editor._viewModel.SelectionStart.IsValid ? editor._viewModel.SelectionStart : new VirtualPosition(position);
                    editor._viewModel.SetSelectionRange(start, new VirtualPosition(position));

                    // Fire events
                    if (oldStop != position || oldStart != editor.SelectionStart)
                    {
                        editor.OnSelectionChanged(new HexSelectionChangedEventArgs(editor.SelectionStart, position, editor.SelectionLength));

                        if (oldStop != position)
                            editor.OnSelectionStopChanged(EventArgs.Empty);
                        if (oldLength != editor.SelectionLength)
                            editor.OnSelectionLengthChanged(EventArgs.Empty);
                    }
                }
            }
        }

        /// <summary>
        /// BytePerLine DependencyProperty for XAML binding
        /// </summary>
        public static readonly DependencyProperty BytePerLineProperty =
            DependencyProperty.Register(nameof(BytePerLine), typeof(int), typeof(HexEditor),
                new PropertyMetadata(16, OnBytePerLinePropertyChanged));

        private static void OnBytePerLinePropertyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is HexEditor editor && e.NewValue is int bytesPerLine && bytesPerLine > 0)
            {
                // CRITICAL FIX: Prevent infinite loop - only update ViewModel if value actually changed
                if (editor._viewModel != null && editor._viewModel.BytePerLine == bytesPerLine)
                    return; // Already synced, avoid recursion

                // ALWAYS update viewport and headers (even during initialization when ViewModel doesn't exist yet)
                editor.HexViewport.BytesPerLine = bytesPerLine;
                editor.RefreshColumnHeader();
                editor.BytesPerLineText.Text = $"Bytes/Line: {bytesPerLine}";

                // Update ViewModel if it exists (may not exist during initial XAML loading)
                if (editor._viewModel != null)
                {
                    editor._viewModel.BytePerLine = bytesPerLine;

                    // Update scrollbar to reflect new total lines (with +3 margin to access last lines)
                    editor.VerticalScroll.Maximum = Math.Max(0, editor._viewModel.TotalLines - editor._viewModel.VisibleLines + 3);
                }
            }
        }

        /// <summary>
        /// EditMode DependencyProperty for XAML binding
        /// </summary>
        public static readonly DependencyProperty EditModeProperty =
            DependencyProperty.Register(nameof(EditMode), typeof(EditMode), typeof(HexEditor),
                new PropertyMetadata(EditMode.Overwrite, OnEditModePropertyChanged));

        private static void OnEditModePropertyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is HexEditor editor && e.NewValue is EditMode mode)
            {
                // CRITICAL FIX: Prevent infinite loop - only update ViewModel if value actually changed
                if (editor._viewModel != null && editor._viewModel.EditMode == mode)
                    return; // Already synced, avoid recursion

                if (editor._viewModel != null)
                {
                    editor._viewModel.EditMode = mode;
                }

                // Sync to HexViewport for caret display
                editor.HexViewport.EditMode = mode;
                editor.HexViewport.InvalidateVisual(); // Refresh to show/hide caret

                // Update status bar
                editor.EditModeText.Text = mode == EditMode.Insert ? "Mode: Insert" : "Mode: Overwrite";
            }
        }

        /// <summary>
        /// IsFileOrStreamLoaded Read-Only DependencyProperty for XAML binding
        /// </summary>
        private static readonly DependencyPropertyKey IsFileOrStreamLoadedPropertyKey =
            DependencyProperty.RegisterReadOnly(nameof(IsFileOrStreamLoaded), typeof(bool), typeof(HexEditor),
                new PropertyMetadata(false));

        public static readonly DependencyProperty IsFileOrStreamLoadedProperty =
            IsFileOrStreamLoadedPropertyKey.DependencyProperty;

        /// <summary>
        /// IsOperationActive Read-Only DependencyProperty for XAML binding
        /// </summary>
        private static readonly DependencyPropertyKey IsOperationActivePropertyKey =
            DependencyProperty.RegisterReadOnly(
                nameof(IsOperationActive),
                typeof(bool),
                typeof(HexEditor),
                new PropertyMetadata(false, OnIsOperationActiveChanged));

        public static readonly DependencyProperty IsOperationActiveProperty =
            IsOperationActivePropertyKey.DependencyProperty;

        private static void OnIsOperationActiveChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is HexEditor editor && e.NewValue is bool isActive)
            {
                try
                {
                    // CRITICAL: Check if control is still loaded (prevents crashes during app shutdown)
                    if (!editor.IsLoaded)
                        return;

                    // Notify CommandManager to re-evaluate all ICommand bindings
                    CommandManager.InvalidateRequerySuggested();

                    // Raise event for parent applications
                    editor.OnOperationStateChanged(new OperationStateChangedEventArgs(isActive));

                    // Update menu items enabled state
                    editor.UpdateMenuItemsEnabled(!isActive);
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"OnIsOperationActiveChanged error: {ex.Message}");
                }
            }
        }

        /// <summary>
        /// When <see langword="true"/> (default), the built-in <see cref="ProgressOverlay"/> is shown during
        /// long-running operations. Set to <see langword="false"/> when the host application (e.g. WpfHexEditor.App)
        /// wants to handle progress display itself via the <see cref="WpfHexEditor.Editor.Core.IDocumentEditor"/>
        /// OperationStarted/Progress/Completed events.
        /// </summary>
        public static readonly DependencyProperty ShowProgressOverlayProperty =
            DependencyProperty.Register(nameof(ShowProgressOverlay), typeof(bool), typeof(HexEditor),
                new PropertyMetadata(true));

        public bool ShowProgressOverlay
        {
            get => (bool)GetValue(ShowProgressOverlayProperty);
            set => SetValue(ShowProgressOverlayProperty, value);
        }

        /// <summary>
        /// AllowContextMenu DependencyProperty for XAML binding
        /// </summary>
        public static readonly DependencyProperty AllowContextMenuProperty =
            DependencyProperty.Register(nameof(AllowContextMenu), typeof(bool), typeof(HexEditor),
                new PropertyMetadata(true, OnAllowContextMenuChanged));

        private static void OnAllowContextMenuChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is HexEditor editor && e.NewValue is bool allowed)
            {
                // Enable or disable context menu
                if (editor.ByteContextMenu != null)
                    editor.ContextMenu = allowed ? editor.ByteContextMenu : null;
            }
        }

        /// <summary>
        /// AllowZoom DependencyProperty for XAML binding
        /// </summary>
        public static readonly DependencyProperty AllowZoomProperty =
            DependencyProperty.Register(nameof(AllowZoom), typeof(bool), typeof(HexEditor),
                new PropertyMetadata(true, OnAllowZoomChanged));

        private static void OnAllowZoomChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is HexEditor editor && e.NewValue is bool allowed)
            {
                // Initialize zoom if enabled, or reset to 1.0 if disabled
                if (allowed)
                    editor.InitialiseZoom();
                else if (editor._scaler != null)
                {
                    editor._scaler.ScaleX = 1.0;
                    editor._scaler.ScaleY = 1.0;
                }
            }
        }

        /// <summary>
        /// Identifies the <see cref="MouseWheelSpeed"/> dependency property.
        /// Controls the mouse wheel scroll speed for vertical scrolling through the hex data.
        /// </summary>
        public static readonly DependencyProperty MouseWheelSpeedProperty =
            DependencyProperty.Register(nameof(MouseWheelSpeed), typeof(MouseWheelSpeed), typeof(HexEditor),
                new PropertyMetadata(Core.MouseWheelSpeed.System));

        /// <summary>
        /// AllowAutoHighLightSelectionByte DependencyProperty for XAML binding
        /// </summary>
        public static readonly DependencyProperty AllowAutoHighLightSelectionByteProperty =
            DependencyProperty.Register(nameof(AllowAutoHighLightSelectionByte), typeof(bool), typeof(HexEditor),
                new PropertyMetadata(false, OnAllowAutoHighLightChanged));

        private static void OnAllowAutoHighLightChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is HexEditor editor)
            {
                // Update auto-highlight byte value when feature is toggled
                editor.UpdateAutoHighlightByte();

                // Refresh viewport to show/hide auto-highlighting
                editor.HexViewport?.InvalidateVisual();
            }
        }

        /// <summary>
        /// AutoHighLiteSelectionByteBrush DependencyProperty for XAML binding
        /// </summary>
        public static readonly DependencyProperty AutoHighLiteSelectionByteBrushProperty =
            DependencyProperty.Register(nameof(AutoHighLiteSelectionByteBrush), typeof(System.Windows.Media.Color), typeof(HexEditor),
                new PropertyMetadata(Color.FromArgb(0x60, 0xFF, 0xFF, 0x00), OnAutoHighLiteColorChanged)); // 40% Yellow

        private static void OnAutoHighLiteColorChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is HexEditor editor && e.NewValue is System.Windows.Media.Color color)
            {
                // Update HexViewport highlight brush
                if (editor.HexViewport != null)
                {
                    editor.HexViewport.AutoHighLiteBrush = new SolidColorBrush(color);
                    editor.HexViewport.InvalidateVisual();
                }
            }
        }

        /// <summary>
        /// AllowAutoSelectSameByteAtDoubleClick DependencyProperty for XAML binding
        /// </summary>
        public static readonly DependencyProperty AllowAutoSelectSameByteAtDoubleClickProperty =
            DependencyProperty.Register(nameof(AllowAutoSelectSameByteAtDoubleClick), typeof(bool), typeof(HexEditor),
                new PropertyMetadata(false));

        /// <summary>
        /// AllowMarkerClickNavigation DependencyProperty for XAML binding
        /// Enables/disables navigation when clicking on scroll markers (default: true)
        /// </summary>
        public static readonly DependencyProperty AllowMarkerClickNavigationProperty =
            DependencyProperty.Register(nameof(AllowMarkerClickNavigation), typeof(bool), typeof(HexEditor),
                new PropertyMetadata(true, OnAllowMarkerClickNavigationChanged));

        private static void OnAllowMarkerClickNavigationChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is HexEditor editor && editor._scrollMarkers != null)
            {
                editor._scrollMarkers.AllowMarkerClickNavigation = (bool)e.NewValue;
            }
        }

        /// <summary>
        /// AllowFileDrop DependencyProperty for XAML binding
        /// </summary>
        public static readonly DependencyProperty AllowFileDropProperty =
            DependencyProperty.Register(nameof(AllowFileDrop), typeof(bool), typeof(HexEditor),
                new PropertyMetadata(true, OnAllowDropChanged));

        /// <summary>
        /// AllowTextDrop DependencyProperty for XAML binding
        /// </summary>
        public static readonly DependencyProperty AllowTextDropProperty =
            DependencyProperty.Register(nameof(AllowTextDrop), typeof(bool), typeof(HexEditor),
                new PropertyMetadata(false, OnAllowDropChanged));

        private static void OnAllowDropChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is HexEditor editor)
            {
                // Enable AllowDrop if either file or text drop is allowed
                editor.AllowDrop = editor.AllowFileDrop || editor.AllowTextDrop;
            }
        }

        /// <summary>
        /// FileDroppingConfirmation DependencyProperty for XAML binding
        /// </summary>
        public static readonly DependencyProperty FileDroppingConfirmationProperty =
            DependencyProperty.Register(nameof(FileDroppingConfirmation), typeof(bool), typeof(HexEditor),
                new PropertyMetadata(true));

        /// <summary>
        /// AllowExtend DependencyProperty for XAML binding
        /// </summary>
        public static readonly DependencyProperty AllowExtendProperty =
            DependencyProperty.Register(nameof(AllowExtend), typeof(bool), typeof(HexEditor),
                new PropertyMetadata(true));

        /// <summary>
        /// AllowDeleteByte DependencyProperty for XAML binding
        /// </summary>
        public static readonly DependencyProperty AllowDeleteByteProperty =
            DependencyProperty.Register(nameof(AllowDeleteByte), typeof(bool), typeof(HexEditor),
                new PropertyMetadata(true));

        /// <summary>
        /// AllowByteCount DependencyProperty for XAML binding
        /// </summary>
        public static readonly DependencyProperty AllowByteCountProperty =
            DependencyProperty.Register(nameof(AllowByteCount), typeof(bool), typeof(HexEditor),
                new PropertyMetadata(true));

        /// <summary>
        /// ShowTblAscii DependencyProperty - Show ASCII characters in TBL
        /// </summary>
        public static readonly DependencyProperty ShowTblAsciiProperty =
            DependencyProperty.Register(nameof(ShowTblAscii), typeof(bool), typeof(HexEditor),
                new PropertyMetadata(true, OnTblTypeVisibilityChanged));

        /// <summary>
        /// ShowTblDte DependencyProperty - Show DTE (Dual-Title Encoding) in TBL
        /// </summary>
        public static readonly DependencyProperty ShowTblDteProperty =
            DependencyProperty.Register(nameof(ShowTblDte), typeof(bool), typeof(HexEditor),
                new PropertyMetadata(true, OnTblTypeVisibilityChanged));

        /// <summary>
        /// ShowTblMte DependencyProperty - Show MTE (Multi-Title Encoding) in TBL
        /// </summary>
        public static readonly DependencyProperty ShowTblMteProperty =
            DependencyProperty.Register(nameof(ShowTblMte), typeof(bool), typeof(HexEditor),
                new PropertyMetadata(true, OnTblTypeVisibilityChanged));

        /// <summary>
        /// ShowTblJaponais DependencyProperty - Show Japanese characters in TBL
        /// </summary>
        public static readonly DependencyProperty ShowTblJaponaisProperty =
            DependencyProperty.Register(nameof(ShowTblJaponais), typeof(bool), typeof(HexEditor),
                new PropertyMetadata(true, OnTblTypeVisibilityChanged));

        /// <summary>
        /// ShowTblEndBlock DependencyProperty - Show End Block markers in TBL
        /// </summary>
        public static readonly DependencyProperty ShowTblEndBlockProperty =
            DependencyProperty.Register(nameof(ShowTblEndBlock), typeof(bool), typeof(HexEditor),
                new PropertyMetadata(true, OnTblTypeVisibilityChanged));

        /// <summary>
        /// ShowTblEndLine DependencyProperty - Show End Line markers in TBL
        /// </summary>
        public static readonly DependencyProperty ShowTblEndLineProperty =
            DependencyProperty.Register(nameof(ShowTblEndLine), typeof(bool), typeof(HexEditor),
                new PropertyMetadata(true, OnTblTypeVisibilityChanged));

        /// <summary>
        /// Callback when any TBL type visibility changes - sync to HexViewport and refresh
        /// </summary>
        private static void OnTblTypeVisibilityChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is HexEditor editor && editor.HexViewport != null)
            {
                // Sync all TBL type visibility flags to HexViewport
                editor.HexViewport.ShowTblAscii = editor.ShowTblAscii;
                editor.HexViewport.ShowTblDte = editor.ShowTblDte;
                editor.HexViewport.ShowTblMte = editor.ShowTblMte;
                editor.HexViewport.ShowTblJaponais = editor.ShowTblJaponais;
                editor.HexViewport.ShowTblEndBlock = editor.ShowTblEndBlock;
                editor.HexViewport.ShowTblEndLine = editor.ShowTblEndLine;

                editor.HexViewport.InvalidateVisual();
            }
        }

        /// <summary>
        /// TblDteColor DependencyProperty for XAML binding
        /// </summary>
        public static readonly DependencyProperty TblDteColorProperty =
            DependencyProperty.Register(nameof(TblDteColor), typeof(System.Windows.Media.Color), typeof(HexEditor),
                new PropertyMetadata(Colors.Green, OnTblColorChanged)); // TEST: Green to distinguish from MTE

        /// <summary>
        /// TblMteColor DependencyProperty for XAML binding
        /// </summary>
        public static readonly DependencyProperty TblMteColorProperty =
            DependencyProperty.Register(nameof(TblMteColor), typeof(System.Windows.Media.Color), typeof(HexEditor),
                new PropertyMetadata(Colors.Red, OnTblColorChanged)); // V1 compatible: Red

        /// <summary>
        /// TblEndBlockColor DependencyProperty for XAML binding
        /// </summary>
        public static readonly DependencyProperty TblEndBlockColorProperty =
            DependencyProperty.Register(nameof(TblEndBlockColor), typeof(System.Windows.Media.Color), typeof(HexEditor),
                new PropertyMetadata(Colors.Blue, OnTblColorChanged)); // V1 compatible: Blue

        /// <summary>
        /// TblEndLineColor DependencyProperty for XAML binding
        /// </summary>
        public static readonly DependencyProperty TblEndLineColorProperty =
            DependencyProperty.Register(nameof(TblEndLineColor), typeof(System.Windows.Media.Color), typeof(HexEditor),
                new PropertyMetadata(Colors.Blue, OnTblColorChanged)); // V1 compatible: Blue

        /// <summary>
        /// TblAsciiColor DependencyProperty for XAML binding
        /// </summary>
        public static readonly DependencyProperty TblAsciiColorProperty =
            DependencyProperty.Register(nameof(TblAsciiColor), typeof(System.Windows.Media.Color), typeof(HexEditor),
                new PropertyMetadata(Color.FromRgb(0x42, 0x42, 0x42), OnTblColorChanged)); // V1 compatible: Dark gray (same as normal ASCII)

        /// <summary>
        /// TblJaponaisColor DependencyProperty for XAML binding
        /// </summary>
        public static readonly DependencyProperty TblJaponaisColorProperty =
            DependencyProperty.Register(nameof(TblJaponaisColor), typeof(System.Windows.Media.Color), typeof(HexEditor),
                new PropertyMetadata(Colors.Red, OnTblColorChanged)); // V1 compatible: Red

        /// <summary>
        /// Tbl3ByteColor DependencyProperty for XAML binding (3-byte sequences)
        /// </summary>
        public static readonly DependencyProperty Tbl3ByteColorProperty =
            DependencyProperty.Register(nameof(Tbl3ByteColor), typeof(System.Windows.Media.Color), typeof(HexEditor),
                new PropertyMetadata(Colors.Red, OnTblColorChanged)); // V1 compatible: Red

        /// <summary>
        /// Tbl4PlusByteColor DependencyProperty for XAML binding (4+ byte sequences)
        /// </summary>
        public static readonly DependencyProperty Tbl4PlusByteColorProperty =
            DependencyProperty.Register(nameof(Tbl4PlusByteColor), typeof(System.Windows.Media.Color), typeof(HexEditor),
                new PropertyMetadata(Colors.Red, OnTblColorChanged)); // V1 compatible: Red

        /// <summary>
        /// TblDefaultColor DependencyProperty for XAML binding
        /// </summary>
        public static readonly DependencyProperty TblDefaultColorProperty =
            DependencyProperty.Register(nameof(TblDefaultColor), typeof(System.Windows.Media.Color), typeof(HexEditor),
                new PropertyMetadata(Colors.White, OnTblColorChanged));

        private static void OnTblColorChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is HexEditor editor && editor.HexViewport != null)
            {
                // Sync all TBL colors to HexViewport for rendering
                editor.HexViewport.TblDteColor = editor.TblDteColor;
                editor.HexViewport.TblMteColor = editor.TblMteColor;
                editor.HexViewport.TblAsciiColor = editor.TblAsciiColor;
                editor.HexViewport.TblJaponaisColor = editor.TblJaponaisColor;
                editor.HexViewport.TblEndBlockColor = editor.TblEndBlockColor;
                editor.HexViewport.TblEndLineColor = editor.TblEndLineColor;
                editor.HexViewport.TblDefaultColor = editor.TblDefaultColor;
                editor.HexViewport.Tbl3ByteColor = editor.Tbl3ByteColor;         // NEW: 3-byte color
                editor.HexViewport.Tbl4PlusByteColor = editor.Tbl4PlusByteColor; // NEW: 4+ byte color

                editor.HexViewport.InvalidateVisual();
            }
        }

        /// <summary>
        /// SelectionActiveBrush DependencyProperty for XAML binding
        /// Brush used for selection in the active panel (Hex or ASCII)
        /// </summary>
        public static readonly DependencyProperty SelectionActiveBrushProperty =
            DependencyProperty.Register(nameof(SelectionActiveBrush), typeof(Brush), typeof(HexEditor),
                new PropertyMetadata(new SolidColorBrush(Color.FromArgb(0x66, 0x00, 0x78, 0xD4)), OnSelectionBrushChanged));

        /// <summary>
        /// SelectionInactiveBrush DependencyProperty for XAML binding
        /// Brush used for selection in the inactive panel (Hex or ASCII)
        /// </summary>
        public static readonly DependencyProperty SelectionInactiveBrushProperty =
            DependencyProperty.Register(nameof(SelectionInactiveBrush), typeof(Brush), typeof(HexEditor),
                new PropertyMetadata(new SolidColorBrush(Color.FromArgb(0x33, 0x00, 0x78, 0xD4)), OnSelectionBrushChanged));

        private static void OnSelectionBrushChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is HexEditor editor && e.NewValue is Brush brush && editor.HexViewport != null)
            {
                // Update HexViewport brushes
                if (e.Property == SelectionActiveBrushProperty)
                {
                    editor.HexViewport.SelectionActiveBrush = brush;
                }
                else if (e.Property == SelectionInactiveBrushProperty)
                {
                    editor.HexViewport.SelectionInactiveBrush = brush;
                }
                editor.HexViewport.InvalidateVisual();
            }
        }

        /// <summary>
        /// DataStringVisual DependencyProperty for XAML binding
        /// </summary>
        public static readonly DependencyProperty DataStringVisualProperty =
            DependencyProperty.Register(nameof(DataStringVisual), typeof(DataVisualType), typeof(HexEditor),
                new PropertyMetadata(DataVisualType.Hexadecimal, OnDataStringVisualChanged));

        private static void OnDataStringVisualChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is HexEditor editor && e.NewValue is DataVisualType visualType)
            {
                // Cancel any in-progress byte edit (format-specific editing state no longer valid)
                if (editor._isEditingByte)
                {
                    editor._isEditingByte = false;
                    editor._editingPosition = VirtualPosition.Invalid;
                    editor.HexViewport.EditingBytePosition = -1; // Clear bold nibble feedback
                    editor._editingValue = 0;
                    editor._editingCharIndex = 0;
                    editor._editingMaxChars = 2;
                    editor._editingBuffer = "";
                }

                // Update HexViewport's DataStringVisual property
                if (editor.HexViewport != null)
                {
                    editor.HexViewport.DataStringVisual = visualType;
                    editor.HexViewport.InvalidateVisual();

                    // Refresh column headers to match the new format
                    editor.RefreshColumnHeader();
                }

                editor.RaiseHexStatusChanged();
            }
        }

        /// <summary>
        /// OffSetStringVisual DependencyProperty for XAML binding
        /// </summary>
        public static readonly DependencyProperty OffSetStringVisualProperty =
            DependencyProperty.Register(nameof(OffSetStringVisual), typeof(DataVisualType), typeof(HexEditor),
                new PropertyMetadata(DataVisualType.Hexadecimal, OnOffSetStringVisualChanged));

        private static void OnOffSetStringVisualChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is HexEditor editor && e.NewValue is DataVisualType visualType)
            {
                // Update HexViewport's OffSetStringVisual property
                if (editor.HexViewport != null)
                {
                    editor.HexViewport.OffSetStringVisual = visualType;
                    editor.HexViewport.InvalidateVisual();

                    // Update ActualOffsetWidth to reflect the new format
                    editor.ActualOffsetWidth = new GridLength(editor.HexViewport.ActualOffsetWidth);
                }

                editor.RaiseHexStatusChanged();
            }
        }

        /// <summary>
        /// ActualOffsetWidth DependencyProperty for XAML binding
        /// </summary>
        public static readonly DependencyProperty ActualOffsetWidthProperty =
            DependencyProperty.Register(nameof(ActualOffsetWidth), typeof(GridLength), typeof(HexEditor),
                new PropertyMetadata(new GridLength(110.0))); // Default value matches hexadecimal format

        /// <summary>
        /// ByteOrder DependencyProperty for XAML binding
        /// </summary>
        public static readonly DependencyProperty ByteOrderProperty =
            DependencyProperty.Register(nameof(ByteOrder), typeof(ByteOrderType), typeof(HexEditor),
                new PropertyMetadata(ByteOrderType.LoHi, OnByteOrderChanged));

        private static void OnByteOrderChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is HexEditor editor && e.NewValue is ByteOrderType byteOrder)
            {
                // CRITICAL FIX: Prevent infinite loop - only update ViewModel if value actually changed
                if (editor._viewModel != null && editor._viewModel.ByteOrder == byteOrder)
                    return; // Already synced, avoid recursion

                if (editor._viewModel != null)
                {
                    editor._viewModel.ByteOrder = byteOrder;
                    // ByteOrder change triggers automatic RefreshVisibleLines() in ViewModel
                    // No need to call RefreshColumnHeader() - headers don't depend on ByteOrder
                }
            }
        }

        /// <summary>
        /// ByteSize DependencyProperty for XAML binding
        /// </summary>
        public static readonly DependencyProperty ByteSizeProperty =
            DependencyProperty.Register(nameof(ByteSize), typeof(ByteSizeType), typeof(HexEditor),
                new PropertyMetadata(ByteSizeType.Bit8, OnByteSizeChanged));

        private static void OnByteSizeChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is HexEditor editor && e.NewValue is ByteSizeType byteSize)
            {
                // CRITICAL FIX: Prevent infinite loop - only update ViewModel if value actually changed
                if (editor._viewModel != null && editor._viewModel.ByteSize == byteSize)
                {
                    return; // Already synced, avoid recursion
                }

                if (editor._viewModel != null)
                {
                    editor._viewModel.ByteSize = byteSize;
                    // ByteSize change triggers automatic ClearLineCache() + RefreshVisibleLines() in ViewModel
                    editor.RefreshColumnHeader(); // Update headers to reflect new stride
                    editor.HexViewport.InvalidateVisual(); // Force viewport redraw
                }
            }
        }

        /// <summary>
        /// DefaultCopyToClipboardMode DependencyProperty for XAML binding
        /// </summary>
        public static readonly DependencyProperty DefaultCopyToClipboardModeProperty =
            DependencyProperty.Register(nameof(DefaultCopyToClipboardMode), typeof(CopyPasteMode), typeof(HexEditor),
                new PropertyMetadata(CopyPasteMode.Auto, (d, _) => (d as HexEditor)?.RaiseHexStatusChanged()));

        /// <summary>
        /// VisualCaretMode DependencyProperty for XAML binding
        /// </summary>
        public static readonly DependencyProperty VisualCaretModeProperty =
            DependencyProperty.Register(nameof(VisualCaretMode), typeof(CaretMode), typeof(HexEditor),
                new PropertyMetadata(CaretMode.Insert));

        /// <summary>
        /// ByteShiftLeft DependencyProperty for XAML binding
        /// </summary>
        public static readonly DependencyProperty ByteShiftLeftProperty =
            DependencyProperty.Register(nameof(ByteShiftLeft), typeof(long), typeof(HexEditor),
                new PropertyMetadata(0L, OnByteShiftLeftChanged));

        private static void OnByteShiftLeftChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is HexEditor editor && e.NewValue is long byteShiftLeft)
            {
                // Prevent infinite loop - only update ViewModel if value actually changed
                if (editor._viewModel != null && editor._viewModel.ByteShiftLeft == byteShiftLeft)
                {
                    return; // Already synced, avoid recursion
                }

                if (editor._viewModel != null)
                {
                    editor._viewModel.ByteShiftLeft = byteShiftLeft;
                    // ViewModel triggers RefreshVisibleLines() which updates viewport
                }

                // Refresh viewport to update offset display
                editor.HexViewport?.InvalidateVisual();
            }
        }

        /// <summary>
        /// AppendNeedConfirmation DependencyProperty for XAML binding
        /// </summary>
        public static readonly DependencyProperty AppendNeedConfirmationProperty =
            DependencyProperty.Register(nameof(AppendNeedConfirmation), typeof(bool), typeof(HexEditor),
                new PropertyMetadata(true));

        /// <summary>
        /// CustomEncoding DependencyProperty for XAML binding
        /// </summary>
        public static readonly DependencyProperty CustomEncodingProperty =
            DependencyProperty.Register(nameof(CustomEncoding), typeof(System.Text.Encoding), typeof(HexEditor),
                new PropertyMetadata(System.Text.Encoding.UTF8, OnCustomEncodingChanged));

        private static void OnCustomEncodingChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is HexEditor editor && e.NewValue is System.Text.Encoding encoding)
            {
                // Sync to HexViewport for encoding-aware rendering
                if (editor.HexViewport != null)
                    editor.HexViewport.CustomEncoding = encoding;

                editor.HexViewport?.InvalidateVisual();
            }
        }

        /// <summary>
        /// PreloadByteInEditorMode DependencyProperty for XAML binding
        /// </summary>
        public static readonly DependencyProperty PreloadByteInEditorModeProperty =
            DependencyProperty.Register(nameof(PreloadByteInEditorMode), typeof(PreloadByteInEditor), typeof(HexEditor),
                new PropertyMetadata(PreloadByteInEditor.MaxScreenVisibleLineAtDataLoad, OnPreloadByteInEditorModeChanged));

        /// <summary>
        /// AllowCustomBackgroundBlock DependencyProperty — OBSOLETE.
        /// Kept for XAML/binary compatibility only. Setting this property has no effect.
        /// CustomBackgroundBlocks are always rendered when blocks exist in the service.
        /// The CustomBackgroundService is the sole control point — use AddCustomBackgroundBlock() / ClearCustomBackgroundBlock().
        /// This property will be removed in a future version.
        /// </summary>
        [Obsolete("AllowCustomBackgroundBlock is ignored. CustomBackgroundBlocks render automatically when blocks are added via AddCustomBackgroundBlock(). This property will be removed in a future version.")]
        public static readonly DependencyProperty AllowCustomBackgroundBlockProperty =
            DependencyProperty.Register(nameof(AllowCustomBackgroundBlock), typeof(bool), typeof(HexEditor),
                new PropertyMetadata(true)); // no callback — neutralized; default true for legacy consumers

        /// <summary>
        /// ProgressRefreshRate DependencyProperty for configuring progress bar update frequency
        /// </summary>
        public static readonly DependencyProperty ProgressRefreshRateProperty =
            DependencyProperty.Register(nameof(ProgressRefreshRate), typeof(ProgressRefreshRate), typeof(HexEditor),
                new PropertyMetadata(ProgressRefreshRate.Fast, OnProgressRefreshRateChanged));

        /// <summary>
        /// Progress bar refresh rate for long-running operations (Open, Save, Find, Replace)
        /// </summary>
        public ProgressRefreshRate ProgressRefreshRate
        {
            get => (ProgressRefreshRate)GetValue(ProgressRefreshRateProperty);
            set => SetValue(ProgressRefreshRateProperty, value);
        }

        private static void OnProgressRefreshRateChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is HexEditor editor && e.NewValue is ProgressRefreshRate rate)
            {
                // Update the long-running operation service with new refresh interval
                editor._longRunningService.MinProgressIntervalMs = (int)rate;
            }
        }

        #endregion
    }
}
