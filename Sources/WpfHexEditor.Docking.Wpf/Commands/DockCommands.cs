// ==========================================================
// Project: WpfHexEditor.Shell
// File: DockCommands.cs
// Author: Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude (Anthropic)
// Created: 2026-03-06
// Description:
//     Defines the standard WPF RoutedUICommands for dock operations: Hide, Show,
//     Close, Float, AutoHide, DockToDocument, and Pin. These commands can be
//     bound in XAML using x:Static syntax and handled by CommandBindings on DockControl.
//
// Architecture Notes:
//     Command pattern via WPF RoutedUICommand. Static class — no state.
//     Commands are consumed by DockControl CommandBindings and context menus in Generic.xaml.
//
// ==========================================================

using System.Windows.Input;

namespace WpfHexEditor.Shell.Commands;

/// <summary>
/// Standard routed commands for dock operations.
/// Can be used in XAML: <c>Command="{x:Static docking:DockCommands.Close}"</c>
/// </summary>
public static class DockCommands
{
    public static readonly RoutedUICommand Hide = new(
        "Hide", nameof(Hide), typeof(DockCommands));

    public static readonly RoutedUICommand Show = new(
        "Show", nameof(Show), typeof(DockCommands));

    public static readonly RoutedUICommand Close = new(
        "Close", nameof(Close), typeof(DockCommands));

    public static readonly RoutedUICommand Float = new(
        "Float", nameof(Float), typeof(DockCommands));

    public static readonly RoutedUICommand AutoHide = new(
        "Auto Hide", nameof(AutoHide), typeof(DockCommands));

    public static readonly RoutedUICommand Dock = new(
        "Dock", nameof(Dock), typeof(DockCommands));

    public static readonly RoutedUICommand DockAsDocument = new(
        "Dock as Tabbed Document", nameof(DockAsDocument), typeof(DockCommands));

    public static readonly RoutedUICommand RestoreToToolPanel = new(
        "Dock as Tool Window", nameof(RestoreToToolPanel), typeof(DockCommands));

    public static readonly RoutedUICommand CloseAll = new(
        "Close All", nameof(CloseAll), typeof(DockCommands));

    public static readonly RoutedUICommand AutoHideAll = new(
        "Auto Hide All", nameof(AutoHideAll), typeof(DockCommands));

    public static readonly RoutedUICommand RestoreAll = new(
        "Restore All", nameof(RestoreAll), typeof(DockCommands));

    public static readonly RoutedUICommand UndoLayout = new(
        "Undo Layout", nameof(UndoLayout), typeof(DockCommands),
        new InputGestureCollection { new KeyGesture(Key.Z, ModifierKeys.Control | ModifierKeys.Shift) });

    public static readonly RoutedUICommand RedoLayout = new(
        "Redo Layout", nameof(RedoLayout), typeof(DockCommands),
        new InputGestureCollection { new KeyGesture(Key.Y, ModifierKeys.Control | ModifierKeys.Shift) });

    public static readonly RoutedUICommand NewVerticalTabGroup = new(
        "New Vertical Tab Group", nameof(NewVerticalTabGroup), typeof(DockCommands));

    public static readonly RoutedUICommand NewHorizontalTabGroup = new(
        "New Horizontal Tab Group", nameof(NewHorizontalTabGroup), typeof(DockCommands));

    public static readonly RoutedUICommand MoveToNextTabGroup = new(
        "Move to Next Tab Group", nameof(MoveToNextTabGroup), typeof(DockCommands));

    public static readonly RoutedUICommand MoveToPreviousTabGroup = new(
        "Move to Previous Tab Group", nameof(MoveToPreviousTabGroup), typeof(DockCommands));

    public static readonly RoutedUICommand PinTab = new(
        "Pin Tab", nameof(PinTab), typeof(DockCommands),
        new InputGestureCollection { new KeyGesture(Key.P, ModifierKeys.Control | ModifierKeys.Alt) });

    public static readonly RoutedUICommand QuickWindowSearch = new(
        "Quick Window Search", nameof(QuickWindowSearch), typeof(DockCommands),
        new InputGestureCollection { new KeyGesture(Key.A, ModifierKeys.Control | ModifierKeys.Shift) });

    public static readonly RoutedUICommand QuickSaveLayout1 = new(
        "Save Layout 1", nameof(QuickSaveLayout1), typeof(DockCommands),
        new InputGestureCollection { new KeyGesture(Key.D1, ModifierKeys.Control | ModifierKeys.Shift) });

    public static readonly RoutedUICommand QuickSaveLayout2 = new(
        "Save Layout 2", nameof(QuickSaveLayout2), typeof(DockCommands),
        new InputGestureCollection { new KeyGesture(Key.D2, ModifierKeys.Control | ModifierKeys.Shift) });

    public static readonly RoutedUICommand QuickSaveLayout3 = new(
        "Save Layout 3", nameof(QuickSaveLayout3), typeof(DockCommands),
        new InputGestureCollection { new KeyGesture(Key.D3, ModifierKeys.Control | ModifierKeys.Shift) });

    public static readonly RoutedUICommand QuickSaveLayout4 = new(
        "Save Layout 4", nameof(QuickSaveLayout4), typeof(DockCommands),
        new InputGestureCollection { new KeyGesture(Key.D4, ModifierKeys.Control | ModifierKeys.Shift) });

    public static readonly RoutedUICommand QuickLoadLayout1 = new(
        "Load Layout 1", nameof(QuickLoadLayout1), typeof(DockCommands),
        new InputGestureCollection { new KeyGesture(Key.D1, ModifierKeys.Control | ModifierKeys.Alt) });

    public static readonly RoutedUICommand QuickLoadLayout2 = new(
        "Load Layout 2", nameof(QuickLoadLayout2), typeof(DockCommands),
        new InputGestureCollection { new KeyGesture(Key.D2, ModifierKeys.Control | ModifierKeys.Alt) });

    public static readonly RoutedUICommand QuickLoadLayout3 = new(
        "Load Layout 3", nameof(QuickLoadLayout3), typeof(DockCommands),
        new InputGestureCollection { new KeyGesture(Key.D3, ModifierKeys.Control | ModifierKeys.Alt) });

    public static readonly RoutedUICommand QuickLoadLayout4 = new(
        "Load Layout 4", nameof(QuickLoadLayout4), typeof(DockCommands),
        new InputGestureCollection { new KeyGesture(Key.D4, ModifierKeys.Control | ModifierKeys.Alt) });

    public static readonly RoutedUICommand ManageProfiles = new(
        "Manage Layout Profiles", nameof(ManageProfiles), typeof(DockCommands));
}
