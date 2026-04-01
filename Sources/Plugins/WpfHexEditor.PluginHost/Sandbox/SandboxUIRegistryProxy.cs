//////////////////////////////////////////////
// GNU Affero General Public License v3.0 - 2026
// Author : Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.6
//////////////////////////////////////////////

// ==========================================================
// Project: WpfHexEditor.PluginHost
// File: Sandbox/SandboxUIRegistryProxy.cs
// Created: 2026-03-15
// Description:
//     Host-side bridge that receives UI registration notifications from the
//     sandbox and maps them to real IUIRegistry calls.
//
//     Phase 9: Panel / DocumentTab HWND embedding via HwndPanelHost.
//     Phase 10: Menu items, toolbar items, status bar items, and panel
//     visibility actions (Show/Hide/Toggle/Focus) are all forwarded from the
//     sandbox IPC to the real IDE IUIRegistry on the WPF Dispatcher thread.
//     Menu and toolbar items backed by an IpcRelayCommand that sends
//     ExecuteCommandRequest to the sandbox on user activation.
//
// Architecture Notes:
//     - Pattern: Proxy + Adapter — translates IPC payloads into IUIRegistry calls.
//     - All IUIRegistry calls are marshalled to the WPF Dispatcher thread.
//     - HwndPanelHost is the WPF UIElement passed to IUIRegistry.RegisterPanel;
//       it internally manages the Win32 HWND lifecycle.
//     - On SandboxHwndLost (process crash) the panel is unregistered and the
//       host is disposed — the parent PluginCrashed event handles restart.
//     - Theme forwarding: when the IDE theme changes, sends ThemeChangedNotification
//       via IPC so the sandbox can re-apply theme resources.
// ==========================================================

using System.Windows;
using System.Windows.Threading;
using WpfHexEditor.SDK.Contracts;
using WpfHexEditor.SDK.Descriptors;
using WpfHexEditor.SDK.Sandbox;

namespace WpfHexEditor.PluginHost.Sandbox;

/// <summary>
/// Receives panel/tab/menu/toolbar/statusbar registration notifications from the
/// sandbox and forwards them to the IDE's <see cref="IUIRegistry"/>.
/// </summary>
internal sealed class SandboxUIRegistryProxy : IDisposable
{
    private readonly IUIRegistry _registry;
    private readonly SandboxProcessManager _procManager;
    private readonly string _pluginId;
    private readonly Dispatcher _dispatcher;
    private readonly Action<string> _log;

    // Registered HwndPanelHost instances keyed by contentId
    private readonly Dictionary<string, HwndPanelHost> _hosts =
        new(StringComparer.OrdinalIgnoreCase);

    // Phase 11 — options page (set once during init, before ReadyNotification)
    private RegisterOptionsPageNotificationPayload? _optionsPageInfo;

    private bool _disposed;

    // ─────────────────────────────────────────────────────────────────────────
    public SandboxUIRegistryProxy(
        IUIRegistry registry,
        SandboxProcessManager procManager,
        string pluginId,
        Dispatcher dispatcher,
        Action<string>? logger = null)
    {
        _registry = registry ?? throw new ArgumentNullException(nameof(registry));
        _procManager = procManager ?? throw new ArgumentNullException(nameof(procManager));
        _pluginId = pluginId;
        _dispatcher = dispatcher ?? throw new ArgumentNullException(nameof(dispatcher));
        _log = logger ?? (_ => { });

        // Phase 9 — panel HWND events
        _procManager.PanelRegistered += OnPanelRegistered;
        _procManager.DocumentTabRegistered += OnDocumentTabRegistered;
        _procManager.PanelUnregistered += OnPanelUnregistered;

        // Phase 10 — menu / toolbar / status-bar events
        _procManager.MenuItemRegistered += OnMenuItemRegistered;
        _procManager.MenuItemUnregistered += OnMenuItemUnregistered;
        _procManager.ToolbarItemRegistered += OnToolbarItemRegistered;
        _procManager.ToolbarItemUnregistered += OnToolbarItemUnregistered;
        _procManager.StatusBarItemRegistered += OnStatusBarItemRegistered;
        _procManager.StatusBarItemUnregistered += OnStatusBarItemUnregistered;

        // Phase 10 — panel visibility forwarding
        _procManager.PanelActionReceived += OnPanelActionReceived;

        // Phase 11 — options page declaration
        _procManager.OptionsPageDeclared += OnOptionsPageDeclared;
    }

