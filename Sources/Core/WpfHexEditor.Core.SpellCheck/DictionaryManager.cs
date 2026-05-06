// ==========================================================
// Project: WpfHexEditor.Core.SpellCheck
// File: DictionaryManager.cs
// Description:
//     Manages Hunspell dictionary files (.dic/.aff) stored in
//     %APPDATA%\WpfHexEditor\Dictionaries\{lang}\
//     Supports install from local file or URL (LibreOffice mirror).
// ==========================================================

using System.IO;
using System.Net.Http;

namespace WpfHexEditor.Core.SpellCheck;

public sealed class DictionaryManager
{
    private static readonly Dictionary<string, (string Display, string RepoPath, string Prefix)> KnownLanguages = new()
    {
        ["ar-SA"]  = ("العربية (السعودية)",      "ar",    "ar"),
        ["cs-CZ"]  = ("Čeština",                 "cs_CZ", "cs_CZ"),
        ["da-DK"]  = ("Dansk",                   "da_DK", "da_DK"),
        ["de-DE"]  = ("Deutsch",                 "de",    "de_DE"),
        ["el-GR"]  = ("Ελληνικά",                "el_GR", "el_GR"),
        ["en-GB"]  = ("English (UK)",            "en",    "en_GB"),
        ["en-US"]  = ("English (US)",            "en",    "en_US"),
        ["es-ES"]  = ("Español (España)",        "es",    "es_ES"),
        ["es-419"] = ("Español (Latinoamérica)", "es_ANY","es_ANY"),
        ["fi-FI"]  = ("Suomi",                   "fi_FI", "fi_FI"),
        ["fr-CA"]  = ("Français (Canada)",       "fr_CA", "fr_CA"),
        ["fr-FR"]  = ("Français (France)",       "fr_FR", "fr_FR"),
        ["hi-IN"]  = ("हिन्दी",                  "hi_IN", "hi_IN"),
        ["hu-HU"]  = ("Magyar",                  "hu_HU", "hu_HU"),
        ["id-ID"]  = ("Bahasa Indonesia",        "id",    "id_ID"),
        ["it-IT"]  = ("Italiano",                "it_IT", "it_IT"),
        ["ja-JP"]  = ("日本語",                  "ja_JP", "ja_JP"),
        ["ko-KR"]  = ("한국어",                  "ko_KR", "ko_KR"),
        ["nl-NL"]  = ("Nederlands",              "nl_NL", "nl_NL"),
        ["pl-PL"]  = ("Polski",                  "pl_PL", "pl_PL"),
        ["pt-BR"]  = ("Português (Brasil)",      "pt_BR", "pt_BR"),
        ["pt-PT"]  = ("Português (Portugal)",    "pt_PT", "pt_PT"),
        ["ro-RO"]  = ("Română",                  "ro",    "ro_RO"),
        ["ru-RU"]  = ("Русский",                 "ru_RU", "ru_RU"),
        ["sv-SE"]  = ("Svenska",                 "sv_SE", "sv_SE"),
        ["th-TH"]  = ("ภาษาไทย",                "th_TH", "th_TH"),
        ["tr-TR"]  = ("Türkçe",                 "tr_TR", "tr_TR"),
        ["uk-UA"]  = ("Українська",              "uk_UA", "uk_UA"),
        ["vi-VN"]  = ("Tiếng Việt",              "vi",    "vi_VI"),
        ["zh-CN"]  = ("中文 (简体)",              "zh_CN", "zh_CN"),
    };

    private readonly SpellCheckerSettings _settings;
    private static readonly HttpClient _http = new() { Timeout = TimeSpan.FromSeconds(60) };

    public DictionaryManager(SpellCheckerSettings settings) => _settings = settings;

    public IReadOnlyList<DictionaryInfo> GetAllLanguages()
    {
        var result = new List<DictionaryInfo>(KnownLanguages.Count);
        foreach (var (code, (display, _, prefix)) in KnownLanguages)
        {
            var (dic, aff) = Paths(code, prefix);
            result.Add(new DictionaryInfo(code, display, File.Exists(dic) && File.Exists(aff), dic, aff));
        }
        return result;
    }

    public DictionaryInfo? GetInfo(string languageCode)
    {
        if (!KnownLanguages.TryGetValue(languageCode, out var meta)) return null;
        var (dic, aff) = Paths(languageCode, meta.Prefix);
        return new DictionaryInfo(languageCode, meta.Display, File.Exists(dic) && File.Exists(aff), dic, aff);
    }

    public bool IsInstalled(string languageCode)
    {
        if (!KnownLanguages.TryGetValue(languageCode, out var meta)) return false;
        var (dic, aff) = Paths(languageCode, meta.Prefix);
        return File.Exists(dic) && File.Exists(aff);
    }

    public void InstallFromFile(string dicFilePath, string affFilePath, string languageCode)
    {
        if (!KnownLanguages.TryGetValue(languageCode, out var meta))
            throw new ArgumentException($"Unknown language code: {languageCode}");
        var (dic, aff) = Paths(languageCode, meta.Prefix);
        Directory.CreateDirectory(Path.GetDirectoryName(dic)!);
        File.Copy(dicFilePath, dic, overwrite: true);
        File.Copy(affFilePath, aff, overwrite: true);
    }

    public async Task InstallFromUrlAsync(
        string languageCode,
        IProgress<double>? progress = null,
        CancellationToken ct = default)
    {
        if (!KnownLanguages.TryGetValue(languageCode, out var meta))
            throw new ArgumentException($"Unknown language code: {languageCode}");

        var (dic, aff) = Paths(languageCode, meta.Prefix);
        Directory.CreateDirectory(Path.GetDirectoryName(dic)!);
        progress?.Report(-1);

        var baseUrl = _settings.MirrorUrl.TrimEnd('/');
        await DownloadFileAsync($"{baseUrl}/{meta.RepoPath}/{meta.Prefix}.dic", dic, progress, 0.0, 0.5, ct);
        await DownloadFileAsync($"{baseUrl}/{meta.RepoPath}/{meta.Prefix}.aff", aff, progress, 0.5, 1.0, ct);
        progress?.Report(1.0);
    }

    public void Remove(string languageCode)
    {
        if (!KnownLanguages.TryGetValue(languageCode, out var meta)) return;
        var (dic, aff) = Paths(languageCode, meta.Prefix);
        try { File.Delete(dic); } catch { }
        try { File.Delete(aff); } catch { }
        try { Directory.Delete(Path.GetDirectoryName(dic)!); } catch { }
    }

    public static string? GetDisplayName(string languageCode) =>
        KnownLanguages.TryGetValue(languageCode, out var m) ? m.Display : null;

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
