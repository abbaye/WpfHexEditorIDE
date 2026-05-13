// Project      : WpfHexEditor.App
// File         : Scripting/ScriptingModule.cs
// Description  : App-layer module providing the dockable Scripting Console panel.
// Architecture : Lazy-init via EnsureActivated(); single panel singleton.
//                Mirrors the pattern of HexDiffModule / BinaryAnalysisModule.

using System.Windows;
using WpfHexEditor.App.Scripting.Panels;
using WpfHexEditor.App.Scripting.ViewModels;
using WpfHexEditor.SDK.Contracts;

namespace WpfHexEditor.App.Scripting;

internal sealed class ScriptingModule
{
    public const string ContentId = "panel-scripting";

    private IIDEHostContext?       _context;
    private bool                   _activated;
    private ScriptingConsolePanel? _panel;

    public System.Threading.Tasks.Task InitializeAsync(IIDEHostContext context)
    {
        _context = context;
        return System.Threading.Tasks.Task.CompletedTask;
    }

    public void Shutdown()
    {
        _context = null;
    }

    public UIElement? GetPanel(string contentId)
    {
        if (contentId != ContentId) return null;
        EnsureActivated();
        return _panel;
    }

    private void EnsureActivated()
    {
        if (_activated || _context is null) return;
        _activated = true;

        var vm = new ScriptingConsolePanelViewModel(_context.Scripting);
        _panel  = new ScriptingConsolePanel(vm);
    }
}
