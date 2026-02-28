#!/usr/bin/env python3
"""
Script de traduction par lots pour les fichiers .resx
Génère les traductions et met à jour les fichiers automatiquement
"""

import xml.etree.ElementTree as ET
from pathlib import Path
import re

SCRIPT_DIR = Path(__file__).parent

# Définition des langues
LANGUAGES = {
    "de-DE": "German",
    "it-IT": "Italian",
    "ja-JP": "Japanese",
    "ko-KR": "Korean",
    "nl-NL": "Dutch",
    "sv-SE": "Swedish",
    "tr-TR": "Turkish",
    "ar-SA": "Arabic",
    "hi-IN": "Hindi",
    "pt-PT": "Portuguese"
}

def extract_english_strings(resx_path):
    """Extrait toutes les chaînes anglaises du fichier Resources.resx"""
    with open(resx_path, 'r', encoding='utf-8-sig') as f:
        content = f.read()

    # Parser les entrées <data name="X"><value>Y</value></data>
    pattern = r'<data name="([^"]+)"[^>]*>\s*<value>(.*?)</value>'
    matches = re.findall(pattern, content, re.DOTALL)

    strings = {}
    for name, value in matches:
        # Déséchapper les entités XML
        value = (value
            .replace("&lt;", "<")
            .replace("&gt;", ">")
            .replace("&amp;", "&"))
        strings[name] = value

    return strings

def apply_translations_to_resx(resx_path, translations):
    """Applique les traductions à un fichier .resx"""
    with open(resx_path, 'r', encoding='utf-8-sig') as f:
        content = f.read()

    # Remplacer chaque valeur
    for key, translated_value in translations.items():
        # Échapper les caractères XML
        translated_value_escaped = (translated_value
            .replace("&", "&amp;")
            .replace("<", "&lt;")
            .replace(">", "&gt;"))

        # Pattern pour trouver et remplacer
        pattern = f'(<data name="{re.escape(key)}"[^>]*>\\s*<value>)(.*?)(</value>)'

        def replace_value(match):
            return match.group(1) + translated_value_escaped + match.group(3)

        content = re.sub(pattern, replace_value, content, flags=re.DOTALL)

    # Écrire le fichier mis à jour
    with open(resx_path, 'w', encoding='utf-8-sig') as f:
        f.write(content)

def main():
    """Fonction principale"""
    print("=" * 60)
    print("Extraction des chaines anglaises")
    print("=" * 60)

    resources_file = SCRIPT_DIR / "Resources.resx"
    english_strings = extract_english_strings(resources_file)

    print(f"Extrait {len(english_strings)} chaines anglaises")
    print()

    # Afficher toutes les chaînes pour traduction manuelle
    print("Chaines a traduire:")
    print("-" * 60)
    for i, (key, value) in enumerate(english_strings.items(), 1):
        # Limiter l'affichage pour ne pas saturer
        if len(value) > 80:
            display_value = value[:77] + "..."
        else:
            display_value = value
        print(f"{i}. {key}: {display_value}")

    print()
    print("=" * 60)
    print("Pour appliquer les traductions, modifiez ce script")
    print("et ajoutez les dictionnaires de traduction pour chaque langue.")
    print("=" * 60)

if __name__ == "__main__":
    main()