    /// <summary>
    /// Returns the options page declaration received from the sandbox, or null if the
    /// plugin does not implement IPluginWithOptions or the notification has not arrived yet.
    /// Safe to call after <see cref="SandboxPluginProxy.InitializeAsync"/> completes because
    /// the notification is sent before ReadyNotification (which unblocks InitializeAsync).
    /// </summary>
    internal RegisterOptionsPageNotificationPayload? OptionsPageInfo => _optionsPageInfo;

    // ── IPC event handlers — Phase 9 ─────────────────────────────────────────

    private void OnPanelRegistered(object? sender, RegisterPanelNotificationPayload e)
        => _dispatcher.InvokeAsync(() => AttachPanel(e));

    private void OnDocumentTabRegistered(object? sender, RegisterDocumentTabNotificationPayload e)
        => _dispatcher.InvokeAsync(() => AttachDocumentTab(e));

    private void OnPanelUnregistered(object? sender, UnregisterPanelNotificationPayload e)
        => _dispatcher.InvokeAsync(() => DetachPanel(e.ContentId));

    // ── IPC event handlers — Phase 10 ────────────────────────────────────────

    private void OnMenuItemRegistered(object? sender, RegisterMenuItemNotificationPayload e)
        => _dispatcher.InvokeAsync(() => AttachMenuItem(e));

    private void OnMenuItemUnregistered(object? sender, UnregisterMenuItemNotificationPayload e)
        => _dispatcher.InvokeAsync(() =>
        {
            _registry.UnregisterMenuItem(e.ContentId);
            _log($"[SandboxUIProxy:{_pluginId}] MenuItem '{e.ContentId}' unregistered.");
        });

    private void OnToolbarItemRegistered(object? sender, RegisterToolbarItemNotificationPayload e)
        => _dispatcher.InvokeAsync(() => AttachToolbarItem(e));

    private void OnToolbarItemUnregistered(object? sender, UnregisterToolbarItemNotificationPayload e)
        => _dispatcher.InvokeAsync(() =>
        {
            _registry.UnregisterToolbarItem(e.ContentId);
            _log($"[SandboxUIProxy:{_pluginId}] ToolbarItem '{e.ContentId}' unregistered.");
        });

    private void OnStatusBarItemRegistered(object? sender, RegisterStatusBarItemNotificationPayload e)
        => _dispatcher.InvokeAsync(() => AttachStatusBarItem(e));

    private void OnStatusBarItemUnregistered(object? sender, UnregisterStatusBarItemNotificationPayload e)
        => _dispatcher.InvokeAsync(() =>
        {
            _registry.UnregisterStatusBarItem(e.ContentId);
            _log($"[SandboxUIProxy:{_pluginId}] StatusBarItem '{e.ContentId}' unregistered.");
        });

    private void OnPanelActionReceived(object? sender, PanelActionNotificationPayload e)
        => _dispatcher.InvokeAsync(() => DispatchPanelAction(e));

    // Phase 11 — options page declaration (store without Dispatcher; no WPF object created)
    private void OnOptionsPageDeclared(object? sender, RegisterOptionsPageNotificationPayload e)
        => _optionsPageInfo = e;

    // ── Attach / Detach — Panels ──────────────────────────────────────────────

