// Project      : WpfHexEditorControl
// File         : Services/DiffModeDetector.cs
// Description  : Heuristically detects the best DiffMode for a given file.
// Architecture : Stateless service; no I/O beyond sniffing first 512 bytes.

using WpfHexEditor.Core.Diff.Models;

namespace WpfHexEditor.Core.Diff.Services;

/// <summary>
/// Detects the most appropriate <see cref="DiffMode"/> for a file
/// based on its extension and first 512 bytes.
/// </summary>
public static class DiffModeDetector
{
    private const long MaxMeyersFileSizeBytes = 50L * 1024 * 1024; // 50 MB
    private const int  SniffBytes             = 512;

    // ── Injectable resolver ────────────────────────────────────────────────────
    // App layer calls Configure() once after LanguageRegistry is populated.
    // Returns the preferred DiffMode for a given extension, or null = unknown (fall through to sniff).
    private static Func<string, DiffMode?>? s_extensionResolver;

    /// <summary>
    /// Registers a language-registry-backed extension resolver.
    /// Must be called once from the App layer after LanguageRegistry is fully populated.
    /// </summary>
    /// <param name="resolver">
    /// Receives the file extension (lower-case, with dot) and returns the preferred
    /// <see cref="DiffMode"/>, or <see langword="null"/> when the extension is unknown
    /// (content sniffing will be used as fallback).
    /// </param>
    public static void Configure(Func<string, DiffMode?> resolver)
        => s_extensionResolver = resolver;

    // Fallback tables used only when Configure() has not been called (unit tests, standalone use).
    private static readonly HashSet<string> FallbackSemanticExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".json", ".xml", ".xaml", ".csproj", ".vbproj", ".fsproj", ".props",
        ".targets", ".config", ".yaml", ".yml", ".toml", ".html", ".htm",
        ".svg", ".cs", ".vb", ".fs"
    };

    private static readonly HashSet<string> FallbackTextExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".txt", ".log", ".md", ".rst", ".csv", ".tsv", ".ini", ".bat",
        ".cmd", ".sh", ".py", ".js", ".ts", ".jsx", ".tsx", ".css",
        ".scss", ".sass", ".less", ".sql", ".ps1", ".psm1", ".gitignore",
        ".editorconfig", ".sln", ".resx"
    };

    /// <summary>
    /// Detects the best <see cref="DiffMode"/> for two files.
    /// Returns <see cref="DiffMode.Binary"/> if sizes diverge in type or if either is too large.
    /// </summary>
    public static DiffMode DetectForPair(string leftPath, string rightPath)
    {
        var leftMode  = Detect(leftPath);
        var rightMode = Detect(rightPath);
        // Agree on mode; if disagreement fall back to Binary
        return leftMode == rightMode ? leftMode : DiffMode.Binary;
    }

    /// <summary>
    /// Detects the best <see cref="DiffMode"/> for a single file.
    /// </summary>
    public static DiffMode Detect(string filePath)
    {
        if (!File.Exists(filePath)) return DiffMode.Binary;

        var fi = new FileInfo(filePath);
        if (fi.Length > MaxMeyersFileSizeBytes) return DiffMode.Binary;

        var ext = Path.GetExtension(filePath);

        // Extension-based shortcut: prefer whfmt-driven resolver when available.
        if (s_extensionResolver is not null)
        {
            var resolved = s_extensionResolver(ext);
            if (resolved.HasValue) return resolved.Value;
        }
        else
        {
            // Fallback when Configure() has not been called (unit tests / standalone use).
            if (FallbackSemanticExtensions.Contains(ext)) return DiffMode.Semantic;
            if (FallbackTextExtensions.Contains(ext))     return DiffMode.Text;
        }

        // Content sniff
        return SniffContent(filePath);
    }

    // -----------------------------------------------------------------------
    // Private helpers
    // -----------------------------------------------------------------------

    private static DiffMode SniffContent(string filePath)
    {
        Span<byte> buf = stackalloc byte[SniffBytes];
        int read;
        using (var fs = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
            read = fs.Read(buf);

        if (read == 0) return DiffMode.Text;

        // Presence of null byte → binary
        for (int i = 0; i < read; i++)
            if (buf[i] == 0) return DiffMode.Binary;

        // BOM or XML/JSON leaders
        if (read >= 3 && buf[0] == 0xEF && buf[1] == 0xBB && buf[2] == 0xBF)
            return DiffMode.Text; // UTF-8 BOM

        // Trim leading whitespace to detect structured
        int j = 0;
        while (j < read && (buf[j] == ' ' || buf[j] == '\t' || buf[j] == '\r' || buf[j] == '\n')) j++;
        if (j < read && (buf[j] == '<' || buf[j] == '{' || buf[j] == '['))
            return DiffMode.Semantic;

        return DiffMode.Text;
    }
}
