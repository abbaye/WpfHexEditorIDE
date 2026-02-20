//////////////////////////////////////////////
// Apache 2.0  2026
// HexEditor V2 - Modern Main Window ViewModel
// Author : Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.5
//////////////////////////////////////////////

using System;
using System.ComponentModel;
using System.Linq;
using System.Windows;
using System.Windows.Input;
using Microsoft.Win32;
using WpfHexaEditor;

namespace WpfHexEditor.Sample.Main.ViewModels
{
    /// <summary>
    /// ViewModel for Modern Main Window
    /// </summary>
    public class ModernMainWindowViewModel : INotifyPropertyChanged
    {
        #region Fields

        private bool _isSettingsPanelVisible = false;
        private bool _isSearchPanelVisible = true;
        private bool _isFileLoaded = false;
        private bool _isOperationActive = false;
        private string _currentFilePath;
        private string _statusMessage = "Ready";
        private string _searchQuery = "";
        private HexEditor _hexEditor;

        #endregion

        #region Events

        public event PropertyChangedEventHandler PropertyChanged;
        public event EventHandler<string> FileOpenRequested;
        public event EventHandler FileSaveRequested;

        #endregion

        #region Properties

        public SettingsPanelViewModel SettingsViewModel { get; }
        public SearchCommandCenterViewModel SearchViewModel { get; }

        public bool IsSettingsPanelVisible
        {
            get => _isSettingsPanelVisible;
            set
            {
                _isSettingsPanelVisible = value;
                OnPropertyChanged(nameof(IsSettingsPanelVisible));
            }
        }

        public bool IsSearchPanelVisible
        {
            get => _isSearchPanelVisible;
            set
            {
                _isSearchPanelVisible = value;
                OnPropertyChanged(nameof(IsSearchPanelVisible));
            }
        }

        public bool IsFileLoaded
        {
            get => _isFileLoaded;
            set
            {
                _isFileLoaded = value;
                OnPropertyChanged(nameof(IsFileLoaded));
            }
        }

        /// <summary>
        /// Indicates whether a long-running async operation is currently active
        /// </summary>
        public bool IsOperationActive
        {
            get => _isOperationActive;
            set
            {
                _isOperationActive = value;
                OnPropertyChanged(nameof(IsOperationActive));

                // CRITICAL: Must invoke on UI thread for WPF CommandManager
                System.Windows.Application.Current?.Dispatcher.BeginInvoke(new Action(() =>
                {
                    // Force re-evaluation of all command CanExecute methods
                    System.Windows.Input.CommandManager.InvalidateRequerySuggested();
                }));
            }
        }

        public string CurrentFilePath
        {
            get => _currentFilePath;
            set
            {
                _currentFilePath = value;
                OnPropertyChanged(nameof(CurrentFilePath));
                OnPropertyChanged(nameof(WindowTitle));
            }
        }

        public string WindowTitle
        {
            get
            {
                var title = "WPF HexEditor V2 - Modern Sample 2026";
                if (!string.IsNullOrEmpty(CurrentFilePath))
                {
                    title += $" - {System.IO.Path.GetFileName(CurrentFilePath)}";
                }
                return title;
            }
        }

        public string StatusMessage
        {
            get => _statusMessage;
            set
            {
                _statusMessage = value;
                OnPropertyChanged(nameof(StatusMessage));
            }
        }

        public string SearchQuery
        {
            get => _searchQuery;
            set
            {
                _searchQuery = value;
                OnPropertyChanged(nameof(SearchQuery));
            }
        }

        #endregion

        #region Commands

        public ICommand OpenFileCommand { get; }
        public ICommand SaveFileCommand { get; }
        public ICommand SaveAsFileCommand { get; }
        public ICommand CloseFileCommand { get; }
        public ICommand ExitCommand { get; }
        public ICommand UndoCommand { get; }
        public ICommand RedoCommand { get; }
        public ICommand CopyCommand { get; }
        public ICommand CutCommand { get; }
        public ICommand PasteCommand { get; }
        public ICommand DeleteSelectionCommand { get; }
        public ICommand SelectAllCommand { get; }
        public ICommand ClearSelectionCommand { get; }
        public ICommand FindNextCommand { get; }
        public ICommand FindPreviousCommand { get; }
        public ICommand GoToPositionCommand { get; }
        public ICommand ToggleBookmarkCommand { get; }
        public ICommand ClearBookmarksCommand { get; }
        public ICommand NextBookmarkCommand { get; }
        public ICommand PreviousBookmarkCommand { get; }
        public ICommand ShowBookmarksCommand { get; }
        public ICommand SearchCommand { get; }
        public ICommand ToggleSettingsPanelCommand { get; }
        public ICommand ToggleSearchPanelCommand { get; }
        public ICommand ShowAboutCommand { get; }
        public ICommand ShowKeyboardShortcutsCommand { get; }
        public ICommand ReverseSelectionCommand { get; }
        public ICommand InvertSelectionCommand { get; }
        public ICommand FillWithByteCommand { get; }
        public ICommand ReplaceByteCommand { get; }

