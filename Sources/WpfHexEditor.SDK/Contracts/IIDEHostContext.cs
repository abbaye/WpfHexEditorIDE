// ==========================================================
// Project: WpfHexEditor.SDK
// File: IIDEHostContext.cs
// Author: Auto
// Created: 2026-03-06
// Description:
//     The complete IDE host context passed to every plugin via InitializeAsync.
//     Provides access to all IDE services through plugin-safe interfaces.
//
// Architecture Notes:
//     This is the single injection point — plugins should NOT reference App internals.
//     Implemented by IDEHostContextImpl in App/Services, injected into PluginHost.
//     All service properties are non-null; unavailable services return no-op implementations.
//
// ==========================================================

using WpfHexEditor.SDK.Contracts.Services;

namespace WpfHexEditor.SDK.Contracts;

/// <summary>
/// Complete IDE host context exposing all services available to plugins.
/// Passed to <see cref="IWpfHexEditorPlugin.InitializeAsync"/> by PluginHost.
/// </summary>
public interface IIDEHostContext
{
    // ── IDE Services ─────────────────────────────────────────────────────────

    /// <summary>Access to the Solution Explorer for file/project navigation.</summary>
    ISolutionExplorerService SolutionExplorer { get; }

    /// <summary>Access to the active HexEditor content and selection state.</summary>
    IHexEditorService HexEditor { get; }

    /// <summary>Access to the active CodeEditor content and cursor state.</summary>
    ICodeEditorService CodeEditor { get; }

    /// <summary>Access to the IDE OutputPanel for plugin log messages.</summary>
    IOutputService Output { get; }

    /// <summary>Access to parsed field data from the active binary document.</summary>
    IParsedFieldService ParsedField { get; }

    /// <summary>Access to the IDE ErrorPanel for plugin diagnostics.</summary>
    IErrorPanelService ErrorPanel { get; }

    // ── Cross-Plugin Services ────────────────────────────────────────────────

    /// <summary>Focus-aware service tracking active document and panel.</summary>
    IFocusContextService FocusContext { get; }

    /// <summary>
    /// Central event bus for decoupled plugin-to-plugin communication.
    /// Publish and subscribe to typed events without direct plugin references.
    /// </summary>
    IPluginEventBus EventBus { get; }

    // ── UI Integration ───────────────────────────────────────────────────────

    /// <summary>
    /// Registry for contributing UI elements (panels, menus, toolbars, status bar items).
    /// All contributed elements are automatically removed on plugin unload.
    /// </summary>
    IUIRegistry UIRegistry { get; }

    // ── IDE Features ──────────────────────────────────────────────────────────

    /// <summary>
    /// Theme service providing current IDE theme resources and theme change notifications.
    /// All plugin WPF controls must use this service for color/brush bindings.
    /// </summary>
    IThemeService Theme { get; }

    /// <summary>
    /// Runtime permission service for checking and responding to permission changes.
    /// Plugins must degrade gracefully when permissions are revoked.
    /// </summary>
    IPermissionService Permissions { get; }
}
