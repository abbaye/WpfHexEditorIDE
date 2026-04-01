//////////////////////////////////////////////
// GNU Affero General Public License v3.0 - 2026
// Author : Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.6
//////////////////////////////////////////////

// ==========================================================
// Project: WpfHexEditor.PluginSandbox
// File: IpcUIRegistry.cs
// Created: 2026-03-15
// Description:
//     Sandbox-side implementation of IUIRegistry.
//     Instead of registering WPF controls directly (impossible cross-process),
//     creates an HwndSource child window hosting the plugin's UIElement and
//     sends its Win32 HWND to the IDE host via a RegisterPanelNotification IPC
//     message. The host wraps the HWND in an HwndHost and docks it.
//
//     Phase 10 additions:
//     - RegisterMenuItem / RegisterToolbarItem: stores the ICommand locally under
//       a generated CommandId, then sends RegisterMenuItemNotification /
//       RegisterToolbarItemNotification IPC so the host creates a real menu item
//       backed by an IpcRelayCommand. When the user clicks, the host sends
//       ExecuteCommandRequest → sandbox looks up the stored command by CommandId
//       and executes it.
//     - ShowPanel / HidePanel / TogglePanel / FocusPanel: sends PanelActionNotification
//       to the host so the docking system handles visibility rather than calling
//       Win32 ShowWindow directly on the embedded HwndSource.
//     - RegisterStatusBarItem: sends RegisterStatusBarItemNotification.
//
// Architecture Notes:
//     - Pattern: Adapter — bridges IUIRegistry API to cross-process HWND embedding.
//     - HwndSource is created with WS_POPUP (no parent) so it can be reparented
//       by the host later via SetParent(). This avoids the chicken-and-egg problem
//       of WS_CHILD requiring a valid parent HWND at creation time.
//     - All HwndSource creation happens on the STA Dispatcher thread (required
//       by WPF). Callers must ensure this invariant (guaranteed via
//       dispatcher.InvokeAsync in Program.cs message loop).
//     - Fire-and-forget SendAsync: HWND notification is best-effort; a missed
//       notification causes the panel not to appear (harmless, no crash).
//     - ResizePanelRequest forwarded from host via HandleResizeAsync().
// ==========================================================

using System.Runtime.InteropServices;
using System.Text.Json;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
using WpfHexEditor.SDK.Contracts;
using WpfHexEditor.SDK.Descriptors;
using WpfHexEditor.SDK.Sandbox;

namespace WpfHexEditor.PluginSandbox;

/// <summary>
/// <see cref="IUIRegistry"/> implementation for the sandbox process.
/// Hosts plugin WPF controls in <see cref="HwndSource"/> child windows and
/// notifies the IDE host of the resulting HWNDs so they can be embedded.
/// Menu, toolbar and status-bar items are forwarded via IPC so the host
/// creates them in the real IDE menus / toolbars.
/// </summary>
internal sealed class IpcUIRegistry : IUIRegistry
{
    private readonly IpcChannel _channel;
    private readonly CancellationToken _ct;

    // Registered HwndSources keyed by content ID
    private readonly Dictionary<string, HwndSource> _sources =
        new(StringComparer.OrdinalIgnoreCase);

    // ICommands registered by plugins keyed by auto-generated CommandId (Guid).
    // The host stores only the CommandId; on user click it sends ExecuteCommandRequest
    // with the CommandId back, and we look up and execute the stored ICommand.
    private readonly Dictionary<string, ICommand> _commands =
        new(StringComparer.Ordinal);

    // ─────────────────────────────────────────────────────────────────────────
    public IpcUIRegistry(IpcChannel channel, CancellationToken ct)
    {
        _channel = channel ?? throw new ArgumentNullException(nameof(channel));
        _ct = ct;
    }

    // ── IUIRegistry ───────────────────────────────────────────────────────────

    public string GenerateUIId(string pluginId, string elementType, string elementName)
        => $"{pluginId}.{elementType}.{elementName}";

    public bool Exists(string uiId)
        => _sources.ContainsKey(uiId);

    public void RegisterPanel(string uiId, UIElement panel, string pluginId, PanelDescriptor descriptor)
    {
        if (_sources.ContainsKey(uiId)) return;

        var source = CreateHwndSource(uiId, panel,
            (int)Math.Max(descriptor.PreferredWidth, 100),
            (int)Math.Max(descriptor.PreferredHeight, 100));

        _sources[uiId] = source;

        // Notify host: send HWND via IPC
        _ = _channel.SendAsync(new SandboxEnvelope
        {
            Kind = SandboxMessageKind.RegisterPanelNotification,
            Payload = JsonSerializer.Serialize(new RegisterPanelNotificationPayload
            {
                ContentId = uiId,
                Title = descriptor.Title,
                Hwnd = source.Handle.ToInt64(),
                PanelType = descriptor.DefaultDockSide,
                Width = descriptor.PreferredWidth > 0 ? descriptor.PreferredWidth : 300,
                Height = descriptor.PreferredHeight > 0 ? descriptor.PreferredHeight : 300,
            }),
        }, _ct);
    }

