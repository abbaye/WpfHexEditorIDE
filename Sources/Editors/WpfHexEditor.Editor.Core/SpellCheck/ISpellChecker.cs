// ==========================================================
// Project: WpfHexEditor.Editor.Core
// File: SpellCheck/ISpellChecker.cs
// Description:
//     SDK-level contract for spell checking.
//     Implementations are provided by DocumentEditor (Hunspell)
//     and accessed via DocumentEditorHost.SpellChecker.
// ==========================================================

namespace WpfHexEditor.Editor.Core.SpellCheck;

/// <summary>
/// Spell checking contract. Implementations must be thread-safe
/// (CheckWord / Suggest are called from a background thread).
/// </summary>
public interface ISpellChecker
{
    /// <summary>True when a dictionary is loaded and ready to use.</summary>
    bool IsLoaded { get; }

    /// <summary>BCP-47 language code of the active dictionary (e.g. "fr-CA").</summary>
    string? ActiveLanguage { get; }

    /// <summary>Returns true when the word is spelled correctly.</summary>
    bool CheckWord(string word);

    /// <summary>Returns up to <paramref name="maxSuggestions"/> correction candidates.</summary>
    IReadOnlyList<string> Suggest(string word, int maxSuggestions = 5);

    /// <summary>Adds <paramref name="word"/> to the user dictionary (persisted to disk).</summary>
    void AddToUserDictionary(string word);

    /// <summary>Loads the dictionary for <paramref name="languageCode"/>. Safe to call multiple times.</summary>
    Task LoadAsync(string languageCode, CancellationToken ct = default);

    /// <summary>Raised when the active dictionary changes (load / unload).</summary>
    event EventHandler? DictionaryChanged;
}
