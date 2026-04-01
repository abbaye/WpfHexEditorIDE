// ==========================================================
// Project: WpfHexEditor.Shell
// File: AutoHideBarAutomationPeer.cs
// Author: Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude (Anthropic)
// Created: 2026-03-06
// Description:
//     Provides UI Automation (UIA) peer information for AutoHideBar so that
//     screen readers and accessibility tools can correctly identify and interact
//     with the auto-hide button bar.
//
// Architecture Notes:
//     Extends FrameworkElementAutomationPeer. Reports as ToolBar control type
//     so screen readers announce the bar with its Dock position as the accessible name.
//
// ==========================================================

using System.Windows.Automation.Peers;
using System.Windows.Controls;

namespace WpfHexEditor.Shell.Automation;

/// <summary>
/// Provides UI Automation information for <see cref="AutoHideBar"/>.
/// Identifies as a ToolBar control so screen readers announce it appropriately.
/// </summary>
internal sealed class AutoHideBarAutomationPeer : FrameworkElementAutomationPeer
{
    public AutoHideBarAutomationPeer(AutoHideBar owner) : base(owner) { }

    protected override string GetClassNameCore() => nameof(AutoHideBar);

    protected override AutomationControlType GetAutomationControlTypeCore() =>
        AutomationControlType.ToolBar;

    protected override string GetNameCore()
    {
        var bar = (AutoHideBar)Owner;
        return $"Auto-Hide {bar.Position}";
    }
}
