// ==========================================================
// Project: WpfHexEditor.SDK
// File: Contracts/Services/IDebuggerService.cs
// Description:
//     SDK contract for the IDE debugger service.
//     Plugins use this to query session state and react to debug events.
//     DebuggerServiceImpl (App layer) is the concrete implementation.
// Architecture:
//     Depends on WpfHexEditor.Core.Debugger models (via transitive ref).
//     No WPF types exposed here — pure contract.
// ==========================================================

namespace WpfHexEditor.SDK.Contracts.Services;

/// <summary>
/// Breakpoint entry visible to plugins (read-only snapshot).
/// </summary>
public sealed record DebugBreakpointInfo(
    string  FilePath,
    int     Line,
    string? Condition,
    bool    IsEnabled,
    bool    IsVerified,
    int     HitCount = 0,
    // Extended settings (mirrors BreakpointLocation)
    WpfHexEditor.Core.Debugger.Models.BpConditionKind ConditionKind  = WpfHexEditor.Core.Debugger.Models.BpConditionKind.None,
    WpfHexEditor.Core.Debugger.Models.BpConditionMode ConditionMode  = WpfHexEditor.Core.Debugger.Models.BpConditionMode.IsTrue,
    WpfHexEditor.Core.Debugger.Models.BpHitCountOp    HitCountOp     = WpfHexEditor.Core.Debugger.Models.BpHitCountOp.Equal,
    int     HitCountTarget    = 1,
    string? FilterExpr        = null,
    bool    HasAction         = false,
    string? LogMessage        = null,
    bool    ContinueExecution = true,
    bool    DisableOnceHit    = false,
    string? DependsOnBpKey    = null
);

/// <summary>
/// Stack frame snapshot visible to plugins.
/// </summary>
public sealed record DebugFrameInfo(
    int     Id,
    string  Name,
    string? FilePath,
    int     Line,
    int     Column,
    string? InstructionPointerReference = null
);

/// <summary>
/// Thread snapshot visible to plugins.
/// </summary>
public sealed record DebugThreadInfo(
    int    Id,
    string Name
);

/// <summary>
/// Exception filter configuration for the Exception Settings panel.
/// </summary>
public sealed record ExceptionFilterInfo(
    string  Filter,
    string  Label,
    bool    IsEnabled,
    string? Condition = null
);

/// <summary>
/// A data breakpoint (memory watchpoint) visible to plugins.
/// </summary>
public sealed record DataBreakpointInfo(
    string  DataId,
    string  Description,
    string? AccessType   = null,
    string? Condition    = null,
    string? HitCondition = null
);

/// <summary>
/// Variable snapshot visible to plugins.
/// </summary>
public sealed record DebugVariableInfo(
    string  Name,
    string  Value,
    string? Type,
    int     VariablesReference
);

/// <summary>
/// A single disassembled machine instruction visible to plugins.
/// </summary>
public sealed record DisassembledInstruction(
    string  Address,
    string  Instruction,
    string? Symbol           = null,
    string? InstructionBytes = null,
    string? SourceFile       = null,
    int     SourceLine       = 0
);

/// <summary>
/// Loaded module (DLL/EXE) snapshot visible to plugins.
/// </summary>
public sealed record DebugModuleInfo(
    string  Name,
    string? Path,
    string? Version,
    string? SymbolStatus,
    bool    IsOptimized,
    bool    IsUserCode
);

/// <summary>
/// IDE debugger lifecycle state (mirrors Core.Debugger.DebugSessionState).
/// Duplicated to avoid SDK depending on Core.Debugger directly.
/// </summary>
public enum DebugState { Idle, Launching, Running, Paused, Stopped }

/// <summary>
/// SDK contract for the IDE integrated debugger.
/// Exposed via <see cref="IIDEHostContext.Debugger"/>.
/// </summary>
public interface IDebuggerService
{
    // ── Session ────────────────────────────────────────────────────────────

    /// <summary>Current session state.</summary>
    DebugState State { get; }

    /// <summary>True when a session is active (Running or Paused).</summary>
    bool IsActive { get; }

    /// <summary>True when the session is paused.</summary>
    bool IsPaused { get; }

    /// <summary>File path where execution is currently paused (null when running).</summary>
    string? PausedFilePath { get; }

    /// <summary>1-based line number where execution is paused (0 when running).</summary>
    int PausedLine { get; }

    /// <summary>Raised whenever session state changes.</summary>
    event EventHandler? SessionChanged;

    /// <summary>Launch a new debug session using the given configuration.</summary>
    Task LaunchAsync(WpfHexEditor.Core.Debugger.Models.DebugLaunchConfig config);

