#!/usr/bin/env python3
"""
Script to replace translated "Offset" back to English in all .resx files
"""
import re
from pathlib import Path

# Translations to replace with "Offset"
OFFSET_TRANSLATIONS = {
    "ar-SA": "الإزاحة",
    "es-419": "Desplazamiento",
    "es-ES": "Desplazamiento",
    "fr-CA": "Décalage",
    "fr-FR": "Décalage",
    "hi-IN": "ऑफसेट",
    "ja-JP": "オフセット",
    "ko-KR": "오프셋",
    "pl-PL": "Przesunięcie",
    "pt-BR": "Deslocamento",
    "pt-PT": "Deslocamento",
    "ru-RU": "Смещение",
    "tr-TR": "Ofset",
    "zh-CN": "偏移"
}

def fix_offset_in_file(file_path, lang_code, is_sample=False):
    """Replace translated Offset with English 'Offset' in a .resx file"""
    if lang_code not in OFFSET_TRANSLATIONS:
        print(f"[SKIP] {file_path.name} - No translation to fix")
        return 0

    translated_offset = OFFSET_TRANSLATIONS[lang_code]

    with open(file_path, 'r', encoding='utf-8-sig') as f:
        content = f.read()

    # Count replacements
    count = 0

    if not is_sample:
        # Main project patterns
        # Replace standalone Offset (OffsetString)
        pattern1 = f'<data name="OffsetString"[^>]*>\\s*<value>{re.escape(translated_offset)}</value>'
        if re.search(pattern1, content):
            content = re.sub(pattern1, lambda m: m.group(0).replace(translated_offset, 'Offset'), content)
            count += 1

        # Replace in "Go to Offset" - keep the "Go to" translation
        pattern2 = f'(<data name="GoToOffsetString"[^>]*>\\s*<value>)([^<]*){re.escape(translated_offset)}([^<]*)(</value>)'
        matches = re.findall(pattern2, content)
        if matches:
            content = re.sub(pattern2, lambda m: m.group(1) + m.group(2) + 'Offset' + m.group(3) + m.group(4), content)
            count += len(matches)
    else:
        # Sample app patterns
        # Replace in "Show Offset" - keep the "Show" translation but replace the translated offset word
        # This handles cases like "Afficher le décalage", "Mostrar _desplazamiento", etc.
        pattern3 = f'(<data name="Menu_View_ShowOffset"[^>]*>\\s*<value>)([^<]*)(</value>)'
        match = re.search(pattern3, content)
        if match:
            original_value = match.group(2)
            # Replace the translated offset word with "Offset"
            new_value = original_value.replace(translated_offset, 'Offset')
            # Also handle lowercase/uppercase variations
            new_value = new_value.replace(translated_offset.lower(), 'Offset')
            new_value = new_value.replace(translated_offset.capitalize(), 'Offset')
            if new_value != original_value:
                content = content.replace(match.group(0), match.group(1) + new_value + match.group(3))
                count += 1

    if count > 0:
        with open(file_path, 'w', encoding='utf-8-sig') as f:
            f.write(content)
        print(f"[OK] {file_path.name} - {count} occurrences fixed")
        return count
    else:
        print(f"[SKIP] {file_path.name} - No occurrences found")
        return 0

def main():
    """Main function"""
    print("=" * 60)
    print("Fixing 'Offset' translations in .resx files")
    print("=" * 60)
    print()

    # Fix main project
    print("Main project (WPFHexaEditor):")
    main_dir = Path(__file__).parent
    total = 0

    for lang_code in OFFSET_TRANSLATIONS.keys():
        file_path = main_dir / f"Resources.{lang_code}.resx"
        if file_path.exists():
            total += fix_offset_in_file(file_path, lang_code)

    print()

    # Fix sample app
    print("Sample app:")
    sample_dir = main_dir.parent.parent / "Samples" / "WpfHexEditor.Sample.Main" / "Properties"

    for lang_code in OFFSET_TRANSLATIONS.keys():
        file_path = sample_dir / f"Resources.{lang_code}.resx"
        if file_path.exists():
            total += fix_offset_in_file(file_path, lang_code, is_sample=True)

    print()
    print("=" * 60)
    print(f"DONE! {total} occurrences fixed total")
    print("=" * 60)

if __name__ == "__main__":
    main()
