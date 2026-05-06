// ==========================================================
// Project: WpfHexEditor.Editor.DocumentEditor
// File: SpellCheck/HunspellSpellChecker.cs
// Description:
//     ISpellChecker implementation backed by WeCantSpell.Hunspell.
//     WordList is immutable after load — thread-safe for concurrent CheckWord/Suggest calls.
//     User dictionary is a plain text file; words appended on AddToUserDictionary.
// Architecture:
//     LoadAsync replaces the active WordList; old instance is disposed.
//     User words are loaded at startup and merged into an in-memory HashSet
//     so they survive without rebuilding the full Hunspell index.
// ==========================================================

using System.IO;
using System.Text.RegularExpressions;
using WeCantSpell.Hunspell;
using WpfHexEditor.Editor.Core.SpellCheck;

namespace WpfHexEditor.Editor.DocumentEditor.SpellCheck;

internal sealed class HunspellSpellChecker : ISpellChecker, IDisposable
{
    private readonly SpellCheckerSettings _settings;
    private readonly DictionaryManager    _dictManager;
    private WordList?                     _wordList;
    private readonly HashSet<string>      _userWords = new(StringComparer.OrdinalIgnoreCase);
    private string?                       _activeLanguage;
    private readonly SemaphoreSlim        _loadLock = new(1, 1);

    public bool    IsLoaded        => _wordList is not null;
    public string? ActiveLanguage  => _activeLanguage;

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
            var old  = _wordList;
            _wordList = await WordList.CreateFromFilesAsync(info.DicPath, info.AffPath, ct);
            _activeLanguage = languageCode;
            old?.Dispose();
        }
        finally
        {
            _loadLock.Release();
        }
        DictionaryChanged?.Invoke(this, EventArgs.Empty);
    }

    public bool CheckWord(string word)
    {
        if (_wordList is null) return true; // no dict loaded — don't mark anything wrong
        if (_userWords.Contains(word)) return true;
        return _wordList.Check(word);
    }

    public IReadOnlyList<string> Suggest(string word, int maxSuggestions = 5)
    {
        if (_wordList is null) return [];
        return [.. _wordList.Suggest(word).Take(maxSuggestions)];
    }

    public void AddToUserDictionary(string word)
    {
        if (_userWords.Add(word))
            AppendToUserDictFile(word);
    }

    public void Dispose()
    {
        _wordList?.Dispose();
        _loadLock.Dispose();
    }

    // ── User dictionary persistence ───────────────────────────────────────

    private string UserDictPath => Path.Combine(
        _settings.DictionariesPath, "userdict.txt");

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
        catch { /* non-critical */ }
    }

    private void AppendToUserDictFile(string word)
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(UserDictPath)!);
            File.AppendAllText(UserDictPath, word + Environment.NewLine);
        }
        catch { /* non-critical */ }
    }
}
