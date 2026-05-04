// ==========================================================
// Project: WpfHexEditor.Core.Debugger
// File: Services/IDapClient.cs
// Description:
//     Debug Adapter Protocol client contract.
//     Implemented by DapClientBase (via NetCoreDapAdapter).
// ==========================================================

using WpfHexEditor.Core.Debugger.Protocol;

namespace WpfHexEditor.Core.Debugger.Services;

/// <summary>
/// Contract for a DAP client that communicates with a debug adapter process
/// (e.g. netcoredbg) over stdin/stdout using JSON-RPC.
/// </summary>
public interface IDapClient : IAsyncDisposable
{
    // ── Lifecycle ─────────────────────────────────────────────────────────────

    /// <summary>Send initialize + configurationDone to the adapter.</summary>
    Task<CapabilitiesBody?> InitializeAsync(InitializeRequestArgs args, CancellationToken ct = default);

    /// <summary>Launch the debuggee process.</summary>
    Task LaunchAsync(LaunchRequestArgs args, CancellationToken ct = default);

    /// <summary>Attach to an already-running process.</summary>
    Task AttachAsync(AttachRequestArgs args, CancellationToken ct = default);

    /// <summary>Send configurationDone (after SetBreakpoints).</summary>
    Task ConfigurationDoneAsync(CancellationToken ct = default);

    /// <summary>Disconnect from the adapter (optionally terminate debuggee).</summary>
    Task DisconnectAsync(DisconnectArgs? args = null, CancellationToken ct = default);

    // ── Breakpoints ───────────────────────────────────────────────────────────

    /// <summary>Set breakpoints for one source file (replaces previous list for that file).</summary>
    Task<SetBreakpointsBody?> SetBreakpointsAsync(SetBreakpointsArgs args, CancellationToken ct = default);

    // ── Execution control ─────────────────────────────────────────────────────

    /// <summary>Resume execution on all threads.</summary>
    Task ContinueAsync(ContinueArgs args, CancellationToken ct = default);

    /// <summary>Step over (next) on the given thread.</summary>
    Task NextAsync(StepArgs args, CancellationToken ct = default);

    /// <summary>Step into on the given thread.</summary>
    Task StepInAsync(StepArgs args, CancellationToken ct = default);

    /// <summary>Step out on the given thread.</summary>
    Task StepOutAsync(StepArgs args, CancellationToken ct = default);

    /// <summary>Pause execution on the given thread.</summary>
    Task PauseAsync(PauseArgs args, CancellationToken ct = default);

    // ── Inspection ────────────────────────────────────────────────────────────

    /// <summary>Get all active threads.</summary>
    Task<ThreadsBody?> ThreadsAsync(CancellationToken ct = default);

    /// <summary>Get the call stack for a given thread.</summary>
    Task<StackTraceBody?> StackTraceAsync(StackTraceArgs args, CancellationToken ct = default);

    /// <summary>Get variable scopes for a given frame.</summary>
    Task<ScopesBody?> ScopesAsync(ScopesArgs args, CancellationToken ct = default);

    /// <summary>Get variables for a given scope or structured variable.</summary>
    Task<VariablesBody?> VariablesAsync(VariablesArgs args, CancellationToken ct = default);

    /// <summary>Evaluate an expression in the context of a frame.</summary>
    Task<EvaluateBody?> EvaluateAsync(EvaluateArgs args, CancellationToken ct = default);

    /// <summary>Set the value of a named variable in a given scope.</summary>
    Task<SetVariableBody?> SetVariableAsync(SetVariableArgs args, CancellationToken ct = default);

    /// <summary>Get goto targets for a given source line (supports Run to Cursor).</summary>
    Task<GotoTargetsBody?> GotoTargetsAsync(GotoTargetsArgs args, CancellationToken ct = default);

    /// <summary>Move execution to a specific goto target.</summary>
    Task GotoAsync(GotoArgs args, CancellationToken ct = default);

    /// <summary>Configure which exception filters trigger a stop.</summary>
    Task SetExceptionBreakpointsAsync(SetExceptionBreakpointsArgs args, CancellationToken ct = default);

    /// <summary>Retrieve loaded modules (DLLs / EXEs). Returns empty list if adapter lacks support.</summary>
    Task<ModulesBody> GetModulesAsync(ModulesArgs? args = null, CancellationToken ct = default);

    /// <summary>Disassemble memory around a given address. Returns empty if not supported.</summary>
    Task<DisassembleBody> DisassembleAsync(DisassembleArgs args, CancellationToken ct = default);

    /// <summary>Read raw memory bytes from the debuggee. Returns null body if not supported.</summary>
    Task<ReadMemoryBody?> ReadMemoryAsync(ReadMemoryArgs args, CancellationToken ct = default);

    /// <summary>Write raw memory bytes to the debuggee. No-op if not supported.</summary>
    Task WriteMemoryAsync(WriteMemoryArgs args, CancellationToken ct = default);

    /// <summary>
    /// Restart the execution of the given stack frame (Edit &amp; Continue).
    /// No-op if the adapter does not support restartFrame.
    /// </summary>
    Task RestartFrameAsync(RestartFrameArgs args, CancellationToken ct = default);

    /// <summary>
    /// Get data breakpoint info for a variable (needed before calling SetDataBreakpointsAsync).
    /// Returns null body if not supported.
    /// </summary>
    Task<DataBreakpointInfoBody?> DataBreakpointInfoAsync(DataBreakpointInfoArgs args, CancellationToken ct = default);

    /// <summary>
    /// Set the list of data breakpoints (memory watchpoints).
    /// Returns null body if not supported.
    /// </summary>
    Task<SetDataBreakpointsBody?> SetDataBreakpointsAsync(SetDataBreakpointsArgs args, CancellationToken ct = default);

    // ── Inbound events ────────────────────────────────────────────────────────

    /// <summary>Raised when the adapter sends a "stopped" event (breakpoint/step/exception).</summary>
    event EventHandler<StoppedEventBody>? Stopped;

    /// <summary>Raised when the adapter sends an "output" event.</summary>
    event EventHandler<OutputEventBody>? Output;

    /// <summary>Raised when the adapter sends an "exited" event.</summary>
    event EventHandler<ExitedEventBody>? Exited;

    /// <summary>Raised when the adapter sends a "terminated" event.</summary>
    event EventHandler? Terminated;
}
