////////////////////////////////////////
// Apache 2.0  - 2026
// Author : Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.6
//////////////////////////////////////////////

using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using WpfHexEditor.Core.Search.Models;
using WpfHexEditor.HexEditor.Search.ViewModels;

namespace WpfHexEditor.HexEditor.Search.Views
{
    /// <summary>
    /// VSCode-style inline quick search bar (Ctrl+F overlay).
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

            DataContext = new SearchViewModel();

            // Wire named buttons — avoids binding to ViewModel for host-level actions
            CloseButton.Click += (_, __) => OnCloseRequested?.Invoke(this, EventArgs.Empty);
            AdvancedSearchButton.Click += (_, __) => OnAdvancedSearchRequested?.Invoke(this, EventArgs.Empty);

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
        /// Binds this quick search bar to the given HexEditor.
        /// Wires ByteProvider and result navigation.
        /// </summary>
        public void BindToHexEditor(HexEditor editor)
        {
            // Detach from previous editor
            if (ViewModel != null)
                ViewModel.OnMatchFound -= OnMatchFound;

            _hexEditor = editor;

            if (editor == null) return;

            var vm = ViewModel;
            if (vm == null) return;

            // Provide the ByteProvider so the ViewModel can search
            vm.ByteProvider = editor.GetByteProvider();

            // Navigate to each result in the editor
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
        /// Gets the underlying SearchViewModel.
        /// </summary>
        public SearchViewModel ViewModel => DataContext as SearchViewModel;

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

        #endregion
    }
}