        #endregion

        #region Constructor

        public ModernMainWindowViewModel()
        {
            SettingsViewModel = new SettingsPanelViewModel();
            SearchViewModel = new SearchCommandCenterViewModel();

            // Wire up settings events
            SettingsViewModel.ThemeChanged += OnThemeChanged;
            SettingsViewModel.LanguageChanged += OnLanguageChanged;

            // Commands
            // File operations - block during async work
            OpenFileCommand = new RelayCommand(OpenFile, () => !IsOperationActive);
            SaveFileCommand = new RelayCommand(SaveFile, () => IsFileLoaded && !IsOperationActive);
            SaveAsFileCommand = new RelayCommand(SaveAsFile, () => IsFileLoaded && !IsOperationActive);
            CloseFileCommand = new RelayCommand(CloseFile, () => IsFileLoaded && !IsOperationActive);
            ExitCommand = new RelayCommand(Exit);

            // Edit operations - block modifications during async work
            UndoCommand = new RelayCommand(Undo, () => IsFileLoaded && !IsOperationActive);
            RedoCommand = new RelayCommand(Redo, () => IsFileLoaded && !IsOperationActive);
            CopyCommand = new RelayCommand(Copy, () => IsFileLoaded); // Read-only, always enabled
            CutCommand = new RelayCommand(Cut, () => IsFileLoaded && !IsOperationActive);
            PasteCommand = new RelayCommand(Paste, () => IsFileLoaded && !IsOperationActive);
            DeleteSelectionCommand = new RelayCommand(DeleteSelection, () => IsFileLoaded && !IsOperationActive);
            SelectAllCommand = new RelayCommand(SelectAll, () => IsFileLoaded); // Read-only, always enabled
            ClearSelectionCommand = new RelayCommand(ClearSelection, () => IsFileLoaded); // Read-only, always enabled

            // Search/navigation - block during async work
            FindNextCommand = new RelayCommand(FindNext, () => IsFileLoaded && !IsOperationActive);
            FindPreviousCommand = new RelayCommand(FindPrevious, () => IsFileLoaded && !IsOperationActive);
            GoToPositionCommand = new RelayCommand(GoToPosition, () => IsFileLoaded && !IsOperationActive);

            // Bookmarks - allow during async (metadata only)
            ToggleBookmarkCommand = new RelayCommand(ToggleBookmark, () => IsFileLoaded);
            ClearBookmarksCommand = new RelayCommand(ClearBookmarks, () => IsFileLoaded);
            NextBookmarkCommand = new RelayCommand(NextBookmark, () => IsFileLoaded);
            PreviousBookmarkCommand = new RelayCommand(PreviousBookmark, () => IsFileLoaded);
            ShowBookmarksCommand = new RelayCommand(ShowBookmarks, () => IsFileLoaded);

            // Search - block during async work
            SearchCommand = new RelayCommand(Search, () => IsFileLoaded && !IsOperationActive && !string.IsNullOrWhiteSpace(SearchQuery));
            ToggleSettingsPanelCommand = new RelayCommand(ToggleSettingsPanel);
            ToggleSearchPanelCommand = new RelayCommand(ToggleSearchPanel);
            ShowAboutCommand = new RelayCommand(ShowAbout);
            ShowKeyboardShortcutsCommand = new RelayCommand(ShowKeyboardShortcuts);

            // Byte operations
            ReverseSelectionCommand = new RelayCommand(ReverseSelection, () => IsFileLoaded && _hexEditor?.HasSelection == true && !IsOperationActive);
            InvertSelectionCommand = new RelayCommand(InvertSelection, () => IsFileLoaded && _hexEditor?.HasSelection == true && !IsOperationActive);
            FillWithByteCommand = new RelayCommand(FillWithByte, () => IsFileLoaded && _hexEditor?.HasSelection == true && !IsOperationActive);
            ReplaceByteCommand = new RelayCommand(ReplaceByte, () => IsFileLoaded && !IsOperationActive);
        }

