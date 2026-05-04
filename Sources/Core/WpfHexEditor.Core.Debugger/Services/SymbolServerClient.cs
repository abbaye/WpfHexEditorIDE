// ==========================================================
// Project: WpfHexEditor.Core.Debugger
// File: Services/SymbolServerClient.cs
// Description: Downloads PDB files from symbol servers using the
//              standard Microsoft symbol server URL scheme:
//              {serverUrl}/{pdbFileName}/{signature+age}/{pdbFileName}
// Architecture: Standalone HTTP client; no dependency on IDapClient.
//              Caller (DebuggerServiceImpl) invokes on module-load events.
// ==========================================================

namespace WpfHexEditor.Core.Debugger.Services;

/// <summary>
/// Downloads PDB symbol files from one or more symbol servers.
/// Uses the Microsoft two-level symbol store URL layout.
/// </summary>
public sealed class SymbolServerClient : IDisposable
{
    private readonly HttpClient _http;
    private readonly string     _cacheRoot;

    public SymbolServerClient(string? cacheRoot = null)
    {
        _cacheRoot = string.IsNullOrWhiteSpace(cacheRoot)
            ? Path.Combine(Path.GetTempPath(), "WpfHexEditorSymbols")
            : cacheRoot;

        _http = new HttpClient { Timeout = TimeSpan.FromSeconds(30) };
        _http.DefaultRequestHeaders.UserAgent.ParseAdd("WpfHexEditor/1.0");
    }

    /// <summary>
    /// Try to resolve a PDB for the given module. Checks the local cache first,
    /// then queries each symbol server URL in order.
    /// </summary>
    /// <param name="pdbFileName">Bare file name (e.g. "MyLib.pdb").</param>
    /// <param name="pdbSignature">PDB GUID + age string (e.g. "AABBCCDD1").</param>
    /// <param name="serverUrls">Ordered list of symbol server base URLs.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Local path to the cached PDB, or null if not found.</returns>
    public async Task<string?> ResolveAsync(
        string              pdbFileName,
        string              pdbSignature,
        IReadOnlyList<string> serverUrls,
        CancellationToken   ct = default)
    {
        var cachedPath = BuildCachePath(pdbFileName, pdbSignature);
        if (File.Exists(cachedPath))
            return cachedPath;

        foreach (var serverUrl in serverUrls)
        {
            var url = BuildSymbolUrl(serverUrl, pdbFileName, pdbSignature);
            var localPath = await TryDownloadAsync(url, cachedPath, ct);
            if (localPath is not null)
                return localPath;
        }

        return null;
    }

    private string BuildCachePath(string pdbFileName, string pdbSignature)
        => Path.Combine(_cacheRoot, pdbFileName, pdbSignature, pdbFileName);

    private static string BuildSymbolUrl(string serverUrl, string pdbFileName, string pdbSignature)
        => $"{serverUrl.TrimEnd('/')}/{pdbFileName}/{pdbSignature}/{pdbFileName}";

    private async Task<string?> TryDownloadAsync(
        string url, string localPath, CancellationToken ct)
    {
        try
        {
            using var resp = await _http.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, ct);
            if (!resp.IsSuccessStatusCode)
                return null;

            Directory.CreateDirectory(Path.GetDirectoryName(localPath)!);
            var tmp = localPath + ".tmp";
            await using (var fs = File.Create(tmp))
                await resp.Content.CopyToAsync(fs, ct);
            File.Move(tmp, localPath, overwrite: true);
            return localPath;
        }
        catch
        {
            return null;
        }
    }

    public void Dispose() => _http.Dispose();
}
