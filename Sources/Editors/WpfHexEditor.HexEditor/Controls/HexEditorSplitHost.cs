// ==========================================================
// Project: WpfHexEditor.HexEditor
// File: Controls/HexEditorSplitHost.cs
// Description:
//     Host container that wraps two HexEditor instances sharing the same
//     ByteProvider. The split toggle is rendered inline by each HexEditor
//     (top of the vertical scrollbar column); the host handles the
//     SplitRequested event to open / close the secondary pane.
//     Implements IDocumentEditor by delegating to the most-recently-focused
//     editor so the docking host interacts with the split transparently.
// Architecture Notes:
//     Proxy / Delegate Pattern  — IDocumentEditor forwarded to _activeEditor.
//     Composite                 — wraps two HexEditor children sharing one
//                                  ByteProvider (single edit + undo history).
//     Observer                  — GotFocus on each pane updates _activeEditor.
//     Standalone-safe: no IDE-only dependencies; the host runs in pure WPF.
// ==========================================================

using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using WpfHexEditor.Core.Events;
using WpfHexEditor.Editor.Core;

namespace WpfHexEditor.HexEditor.Controls
{
    /// <summary>
    /// A split-view host for <see cref="HexEditor"/>. Both editors share the same
    /// <see cref="WpfHexEditor.Core.Bytes.ByteProvider"/>; scroll positions and
    /// selections are independent. Edits in one pane are visible immediately in
    /// the other; undo/redo is unified through the shared provider.
    /// </summary>
    public sealed class HexEditorSplitHost : Grid, IDocumentEditor
    {
        private const double SplitterHeight = 4.0;

        private readonly HexEditor _primaryEditor;
        private          HexEditor _secondaryEditor; // lazily created on first split
        private GridSplitter       _splitter        = null!;
        private Border             _primaryFocusBorder   = null!;
        private Border             _secondaryFocusBorder = null!;

        private RowDefinition _primaryRow   = null!;
        private RowDefinition _splitterRow  = null!;
        private RowDefinition _secondaryRow = null!;

        // The pane that most recently received focus — IDocumentEditor delegates here.
        private HexEditor _activeEditor;

        // ── Constructors ─────────────────────────────────────────────────────

        /// <summary>Wraps a fresh <see cref="HexEditor"/> instance.</summary>
        public HexEditorSplitHost() : this(new HexEditor()) { }

        /// <summary>Wraps an existing <see cref="HexEditor"/> as the primary pane.</summary>
        public HexEditorSplitHost(HexEditor primaryEditor)
        {
            if (primaryEditor == null) throw new ArgumentNullException(nameof(primaryEditor));
            _primaryEditor = primaryEditor;
            _primaryEditor.IsSplitToggleVisible = true;
            BuildLayout();
        }

        private void BuildLayout()
        {
            // Row layout:
            //  Row 0 = primary HexEditor (star)
            //  Row 1 = GridSplitter (auto, hidden when not split)
            //  Row 2 = secondary HexEditor (0 → star when split active)
            _primaryRow   = new RowDefinition { Height = new GridLength(1, GridUnitType.Star) };
            _splitterRow  = new RowDefinition { Height = new GridLength(0) };
            _secondaryRow = new RowDefinition { Height = new GridLength(0) };

            RowDefinitions.Add(_primaryRow);
            RowDefinitions.Add(_splitterRow);
            RowDefinitions.Add(_secondaryRow);

            // -- Primary editor (Row 0, wrapped in a focus border) ----------------
            // _primaryEditor was assigned in the constructor.
            _primaryEditor.SplitRequested += OnPrimarySplitRequested;

            _primaryFocusBorder = new Border
            {
                BorderThickness = new Thickness(2),
                BorderBrush     = Brushes.Transparent,
                Child           = _primaryEditor
            };
            SetRow(_primaryFocusBorder, 0);
            Children.Add(_primaryFocusBorder);

            // -- Splitter (Row 1, hidden until split active) ----------------------
            _splitter = new GridSplitter
            {
                Height              = SplitterHeight,
                HorizontalAlignment = HorizontalAlignment.Stretch,
                VerticalAlignment   = VerticalAlignment.Stretch,
                ResizeBehavior      = GridResizeBehavior.PreviousAndNext,
                ResizeDirection     = GridResizeDirection.Rows,
                Visibility          = Visibility.Collapsed,
            };
            _splitter.SetResourceReference(GridSplitter.BackgroundProperty, "DockBorderBrush");
            SetRow(_splitter, 1);
            Children.Add(_splitter);

            // -- Secondary border placeholder (Row 2, populated on first split) ---
            _secondaryFocusBorder = new Border
            {
                BorderThickness = new Thickness(2),
                BorderBrush     = Brushes.Transparent,
                Visibility      = Visibility.Collapsed,
            };
            SetRow(_secondaryFocusBorder, 2);
            Children.Add(_secondaryFocusBorder);

            // -- Active editor + focus tracking -----------------------------------
            _activeEditor = _primaryEditor;
            _primaryEditor.GotFocus += OnPrimaryGotFocus;

            // -- Forward IDocumentEditor events from primary ----------------------
            WireEditorEvents(_primaryEditor);

            // -- Allow the Grid host itself to receive keyboard focus -------------
            Focusable        = true;
            FocusVisualStyle = null;
        }

