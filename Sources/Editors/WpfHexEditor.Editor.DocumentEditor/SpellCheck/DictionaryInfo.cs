// ==========================================================
// Project: WpfHexEditor.Editor.DocumentEditor
// File: SpellCheck/DictionaryInfo.cs
// Description: Metadata for a spell-check dictionary entry.
// ==========================================================

namespace WpfHexEditor.Editor.DocumentEditor.SpellCheck;

internal sealed record DictionaryInfo(
    string LanguageCode,
    string DisplayName,
    bool   IsInstalled,
    string DicPath,
    string AffPath);
