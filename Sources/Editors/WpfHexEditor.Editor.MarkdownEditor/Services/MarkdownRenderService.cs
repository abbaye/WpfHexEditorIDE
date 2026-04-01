// ==========================================================
// Project: WpfHexEditor.Editor.MarkdownEditor
// File: Services/MarkdownRenderService.cs
// Author: Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.6
// Created: 2026-03-19
// Description:
//     Builds a complete, self-contained HTML document from a Markdown
//     string. All JS and CSS assets are loaded from embedded resources
//     and inlined into the page — no internet access required at runtime.
//
//     Rendered features:
//       - GitHub-Flavored Markdown (tables, strikethrough, task lists)
//       - Mermaid diagrams (```mermaid blocks)
//       - Syntax highlighting for fenced code blocks (highlight.js)
//       - Emoji shortcodes (:smile: → 😄)
//       - Dark / light theme selection driven by IDE theme
//       - Line anchors for sync-scroll (data-line attributes on headings)
//
// Architecture Notes:
//     Singleton-style lazy loading: assets are loaded once from the
//     assembly's embedded resources and cached in static fields.
//     Thread-safe via Lazy<T>.
// ==========================================================

using System.IO;
using System.Reflection;
using System.Text;

namespace WpfHexEditor.Editor.MarkdownEditor.Services;

/// <summary>
/// Produces a self-contained HTML page from Markdown text with GitHub-flavored
/// rendering, Mermaid diagrams, code highlighting, and emoji support.
/// </summary>
public static class MarkdownRenderService
{
    // --- Embedded resource cache (loaded once, lazy + thread-safe) --------

    private static readonly Lazy<string> _markedJs        = Load("marked.min.js");
    private static readonly Lazy<string> _mermaidJs       = Load("mermaid.min.js");
    private static readonly Lazy<string> _highlightJs     = Load("highlight.min.js");
    private static readonly Lazy<string> _hljsDarkCss     = Load("highlight-github-dark.min.css");
    private static readonly Lazy<string> _hljsLightCss    = Load("highlight-github.min.css");
    private static readonly Lazy<string> _ghDarkCss       = Load("github-markdown-dark.css");
    private static readonly Lazy<string> _ghLightCss      = Load("github-markdown.css");
    private static readonly Lazy<string> _emojiJs         = Load("emoji.js");

    // --- Public API -------------------------------------------------------

