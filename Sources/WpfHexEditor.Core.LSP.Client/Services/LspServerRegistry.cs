// ==========================================================
// Project: WpfHexEditor.Core.LSP.Client
// File: Services/LspServerRegistry.cs
// Author: Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.6
// Created: 2026-03-16
// Description:
//     Concrete ILspServerRegistry implementation.
//     Entries are stored in-memory and persisted to/from a JSON settings file
//     in the user's app-data folder (%LOCALAPPDATA%\WpfHexEditor\lsp-servers.json).
//
// Architecture Notes:
//     Registry Pattern — maps language IDs / file extensions to server entries.
//     Lazy persistence — written on Register/Unregister only, never on every call.
// ==========================================================

using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Windows.Threading;
using WpfHexEditor.Editor.Core.LSP;

namespace WpfHexEditor.Core.LSP.Client.Services;

/// <summary>
/// Stores and resolves LSP server configurations, and creates <see cref="LspClientImpl"/>
/// instances on demand.
/// </summary>
public sealed class LspServerRegistry : ILspServerRegistry
{
    // ── Constants ──────────────────────────────────────────────────────────────
    private static readonly string s_settingsPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "WpfHexEditor",
        "lsp-servers.json");

    private static readonly JsonSerializerOptions s_json = new()
    {
        WriteIndented  = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    // ── Fields ─────────────────────────────────────────────────────────────────
    private readonly List<LspServerEntry> _entries    = new();
    private readonly object               _lock       = new();
    private readonly Dispatcher           _dispatcher;

    // ── Construction ───────────────────────────────────────────────────────────

    /// <summary>
    /// Creates a registry pre-populated with built-in (optional) server entries
    /// for JSON, XML, and C# if their executables are found on PATH.
    /// </summary>
    public LspServerRegistry(Dispatcher dispatcher)
    {
        _dispatcher = dispatcher;
        LoadFromDisk();
        AddBuiltInEntriesIfMissing();
    }

    // ── ILspServerRegistry ─────────────────────────────────────────────────────

    public IReadOnlyList<LspServerEntry> Entries
    {
        get { lock (_lock) return _entries.ToList(); }
    }

    public LspServerEntry? FindByExtension(string fileExtension)
    {
        var ext = fileExtension.ToLowerInvariant();
        lock (_lock)
            return _entries.FirstOrDefault(e =>
                e.IsEnabled && e.FileExtensions.Any(x => x.Equals(ext, StringComparison.OrdinalIgnoreCase)));
    }

    public LspServerEntry? FindByLanguage(string languageId)
    {
        lock (_lock)
            return _entries.FirstOrDefault(e =>
                e.IsEnabled && e.LanguageId.Equals(languageId, StringComparison.OrdinalIgnoreCase));
    }

    public ILspClient CreateClient(LspServerEntry entry)
        => new LspClientImpl(entry.ExecutablePath, entry.Arguments, null, _dispatcher);

    public void Register(LspServerEntry entry)
    {
        lock (_lock)
        {
            var idx = _entries.FindIndex(e =>
                e.LanguageId.Equals(entry.LanguageId, StringComparison.OrdinalIgnoreCase));
            if (idx >= 0)
                _entries[idx] = entry;
            else
                _entries.Add(entry);
        }
        SaveToDisk();
    }

    public void Unregister(string languageId)
    {
        lock (_lock)
            _entries.RemoveAll(e =>
                e.LanguageId.Equals(languageId, StringComparison.OrdinalIgnoreCase));
        SaveToDisk();
    }

    // ── Persistence ────────────────────────────────────────────────────────────

    private void LoadFromDisk()
    {
        if (!File.Exists(s_settingsPath)) return;
        try
        {
            var json    = File.ReadAllText(s_settingsPath);
            var entries = JsonSerializer.Deserialize<List<LspServerEntryDto>>(json, s_json);
            if (entries is null) return;

            foreach (var dto in entries)
            {
                _entries.Add(new LspServerEntry
                {
                    LanguageId     = dto.LanguageId     ?? string.Empty,
                    FileExtensions = (IReadOnlyList<string>?)dto.FileExtensions ?? Array.Empty<string>(),
                    ExecutablePath = dto.ExecutablePath ?? string.Empty,
                    Arguments      = dto.Arguments,
                    IsEnabled      = dto.IsEnabled,
                    IsBundled      = dto.IsBundled,
                });
            }
        }
        catch { /* corrupt file — ignore */ }
    }

    private void SaveToDisk()
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(s_settingsPath)!);
            List<LspServerEntry> snapshot;
            lock (_lock) snapshot = _entries.ToList();

            var dtos = snapshot.Select(e => new LspServerEntryDto
            {
                LanguageId     = e.LanguageId,
                FileExtensions = e.FileExtensions.ToList(),
                ExecutablePath = e.ExecutablePath,
                Arguments      = e.Arguments,
                IsEnabled      = e.IsEnabled,
                IsBundled      = e.IsBundled,
            }).ToList();

            var json = JsonSerializer.Serialize(dtos, s_json);
            File.WriteAllText(s_settingsPath, json);
        }
        catch { /* best-effort persistence — never crash on save failure */ }
    }

    // ── Built-in entries ───────────────────────────────────────────────────────

    private void AddBuiltInEntriesIfMissing()
    {
        // Bundled servers (OmniSharp, clangd) — installed by Scripts/Download-LspServers.ps1.
        TryAddBuiltIn("csharp", new[] { ".cs", ".csx" },              "OmniSharp", "--languageserver");
        TryAddBuiltIn("cpp",    new[] { ".cpp", ".c", ".h", ".hpp" }, "clangd",    string.Empty);

        // PATH-discovered servers (optional, user must install separately).
        TryAddBuiltIn("json",   new[] { ".json", ".jsonc" },     "vscode-json-languageserver", "--stdio");
        TryAddBuiltIn("xml",    new[] { ".xml", ".xaml" },       "lemminx",                    string.Empty);
        TryAddBuiltIn("fsharp", new[] { ".fs", ".fsx", ".fsi" }, "fsautocomplete",             "--stdio");
        TryAddBuiltIn("vbnet",  new[] { ".vb" },                 "OmniSharp",                  "--languageserver");
    }

    private void TryAddBuiltIn(string langId, string[] exts, string execName, string? args)
    {
        lock (_lock)
            if (_entries.Any(e => e.LanguageId.Equals(langId, StringComparison.OrdinalIgnoreCase)))
                return;

        // Prefer bundled executable; fall back to system PATH.
        var bundledPath = LspBundledLocator.TryGetBundledExecutable(execName);
        var path        = bundledPath ?? FindOnPath(execName);
        if (path is null) return;

        _entries.Add(new LspServerEntry
        {
            LanguageId     = langId,
            FileExtensions = exts,
            ExecutablePath = path,
            Arguments      = args,
            IsEnabled      = true,
            IsBundled      = bundledPath is not null,
        });
    }

    private static string? FindOnPath(string executableName)
    {
        // Check for .cmd / .exe variants on Windows.
        foreach (var suffix in new[] { string.Empty, ".cmd", ".exe" })
        {
            var full = executableName + suffix;
            var which = Environment.GetEnvironmentVariable("PATH")
                ?.Split(Path.PathSeparator)
                .Select(dir => Path.Combine(dir, full))
                .FirstOrDefault(File.Exists);
            if (which is not null) return which;
        }
        return null;
    }

    // ── Private DTO (for JSON persistence) ────────────────────────────────────

    private sealed class LspServerEntryDto
    {
        public string?        LanguageId     { get; set; }
        public List<string>?  FileExtensions { get; set; }
        public string?        ExecutablePath { get; set; }
        public string?        Arguments      { get; set; }
        public bool           IsEnabled      { get; set; } = true;
        public bool           IsBundled      { get; set; }
    }
}
