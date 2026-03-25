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
}
