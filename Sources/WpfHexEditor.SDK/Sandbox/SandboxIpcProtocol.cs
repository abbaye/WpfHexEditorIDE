//////////////////////////////////////////////
// GNU Affero General Public License v3.0 - 2026
// Author : Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.6
//////////////////////////////////////////////

// ==========================================================
// Project: WpfHexEditor.SDK
// File: SandboxIpcProtocol.cs
// Author: Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.6
// Created: 2026-03-15
// Description:
//     Shared IPC message protocol between the IDE host process and
//     WpfHexEditor.PluginSandbox.exe child processes.
//     All messages are length-prefixed JSON over Named Pipes.
//
// Architecture Notes:
//     - Pattern: Command / Response (request-response pairing via CorrelationId)
//     - Each request carries a CorrelationId (Guid string) so the proxy can
//       match async responses to waiting callers.
//     - MetricsPush and CrashNotification are fire-and-forget (no response).
//     - All types are self-contained (no WPF / no Windows-specific types) so
//       the PluginSandbox.exe console app can reference this file directly.
// ==========================================================

using System.Text.Json.Serialization;

namespace WpfHexEditor.SDK.Sandbox;

// ──────────────────────────────────────────────────────────
// Message kind discriminator
// ──────────────────────────────────────────────────────────

/// <summary>Discriminates IPC message types carried over the Named Pipe.</summary>
public enum SandboxMessageKind
{
    // IDE → Sandbox (requests)
    InitializeRequest,
    ShutdownRequest,
    InvokeRequest,

    // Sandbox → IDE (responses)
    InitializeResponse,
    ShutdownResponse,
    InvokeResponse,

    // Sandbox → IDE (fire-and-forget push)
    MetricsPush,
    CrashNotification,
    ReadyNotification,
}

// ──────────────────────────────────────────────────────────
// Base envelope
// ──────────────────────────────────────────────────────────

/// <summary>
/// Top-level envelope.  All messages are serialised as this type;
/// the actual payload lives in <see cref="Payload"/> (opaque JSON string).
/// </summary>
public sealed class SandboxEnvelope
{
    /// <summary>Message discriminator.</summary>
    [JsonPropertyName("kind")]
    public SandboxMessageKind Kind { get; set; }

    /// <summary>
    /// Correlation identifier that matches a request to its response.
    /// Fire-and-forget messages (MetricsPush, CrashNotification) set this to empty string.
    /// </summary>
    [JsonPropertyName("correlationId")]
    public string CorrelationId { get; set; } = string.Empty;

    /// <summary>Inner payload serialised as a JSON string (double-encoded).</summary>
    [JsonPropertyName("payload")]
    public string Payload { get; set; } = string.Empty;
}

// ──────────────────────────────────────────────────────────
// Request payloads  (IDE → Sandbox)
// ──────────────────────────────────────────────────────────

/// <summary>
/// Sent immediately after the pipe is connected.
/// Carries the manifest metadata required to load and initialise the plugin.
/// </summary>
public sealed class InitializeRequestPayload
{
    [JsonPropertyName("pluginId")]
    public string PluginId { get; set; } = string.Empty;

    [JsonPropertyName("pluginName")]
    public string PluginName { get; set; } = string.Empty;

    [JsonPropertyName("assemblyPath")]
    public string AssemblyPath { get; set; } = string.Empty;

    [JsonPropertyName("entryType")]
    public string EntryType { get; set; } = string.Empty;

    /// <summary>
    /// Marshalled service capabilities granted to this sandbox instance.
    /// Services the plugin is NOT permitted to call will be absent.
    /// </summary>
    [JsonPropertyName("grantedPermissions")]
    public List<string> GrantedPermissions { get; set; } = [];
}

/// <summary>Requests graceful plugin shutdown before the sandbox process exits.</summary>
public sealed class ShutdownRequestPayload
{
    [JsonPropertyName("reason")]
    public string Reason { get; set; } = "HostShutdown";
}

/// <summary>
/// Requests invocation of a marshalled IDE service method.
/// Used when the sandbox-hosted plugin calls back into an IDE service
/// (e.g. IOutputService.WriteLine) — the sandbox serialises the call and
/// sends it to the IDE which executes it and returns the result.
/// </summary>
public sealed class InvokeRequestPayload
{
    [JsonPropertyName("serviceInterface")]
    public string ServiceInterface { get; set; } = string.Empty;

    [JsonPropertyName("methodName")]
    public string MethodName { get; set; } = string.Empty;

    /// <summary>JSON-serialised array of method arguments.</summary>
    [JsonPropertyName("arguments")]
    public string ArgumentsJson { get; set; } = "[]";
}

// ──────────────────────────────────────────────────────────
// Response payloads  (Sandbox → IDE)
// ──────────────────────────────────────────────────────────

/// <summary>Common result wrapper for all responses.</summary>
public sealed class SandboxResponsePayload
{
    [JsonPropertyName("success")]
    public bool Success { get; set; }

    [JsonPropertyName("errorMessage")]
    public string? ErrorMessage { get; set; }

    /// <summary>Optional JSON-serialised return value for InvokeResponse.</summary>
    [JsonPropertyName("resultJson")]
    public string? ResultJson { get; set; }
}

// ──────────────────────────────────────────────────────────
// Push payloads  (Sandbox → IDE, no response)
// ──────────────────────────────────────────────────────────

/// <summary>
/// Sent once after the sandbox has successfully loaded and initialised the plugin.
/// </summary>
public sealed class ReadyNotificationPayload
{
    [JsonPropertyName("pluginId")]
    public string PluginId { get; set; } = string.Empty;

    [JsonPropertyName("pluginVersion")]
    public string PluginVersion { get; set; } = string.Empty;
}

/// <summary>
/// Periodic performance snapshot pushed from the sandbox to the IDE.
/// Enables the Plugin Monitor to display real, per-plugin CPU/memory
/// derived from the sandbox process rather than the shared IDE process.
/// </summary>
public sealed class MetricsPushPayload
{
    [JsonPropertyName("pluginId")]
    public string PluginId { get; set; } = string.Empty;

    /// <summary>CPU usage of the sandbox process (0–100).</summary>
    [JsonPropertyName("cpuPercent")]
    public double CpuPercent { get; set; }

    /// <summary>Private memory bytes of the sandbox process.</summary>
    [JsonPropertyName("privateMemoryBytes")]
    public long PrivateMemoryBytes { get; set; }

    /// <summary>GC managed heap bytes inside the sandbox.</summary>
    [JsonPropertyName("gcMemoryBytes")]
    public long GcMemoryBytes { get; set; }

    /// <summary>Average plugin callback execution time over the last window.</summary>
    [JsonPropertyName("avgExecMs")]
    public double AvgExecMs { get; set; }

    [JsonPropertyName("timestamp")]
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// Sent when an unhandled exception escapes the plugin inside the sandbox.
/// The IDE can mark the plugin as Faulted and optionally restart the sandbox.
/// </summary>
public sealed class CrashNotificationPayload
{
    [JsonPropertyName("pluginId")]
    public string PluginId { get; set; } = string.Empty;

    [JsonPropertyName("exceptionType")]
    public string ExceptionType { get; set; } = string.Empty;

    [JsonPropertyName("message")]
    public string Message { get; set; } = string.Empty;

    [JsonPropertyName("stackTrace")]
    public string StackTrace { get; set; } = string.Empty;

    [JsonPropertyName("phase")]
    public string Phase { get; set; } = "Runtime";
}
