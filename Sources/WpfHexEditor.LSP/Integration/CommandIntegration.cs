// ==========================================================
// Project: WpfHexEditor.LSP
// File: Integration/CommandIntegration.cs
// Author: Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.6
// Created: 2026-03-16
// Description:
//     Bridges LSP operations (GoToDefinition, Rename, Format, Lint)
//     to IDE commands via IIDEEventBus so the CodeEditor and MainWindow
//     can trigger LSP actions without a direct reference to LSP types.
//
// Architecture Notes:
//     Pattern: Mediator (event-driven command routing)
//     Subscribes to LspCommandRequestedEvent on IDEEventBus and dispatches
//     to the appropriate LSP provider. Results are published back as
//     LspCommandResultEvent for UI consumers (ErrorList, NavigationService).
// ==========================================================

using WpfHexEditor.Events;
using WpfHexEditor.LSP.Formatting;
using WpfHexEditor.LSP.Navigation;
using WpfHexEditor.LSP.Refactoring;
using WpfHexEditor.LSP.Symbols;

namespace WpfHexEditor.LSP.Integration;

/// <summary>
/// Routes IDE-level LSP command requests to the appropriate LSP provider.
/// Subscribe by calling <see cref="Attach"/>; unsubscribe via <see cref="Detach"/>.
/// </summary>
public sealed class CommandIntegration : IDisposable
{
    private readonly IIDEEventBus         _eventBus;
    private readonly NavigationProvider   _navigationProvider;
    private readonly RefactoringEngine    _refactoringEngine;
    private readonly CodeFormatter        _codeFormatter;
    private readonly SymbolTableManager   _symbolTableManager;
    private bool        _attached;
    private IDisposable? _subscription;

    public CommandIntegration(
        IIDEEventBus        eventBus,
        NavigationProvider  navigationProvider,
        RefactoringEngine   refactoringEngine,
        CodeFormatter       codeFormatter,
        SymbolTableManager  symbolTableManager)
    {
        _eventBus           = eventBus           ?? throw new ArgumentNullException(nameof(eventBus));
        _navigationProvider = navigationProvider ?? throw new ArgumentNullException(nameof(navigationProvider));
        _refactoringEngine  = refactoringEngine  ?? throw new ArgumentNullException(nameof(refactoringEngine));
        _codeFormatter      = codeFormatter      ?? throw new ArgumentNullException(nameof(codeFormatter));
        _symbolTableManager = symbolTableManager ?? throw new ArgumentNullException(nameof(symbolTableManager));
    }

    // -----------------------------------------------------------------------
    // Lifecycle
    // -----------------------------------------------------------------------

    /// <summary>Subscribes to the IDE event bus. Call once after construction.</summary>
    public void Attach()
    {
        if (_attached) return;
        _subscription = _eventBus.Subscribe<LspCommandRequestedEvent>(OnCommandRequested);
        _attached = true;
    }

    /// <summary>Unsubscribes from the IDE event bus.</summary>
    public void Detach()
    {
        if (!_attached) return;
        _subscription?.Dispose();
        _subscription = null;
        _attached = false;
    }

    public void Dispose() => Detach();

    // -----------------------------------------------------------------------
    // Dispatch
    // -----------------------------------------------------------------------

    private void OnCommandRequested(LspCommandRequestedEvent e)
    {
        switch (e.Command)
        {
            case LspCommand.GoToDefinition:
                DispatchGoToDefinition(e);
                break;

            case LspCommand.FindAllReferences:
                DispatchFindAllReferences(e);
                break;

            case LspCommand.Rename:
                DispatchRename(e);
                break;

            case LspCommand.FormatDocument:
                DispatchFormatDocument(e);
                break;

            case LspCommand.FormatRange:
                DispatchFormatRange(e);
                break;
        }
    }

