//////////////////////////////////////////////
// Apache 2.0  - 2026
// Author : Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Opus 4.6
//////////////////////////////////////////////

using System.Windows.Automation.Peers;
using System.Windows.Controls;

namespace WpfHexEditor.Docking.Wpf.Automation;

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
