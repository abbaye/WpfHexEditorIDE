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

    public static readonly RoutedUICommand CloseAll = new(
        "Close All", nameof(CloseAll), typeof(DockCommands));

    public static readonly RoutedUICommand AutoHideAll = new(
        "Auto Hide All", nameof(AutoHideAll), typeof(DockCommands));

    public static readonly RoutedUICommand RestoreAll = new(
        "Restore All", nameof(RestoreAll), typeof(DockCommands));

    public static readonly RoutedUICommand UndoLayout = new(
        "Undo Layout", nameof(UndoLayout), typeof(DockCommands),
        new InputGestureCollection { new KeyGesture(Key.Z, ModifierKeys.Control | ModifierKeys.Alt) });

    public static readonly RoutedUICommand RedoLayout = new(
        "Redo Layout", nameof(RedoLayout), typeof(DockCommands),
        new InputGestureCollection { new KeyGesture(Key.Y, ModifierKeys.Control | ModifierKeys.Shift) });
}