    private void DispatchGoToDefinition(LspCommandRequestedEvent e)
    {
        if (e.ParseResult is null) return;

        var location = _navigationProvider.GoToDefinition(
            e.FilePath, e.ParseResult, e.Line, e.Column);

        if (location is not null)
            _eventBus.Publish(new LspNavigationResultEvent(location.FilePath, location.Line, location.Column));
    }

    private void DispatchFindAllReferences(LspCommandRequestedEvent e)
    {
        var token = e.SymbolName;
        if (string.IsNullOrWhiteSpace(token)) return;

        var locations = _navigationProvider.FindAllReferences(token);
        _eventBus.Publish(new LspFindReferencesResultEvent(locations));
    }

    private void DispatchRename(LspCommandRequestedEvent e)
    {
        if (e.ParseResult is null || string.IsNullOrWhiteSpace(e.SymbolName)
            || string.IsNullOrWhiteSpace(e.NewName)) return;

        var ctx = new RefactoringContext
        {
            FilePath           = e.FilePath,
            ParseResult        = e.ParseResult,
            SymbolTableManager = _symbolTableManager,
        };
        var refactoring = new RenameRefactoring { NewName = e.NewName ?? string.Empty };
        var edits = refactoring.Apply(ctx);
        _eventBus.Publish(new LspRenameEditsResultEvent(e.FilePath, edits));
    }

    private void DispatchFormatDocument(LspCommandRequestedEvent e)
    {
        if (e.DocumentText is null) return;
        var edits = _codeFormatter.FormatDocument(e.FilePath, e.DocumentText, e.FormattingOptions ?? new FormattingOptions());
        _eventBus.Publish(new LspTextEditsResultEvent(e.FilePath, edits));
    }

    private void DispatchFormatRange(LspCommandRequestedEvent e)
    {
        if (e.DocumentText is null) return;
        var edits = _codeFormatter.FormatRange(
            e.FilePath, e.DocumentText, e.StartLine, e.EndLine,
            e.FormattingOptions ?? new FormattingOptions());
        _eventBus.Publish(new LspTextEditsResultEvent(e.FilePath, edits));
    }
}

// -----------------------------------------------------------------------
// Event types
// -----------------------------------------------------------------------

/// <summary>Identifies the LSP operation requested.</summary>
public enum LspCommand
{
    GoToDefinition,
    FindAllReferences,
    Rename,
    FormatDocument,
    FormatRange,
}

/// <summary>Request event published by CodeEditor to trigger an LSP operation.</summary>
public sealed record LspCommandRequestedEvent : IDEEventBase
{
    public LspCommand        Command          { get; init; }
    public string            FilePath         { get; init; } = string.Empty;
    public int               Line             { get; init; }
    public int               Column           { get; init; }
    public string?           SymbolName       { get; init; }
    public string?           NewName          { get; init; }
    public string?           DocumentText     { get; init; }
    public int               StartLine        { get; init; }
    public int               EndLine          { get; init; }
    public Parsing.ParseResult? ParseResult   { get; init; }
    public FormattingOptions? FormattingOptions { get; init; }
}

/// <summary>Published when GoToDefinition resolves successfully.</summary>
public sealed record LspNavigationResultEvent(string FilePath, int Line, int Column) : IDEEventBase;

/// <summary>Published with all reference locations for FindAllReferences.</summary>
public sealed record LspFindReferencesResultEvent(
    IReadOnlyList<Navigation.NavigationLocation> Locations) : IDEEventBase;

/// <summary>Published when a Format operation produces text edits (line/column ranges).</summary>
public sealed record LspTextEditsResultEvent(
    string FilePath,
    IReadOnlyList<Formatting.TextEdit> Edits) : IDEEventBase;

/// <summary>Published when a Rename operation produces offset-based text edits.</summary>
public sealed record LspRenameEditsResultEvent(
    string FilePath,
    IReadOnlyList<Refactoring.TextEdit> Edits) : IDEEventBase;
