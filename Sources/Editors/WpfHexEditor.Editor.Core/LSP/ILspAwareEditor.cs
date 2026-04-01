// ==========================================================
// Project: WpfHexEditor.Editor.Core
// File: LSP/ILspAwareEditor.cs
// Author: Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.6
// Created: 2026-03-23
// Description:
//     Opt-in interface for editors that can consume an ILspClient to invoke
//     features such as Code Actions (Ctrl+.) and Rename (F2).
//     Implemented by CodeEditor; injected by LspDocumentBridgeService.
//
// Architecture Notes:
//     Pattern: Marker / Strategy injection — keeps LspDocumentBridgeService
//     decoupled from the concrete CodeEditor type.
// ==========================================================

namespace WpfHexEditor.Editor.Core.LSP;

/// <summary>
/// Opt-in interface for editors that want to invoke LSP features
/// (code actions, rename) directly on an <see cref="ILspClient"/>.
/// </summary>
public interface ILspAwareEditor
{
    /// <summary>
    /// Injects (or clears) the active LSP client for this editor instance.
    /// Called by <c>LspDocumentBridgeService</c> after the bridge is established.
    /// </summary>
    void SetLspClient(ILspClient? client);

    /// <summary>
    /// Provides the document manager so the editor can apply workspace-wide edits
    /// (e.g. rename touching multiple files). Called once after construction.
    /// </summary>
    void SetDocumentManager(WpfHexEditor.Editor.Core.Documents.IDocumentManager manager);
}
