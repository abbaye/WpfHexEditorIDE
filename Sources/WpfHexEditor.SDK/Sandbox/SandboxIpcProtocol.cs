//////////////////////////////////////////////
// GNU Affero General Public License v3.0 - 2026
// Author : Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.6
//////////////////////////////////////////////

// ==========================================================
// Project: WpfHexEditor.SDK
// File: SandboxIpcProtocol.cs
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

    // Sandbox → IDE (UI registration — Phase 9)
    RegisterPanelNotification,
    RegisterDocumentTabNotification,
    UnregisterPanelNotification,

    // Sandbox → IDE (menu / toolbar / status-bar registration — Phase 10)
    RegisterMenuItemNotification,
    UnregisterMenuItemNotification,
    RegisterToolbarItemNotification,
    UnregisterToolbarItemNotification,
    RegisterStatusBarItemNotification,
    UnregisterStatusBarItemNotification,

    // Sandbox → IDE (panel visibility forwarding — Phase 10)
    PanelActionNotification,

    // Sandbox → IDE (options page — Phase 11)
    RegisterOptionsPageNotification,

    // IDE → Sandbox (UI lifecycle — Phase 9 / 10)
    ResizePanelRequest,
    ThemeChangedNotification,
    ExecuteCommandRequest,
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

    /// <summary>
    /// Serialised XAML of the host's active theme ResourceDictionaries.
    /// The sandbox merges this into Application.Resources so plugin WPF
    /// controls inherit the same theme tokens as the IDE.
    /// Empty string means no theme forwarding (fallback to default styles).
    /// </summary>
    [JsonPropertyName("themeResourcesXaml")]
    public string ThemeResourcesXaml { get; set; } = string.Empty;

    /// <summary>
    /// Source URIs (pack://application:,,,/…) of all ResourceDictionaries in the
    /// host's theme hierarchy. The sandbox loads these BEFORE applying ThemeResourcesXaml
    /// so that Style resources (PanelToolbarStyle, PanelTreeViewItemStyle, etc.) are
    /// present in Application.Resources when plugin XAML calls InitializeComponent().
    /// <para>
    /// ThemeResourcesXaml only serialises primitive values (SolidColorBrush, Color, etc.).
    /// The source URIs carry the full Style / ControlTemplate definitions.
    /// </para>
    /// </summary>
    [JsonPropertyName("themeDictionaryUris")]
    public List<string> ThemeDictionaryUris { get; set; } = [];
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

// ──────────────────────────────────────────────────────────
// Phase 9 — UI bridge payloads  (Sandbox → IDE, no response)
// ──────────────────────────────────────────────────────────

/// <summary>
/// Sent by the sandbox when a plugin calls IUIRegistry.RegisterPanel().
/// Carries the Win32 HWND (as long) of the HwndSource child window that hosts
/// the plugin's UserControl.  The IDE wraps it in an HwndHost and docks it.
/// </summary>
public sealed class RegisterPanelNotificationPayload
{
    [JsonPropertyName("contentId")]
    public string ContentId { get; set; } = string.Empty;

    [JsonPropertyName("title")]
    public string Title { get; set; } = string.Empty;

    /// <summary>Win32 HWND of the sandbox-side HwndSource (WS_CHILD).</summary>
    [JsonPropertyName("hwnd")]
    public long Hwnd { get; set; }

    /// <summary>Requested dock position hint (e.g. "Left", "Right", "Bottom").</summary>
    [JsonPropertyName("panelType")]
    public string PanelType { get; set; } = string.Empty;

    [JsonPropertyName("width")]
    public double Width { get; set; } = 300;

    [JsonPropertyName("height")]
    public double Height { get; set; } = 300;
}

/// <summary>
/// Sent by the sandbox when a plugin calls IUIRegistry.RegisterDocumentTab().
/// </summary>
public sealed class RegisterDocumentTabNotificationPayload
{
    [JsonPropertyName("contentId")]
    public string ContentId { get; set; } = string.Empty;

