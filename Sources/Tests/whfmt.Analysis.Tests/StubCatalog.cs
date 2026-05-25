// ==========================================================
// Project: whfmt.Analysis.Tests
// File: StubCatalog.cs
// Description: Minimal IEmbeddedFormatCatalog stub for unit tests — no disk access.
// ==========================================================

using WpfHexEditor.Core.Contracts;

namespace WhfmtAnalysis.Tests;

/// <summary>
/// In-memory catalog stub that returns a fixed whfmt JSON for a single format.
/// FormatFileAnalyzer.Analyze() uses magic-byte matching; the stub exposes the
/// configured entry so that tests can force a known format via forcedFormat.
/// </summary>
internal sealed class StubCatalog : IEmbeddedFormatCatalog
{
    private readonly Dictionary<string, (EmbeddedFormatEntry Entry, string Json)> _formats = new(StringComparer.OrdinalIgnoreCase);

    internal StubCatalog Register(string name, string json,
        string[]? extensions = null,
        string category = "misc",
        string formatId = "")
    {
        var entry = new EmbeddedFormatEntry(
            ResourceKey:          name,
            Name:                 name,
            Category:             category,
            Description:          name,
            Extensions:           extensions ?? [],
            QualityScore:         80,
            Version:              "1.0",
            Author:               "test",
            Platform:             "",
            PreferredEditor:      null,
            IsTextFormat:         false,
            FormatId:             formatId);
        _formats[name] = (entry, json);
        return this;
    }

    public IReadOnlySet<EmbeddedFormatEntry>      GetAll()            => _formats.Values.Select(v => v.Entry).ToHashSet();
    public IReadOnlySet<string>                   GetCategories()     => _formats.Values.Select(v => v.Entry.Category).ToHashSet();
    public string                                 GetJson(string key) => _formats.TryGetValue(key, out var v) ? v.Json : "";
    public EmbeddedFormatEntry?                   GetByExtension(string ext) => _formats.Values.FirstOrDefault(v => v.Entry.Extensions.Contains(ext)).Entry;
    public IReadOnlyList<EmbeddedFormatEntry>     GetByCategory(string cat) => _formats.Values.Where(v => v.Entry.Category.Equals(cat, StringComparison.OrdinalIgnoreCase)).Select(v => v.Entry).ToList();
    public IReadOnlyList<string>                  GetCompatibleEditorIds(string _) => ["hex-editor"];
    public EmbeddedFormatEntry?                   DetectFromBytes(ReadOnlySpan<byte> _) => null;
    public EmbeddedFormatEntry?                   GetByMimeType(string _) => null;
    public string?                                GetSchemaJson(string _) => null;
}
