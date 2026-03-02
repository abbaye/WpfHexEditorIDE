//////////////////////////////////////////////
// Apache 2.0  - 2026
// Author : Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Opus 4.6, Claude Sonnet 4.6
//////////////////////////////////////////////

using System.Windows.Automation.Peers;
using System.Windows.Controls;

namespace WpfHexEditor.Docking.Wpf.Automation;

/// <summary>
/// Provides UI Automation information for <see cref="DockTabControl"/>.
/// Exposes each tab as a selectable child with its dock item title.
/// </summary>
internal sealed class DockTabControlAutomationPeer : TabControlAutomationPeer
{
    public DockTabControlAutomationPeer(DockTabControl owner) : base(owner) { }

    protected override string GetClassNameCore() => nameof(DockTabControl);

    protected override AutomationControlType GetAutomationControlTypeCore() =>
        AutomationControlType.Tab;

    protected override string GetNameCore()
    {
        var tc = (DockTabControl)Owner;
        return tc.Node?.ActiveItem?.Title ?? "Dock Tab Group";
    }
}

/// <summary>
/// Provides UI Automation information for a dock tab item.
/// Uses the <see cref="DockItem.Title"/> as the accessible name.
/// </summary>
internal sealed class DockTabItemAutomationPeer : TabItemAutomationPeer
{
    private readonly TabItem _tabItem;

    public DockTabItemAutomationPeer(TabItem owner, TabControlAutomationPeer parent)
        : base(owner, parent)
    {
        _tabItem = owner;
    }

    protected override string GetClassNameCore() => "DockTabItem";

    protected override string GetNameCore()
    {
        if (_tabItem.Tag is Core.Nodes.DockItem item)
            return item.Title;
        return base.GetNameCore();
    }
}
