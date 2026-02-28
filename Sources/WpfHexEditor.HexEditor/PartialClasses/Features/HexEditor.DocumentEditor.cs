//////////////////////////////////////////////
// Apache 2.0  - 2026
// Author : Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Opus 4.6
//////////////////////////////////////////////

using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Input;
using WpfHexEditor.Editor.Core;

namespace WpfHexEditor.HexEditor
{
    /// <summary>
    /// HexEditor partial class — IDocumentEditor implementation.
    /// Bridges the existing HexEditor API to the unified editor contract
    /// used by the docking host and other multi-editor scenarios.
    /// </summary>
    public partial class HexEditor
    {
        // ═══════════════════════════════════════════════════════════════════
        // IDocumentEditor — State
        // ═══════════════════════════════════════════════════════════════════

        /// <inheritdoc />
        bool IDocumentEditor.IsDirty => IsModified;

        /// <inheritdoc />
        bool IDocumentEditor.IsReadOnly
        {
            get => ReadOnlyMode;
            set => ReadOnlyMode = value;
        }

        /// <inheritdoc />
        public string Title
        {
            get
            {
                var name = string.IsNullOrEmpty(FileName)
                    ? "Untitled"
                    : Path.GetFileName(FileName);
                return IsModified ? $"{name} *" : name;
            }
        }

        // ═══════════════════════════════════════════════════════════════════
        // IDocumentEditor — Commands
        // ═══════════════════════════════════════════════════════════════════

        private ICommand _docEditorUndoCommand;
        private ICommand _docEditorRedoCommand;
        private ICommand _docEditorSaveCommand;
        private ICommand _docEditorCopyCommand;
        private ICommand _docEditorCutCommand;
        private ICommand _docEditorPasteCommand;
        private ICommand _docEditorDeleteCommand;
        private ICommand _docEditorSelectAllCommand;

        /// <inheritdoc />
        ICommand IDocumentEditor.UndoCommand =>
            _docEditorUndoCommand ?? (_docEditorUndoCommand = new HexEditorRelayCommand(_ => Undo(), _ => CanUndo));

        /// <inheritdoc />
        ICommand IDocumentEditor.RedoCommand =>
            _docEditorRedoCommand ?? (_docEditorRedoCommand = new HexEditorRelayCommand(_ => Redo(), _ => CanRedo));

        /// <inheritdoc />
        ICommand IDocumentEditor.SaveCommand =>
            _docEditorSaveCommand ?? (_docEditorSaveCommand = new HexEditorRelayCommand(_ => Save(), _ => IsModified && _viewModel != null));

        /// <inheritdoc />
        ICommand IDocumentEditor.CopyCommand =>
            _docEditorCopyCommand ?? (_docEditorCopyCommand = new HexEditorRelayCommand(_ => ((IDocumentEditor)this).Copy(), _ => HasSelection));

        /// <inheritdoc />
        ICommand IDocumentEditor.CutCommand =>
            _docEditorCutCommand ?? (_docEditorCutCommand = new HexEditorRelayCommand(_ => ((IDocumentEditor)this).Cut(), _ => HasSelection && !ReadOnlyMode));

        /// <inheritdoc />
        ICommand IDocumentEditor.PasteCommand =>
            _docEditorPasteCommand ?? (_docEditorPasteCommand = new HexEditorRelayCommand(_ => ((IDocumentEditor)this).Paste(), _ => !ReadOnlyMode && _viewModel != null));

        /// <inheritdoc />
        ICommand IDocumentEditor.DeleteCommand =>
            _docEditorDeleteCommand ?? (_docEditorDeleteCommand = new HexEditorRelayCommand(_ => ((IDocumentEditor)this).Delete(), _ => HasSelection && !ReadOnlyMode));

        /// <inheritdoc />
        ICommand IDocumentEditor.SelectAllCommand =>
            _docEditorSelectAllCommand ?? (_docEditorSelectAllCommand = new HexEditorRelayCommand(_ => SelectAll(), _ => _viewModel != null));

        // ═══════════════════════════════════════════════════════════════════
        // IDocumentEditor — Methods
        // ═══════════════════════════════════════════════════════════════════

        // Undo(), Redo(), Save(), SelectAll(), Close() already exist as public methods.

        /// <inheritdoc />
        void IDocumentEditor.Copy() => Copy();

        /// <inheritdoc />
        void IDocumentEditor.Cut() => Cut();

        /// <inheritdoc />
        void IDocumentEditor.Paste() => Paste();

        /// <inheritdoc />
        void IDocumentEditor.Delete() => DeleteSelection();

        /// <inheritdoc />
        Task IDocumentEditor.SaveAsync(CancellationToken ct)
        {
            return Task.Run(() =>
            {
                ct.ThrowIfCancellationRequested();
                Dispatcher.Invoke(() => Save());
            }, ct);
        }

