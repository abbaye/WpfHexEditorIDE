// ==========================================================
// Project: WpfHexEditor.PluginHost
// File: IDEHostContext.cs
// Author: Auto
// Created: 2026-03-06
// Description:
//     Concrete implementation of IIDEHostContext passed to every plugin.
//     Aggregates all IDE service references and exposes them as a single
//     injection point, keeping plugin code decoupled from App internals.
//
// Architecture Notes:
//     Constructed by App (IDEHostContextImpl inherits this or App injects directly).
//     All service references are set via constructor injection.
//     IPermissionService and IUIRegistry are PluginHost-owned singletons.
//
// ==========================================================

using WpfHexEditor.SDK.Contracts;
using WpfHexEditor.SDK.Contracts.Services;

namespace WpfHexEditor.PluginHost;

/// <summary>
/// Base implementation of IIDEHostContext assembled by the App layer.
/// </summary>
public sealed class IDEHostContext : IIDEHostContext
{
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

    public IDEHostContext(
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
        IPermissionService permissions)
    {
        SolutionExplorer = solutionExplorer ?? throw new ArgumentNullException(nameof(solutionExplorer));
        HexEditor = hexEditor ?? throw new ArgumentNullException(nameof(hexEditor));
        CodeEditor = codeEditor ?? throw new ArgumentNullException(nameof(codeEditor));
        Output = output ?? throw new ArgumentNullException(nameof(output));
        ParsedField = parsedField ?? throw new ArgumentNullException(nameof(parsedField));
        ErrorPanel = errorPanel ?? throw new ArgumentNullException(nameof(errorPanel));
        FocusContext = focusContext ?? throw new ArgumentNullException(nameof(focusContext));
        EventBus = eventBus ?? throw new ArgumentNullException(nameof(eventBus));
        UIRegistry = uiRegistry ?? throw new ArgumentNullException(nameof(uiRegistry));
        Theme = theme ?? throw new ArgumentNullException(nameof(theme));
        Permissions = permissions ?? throw new ArgumentNullException(nameof(permissions));
    }
}