    /// <summary>
    /// Builds a complete HTML document for the given Markdown text.
    /// </summary>
    /// <param name="markdownText">Raw Markdown source.</param>
    /// <param name="isDarkTheme">
    ///   When <see langword="true"/> uses the GitHub dark stylesheet;
    ///   otherwise uses the light stylesheet.
    /// </param>
    /// <param name="hasMermaid">
    ///   When <see langword="false"/> the 2.9 MB mermaid.js bundle is omitted
    ///   entirely. Pass <see langword="true"/> only when the source contains
    ///   a <c>```mermaid</c> fenced block.
    /// </param>
    public static string GetHtmlPage(string markdownText, bool isDarkTheme, bool hasMermaid = true)
    {
        var ghCss        = isDarkTheme ? _ghDarkCss.Value    : _ghLightCss.Value;
        var hljsCss      = isDarkTheme ? _hljsDarkCss.Value  : _hljsLightCss.Value;
        var bodyBg       = isDarkTheme ? "#0d1117" : "#ffffff";
        var bodyColor    = isDarkTheme ? "#c9d1d9" : "#24292f";
        var mermaidTheme = isDarkTheme ? "dark" : "default";

        // Escape markdown for JSON embedding
        var mdEscaped  = EscapeForJsString(markdownText);

        var sb = new StringBuilder(markdownText.Length * 4);
        sb.AppendLine("<!DOCTYPE html>");
        sb.AppendLine("<html lang=\"en\">");
        sb.AppendLine("<head>");
        sb.AppendLine("  <meta charset=\"UTF-8\">");
        sb.AppendLine("  <meta name=\"viewport\" content=\"width=device-width, initial-scale=1\">");
        sb.AppendLine("  <title>Preview</title>");
        sb.AppendLine();

        // GitHub Markdown CSS
        sb.AppendLine("  <style>");
        sb.AppendLine(ghCss);
        sb.AppendLine("  </style>");

        // Highlight.js CSS
        sb.AppendLine("  <style>");
        sb.AppendLine(hljsCss);
        sb.AppendLine("  </style>");

        // Body / layout overrides
        sb.AppendLine("  <style>");
        sb.AppendLine($"    body {{ background-color: {bodyBg}; color: {bodyColor}; margin: 0; padding: 0; }}");
        sb.AppendLine("    .markdown-body {");
        sb.AppendLine("      box-sizing: border-box;");
        sb.AppendLine("      min-width: 200px;");
        sb.AppendLine("      max-width: 980px;");
        sb.AppendLine("      margin: 0 auto;");
        sb.AppendLine("      padding: 24px 32px;");
        sb.AppendLine("    }");
        sb.AppendLine("    @media (max-width: 767px) { .markdown-body { padding: 15px; } }");
        // Task list checkboxes (GitHub style)
        sb.AppendLine("    .task-list-item { list-style-type: none; }");
        sb.AppendLine("    .task-list-item input { margin: 0 0.2em 0.25em -1.4em; vertical-align: middle; }");
        // Emoji span
        sb.AppendLine("    .emoji { font-style: normal; }");
        // Mermaid diagram container
        sb.AppendLine("    .mermaid { margin: 1em 0; }");
        sb.AppendLine("  </style>");
        sb.AppendLine("</head>");
        sb.AppendLine("<body>");
        sb.AppendLine("  <article class=\"markdown-body\" id=\"content\">");
        sb.AppendLine("  </article>");
        sb.AppendLine();

        // marked.js (GFM parser)
        sb.AppendLine("  <script>");
        sb.AppendLine(_markedJs.Value);
        sb.AppendLine("  </script>");

        // emoji.js extension (registers with marked)
        sb.AppendLine("  <script>");
        sb.AppendLine(_emojiJs.Value);
        sb.AppendLine("  </script>");

        // highlight.js
        sb.AppendLine("  <script>");
        sb.AppendLine(_highlightJs.Value);
        sb.AppendLine("  </script>");

        // mermaid.js — only injected when a ```mermaid block is present in the source
        if (hasMermaid)
        {
            sb.AppendLine("  <script>");
            sb.AppendLine(_mermaidJs.Value);
            sb.AppendLine("  </script>");
        }

        // Render script
        sb.AppendLine("  <script>");
        sb.AppendLine("  (function() {");

        // Configure marked with GFM + extensions
        sb.AppendLine("    marked.setOptions({");
        sb.AppendLine("      gfm: true,");
        sb.AppendLine("      breaks: false,");
        sb.AppendLine("      pedantic: false,");
        sb.AppendLine("    });");
        sb.AppendLine();

        // Configure mermaid (only when bundle was injected)
        if (hasMermaid)
            sb.AppendLine($"    mermaid.initialize({{ startOnLoad: false, theme: '{mermaidTheme}', securityLevel: 'loose' }});");
        sb.AppendLine();

        // Custom renderer: intercept fenced code blocks to detect mermaid
        sb.AppendLine("    const renderer = new marked.Renderer();");
        sb.AppendLine("    renderer.code = function(code, lang) {");
        // marked v9+ passes a single token object {text, lang, ...}; older versions pass (string, string).
        // Always prefer code.lang when code is an object (v9+ token form).
        sb.AppendLine("      const isToken = typeof code === 'object' && code !== null;");
        sb.AppendLine("      const language = isToken ? (code.lang || '') : (lang || '');");
        sb.AppendLine("      const text = isToken ? (code.text || '') : (code || '');");
        sb.AppendLine("      if (language === 'mermaid') {");
        sb.AppendLine("        return '<div class=\"mermaid\">' + text + '</div>';");
        sb.AppendLine("      }");
        sb.AppendLine("      const validLang = language && hljs.getLanguage(language);");
        sb.AppendLine("      const highlighted = validLang");
        sb.AppendLine("        ? hljs.highlight(text, { language: language }).value");
        sb.AppendLine("        : hljs.highlightAuto(text).value;");
        sb.AppendLine("      const cls = validLang ? ' class=\"hljs language-' + language + '\"' : ' class=\"hljs\"';");
        sb.AppendLine("      return '<pre><code' + cls + '>' + highlighted + '</code></pre>';");
        sb.AppendLine("    };");
        sb.AppendLine();

        // GFM task-list renderer override
        sb.AppendLine("    renderer.listitem = function(item) {");
        sb.AppendLine("      const text = (typeof item === 'object' && item) ? item.text : item;");
        sb.AppendLine("      const task = (typeof item === 'object' && item) ? item.task : false;");
        sb.AppendLine("      const checked = (typeof item === 'object' && item) ? item.checked : false;");
        sb.AppendLine("      if (task) {");
        sb.AppendLine("        const chk = checked ? ' checked' : '';");
        sb.AppendLine("        return '<li class=\"task-list-item\"><input type=\"checkbox\" disabled' + chk + '> ' + text + '</li>\\n';");
        sb.AppendLine("      }");
        sb.AppendLine("      return '<li>' + text + '</li>\\n';");
        sb.AppendLine("    };");
        sb.AppendLine();

        // Render markdown
        sb.AppendLine($"    const md = \"{mdEscaped}\";");
        sb.AppendLine("    const html = marked.parse(md, { renderer: renderer });");
        sb.AppendLine("    document.getElementById('content').innerHTML = html;");
        sb.AppendLine();

        // Run mermaid on all .mermaid divs (only when bundle was injected)
        if (hasMermaid)
            sb.AppendLine("    mermaid.run({ nodes: document.querySelectorAll('.mermaid') });");
        sb.AppendLine();

        // Expose scrollToLine for sync-scroll from C#
        sb.AppendLine("    window.scrollToPercent = function(pct) {");
        sb.AppendLine("      const h = document.documentElement.scrollHeight - document.documentElement.clientHeight;");
        sb.AppendLine("      if (h > 0) window.scrollTo(0, h * pct);");
        sb.AppendLine("    };");
        sb.AppendLine();

        // Forward link clicks to C# via postMessage
        sb.AppendLine("    document.addEventListener('click', function(e) {");
        sb.AppendLine("      const a = e.target.closest('a');");
        sb.AppendLine("      if (a && a.href && !a.href.startsWith('about:') && window.chrome && window.chrome.webview) {");
        sb.AppendLine("        e.preventDefault();");
        sb.AppendLine("        window.chrome.webview.postMessage(JSON.stringify({ type: 'link', href: a.href }));");
        sb.AppendLine("      }");
        sb.AppendLine("    });");

        sb.AppendLine("  })();");
        sb.AppendLine("  </script>");
        sb.AppendLine("</body>");
        sb.AppendLine("</html>");

        return sb.ToString();
    }