    /// <summary>
    /// Attach to a remote debug adapter over TCP or SSH tunnel.
    /// Creates a <c>TcpDapClient</c> or <c>SshTunnelDapClient</c> per
    /// <see cref="WpfHexEditor.Core.Debugger.Models.RemoteDebugConfig.Transport"/>
    /// and runs the DAP initialize/attach handshake.
    /// </summary>
    Task LaunchRemoteAsync(WpfHexEditor.Core.Debugger.Models.RemoteDebugConfig config, CancellationToken ct = default)
        => Task.CompletedTask;

    /// <summary>Stop the active debug session. No-op when idle.</summary>
    Task StopSessionAsync();

    // ── Breakpoints ────────────────────────────────────────────────────────

    /// <summary>All currently registered breakpoints.</summary>
    IReadOnlyList<DebugBreakpointInfo> Breakpoints { get; }

    /// <summary>Raised when the breakpoint list changes (add/remove/verify).</summary>
    event EventHandler? BreakpointsChanged;

    /// <summary>Toggle a breakpoint at the given file/line. Returns the new state.</summary>
    Task<bool> ToggleBreakpointAsync(string filePath, int line, string? condition = null);

    /// <summary>
    /// Update an existing breakpoint's condition and/or enabled state.
    /// No-op when no breakpoint exists at that location.
    /// </summary>
    Task UpdateBreakpointAsync(string filePath, int line, string? condition, bool isEnabled);

    /// <summary>
    /// Apply full breakpoint settings (condition, action, options) from the
    /// <c>BreakpointConditionDialog</c>.  No-op when no breakpoint exists at that location.
    /// </summary>
    Task UpdateBreakpointSettingsAsync(string filePath, int line,
        WpfHexEditor.Core.Debugger.Models.BreakpointSettings settings);

    /// <summary>
    /// Explicitly delete a breakpoint. No-op when no breakpoint exists at that location.
    /// </summary>
    Task DeleteBreakpointAsync(string filePath, int line);

    /// <summary>Remove all breakpoints.</summary>
    Task ClearAllBreakpointsAsync();

    /// <summary>Get the hit count for a breakpoint in the current session (0 if not hit or no session).</summary>
    int GetHitCount(string filePath, int line) => 0;

    /// <summary>Get all breakpoints for a specific file.</summary>
    IReadOnlyList<DebugBreakpointInfo> GetBreakpointsForFile(string filePath) =>
        Breakpoints.Where(b => string.Equals(b.FilePath, filePath, StringComparison.OrdinalIgnoreCase)).ToList();

    // ── Execution ──────────────────────────────────────────────────────────

    /// <summary>Continue execution (resume after pause).</summary>
    Task ContinueAsync();

    /// <summary>Step over the current line.</summary>
    Task StepOverAsync();

    /// <summary>Step into a method call on the current line.</summary>
    Task StepIntoAsync();

    /// <summary>Step out of the current method.</summary>
    Task StepOutAsync();

    /// <summary>Pause execution (break all). No-op when already paused or idle.</summary>
    Task PauseAsync();

    // ── Inspection ─────────────────────────────────────────────────────────

    /// <summary>Get all active threads (only valid when paused).</summary>
    Task<IReadOnlyList<DebugThreadInfo>> GetThreadsAsync();

    /// <summary>Get the call stack frames for a specific thread (0 = active thread).</summary>
    Task<IReadOnlyList<DebugFrameInfo>> GetCallStackForThreadAsync(int threadId);

    /// <summary>Get the current call stack frames (only valid when paused).</summary>
    Task<IReadOnlyList<DebugFrameInfo>> GetCallStackAsync();

    /// <summary>Get variables in a scope by variablesReference (0 = locals).</summary>
    Task<IReadOnlyList<DebugVariableInfo>> GetVariablesAsync(int variablesReference);

    /// <summary>
    /// Get the register variables for the current frame.
    /// Returns an empty list if the adapter does not expose a Registers scope.
    /// </summary>
    Task<IReadOnlyList<DebugVariableInfo>> GetRegistersAsync();

    /// <summary>Evaluate an expression in the current frame context.</summary>
    Task<string> EvaluateAsync(string expression, int? frameId = null);

    /// <summary>
    /// Set a variable's value in the given scope (variablesReference).
    /// Returns the new value string, or null if the adapter does not support setVariable.
    /// </summary>
    Task<string?> SetVariableAsync(int variablesReference, string name, string newValue);

    /// <summary>
    /// Run to the given file/line (set a temporary breakpoint + continue, or use gotoTargets/goto if supported).
    /// No-op when no session is active.
    /// </summary>
    Task RunToCursorAsync(string filePath, int line1);

