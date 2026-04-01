// ==========================================================
// Project: WpfHexEditor.PluginHost
// File: IDEHostContext.cs
// Author: Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.6
// Created: 2026-03-15
// Description:
//     Base implementation of IIDEHostContext assembled by the App layer.
//     Extended with IDEEvents (IDE-wide EventBus), CapabilityRegistry,
//     and ExtensionRegistry for Feature 3, 4, and 5.
//
// Architecture Notes:
//     All three new services are injected via constructor; they are never null.
//     PluginCapabilityRegistryAdapter resolves the circular-dependency
//     between WpfPluginHost and IDEHostContext (SetInner called post-construction).
// ==========================================================

using WpfHexEditor.Editor.Core;
using WpfHexEditor.Editor.Core.Notifications;
using WpfHexEditor.Core.Events;
using WpfHexEditor.Core.Interfaces;
using WpfHexEditor.SDK.Contracts;
using WpfHexEditor.SDK.Contracts.Services;
using System.Linq;

namespace WpfHexEditor.PluginHost;

/// <summary>
/// Base implementation of IIDEHostContext assembled by the App layer.
/// </summary>
public sealed class IDEHostContext : IIDEHostContext
{
    /// <inheritdoc />
    public IDocumentHostService DocumentHost { get; }

    /// <inheritdoc />
    public ISolutionExplorerService SolutionExplorer { get; }

    /// <inheritdoc />
    public IHexEditorService HexEditor { get; }

    /// <inheritdoc />
    public ICodeEditorService CodeEditor { get; }

    /// <inheritdoc />
    public IOutputService Output { get; }

    /// <inheritdoc />
    public IParsedFieldService ParsedField { get; }

    /// <inheritdoc />
    public IErrorPanelService ErrorPanel { get; }

    /// <inheritdoc />
    public IFocusContextService FocusContext { get; }

    /// <inheritdoc />
    public IPluginEventBus EventBus { get; }

    /// <inheritdoc />
    public IUIRegistry UIRegistry { get; }

    /// <inheritdoc />
    public IThemeService Theme { get; }

    /// <inheritdoc />
    public IPermissionService Permissions { get; }

    /// <inheritdoc />
    public ITerminalService Terminal { get; }

    /// <inheritdoc />
    public IIDEEventBus IDEEvents { get; }

    /// <inheritdoc />
    public IPluginCapabilityRegistry CapabilityRegistry { get; }

    /// <inheritdoc />
    public IExtensionRegistry ExtensionRegistry { get; }

    /// <inheritdoc />
    public ISolutionManager? SolutionManager { get; }

    /// <inheritdoc />
    public WpfHexEditor.Editor.Core.LSP.ILspServerRegistry? LspServers { get; init; }

    /// <inheritdoc />
    public WpfHexEditor.SDK.Commands.ICommandRegistry? CommandRegistry { get; }

    /// <inheritdoc />
    public WpfHexEditor.SDK.Contracts.Services.IDebuggerService? Debugger { get; }

    /// <inheritdoc />
    public WpfHexEditor.SDK.Contracts.Services.IScriptingService? Scripting { get; }

    /// <inheritdoc />
    public IBuildSystem? BuildSystem { get; }

    /// <inheritdoc />
    public WpfHexEditor.SDK.Contracts.Services.IWorkspaceService? Workspace { get; }

    /// <inheritdoc />
    public IFormatCatalogService FormatCatalog { get; }

    /// <inheritdoc />
    public IFormatParsingService? FormatParsing { get; }

    /// <inheritdoc />
    public INotificationService? Notifications { get; init; }

    /// <inheritdoc />
    /// Resolved lazily from <see cref="ExtensionRegistry"/> — set by UnitTesting plugin on init.
    public ITestRunnerService? TestRunner
        => ExtensionRegistry.GetExtensions<ITestRunnerService>().FirstOrDefault();

    /// <inheritdoc />
    /// Resolved lazily from <see cref="ExtensionRegistry"/> — set by FileComparison plugin on init.
    public IDiffService? DiffService
        => ExtensionRegistry.GetExtensions<IDiffService>().FirstOrDefault();

    public IDEHostContext(
        IDocumentHostService documentHost,
        ISolutionExplorerService solutionExplorer,
        IHexEditorService hexEditor,
        ICodeEditorService codeEditor,
        IOutputService output,
        IParsedFieldService parsedField,
        IErrorPanelService errorPanel,
        IFocusContextService focusContext,
        IPluginEventBus eventBus,
        IUIRegistry uiRegistry,
        IThemeService theme,
        IPermissionService permissions,
        ITerminalService terminal,
        IIDEEventBus ideEvents,
        IPluginCapabilityRegistry capabilityRegistry,
        IExtensionRegistry extensionRegistry,
        ISolutionManager? solutionManager = null,
        WpfHexEditor.SDK.Commands.ICommandRegistry? commandRegistry = null,
        WpfHexEditor.SDK.Contracts.Services.IDebuggerService? debuggerService = null,
        WpfHexEditor.SDK.Contracts.Services.IScriptingService? scriptingService = null,
        IBuildSystem? buildSystem = null,
        WpfHexEditor.SDK.Contracts.Services.IWorkspaceService? workspaceService = null,
        IFormatParsingService? formatParsingService = null,
        IFormatCatalogService? formatCatalogService = null)
    {
        DocumentHost        = documentHost        ?? throw new ArgumentNullException(nameof(documentHost));
        SolutionExplorer    = solutionExplorer    ?? throw new ArgumentNullException(nameof(solutionExplorer));
        HexEditor           = hexEditor           ?? throw new ArgumentNullException(nameof(hexEditor));
        CodeEditor          = codeEditor          ?? throw new ArgumentNullException(nameof(codeEditor));
        Output              = output              ?? throw new ArgumentNullException(nameof(output));
        ParsedField         = parsedField         ?? throw new ArgumentNullException(nameof(parsedField));
        ErrorPanel          = errorPanel          ?? throw new ArgumentNullException(nameof(errorPanel));
        FocusContext        = focusContext        ?? throw new ArgumentNullException(nameof(focusContext));
        EventBus            = eventBus            ?? throw new ArgumentNullException(nameof(eventBus));
        UIRegistry          = uiRegistry          ?? throw new ArgumentNullException(nameof(uiRegistry));
        Theme               = theme               ?? throw new ArgumentNullException(nameof(theme));
        Permissions         = permissions         ?? throw new ArgumentNullException(nameof(permissions));
        Terminal            = terminal            ?? throw new ArgumentNullException(nameof(terminal));
        IDEEvents           = ideEvents           ?? throw new ArgumentNullException(nameof(ideEvents));
        CapabilityRegistry  = capabilityRegistry  ?? throw new ArgumentNullException(nameof(capabilityRegistry));
        ExtensionRegistry   = extensionRegistry   ?? throw new ArgumentNullException(nameof(extensionRegistry));
        SolutionManager     = solutionManager;
        CommandRegistry     = commandRegistry;
        Debugger            = debuggerService;
        Scripting           = scriptingService;
        BuildSystem         = buildSystem;
        Workspace           = workspaceService;
        FormatParsing       = formatParsingService;
        FormatCatalog       = formatCatalogService!;
    }
}
