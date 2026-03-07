// ==========================================================
// Project: WpfHexEditor.HexEditor
// File: HexEditor.DocumentEditor.cs
// Author: Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude (Anthropic)
// Created: 2026-03-06
// Description:
//     Partial class implementing IDocumentEditor for the HexEditor control.
//     Integrates the HexEditor into the IDE document editor framework, exposing
//     document lifecycle (open, close, save, dirty state) and async operations.
//
// Architecture Notes:
//     Implements IDocumentEditor from WpfHexEditor.Editor.Core.
//     Bridges the WPF UserControl lifecycle to the IDE document model.
//
// ==========================================================

using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Input;
using WpfHexEditor.Core.Events;
using WpfHexEditor.Editor.Core;

namespace WpfHexEditor.HexEditor
{
    /// <summary>
    /// HexEditor partial class — IDocumentEditor implementation.
    /// Bridges the existing HexEditor API to the unified editor contract
    /// used by the docking host and other multi-editor scenarios.
    /// </summary>
    public partial class HexEditor : IOpenableDocument
    {
        // ═══════════════════════════════════════════════════════════════════
        // IOpenableDocument
        // ═══════════════════════════════════════════════════════════════════

        Task IOpenableDocument.OpenAsync(string filePath, CancellationToken ct)
        {
            ct.ThrowIfCancellationRequested();
            Dispatcher.Invoke(() => OpenFile(filePath));
            return Task.CompletedTask;
        }

        // ═══════════════════════════════════════════════════════════════════
        // IDocumentEditor — New-file state (Phase 12)
        // ═══════════════════════════════════════════════════════════════════

        /// <summary>
        /// True after <see cref="OpenNew"/> is called; cleared once saved to disk.
        /// The editor is backed by a memory buffer (no file path yet).
        /// </summary>
        internal bool   _isNewUnsavedFile;
        internal string _newFileDisplayName = "";

        /// <summary>
        /// Undo-count baseline set by <see cref="IEditorPersistable.MarkChangesetSaved"/>.
        /// -1 = no tracked save yet (standard IsModified = UndoCount > 0 behaviour).
        /// </summary>
        private long _changesetSavedUndoCount = -1;

        // ═══════════════════════════════════════════════════════════════════
        // IDocumentEditor — State
        // ═══════════════════════════════════════════════════════════════════

        /// <inheritdoc />
        bool IDocumentEditor.IsDirty => IsModified || _isNewUnsavedFile;

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
                var name = _isNewUnsavedFile && _newFileDisplayName.Length > 0
                    ? _newFileDisplayName
                    : (string.IsNullOrEmpty(FileName) ? "Untitled" : Path.GetFileName(FileName));
                return (IsModified || _isNewUnsavedFile) ? $"{name} *" : name;
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
            _docEditorUndoCommand ?? (_docEditorUndoCommand = new HexEditorRelayCommand(_ => Undo(), _ => CanUndo && !IsOperationActive));

        /// <inheritdoc />
        ICommand IDocumentEditor.RedoCommand =>
            _docEditorRedoCommand ?? (_docEditorRedoCommand = new HexEditorRelayCommand(_ => Redo(), _ => CanRedo && !IsOperationActive));

        /// <inheritdoc />
        ICommand IDocumentEditor.SaveCommand =>
            _docEditorSaveCommand ?? (_docEditorSaveCommand = new HexEditorRelayCommand(
                _ => SaveOrSaveAs(),
                _ => (IsModified || _isNewUnsavedFile) && _viewModel != null && !IsOperationActive));

        /// <inheritdoc />
        ICommand IDocumentEditor.CopyCommand =>
            _docEditorCopyCommand ?? (_docEditorCopyCommand = new HexEditorRelayCommand(_ => ((IDocumentEditor)this).Copy(), _ => HasSelection && !IsOperationActive));

        /// <inheritdoc />
        ICommand IDocumentEditor.CutCommand =>
            _docEditorCutCommand ?? (_docEditorCutCommand = new HexEditorRelayCommand(_ => ((IDocumentEditor)this).Cut(), _ => HasSelection && !ReadOnlyMode && !IsOperationActive));

        /// <inheritdoc />
        ICommand IDocumentEditor.PasteCommand =>
            _docEditorPasteCommand ?? (_docEditorPasteCommand = new HexEditorRelayCommand(_ => ((IDocumentEditor)this).Paste(), _ => !ReadOnlyMode && _viewModel != null && !IsOperationActive));

        /// <inheritdoc />
        ICommand IDocumentEditor.DeleteCommand =>
            _docEditorDeleteCommand ?? (_docEditorDeleteCommand = new HexEditorRelayCommand(_ => ((IDocumentEditor)this).Delete(), _ => HasSelection && !ReadOnlyMode && !IsOperationActive));