    /// <summary>
    /// Move the instruction pointer to the given file/line without executing intervening code.
    /// Uses DAP gotoTargets + goto request. No-op when adapter doesn't support goto.
    /// </summary>
    Task SetNextStatementAsync(string filePath, int line1);

    /// <summary>
    /// Get the exception filter list supported by the active adapter (or a built-in default set).
    /// Returns empty list when no session active or adapter does not support exception filters.
    /// </summary>
    IReadOnlyList<ExceptionFilterInfo> ExceptionFilters { get; }

    /// <summary>
    /// Apply the given exception filter settings to the active debug session.
    /// Persisted in the session; re-applied on next LaunchAsync/AttachAsync.
    /// </summary>
    Task SetExceptionFiltersAsync(IReadOnlyList<ExceptionFilterInfo> filters);

    /// <summary>Get all loaded modules/assemblies in the current debug session.</summary>
    Task<IReadOnlyList<DebugModuleInfo>> GetModulesAsync();

    /// <summary>
    /// Freeze a thread so it does not execute during continue/step operations.
    /// Best-effort: no-op if the debug adapter does not support per-thread freeze.
    /// </summary>
    Task FreezeThreadAsync(int threadId) => Task.CompletedTask;

    /// <summary>Thaw a previously frozen thread. No-op if not supported.</summary>
    Task ThawThreadAsync(int threadId) => Task.CompletedTask;

    /// <summary>Returns true if the given thread is currently frozen (local tracking only).</summary>
    bool IsThreadFrozen(int threadId) => false;

    // ── Disassembly / Memory ───────────────────────────────────────────────────

    /// <summary>Disassemble at the given memory reference (hex address or DAP symbol).</summary>
    Task<IReadOnlyList<DisassembledInstruction>> DisassembleAsync(string memRef, int count);

    /// <summary>Read raw memory. Returns null if the adapter does not support readMemory.</summary>
    Task<byte[]?> ReadMemoryAsync(string memRef, int byteCount, int offset = 0);

    /// <summary>Write raw memory. No-op if not supported.</summary>
    Task WriteMemoryAsync(string memRef, byte[] data, int offset = 0);

    // ── Adapter registry ───────────────────────────────────────────────────

    // ── Symbol server ──────────────────────────────────────────────────────────

    /// <summary>
    /// Resolve PDB symbols for all loaded modules using the configured symbol servers.
    /// No-op when symbol support is disabled in settings.
    /// </summary>
    Task ResolveSymbolsAsync(CancellationToken ct = default) => Task.CompletedTask;

    // ── Edit & Continue / Hot Reload ───────────────────────────────────────────

    /// <summary>
    /// Restart the given stack frame from its beginning (Edit &amp; Continue).
    /// Best-effort: no-op if the adapter does not support restartFrame.
    /// </summary>
    Task RestartFrameAsync(int frameId, CancellationToken ct = default) => Task.CompletedTask;

    /// <summary>
    /// Apply hot-reload delta metadata to the running process.
    /// Uses <c>System.Reflection.Metadata.MetadataUpdater.ApplyUpdate</c>.
    /// No-op if the runtime does not support hot reload.
    /// </summary>
    Task ApplyHotReloadAsync(Type[] updatedTypes, byte[] metadataDelta, byte[] ilDelta, byte[] pdbDelta, CancellationToken ct = default)
        => Task.CompletedTask;

    // ── Data breakpoints ───────────────────────────────────────────────────────

    /// <summary>
    /// Get data breakpoint metadata for a variable by name/reference.
    /// Returns null if not supported.
    /// </summary>
    Task<DataBreakpointInfo?> GetDataBreakpointInfoAsync(string name, int? variablesReference = null, CancellationToken ct = default)
        => Task.FromResult<DataBreakpointInfo?>(null);

    /// <summary>
    /// Set the active data breakpoints (memory watchpoints).
    /// Replaces the previous list. Pass empty list to clear all.
    /// </summary>
    Task SetDataBreakpointsAsync(IReadOnlyList<DataBreakpointInfo> breakpoints, CancellationToken ct = default)
        => Task.CompletedTask;

    /// <summary>Current data breakpoints (memory watchpoints).</summary>
    IReadOnlyList<DataBreakpointInfo> DataBreakpoints => [];

    // ── Adapter registry ───────────────────────────────────────────────────────

    /// <summary>
    /// Register a custom debug adapter factory for a language ID.
    /// Plugins use this to add support for non-built-in languages (e.g. "ruby", "rust").
    /// The factory is called each time a new session is launched for that language.
    /// Default implementation is a no-op (override in DebuggerServiceImpl).
    /// </summary>
    void RegisterAdapter(string languageId, Func<object> factory) { }

    /// <summary>Unregister a previously registered custom adapter factory.</summary>
    void UnregisterAdapter(string languageId) { }
}
