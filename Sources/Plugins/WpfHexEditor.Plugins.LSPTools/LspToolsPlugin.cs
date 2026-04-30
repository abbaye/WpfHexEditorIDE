// ==========================================================
// Project: WpfHexEditor.Plugins.LSPTools
// File: LspToolsPlugin.cs
// Author: Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.6
// Created: 2026-03-30
// Description:
//     Plugin entry point for LSP Tools — provides Call Hierarchy and Type Hierarchy
//     panels powered by the Language Server Protocol.
//
// Architecture Notes:
//     Subscribes to CallHierarchyReadyEvent / TypeHierarchyReadyEvent published by
//     the IDE host (MainWindow) when the user triggers Shift+Alt+H / Ctrl+F12.
//     Panels are registered once at init and shown on demand via IUIRegistry.ShowPanel.
//     Navigation routes through IDocumentHostService (standard IDE open+scroll path).
// ==========================================================

using System;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using WpfHexEditor.Plugins.LSPTools.Properties;
using WpfHexEditor.SDK.Contracts;
using WpfHexEditor.SDK.Descriptors;
using WpfHexEditor.SDK.Events;
using WpfHexEditor.SDK.Models;
using WpfHexEditor.Plugins.LSPTools.Commands;
using WpfHexEditor.Plugins.LSPTools.Panels;

namespace WpfHexEditor.Plugins.LSPTools;

/// <summary>Plugin providing Call Hierarchy and Type Hierarchy panels via LSP.</summary>
public sealed class LspToolsPlugin : IWpfHexEditorPlugin
{
    // ── Identity ──────────────────────────────────────────────────────────────

    public string  Id      => "WpfHexEditor.Plugins.LSPTools";
    public string  Name    => "LSP Tools";
    public Version Version => new(1, 0, 0);

    public PluginCapabilities Capabilities => new()
    {
        AccessCodeEditor         = true,
        RegisterMenus            = true,
        WriteOutput              = true,
        RegisterTerminalCommands = true,
    };

    // ── UI IDs ────────────────────────────────────────────────────────────────

    private const string CallHierarchyPanelUiId = "WpfHexEditor.Plugins.LSPTools.Panel.CallHierarchy";
    private const string TypeHierarchyPanelUiId = "WpfHexEditor.Plugins.LSPTools.Panel.TypeHierarchy";

    // ── State ─────────────────────────────────────────────────────────────────

    private IIDEHostContext?   _context;
    private CallHierarchyPanel? _callPanel;
    private TypeHierarchyPanel? _typePanel;

    private IDisposable? _callHierarchySub;
    private IDisposable? _typeHierarchySub;

    // ── Lifecycle ─────────────────────────────────────────────────────────────

    public Task InitializeAsync(IIDEHostContext context, CancellationToken ct = default)
    {
        _context = context;

        // ── Build panels ──────────────────────────────────────────────────────
        _callPanel = new CallHierarchyPanel();
        _callPanel.NavigateRequested += OnNavigateRequested;

        _typePanel = new TypeHierarchyPanel();
        _typePanel.NavigateRequested += OnNavigateRequested;

        // ── Register dockable panels ──────────────────────────────────────────
        context.UIRegistry.RegisterPanel(
            CallHierarchyPanelUiId,
            _callPanel,
            Id,
            new PanelDescriptor
            {
                Title           = LspToolsResources.LspTools_CallHierarchy,
                DefaultDockSide = "Bottom",
                DefaultAutoHide = false,
                CanClose        = true,
                PreferredHeight = 220,
            });

        context.UIRegistry.RegisterPanel(
            TypeHierarchyPanelUiId,
            _typePanel,
            Id,
            new PanelDescriptor
            {
                Title           = LspToolsResources.LspTools_TypeHierarchy,
                DefaultDockSide = "Bottom",
                DefaultAutoHide = false,
                CanClose        = true,
                PreferredHeight = 220,
            });

        // ── Subscribe to IDE hierarchy events ─────────────────────────────────
        _callHierarchySub = context.EventBus.Subscribe<CallHierarchyReadyEvent>(OnCallHierarchyReady);
        _typeHierarchySub = context.EventBus.Subscribe<TypeHierarchyReadyEvent>(OnTypeHierarchyReady);

        // ── Terminal commands ─────────────────────────────────────────────────
        context.Terminal.RegisterCommand(new LspCallHierarchyCommand());
        context.Terminal.RegisterCommand(new LspTypeHierarchyCommand());

        return Task.CompletedTask;
    }

    public Task ShutdownAsync(CancellationToken ct = default)
    {
        _callHierarchySub?.Dispose();
        _typeHierarchySub?.Dispose();

        if (_callPanel is not null)
            _callPanel.NavigateRequested -= OnNavigateRequested;
        if (_typePanel is not null)
            _typePanel.NavigateRequested -= OnNavigateRequested;

        _context?.Terminal.UnregisterCommand("lsp-call-hierarchy");
        _context?.Terminal.UnregisterCommand("lsp-type-hierarchy");

        _callPanel = null;
        _typePanel = null;
        _context   = null;

        return Task.CompletedTask;
    }

    // ── Event handlers ────────────────────────────────────────────────────────

    private void OnCallHierarchyReady(CallHierarchyReadyEvent e)
    {
        if (_callPanel is null || _context is null) return;

        // Wire LSP callbacks on first call (or re-wire when client changes).
        if (e.LspClient is not null)
        {
            _callPanel.SetCallbacks(
                getIncoming: item => e.LspClient.GetIncomingCallsAsync(item),
                getOutgoing: item => e.LspClient.GetOutgoingCallsAsync(item));
        }

        _callPanel.Refresh(e.Items, e.SymbolName);
        _context.UIRegistry.ShowPanel(CallHierarchyPanelUiId);
    }

    private void OnTypeHierarchyReady(TypeHierarchyReadyEvent e)
    {
        if (_typePanel is null || _context is null) return;

        if (e.LspClient is not null)
        {
            _typePanel.SetCallbacks(
                getSupertypes: item => e.LspClient.GetSupertypesAsync(item),
                getSubtypes:   item => e.LspClient.GetSubtypesAsync(item));
        }

        _typePanel.Refresh(e.Items, e.SymbolName);
        _context.UIRegistry.ShowPanel(TypeHierarchyPanelUiId);
    }

    private void OnNavigateRequested(string filePath, int lineOneBased)
    {
        if (_context is null) return;
        _context.DocumentHost.ActivateAndNavigateTo(filePath, lineOneBased, 1);
    }
}
