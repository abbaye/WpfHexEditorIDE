// ==========================================================
// Project: WpfHexEditor.Core.Debugger
// File: Models/DebugLaunchConfig.cs
// Description: Configuration for launching a debug session.
// ==========================================================

namespace WpfHexEditor.Core.Debugger.Models;

/// <summary>
/// Configuration record for a debug launch request.
/// Passed to <see cref="Services.IDebuggerService.LaunchAsync"/>.
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
