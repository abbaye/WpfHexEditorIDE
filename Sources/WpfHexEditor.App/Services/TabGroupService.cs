//////////////////////////////////////////////
// Project      : WpfHexEditor.App
// File         : TabGroupService.cs
// Description  : Concrete ITabGroupService that wraps DockControl to expose
//                tab group operations through the plugin SDK.
// Architecture : Internal service, instantiated in MainWindow.PluginSystem.cs
//                and exposed via IDEHostContext.TabGroups.
//////////////////////////////////////////////

using WpfHexEditor.Docking.Core;
using WpfHexEditor.Docking.Core.Nodes;
using WpfHexEditor.SDK.Contracts.Services;
using WpfHexEditor.Shell.Commands;

namespace WpfHexEditor.App.Services;

internal sealed class TabGroupService : ITabGroupService
{
    private readonly WpfHexEditor.Shell.DockControl _dockControl;
    private int _lastGroupCount;

    public TabGroupService(WpfHexEditor.Shell.DockControl dockControl)
    {
        _dockControl = dockControl ?? throw new ArgumentNullException(nameof(dockControl));
        _lastGroupCount = GroupCount;
    }

    // Called by MainWindow after _engine is available so we can subscribe to LayoutChanged.
    internal void AttachEngine(DockEngine engine)
    {
        engine.LayoutChanged += OnLayoutChanged;
    }

    private void OnLayoutChanged()
    {
        var current = GroupCount;
        if (current != _lastGroupCount)
        {
            _lastGroupCount = current;
            GroupCountChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    // ── ITabGroupService ────────────────────────────────────────────────────

    public int GroupCount
        => _dockControl.Layout?.GetAllDocumentHosts().Count() ?? 1;

    public int ActiveGroupIndex
    {
        get
        {
            var hosts = _dockControl.Layout?.GetAllDocumentHosts().ToList();
            if (hosts is null) return 0;
            var active = _dockControl.GetActiveDocumentItem();
            if (active is null) return 0;
            var ownerIdx = hosts.IndexOf(active.Owner as DocumentHostNode ?? hosts[0]);
            return ownerIdx >= 0 ? ownerIdx : 0;
        }
    }

    public void SplitVertical()
        => DockCommands.NewVerticalTabGroup.Execute(null, _dockControl);

    public void SplitHorizontal()
        => DockCommands.NewHorizontalTabGroup.Execute(null, _dockControl);

    public void MoveActiveToNextGroup()
        => DockCommands.MoveToNextTabGroup.Execute(null, _dockControl);

    public void MoveActiveToPreviousGroup()
        => DockCommands.MoveToPreviousTabGroup.Execute(null, _dockControl);

    public void CloseAllGroups()
        => _dockControl.HandleCloseAllTabGroups();

    public void FocusGroup(int index)
    {
        var hosts = _dockControl.Layout?.GetAllDocumentHosts().ToList();
        if (hosts is null || index < 0 || index >= hosts.Count) return;
        _dockControl.FocusDocumentHost(hosts[index]);
    }

    public event EventHandler? GroupCountChanged;
}