    private void AttachPanel(RegisterPanelNotificationPayload payload)
    {
        if (_hosts.ContainsKey(payload.ContentId)) return;

        var hwnd = new IntPtr(payload.Hwnd);
        var host = new HwndPanelHost(hwnd, payload.ContentId);
        host.SandboxHwndLost += OnHwndLost;

        _hosts[payload.ContentId] = host;

        var descriptor = new PanelDescriptor
        {
            Title = payload.Title,
            DefaultDockSide = payload.PanelType,
            PreferredWidth = payload.Width,
            PreferredHeight = payload.Height,
            CanClose = true,
        };

        _registry.RegisterPanel(payload.ContentId, host, _pluginId, descriptor);
        _log($"[SandboxUIProxy:{_pluginId}] Panel '{payload.ContentId}' registered (HWND=0x{payload.Hwnd:X}).");
    }

    private void AttachDocumentTab(RegisterDocumentTabNotificationPayload payload)
    {
        if (_hosts.ContainsKey(payload.ContentId)) return;

        var hwnd = new IntPtr(payload.Hwnd);
        var host = new HwndPanelHost(hwnd, payload.ContentId);
        host.SandboxHwndLost += OnHwndLost;

        _hosts[payload.ContentId] = host;

        var descriptor = new DocumentDescriptor
        {
            Title = payload.Title,
            CanClose = true,
        };

        _registry.RegisterDocumentTab(payload.ContentId, host, _pluginId, descriptor);
        _log($"[SandboxUIProxy:{_pluginId}] DocumentTab '{payload.ContentId}' registered (HWND=0x{payload.Hwnd:X}).");
    }

    private void DetachPanel(string contentId)
    {
        if (!_hosts.Remove(contentId, out var host)) return;

        host.SandboxHwndLost -= OnHwndLost;
        _registry.UnregisterPanel(contentId);
        host.Dispose();

        _log($"[SandboxUIProxy:{_pluginId}] Panel '{contentId}' unregistered.");
    }

    // ── Attach — Menu Items ───────────────────────────────────────────────────

    private void AttachMenuItem(RegisterMenuItemNotificationPayload payload)
    {
        // Build a host-side ICommand that relays execution to the sandbox
        var command = payload.CommandId is not null
            ? new IpcRelayCommand(payload.CommandId, _procManager)
            : (System.Windows.Input.ICommand?)null;

        var descriptor = new MenuItemDescriptor
        {
            Header = payload.Header,
            ParentPath = payload.ParentPath,
            Group = payload.Group,
            IconGlyph = payload.IconGlyph,
            GestureText = payload.GestureText,
            ToolTip = payload.ToolTip,
            InsertPosition = payload.InsertPosition,
            Command = command,
        };

        _registry.RegisterMenuItem(payload.ContentId, _pluginId, descriptor);
        _log($"[SandboxUIProxy:{_pluginId}] MenuItem '{payload.ContentId}' registered (cmd={payload.CommandId ?? "none"}).");
    }

    // ── Attach — Toolbar Items ────────────────────────────────────────────────

    private void AttachToolbarItem(RegisterToolbarItemNotificationPayload payload)
    {
        var command = payload.CommandId is not null
            ? new IpcRelayCommand(payload.CommandId, _procManager)
            : (System.Windows.Input.ICommand?)null;

        var descriptor = new ToolbarItemDescriptor
        {
            IconGlyph = payload.IconGlyph,
            ToolTip = payload.ToolTip,
            IsSeparator = payload.IsSeparator,
            Group = payload.Group,
            Command = command,
        };

        _registry.RegisterToolbarItem(payload.ContentId, _pluginId, descriptor);
        _log($"[SandboxUIProxy:{_pluginId}] ToolbarItem '{payload.ContentId}' registered.");
    }

    // ── Attach — Status Bar Items ─────────────────────────────────────────────

