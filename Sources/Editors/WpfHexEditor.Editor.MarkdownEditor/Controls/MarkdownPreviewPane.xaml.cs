// ==========================================================
// Project: WpfHexEditor.Editor.MarkdownEditor
// File: Controls/MarkdownPreviewPane.xaml.cs
// Author: Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.6
// Created: 2026-03-19
// Description:
//     Code-behind for the WebView2-based Markdown preview pane.
//     Manages WebView2 initialization, HTML rendering, zoom,
//     and link-click forwarding to the host.
//
// Architecture Notes:
//     - Wraps Microsoft.Web.WebView2.Wpf.WebView2
//     - All rendering calls are async; the host must await them
//     - Uses an isolated user-data folder under %TEMP% to avoid
//       session pollution across multiple editor instances
//     - If the WebView2 runtime is absent the fallback overlay is shown
//       instead of throwing
// ==========================================================

using System.IO;
using System.Windows;
using System.Windows.Controls;
using Microsoft.Web.WebView2.Core;
using WpfHexEditor.Editor.MarkdownEditor.Core.Services;

namespace WpfHexEditor.Editor.MarkdownEditor.Controls;

/// <summary>Actions that can be triggered from the preview-pane context menu.</summary>
public enum MdPreviewContextAction
{
    SourceOnly,
    SplitView,
    PreviewOnly,
    Refresh,
    CycleLayout,
}

/// <summary>
/// WebView2-backed control that renders GitHub-Flavored Markdown as HTML.
/// </summary>
public sealed partial class MarkdownPreviewPane : UserControl
{
    // --- State ------------------------------------------------------------

    private bool   _isInitialized;
    private string _pendingMarkdown = string.Empty;
    private bool   _pendingIsDark;
    private bool   _pendingHasMermaidInit = true;   // used before initialization only

    // Monotonically-increasing render stamp; used to discard stale renders.
    private int    _renderStamp;

    // Incremental-render shell state — shell is written once per (theme × mermaid) combination.
    // Subsequent renders call window.updateMarkdown() via ExecuteScriptAsync instead of Navigate().
    private bool   _shellReady;
    private bool?  _shellIsDark;      // null = shell never loaded
    private bool?  _shellHasMermaid;
    private string? _pendingMdUpdate; // markdown queued for post-navigation injection
    private double  _currentZoom = 1.0;

    // --- Events -----------------------------------------------------------

    /// <summary>
    /// Raised when the user clicks a hyperlink in the preview.
    /// The string argument is the target URL.
    /// </summary>
    public event EventHandler<string>? LinkClicked;

    /// <summary>
    /// Raised by context menu items in the preview pane.
    /// The host (<see cref="MarkdownEditorHost"/>) handles these actions.
    /// </summary>
    public event EventHandler<MdPreviewContextAction>? PreviewContextMenuAction;

    // --- Construction -----------------------------------------------------

    public MarkdownPreviewPane()
    {
        InitializeComponent();
        Loaded += OnLoaded;
    }

    // --- Public API -------------------------------------------------------

    /// <summary>
    /// Initializes the WebView2 environment (idempotent — safe to call multiple times).
    /// Must be called from the UI thread.
    /// </summary>
    public async Task InitializeAsync()
    {
        if (_isInitialized) return;

        try
        {
            var userDataFolder = Path.Combine(
                Path.GetTempPath(), "WpfHexEditor", "WebView2");
            Directory.CreateDirectory(userDataFolder);

            var env = await CoreWebView2Environment.CreateAsync(
                browserExecutableFolder: null,
                userDataFolder: userDataFolder);

            await _webView.EnsureCoreWebView2Async(env);

            _webView.CoreWebView2.WebMessageReceived    += OnWebMessageReceived;
            _webView.CoreWebView2.NavigationCompleted  += OnNavigationCompleted;

            // Disable default context menu and DevTools (cleaner UX)
            _webView.CoreWebView2.Settings.AreDefaultContextMenusEnabled = false;
            _webView.CoreWebView2.Settings.AreDevToolsEnabled            = false;
            _webView.CoreWebView2.Settings.IsStatusBarEnabled            = false;

            _isInitialized = true;

            // Show WebView2, hide loading overlay
            _webView.Visibility       = Visibility.Visible;
            _loadingOverlay.Visibility = Visibility.Collapsed;

            // Render any content that arrived before initialization completed
            if (!string.IsNullOrEmpty(_pendingMarkdown))
                await RenderAsync(_pendingMarkdown, _pendingIsDark, _pendingHasMermaidInit);
        }
        catch (Exception ex) when (IsWebView2RuntimeMissing(ex))
        {
            _loadingOverlay.Visibility = Visibility.Collapsed;
            _fallback.Visibility       = Visibility.Visible;
        }
    }

