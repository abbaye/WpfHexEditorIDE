// ==========================================================
// Project: WpfHexEditor.Core.SpellCheck
// File: HunspellSpellChecker.cs
// Description:
//     ISpellChecker backed by WeCantSpell.Hunspell.
//     Single-language mode: uses one active WordList (LoadAsync).
//     Multi-language mode: loads all installed dicts; CheckWord accepts
//     a word if ANY loaded WordList recognises it. This eliminates false
//     positives in multilingual documents (e.g. FR+EN CV).
//     WordList is immutable after load — thread-safe for CheckWord/Suggest.
// ==========================================================

using System.IO;
using WeCantSpell.Hunspell;

namespace WpfHexEditor.Core.SpellCheck;

public sealed class HunspellSpellChecker : ISpellChecker
{
    private readonly SpellCheckerSettings _settings;
    private readonly DictionaryManager    _dictManager;

    // Single-language state
    private WordList?  _wordList;
    private string?    _activeLanguage;

    // Multi-language state — code → WordList
    private readonly Dictionary<string, WordList> _multiLists = new(StringComparer.OrdinalIgnoreCase);

    private readonly HashSet<string>  _userWords = new(StringComparer.OrdinalIgnoreCase);
    private readonly SemaphoreSlim    _loadLock  = new(1, 1);

    public bool    IsLoaded       => _wordList is not null || _multiLists.Count > 0;
    public string? ActiveLanguage => _activeLanguage;
    public bool    MultiLanguageMode { get; set; } = true;

    public event EventHandler? DictionaryChanged;

    public HunspellSpellChecker(SpellCheckerSettings settings, DictionaryManager dictManager)
    {
        _settings    = settings;
        _dictManager = dictManager;
        LoadUserWords();
    }

    public async Task LoadAsync(string languageCode, CancellationToken ct = default)
    {
        var info = _dictManager.GetInfo(languageCode);
        if (info is null || !info.IsInstalled) return;

        await _loadLock.WaitAsync(ct);
        try
        {
            var wl = await WordList.CreateFromFilesAsync(info.DicPath, info.AffPath, ct);
            _wordList       = wl;
            _activeLanguage = languageCode;

            // Also register in multi-list so switching modes doesn't lose the dict
            _multiLists[languageCode] = wl;
        }
        finally { _loadLock.Release(); }

        DictionaryChanged?.Invoke(this, EventArgs.Empty);
    }

    public async Task LoadAllInstalledAsync(CancellationToken ct = default)
    {
        var installed = _dictManager.GetAllLanguages().Where(l => l.IsInstalled).ToList();
        if (installed.Count == 0) return;

        await _loadLock.WaitAsync(ct);
        try
        {
            _multiLists.Clear();
            foreach (var info in installed)
            {
                ct.ThrowIfCancellationRequested();
                var wl = await WordList.CreateFromFilesAsync(info.DicPath, info.AffPath, ct);
                _multiLists[info.LanguageCode] = wl;
            }
            // Keep primary WordList pointing at first (or existing active) language
            if (_activeLanguage is not null && _multiLists.TryGetValue(_activeLanguage, out var primary))
                _wordList = primary;
            else if (_multiLists.Count > 0)
            {
                var first = _multiLists.First();
                _wordList       = first.Value;
                _activeLanguage = first.Key;
            }
        }
        finally { _loadLock.Release(); }

        DictionaryChanged?.Invoke(this, EventArgs.Empty);
    }

    public bool CheckWord(string word)
    {
        if (_userWords.Contains(word)) return true;

        if (MultiLanguageMode && _multiLists.Count > 0)
        {
            foreach (var wl in _multiLists.Values)
                if (wl.Check(word)) return true;
            return false;
        }

        if (_wordList is null) return true;
        return _wordList.Check(word);
    }

    public IReadOnlyList<string> Suggest(string word, int maxSuggestions = 5)
    {
        // Suggest from primary WordList only (most relevant)
        if (_wordList is null) return [];
        return [.. _wordList.Suggest(word).Take(maxSuggestions)];
    }

    public void AddToUserDictionary(string word)
    {
        if (_userWords.Add(word))
            AppendToUserDictFile(word);
    }

    private string UserDictPath => Path.Combine(_settings.DictionariesPath, "userdict.txt");

    private void LoadUserWords()
    {
        try
        {
            if (!File.Exists(UserDictPath)) return;
            foreach (var line in File.ReadLines(UserDictPath))
            {
                var w = line.Trim();
                if (w.Length > 0) _userWords.Add(w);
            }
        }
        catch { }
    }

    private void AppendToUserDictFile(string word)
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(UserDictPath)!);
            File.AppendAllText(UserDictPath, word + Environment.NewLine);
        }
        catch { }
    }
}
