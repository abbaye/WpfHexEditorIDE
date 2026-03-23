// ==========================================================
// Project: WpfHexEditor.Editor.CodeEditor
// File: Controls/LspBreadcrumbBar.cs
// Author: Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.6
// Created: 2026-03-23
// Description:
//     A compact breadcrumb bar shown above the code editor when an LSP client
//     is active. Displays "Namespace › TypeName › MethodName" for the current
//     caret position by calling ILspClient.DocumentSymbolsAsync.
//
// Architecture Notes:
//     Observer — subscribes to CodeEditor.CaretMoved (200ms debounce).
//     Hidden (Visibility.Collapsed) when no LSP client is set so it takes
//     no space in the split-host layout when LSP is not configured.
//     Theme tokens: BC_Background, BC_Foreground, BC_SeparatorForeground,
//                   BC_HoverBackground.
// ==========================================================

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using WpfHexEditor.Editor.Core.LSP;

namespace WpfHexEditor.Editor.CodeEditor.Controls;

/// <summary>
/// LSP-powered breadcrumb bar: <c>Namespace › TypeName › MemberName</c>.
/// Shown only when an <see cref="ILspClient"/> is active for the host editor.
/// </summary>
public sealed class LspBreadcrumbBar : Border
{
    // ── Child controls ────────────────────────────────────────────────────────
    private readonly StackPanel _crumbPanel;
    private readonly TextBlock  _crumbText;

    // ── State ─────────────────────────────────────────────────────────────────
    private ILspClient?          _lspClient;
    private CodeEditor?          _editor;
    private string?              _filePath;
    private readonly DispatcherTimer _debounce;
    private CancellationTokenSource? _cts;

    private static readonly string Separator = " › ";

    // ── Constructor ───────────────────────────────────────────────────────────

    public LspBreadcrumbBar()
    {
        Height = 22;
        Padding = new Thickness(4, 0, 4, 0);
        Visibility = Visibility.Collapsed;  // hidden until LSP is set

        SetResourceReference(BackgroundProperty, "BC_Background");
        SetResourceReference(BorderBrushProperty, "CE_NavBarBorder");
        BorderThickness = new Thickness(0, 0, 0, 1);

        _crumbText = new TextBlock
        {
            VerticalAlignment = VerticalAlignment.Center,
            TextTrimming       = TextTrimming.CharacterEllipsis,
            FontSize           = 11,
        };
        _crumbText.SetResourceReference(TextBlock.ForegroundProperty, "BC_Foreground");

        _crumbPanel = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            VerticalAlignment = VerticalAlignment.Center,
        };
        _crumbPanel.Children.Add(_crumbText);

        Child = _crumbPanel;

        _debounce = new DispatcherTimer(DispatcherPriority.Background)
        {
            Interval = TimeSpan.FromMilliseconds(200)
        };
        _debounce.Tick += OnDebounce;
    }

    // ── Public API ────────────────────────────────────────────────────────────

    /// <summary>Attaches the bar to a code editor. Call once.</summary>
    public void Attach(CodeEditor editor, string? filePath)
    {
        if (_editor is not null)
            _editor.CaretMoved -= OnCaretMoved;

        _editor   = editor;
        _filePath = filePath;

        if (_editor is not null)
            _editor.CaretMoved += OnCaretMoved;
    }

    /// <summary>Updates the file path (called when user opens/saves a different file).</summary>
    public void SetFilePath(string? filePath)
    {
        _filePath = filePath;
        RequestRefresh();
    }

    /// <summary>Injects or clears the LSP client. Bar becomes visible when non-null.</summary>
    public void SetLspClient(ILspClient? client)
    {
        _lspClient = client;
        Visibility = client is not null ? Visibility.Visible : Visibility.Collapsed;

        if (client is not null)
            RequestRefresh();
        else
            _crumbText.Text = string.Empty;
    }

    // ── Private helpers ───────────────────────────────────────────────────────

    private void OnCaretMoved(object? sender, EventArgs e) => RequestRefresh();

    private void RequestRefresh()
    {
        _debounce.Stop();
        _debounce.Start();
    }

    private async void OnDebounce(object? sender, EventArgs e)
    {
        _debounce.Stop();

        if (_lspClient?.IsInitialized != true || _filePath is null || _editor is null)
        {
            _crumbText.Text = string.Empty;
            return;
        }

        _cts?.Cancel();
        _cts = new CancellationTokenSource(TimeSpan.FromSeconds(2));

        try
        {
            var symbols = await _lspClient.DocumentSymbolsAsync(_filePath, _cts.Token)
                .ConfigureAwait(true);

            var crumbs = ResolveCrumbs(symbols, _editor.CursorPosition.Line);
            RenderCrumbs(crumbs);
        }
        catch (OperationCanceledException) { }
        catch { _crumbText.Text = string.Empty; }
    }

    private static List<string> ResolveCrumbs(IReadOnlyList<LspDocumentSymbol> symbols, int caretLine)
    {
        // Find all symbols whose range contains the caret line, ordered by specificity.
        var enclosing = symbols
            .Where(s => s.StartLine <= caretLine)
            .OrderBy(s => s.StartLine)
            .ToList();

        if (enclosing.Count == 0)
            return [];

        // Build path: namespace → type → member (max 3 crumbs).
        var path = new List<string>();

        // Try to find a namespace container.
        var ns = enclosing.FirstOrDefault(s =>
            s.Kind.Equals("namespace", StringComparison.OrdinalIgnoreCase));
        if (ns is not null)
            path.Add(ns.Name);

        // Type-level symbol.
        var type = enclosing.LastOrDefault(s =>
            s.Kind is "class" or "interface" or "struct" or "enum");
        if (type is not null && type != ns)
            path.Add(type.Name);

        // Member-level symbol (method, property, field).
        var member = enclosing.LastOrDefault(s =>
            s.Kind is "method" or "function" or "constructor" or "property" or "field");
        if (member is not null && member != type)
            path.Add(member.Name);

        return path;
    }

    private void RenderCrumbs(List<string> crumbs)
    {
        if (crumbs.Count == 0)
        {
            _crumbText.Text = string.Empty;
            return;
        }

        // Build the inline "A › B › C" text.  The last segment is bold.
        _crumbPanel.Children.Clear();

        for (int i = 0; i < crumbs.Count; i++)
        {
            if (i > 0)
            {
                var sep = new TextBlock
                {
                    Text              = Separator,
                    VerticalAlignment = VerticalAlignment.Center,
                    FontSize          = 11,
                };
                sep.SetResourceReference(TextBlock.ForegroundProperty, "BC_SeparatorForeground");
                _crumbPanel.Children.Add(sep);
            }

            var lbl = new TextBlock
            {
                Text              = crumbs[i],
                VerticalAlignment = VerticalAlignment.Center,
                FontSize          = 11,
                FontWeight        = i == crumbs.Count - 1 ? FontWeights.SemiBold : FontWeights.Normal,
                Cursor            = Cursors.Hand,
                Tag               = crumbs[i],
            };
            lbl.SetResourceReference(TextBlock.ForegroundProperty, "BC_Foreground");

            // Hover colour change via MouseEnter/Leave.
            lbl.MouseEnter += (_, _) => lbl.SetResourceReference(BackgroundProperty, "BC_HoverBackground");
            lbl.MouseLeave += (_, _) => lbl.Background = Brushes.Transparent;

            _crumbPanel.Children.Add(lbl);
        }
    }
}
