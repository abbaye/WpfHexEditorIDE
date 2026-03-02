////////////////////////////////////////
// Apache 2.0  - 2026
// Author : Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.6
//////////////////////////////////////////////

using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using WpfHexEditor.Core.Search.Models;
using WpfHexEditor.HexEditor.Search.ViewModels;

namespace WpfHexEditor.HexEditor.Search.Views
{
    /// <summary>
    /// VS-style inline quick search/replace bar (Ctrl+F overlay).
    /// Features:
    ///   • Resizable by dragging the left edge (width only, like Visual Studio).
    ///   • Expand/collapse replace row via the chevron toggle at the far left.
    /// Bind to a HexEditor via <see cref="BindToHexEditor"/> after instantiation.
    /// </summary>
    public partial class QuickSearchBar : UserControl
    {
        #region Fields

        private HexEditor _hexEditor;

        #endregion

        #region Constructor

        public QuickSearchBar()
        {
            InitializeComponent();

            DataContext = new ReplaceViewModel();

            // ── Resize thumb — width-only resize from the left edge ──────────────
            ResizeThumb.DragDelta += ResizeThumb_DragDelta;

            // ── Expand / collapse replace row ────────────────────────────────────
            ExpandReplaceToggle.Checked += (_, __) =>
            {
                // Rotate chevron to point downward (expanded)
                if (ExpandIcon.RenderTransform is RotateTransform rt)
                    rt.Angle = 0;

                // Focus the replace input once the row is visible
                Dispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.Input,
                    new Action(() => ReplaceInput?.Focus()));
            };

            ExpandReplaceToggle.Unchecked += (_, __) =>
            {
                // Rotate chevron back to point right (collapsed)
                if (ExpandIcon.RenderTransform is RotateTransform rt)
                    rt.Angle = -90;

                Dispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.Input,
                    new Action(() => SearchInput?.Focus()));
            };

            // ── Wire named buttons (host-level actions, not bound to ViewModel) ──
            CloseButton.Click           += (_, __) => OnCloseRequested?.Invoke(this, EventArgs.Empty);
            AdvancedSearchButton.Click  += (_, __) => OnAdvancedSearchRequested?.Invoke(this, EventArgs.Empty);

            // Focus search box when the bar becomes visible
            IsVisibleChanged += (_, e) =>
            {
                if ((bool)e.NewValue)
                    Dispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.Input,
                        new Action(() => SearchInput?.Focus()));
            };

            // Esc closes the bar
            KeyDown += (_, e) =>
            {
                if (e.Key == Key.Escape)
                {
                    OnCloseRequested?.Invoke(this, EventArgs.Empty);
                    e.Handled = true;
                }
            };
        }

        #endregion

        #region Public API

        /// <summary>
        /// Binds this quick search/replace bar to the given HexEditor.
        /// Wires ByteProvider and result navigation.
        /// </summary>
        public void BindToHexEditor(HexEditor editor)
        {
            if (ViewModel != null)
                ViewModel.OnMatchFound -= OnMatchFound;

            _hexEditor = editor;

            if (editor == null) return;

            var vm = ViewModel;
            if (vm == null) return;

            vm.ByteProvider = editor.GetByteProvider();
            vm.OnMatchFound += OnMatchFound;
        }

        /// <summary>
        /// Detaches from the bound HexEditor and clears results.
        /// Call this before hiding the bar.
        /// </summary>
        public void Detach()
        {
            if (ViewModel != null)
            {
                ViewModel.OnMatchFound -= OnMatchFound;
                ViewModel.ClearResults();
            }

            _hexEditor = null;
        }

        /// <summary>
        /// Gets the underlying <see cref="ReplaceViewModel"/>.
        /// </summary>
        public ReplaceViewModel ViewModel => DataContext as ReplaceViewModel;

        /// <summary>
        /// Expands the replace row (same as clicking the chevron toggle).
        /// </summary>
        public void ExpandReplace() => ExpandReplaceToggle.IsChecked = true;

        /// <summary>
        /// Collapses the replace row.
        /// </summary>
        public void CollapseReplace() => ExpandReplaceToggle.IsChecked = false;

        #endregion

        #region Events

        /// <summary>
        /// Raised when the user requests to close the bar (✖ or Esc).
        /// </summary>
        public event EventHandler OnCloseRequested;

        /// <summary>
        /// Raised when the user clicks "⋯" to open the Advanced Search dialog.
        /// </summary>
        public event EventHandler OnAdvancedSearchRequested;

        #endregion

        #region Private Helpers

        private void OnMatchFound(object sender, SearchMatch match)
        {
            if (_hexEditor == null || match == null) return;

            _hexEditor.FindSelect(match.Position, match.Length);
            _hexEditor.SetPosition(match.Position);
        }

        /// <summary>
        /// Handles left-edge thumb drag: resize the bar horizontally only.
        /// Dragging left expands, dragging right shrinks.
        /// </summary>
        private void ResizeThumb_DragDelta(object sender, DragDeltaEventArgs e)
        {
            double current = double.IsNaN(Width) ? ActualWidth : Width;
            Width = Math.Max(MinWidth, Math.Min(700, current - e.HorizontalChange));
        }

        #endregion
    }
}
