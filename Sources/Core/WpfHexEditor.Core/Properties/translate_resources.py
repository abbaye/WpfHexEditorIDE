#!/usr/bin/env python3
"""
Script pour appliquer les traductions aux fichiers .resx
Ce script lit un fichier JSON contenant les traductions et met à jour les fichiers .resx
"""

import xml.etree.ElementTree as ET
from pathlib import Path
import json

SCRIPT_DIR = Path(__file__).parent

def update_resx_with_translations(resx_path, translations):
    """Met à jour un fichier .resx avec les traductions fournies."""

    # Lire le fichier existant
    with open(resx_path, 'r', encoding='utf-8-sig') as f:
        content = f.read()

    # Remplacer chaque valeur
    for key, translated_value in translations.items():
        # Échapper les caractères XML
        translated_value_escaped = (translated_value
            .replace("&", "&amp;")
            .replace("<", "&lt;")
            .replace(">", "&gt;"))

        # Pattern de recherche pour trouver la data entry
        # Chercher <data name="key"...><value>old_value</value></data>
        import re
        pattern = f'(<data name="{re.escape(key)}"[^>]*>\\s*<value>)(.*?)(</value>)'

        def replace_value(match):
            return match.group(1) + translated_value_escaped + match.group(3)

        content = re.sub(pattern, replace_value, content, flags=re.DOTALL)

    # Écrire le fichier mis à jour
    with open(resx_path, 'w', encoding='utf-8-sig') as f:
        f.write(content)

    print(f"[OK] Mis a jour: {resx_path.name} ({len(translations)} traductions)")

def main():
    """Fonction principale."""
    print("=" * 60)
    print("Application des traductions aux fichiers .resx")
    print("=" * 60)
    print()

    # Charger le fichier de traductions JSON
    translations_file = SCRIPT_DIR / "translations.json"

    if not translations_file.exists():
        print("ERREUR: translations.json n'existe pas!")
        print("Veuillez creer un fichier translations.json avec les traductions.")
        return

    with open(translations_file, 'r', encoding='utf-8') as f:
        all_translations = json.load(f)

    # Appliquer les traductions pour chaque langue
    for lang_code, translations in all_translations.items():
        resx_path = SCRIPT_DIR / f"Resources.{lang_code}.resx"

        if not resx_path.exists():
            print(f"ATTENTION: {resx_path.name} n'existe pas, passe...")
            continue

        update_resx_with_translations(resx_path, translations)

    print()
    print("=" * 60)
    print("TERMINE! Toutes les traductions ont ete appliquees.")
    print("=" * 60)

if __name__ == "__main__":
    main()