    public void RegisterDocumentTab(string uiId, UIElement content, string pluginId, DocumentDescriptor descriptor)
    {
        if (_sources.ContainsKey(uiId)) return;

        var source = CreateHwndSource(uiId, content, 600, 400);
        _sources[uiId] = source;

        _ = _channel.SendAsync(new SandboxEnvelope
        {
            Kind = SandboxMessageKind.RegisterDocumentTabNotification,
            Payload = JsonSerializer.Serialize(new RegisterDocumentTabNotificationPayload
            {
                ContentId = uiId,
                Title = descriptor.Title,
                Hwnd = source.Handle.ToInt64(),
                Width = 600,
                Height = 400,
            }),
        }, _ct);
    }

    public void UnregisterPanel(string uiId)
    {
        if (!_sources.Remove(uiId, out var src)) return;
        src.Dispose();

        _ = _channel.SendAsync(new SandboxEnvelope
        {
            Kind = SandboxMessageKind.UnregisterPanelNotification,
            Payload = JsonSerializer.Serialize(new UnregisterPanelNotificationPayload
            {
                ContentId = uiId,
            }),
        }, _ct);
    }

    public void UnregisterDocumentTab(string uiId) => UnregisterPanel(uiId);

    // ── Menu Registration ─────────────────────────────────────────────────────

    public void RegisterMenuItem(string uiId, string pluginId, MenuItemDescriptor descriptor)
    {
        // Assign a stable CommandId if the descriptor carries a command
        string? commandId = null;
        if (descriptor.Command is not null)
        {
            commandId = Guid.NewGuid().ToString("N");
            _commands[commandId] = descriptor.Command;
        }

        _ = _channel.SendAsync(new SandboxEnvelope
        {
            Kind = SandboxMessageKind.RegisterMenuItemNotification,
            Payload = JsonSerializer.Serialize(new RegisterMenuItemNotificationPayload
            {
                ContentId = uiId,
                Header = descriptor.Header,
                ParentPath = descriptor.ParentPath,
                Group = descriptor.Group,
                IconGlyph = descriptor.IconGlyph,
                GestureText = descriptor.GestureText,
                ToolTip = descriptor.ToolTip,
                InsertPosition = descriptor.InsertPosition,
                CommandId = commandId,
            }),
        }, _ct);
    }

    public void UnregisterMenuItem(string uiId)
    {
        // Clean up any commands associated with this menu item
        var toRemove = _commands.Keys
            .Where(k => k.StartsWith(uiId, StringComparison.OrdinalIgnoreCase))
            .ToList();
        foreach (var k in toRemove) _commands.Remove(k);

        _ = _channel.SendAsync(new SandboxEnvelope
        {
            Kind = SandboxMessageKind.UnregisterMenuItemNotification,
            Payload = JsonSerializer.Serialize(new UnregisterMenuItemNotificationPayload
            {
                ContentId = uiId,
            }),
        }, _ct);
    }

    // ── Toolbar Registration ──────────────────────────────────────────────────

    public void RegisterToolbarItem(string uiId, string pluginId, ToolbarItemDescriptor descriptor)
    {
        string? commandId = null;
        if (descriptor.Command is not null)
        {
            commandId = Guid.NewGuid().ToString("N");
            _commands[commandId] = descriptor.Command;
        }

        _ = _channel.SendAsync(new SandboxEnvelope
        {
            Kind = SandboxMessageKind.RegisterToolbarItemNotification,
            Payload = JsonSerializer.Serialize(new RegisterToolbarItemNotificationPayload
            {
                ContentId = uiId,
                IconGlyph = descriptor.IconGlyph,
                ToolTip = descriptor.ToolTip,
                IsSeparator = descriptor.IsSeparator,
                Group = descriptor.Group,
                CommandId = commandId,
            }),
        }, _ct);
    }

    public void UnregisterToolbarItem(string uiId)
    {
        _ = _channel.SendAsync(new SandboxEnvelope
        {
            Kind = SandboxMessageKind.UnregisterToolbarItemNotification,
            Payload = JsonSerializer.Serialize(new UnregisterToolbarItemNotificationPayload
            {
                ContentId = uiId,
            }),
        }, _ct);
    }

    // ── Status Bar Registration ───────────────────────────────────────────────

    public void RegisterStatusBarItem(string uiId, string pluginId, StatusBarItemDescriptor descriptor)
    {
        _ = _channel.SendAsync(new SandboxEnvelope
        {
            Kind = SandboxMessageKind.RegisterStatusBarItemNotification,
            Payload = JsonSerializer.Serialize(new RegisterStatusBarItemNotificationPayload
            {
                ContentId = uiId,
                Text = descriptor.Text,
                Alignment = descriptor.Alignment.ToString(),
                ToolTip = descriptor.ToolTip,
                Order = descriptor.Order,
            }),
        }, _ct);
    }

    public void UnregisterStatusBarItem(string uiId)
    {
        _ = _channel.SendAsync(new SandboxEnvelope
        {
            Kind = SandboxMessageKind.UnregisterStatusBarItemNotification,
            Payload = JsonSerializer.Serialize(new UnregisterStatusBarItemNotificationPayload
            {
                ContentId = uiId,
            }),
        }, _ct);
    }

