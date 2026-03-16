//////////////////////////////////////////////
// GNU Affero General Public License v3.0 - 2026
// Author : Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.6
//////////////////////////////////////////////

// ==========================================================
// Project: WpfHexEditor.PluginHost
// File: Sandbox/IpcRelayCommand.cs
// Created: 2026-03-15
// Description:
//     ICommand implementation for host-side menu items and toolbar buttons
//     registered by sandbox plugins. On Execute, sends an
//     ExecuteCommandRequest IPC message to the sandbox so it can invoke the
//     plugin's stored ICommand by CommandId.
//
// Architecture Notes:
//     - Pattern: Proxy Command — the "real" ICommand lives in the sandbox;
//       this is a fire-and-forget IPC proxy on the host side.
//     - CanExecuteChanged is never raised (always enabled). Plugins that need
//       dynamic enable/disable must manage it through future IPC extensions.
// ==========================================================

using System.Windows.Input;
using WpfHexEditor.SDK.Sandbox;

namespace WpfHexEditor.PluginHost.Sandbox;

/// <summary>
/// Host-side <see cref="ICommand"/> that forwards user activations to a sandboxed
/// plugin command via <see cref="SandboxMessageKind.ExecuteCommandRequest"/> IPC.
/// </summary>
internal sealed class IpcRelayCommand : ICommand
{
    private readonly string _commandId;
    private readonly SandboxProcessManager _procManager;

    // ─────────────────────────────────────────────────────────────────────────
    public IpcRelayCommand(string commandId, SandboxProcessManager procManager)
    {
        _commandId = commandId ?? throw new ArgumentNullException(nameof(commandId));
        _procManager = procManager ?? throw new ArgumentNullException(nameof(procManager));
    }

    // ── ICommand ──────────────────────────────────────────────────────────────

    // Sandbox commands are always considered enabled from the host's perspective.
    // Future phases may add a CanExecuteChanged IPC notification.
    public bool CanExecute(object? parameter) => true;

    public void Execute(object? parameter)
    {
        var req = SandboxProcessManager.BuildRequest(
            SandboxMessageKind.ExecuteCommandRequest,
            new ExecuteCommandRequestPayload { CommandId = _commandId });

        // Fire-and-forget: the sandbox executes the command asynchronously.
        _ = _procManager.SendAsync(req);
    }

    // CanExecuteChanged is not supported — always enabled.
    public event EventHandler? CanExecuteChanged
    {
        add    { }
        remove { }
    }
}