    // --- Private helpers --------------------------------------------------

    /// <summary>
    /// Escapes a string so it can be safely embedded in a JS double-quoted string literal.
    /// </summary>
    private static string EscapeForJsString(string text)
    {
        // Use a StringBuilder to avoid O(n²) string concatenation for large docs.
        var sb = new StringBuilder(text.Length + 64);
        foreach (var c in text)
        {
            switch (c)
            {
                case '\\': sb.Append("\\\\"); break;
                case '"':  sb.Append("\\\""); break;
                case '\n': sb.Append("\\n");  break;
                case '\r': break;             // collapse \r\n → \n
                case '\t': sb.Append("\\t");  break;
                case '\b': sb.Append("\\b");  break;
                case '\f': sb.Append("\\f");  break;
                default:
                    // Escape non-BMP and control characters
                    if (c < 0x20)
                        sb.Append($"\\u{(int)c:X4}");
                    else
                        sb.Append(c);
                    break;
            }
        }
        return sb.ToString();
    }

    /// <summary>
    /// Returns a <see cref="Lazy{T}"/> that reads the named embedded resource
    /// from this assembly. Falls back to an empty string if not found (prevents
    /// crash during development when resource files have not yet been populated).
    /// </summary>
    private static Lazy<string> Load(string fileName)
        => new(() =>
        {
            var asm  = Assembly.GetExecutingAssembly();
            var name = $"WpfHexEditor.Editor.MarkdownEditor.Resources.{fileName}";
            using var stream = asm.GetManifestResourceStream(name);
            if (stream is null) return $"/* embedded resource '{fileName}' not found */";
            using var reader = new StreamReader(stream, Encoding.UTF8);
            return reader.ReadToEnd();
        });
}
