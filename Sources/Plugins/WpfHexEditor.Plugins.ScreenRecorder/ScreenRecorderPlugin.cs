// ==========================================================
// Project: WpfHexEditor.Plugins.ScreenRecorder
// File: ScreenRecorderPlugin.cs
// Description: Plugin entry point for the Screen Recorder feature.
//              Registers the Tools menu item and opens a document tab on demand.
//              Commands (F9/Shift+F9/ESC) are registered via ICommandRegistry.
// Architecture Notes:
//     Document tab is lazy — created on first open, then activated on subsequent calls.
//     IPluginWithOptions wires the Options page into the IDE settings panel.
// ==========================================================

using System.Windows;
using WpfHexEditor.Plugins.ScreenRecorder.Properties;
using WpfHexEditor.Plugins.ScreenRecorder.ViewModels;
using WpfHexEditor.Plugins.ScreenRecorder.Views;
using WpfHexEditor.SDK.Commands;
using WpfHexEditor.SDK.Contracts;
using WpfHexEditor.SDK.Descriptors;
using WpfHexEditor.SDK.Models;

namespace WpfHexEditor.Plugins.ScreenRecorder;

public sealed class ScreenRecorderPlugin : IWpfHexEditorPlugin, IPluginWithOptions
{
    private const string DocUiId      = "WpfHexEditor.Plugins.ScreenRecorder.Document";
    private const string CmdCapture   = "ScreenRecorder.CaptureFrame";
    private const string CmdStop      = "ScreenRecorder.StopSession";
    private const string CmdCancel    = "ScreenRecorder.CancelSession";
    private const string CmdStart     = "ScreenRecorder.StartSession";

    public string  Id      => "WpfHexEditor.Plugins.ScreenRecorder";
    public string  Name    => ScreenRecorderResources.ScreenRecorder_PluginName;
    public Version Version => new(1, 0, 0);

    public PluginCapabilities Capabilities => new()
    {
        AccessFileSystem = true,
        RegisterMenus    = true,
        WriteOutput      = true
    };

    private IIDEHostContext?         _context;
    private ScreenRecorderDocument?  _document;
    private ScreenRecorderViewModel? _vm;
    private bool                     _documentOpen;

    public Task InitializeAsync(IIDEHostContext context, CancellationToken ct = default)
    {
        _context = context;

        context.UIRegistry.RegisterMenuItem(
            $"{Id}.Menu.Show",
            Id,
            new MenuItemDescriptor
            {
                Header     = ScreenRecorderResources.ScreenRecorder_MenuItem,
                ParentPath = "Tools",
                Group      = "MediaTools",
                IconGlyph  = "",
                Command    = new RelayCommand(_ => OpenOrFocusDocument())
            });

        RegisterCommands(context);
        return Task.CompletedTask;
    }

    public Task ShutdownAsync(CancellationToken ct = default)
    {
        _context?.CommandRegistry?.Unregister(CmdCapture);
        _context?.CommandRegistry?.Unregister(CmdStop);
        _context?.CommandRegistry?.Unregister(CmdCancel);
        _context?.CommandRegistry?.Unregister(CmdStart);

        _document     = null;
        _vm           = null;
        _documentOpen = false;
        _context      = null;
        return Task.CompletedTask;
    }

    // ── Options (IPluginWithOptions) ──────────────────────────────────────────

    public FrameworkElement CreateOptionsPage() => new Options.ScreenRecorderOptionsPage();
    public void SaveOptions() => Options.ScreenRecorderOptions.Instance.Save();
    public void LoadOptions() => Options.ScreenRecorderOptions.Reload();
    public string GetOptionsCategory()     => "Tools";
    public string GetOptionsCategoryIcon() => "";

    // ── Document tab management ───────────────────────────────────────────────

    private void OpenOrFocusDocument()
    {
        if (_context is null) return;

        if (!_documentOpen)
        {
            _vm       = new ScreenRecorderViewModel();
            _document = new ScreenRecorderDocument();
            _document.SetViewModel(_vm);

            _context.UIRegistry.RegisterDocumentTab(
                DocUiId,
                _document,
                Id,
                new DocumentDescriptor
                {
                    Title    = ScreenRecorderResources.ScreenRecorder_DocumentTitle,
                    CanClose = true
                });

            _documentOpen = true;
        }
        else
        {
            _context.UIRegistry.ShowPanel(DocUiId);
        }
    }

    // ── Command registration ──────────────────────────────────────────────────

    private void RegisterCommands(IIDEHostContext context)
    {
        if (context.CommandRegistry is null) return;

        context.CommandRegistry.Register(new SdkCommandDefinition(
            CmdStart,
            ScreenRecorderResources.ScreenRecorder_Start,
            Name, null, "",
            new RelayCommand(_ => OpenOrFocusDocument())));

        context.CommandRegistry.Register(new SdkCommandDefinition(
            CmdCapture,
            ScreenRecorderResources.ScreenRecorder_CaptureFrame,
            Name, "F9", "",
            new RelayCommand(
                _ => _vm?.CaptureFrameCommand.Execute(null),
                _ => _vm?.IsSessionActive ?? false)));

        context.CommandRegistry.Register(new SdkCommandDefinition(
            CmdStop,
            ScreenRecorderResources.ScreenRecorder_Stop,
            Name, "Shift+F9", "",
            new RelayCommand(
                _ => _vm?.StopCaptureCommand.Execute(null),
                _ => _vm?.IsSessionActive ?? false)));

        context.CommandRegistry.Register(new SdkCommandDefinition(
            CmdCancel,
            ScreenRecorderResources.ScreenRecorder_ConfirmCancelTitle,
            Name, "Escape", "",
            new RelayCommand(
                _ => _vm?.StopCaptureCommand.Execute(null),
                _ => _vm?.IsSessionActive ?? false)));
    }
}
