// ==========================================================
// Project: WpfHexEditor.Shell
// File: DockTabControlAutomationPeer.cs
// Author: Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude (Anthropic)
// Created: 2026-03-06
// Description:
//     Provides UI Automation (UIA) peer information for DockTabControl so that
//     screen readers and accessibility tools can correctly identify the active tab
//     group and its docked items.
//
// Architecture Notes:
//     Extends TabControlAutomationPeer. Exposes each tab as a selectable child
//     with its DockItem.Title as the accessible name for full UIA compliance.
//
// ==========================================================

using System.Windows.Automation.Peers;
using System.Windows.Controls;
using Core = WpfHexEditor.Docking.Core;

namespace WpfHexEditor.Shell.Automation;

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
