////////////////////////////////////////
// GNU Affero General Public License v3.0 - 2026
// Author : Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.6
//////////////////////////////////////////////

using System;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Input;
using WpfHexEditor.Editor.Core.ViewModels;

namespace WpfHexEditor.Editor.Core.Views
{
    /// <summary>
    /// VS Code-style inline quick search/replace bar.
    /// <para>
    ///   • 3-row VS-style layout: Search | Replace (collapsible) | Options (toggles).
    ///   • Drag the bottom-left grip (::) anywhere in the parent <see cref="Canvas"/> (drag-to-move).
    ///   • Resize the width by dragging the transparent left-edge <see cref="ResizeThumb"/>.
    ///   • Capabilities (toggles, replace row, custom filters, advanced button) are
    ///     automatically shown/hidden after calling <see cref="BindToTarget"/>.
    ///   • Supports any editor that implements <see cref="ISearchTarget"/>.
    /// </para>
    /// </summary>
    public partial class QuickSearchBar : UserControl
    {
        #region Fields

        private ISearchTarget? _target;
        private bool _hasCustomPosition;

        #endregion

        #region Constructor

        public QuickSearchBar()
        {
            InitializeComponent();
            DataContext = new SearchBarViewModel();

            // -- Expand / collapse replace row ------------------------------------
            ExpandReplaceToggle.Checked += (_, __) =>
            {
                // Icon opacity: full when replace row is open
                ExpandIcon.Opacity = 1.0;
                Dispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.Input,
                    new Action(() => ReplaceInput?.Focus()));
            };

            ExpandReplaceToggle.Unchecked += (_, __) =>
            {
                // Icon opacity: dimmed when replace row is hidden
                ExpandIcon.Opacity = 0.6;
                Dispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.Input,
                    new Action(() => SearchInput?.Focus()));
            };

            // -- Drag-to-move (styled MoveThumb, bottom-left) ---------------------
            MoveThumb.DragDelta += MoveThumb_DragDelta;

            // -- Width-only resize (transparent ResizeThumb, left edge) ------------
            ResizeThumb.DragDelta += ResizeThumb_DragDelta;

            // -- Host-level actions (not bound to ViewModel) -----------------------
            CloseButton.Click          += (_, __) => OnCloseRequested?.Invoke(this, EventArgs.Empty);
            AdvancedSearchButton.Click += (_, __) => OnAdvancedSearchRequested?.Invoke(this, EventArgs.Empty);

            // Auto-focus the search input whenever the bar becomes visible
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

        /// <summary>Gets the current search text from the ViewModel.</summary>
        public string SearchText => ViewModel?.SearchText ?? string.Empty;

        /// <summary>Gets the underlying <see cref="SearchBarViewModel"/>.</summary>
        public SearchBarViewModel? ViewModel => DataContext as SearchBarViewModel;

        /// <summary>
        /// Binds this bar to the given <see cref="ISearchTarget"/> and adapts the visible
        /// controls to the target's <see cref="SearchBarCapabilities"/>.
        /// </summary>
        public void BindToTarget(ISearchTarget target)
        {
            _target = target;
            ViewModel?.BindToTarget(target);
            ApplyCapabilities(target.Capabilities);
        }

        /// <summary>
        /// Detaches from the current target and clears search state.
        /// Call this before hiding the bar.
        /// </summary>
        public void Detach()
        {
            ViewModel?.Detach();
            _target = null;
        }

        /// <summary>Expands the replace row (same as clicking the chevron toggle).</summary>
        public void ExpandReplace() => ExpandReplaceToggle.IsChecked = true;

        /// <summary>Collapses the replace row.</summary>
        public void CollapseReplace() => ExpandReplaceToggle.IsChecked = false;