        #endregion

        #region Methods

        public void SetHexEditor(HexEditor hexEditor)
        {
            _hexEditor = hexEditor;
            if (_hexEditor != null)
            {
                _hexEditor.FileOpened += OnFileLoaded;
                _hexEditor.FileClosed += OnFileClosed;
            }
        }

        private void OpenFile()
        {
            var dialog = new OpenFileDialog
            {
                Title = "Open File",
                Filter = "All Files (*.*)|*.*",
                CheckFileExists = true
            };

            if (dialog.ShowDialog() == true)
            {
                FileOpenRequested?.Invoke(this, dialog.FileName);
                CurrentFilePath = dialog.FileName;
                StatusMessage = $"Loaded: {System.IO.Path.GetFileName(dialog.FileName)}";
            }
        }

        private void SaveFile()
        {
            FileSaveRequested?.Invoke(this, EventArgs.Empty);
            StatusMessage = "File saved";
        }

        private void SaveAsFile()
        {
            var dialog = new SaveFileDialog
            {
                Title = "Save File As",
                Filter = "All Files (*.*)|*.*"
            };

            if (dialog.ShowDialog() == true)
            {
                try
                {
                    _hexEditor?.SubmitChanges(dialog.FileName, true);
                    CurrentFilePath = dialog.FileName;
                    StatusMessage = $"Saved as: {System.IO.Path.GetFileName(dialog.FileName)}";
                }
                catch (Exception ex)
                {
                    StatusMessage = $"Save failed: {ex.Message}";
                }
            }
        }

        private void CloseFile()
        {
            _hexEditor?.Close();
            CurrentFilePath = null;
            IsFileLoaded = false;
            StatusMessage = "File closed";
        }

        private void Exit()
        {
            Application.Current.Shutdown();
        }

        private void Undo()
        {
            try
            {
                _hexEditor?.Undo();
                StatusMessage = "Undo completed";
            }
            catch (Exception ex)
            {
                StatusMessage = $"Undo failed: {ex.Message}";
            }
        }

        private void Redo()
        {
            try
            {
                _hexEditor?.Redo();
                StatusMessage = "Redo completed";
            }
            catch (Exception ex)
            {
                StatusMessage = $"Redo failed: {ex.Message}";
            }
        }

