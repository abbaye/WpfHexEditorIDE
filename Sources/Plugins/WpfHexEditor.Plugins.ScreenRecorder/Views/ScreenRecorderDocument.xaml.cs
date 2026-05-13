// ==========================================================
// Project: WpfHexEditor.Plugins.ScreenRecorder
// File: Views/ScreenRecorderDocument.xaml.cs
// Description: Code-behind for the Screen Recorder document tab.
//              Implements IEditorToolbarContributor so the IDE toolbar shows
//              recorder controls when this tab is active.
// Architecture Notes:
//     IEditorToolbarContributor swap is driven by MainWindow.OnActiveDocumentChanged.
//     ViewModel is injected after construction via SetViewModel().
// ==========================================================

using System.Collections.ObjectModel;
using WpfHexEditor.Editor.Core;
using WpfHexEditor.Plugins.ScreenRecorder.ViewModels;
using WpfHexEditor.SDK.Commands;

namespace WpfHexEditor.Plugins.ScreenRecorder.Views;

public partial class ScreenRecorderDocument : System.Windows.Controls.UserControl,
                                               IEditorToolbarContributor
{
    public ObservableCollection<EditorToolbarItem> ToolbarItems { get; } = [];

    private ScreenRecorderViewModel? _vm;

    public ScreenRecorderDocument() => InitializeComponent();

    public void SetViewModel(ScreenRecorderViewModel vm)
    {
        _vm      = vm;
        DataContext = vm;

        PreviewPane.DataContext   = vm.Preview;
        TimelineStrip.DataContext = vm.Timeline;
        PropertiesPanel.DataContext = vm.Properties;

        BuildToolbarItems();
    }

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
        ToolbarItems.Add(new() { Icon = "", Tooltip = Properties.ScreenRecorderResources.ScreenRecorder_Start,         IsToggle = true, Command = _vm.StartCaptureCommand  });
        ToolbarItems.Add(new() { Icon = "", Tooltip = Properties.ScreenRecorderResources.ScreenRecorder_Stop,          IsToggle = true, Command = _vm.StopCaptureCommand   });
        ToolbarItems.Add(new() { Icon = "", Tooltip = Properties.ScreenRecorderResources.ScreenRecorder_Pause,         IsToggle = true, Command = _vm.PauseCaptureCommand  });
        ToolbarItems.Add(new() { IsSeparator = true });
        ToolbarItems.Add(new() { Icon = "", Tooltip = Properties.ScreenRecorderResources.ScreenRecorder_CaptureFrame,  Command  = _vm.CaptureFrameCommand                 });
        ToolbarItems.Add(new() { Icon = "", Tooltip = Properties.ScreenRecorderResources.ScreenRecorder_SelectRegion,  Command  = _vm.SelectRegionCommand                 });
        ToolbarItems.Add(new() { IsSeparator = true });
        ToolbarItems.Add(new() { Icon = "", Tooltip = Properties.ScreenRecorderResources.ScreenRecorder_SaveSession,   Command  = _vm.SaveSessionCommand,   DropdownItems = new ObservableCollection<EditorToolbarItem> { new() { Label = Properties.ScreenRecorderResources.ScreenRecorder_OpenSession, Command = _vm.OpenSessionCommand } } });
        ToolbarItems.Add(new() { Label = "Export",                                                                           DropdownItems = exportItems });
    }
}
