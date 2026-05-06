// ==========================================================
// Project: WpfHexEditor.Core.SpellCheck
// File: ISpellChecker.cs
// Description:
//     SDK-level contract for spell checking.
//     Supports single-language and multi-language (all installed) modes.
// ==========================================================

namespace WpfHexEditor.Core.SpellCheck;

/// <summary>
/// Spell checking contract. Implementations must be thread-safe
/// (CheckWord / Suggest are called from a background thread).
/// </summary>
public interface ISpellChecker
{
    /// <summary>True when at least one dictionary is loaded and ready.</summary>
    bool IsLoaded { get; }

    /// <summary>
    /// BCP-47 language code of the primary active dictionary (e.g. "fr-CA").
    /// Null when no language is explicitly selected (multi-language mode).
    /// </summary>
    string? ActiveLanguage { get; }

    /// <summary>
    /// When true, CheckWord accepts a word if ANY installed dictionary recognises it.
    /// When false, only the ActiveLanguage dictionary is used.
    /// </summary>
    bool MultiLanguageMode { get; set; }

    /// <summary>Returns true when the word is spelled correctly.</summary>
    bool CheckWord(string word);

    /// <summary>Returns up to <paramref name="maxSuggestions"/> correction candidates.</summary>
    IReadOnlyList<string> Suggest(string word, int maxSuggestions = 5);

    /// <summary>Adds <paramref name="word"/> to the user dictionary (persisted to disk).</summary>
    void AddToUserDictionary(string word);

    /// <summary>Loads the dictionary for <paramref name="languageCode"/>. Safe to call multiple times.</summary>
    Task LoadAsync(string languageCode, CancellationToken ct = default);

    /// <summary>
    /// Loads ALL installed dictionaries for multi-language mode.
    /// Replaces any previously loaded set.
    /// </summary>
    Task LoadAllInstalledAsync(CancellationToken ct = default);

    /// <summary>Raised when the active dictionary set changes (load / unload).</summary>
    event EventHandler? DictionaryChanged;
}