    /// <summary>
    /// Renders the given Markdown text in the preview pane.
    /// Safe to call before <see cref="InitializeAsync"/> completes — the render
    /// will be queued and executed once initialization finishes.
    /// On the first call (or when theme/mermaid changes) the HTML shell is written to disk
    /// and navigated once.  All subsequent calls update content via
    /// <c>window.updateMarkdown()</c> — no page reload, no disk I/O.
    /// </summary>
    /// <param name="hasMermaid">
    ///   Pass <see langword="false"/> when the source contains no mermaid blocks
    ///   to skip the 2.9 MB mermaid.js bundle.
    /// </param>
    public async Task RenderAsync(string markdownText, bool isDarkTheme, bool hasMermaid = true)
    {
        if (!_isInitialized)
        {
            // Queue for later; InitializeAsync will pick it up
            _pendingMarkdown         = markdownText;
            _pendingIsDark           = isDarkTheme;
            _pendingHasMermaidInit   = hasMermaid;
            return;
        }

        var stamp = System.Threading.Interlocked.Increment(ref _renderStamp);

        // Shell reload required when theme or mermaid availability changes (or first render)
        bool needsShellReload = _shellIsDark != isDarkTheme || _shellHasMermaid != hasMermaid;
        if (needsShellReload)
        {
            _shellReady      = false;
            _shellIsDark     = isDarkTheme;
            _shellHasMermaid = hasMermaid;
            _pendingMdUpdate = markdownText;

            // Build shell page off the UI thread (heavy — ~300 KB StringBuilder)
            var shellHtml = await Task.Run(() =>
                MarkdownRenderService.GetShellPage(isDarkTheme, hasMermaid));
            if (stamp != _renderStamp) return;

            var shellFile = Path.Combine(Path.GetTempPath(), "WpfHexEditor",
                $"md_shell_{Environment.ProcessId}.html");
            Directory.CreateDirectory(Path.GetDirectoryName(shellFile)!);
            await File.WriteAllTextAsync(shellFile, shellHtml, System.Text.Encoding.UTF8);
            if (stamp != _renderStamp) return;

            _webView.CoreWebView2.Navigate(new Uri(shellFile).AbsoluteUri);
            // Content injection happens in OnNavigationCompleted
            return;
        }

        if (!_shellReady)
        {
            // Shell navigation still in progress — keep latest markdown queued
            _pendingMdUpdate = markdownText;
            return;
        }

        // --- Hot path: incremental update — no Navigate, no disk I/O ---
        var escaped = await Task.Run(() =>
            MarkdownRenderService.EscapeMarkdownForJs(markdownText));
        if (stamp != _renderStamp) return;
        await _webView.CoreWebView2.ExecuteScriptAsync(
            $"window.updateMarkdown(\"{escaped}\");");
    }

    // --- Private ----------------------------------------------------------

    private async void OnLoaded(object sender, RoutedEventArgs e)
    {
        Loaded -= OnLoaded;
        try
        {
            await InitializeAsync();
        }
        catch (Exception ex)
        {
            // Prevent unhandled async-void exception from crashing the host.
            System.Diagnostics.Debug.WriteLine($"[MarkdownPreviewPane] Init failed: {ex.Message}");
            _loadingOverlay.Visibility = Visibility.Collapsed;
            _fallback.Visibility       = Visibility.Visible;
        }
    }

    private void OnWebMessageReceived(object? sender, CoreWebView2WebMessageReceivedEventArgs e)
    {
        try
        {
            var raw = e.TryGetWebMessageAsString();
            if (string.IsNullOrEmpty(raw)) return;

            // Expect: { "type": "link", "href": "..." }
            using var doc = System.Text.Json.JsonDocument.Parse(raw);
            var root = doc.RootElement;
            if (root.TryGetProperty("type", out var typeProp) &&
                typeProp.GetString() == "link" &&
                root.TryGetProperty("href", out var hrefProp))
            {
                var href = hrefProp.GetString();
                if (!string.IsNullOrEmpty(href))
                    LinkClicked?.Invoke(this, href);
            }
        }
        catch
        {
            // Ignore malformed messages from the web content
        }
    }

    private static bool IsWebView2RuntimeMissing(Exception ex)
        => ex is WebView2RuntimeNotFoundException ||
           ex.Message.Contains("WebView2", StringComparison.OrdinalIgnoreCase) ||
           ex.InnerException is WebView2RuntimeNotFoundException;

    private async void OnNavigationCompleted(object? sender,
        CoreWebView2NavigationCompletedEventArgs e)
    {
        _shellReady = true;

        // Inject any markdown that was queued while navigation was in progress
        var md = _pendingMdUpdate;
        _pendingMdUpdate = null;
        if (md is null) return;

        var escaped = await Task.Run(() =>
            MarkdownRenderService.EscapeMarkdownForJs(md));
        await _webView.CoreWebView2.ExecuteScriptAsync(
            $"window.updateMarkdown(\"{escaped}\");");

        // Restore zoom level after shell reload
        if (_currentZoom != 1.0)
            SetZoom(_currentZoom);
    }

    // --- Context menu handlers --------------------------------------------

    /// <summary>
    /// Applies an HTML zoom level to the preview content.
    /// Zoom is persisted in <c>_currentZoom</c> and re-applied after shell reloads.
    /// </summary>
    /// <param name="zoom">Zoom factor (e.g. 1.5 = 150 %).  Clamped to [0.5, 3.0].</param>
    public void SetZoom(double zoom)
    {
        _currentZoom = Math.Clamp(zoom, 0.5, 3.0);
        if (!_isInitialized || !_shellReady) return;
        var pct = (_currentZoom * 100).ToString("F0",
            System.Globalization.CultureInfo.InvariantCulture);
        _webView.CoreWebView2.ExecuteScriptAsync(
            $"document.body.style.zoom='{pct}%';");
    }

    private void OnCtxSourceOnly(object sender, RoutedEventArgs e)
        => PreviewContextMenuAction?.Invoke(this, MdPreviewContextAction.SourceOnly);

    private void OnCtxSplitView(object sender, RoutedEventArgs e)
        => PreviewContextMenuAction?.Invoke(this, MdPreviewContextAction.SplitView);

    private void OnCtxPreviewOnly(object sender, RoutedEventArgs e)
        => PreviewContextMenuAction?.Invoke(this, MdPreviewContextAction.PreviewOnly);

    private void OnCtxRefresh(object sender, RoutedEventArgs e)
        => PreviewContextMenuAction?.Invoke(this, MdPreviewContextAction.Refresh);

    private void OnCtxCycleLayout(object sender, RoutedEventArgs e)
        => PreviewContextMenuAction?.Invoke(this, MdPreviewContextAction.CycleLayout);
}
