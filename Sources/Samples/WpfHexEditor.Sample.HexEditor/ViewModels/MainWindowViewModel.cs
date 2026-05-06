// WpfHexEditor.Sample.HexEditor — MainWindowViewModel.cs

using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using Microsoft.Win32;
using HexEditorControl = WpfHexEditor.HexEditor.HexEditor;

namespace WpfHexEditor.Sample.HexEditor.ViewModels
{
    public class MainWindowViewModel : INotifyPropertyChanged
    {
        private HexEditorControl? _hexEditor;
        private bool _isFileLoaded;
        private string _statusText = "Ready";
        private bool _showParsedFieldsPanel = true;
        private bool _showSettingsPanel = true;

        public event EventHandler<string>? OpenFileRequested;
        public event EventHandler? SaveFileRequested;
        public event PropertyChangedEventHandler? PropertyChanged;

        public bool IsFileLoaded
        {
            get => _isFileLoaded;
            set { _isFileLoaded = value; OnPropertyChanged(); RaiseCanExecuteChanged(); }
        }

        public string StatusText
        {
            get => _statusText;
            set { _statusText = value; OnPropertyChanged(); }
        }

        public bool ShowParsedFieldsPanel
        {
            get => _showParsedFieldsPanel;
            set { _showParsedFieldsPanel = value; OnPropertyChanged(); }
        }

        public bool ShowSettingsPanel
        {
            get => _showSettingsPanel;
            set { _showSettingsPanel = value; OnPropertyChanged(); }
        }

        public ICommand OpenCommand       { get; }
        public ICommand SaveCommand       { get; }
        public ICommand CloseFileCommand  { get; }
        public ICommand UndoCommand       { get; }
        public ICommand RedoCommand       { get; }
        public ICommand ExitCommand       { get; }

        public MainWindowViewModel()
        {
            OpenCommand       = new RelayCommand(RequestOpen);
            SaveCommand       = new RelayCommand(RequestSave,   () => IsFileLoaded);
            CloseFileCommand  = new RelayCommand(CloseFile,     () => IsFileLoaded);
            UndoCommand       = new RelayCommand(Undo,          () => _hexEditor?.CanUndo == true);
            RedoCommand       = new RelayCommand(Redo,          () => _hexEditor?.CanRedo == true);
            ExitCommand       = new RelayCommand(() => System.Windows.Application.Current.Shutdown());
        }

        public void SetHexEditor(HexEditorControl hexEditor)
        {
            _hexEditor = hexEditor;
            _hexEditor.FileOpened += (_, _) => { IsFileLoaded = true; };
            _hexEditor.FileClosed += (_, _) => { IsFileLoaded = false; };
        }

        private void RequestOpen()
        {
            var dlg = new OpenFileDialog
            {
                Title = "Open File",
                Filter = "All Files (*.*)|*.*"
            };
            if (dlg.ShowDialog() == true)
                OpenFileRequested?.Invoke(this, dlg.FileName);
        }

        private void RequestSave()  => SaveFileRequested?.Invoke(this, EventArgs.Empty);
        private void CloseFile()    => _hexEditor?.Close();
        private void Undo()         => _hexEditor?.Undo();
        private void Redo()         => _hexEditor?.Redo();

        private void RaiseCanExecuteChanged()
        {
            (SaveCommand      as RelayCommand)?.RaiseCanExecuteChanged();
            (CloseFileCommand as RelayCommand)?.RaiseCanExecuteChanged();
            (UndoCommand      as RelayCommand)?.RaiseCanExecuteChanged();
            (RedoCommand      as RelayCommand)?.RaiseCanExecuteChanged();
        }

        protected void OnPropertyChanged([CallerMemberName] string? name = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }

    public class RelayCommand : ICommand
    {
        private readonly Action _execute;
        private readonly Func<bool>? _canExecute;

        public event EventHandler? CanExecuteChanged;

        public RelayCommand(Action execute, Func<bool>? canExecute = null)
        {
            _execute    = execute;
            _canExecute = canExecute;
        }

        public bool CanExecute(object? parameter) => _canExecute?.Invoke() ?? true;
        public void Execute(object? parameter)    => _execute();
        public void RaiseCanExecuteChanged()      => CanExecuteChanged?.Invoke(this, EventArgs.Empty);
    }
}