        private void Copy()
        {
            System.Diagnostics.Debug.WriteLine("=== Copy() called ===");
            try
            {
                System.Diagnostics.Debug.WriteLine($"_hexEditor is null: {_hexEditor == null}");
                if (_hexEditor != null)
                {
                    System.Diagnostics.Debug.WriteLine($"HasSelection: {_hexEditor.HasSelection}");
                    System.Diagnostics.Debug.WriteLine($"SelectionStart: {_hexEditor.SelectionStart}");
                    System.Diagnostics.Debug.WriteLine($"SelectionLength: {_hexEditor.SelectionLength}");
                }

                if (_hexEditor == null || !_hexEditor.HasSelection)
                {
                    StatusMessage = "No selection to copy";
                    System.Diagnostics.Debug.WriteLine("No selection to copy - returning");
                    return;
                }

                System.Diagnostics.Debug.WriteLine("Calling _hexEditor.Copy()...");
                bool copyResult = _hexEditor.Copy();
                System.Diagnostics.Debug.WriteLine($"Copy() returned: {copyResult}");

                if (copyResult)
                {
                    StatusMessage = "Copied to clipboard";
                }
                else
                {
                    StatusMessage = "Copy failed";
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Exception in Copy(): {ex}");
                StatusMessage = $"Copy failed: {ex.Message}";
            }
            System.Diagnostics.Debug.WriteLine("=== Copy() finished ===");
        }

        private void Cut()
        {
            System.Diagnostics.Debug.WriteLine("=== Cut() called ===");
            try
            {
                System.Diagnostics.Debug.WriteLine($"_hexEditor is null: {_hexEditor == null}");
                if (_hexEditor != null)
                {
                    System.Diagnostics.Debug.WriteLine($"HasSelection: {_hexEditor.HasSelection}");
                }

                if (_hexEditor == null || !_hexEditor.HasSelection)
                {
                    StatusMessage = "No selection to cut";
                    System.Diagnostics.Debug.WriteLine("No selection to cut - returning");
                    return;
                }

                // Save the start position before any operation
                var startPos = _hexEditor.SelectionStart;
                System.Diagnostics.Debug.WriteLine($"Start position: {startPos}");

                // Copy first (like old MainWindow does)
                System.Diagnostics.Debug.WriteLine("Calling _hexEditor.Copy()...");
                bool copyResult = _hexEditor.Copy();
                System.Diagnostics.Debug.WriteLine($"Copy() returned: {copyResult}");

                if (copyResult)
                {
                    System.Diagnostics.Debug.WriteLine("Calling DeleteSelection()...");
                    _hexEditor.DeleteSelection();

                    System.Diagnostics.Debug.WriteLine($"Calling SetPosition({startPos})...");
                    _hexEditor.SetPosition(startPos);

                    StatusMessage = "Cut to clipboard";
                }
                else
                {
                    StatusMessage = "Cut failed - copy to clipboard failed";
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Exception in Cut(): {ex}");
                StatusMessage = $"Cut failed: {ex.Message}";
            }
            System.Diagnostics.Debug.WriteLine("=== Cut() finished ===");
        }

        private void Paste()
        {
            try
            {
                _hexEditor?.Paste();
                StatusMessage = "Pasted from clipboard";
            }
            catch (Exception ex)
            {
                StatusMessage = $"Paste failed: {ex.Message}";
            }
        }

        private void DeleteSelection()
        {
            try
            {
                _hexEditor?.DeleteSelection();
                StatusMessage = "Selection deleted";
            }
            catch (Exception ex)
            {
                StatusMessage = $"Delete failed: {ex.Message}";
            }
        }

        private void SelectAll()
        {
            try
            {
                _hexEditor?.SelectAll();
                StatusMessage = "All selected";
            }
            catch (Exception ex)
            {
                StatusMessage = $"Select all failed: {ex.Message}";
            }
        }

        private void ClearSelection()
        {
            try
            {
                _hexEditor?.ClearSelection();
                StatusMessage = "Selection cleared";
            }
            catch (Exception ex)
            {
                StatusMessage = $"Clear selection failed: {ex.Message}";
            }
        }

        private void ReverseSelection()
        {
            try
            {
                _hexEditor?.ReverseSelection();
                var length = _hexEditor?.SelectionLength ?? 0;
                StatusMessage = $"Reversed {length} bytes";
            }
            catch (Exception ex)
            {
                StatusMessage = $"Reverse selection failed: {ex.Message}";
            }
        }

        private void InvertSelection()
        {
            try
            {
                _hexEditor?.InvertSelection();
                var length = _hexEditor?.SelectionLength ?? 0;
                StatusMessage = $"Inverted {length} bytes";
            }
            catch (Exception ex)
            {
                StatusMessage = $"Invert selection failed: {ex.Message}";
            }
        }

        private void FillWithByte()
        {
            try
            {
                if (_hexEditor == null || !_hexEditor.HasSelection) return;

                // Use the modern MVVM GiveByteWindow dialog
                var dialog = new WpfHexaEditor.Dialog.GiveByteWindow
                {
                    Owner = System.Windows.Application.Current.MainWindow
                };

                if (dialog.ShowDialog() == true)
                {
                    byte fillByte = dialog.ByteValue;
                    long fillStart = _hexEditor.SelectionStart;
                    long fillLength = _hexEditor.SelectionLength;

                    _hexEditor.FillWithByte(fillByte, fillStart, fillLength);
                    StatusMessage = $"Filled {fillLength} bytes with 0x{fillByte:X2}";
                }
            }
            catch (Exception ex)
            {
                StatusMessage = $"Fill with byte failed: {ex.Message}";
            }
        }

        private void ReplaceByte()
        {
            try
            {
                if (_hexEditor == null) return;

                // Use the modern MVVM ReplaceByteWindow dialog
                var dialog = new WpfHexaEditor.Dialog.ReplaceByteWindow
                {
                    Owner = System.Windows.Application.Current.MainWindow
                };

                // Pre-fill options if a selection exists
                if (_hexEditor.HasSelection)
                {
                    dialog.ViewModel.ReplaceInSelectionOnly = true;

                    if (_hexEditor.SelectionLength == 1)
                    {
                        try
                        {
                            var selectedByte = _hexEditor.GetByte(_hexEditor.SelectionStart);
                            dialog.ViewModel.FindByte = selectedByte;
                        }
                        catch { /* Ignore errors */ }
                    }
                }

                if (dialog.ShowDialog() == true)
                {
                    byte findByte = dialog.FindByte;
                    byte replaceByte = dialog.ReplaceByte;
                    byte[] findData = new byte[] { findByte };
                    byte[] replaceData = new byte[] { replaceByte };
                    bool inSelectionOnly = dialog.ReplaceInSelectionOnly;
                    int replacedCount = 0;

                    if (inSelectionOnly && _hexEditor.HasSelection)
                    {
                        // Replace only within selection using FindFirst approach
                        long selStart = _hexEditor.SelectionStart;
                        long selLength = _hexEditor.SelectionLength;
                        long selEnd = selStart + selLength;
                        long searchPos = selStart;

                        while (searchPos < selEnd)
                        {
                            long foundPos = _hexEditor.FindFirst(findData, searchPos);
                            if (foundPos >= 0 && foundPos < selEnd)
                            {
                                _hexEditor.SetByte(foundPos, replaceByte);
                                replacedCount++;
                                searchPos = foundPos + 1;
                            }
                            else
                            {
                                break;
                            }
                        }
                    }
                    else
                    {
                        // Replace in entire file
                        var replaced = _hexEditor.ReplaceAll(findData, replaceData, false, false);
                        replacedCount = replaced.Count();
                    }

                    _hexEditor.ClearSelection();

                    string scope = inSelectionOnly ? "in selection" : "in file";
                    StatusMessage = $"Replaced {replacedCount} occurrences (0x{findByte:X2} → 0x{replaceByte:X2}) {scope}";
                }
            }
            catch (Exception ex)
            {
                StatusMessage = $"Replace byte failed: {ex.Message}";
            }
        }

        private void FindNext()
        {
            try
            {
                if (SearchViewModel != null && SearchViewModel.FindNextCommand.CanExecute(null))
                {
                    SearchViewModel.FindNextCommand.Execute(null);
                    StatusMessage = "Finding next match";
                }
            }
            catch (Exception ex)
            {
                StatusMessage = $"Find next failed: {ex.Message}";
            }
        }

        private void FindPrevious()
        {
            try
            {
                if (SearchViewModel != null && SearchViewModel.FindPreviousCommand.CanExecute(null))
                {
                    SearchViewModel.FindPreviousCommand.Execute(null);
                    StatusMessage = "Finding previous match";
                }
            }
            catch (Exception ex)
            {
                StatusMessage = $"Find previous failed: {ex.Message}";
            }
        }

        private void GoToPosition()
        {
            // TODO: Implement Go To Position dialog
            StatusMessage = "Go to position - Not implemented yet";
        }

        private void ToggleBookmark()
        {
            try
            {
                if (_hexEditor == null) return;

                var currentPos = _hexEditor.SelectionStart;
                if (_hexEditor.IsBookmarked(currentPos))
                {
                    _hexEditor.RemoveBookmark(currentPos);
                    StatusMessage = "Bookmark removed";
                }
                else
                {
                    _hexEditor.SetBookmark(currentPos);
                    StatusMessage = "Bookmark added";
                }
            }
            catch (Exception ex)
            {
                StatusMessage = $"Toggle bookmark failed: {ex.Message}";
            }
        }

        private void ClearBookmarks()
        {
            try
            {
                _hexEditor?.ClearAllBookmarks();
                StatusMessage = "All bookmarks cleared";
            }
            catch (Exception ex)
            {
                StatusMessage = $"Clear bookmarks failed: {ex.Message}";
            }
        }

        private void NextBookmark()
        {
            try
            {
                if (_hexEditor == null) return;

                var currentPos = _hexEditor.SelectionStart;
                var nextPos = _hexEditor.GetNextBookmark(currentPos);
                if (nextPos >= 0)
                {
                    _hexEditor.SetPosition(nextPos);
                    StatusMessage = $"Jumped to bookmark at {nextPos:X}";
                }
                else
                {
                    StatusMessage = "No more bookmarks ahead";
                }
            }
            catch (Exception ex)
            {
                StatusMessage = $"Next bookmark failed: {ex.Message}";
            }
        }

        private void PreviousBookmark()
        {
            try
            {
                if (_hexEditor == null) return;

                var currentPos = _hexEditor.SelectionStart;
                var prevPos = _hexEditor.GetPreviousBookmark(currentPos);
                if (prevPos >= 0)
                {
                    _hexEditor.SetPosition(prevPos);
                    StatusMessage = $"Jumped to bookmark at {prevPos:X}";
                }
                else
                {
                    StatusMessage = "No more bookmarks before";
                }
            }
            catch (Exception ex)
            {
                StatusMessage = $"Previous bookmark failed: {ex.Message}";
            }
        }

        private void ShowBookmarks()
        {
            // TODO: Implement Show All Bookmarks dialog
            StatusMessage = "Show bookmarks - Not implemented yet";
        }

        private void ShowKeyboardShortcuts()
        {
            MessageBox.Show(
                "Keyboard Shortcuts:\n\n" +
                "File Operations:\n" +
                "  Ctrl+O - Open File\n" +
                "  Ctrl+S - Save File\n" +
                "  Ctrl+W - Close File\n\n" +
                "Edit Operations:\n" +
                "  Ctrl+Z - Undo\n" +
                "  Ctrl+Y - Redo\n" +
                "  Ctrl+C - Copy\n" +
                "  Ctrl+V - Paste\n" +
                "  Del - Delete Selection\n" +
                "  Ctrl+A - Select All\n\n" +
                "Search:\n" +
                "  Ctrl+F - Find\n" +
                "  Ctrl+H - Find & Replace\n" +
                "  F3 - Find Next\n" +
                "  Shift+F3 - Find Previous\n\n" +
                "Navigation:\n" +
                "  Ctrl+G - Go To Position\n" +
                "  Ctrl+B - Toggle Bookmark\n" +
                "  Ctrl+N - Next Bookmark\n" +
                "  Ctrl+P - Previous Bookmark\n\n" +
                "View:\n" +
                "  Ctrl+, - Toggle Settings Panel",
                "Keyboard Shortcuts",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
        }

        private void Search()
        {
            if (_hexEditor == null || string.IsNullOrWhiteSpace(SearchQuery))
                return;

            try
            {
                // Use the SearchViewModel functionality if available
                if (SearchViewModel != null && SearchViewModel.SearchCommand.CanExecute(null))
                {
                    SearchViewModel.SearchPattern = SearchQuery;
                    SearchViewModel.SearchCommand.Execute(null);
                    StatusMessage = $"Searching for: {SearchQuery}";
                }
            }
            catch (Exception ex)
            {
                StatusMessage = $"Search failed: {ex.Message}";
            }
        }

        private void ToggleSettingsPanel()
        {
            IsSettingsPanelVisible = !IsSettingsPanelVisible;
        }

        private void ToggleSearchPanel()
        {
            IsSearchPanelVisible = !IsSearchPanelVisible;
        }

        private void ShowAbout()
        {
            MessageBox.Show(
                "WPF HexEditor V2 - Modern Sample 2026\n\n" +
                "Ultra-performant hex editor with modern UI\n" +
                "• 99% faster search with V2 engine\n" +
                "• 4 modern themes\n" +
                "• Multi-language support\n" +
                "• Search-first architecture\n\n" +
                "Apache 2.0 License © 2026\n" +
                "Author: Derek Tremblay\n" +
                "Contributors: Claude Sonnet 4.5",
                "About",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
        }

        private void OnThemeChanged(object sender, string themeName)
        {
            StatusMessage = $"Theme changed to: {themeName}";
        }

        private void OnLanguageChanged(object sender, string languageCode)
        {
            StatusMessage = $"Language changed to: {languageCode}";
        }

        private void OnFileLoaded(object sender, EventArgs e)
        {
            IsFileLoaded = true;

            // Connect search to hex editor
            if (_hexEditor != null)
            {
                SearchViewModel.ByteProvider = _hexEditor.GetByteProvider();
                SearchViewModel.OnMatchFound += (s, match) =>
                {
                    _hexEditor.FindSelect(match.Position, match.Length);
                };
            }
        }

        private void OnFileClosed(object sender, EventArgs e)
        {
            IsFileLoaded = false;
            SearchViewModel.ByteProvider = null;
        }

        /// <summary>
        /// Called when HexEditor operation state changes (async operation starts/completes)
        /// </summary>
        public void OnOperationStateChanged(bool isActive)
        {
            IsOperationActive = isActive;
        }

        protected void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        #endregion
    }
}