    [JsonPropertyName("title")]
    public string Title { get; set; } = string.Empty;

    /// <summary>Win32 HWND of the sandbox-side HwndSource (WS_CHILD).</summary>
    [JsonPropertyName("hwnd")]
    public long Hwnd { get; set; }

    [JsonPropertyName("width")]
    public double Width { get; set; } = 600;

    [JsonPropertyName("height")]
    public double Height { get; set; } = 400;
}

/// <summary>
/// Sent by the sandbox when a plugin panel/tab is closed from within the sandbox.
/// The IDE should remove the corresponding HwndHost from the docking layout.
/// </summary>
public sealed class UnregisterPanelNotificationPayload
{
    [JsonPropertyName("contentId")]
    public string ContentId { get; set; } = string.Empty;
}

// ──────────────────────────────────────────────────────────
// Phase 9 — UI lifecycle payloads  (IDE → Sandbox)
// ──────────────────────────────────────────────────────────

/// <summary>
/// Sent by the IDE when the HwndHost is resized so the sandbox can
/// call SetWindowPos on its HwndSource to match.
/// </summary>
public sealed class ResizePanelRequestPayload
{
    [JsonPropertyName("contentId")]
    public string ContentId { get; set; } = string.Empty;

    [JsonPropertyName("width")]
    public double Width { get; set; }

    [JsonPropertyName("height")]
    public double Height { get; set; }
}

/// <summary>
/// Sent by the IDE when the user switches themes.
/// The sandbox re-applies the new ResourceDictionaries via ThemeBootstrapper.
/// </summary>
public sealed class ThemeChangedNotificationPayload
{
    [JsonPropertyName("themeResourcesXaml")]
    public string ThemeResourcesXaml { get; set; } = string.Empty;
}

// ──────────────────────────────────────────────────────────
// Phase 10 — Menu / Toolbar / StatusBar bridge payloads
// (Sandbox → IDE, no response)
// ──────────────────────────────────────────────────────────

/// <summary>
/// Sent when a plugin calls IUIRegistry.RegisterMenuItem().
/// The host creates a real menu item backed by an IpcRelayCommand that
/// sends ExecuteCommandRequest back to the sandbox.
/// </summary>
public sealed class RegisterMenuItemNotificationPayload
{
    [JsonPropertyName("contentId")]
    public string ContentId { get; set; } = string.Empty;

    [JsonPropertyName("header")]
    public string Header { get; set; } = string.Empty;

    [JsonPropertyName("parentPath")]
    public string ParentPath { get; set; } = "Tools";

    [JsonPropertyName("group")]
    public string? Group { get; set; }

    [JsonPropertyName("iconGlyph")]
    public string? IconGlyph { get; set; }

    [JsonPropertyName("gestureText")]
    public string? GestureText { get; set; }

    [JsonPropertyName("toolTip")]
    public string? ToolTip { get; set; }

    [JsonPropertyName("insertPosition")]
    public int InsertPosition { get; set; } = -1;

    /// <summary>
    /// Opaque command identifier that the host passes back in ExecuteCommandRequest.
    /// Null when the plugin registered no command.
    /// </summary>
    [JsonPropertyName("commandId")]
    public string? CommandId { get; set; }
}

/// <summary>Sent when a plugin calls IUIRegistry.UnregisterMenuItem().</summary>
public sealed class UnregisterMenuItemNotificationPayload
{
    [JsonPropertyName("contentId")]
    public string ContentId { get; set; } = string.Empty;
}

/// <summary>
/// Sent when a plugin calls IUIRegistry.RegisterToolbarItem().
/// The host creates a toolbar button backed by an IpcRelayCommand.
/// </summary>
public sealed class RegisterToolbarItemNotificationPayload
{
    [JsonPropertyName("contentId")]
    public string ContentId { get; set; } = string.Empty;

    [JsonPropertyName("iconGlyph")]
    public string? IconGlyph { get; set; }

