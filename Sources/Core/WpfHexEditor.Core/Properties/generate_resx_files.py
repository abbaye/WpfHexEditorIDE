#!/usr/bin/env python3
"""
Script pour générer 10 nouveaux fichiers .resx avec les valeurs anglaises
pour le projet WPFHexaEditor.
"""

import xml.etree.ElementTree as ET
from pathlib import Path

# Définir les 10 nouvelles langues à ajouter
NEW_LANGUAGES = [
    ("de-DE", "German"),
    ("it-IT", "Italian"),
    ("ja-JP", "Japanese"),
    ("ko-KR", "Korean"),
    ("nl-NL", "Dutch"),
    ("sv-SE", "Swedish"),
    ("tr-TR", "Turkish"),
    ("ar-SA", "Arabic"),
    ("hi-IN", "Hindi"),
    ("pt-PT", "Portuguese (Portugal)")
]

# Chemin vers les fichiers
SCRIPT_DIR = Path(__file__).parent
RESOURCES_FILE = SCRIPT_DIR / "Resources.resx"

def parse_resources(file_path):
    """Parse le fichier Resources.resx et extrait toutes les entrées de données."""
    tree = ET.parse(file_path)
    root = tree.getroot()

    # Définir les namespaces pour éviter les préfixes
    namespaces = {'': 'http://www.w3.org/2001/XMLSchema'}

    resources = []

    # Extraire toutes les entrées <data>
    for data_elem in root.findall("data"):
        name = data_elem.get("name")
        xml_space = data_elem.get("{http://www.w3.org/XML/1998/namespace}space")
        value_elem = data_elem.find("value")

        # Ignorer les références de fichiers (EBCDIC)
        type_attr = data_elem.get("type")
        if type_attr and "ResXFileRef" in type_attr:
            continue

        if value_elem is not None:
            value = value_elem.text if value_elem.text else ""
            resources.append({
                "name": name,
                "value": value,
                "xml_space": xml_space
            })

    return resources

def generate_resx_file(language_code, language_name, resources, output_path):
    """Génère un fichier .resx pour une langue donnée."""

    # Template du header XML
    xml_content = '''<?xml version="1.0" encoding="utf-8"?>
<root>
  <xsd:schema id="root" xmlns="" xmlns:xsd="http://www.w3.org/2001/XMLSchema" xmlns:msdata="urn:schemas-microsoft-com:xml-msdata">
    <xsd:import namespace="http://www.w3.org/XML/1998/namespace" />
    <xsd:element name="root" msdata:IsDataSet="true">
      <xsd:complexType>
        <xsd:choice maxOccurs="unbounded">
          <xsd:element name="metadata">
            <xsd:complexType>
              <xsd:sequence>
                <xsd:element name="value" type="xsd:string" minOccurs="0" />
              </xsd:sequence>
              <xsd:attribute name="name" use="required" type="xsd:string" />
              <xsd:attribute name="type" type="xsd:string" />
              <xsd:attribute name="mimetype" type="xsd:string" />
              <xsd:attribute ref="xml:space" />
            </xsd:complexType>
          </xsd:element>
          <xsd:element name="assembly">
            <xsd:complexType>
              <xsd:attribute name="alias" type="xsd:string" />
              <xsd:attribute name="name" type="xsd:string" />
            </xsd:complexType>
          </xsd:element>
          <xsd:element name="data">
            <xsd:complexType>
              <xsd:sequence>
                <xsd:element name="value" type="xsd:string" minOccurs="0" msdata:Ordinal="1" />
                <xsd:element name="comment" type="xsd:string" minOccurs="0" msdata:Ordinal="2" />
              </xsd:sequence>
              <xsd:attribute name="name" type="xsd:string" use="required" msdata:Ordinal="1" />
              <xsd:attribute name="type" type="xsd:string" msdata:Ordinal="3" />
              <xsd:attribute name="mimetype" type="xsd:string" msdata:Ordinal="4" />
              <xsd:attribute ref="xml:space" />
            </xsd:complexType>
          </xsd:element>
          <xsd:element name="resheader">
            <xsd:complexType>
              <xsd:sequence>
                <xsd:element name="value" type="xsd:string" minOccurs="0" msdata:Ordinal="1" />
              </xsd:sequence>
              <xsd:attribute name="name" type="xsd:string" use="required" />
            </xsd:complexType>
          </xsd:element>
        </xsd:choice>
      </xsd:complexType>
    </xsd:element>
  </xsd:schema>
  <resheader name="resmimetype">
    <value>text/microsoft-resx</value>
  </resheader>
  <resheader name="version">
    <value>2.0</value>
  </resheader>
  <resheader name="reader">
    <value>System.Resources.ResXResourceReader, System.Windows.Forms, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089</value>
  </resheader>
  <resheader name="writer">
    <value>System.Resources.ResXResourceWriter, System.Windows.Forms, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089</value>
  </resheader>

  <!-- {language_name} ({language_code}) - Generated with English placeholders -->
'''

    # Remplacer les placeholders
    xml_content = xml_content.replace("{language_name}", language_name).replace("{language_code}", language_code)

    # Ajouter toutes les ressources
    for resource in resources:
        name = resource["name"]
        value = resource["value"]
        xml_space = resource["xml_space"]

        # Échapper les caractères XML dans la valeur
        value_escaped = value.replace("&", "&amp;").replace("<", "&lt;").replace(">", "&gt;")

        # Si xml:space="preserve" est défini
        xml_space_attr = ' xml:space="preserve"' if xml_space else ''

        xml_content += f'  <data name="{name}"{xml_space_attr}>\n'
        xml_content += f'    <value>{value_escaped}</value>\n'
        xml_content += f'  </data>\n'

    # Fermer le root
    xml_content += '</root>\n'

    # Écrire le fichier avec encodage UTF-8 BOM
    with open(output_path, 'w', encoding='utf-8-sig') as f:
        f.write(xml_content)

    print(f"[OK] Cree: {output_path.name} ({len(resources)} ressources)")

def main():
    """Fonction principale."""
    print("=" * 60)
    print("Generateur de fichiers .resx pour WPFHexaEditor")
    print("=" * 60)
    print()

    # Vérifier que Resources.resx existe
    if not RESOURCES_FILE.exists():
        print(f"ERREUR: {RESOURCES_FILE} n'existe pas!")
        return

    print(f"Lecture de {RESOURCES_FILE.name}...")
    resources = parse_resources(RESOURCES_FILE)
    print(f"   {len(resources)} ressources traduisibles trouvees")
    print()

    # Générer les fichiers pour chaque langue
    print("Generation des fichiers .resx:")
    print()

    for lang_code, lang_name in NEW_LANGUAGES:
        output_file = SCRIPT_DIR / f"Resources.{lang_code}.resx"
        generate_resx_file(lang_code, lang_name, resources, output_file)

    print()
    print("=" * 60)
    print(f"TERMINE! {len(NEW_LANGUAGES)} fichiers crees avec succes.")
    print()
    print("Prochaines etapes:")
    print("  1. Reviser les fichiers generes")
    print("  2. Commencer les traductions (AI-assiste + revision humaine)")
    print("  3. Valider les format strings")
    print("  4. Tester avec 'dotnet build'")
    print("=" * 60)

if __name__ == "__main__":
    main()