        /// <inheritdoc />
        ICommand IDocumentEditor.SelectAllCommand =>
            _docEditorSelectAllCommand ?? (_docEditorSelectAllCommand = new HexEditorRelayCommand(_ => SelectAll(), _ => _viewModel != null && !IsOperationActive));

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
                Dispatcher.Invoke(SaveOrSaveAs);
            }, ct);
        }

        /// <summary>
        /// Routes to <see cref="SaveAsNewFile"/> for new in-memory documents,
        /// or to <see cref="Save"/> for normal file-backed documents.
        /// </summary>
        private void SaveOrSaveAs()
        {
            if (_isNewUnsavedFile)
                SaveAsNewFile();
            else
                Save();
        }

        /// <summary>
        /// Shows a SaveFileDialog, then writes the in-memory buffer to disk and
        /// transitions the document from "new unsaved" to a normal file-backed document.
        /// </summary>
        private void SaveAsNewFile()
        {
            var ext    = Path.GetExtension(_newFileDisplayName);
            var filter = string.IsNullOrEmpty(ext)
                ? "All Files (*.*)|*.*"
                : $"{ext.TrimStart('.').ToUpperInvariant()} Files (*{ext})|*{ext}|All Files (*.*)|*.*";

            var dlg = new Microsoft.Win32.SaveFileDialog
            {
                FileName   = _newFileDisplayName,
                DefaultExt = ext,
                Filter     = filter
            };

            if (dlg.ShowDialog() != true) return;
            if (_viewModel == null) return;

            _viewModel.SaveAs(dlg.FileName, overwrite: true);
            FileName            = dlg.FileName;
            _isNewUnsavedFile   = false;
            _newFileDisplayName = "";
            IsModified          = false;
            RaiseDocumentEditorTitleChanged();
            RaiseHexStatusChanged();
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

        private EventHandler<DocumentOperationEventArgs>?          _docEditorOperationStarted;
        private EventHandler<DocumentOperationEventArgs>?          _docEditorOperationProgress;
        private EventHandler<DocumentOperationCompletedEventArgs>? _docEditorOperationCompleted;

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

        // HexEditor does not produce verbose output messages — no-op stub required by IDocumentEditor
        event EventHandler<string>? IDocumentEditor.OutputMessage { add { } remove { } }

        event EventHandler IDocumentEditor.SelectionChanged
        {
            add => _docEditorSelectionChanged += value;
            remove => _docEditorSelectionChanged -= value;
        }

        // -- IDocumentEditor — Long-running operation ----------------------

        /// <inheritdoc />
        bool IDocumentEditor.IsBusy => IsOperationActive;

        /// <inheritdoc />
        void IDocumentEditor.CancelOperation() => _longRunningService.CancelCurrentOperation();

        event EventHandler<DocumentOperationEventArgs> IDocumentEditor.OperationStarted
        {
            add    => _docEditorOperationStarted += value;
            remove => _docEditorOperationStarted -= value;
        }

        event EventHandler<DocumentOperationEventArgs> IDocumentEditor.OperationProgress
        {
            add    => _docEditorOperationProgress += value;
            remove => _docEditorOperationProgress -= value;
        }

        event EventHandler<DocumentOperationCompletedEventArgs> IDocumentEditor.OperationCompleted
        {
            add    => _docEditorOperationCompleted += value;
            remove => _docEditorOperationCompleted -= value;
        }

        // ═══════════════════════════════════════════════════════════════════
        // IDocumentEditor — Long-running operation relay helpers
        // Called from LongRunningService_Operation* handlers in HexEditor.xaml.cs
        // ═══════════════════════════════════════════════════════════════════

        internal void RaiseDocEditorOperationStarted(OperationProgressEventArgs e)
            => _docEditorOperationStarted?.Invoke(this, new DocumentOperationEventArgs
            {
                Title           = e.OperationTitle  ?? "",
                Message         = e.StatusMessage   ?? "",
                Percentage      = e.ProgressPercentage,
                IsIndeterminate = e.IsIndeterminate,
                CanCancel       = e.CanCancel
            });

        internal void RaiseDocEditorOperationProgress(OperationProgressEventArgs e)
            => _docEditorOperationProgress?.Invoke(this, new DocumentOperationEventArgs
            {
                Title           = e.OperationTitle ?? "",
                Message         = e.StatusMessage  ?? "",
                Percentage      = e.ProgressPercentage,
                IsIndeterminate = e.IsIndeterminate,
                CanCancel       = e.CanCancel
            });

        internal void RaiseDocEditorOperationCompleted(OperationCompletedEventArgs e)
            => _docEditorOperationCompleted?.Invoke(this, new DocumentOperationCompletedEventArgs
            {
                Success      = e.Success,
                WasCancelled = e.WasCancelled,
                ErrorMessage = e.ErrorMessage ?? ""
            });

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

        /// <summary>
        /// Fires IDocumentEditor.StatusMessage with the current aggregated status bar content.
        /// Called after key status bar updates so that host applications (e.g. WpfHexEditor.App)
        /// can mirror the active editor's status without displaying HexEditor's own status bar.
        /// </summary>
        internal void RaiseHexStatusChanged()
        {
            var parts = new System.Collections.Generic.List<string>(6);
            if (StatusText?.Text      is { Length: > 0 } st)  parts.Add(st);
            if (FileSizeText?.Text    is { Length: > 0 } fs)  parts.Add(fs);
            if (EditModeText?.Text    is { Length: > 0 } em)  parts.Add(em);
            if (BytesPerLineText?.Text is { Length: > 0 } bpl) parts.Add(bpl);
            if (RefreshTimeText?.Text  is { Length: > 0 } rt)  parts.Add(rt);
            if (parts.Count > 0)
                _docEditorStatusMessage?.Invoke(this, string.Join("  |  ", parts));

            // Sync interactive status bar items (IStatusBarContributor)
            RefreshStatusBarItemValues();
        }

        /// <summary>
        /// Called by the host (e.g. docking window) when this editor becomes the active document,
        /// so that the host's status bar is refreshed immediately on tab switch.
        /// </summary>
        public void RefreshDocumentStatus() => RaiseHexStatusChanged();
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