        // ── Public surface ───────────────────────────────────────────────────

        /// <summary>The primary (always present) hex editor pane.</summary>
        public HexEditor PrimaryEditor => _primaryEditor;

        /// <summary>The secondary pane, or null when the split is closed.</summary>
        public HexEditor SecondaryEditor => _secondaryEditor;

        /// <summary>True when a split is currently open.</summary>
        public bool IsSplitOpen => _secondaryEditor != null;

        /// <summary>Convenience pass-through to <see cref="HexEditor.OpenFile"/>.</summary>
        public void OpenFile(string filePath) => _primaryEditor.OpenFile(filePath);

        /// <summary>Convenience pass-through to <see cref="HexEditor.OpenStream"/>.</summary>
        public void OpenStream(Stream stream, bool readOnly = false)
            => _primaryEditor.OpenStream(stream, readOnly);

        // ── Split toggle (driven by the inline HexEditor button) ─────────────

        private void OnPrimarySplitRequested(object sender, bool wantsOpen)
        {
            if (wantsOpen) OpenSecondary();
            else           CloseSecondary();
        }

        private void OpenSecondary()
        {
            var provider = _primaryEditor.Provider;
            if (provider == null)
            {
                _primaryEditor.IsSplitOpen = false;
                return;
            }

            if (_secondaryEditor == null)
            {
                _secondaryEditor = new HexEditor
                {
                    IsSplitToggleVisible = false, // toggle lives only on the primary
                    ShowStatusBar        = false,  // status bar belongs to the primary pane only
                };
                _secondaryEditor.GotFocus += OnSecondaryGotFocus;
                WireEditorEvents(_secondaryEditor);
                _secondaryFocusBorder.Child = _secondaryEditor;
            }

            _secondaryEditor.AttachToProvider(provider);

            // Propagate custom background blocks (whfmt format detection overlays, bookmarks, etc.)
            SyncCustomBackgroundBlocks();
            _primaryEditor.CustomBackgroundBlockChanged += OnPrimaryBlocksChanged;

            // When a byte is edited in either pane, refresh the other pane so both stay in sync.
            _primaryEditor.ByteModified += OnPrimaryByteModified;
            _secondaryEditor.ByteModified += OnSecondaryByteModified;

            double currentHeight = _primaryRow.ActualHeight;
            double half = Math.Max(80, currentHeight / 2);
            _primaryRow.Height    = new GridLength(half, GridUnitType.Star);
            _splitterRow.Height   = GridLength.Auto;
            _secondaryRow.Height  = new GridLength(half, GridUnitType.Star);
            _splitter.Visibility  = Visibility.Visible;
            _secondaryFocusBorder.Visibility = Visibility.Visible;

            _primaryEditor.IsSplitOpen = true;
            _secondaryEditor.Focus();
        }

        private void CloseSecondary()
        {
            _splitterRow.Height  = new GridLength(0);
            _secondaryRow.Height = new GridLength(0);
            _splitter.Visibility = Visibility.Collapsed;
            _secondaryFocusBorder.Visibility = Visibility.Collapsed;

            _primaryEditor.CustomBackgroundBlockChanged -= OnPrimaryBlocksChanged;
            _primaryEditor.ByteModified                -= OnPrimaryByteModified;

            if (_secondaryEditor != null)
            {
                _secondaryEditor.ByteModified -= OnSecondaryByteModified;
                UnwireEditorEvents(_secondaryEditor);
                _secondaryEditor.GotFocus -= OnSecondaryGotFocus;
                _secondaryEditor.DetachFromProvider();
                _secondaryFocusBorder.Child = null;
                _secondaryEditor = null;
            }

            _primaryEditor.IsSplitOpen = false;
            _activeEditor = _primaryEditor;
            UpdateFocusBorders();
            _primaryEditor.Focus();
        }

