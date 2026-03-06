// ==========================================================
// Project: WpfHexEditor.PluginHost
// File: FocusContextService.cs
// Author: Auto
// Created: 2026-03-06
// Description:
//     Tracks the currently active document and panel in the IDE.
//     MainWindow calls SetActiveDocument / SetActivePanel on each focus change.
//     FocusChanged event is always raised on the WPF Dispatcher thread.
//
// Architecture Notes:
//     Singleton — one instance per App lifetime, shared via IIDEHostContext.
//     No polling — purely event-driven push model.
//     Thread-safe reads; writes must come from UI thread (or are marshalled).
//
// ==========================================================

using System.Windows;
using WpfHexEditor.SDK.Contracts;
using WpfHexEditor.SDK.Contracts.Focus;

namespace WpfHexEditor.PluginHost.Services;

/// <summary>
/// Focus-aware tracking of active document and panel.
/// </summary>
public sealed class FocusContextService : IFocusContextService
{
    /// <inheritdoc />
    public IDocument? ActiveDocument { get; private set; }

    /// <inheritdoc />
    public IPanel? ActivePanel { get; private set; }

    /// <inheritdoc />
    public event EventHandler<FocusChangedEventArgs>? FocusChanged;

    /// <summary>
    /// Called by MainWindow when the active document changes.
    /// </summary>
    public void SetActiveDocument(IDocument? document)
    {
        var previous = ActiveDocument;
        var previousPanel = ActivePanel;

        if (ReferenceEquals(previous, document)) return;

        ActiveDocument = document;
        RaiseFocusChanged(previous, document, previousPanel, ActivePanel);
    }

    /// <summary>
    /// Called by MainWindow when the active panel changes.
    /// </summary>
    public void SetActivePanel(IPanel? panel)
    {
        var previousDocument = ActiveDocument;
        var previousPanel = ActivePanel;

        if (ReferenceEquals(previousPanel, panel)) return;

        ActivePanel = panel;
        RaiseFocusChanged(previousDocument, ActiveDocument, previousPanel, panel);
    }

    private void RaiseFocusChanged(
        IDocument? previousDocument,
        IDocument? activeDocument,
        IPanel? previousPanel,
        IPanel? activePanel)
    {
        var args = new FocusChangedEventArgs
        {
            PreviousDocument = previousDocument,
            ActiveDocument = activeDocument,
            PreviousPanel = previousPanel,
            ActivePanel = activePanel
        };

        if (Application.Current?.Dispatcher is { } d && !d.CheckAccess())
            d.InvokeAsync(() => FocusChanged?.Invoke(this, args));
        else
            FocusChanged?.Invoke(this, args);
    }
}
