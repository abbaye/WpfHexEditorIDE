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
using System.Windows;
using System.Windows.Input;
using WpfHexEditor.Editor.Core;
using WpfHexEditor.Plugins.ScreenRecorder.Services;
using WpfHexEditor.Plugins.ScreenRecorder.ViewModels;
using WpfHexEditor.SDK.Commands;

namespace WpfHexEditor.Plugins.ScreenRecorder.Views;

public partial class ScreenRecorderDocument : System.Windows.Controls.UserControl,
                                               IEditorToolbarContributor
{
    // Segoe MDL2 Assets glyph codes used in the contextual toolbar pod.
    private const string IcoRecord  = ""; // Record
    private const string IcoStop    = ""; // Stop
    private const string IcoPause   = ""; // Pause
    private const string IcoPlay    = ""; // Play
    private const string IcoCamera  = ""; // Camera (Capture Frame)
    private const string IcoCrop    = ""; // Crop (Select Region)
    private const string IcoImport  = ""; // Import (Import Images)
    private const string IcoSave    = ""; // Save

    public ObservableCollection<EditorToolbarItem> ToolbarItems { get; } = [];

    private ScreenRecorderViewModel? _vm;

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
