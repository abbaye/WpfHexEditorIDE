#!/usr/bin/env python3
"""
Apply translations from a JSON file to .resx satellite files.

Usage:
    python translate_resources.py <resx_dir> [translations_json]

Arguments:
    resx_dir          Directory containing Resources.resx and Resources.*.resx files.
    translations_json Path to translations JSON file (default: <resx_dir>/translations.json).

JSON format:
    {
        "fr-FR": { "KeyName": "Translated value", ... },
        "de-DE": { "KeyName": "Translated value", ... }
    }
"""

import re
import sys
import json
from pathlib import Path


def update_resx_with_translations(resx_path: Path, translations: dict) -> None:
    """Update a .resx file with the provided translations."""
    with open(resx_path, 'r', encoding='utf-8-sig') as f:
        content = f.read()

    for key, translated_value in translations.items():
        escaped = (translated_value
                   .replace("&", "&amp;")
                   .replace("<", "&lt;")
                   .replace(">", "&gt;"))

        pattern = f'(<data name="{re.escape(key)}"[^>]*>\\s*<value>)(.*?)(</value>)'

        def replace_value(match):
            return match.group(1) + escaped + match.group(3)

        content = re.sub(pattern, replace_value, content, flags=re.DOTALL)

    with open(resx_path, 'w', encoding='utf-8-sig') as f:
        f.write(content)

    print(f"[OK] Updated: {resx_path.name} ({len(translations)} translations)")


def main() -> None:
    if len(sys.argv) < 2:
        print("Usage: translate_resources.py <resx_dir> [translations_json]")
        sys.exit(1)

    resx_dir = Path(sys.argv[1]).resolve()
    translations_file = Path(sys.argv[2]).resolve() if len(sys.argv) >= 3 else resx_dir / "translations.json"

    if not resx_dir.is_dir():
        print(f"ERROR: Directory not found: {resx_dir}")
        sys.exit(1)

    if not translations_file.exists():
        print(f"ERROR: translations.json not found: {translations_file}")
        sys.exit(1)

    with open(translations_file, 'r', encoding='utf-8') as f:
        all_translations = json.load(f)

    print("=" * 60)
    print(f"Applying translations from: {translations_file.name}")
    print(f"Target directory          : {resx_dir}")
    print("=" * 60)

    for lang_code, translations in all_translations.items():
        resx_path = resx_dir / f"Resources.{lang_code}.resx"
        if not resx_path.exists():
            print(f"[--] Skipped: {resx_path.name} (file not found)")
            continue
        update_resx_with_translations(resx_path, translations)

    print("=" * 60)
    print("Done.")
    print("=" * 60)


if __name__ == "__main__":
    main()