    private void AttachStatusBarItem(RegisterStatusBarItemNotificationPayload payload)
    {
        var alignment = Enum.TryParse<StatusBarAlignment>(payload.Alignment, out var a)
            ? a
            : StatusBarAlignment.Right;

        var descriptor = new StatusBarItemDescriptor
        {
            Text = payload.Text,
            Alignment = alignment,
            ToolTip = payload.ToolTip,
            Order = payload.Order,
        };

        _registry.RegisterStatusBarItem(payload.ContentId, _pluginId, descriptor);
        _log($"[SandboxUIProxy:{_pluginId}] StatusBarItem '{payload.ContentId}' registered.");
    }

    // ── Panel Action Forwarding ───────────────────────────────────────────────

    private void DispatchPanelAction(PanelActionNotificationPayload payload)
    {
        switch (payload.Action)
        {
            case "Show":   _registry.ShowPanel(payload.ContentId);   break;
            case "Hide":   _registry.HidePanel(payload.ContentId);   break;
            case "Toggle": _registry.TogglePanel(payload.ContentId); break;
            case "Focus":  _registry.FocusPanel(payload.ContentId);  break;
            default:
                _log($"[SandboxUIProxy:{_pluginId}] Unknown panel action '{payload.Action}'.");
                break;
        }
    }

    // ── Resize forwarding ─────────────────────────────────────────────────────

    /// <summary>
    /// Forwards a resize request from the IDE (e.g. user resizes docking panel)
    /// to the sandbox via IPC so the sandbox HwndSource matches.
    /// </summary>
    public Task ForwardResizeAsync(string contentId, double width, double height,
        CancellationToken ct = default)
    {
        var req = SandboxProcessManager.BuildRequest(
            SandboxMessageKind.ResizePanelRequest,
            new ResizePanelRequestPayload { ContentId = contentId, Width = width, Height = height });

        return _procManager.SendAsync(req, ct);
    }

    // ── Theme change forwarding ───────────────────────────────────────────────

    /// <summary>
    /// Sends the new serialized theme XAML to the sandbox so it re-applies theme resources.
    /// </summary>
    public Task ForwardThemeChangeAsync(string themeXaml, CancellationToken ct = default)
    {
        var envelope = SandboxProcessManager.BuildRequest(
            SandboxMessageKind.ThemeChangedNotification,
            new ThemeChangedNotificationPayload { ThemeResourcesXaml = themeXaml });

        return _procManager.SendAsync(envelope, ct);
    }

    // ── Error handling ────────────────────────────────────────────────────────

    private void OnHwndLost(object? sender, string contentId)
    {
        _dispatcher.InvokeAsync(() =>
        {
            _log($"[SandboxUIProxy:{_pluginId}] HWND lost for '{contentId}' — removing panel.");
            DetachPanel(contentId);
        });
    }

    // ── Dispose ───────────────────────────────────────────────────────────────

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        // Phase 9
        _procManager.PanelRegistered -= OnPanelRegistered;
        _procManager.DocumentTabRegistered -= OnDocumentTabRegistered;
        _procManager.PanelUnregistered -= OnPanelUnregistered;

        // Phase 10 — menu / toolbar / status-bar
        _procManager.MenuItemRegistered -= OnMenuItemRegistered;
        _procManager.MenuItemUnregistered -= OnMenuItemUnregistered;
        _procManager.ToolbarItemRegistered -= OnToolbarItemRegistered;
        _procManager.ToolbarItemUnregistered -= OnToolbarItemUnregistered;
        _procManager.StatusBarItemRegistered -= OnStatusBarItemRegistered;
        _procManager.StatusBarItemUnregistered -= OnStatusBarItemUnregistered;

        // Phase 10 — panel action
        _procManager.PanelActionReceived -= OnPanelActionReceived;

        // Phase 11 — options page
        _procManager.OptionsPageDeclared -= OnOptionsPageDeclared;

        _dispatcher.InvokeAsync(() =>
        {
            foreach (var (contentId, host) in _hosts)
            {
                host.SandboxHwndLost -= OnHwndLost;
                _registry.UnregisterPanel(contentId);
                host.Dispose();
            }
            _hosts.Clear();
        });
    }
}
