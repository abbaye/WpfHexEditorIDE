// ==========================================================
// Project: WpfHexEditor.Shell
// File: DockKeyboardNavigation.cs
// Author: Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude (Anthropic)
// Created: 2026-03-06
// Description:
//     Keyboard navigation helper for DockControl. Handles Ctrl+Tab to open the
//     NavigatorWindow for visual document switching, and Alt+F6 to cycle through
//     visible tool panels in activation-history order.
//
// Architecture Notes:
//     Subscribes to DockControl.PreviewKeyDown at construction; must call Detach()
//     on disposal to prevent memory leaks. NavigatorWindow is created lazily and
//     destroyed after the key is released.
//
// ==========================================================

using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using WpfHexEditor.Docking.Core.Nodes;
using DCore = WpfHexEditor.Docking.Core;

namespace WpfHexEditor.Shell;

/// <summary>
/// Keyboard navigation helper for <see cref="DockControl"/>.
/// Handles Ctrl+Tab (<see cref="NavigatorWindow"/>), Alt+F6 (cycle panels).
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

    private bool _isDockNavMode;

    private void OnPreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Tab && Keyboard.Modifiers.HasFlag(ModifierKeys.Control))
        {
            OpenNavigator();
            e.Handled = true;
        }
        else if (e.Key == Key.F6 && Keyboard.Modifiers.HasFlag(ModifierKeys.Alt))
        {
            CyclePanels();
            e.Handled = true;
        }
        else if (e.Key == Key.D && Keyboard.Modifiers == (ModifierKeys.Alt | ModifierKeys.Shift))
        {
            ToggleDockNavigationMode();
            e.Handled = true;
        }
        else if (_isDockNavMode)
        {
            HandleDockNavKey(e);
        }
    }

    private void ToggleDockNavigationMode()
    {
        _isDockNavMode = !_isDockNavMode;
        // Visual feedback: highlight the border of the focused panel
        if (!_isDockNavMode) return;
        var tabControls = new List<DockTabControl>();
        CollectTabControls(_dockControl, tabControls);
        if (tabControls.Count > 0 && tabControls[0].SelectedItem is TabItem tab)
            tab.Focus();
    }

    private void HandleDockNavKey(KeyEventArgs e)
    {
        switch (e.Key)
        {
            case Key.Escape:
                _isDockNavMode = false;
                e.Handled = true;
                break;

            case Key.Enter:
                _isDockNavMode = false;
                e.Handled = true;
                break;

            case Key.Left:
            case Key.Right:
            case Key.Up:
            case Key.Down:
                CyclePanels();
                e.Handled = true;
                break;
        }
    }

    private void OpenNavigator()
    {
        if (_dockControl.Layout is null) return;

        // Partition items into documents vs tools, ordered by MRU
        var documents = new List<DockItem>();
        var tools = new List<DockItem>();

        // Start with MRU order
        foreach (var item in _dockControl.ActivationHistory)
        {
            if (item.State is not (DCore.DockItemState.Docked or DCore.DockItemState.Float))
                continue;

            if (item.Owner is DocumentHostNode)
                documents.Add(item);
            else
                tools.Add(item);
        }

        // Add any items not yet in history
        foreach (var group in _dockControl.Layout.GetAllGroups())
        {
            foreach (var item in group.Items)
            {
                if (documents.Contains(item) || tools.Contains(item)) continue;
                if (group is DocumentHostNode)
                    documents.Add(item);
                else
                    tools.Add(item);
            }
        }

        if (documents.Count == 0 && tools.Count == 0) return;

        // Pre-select the second MRU item (the "previous" tab)
        DockItem? preSelect = _dockControl.ActivationHistory.Count > 1
            ? _dockControl.ActivationHistory[1]
            : _dockControl.ActivationHistory.FirstOrDefault();

        var navigator = new NavigatorWindow(documents, tools, preSelect)
        {
            Owner = Window.GetWindow(_dockControl),
            WindowStartupLocation = WindowStartupLocation.CenterOwner
        };
        navigator.ShowDialog();

        if (navigator.SelectedItem is { } selected)
            ActivateItem(selected);
    }

    private void ActivateItem(DockItem item)
    {
        var tabControls = new List<DockTabControl>();
        CollectTabControls(_dockControl, tabControls);

        foreach (var tc in tabControls)
        {
            for (var i = 0; i < tc.Items.Count; i++)
            {
                if (tc.Items[i] is TabItem { Tag: DockItem di } && di == item)
                {
                    tc.SelectedIndex = i;
                    if (tc.Items[i] is TabItem tab)
                        tab.Focus();
                    return;
                }
            }
        }
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