        // ── Secondary pane sync ──────────────────────────────────────────────

        // Called by the primary editor's ByteModified event so the secondary viewport
        // repaints when the user edits bytes in the primary (or vice-versa).
        private void OnPrimaryByteModified(object sender, Core.Events.ByteModifiedEventArgs e)
        {
            if (_secondaryEditor == null) return;
            // The secondary shares the same ByteProvider; its ViewModel already has the
            // updated data — just trigger a visual refresh.
            _secondaryEditor.RefreshView(refreshData: true);
        }

        private void OnSecondaryByteModified(object sender, Core.Events.ByteModifiedEventArgs e)
        {
            _primaryEditor.RefreshView(refreshData: true);
        }

        private void SyncCustomBackgroundBlocks()
        {
            if (_secondaryEditor == null) return;
            _secondaryEditor.ClearCustomBackgroundBlock();
            foreach (var block in _primaryEditor.CustomBackgroundService.GetAllBlocks())
                _secondaryEditor.AddCustomBackgroundBlock(block);
        }

        private void OnPrimaryBlocksChanged(object sender, Core.Events.CustomBackgroundBlockEventArgs e)
        {
            SyncCustomBackgroundBlocks();
        }

        // ── Focus tracking ───────────────────────────────────────────────────

        private void OnPrimaryGotFocus(object sender, RoutedEventArgs e)
        {
            _activeEditor = _primaryEditor;
            UpdateFocusBorders();
        }

        private void OnSecondaryGotFocus(object sender, RoutedEventArgs e)
        {
            if (_secondaryEditor == null) return;
            _activeEditor = _secondaryEditor;
            UpdateFocusBorders();
        }

        private void UpdateFocusBorders()
        {
            var active   = TryFindResource("CE_FocusBorder") as Brush
                         ?? TryFindResource("DockTabActiveBorderBrush") as Brush
                         ?? Brushes.DodgerBlue;
            var inactive = Brushes.Transparent;

            _primaryFocusBorder.BorderBrush   = ReferenceEquals(_activeEditor, _primaryEditor)   ? active : inactive;
            _secondaryFocusBorder.BorderBrush = ReferenceEquals(_activeEditor, _secondaryEditor) ? active : inactive;
        }

        // ── IDocumentEditor — State ──────────────────────────────────────────

        bool IDocumentEditor.IsDirty => ((IDocumentEditor)_primaryEditor).IsDirty;
        // Undo/Redo state and commands always route through the primary editor where the
        // shared UndoEngine (DocumentManager) or ByteProvider undo stack is attached.
        bool IDocumentEditor.CanUndo => ((IDocumentEditor)_primaryEditor).CanUndo;
        bool IDocumentEditor.CanRedo => ((IDocumentEditor)_primaryEditor).CanRedo;

        bool IDocumentEditor.IsReadOnly
        {
            get => ((IDocumentEditor)_primaryEditor).IsReadOnly;
            set
            {
                ((IDocumentEditor)_primaryEditor).IsReadOnly = value;
                if (_secondaryEditor != null) ((IDocumentEditor)_secondaryEditor).IsReadOnly = value;
            }
        }

        bool IDocumentEditor.IsBusy => ((IDocumentEditor)_primaryEditor).IsBusy;

        public string Title => _primaryEditor.Title;

        // ── IDocumentEditor — Commands (delegate to active pane) ─────────────

        ICommand IDocumentEditor.UndoCommand      => ((IDocumentEditor)_primaryEditor).UndoCommand;
        ICommand IDocumentEditor.RedoCommand      => ((IDocumentEditor)_primaryEditor).RedoCommand;
        ICommand IDocumentEditor.SaveCommand      => ((IDocumentEditor)_primaryEditor).SaveCommand;
        ICommand IDocumentEditor.CopyCommand      => ((IDocumentEditor)_activeEditor).CopyCommand;
        ICommand IDocumentEditor.CutCommand       => ((IDocumentEditor)_activeEditor).CutCommand;
        ICommand IDocumentEditor.PasteCommand     => ((IDocumentEditor)_activeEditor).PasteCommand;
        ICommand IDocumentEditor.DeleteCommand    => ((IDocumentEditor)_activeEditor).DeleteCommand;
        ICommand IDocumentEditor.SelectAllCommand => ((IDocumentEditor)_activeEditor).SelectAllCommand;

        // ── IDocumentEditor — Methods ────────────────────────────────────────

