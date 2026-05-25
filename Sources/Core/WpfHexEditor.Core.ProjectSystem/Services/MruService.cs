//////////////////////////////////////////////
// GNU Affero General Public License v3.0 - 2026
// Author : Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.6
//////////////////////////////////////////////

using System.IO;
using System.Text.Json;

namespace WpfHexEditor.Core.ProjectSystem.Services;

/// <summary>
/// Persists up to <see cref="MaxEntries"/> most-recently-used paths for
/// solutions and standalone files to %APPDATA%\WpfHexEditor\mru.json.
/// Thread-safe for single-threaded UI use (no locking required).
/// </summary>
internal sealed class MruService
{
    private const int MaxEntries = 10;
    private static readonly string _mruPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "WpfHexEditor", "mru.json");

    private readonly List<string> _solutions = [];
    private readonly List<string> _files     = [];
    private readonly HashSet<string> _pinnedFiles     = new(StringComparer.OrdinalIgnoreCase);
    private readonly HashSet<string> _pinnedSolutions = new(StringComparer.OrdinalIgnoreCase);

    public IReadOnlyList<string> RecentSolutions => _solutions;
    public IReadOnlyList<string> RecentFiles     => _files;
    public IReadOnlySet<string>  PinnedFiles     => _pinnedFiles;
    public IReadOnlySet<string>  PinnedSolutions => _pinnedSolutions;

    // -- Lifecycle --------------------------------------------------------

    public void Load()
    {
        try
        {
            if (!File.Exists(_mruPath)) return;
            var json = File.ReadAllText(_mruPath);
            var dto  = JsonSerializer.Deserialize<MruDto>(json);
            if (dto is null) return;
            _solutions.Clear();
            _files.Clear();
            _pinnedFiles.Clear();
            _pinnedSolutions.Clear();
            _solutions.AddRange(dto.Solutions ?? []);
            _files.AddRange(dto.Files ?? []);
            foreach (var p in dto.PinnedFiles     ?? []) _pinnedFiles.Add(p);
            foreach (var p in dto.PinnedSolutions ?? []) _pinnedSolutions.Add(p);
        }
        catch { /* silent on corrupt file */ }
    }

    public void Save()
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(_mruPath)!);
            var dto  = new MruDto
            {
                Solutions        = [.. _solutions],
                Files            = [.. _files],
                PinnedFiles      = [.. _pinnedFiles],
                PinnedSolutions  = [.. _pinnedSolutions]
            };
            var json = JsonSerializer.Serialize(dto, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(_mruPath, json);
        }
        catch { /* silent */ }
    }

    // -- Mutation ---------------------------------------------------------

    public void PushSolution(string path) => Push(_solutions, path);
    public void PushFile(string path)     => Push(_files, path);

    public void PinFile(string path)      { _pinnedFiles.Add(path);                Save(); }
    public void UnpinFile(string path)    { _pinnedFiles.Remove(path);             Save(); }
    public void PinSolution(string path)  { _pinnedSolutions.Add(path);            Save(); }
    public void UnpinSolution(string path){ _pinnedSolutions.Remove(path);         Save(); }
    public void RemoveFile(string path)   { _files.RemoveAll(p => string.Equals(p, path, StringComparison.OrdinalIgnoreCase)); _pinnedFiles.Remove(path); Save(); }
    public void RemoveSolution(string path){ _solutions.RemoveAll(p => string.Equals(p, path, StringComparison.OrdinalIgnoreCase)); _pinnedSolutions.Remove(path); Save(); }

    private static void Push(List<string> list, string path)
    {
        list.RemoveAll(p => string.Equals(p, path, StringComparison.OrdinalIgnoreCase));
        list.Insert(0, path);
        while (list.Count > MaxEntries)
            list.RemoveAt(list.Count - 1);
    }

    // -- DTO ---------------------------------------------------------------

    private sealed class MruDto
    {
        public List<string>? Solutions        { get; set; }
        public List<string>? Files            { get; set; }
        public List<string>? PinnedFiles      { get; set; }
        public List<string>? PinnedSolutions  { get; set; }
    }
}
