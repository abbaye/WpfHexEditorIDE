// ==========================================================
// Project: WpfHexEditor.Core.SpellCheck
// File: DictionaryManager.cs
// Description:
//     Manages Hunspell dictionary files (.dic/.aff) stored in
//     %APPDATA%\WpfHexEditor\Dictionaries\{lang}\
//     Language metadata comes from LanguageCatalog (embedded JSON).
//     Supports install from local file or URL (LibreOffice GitHub mirror).
// ==========================================================

using System.IO;
using System.Net.Http;

namespace WpfHexEditor.Core.SpellCheck;

public sealed class DictionaryManager
{
    private readonly SpellCheckerSettings _settings;
    private static readonly HttpClient _http = new() { Timeout = TimeSpan.FromSeconds(60) };

    public DictionaryManager(SpellCheckerSettings settings) => _settings = settings;

    public IReadOnlyList<DictionaryInfo> GetAllLanguages()
    {
        var result = new List<DictionaryInfo>(LanguageCatalog.Languages.Count);
        foreach (var entry in LanguageCatalog.Languages)
        {
            var (dic, aff) = Paths(entry.Code, entry.Prefix);
            result.Add(new DictionaryInfo(entry.Code, entry.Display, File.Exists(dic) && File.Exists(aff), dic, aff));
        }
        return result;
    }

    public DictionaryInfo? GetInfo(string languageCode)
    {
        var entry = LanguageCatalog.Get(languageCode);
        if (entry is null) return null;
        var (dic, aff) = Paths(languageCode, entry.Prefix);
        return new DictionaryInfo(languageCode, entry.Display, File.Exists(dic) && File.Exists(aff), dic, aff);
    }

    public bool IsInstalled(string languageCode)
    {
        var entry = LanguageCatalog.Get(languageCode);
        if (entry is null) return false;
        var (dic, aff) = Paths(languageCode, entry.Prefix);
        return File.Exists(dic) && File.Exists(aff);
    }

    public void InstallFromFile(string dicFilePath, string affFilePath, string languageCode)
    {
        var entry = LanguageCatalog.Get(languageCode)
            ?? throw new ArgumentException($"Unknown language code: {languageCode}");
        var (dic, aff) = Paths(languageCode, entry.Prefix);
        Directory.CreateDirectory(Path.GetDirectoryName(dic)!);
        File.Copy(dicFilePath, dic, overwrite: true);
        File.Copy(affFilePath, aff, overwrite: true);
    }

    public async Task InstallFromUrlAsync(
        string languageCode,
        IProgress<double>? progress = null,
        CancellationToken ct = default)
    {
        var entry = LanguageCatalog.Get(languageCode)
            ?? throw new ArgumentException($"Unknown language code: {languageCode}");

        var (dic, aff) = Paths(languageCode, entry.Prefix);
        Directory.CreateDirectory(Path.GetDirectoryName(dic)!);
        progress?.Report(-1);

        var baseUrl = _settings.MirrorUrl.TrimEnd('/');
        await DownloadFileAsync($"{baseUrl}/{entry.RepoPath}/{entry.Prefix}.dic", dic, progress, 0.0, 0.5, ct);
        await DownloadFileAsync($"{baseUrl}/{entry.RepoPath}/{entry.Prefix}.aff", aff, progress, 0.5, 1.0, ct);
        progress?.Report(1.0);
    }

    public void Remove(string languageCode)
    {
        var entry = LanguageCatalog.Get(languageCode);
        if (entry is null) return;
        var (dic, aff) = Paths(languageCode, entry.Prefix);
        try { File.Delete(dic); } catch { }
        try { File.Delete(aff); } catch { }
        try { Directory.Delete(Path.GetDirectoryName(dic)!); } catch { }
    }

    public static string? GetDisplayName(string languageCode) =>
        LanguageCatalog.Get(languageCode)?.Display;

    private (string Dic, string Aff) Paths(string code, string prefix)
    {
        var dir = Path.Combine(_settings.DictionariesPath, code);
        return (Path.Combine(dir, $"{prefix}.dic"), Path.Combine(dir, $"{prefix}.aff"));
    }

    private static async Task DownloadFileAsync(
        string url, string dest,
        IProgress<double>? progress, double from, double to,
        CancellationToken ct)
    {
        using var response = await _http.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, ct);
        response.EnsureSuccessStatusCode();
        var total  = response.Content.Headers.ContentLength ?? -1L;
        var buffer = new byte[81920];
        long read  = 0;
        await using var src = await response.Content.ReadAsStreamAsync(ct);
        await using var dst = File.Create(dest);
        int chunk;
        while ((chunk = await src.ReadAsync(buffer, ct)) > 0)
        {
            await dst.WriteAsync(buffer.AsMemory(0, chunk), ct);
            read += chunk;
            if (total > 0 && progress is not null)
                progress.Report(from + (to - from) * ((double)read / total));
        }
    }
}