        void IDocumentEditor.Undo()      => _primaryEditor.Undo();
        void IDocumentEditor.Redo()      => _primaryEditor.Redo();
        void IDocumentEditor.Save()      => ((IDocumentEditor)_primaryEditor).Save();
        Task IDocumentEditor.SaveAsync(CancellationToken ct)
            => ((IDocumentEditor)_primaryEditor).SaveAsync(ct);
        Task IDocumentEditor.SaveAsAsync(string filePath, CancellationToken ct)
            => ((IDocumentEditor)_primaryEditor).SaveAsAsync(filePath, ct);
        void IDocumentEditor.Copy()      => ((IDocumentEditor)_activeEditor).Copy();
        void IDocumentEditor.Cut()       => ((IDocumentEditor)_activeEditor).Cut();
        void IDocumentEditor.Paste()     => ((IDocumentEditor)_activeEditor).Paste();
        void IDocumentEditor.Delete()    => ((IDocumentEditor)_activeEditor).Delete();
        void IDocumentEditor.SelectAll() => _activeEditor.SelectAll();

        void IDocumentEditor.Close()
        {
            // Tear down the secondary first, then close the primary (which closes the provider).
            CloseSecondary();
            _primaryEditor.Close();
        }

        void IDocumentEditor.CancelOperation()
            => ((IDocumentEditor)_primaryEditor).CancelOperation();

        // ── IDocumentEditor — Events (relay from primary; secondary forwards too) ──

        public event EventHandler                                      ModifiedChanged;
        public event EventHandler                                      CanUndoChanged;
        public event EventHandler                                      CanRedoChanged;
        public event EventHandler<string>                              TitleChanged;
        public event EventHandler<string>                              StatusMessage;

        // HexEditor does not produce verbose OutputMessage events.
#pragma warning disable CS0067
        public event EventHandler<string>                              OutputMessage;
#pragma warning restore CS0067

        public event EventHandler                                      SelectionChanged;
        public event EventHandler<DocumentOperationEventArgs>          OperationStarted;
        public event EventHandler<DocumentOperationEventArgs>          OperationProgress;
        public event EventHandler<DocumentOperationCompletedEventArgs> OperationCompleted;

        private void WireEditorEvents(HexEditor editor)
        {
            var doc = (IDocumentEditor)editor;
            doc.ModifiedChanged    += RelayModifiedChanged;
            doc.CanUndoChanged     += RelayCanUndoChanged;
            doc.CanRedoChanged     += RelayCanRedoChanged;
            doc.TitleChanged       += RelayTitleChanged;
            doc.StatusMessage      += RelayStatusMessage;
            doc.SelectionChanged   += RelaySelectionChanged;
            doc.OperationStarted   += RelayOperationStarted;
            doc.OperationProgress  += RelayOperationProgress;
            doc.OperationCompleted += RelayOperationCompleted;
        }

        private void UnwireEditorEvents(HexEditor editor)
        {
            var doc = (IDocumentEditor)editor;
            doc.ModifiedChanged    -= RelayModifiedChanged;
            doc.CanUndoChanged     -= RelayCanUndoChanged;
            doc.CanRedoChanged     -= RelayCanRedoChanged;
            doc.TitleChanged       -= RelayTitleChanged;
            doc.StatusMessage      -= RelayStatusMessage;
            doc.SelectionChanged   -= RelaySelectionChanged;
            doc.OperationStarted   -= RelayOperationStarted;
            doc.OperationProgress  -= RelayOperationProgress;
            doc.OperationCompleted -= RelayOperationCompleted;
        }

        private void RelayModifiedChanged    (object s, EventArgs e)                              => ModifiedChanged?.Invoke(this, e);
        private void RelayCanUndoChanged     (object s, EventArgs e)                              => CanUndoChanged?.Invoke(this, e);
        private void RelayCanRedoChanged     (object s, EventArgs e)                              => CanRedoChanged?.Invoke(this, e);
        private void RelayTitleChanged       (object s, string e)                                 => TitleChanged?.Invoke(this, e);
        private void RelayStatusMessage      (object s, string e)                                 => StatusMessage?.Invoke(this, e);
        private void RelaySelectionChanged   (object s, EventArgs e)                              => SelectionChanged?.Invoke(this, e);
        private void RelayOperationStarted   (object s, DocumentOperationEventArgs e)             => OperationStarted?.Invoke(this, e);
        private void RelayOperationProgress  (object s, DocumentOperationEventArgs e)             => OperationProgress?.Invoke(this, e);
        private void RelayOperationCompleted (object s, DocumentOperationCompletedEventArgs e)    => OperationCompleted?.Invoke(this, e);
    }
}
