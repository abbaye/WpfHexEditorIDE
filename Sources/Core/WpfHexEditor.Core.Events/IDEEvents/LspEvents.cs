// ==========================================================
// Project: WpfHexEditor.Core.Events
// File: IDEEvents/LspEvents.cs
// Author: Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.6
// Created: 2026-03-16
// Description:
//     IDE event records published by the WpfHexEditor.Core.LSP engine.
//     Consumed by panels (ErrorList, StatusBar, SymbolBrowser) and plugins.
// ==========================================================

namespace WpfHexEditor.Core.Events;

/// <summary>Published after a document's symbol table is rebuilt by the LSP engine.</summary>
public sealed record LspSymbolTableUpdatedEvent : IDEEventBase
{
    public string FilePath { get; init; } = string.Empty;
}

/// <summary>Published after LSP diagnostics have been evaluated for a document.</summary>
public sealed record LspDiagnosticsUpdatedEvent : IDEEventBase
{
    public string FilePath     { get; init; } = string.Empty;
    public int    ErrorCount   { get; init; }
    public int    WarningCount { get; init; }
}

/// <summary>Published after a full or incremental parse of a document completes.</summary>
public sealed record LspDocumentParsedEvent : IDEEventBase
{
    public string FilePath   { get; init; } = string.Empty;
    public string LanguageId { get; init; } = string.Empty;
    public int    LineCount  { get; init; }
}
