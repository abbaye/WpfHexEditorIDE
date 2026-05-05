// ==========================================================
// Project: WpfHexEditor.HexEditor
// File: HexEditor.SplitView.cs
// Description:
//     Partial class exposing AttachToProvider / DetachFromProvider so a second
//     HexEditor instance can render the same ByteProvider as a primary instance
//     (split-view scenario). The secondary editor must NEVER call Close(), as
//     that would close the shared provider and break the primary editor.
// Architecture Notes:
//     Used by HexEditorSplitHost. AttachToProvider mirrors the post-load wiring
//     of OpenStream/OpenMemory but skips construction of a new ByteProvider.
//     DetachFromProvider tears down the viewmodel + viewport bindings without
//     touching the provider lifetime.
// ==========================================================

using System;
using System.Windows;
using System.Windows.Controls.Primitives;
using WpfHexEditor.Core.Events;
using WpfHexEditor.HexEditor.ViewModels;

namespace WpfHexEditor.HexEditor
{
    public partial class HexEditor
    {
        // ── ViewModel byte-modified relay ────────────────────────────────────

        /// <summary>
        /// Forwards <see cref="HexEditorViewModel.ByteModified"/> to the public
        /// <see cref="HexEditor.ByteModified"/> event so consumers (shared UndoEngine
        /// bridge, plugins, scrollbar markers) can react to byte edits.
        /// </summary>
        private void ViewModel_ByteModified(object sender, ByteModifiedEventArgs e)
            => OnByteModified(e);

        // ── Split-toggle button DPs + event ──────────────────────────────────

        /// <summary>
        /// When true, the small "Split" toggle becomes visible at the top of the
        /// vertical scrollbar column. Hosts (e.g. <c>HexEditorSplitHost</c>) flip
        /// this on to expose the toggle and listen on <see cref="SplitRequested"/>.
        /// </summary>
        public static readonly DependencyProperty IsSplitToggleVisibleProperty =
            DependencyProperty.Register(nameof(IsSplitToggleVisible), typeof(bool), typeof(HexEditor),
                new PropertyMetadata(true, OnIsSplitToggleVisibleChanged));

        public bool IsSplitToggleVisible
        {
            get => (bool)GetValue(IsSplitToggleVisibleProperty);
            set => SetValue(IsSplitToggleVisibleProperty, value);
        }

        private static void OnIsSplitToggleVisibleChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is HexEditor self && self.SplitToggleButton is not null)
                self.SplitToggleButton.Visibility = (bool)e.NewValue
                    ? Visibility.Visible
                    : Visibility.Collapsed;
        }

        /// <summary>
        /// Reflects the host's split state. The host sets it after open/close so
        /// the toggle stays in sync (button checked when split is open).
        /// </summary>
        public static readonly DependencyProperty IsSplitOpenProperty =
            DependencyProperty.Register(nameof(IsSplitOpen), typeof(bool), typeof(HexEditor),
                new PropertyMetadata(false, OnIsSplitOpenChanged));

        public bool IsSplitOpen
        {
            get => (bool)GetValue(IsSplitOpenProperty);
            set => SetValue(IsSplitOpenProperty, value);
        }

        private static void OnIsSplitOpenChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is HexEditor self && self.SplitToggleButton is ToggleButton btn)
                btn.IsChecked = (bool)e.NewValue;
        }

        /// <summary>
        /// Raised when the user clicks the split toggle. Hosts handle this by
        /// opening or closing the secondary pane and updating <see cref="IsSplitOpen"/>.
        /// </summary>
        public event EventHandler<bool>? SplitRequested;

        private void SplitToggleButton_Click(object sender, RoutedEventArgs e)
        {
            if (sender is ToggleButton btn)
                SplitRequested?.Invoke(this, btn.IsChecked == true);
        }

        /// <summary>
        /// The underlying <see cref="WpfHexEditor.Core.Bytes.ByteProvider"/> that backs the
        /// active document. Null until a file/stream has been opened or
        /// <see cref="AttachToProvider"/> has been called.
        /// Exposed for split-view scenarios where a host wires a secondary editor to
        /// the same provider as a primary editor.
        /// </summary>
        public WpfHexEditor.Core.Bytes.ByteProvider Provider => _viewModel?.Provider;

        /// <summary>
        /// Attaches this editor to an externally-owned <see cref="WpfHexEditor.Core.Bytes.ByteProvider"/>.
        /// Use only for split-view secondary panes. The provider's lifetime is owned
        /// by the caller — this editor must not be closed via <see cref="Close"/>;
        /// call <see cref="DetachFromProvider"/> instead.
        /// </summary>
        /// <param name="provider">Already-opened ByteProvider shared with the primary pane.</param>
        public void AttachToProvider(WpfHexEditor.Core.Bytes.ByteProvider provider)
        {
            if (provider == null) throw new ArgumentNullException(nameof(provider));

            try
            {
                _isOpeningFile = true;

                // No Close() — the provider belongs to the primary pane.
                if (_viewModel != null)
                {
                    _viewModel.PropertyChanged -= ViewModel_PropertyChanged;
                    _viewModel.ByteModified   -= ViewModel_ByteModified;
                    _viewModel = null;
                }

                _viewModel = new HexEditorViewModel(provider);
                HexViewport.LinesSource = _viewModel.Lines;

                _viewModel.BytePerLine = BytePerLine;
                HexViewport.BytesPerLine = BytePerLine;
                _viewModel.EditMode = EditMode;
                HexViewport.EditMode = EditMode;
                _viewModel.ByteSize = ByteSize;
                _viewModel.ByteOrder = ByteOrder;
                _viewModel.ByteShiftLeft = ByteShiftLeft;

                HexViewport.ByteSpacerPositioning  = ByteSpacerPositioning;
                HexViewport.ByteSpacerWidthTickness = ByteSpacerWidthTickness;
                HexViewport.ByteGrouping           = ByteGrouping;
                HexViewport.ByteSpacerVisualStyle  = ByteSpacerVisualStyle;

                var normalBrush    = Resources["ByteForegroundBrush"]          as System.Windows.Media.Brush;
                var alternateBrush = Resources["AlternateByteForegroundBrush"] as System.Windows.Media.Brush;
                HexViewport.SetByteForegroundColors(normalBrush, alternateBrush);

                FileName             = null;
                IsModified           = provider.HasChanges;
                IsFileOrStreamLoaded = true;

                _viewModel.PropertyChanged += ViewModel_PropertyChanged;
                _viewModel.ByteModified   += ViewModel_ByteModified;

                Dispatcher.BeginInvoke(new Action(UpdateVisibleLines),
                    System.Windows.Threading.DispatcherPriority.ApplicationIdle);

                VerticalScroll.Maximum     = Math.Max(0, _viewModel.TotalLines - _viewModel.VisibleLines + 3);
                VerticalScroll.ViewportSize = _viewModel.VisibleLines;

                OnFileOpened(EventArgs.Empty);
                RaiseHexStatusChanged();
            }
            finally
            {
                _isOpeningFile = false;
            }
        }

        /// <summary>
        /// Detaches this editor from the shared provider without closing it.
        /// Releases viewport bindings + viewmodel; the underlying provider is
        /// kept alive for the primary pane.
        /// </summary>
        public void DetachFromProvider()
        {
            if (_viewModel != null)
            {
                _viewModel.PropertyChanged -= ViewModel_PropertyChanged;
                _viewModel.ByteModified   -= ViewModel_ByteModified;
                // Note: no _viewModel.Close() — shared provider must stay open.
                _viewModel = null;
            }

            if (HexViewport != null)
            {
                HexViewport.LinesSource = null;
                HexViewport.InvalidateCustomBackgroundCache();
                HexViewport.Refresh();
            }

            FileName             = null;
            IsModified           = false;
            IsFileOrStreamLoaded = false;
        }
    }
}