        /// <summary>
        /// Focuses the search input TextBox and selects all text.
        /// Useful when the bar is already visible and the host wants to re-focus it.
        /// </summary>
        public void FocusSearchInput()
        {
            Dispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.Input,
                new Action(() =>
                {
                    SearchInput?.Focus();
                    SearchInput?.SelectAll();
                }));
        }

        /// <summary>
        /// Sets the bar's initial position (top-right of parent Canvas with a margin),
        /// unless the user has already moved it manually.
        /// </summary>
        public void EnsureDefaultPosition(Canvas canvas)
        {
            if (_hasCustomPosition) return;

            double left = Math.Max(0, canvas.ActualWidth - Width - 24);
            Canvas.SetLeft(this, left);
            Canvas.SetTop(this, 4);
        }

        /// <summary>
        /// Resets the bar's position to the default (top-right of parent Canvas).
        /// Call this e.g. from a reset button or when reopening the editor.
        /// </summary>
        public void ResetPosition() => _hasCustomPosition = false;

        #endregion

        #region Events

        /// <summary>Raised when the user closes the bar (✖ button or Esc).</summary>
        public event EventHandler? OnCloseRequested;

        /// <summary>Raised when the user clicks "⋯" to open the advanced search dialog.</summary>
        public event EventHandler? OnAdvancedSearchRequested;

        #endregion

        #region Capability-driven Visibility

        /// <summary>
        /// Adjusts the visibility of controls in the bar to match what the editor supports.
        /// </summary>
        private void ApplyCapabilities(SearchBarCapabilities caps)
        {
            bool hasCaseSensitive  = caps.HasFlag(SearchBarCapabilities.CaseSensitive);
            bool hasWildcard       = caps.HasFlag(SearchBarCapabilities.Wildcard);
            bool hasHexMode        = caps.HasFlag(SearchBarCapabilities.HexMode);
            bool hasReplace        = caps.HasFlag(SearchBarCapabilities.Replace);
            bool hasAdvanced       = caps.HasFlag(SearchBarCapabilities.AdvancedSearch);
            bool hasCustomFilters  = caps.HasFlag(SearchBarCapabilities.CustomFilters);

            // -- Options row toggles (Row 2) --------------------------------------
            AbToggle.Visibility      = hasCaseSensitive ? Visibility.Visible : Visibility.Collapsed;
            WildcardToggle.Visibility = hasWildcard      ? Visibility.Visible : Visibility.Collapsed;
            RegexToggle.Visibility    = hasWildcard      ? Visibility.Visible : Visibility.Collapsed;
            HexModeToggle.Visibility  = hasHexMode       ? Visibility.Visible : Visibility.Collapsed;

            // Separator in options row: hide if no toggles and no custom filters
            bool anyToggleOrFilter = hasCaseSensitive || hasWildcard || hasHexMode || hasCustomFilters;
            OptionsSeparator.Visibility = anyToggleOrFilter ? Visibility.Visible : Visibility.Collapsed;

            // -- Replace row (Row 1) ----------------------------------------------
            ExpandReplaceToggle.Visibility = hasReplace ? Visibility.Visible : Visibility.Collapsed;
            if (!hasReplace)
            {
                ExpandReplaceToggle.IsChecked = false;
                ReplaceRow.Visibility = Visibility.Collapsed;
            }

            // -- Advanced search button + its separator (Row 0) -------------------
            AdvancedSearchButton.Visibility = hasAdvanced ? Visibility.Visible : Visibility.Collapsed;
            ActionsSeparator.Visibility     = hasAdvanced ? Visibility.Visible : Visibility.Collapsed;

            // -- Custom filters zone (Row 2) --------------------------------------
            if (hasCustomFilters && _target != null)
            {
                var content = _target.GetCustomFiltersContent();
                CustomFiltersZone.Content    = content;
                CustomFiltersZone.Visibility = content != null ? Visibility.Visible : Visibility.Collapsed;
            }
            else
            {
                CustomFiltersZone.Content    = null;
                CustomFiltersZone.Visibility = Visibility.Collapsed;
            }
        }

        #endregion

        #region Drag-to-Move (MoveThumb)

        private void MoveThumb_DragDelta(object sender, DragDeltaEventArgs e)
        {
            if (Parent is not Canvas canvas) return;

            double left = Canvas.GetLeft(this);
            double top  = Canvas.GetTop(this);

            // Clamp to canvas bounds
            double newLeft = Math.Max(0, Math.Min(canvas.ActualWidth  - ActualWidth,  left + e.HorizontalChange));
            double newTop  = Math.Max(0, Math.Min(canvas.ActualHeight - ActualHeight, top  + e.VerticalChange));

            Canvas.SetLeft(this, newLeft);
            Canvas.SetTop(this, newTop);
            _hasCustomPosition = true;
        }

        #endregion

        #region Width-only Resize (ResizeThumb)

        /// <summary>
        /// Resizes the bar horizontally by dragging the left edge.
        /// Dragging left expands (right edge stays fixed); dragging right shrinks.
        /// Adjusts <c>Canvas.Left</c> to keep the right edge anchored.
        /// </summary>
        private void ResizeThumb_DragDelta(object sender, DragDeltaEventArgs e)
        {
            double current  = double.IsNaN(Width) ? ActualWidth : Width;
            double newWidth = Math.Max(MinWidth, Math.Min(700, current - e.HorizontalChange));
            double delta    = current - newWidth;   // positive = bar got narrower

            // Shift left edge so the right edge stays in the same place
            double left = Canvas.GetLeft(this);
            if (!double.IsNaN(left))
                Canvas.SetLeft(this, left + delta);

            Width = newWidth;
        }

        #endregion
    }

    // ═══════════════════════════════════════════════════════════════════════════
    //  Value Converters (defined here to avoid a separate file for a small class)
    // ═══════════════════════════════════════════════════════════════════════════

    /// <summary>Converts <see cref="bool"/> to <see cref="Visibility"/>.</summary>
    public sealed class BoolToVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
            => value is bool b && b ? Visibility.Visible : Visibility.Collapsed;

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => throw new NotImplementedException();
    }

    /// <summary>
    /// Returns <see cref="Visibility.Visible"/> when the string is null/empty
    /// (used for placeholder text), <see cref="Visibility.Collapsed"/> otherwise.
    /// </summary>
    public sealed class StringEmptyToVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
            => string.IsNullOrEmpty(value as string) ? Visibility.Visible : Visibility.Collapsed;

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => throw new NotImplementedException();
    }
}
