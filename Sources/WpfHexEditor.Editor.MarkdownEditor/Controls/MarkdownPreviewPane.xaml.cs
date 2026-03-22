// ==========================================================
// Project: WpfHexEditor.Editor.MarkdownEditor
// File: Controls/MarkdownPreviewPane.xaml.cs
// Author: Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.6
// Created: 2026-03-19
// Description:
//     Code-behind for the WebView2-based Markdown preview pane.
//     Manages WebView2 initialization, HTML rendering, scroll sync,
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
    ToggleSyncScroll,
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

    // Monotonically-increasing render stamp; used to discard stale renders.
    private int    _renderStamp;

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

            _webView.CoreWebView2.WebMessageReceived += OnWebMessageReceived;

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
                await RenderAsync(_pendingMarkdown, _pendingIsDark, _pendingHasMermaid);
        }
        catch (Exception ex) when (IsWebView2RuntimeMissing(ex))
        {
            _loadingOverlay.Visibility = Visibility.Collapsed;
            _fallback.Visibility       = Visibility.Visible;
        }
    }

    // Cached mermaid detection flag — updated each render call
    private bool _pendingHasMermaid = true;

    /// <summary>
    /// Renders the given Markdown text as a full HTML page in the preview pane.
    /// Safe to call before <see cref="InitializeAsync"/> completes — the render
    /// will be queued and executed once initialization finishes.
    /// </summary>
    /// <param name="hasMermaid">
    ///   Pass <see langword="false"/> when the source contains no mermaid blocks
    ///   to skip the 2.9 MB mermaid.js bundle injection.
    /// </param>
    public async Task RenderAsync(string markdownText, bool isDarkTheme, bool hasMermaid = true)
    {
        if (!_isInitialized)
        {
            // Queue for later; InitializeAsync will pick it up
            _pendingMarkdown    = markdownText;
            _pendingIsDark      = isDarkTheme;
            _pendingHasMermaid  = hasMermaid;
            return;
        }

        var stamp = System.Threading.Interlocked.Increment(ref _renderStamp);

        // Build HTML off the UI thread to keep it responsive during large docs
        var html = await Task.Run(() => MarkdownRenderService.GetHtmlPage(markdownText, isDarkTheme, hasMermaid));

        // Discard if a newer render was requested while we were working
        if (stamp != _renderStamp) return;

        // NavigateToString has a ~2 MB size limit and throws ArgumentException when
        // bundled assets are inlined (mermaid.js alone is ~2.9 MB). Write to a temp
        // file instead and navigate via file:// URI to bypass the size restriction.
        var tempDir  = Path.Combine(Path.GetTempPath(), "WpfHexEditor");
        Directory.CreateDirectory(tempDir);
        var tempFile = Path.Combine(tempDir, $"md_preview_{Environment.ProcessId}.html");
        await File.WriteAllTextAsync(tempFile, html, System.Text.Encoding.UTF8);
        _webView.CoreWebView2.Navigate(new Uri(tempFile).AbsoluteUri);
    }

    /// <summary>
    /// Scrolls the preview to the given vertical percentage (0.0 – 1.0).
    /// Used by the sync-scroll feature in <see cref="MarkdownEditorHost"/>.
    /// </summary>
    public void ScrollToPercent(double percent)
    {
        if (!_isInitialized) return;

        var pct = Math.Clamp(percent, 0.0, 1.0).ToString("F4",
            System.Globalization.CultureInfo.InvariantCulture);

        _webView.CoreWebView2.ExecuteScriptAsync(
            $"if(window.scrollToPercent)window.scrollToPercent({pct});");
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

    // --- Context menu sync ------------------------------------------------

    /// <summary>
    /// Applies an HTML zoom level to the preview content.
    /// </summary>
    /// <param name="zoom">Zoom factor (e.g. 1.5 = 150 %).  Clamped to [0.5, 3.0].</param>
    public void SetZoom(double zoom)
    {
        if (!_isInitialized) return;
        var pct = (Math.Clamp(zoom, 0.5, 3.0) * 100).ToString("F0",
            System.Globalization.CultureInfo.InvariantCulture);
        _webView.CoreWebView2.ExecuteScriptAsync(
            $"document.body.style.zoom='{pct}%';");
    }

    /// <summary>
    /// Updates the Sync Scroll checkmark from the host when the panel opens.
    /// </summary>
    public void SetSyncScrollChecked(bool value)
    {
        if (_ctxSyncScroll is not null)
            _ctxSyncScroll.IsChecked = value;
    }

    // --- Context menu handlers --------------------------------------------

    private void OnCtxSourceOnly(object sender, RoutedEventArgs e)
        => PreviewContextMenuAction?.Invoke(this, MdPreviewContextAction.SourceOnly);

    private void OnCtxSplitView(object sender, RoutedEventArgs e)
        => PreviewContextMenuAction?.Invoke(this, MdPreviewContextAction.SplitView);

    private void OnCtxPreviewOnly(object sender, RoutedEventArgs e)
        => PreviewContextMenuAction?.Invoke(this, MdPreviewContextAction.PreviewOnly);

    private void OnCtxRefresh(object sender, RoutedEventArgs e)
        => PreviewContextMenuAction?.Invoke(this, MdPreviewContextAction.Refresh);

    private void OnCtxSyncScroll(object sender, RoutedEventArgs e)
        => PreviewContextMenuAction?.Invoke(this, MdPreviewContextAction.ToggleSyncScroll);

    private void OnCtxCycleLayout(object sender, RoutedEventArgs e)
        => PreviewContextMenuAction?.Invoke(this, MdPreviewContextAction.CycleLayout);
}
