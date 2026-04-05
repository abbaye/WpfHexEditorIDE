// ==========================================================
// Project: WpfHexEditor.PluginHost
// File: PluginScopedContext.cs
// Author: Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.6
// Created: 2026-03-08
// Description:
//     Per-plugin IIDEHostContext decorator that substitutes IHexEditorService
//     with a TimedHexEditorService proxy while forwarding all other services
//     unchanged to the shared base context.
//
// Architecture Notes:
//     Implements the Decorator pattern over IIDEHostContext.
//     One instance is created per loaded plugin inside WpfPluginHost.LoadPluginAsync.
//     Substituting only HexEditor keeps allocation minimal and avoids
//     changing any other service behaviour for the plugin.
//
// ==========================================================

using WpfHexEditor.Core.Events;
using WpfHexEditor.Core.Interfaces;
using WpfHexEditor.Editor.Core;
using WpfHexEditor.SDK.Commands;
using WpfHexEditor.SDK.Contracts;
using WpfHexEditor.SDK.Contracts.Services;

namespace WpfHexEditor.PluginHost;

/// <summary>
/// Per-plugin decorator over <see cref="IIDEHostContext"/> that exposes a
/// <see cref="TimedHexEditorService"/> proxy instead of the shared
/// <see cref="IHexEditorService"/> instance.
/// All other services are forwarded unchanged to the shared base context.
/// </summary>
internal sealed class PluginScopedContext : IIDEHostContext
{
    private readonly IIDEHostContext _inner;

    /// <inheritdoc />
    public IHexEditorService HexEditor { get; }

    // ── All remaining services — pure delegation ──────────────────────────────

    /// <inheritdoc />
    public ISolutionExplorerService SolutionExplorer => _inner.SolutionExplorer;

    /// <inheritdoc />
    public WpfHexEditor.Editor.Core.ISolutionManager? SolutionManager => _inner.SolutionManager;

    /// <inheritdoc />
    public WpfHexEditor.Editor.Core.LSP.ILspServerRegistry? LspServers => _inner.LspServers;

    /// <inheritdoc />
    public ICodeEditorService CodeEditor => _inner.CodeEditor;

    /// <inheritdoc />
    public IOutputService Output => _inner.Output;

    /// <inheritdoc />
    public IParsedFieldService ParsedField => _inner.ParsedField;

    /// <inheritdoc />
    public IErrorPanelService ErrorPanel => _inner.ErrorPanel;

    /// <inheritdoc />
    public IFocusContextService FocusContext => _inner.FocusContext;

    /// <inheritdoc />
    public IPluginEventBus EventBus => _inner.EventBus;

    /// <inheritdoc />
    public IUIRegistry UIRegistry => _inner.UIRegistry;

    /// <inheritdoc />
    public IThemeService Theme => _inner.Theme;

    /// <inheritdoc />
    public IPermissionService Permissions => _inner.Permissions;

    /// <inheritdoc />
    public ITerminalService Terminal => _inner.Terminal;

    /// <inheritdoc />
    public IIDEEventBus IDEEvents => _inner.IDEEvents;

    /// <inheritdoc />
    public IPluginCapabilityRegistry CapabilityRegistry => _inner.CapabilityRegistry;

    /// <inheritdoc />
    public IExtensionRegistry ExtensionRegistry => _inner.ExtensionRegistry;

    /// <inheritdoc />
    public IDocumentHostService DocumentHost => _inner.DocumentHost;

    /// <inheritdoc />
    public ICommandRegistry? CommandRegistry => _inner.CommandRegistry;

    /// <inheritdoc />
    public IDebuggerService? Debugger => _inner.Debugger;

    /// <inheritdoc />
    public IScriptingService? Scripting => _inner.Scripting;

    /// <inheritdoc />
    public IBuildSystem? BuildSystem => _inner.BuildSystem;

    /// <inheritdoc />
    public IWorkspaceService? Workspace => _inner.Workspace;

    /// <inheritdoc />
    public IFormatCatalogService FormatCatalog => _inner.FormatCatalog;

    /// <inheritdoc />
    public IFormatParsingService? FormatParsing => _inner.FormatParsing;

    /// <inheritdoc />
    public ITestRunnerService? TestRunner => _inner.TestRunner;

    /// <inheritdoc />
    public IDiffService? DiffService => _inner.DiffService;

    /// <inheritdoc />
    public IUIControlFactory? UIFactory => _inner.UIFactory;

    /// <inheritdoc />
    public ISyntaxColoringService? SyntaxColoring => _inner.SyntaxColoring;

    public PluginScopedContext(IIDEHostContext inner, TimedHexEditorService timedHexEditor)
    {
        _inner    = inner          ?? throw new ArgumentNullException(nameof(inner));
        HexEditor = timedHexEditor ?? throw new ArgumentNullException(nameof(timedHexEditor));
    }
}
