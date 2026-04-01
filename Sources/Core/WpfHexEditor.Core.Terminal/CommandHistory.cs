//////////////////////////////////////////////
// GNU Affero General Public License v3.0 - 2026
// Author : Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.6
//////////////////////////////////////////////

using System.Text.Json;

namespace WpfHexEditor.Core.Terminal;

/// <summary>
/// Circular buffer (max 500 entries) for terminal command history.
/// Persisted to %AppData%\WpfHexEditor\TerminalHistory.json.
/// </summary>
public sealed class CommandHistory
{
    private const int MaxEntries = 500;

    private static readonly string HistoryPath =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                     "WpfHexEditor", "TerminalHistory.json");

    private readonly LinkedList<string> _entries = new();
    private int _position = -1;

    public void Push(string command)
    {
        if (string.IsNullOrWhiteSpace(command)) return;

        // Remove duplicate
        _entries.Remove(command);
        _entries.AddFirst(command);

        while (_entries.Count > MaxEntries)
            _entries.RemoveLast();

        _position = -1;
    }

    public string? NavigatePrevious()
    {
        if (_entries.Count == 0) return null;
        _position = Math.Min(_position + 1, _entries.Count - 1);
        return _entries.ElementAt(_position);
    }

    public string? NavigateNext()
    {
        if (_position <= 0) { _position = -1; return string.Empty; }
        _position--;
        return _entries.ElementAt(_position);
    }

    public IReadOnlyList<string> GetAll() => _entries.ToList();

    public async Task SaveAsync(CancellationToken ct = default)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(HistoryPath)!);
        var tmp = HistoryPath + ".tmp";
        await File.WriteAllTextAsync(tmp,
            JsonSerializer.Serialize(_entries.ToList()), ct).ConfigureAwait(false);
        File.Move(tmp, HistoryPath, overwrite: true);
    }

    public async Task LoadAsync(CancellationToken ct = default)
    {
        if (!File.Exists(HistoryPath)) return;
        try
        {
            var json = await File.ReadAllTextAsync(HistoryPath, ct).ConfigureAwait(false);
            var list = JsonSerializer.Deserialize<List<string>>(json);
            if (list is null) return;
            _entries.Clear();
            foreach (var entry in list.Take(MaxEntries))
                _entries.AddLast(entry);
        }
        catch { /* corrupt history — ignore */ }
    }
}
