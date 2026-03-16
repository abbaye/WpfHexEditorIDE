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

using WpfHexEditor.Events;
using WpfHexEditor.SDK.Contracts;
using WpfHexEditor.SDK.Contracts.Services;

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
        IExtensionRegistry extensionRegistry)
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
    }
}