    // ── Panel Visibility — forwarded to host via IPC ──────────────────────────
    // The panel's HWND is embedded in the host's docking layout; visibility
    // must be managed by the host docking system, not by Win32 ShowWindow.

    public void ShowPanel(string uiId) => SendPanelAction(uiId, "Show");

    public void HidePanel(string uiId) => SendPanelAction(uiId, "Hide");

    public void TogglePanel(string uiId) => SendPanelAction(uiId, "Toggle");

    public void FocusPanel(string uiId) => SendPanelAction(uiId, "Focus");

    public bool IsPanelVisible(string uiId)
    {
        // Cannot reliably query host docking visibility from the sandbox.
        // Return true (fail-open) so plugins do not suppress work unnecessarily.
        return true;
    }

    // Sandbox plugins cannot receive dock events — stubs for interface compliance.
    public event EventHandler<string>? PanelShown;
    public event EventHandler<string>? PanelHidden;

    private void SendPanelAction(string contentId, string action)
    {
        _ = _channel.SendAsync(new SandboxEnvelope
        {
            Kind = SandboxMessageKind.PanelActionNotification,
            Payload = JsonSerializer.Serialize(new PanelActionNotificationPayload
            {
                ContentId = contentId,
                Action = action,
            }),
        }, _ct);
    }

    // ── Command execution (called by SandboxedPluginRunner) ───────────────────

    /// <summary>
    /// Executes the stored <see cref="ICommand"/> identified by <paramref name="commandId"/>.
    /// Called when the host sends an <see cref="SandboxMessageKind.ExecuteCommandRequest"/>.
    /// Must be called on the WPF STA Dispatcher thread.
    /// </summary>
    public void ExecuteCommand(string commandId)
    {
        if (!_commands.TryGetValue(commandId, out var cmd)) return;
        if (cmd.CanExecute(null))
            cmd.Execute(null);
    }

    // ── Solution Explorer Context Menu Contributors ───────────────────────────
    // Sandbox plugins run out-of-process and cannot inject WPF controls into
    // the host's Solution Explorer. Contributors are registered locally but have
    // no effect on the host-side context menu. In-process plugins (ClassDiagram)
    // use the in-process UIRegistry which does support this fully.

    public void RegisterContextMenuContributor(string pluginId, ISolutionExplorerContextMenuContributor contributor)
    { /* No-op in sandbox: context menu injection is in-process only */ }

    public void UnregisterContextMenuContributor(string pluginId)
    { /* No-op in sandbox */ }

    public IReadOnlyList<ISolutionExplorerContextMenuContributor> GetContextMenuContributors()
        => [];

    public void RegisterTitleBarItem(string uiId, string pluginId, ITitleBarContributor contributor)
    { /* No-op in sandbox: title bar injection is in-process only */ }

    public void UnregisterTitleBarItem(string uiId)
    { /* No-op in sandbox */ }

    // ── Bulk unregister ───────────────────────────────────────────────────────

    public void UnregisterAllForPlugin(string pluginId)
    {
        var toRemove = _sources.Keys
            .Where(k => k.StartsWith(pluginId + ".", StringComparison.OrdinalIgnoreCase))
            .ToList();
        foreach (var key in toRemove)
            UnregisterPanel(key);
    }

    // ── Resize ────────────────────────────────────────────────────────────────

    /// <summary>
    /// Called when the host sends a <see cref="SandboxMessageKind.ResizePanelRequest"/>.
    /// Updates the HwndSource window size so WPF layout recalculates.
    /// </summary>
    public void HandleResize(string contentId, double width, double height)
    {
        if (!_sources.TryGetValue(contentId, out var src)) return;

        SetWindowPos(src.Handle, IntPtr.Zero,
            0, 0, (int)Math.Max(width, 1), (int)Math.Max(height, 1),
            SWP_NOZORDER | SWP_NOMOVE);
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static HwndSource CreateHwndSource(string name, UIElement content, int w, int h)
    {
        // WS_POPUP (default when no WS_CHILD): creates a top-level window with no chrome.
        // The host will reparent it via SetParent and adjust styles to WS_CHILD.
        var parameters = new HwndSourceParameters($"SandboxPanel_{name}")
        {
            WindowStyle = WS_POPUP,
            ExtendedWindowStyle = 0,
            PositionX = 0,
            PositionY = 0,
            Width = w,
            Height = h,
        };
        return new HwndSource(parameters) { RootVisual = content };
    }

    // ── Dispose ───────────────────────────────────────────────────────────────

    public void DisposeAll()
    {
        _commands.Clear();
        foreach (var src in _sources.Values)
            src.Dispose();
        _sources.Clear();
    }

    // ── Win32 ─────────────────────────────────────────────────────────────────

    private const int WS_POPUP = unchecked((int)0x80000000);
    private const uint SWP_NOZORDER = 0x0004;
    private const uint SWP_NOMOVE = 0x0002;

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter,
        int X, int Y, int cx, int cy, uint uFlags);
}
