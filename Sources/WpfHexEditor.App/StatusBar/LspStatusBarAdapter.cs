// ==========================================================
// Project: WpfHexEditor.App
// File: StatusBar/LspStatusBarAdapter.cs
// Author: Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.6
// Created: 2026-03-23
// Description:
//     Adapts LspDocumentBridgeService.ServerStateChanged events to the
//     StatusBarItem that MainWindow shows for the active LSP server.
//
//     State → display:
//       Idle        → hidden (IsVisible = false)
//       Connecting  → "◌ Connecting…"  (LSP_ConnectingDot token)
//       Ready       → "● {ServerName}" (LSP_ReadyDot token)
//       Error       → "✕ LSP Error"    (LSP_ErrorDot token); click → open options
//
// Architecture Notes:
//     Pattern: Observer / Adapter.
//     Must be created and used on the WPF Dispatcher thread.
// ==========================================================

using System.Windows;
using System.Windows.Media;
using WpfHexEditor.App.Services;
using WpfHexEditor.Editor.Core;

namespace WpfHexEditor.App.Services;

/// <summary>
/// Adapts <see cref="LspDocumentBridgeService"/> state-change events
/// to a single <see cref="StatusBarItem"/> shown in the MainWindow status bar.
/// </summary>
public sealed class LspStatusBarAdapter : IDisposable
{
    private readonly LspDocumentBridgeService _bridgeService;
    private readonly Action?                  _onErrorClick;

    /// <summary>
    /// The status bar item whose Label/Value/IsVisible reflect LSP server state.
    /// Bind this in the MainWindow status-bar template.
    /// </summary>
    public StatusBarItem Item { get; } = new StatusBarItem
    {
        Label     = string.Empty,
        Value     = string.Empty,
        IsVisible = false,
        Tooltip   = "Language Server Protocol",
    };

    // ── Construction ──────────────────────────────────────────────────────────

    /// <param name="bridgeService">The running bridge service.</param>
    /// <param name="onErrorClick">Optional callback invoked when the user clicks
    /// the error indicator (e.g. open the LSP options page).</param>
    internal LspStatusBarAdapter(LspDocumentBridgeService bridgeService, Action? onErrorClick = null)
    {
        _bridgeService = bridgeService ?? throw new ArgumentNullException(nameof(bridgeService));
        _onErrorClick  = onErrorClick;

        _bridgeService.ServerStateChanged += OnServerStateChanged;
    }

    // ── Event handler ─────────────────────────────────────────────────────────

    private void OnServerStateChanged(object? sender, LspServerStateChangedEventArgs e)
    {
        // Ensure we're on the UI thread (bridge service already dispatches to it).
        switch (e.State)
        {
            case LspServerState.Idle:
                Item.IsVisible = false;
                Item.Value     = string.Empty;
                break;

            case LspServerState.Connecting:
                Item.IsVisible = true;
                Item.Label     = "◌";
                Item.Value     = $"Connecting… ({e.ServerName})";
                Item.Tooltip   = $"LSP: {e.LanguageId} server is starting";
                ApplyDotColor("LSP_ConnectingDot");
                break;

            case LspServerState.Ready:
                Item.IsVisible = true;
                Item.Label     = "●";
                Item.Value     = e.ServerName ?? e.LanguageId;
                Item.Tooltip   = $"LSP: {e.ServerName} ready for {e.LanguageId}";
                ApplyDotColor("LSP_ReadyDot");
                break;

            case LspServerState.Error:
                Item.IsVisible = true;
                Item.Label     = "✕";
                Item.Value     = "LSP Error";
                Item.Tooltip   = e.ErrorMessage ?? "Language server failed to start. Click to open settings.";
                ApplyDotColor("LSP_ErrorDot");

                // Add a click-to-open-options choice if not already present.
                if (Item.Choices.Count == 0 && _onErrorClick is not null)
                {
                    Item.Choices.Add(new StatusBarChoice
                    {
                        DisplayName = "Open LSP Settings…",
                        Command     = new RelayActionCommand(_onErrorClick),
                    });
                }
                break;
        }
    }

    private static void ApplyDotColor(string tokenKey)
    {
        // Resolve the theme brush lazily.  No-op if the resource isn't defined yet.
        _ = tokenKey;  // Referenced by token name; actual binding done in XAML / DataTemplate
    }

    // ── IDisposable ───────────────────────────────────────────────────────────

    public void Dispose()
        => _bridgeService.ServerStateChanged -= OnServerStateChanged;

    // ── Inner helpers ─────────────────────────────────────────────────────────

    /// <summary>Minimal ICommand wrapper around an Action for the status bar choice.</summary>
    private sealed class RelayActionCommand : System.Windows.Input.ICommand
    {
        private readonly Action _action;
        public RelayActionCommand(Action action) => _action = action;
        public bool CanExecute(object? p) => true;
        public void Execute(object? p) => _action();
        public event EventHandler? CanExecuteChanged { add { } remove { } }
    }
}