        /// <inheritdoc />
        Task IDocumentEditor.SaveAsAsync(string filePath, CancellationToken ct)
        {
            return Task.Run(() =>
            {
                ct.ThrowIfCancellationRequested();
                Dispatcher.Invoke(() =>
                {
                    if (_viewModel == null)
                        throw new InvalidOperationException("No file loaded");

                    _viewModel.SaveAs(filePath, overwrite: true);
                    FileName = filePath;
                    IsModified = false;
                    RaiseDocumentEditorTitleChanged();
                    _docEditorStatusMessage?.Invoke(this, $"Saved: {Path.GetFileName(filePath)}");
                });
            }, ct);
        }

        // ═══════════════════════════════════════════════════════════════════
        // IDocumentEditor — Events
        // ═══════════════════════════════════════════════════════════════════

        private EventHandler _docEditorModifiedChanged;
        private EventHandler _docEditorCanUndoChanged;
        private EventHandler _docEditorCanRedoChanged;
        private EventHandler<string> _docEditorTitleChanged;
        private EventHandler<string> _docEditorStatusMessage;
        private EventHandler _docEditorSelectionChanged;

        event EventHandler IDocumentEditor.ModifiedChanged
        {
            add => _docEditorModifiedChanged += value;
            remove => _docEditorModifiedChanged -= value;
        }

        event EventHandler IDocumentEditor.CanUndoChanged
        {
            add => _docEditorCanUndoChanged += value;
            remove => _docEditorCanUndoChanged -= value;
        }

        event EventHandler IDocumentEditor.CanRedoChanged
        {
            add => _docEditorCanRedoChanged += value;
            remove => _docEditorCanRedoChanged -= value;
        }

        event EventHandler<string> IDocumentEditor.TitleChanged
        {
            add => _docEditorTitleChanged += value;
            remove => _docEditorTitleChanged -= value;
        }

        event EventHandler<string> IDocumentEditor.StatusMessage
        {
            add => _docEditorStatusMessage += value;
            remove => _docEditorStatusMessage -= value;
        }

        event EventHandler IDocumentEditor.SelectionChanged
        {
            add => _docEditorSelectionChanged += value;
            remove => _docEditorSelectionChanged -= value;
        }

        // ═══════════════════════════════════════════════════════════════════
        // IDocumentEditor — Event wiring (called from existing event handlers)
        // ═══════════════════════════════════════════════════════════════════

        /// <summary>
        /// Tracks the previous IsModified state to detect transitions and fire
        /// ModifiedChanged / TitleChanged / CanUndoChanged / CanRedoChanged.
        /// </summary>
        private bool _previousIsModified;
        private bool _previousCanUndo;
        private bool _previousCanRedo;

        /// <summary>
        /// Called after any state change that might affect IDocumentEditor events.
        /// Wired from UpdateIsModifiedState() and OnSelectionChanged().
        /// </summary>
        private void RaiseDocumentEditorEvents()
        {
            // Modified changed?
            var currentModified = IsModified;
            if (currentModified != _previousIsModified)
            {
                _previousIsModified = currentModified;
                _docEditorModifiedChanged?.Invoke(this, EventArgs.Empty);
                RaiseDocumentEditorTitleChanged();
            }

            // CanUndo changed?
            var currentCanUndo = CanUndo;
            if (currentCanUndo != _previousCanUndo)
            {
                _previousCanUndo = currentCanUndo;
                _docEditorCanUndoChanged?.Invoke(this, EventArgs.Empty);
            }

            // CanRedo changed?
            var currentCanRedo = CanRedo;
            if (currentCanRedo != _previousCanRedo)
            {
                _previousCanRedo = currentCanRedo;
                _docEditorCanRedoChanged?.Invoke(this, EventArgs.Empty);
            }
        }

        /// <summary>
        /// Raises TitleChanged with the current computed title.
        /// </summary>
        private void RaiseDocumentEditorTitleChanged()
        {
            _docEditorTitleChanged?.Invoke(this, Title);
        }

        /// <summary>
        /// Raises the IDocumentEditor.SelectionChanged event.
        /// Called from OnSelectionChanged().
        /// </summary>
        private void RaiseDocumentEditorSelectionChanged()
        {
            _docEditorSelectionChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    /// <summary>
    /// Minimal ICommand implementation for HexEditor IDocumentEditor commands.
    /// </summary>
    internal sealed class HexEditorRelayCommand : ICommand
    {
        private readonly Action<object> _execute;
        private readonly Predicate<object> _canExecute;

        public HexEditorRelayCommand(Action<object> execute, Predicate<object> canExecute = null)
        {
            _execute = execute ?? throw new ArgumentNullException(nameof(execute));
            _canExecute = canExecute;
        }

        public bool CanExecute(object parameter) => _canExecute?.Invoke(parameter) ?? true;
        public void Execute(object parameter) => _execute(parameter);

        public event EventHandler CanExecuteChanged
        {
            add => CommandManager.RequerySuggested += value;
            remove => CommandManager.RequerySuggested -= value;
        }
    }
}
