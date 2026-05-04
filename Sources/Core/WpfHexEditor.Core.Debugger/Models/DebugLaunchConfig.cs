// ==========================================================
// Project: WpfHexEditor.Core.Debugger
// File: Models/DebugLaunchConfig.cs
// Description: Configuration for launching a debug session.
// ==========================================================

namespace WpfHexEditor.Core.Debugger.Models;

/// <summary>Transport used for a remote debug connection.</summary>
public enum RemoteDebugTransport { Tcp, Ssh }

/// <summary>
/// Configuration for attaching to a remote debug adapter over TCP or SSH.
/// Passed to <see cref="WpfHexEditor.SDK.Contracts.Services.IDebuggerService.LaunchRemoteAsync"/>.
/// </summary>
public sealed record RemoteDebugConfig
{
    /// <summary>Transport layer: direct TCP or SSH tunnel.</summary>
    public RemoteDebugTransport Transport   { get; init; } = RemoteDebugTransport.Tcp;

    /// <summary>Remote host running the debug adapter (TCP: adapter host; SSH: SSH server host).</summary>
    public string Host                     { get; init; } = "localhost";

    /// <summary>Port the remote debug adapter listens on.</summary>
    public int    AdapterPort              { get; init; } = 4711;

    // ── SSH-only ──────────────────────────────────────────────────────────

    /// <summary>SSH server port (SSH transport only).</summary>
    public int    SshPort                  { get; init; } = 22;

    /// <summary>SSH username (SSH transport only).</summary>
    public string SshUser                  { get; init; } = string.Empty;

    /// <summary>Optional path to SSH private key file (SSH transport only).</summary>
    public string? SshKeyPath             { get; init; }

    /// <summary>Host where the adapter listens as seen from the SSH server (default: localhost).</summary>
    public string RemoteAdapterHost        { get; init; } = "localhost";

    /// <summary>Local port used for the SSH tunnel forwarding (SSH transport only).</summary>
    public int    LocalTunnelPort          { get; init; } = 14711;

    // ── Adapter init ──────────────────────────────────────────────────────

    /// <summary>Language ID used to select initialisation arguments (default: "csharp").</summary>
    public string LanguageId               { get; init; } = "csharp";

    /// <summary>When true, halts at the program entry point.</summary>
    public bool   StopAtEntry              { get; init; }
}

/// <summary>
/// Configuration record for a debug launch request.
/// Passed to <see cref="WpfHexEditor.SDK.Contracts.Services.IDebuggerService.LaunchAsync"/>.
/// </summary>
public sealed record DebugLaunchConfig
{
    /// <summary>Full path to the startup project (.csproj or output .dll).</summary>
    public string ProjectPath   { get; init; } = string.Empty;

    /// <summary>Full path to the output executable or DLL to debug.</summary>
    public string ProgramPath   { get; init; } = string.Empty;

    /// <summary>Command-line arguments for the debuggee.</summary>
    public string[] Args        { get; init; } = [];

    /// <summary>Working directory (defaults to ProgramPath directory).</summary>
    public string? WorkDir      { get; init; }

    /// <summary>Additional environment variables injected into debuggee.</summary>
    public Dictionary<string, string> Env { get; init; } = [];

    /// <summary>When true, halts at the program entry point.</summary>
    public bool StopAtEntry     { get; init; }

    /// <summary>Preferred debug adapter language ID (e.g. "csharp").</summary>
    public string LanguageId    { get; init; } = "csharp";

    /// <summary>Request mode: "launch" (default) or "attach".</summary>
    public string Request       { get; init; } = "launch";

    /// <summary>Process ID to attach to (only used when <see cref="Request"/> is "attach").</summary>
    public int? ProcessId       { get; init; }

    /// <summary>When true, the debugger skips non-user (framework) code.</summary>
    public bool JustMyCode      { get; init; } = true;

    /// <summary>Where the debuggee stdout/stderr is shown: "internalConsole", "integratedTerminal", or "externalTerminal".</summary>
    public string Console       { get; init; } = "internalConsole";
}
