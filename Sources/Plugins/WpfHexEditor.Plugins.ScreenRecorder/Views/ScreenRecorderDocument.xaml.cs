// ==========================================================
// Project: WpfHexEditor.Plugins.ScreenRecorder
// File: Views/ScreenRecorderDocument.xaml.cs
// Description: Code-behind for the Screen Recorder document tab.
//              Implements IEditorToolbarContributor so the IDE toolbar shows
//              recorder controls when this tab is active.
// Architecture Notes:
//     IEditorToolbarContributor swap is driven by MainWindow.OnActiveDocumentChanged.
//     ViewModel is injected after construction via SetViewModel().
//     Undo/Redo are wired via ApplicationCommands so the IDE Edit menu + Ctrl+Z/Y work.
//     Ctrl+C/V are wired via ApplicationCommands for clipboard image paste.
// ==========================================================

using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Threading;
using System.Windows;
using System.Windows.Input;
using WpfHexEditor.Editor.Core;
using WpfHexEditor.Plugins.ScreenRecorder.Services;
using WpfHexEditor.Plugins.ScreenRecorder.ViewModels;
using WpfHexEditor.SDK.Commands;

namespace WpfHexEditor.Plugins.ScreenRecorder.Views;

public partial class ScreenRecorderDocument : System.Windows.Controls.UserControl,
                                               IEditorToolbarContributor,
                                               IDocumentEditor
{
    // Segoe MDL2 Assets glyph codes used in the contextual toolbar pod.
    private const string IcoRecord = "\uE9D9";
    private const string IcoStop   = "\uE71C";
    private const string IcoPause  = "\uE769";
    private const string IcoPlay   = "\uE768";
    private const string IcoCamera = "\uE722";
    private const string IcoCrop   = "\uE7A8";
    private const string IcoImport = "\uE8B7";
    private const string IcoSave   = "\uE74E";

    public ObservableCollection<EditorToolbarItem> ToolbarItems { get; } = [];

    private ScreenRecorderViewModel? _vm;
    private bool _isDirty;

    // \u2500\u2500 IDocumentEditor \u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500

    public bool   IsDirty    => _isDirty;
    public bool   CanUndo    => _vm?.Timeline.CanUndo == true;
    public bool   CanRedo    => _vm?.Timeline.CanRedo == true;
    public bool   IsReadOnly { get => false; set { } }
    public string Title      => _isDirty
        ? $"{Properties.ScreenRecorderResources.ScreenRecorder_DocumentTitle} *"
        : Properties.ScreenRecorderResources.ScreenRecorder_DocumentTitle;

    public ICommand? UndoCommand      => ApplicationCommands.Undo;
    public ICommand? RedoCommand      => ApplicationCommands.Redo;
    public ICommand? SaveCommand      => _vm?.SaveSessionCommand;
    public ICommand? CopyCommand      => ApplicationCommands.Copy;
    public ICommand? CutCommand       => null;
    public ICommand? PasteCommand     => ApplicationCommands.Paste;
    public ICommand? DeleteCommand    => _vm?.Timeline.DeleteSelectedCommand;
    public ICommand? SelectAllCommand => null;

    public void Undo()      => _vm?.Timeline.Undo();
    public void Redo()      => _vm?.Timeline.Redo();
    public void Save()      => _vm?.SaveSessionCommand.Execute(null);
    public Task SaveAsync(CancellationToken ct = default)
    {
        _vm?.SaveSessionCommand.Execute(null);
        return Task.CompletedTask;
    }
    public Task SaveAsAsync(string filePath, CancellationToken ct = default) => SaveAsync(ct);
    public void Copy()      { if (_vm?.Timeline.SelectedFrame?.FullBitmap is { } bmp) Clipboard.SetImage(bmp); }
    public void Cut()       { }
    public void Paste()     => OnPaste(this, null!);
    public void Delete()    => _vm?.Timeline.DeleteSelectedCommand.Execute(null);
    public void SelectAll() { }
    public void Close()     { }

    public bool IsBusy => false;
    public void CancelOperation() { }

    public event EventHandler? ModifiedChanged;
    public event EventHandler? CanUndoChanged;
    public event EventHandler? CanRedoChanged;
    public event EventHandler? SelectionChanged;
    public event EventHandler<string>? TitleChanged;
    public event EventHandler<string>? StatusMessage;
    public event EventHandler<string>? OutputMessage;
    public event EventHandler<DocumentOperationEventArgs>?          OperationStarted;
    public event EventHandler<DocumentOperationEventArgs>?          OperationProgress;
    public event EventHandler<DocumentOperationCompletedEventArgs>? OperationCompleted;

    private void SetDirty(bool dirty)
    {
        if (_isDirty == dirty) return;
        _isDirty = dirty;
        ModifiedChanged?.Invoke(this, EventArgs.Empty);
        TitleChanged?.Invoke(this, Title);
    }

    public ScreenRecorderDocument()
    {
        InitializeComponent();

        CommandBindings.Add(new CommandBinding(ApplicationCommands.Undo,
            (_, _) => _vm?.Timeline.Undo(),
            (_, e)  => e.CanExecute = _vm?.Timeline.CanUndo == true));

        CommandBindings.Add(new CommandBinding(ApplicationCommands.Redo,
            (_, _) => _vm?.Timeline.Redo(),
            (_, e)  => e.CanExecute = _vm?.Timeline.CanRedo == true));

        CommandBindings.Add(new CommandBinding(ApplicationCommands.Copy,
            (_, _) => { if (_vm?.Timeline.SelectedFrame?.FullBitmap is { } bmp) Clipboard.SetImage(bmp); },
            (_, e)  => e.CanExecute = _vm?.Timeline.SelectedFrame?.FullBitmap is not null));

        CommandBindings.Add(new CommandBinding(ApplicationCommands.Paste,
            OnPaste,
            (_, e)  => e.CanExecute = _vm is not null && Clipboard.ContainsImage()));
    }

    public void SetViewModel(ScreenRecorderViewModel vm)
    {
        _vm         = vm;
        DataContext = vm;

        PreviewPane.DataContext     = vm.Preview;
        TimelineStrip.DataContext   = vm.Timeline;
        PropertiesPanel.DataContext = vm.Properties;

        // Mark dirty whenever a frame is added/removed, and clear after save.
        vm.Timeline.Frames.CollectionChanged += (_, _) => SetDirty(true);
        vm.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName is nameof(ScreenRecorderViewModel.LastSavedPath))
                SetDirty(false);
        };

        BuildToolbarItems();
    }

    // ── Clipboard ─────────────────────────────────────────────────────────────

    private void OnPaste(object sender, ExecutedRoutedEventArgs e)
    {
        if (_vm is null) return;
        var bmp = Clipboard.GetImage();
        if (bmp is null) return;

        bmp.Freeze();
        _vm.Timeline.PushUndoPublic();
        var thumb = FrameCaptureEngine.CreateThumbnail(bmp);
        var idx   = _vm.Timeline.Frames.Count;
        _vm.Timeline.AddFrame(new FrameCardViewModel(idx, thumb, _vm.Properties.TimerInterval, bmp));
    }

    // ── Toolbar ───────────────────────────────────────────────────────────────

    private void BuildToolbarItems()
    {
        if (_vm is null) return;

        var exportItems = new ObservableCollection<EditorToolbarItem>
        {
            new() { Label = Properties.ScreenRecorderResources.ScreenRecorder_ExportGif, Command = _vm.ExportGifCommand },
            new() { Label = Properties.ScreenRecorderResources.ScreenRecorder_ExportPng, Command = _vm.ExportPngCommand },
            new() { Label = Properties.ScreenRecorderResources.ScreenRecorder_ExportMp4, Command = _vm.ExportMp4Command },
        };

        ToolbarItems.Clear();
        ToolbarItems.Add(new() { Icon = IcoRecord, Tooltip = Properties.ScreenRecorderResources.ScreenRecorder_Start,        IsToggle = true, Command = _vm.StartCaptureCommand });
        ToolbarItems.Add(new() { Icon = IcoStop,   Tooltip = Properties.ScreenRecorderResources.ScreenRecorder_Stop,         IsToggle = true, Command = _vm.StopCaptureCommand  });
        ToolbarItems.Add(new() { Icon = IcoPause,  Tooltip = Properties.ScreenRecorderResources.ScreenRecorder_Pause,        IsToggle = true, Command = _vm.PauseCaptureCommand });
        ToolbarItems.Add(new() { IsSeparator = true });
        ToolbarItems.Add(new() { Icon = IcoPlay,   Tooltip = Properties.ScreenRecorderResources.ScreenRecorder_Play,         IsToggle = true, Command = _vm.PlayCommand         });
        ToolbarItems.Add(new() { Icon = IcoStop,   Tooltip = Properties.ScreenRecorderResources.ScreenRecorder_StopPlayback, Command  = _vm.StopPlaybackCommand               });
        ToolbarItems.Add(new() { IsSeparator = true });
        ToolbarItems.Add(new() { Icon = IcoCamera, Tooltip = Properties.ScreenRecorderResources.ScreenRecorder_CaptureFrame, Command  = _vm.CaptureFrameCommand               });
        ToolbarItems.Add(new() { Icon = IcoCrop,   Tooltip = Properties.ScreenRecorderResources.ScreenRecorder_SelectRegion, Command  = _vm.SelectRegionCommand               });
        ToolbarItems.Add(new() { Icon = IcoImport, Tooltip = Properties.ScreenRecorderResources.ScreenRecorder_ImportImages, Command  = _vm.ImportImagesCommand               });
        ToolbarItems.Add(new() { IsSeparator = true });
        ToolbarItems.Add(new() { Icon = IcoSave,   Tooltip = Properties.ScreenRecorderResources.ScreenRecorder_SaveSession,  Command  = _vm.SaveSessionCommand,
            DropdownItems = new ObservableCollection<EditorToolbarItem>
            {
                new() { Label = Properties.ScreenRecorderResources.ScreenRecorder_OpenSession, Command = _vm.OpenSessionCommand }
            }});
        ToolbarItems.Add(new() { Label = "Export", DropdownItems = exportItems });
    }
}
