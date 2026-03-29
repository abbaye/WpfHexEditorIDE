// ==========================================================
// Project: WpfHexEditor.App
// File: Services/StatusBarManager.cs
// Author: Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.6
// Created: 2026-03-29
// Description:
//     Unified facade for all status bar update operations.
//     Owns the StatusBarAdapter (plugin items) and exposes typed
//     update methods for Build, Debug, and Workspace status slots.
//     Replaces scattered null-guard + Visibility assignments in
//     MainWindow.Build.cs, MainWindow.Debug.cs and MainWindow.Workspace.cs.
//
// Architecture Notes:
//     Initialized by MainWindow after InitializeComponent() so it can hold
//     references to named XAML controls.  Not DI-registered (needs WPF
//     control refs that are only available after XAML loads).
//     All update methods are dispatcher-safe — they InvokeAsync when called
//     from background threads.
// ==========================================================

using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Threading;

namespace WpfHexEditor.App.Services;

/// <summary>
/// Centralises all status bar update logic for the app shell.
/// </summary>
public sealed class StatusBarManager
{
    // ── WPF control refs (set once, never null after Initialize) ──────────────
    private readonly Dispatcher _dispatcher;

    // Build slot
    private StatusBarItem? _buildItem;
    private TextBlock?     _buildText;
    private TextBlock?     _buildIcon;
    private ProgressBar?   _buildProgress;

    // Debug slot
    private StatusBarItem? _debugItem;
    private TextBlock?     _debugText;

    // Workspace slot
    private StatusBarItem? _workspaceItem;
    private TextBlock?     _workspaceText;

    // ── Constructor ───────────────────────────────────────────────────────────

    public StatusBarManager(Dispatcher dispatcher)
    {
        _dispatcher = dispatcher ?? throw new ArgumentNullException(nameof(dispatcher));
    }

    // ── Initialization ────────────────────────────────────────────────────────

    /// <summary>
    /// Must be called once by MainWindow after <c>InitializeComponent()</c>
    /// with the named XAML controls that this manager owns.
    /// </summary>
    public void Initialize(
        StatusBarItem? buildItem,    TextBlock? buildText,    TextBlock? buildIcon,  ProgressBar? buildProgress,
        StatusBarItem? debugItem,    TextBlock? debugText,
        StatusBarItem? workspaceItem, TextBlock? workspaceText)
    {
        _buildItem      = buildItem;
        _buildText      = buildText;
        _buildIcon      = buildIcon;
        _buildProgress  = buildProgress;
        _debugItem      = debugItem;
        _debugText      = debugText;
        _workspaceItem  = workspaceItem;
        _workspaceText  = workspaceText;
    }

    // ── Build status ──────────────────────────────────────────────────────────

    /// <summary>
    /// Updates the build status slot.
    /// <paramref name="progressPercent"/>: 0–100 = show progress bar; -1 = hide.
    /// </summary>
    public void UpdateBuildStatus(string text, string icon, bool visible, int progressPercent)
    {
        _dispatcher.InvokeAsync(() =>
        {
            if (_buildItem is null || _buildText is null || _buildIcon is null || _buildProgress is null) return;

            _buildText.Text      = text;
            _buildIcon.Text      = icon;
            _buildItem.Visibility = visible ? Visibility.Visible : Visibility.Collapsed;

            if (progressPercent >= 0)
            {
                _buildProgress.Value      = progressPercent;
                _buildProgress.Visibility = Visibility.Visible;
            }
            else
            {
                _buildProgress.Visibility = Visibility.Collapsed;
            }
        });
    }

    // ── Debug status ──────────────────────────────────────────────────────────

    /// <summary>
    /// Shows or hides the debug status slot with an optional message.
    /// </summary>
    public void UpdateDebugStatus(string? message, bool visible)
    {
        _dispatcher.InvokeAsync(() =>
        {
            if (_debugItem is null) return;
            _debugItem.Visibility = visible ? Visibility.Visible : Visibility.Collapsed;
            if (_debugText is not null && message is not null)
                _debugText.Text = message;
        });
    }

    // ── Workspace status ──────────────────────────────────────────────────────

    /// <summary>
    /// Shows or hides the workspace status slot.
    /// Pass <c>null</c> or empty to hide.
    /// </summary>
    public void UpdateWorkspaceStatus(string? workspaceName)
    {
        _dispatcher.InvokeAsync(() =>
        {
            if (_workspaceItem is null) return;
            if (string.IsNullOrEmpty(workspaceName))
            {
                _workspaceItem.Visibility = Visibility.Collapsed;
                return;
            }
            if (_workspaceText is not null)
                _workspaceText.Text = workspaceName;
            _workspaceItem.Visibility = Visibility.Visible;
        });
    }
}
