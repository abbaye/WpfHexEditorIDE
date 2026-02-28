//////////////////////////////////////////////
// Apache 2.0  - 2026
// Author : Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Opus 4.6
//////////////////////////////////////////////

using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace WpfHexEditor.Docking.Wpf;

/// <summary>
/// Keyboard navigation helper for <see cref="DockControl"/>.
/// Handles Ctrl+Tab (cycle document tabs), Ctrl+Shift+Tab (reverse), Alt+F6 (cycle panels).
/// </summary>
internal sealed class DockKeyboardNavigation
{
    private readonly DockControl _dockControl;

    public DockKeyboardNavigation(DockControl dockControl)
    {
        _dockControl = dockControl;
        _dockControl.PreviewKeyDown += OnPreviewKeyDown;
    }

    public void Detach()
    {
        _dockControl.PreviewKeyDown -= OnPreviewKeyDown;
    }

    private void OnPreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Tab && Keyboard.Modifiers.HasFlag(ModifierKeys.Control))
        {
            // Ctrl+Tab / Ctrl+Shift+Tab: cycle tabs in the focused TabControl
            var reverse = Keyboard.Modifiers.HasFlag(ModifierKeys.Shift);
            CycleTabs(reverse);
            e.Handled = true;
        }
        else if (e.Key == Key.F6 && Keyboard.Modifiers.HasFlag(ModifierKeys.Alt))
        {
            // Alt+F6: move focus to the next panel group
            CyclePanels();
            e.Handled = true;
        }
    }

    private void CycleTabs(bool reverse)
    {
        var tabControl = FindFocusedTabControl();
        if (tabControl is null || tabControl.Items.Count <= 1) return;

        var index = tabControl.SelectedIndex;
        var count = tabControl.Items.Count;
        tabControl.SelectedIndex = reverse
            ? (index - 1 + count) % count
            : (index + 1) % count;

        // Move focus into the newly selected tab content
        if (tabControl.SelectedItem is TabItem tab)
            tab.Focus();
    }

    private void CyclePanels()
    {
        var tabControls = new List<DockTabControl>();
        CollectTabControls(_dockControl, tabControls);
        if (tabControls.Count <= 1) return;

        var current = FindFocusedTabControl();
        var index = current is not null ? tabControls.IndexOf(current) : -1;
        var next = tabControls[(index + 1) % tabControls.Count];

        if (next.SelectedItem is TabItem tab)
            tab.Focus();
        else
            next.Focus();
    }

    private DockTabControl? FindFocusedTabControl()
    {
        var focused = Keyboard.FocusedElement as DependencyObject;
        while (focused is not null)
        {
            if (focused is DockTabControl tc)
                return tc;
            focused = VisualTreeHelper.GetParent(focused);
        }
        return null;
    }

    private static void CollectTabControls(DependencyObject parent, List<DockTabControl> result)
    {
        var count = VisualTreeHelper.GetChildrenCount(parent);
        for (var i = 0; i < count; i++)
        {
            var child = VisualTreeHelper.GetChild(parent, i);
            if (child is DockTabControl tc)
                result.Add(tc);
            else
                CollectTabControls(child, result);
        }
    }
}
