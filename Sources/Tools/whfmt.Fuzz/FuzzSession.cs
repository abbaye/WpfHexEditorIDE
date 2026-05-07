// ==========================================================
// Project: whfmt.Fuzz
// File: FuzzSession.cs
// Description: Multi-generation reproducible fuzzing session with corpus management.
// Architecture: Wraps FormatFuzzer — maintains seed state, corpus list, manifest JSON.
// ==========================================================

using System.Text.Json;
using WpfHexEditor.Core.Contracts;

namespace WhfmtFuzz;

/// <summary>
/// A reproducible multi-generation fuzzing session.
/// Use a fixed <paramref name="seed"/> for identical corpora across CI runs.
/// </summary>
public sealed class FuzzSession
{
    private readonly IEmbeddedFormatCatalog _catalog;
    private readonly int? _seed;
    private int _generation;
    private readonly List<FuzzVariant> _corpus = [];

    /// <summary>All variants accumulated across all generations.</summary>
    public IReadOnlyList<FuzzVariant> Corpus => _corpus;

    /// <summary>Current generation index (0-based).</summary>
    public int Generation => _generation;

    public FuzzSession(IEmbeddedFormatCatalog catalog, int? seed = null)
    {
        _catalog = catalog;
        _seed    = seed;
    }

    /// <summary>
    /// Generate the next batch of variants and add them to the corpus.
    /// </summary>
    public IReadOnlyList<FuzzVariant> NextGeneration(
        string inputFile,
        int count = 10,
        string? forcedFormat = null,
        int compound = 1)
    {
        int effectiveSeed = _seed.HasValue ? _seed.Value + _generation * 1000 : new Random().Next();
        var batch = FormatFuzzer.Generate(_catalog, inputFile, count, forcedFormat, effectiveSeed, compound);

        // Restamp indices globally across the session
        int offset = _corpus.Count;
        var stamped = batch.Select((v, i) => Restamp(v, offset + i)).ToList();

        _corpus.AddRange(stamped);
        _generation++;
        return stamped;
    }

    /// <summary>
    /// Generate the next batch from raw bytes.
    /// </summary>
    public IReadOnlyList<FuzzVariant> NextGeneration(
        byte[] inputData,
        string fileName,
        int count = 10,
        string? forcedFormat = null,
        int compound = 1)
    {
        int effectiveSeed = _seed.HasValue ? _seed.Value + _generation * 1000 : new Random().Next();
        var batch = FormatFuzzer.Generate(_catalog, inputData, fileName, count, forcedFormat, effectiveSeed, compound);

        int offset = _corpus.Count;
        var stamped = batch.Select((v, i) => Restamp(v, offset + i)).ToList();

        _corpus.AddRange(stamped);
        _generation++;
        return stamped;
    }

    /// <summary>
    /// Write all successful corpus variants to <paramref name="directory"/> with a manifest.json.
    /// </summary>
    public async Task SaveCorpusAsync(string directory)
    {
        Directory.CreateDirectory(directory);

        var manifest = new List<object>();
        foreach (var v in _corpus.Where(v => !v.IsError))
        {
            string path = Path.Combine(directory, v.SuggestedFileName);
            await File.WriteAllBytesAsync(path, v.Data);
            manifest.Add(new
            {
                index        = v.Index,
                file         = v.SuggestedFileName,
                format       = v.FormatName,
                strategy     = v.Strategy,
                field        = v.Field,
                description  = v.Description,
                mutations    = v.MutationCount,
                mutationLog  = v.MutationLog.Select(m => new { mutation = m.Mutation.ToString(), m.Field }),
                sizeBytes    = v.Data.Length,
            });
        }

        string manifestJson = JsonSerializer.Serialize(manifest, new JsonSerializerOptions { WriteIndented = true });
        await File.WriteAllTextAsync(Path.Combine(directory, "manifest.json"), manifestJson);

        Console.WriteLine($"  Saved {manifest.Count} variants to '{directory}'");
        Console.WriteLine($"  manifest.json written ({manifest.Count} entries)");
    }

    /// <summary>Save corpus synchronously.</summary>
    public void SaveCorpus(string directory) => SaveCorpusAsync(directory).GetAwaiter().GetResult();

    /// <summary>
    /// Reload a previously saved corpus from a directory (reads manifest.json + all variant files).
    /// Returns a new session pre-populated with the loaded variants.
    /// </summary>
    private static FuzzVariant Restamp(FuzzVariant v, int newIndex) => new FuzzVariant
    {
        Index         = newIndex,
        OriginalFile  = v.OriginalFile,
        FormatName    = v.FormatName,
        Strategy      = v.Strategy,
        Field         = v.Field,
        Description   = v.Description,
        Data          = v.Data,
        MutationCount = v.MutationCount,
        MutationLog   = v.MutationLog,
        Error         = v.Error,
    };

    public static async Task<FuzzSession> LoadCorpusAsync(IEmbeddedFormatCatalog catalog, string directory, int? seed = null)
    {
        var session = new FuzzSession(catalog, seed);
        string manifestPath = Path.Combine(directory, "manifest.json");
        if (!File.Exists(manifestPath)) return session;

        using var doc = JsonDocument.Parse(await File.ReadAllTextAsync(manifestPath));
        foreach (var entry in doc.RootElement.EnumerateArray())
        {
            string file = entry.TryGetProperty("file", out var f) ? f.GetString() ?? "" : "";
            string path = Path.Combine(directory, file);
            if (!File.Exists(path)) continue;

            byte[] data     = await File.ReadAllBytesAsync(path);
            int    index    = entry.TryGetProperty("index",    out var idx) ? idx.GetInt32() : 0;
            string format   = entry.TryGetProperty("format",   out var fmt) ? fmt.GetString() ?? "" : "";
            string strategy = entry.TryGetProperty("strategy", out var st)  ? st.GetString()  ?? "" : "";
            string field    = entry.TryGetProperty("field",    out var fld) ? fld.GetString()  ?? "" : "";
            string desc     = entry.TryGetProperty("description", out var d) ? d.GetString()  ?? "" : "";
            int    mutations= entry.TryGetProperty("mutations", out var m)  ? m.GetInt32() : 1;

            session._corpus.Add(new FuzzVariant
            {
                Index         = index,
                OriginalFile  = file,
                FormatName    = format,
                Strategy      = strategy,
                Field         = field,
                Description   = desc,
                Data          = data,
                MutationCount = mutations,
            });
        }

        return session;
    }
}