    [JsonPropertyName("toolTip")]
    public string? ToolTip { get; set; }

    [JsonPropertyName("isSeparator")]
    public bool IsSeparator { get; set; }

    /// <summary>Toolbar group index (matches ToolbarItemDescriptor.Group int field).</summary>
    [JsonPropertyName("group")]
    public int Group { get; set; }

    [JsonPropertyName("commandId")]
    public string? CommandId { get; set; }
}

/// <summary>Sent when a plugin calls IUIRegistry.UnregisterToolbarItem().</summary>
public sealed class UnregisterToolbarItemNotificationPayload
{
    [JsonPropertyName("contentId")]
    public string ContentId { get; set; } = string.Empty;
}

/// <summary>
/// Sent when a plugin calls IUIRegistry.RegisterStatusBarItem().
/// </summary>
public sealed class RegisterStatusBarItemNotificationPayload
{
    [JsonPropertyName("contentId")]
    public string ContentId { get; set; } = string.Empty;

    [JsonPropertyName("text")]
    public string Text { get; set; } = string.Empty;

    /// <summary>Serialized StatusBarAlignment value: "Left", "Center", or "Right".</summary>
    [JsonPropertyName("alignment")]
    public string Alignment { get; set; } = "Right";

    [JsonPropertyName("toolTip")]
    public string? ToolTip { get; set; }

    [JsonPropertyName("order")]
    public int Order { get; set; }
}

/// <summary>Sent when a plugin calls IUIRegistry.UnregisterStatusBarItem().</summary>
public sealed class UnregisterStatusBarItemNotificationPayload
{
    [JsonPropertyName("contentId")]
    public string ContentId { get; set; } = string.Empty;
}

/// <summary>
/// Sent when a sandbox plugin calls ShowPanel / HidePanel / TogglePanel / FocusPanel.
/// Routes through the host so the docking system handles visibility correctly.
/// </summary>
public sealed class PanelActionNotificationPayload
{
    [JsonPropertyName("contentId")]
    public string ContentId { get; set; } = string.Empty;

    /// <summary>"Show", "Hide", "Toggle", or "Focus".</summary>
    [JsonPropertyName("action")]
    public string Action { get; set; } = string.Empty;
}

// ──────────────────────────────────────────────────────────
// Phase 10 — Command execution  (IDE → Sandbox, fire-and-forget)
// ──────────────────────────────────────────────────────────

// ──────────────────────────────────────────────────────────
// Phase 11 — Options page bridge  (Sandbox → IDE, no response)
// ──────────────────────────────────────────────────────────

/// <summary>
/// Sent after plugin initialisation when the plugin implements
/// <c>IPluginWithOptions</c>. Carries the Win32 HWND of the sandbox-side
/// HwndSource that hosts the options page UIElement.
/// The IDE registers a factory in OptionsPageRegistry that wraps the HWND
/// in an HwndPanelHost each time the Options dialog is opened.
/// </summary>
public sealed class RegisterOptionsPageNotificationPayload
{
    [JsonPropertyName("pluginId")]
    public string PluginId { get; set; } = string.Empty;

    [JsonPropertyName("pluginName")]
    public string PluginName { get; set; } = string.Empty;

    /// <summary>Win32 HWND (as long) of the sandbox options-page HwndSource.</summary>
    [JsonPropertyName("hwnd")]
    public long Hwnd { get; set; }
}

// ──────────────────────────────────────────────────────────
// Phase 10 — Command execution  (IDE → Sandbox, fire-and-forget)
// ──────────────────────────────────────────────────────────

/// <summary>
/// Sent by the IDE when the user activates a menu item or toolbar button
/// that was registered by the sandbox plugin. The sandbox looks up the
/// stored ICommand by CommandId and executes it.
/// </summary>
public sealed class ExecuteCommandRequestPayload
{
    [JsonPropertyName("commandId")]
    public string CommandId { get; set; } = string.Empty;
}
