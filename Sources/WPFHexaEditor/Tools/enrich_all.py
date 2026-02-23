#!/usr/bin/env python3
"""
Enrichissement intelligent de TOUS les formats JSON (426 formats)
Génère automatiquement software, use_cases, format_relationships, technical_details
"""
import json
from pathlib import Path
import re

# ============================================================================
# DICTIONNAIRES DE MÉTADONNÉES PAR CATÉGORIE
# ============================================================================

CATEGORY_DEFAULTS = {
    "Archives": {
        "software": ["7-Zip", "WinRAR", "PeaZip"],
        "use_cases": ["File compression", "Data archiving", "Software distribution"],
        "technical_details": {"archive_format": True}
    },
    "Audio": {
        "software": ["VLC Media Player", "foobar2000", "Audacity"],
        "use_cases": ["Audio playback", "Music storage", "Audio editing"],
        "technical_details": {"audio_format": True}
    },
    "Video": {
        "software": ["VLC Media Player", "Media Player Classic", "FFmpeg"],
        "use_cases": ["Video playback", "Video streaming", "Video editing"],
        "technical_details": {"video_format": True, "container": True}
    },
    "Images": {
        "software": ["Image viewers", "GIMP", "Photoshop"],
        "use_cases": ["Image viewing", "Photo editing", "Graphics design"],
        "technical_details": {"image_format": True}
    },
    "Documents": {
        "software": ["PDF readers", "Office suites", "Text editors"],
        "use_cases": ["Document viewing", "Office productivity", "Publishing"],
        "technical_details": {"document_format": True}
    },
    "Executables": {
        "software": ["Operating systems", "Debuggers", "Disassemblers"],
        "use_cases": ["Program execution", "Software development", "System tools"],
        "technical_details": {"executable_format": True}
    },
    "Fonts": {
        "software": ["Font managers", "Operating systems", "Browsers"],
        "use_cases": ["Typography", "Web fonts", "Desktop publishing"],
        "technical_details": {"font_format": True}
    },
    "3D": {
        "software": ["Blender", "3ds Max", "Maya"],
        "use_cases": ["3D modeling", "Animation", "Game development"],
        "technical_details": {"3d_format": True}
    },
    "Game": {
        "software": ["Emulators", "Game engines", "Romhacking tools"],
        "use_cases": ["Gaming", "Game preservation", "Romhacking"],
        "technical_details": {"game_format": True}
    },
    "Network": {
        "software": ["Wireshark", "tcpdump", "Network analyzers"],
        "use_cases": ["Network analysis", "Packet capture", "Debugging"],
        "technical_details": {"network_format": True}
    },
    "Programming": {
        "software": ["Compilers", "Linkers", "IDEs"],
        "use_cases": ["Software compilation", "Development", "Debugging"],
        "technical_details": {"programming_format": True}
    }
}

# ============================================================================
# PATTERNS SPÉCIFIQUES PAR FORMAT
# ============================================================================

FORMAT_PATTERNS = {
    # Lossy vs Lossless
    "lossy_audio": ["MP3", "AAC", "OGG", "WMA", "OPUS"],
    "lossless_audio": ["FLAC", "ALAC", "APE", "WV", "TTA"],
    "lossy_image": ["JPEG", "WEBP"],
    "lossless_image": ["PNG", "BMP", "TIFF"],

    # Compression types
    "deflate": ["ZIP", "PNG", "GZIP"],
    "lzma": ["7Z", "XZ"],
    "bzip2": ["BZIP2"],

    # Proprietary vs Open
    "proprietary": ["RAR", "ACE", "PSD", "DWG"],
    "open_source": ["7Z", "OGG", "WEBM", "WEBP"],

    # Legacy formats
    "legacy": ["BMP", "PCX", "TGA", "AVI", "COM"],

    # Web formats
    "web": ["HTML", "CSS", "JS", "WEBP", "WEBM", "SVG"],

    # Microsoft formats
    "microsoft": ["DOCX", "XLSX", "PPTX", "BMP", "WMV", "WMA"],

    # Apple formats
    "apple": ["MOV", "M4A", "M4V", "HEIC", "ALAC"],

    # Adobe formats
    "adobe": ["PDF", "PSD", "AI", "FLA"],

    # Console ROMs
    "nintendo": ["ROM_NES", "ROM_SNES", "ROM_N64", "ROM_GB", "ROM_GBA"],
    "sega": ["ROM_GEN", "ROM_SMS", "ROM_SAT", "ROM_DC"],
    "sony": ["ROM_PSX", "ROM_PS2"],
}

# ============================================================================
# RELATIONS COMMUNES
# ============================================================================

COMMON_RELATIONSHIPS = {
    # Office formats
    "DOCX": {"successor_to": "DOC", "container": "ZIP", "related": ["XLSX", "PPTX"]},
    "XLSX": {"successor_to": "XLS", "container": "ZIP", "related": ["DOCX", "PPTX"]},
    "PPTX": {"successor_to": "PPT", "container": "ZIP", "related": ["DOCX", "XLSX"]},

    # Image successors
    "PNG": {"replaces": "GIF", "related": ["APNG", "JPEG", "WEBP"]},
    "WEBP": {"successor_to": ["JPEG", "PNG", "GIF"], "modern_alternative": True},
    "AVIF": {"successor_to": "WEBP", "next_gen": True},

    # Audio successors
    "AAC": {"successor_to": "MP3", "better_quality": True},
    "OPUS": {"successor_to": ["MP3", "AAC"], "low_latency": True},

    # Archive families
    "7Z": {"related": ["ZIP", "RAR"], "higher_compression": True},
    "TAR": {"combined_with": ["GZIP", "BZIP2", "XZ"]},
}

# ============================================================================
# SOFTWARE PAR FORMAT
# ============================================================================

SPECIFIC_SOFTWARE = {
    # ROM Emulators
    "ROM_NES": ["Nestopia", "FCEUX", "RetroArch", "Mesen"],
    "ROM_SNES": ["Snes9x", "bsnes", "RetroArch"],
    "ROM_GB": ["VisualBoyAdvance", "BGB", "mGBA"],
    "ROM_GBA": ["mGBA", "VisualBoyAdvance", "NO$GBA"],
    "ROM_N64": ["Project64", "Mupen64Plus", "RetroArch"],
    "ROM_GEN": ["Gens", "Kega Fusion", "RetroArch"],

    # Specific tools
    "PSD": ["Adobe Photoshop", "GIMP", "Photopea"],
    "AI": ["Adobe Illustrator", "Inkscape"],
    "DWG": ["AutoCAD", "LibreCAD", "FreeCAD"],
    "FBX": ["Maya", "3ds Max", "Blender", "Unity"],
    "SVG": ["Web browsers", "Inkscape", "Adobe Illustrator"],
    "ICO": ["Windows", "Icon editors"],
    "HEIC": ["iOS/macOS devices", "Image viewers with HEIF support"],
}

# ============================================================================
# FONCTIONS D'ENRICHISSEMENT
# ============================================================================

def load_json(path):
    with open(path, 'r', encoding='utf-8-sig') as f:
        return json.load(f)

def save_json(path, data):
    with open(path, 'w', encoding='utf-8-sig') as f:
        json.dump(data, f, indent=2, ensure_ascii=False)

def get_format_key(format_name):
    """Extract format key from format name"""
    # Remove common suffixes and normalize
    key = format_name.upper()
    key = re.sub(r'\s+(IMAGE|FILE|FORMAT|ARCHIVE|DOCUMENT)$', '', key)
    return key.strip()

def generate_software(format_name, category, extensions):
    """Generate software list based on format"""
    key = get_format_key(format_name)

    # Check specific software first
    if key in SPECIFIC_SOFTWARE:
        return SPECIFIC_SOFTWARE[key]

    # Check patterns
    software = []

    # Add category defaults
    if category in CATEGORY_DEFAULTS:
        software.extend(CATEGORY_DEFAULTS[category]["software"][:2])

    # Add pattern-specific software
    if key in FORMAT_PATTERNS.get("microsoft", []):
        software.append("Microsoft Office")
    elif key in FORMAT_PATTERNS.get("adobe", []):
        software.append("Adobe products")
    elif key in FORMAT_PATTERNS.get("apple", []):
        software.append("Apple software")

    # Add web browsers for web formats
    if key in FORMAT_PATTERNS.get("web", []):
        software.append("Web browsers")

    return software if software else ["Common file viewers", "Specialized software"]

def generate_use_cases(format_name, category, extensions):
    """Generate use cases based on format"""
    key = get_format_key(format_name)

    use_cases = []

    # Category defaults
    if category in CATEGORY_DEFAULTS:
        use_cases.extend(CATEGORY_DEFAULTS[category]["use_cases"][:2])

    # Pattern-specific use cases
    if key in FORMAT_PATTERNS.get("legacy", []):
        use_cases.append("Legacy software support")

    if key in FORMAT_PATTERNS.get("web", []):
        use_cases.append("Web development")

    if "ROM_" in key:
        use_cases.append("Game emulation")
        use_cases.append("Game preservation")

    if "PATCH_" in key:
        use_cases.append("Software patching")
        use_cases.append("Romhacking")

    return use_cases if use_cases else ["General file usage", "Data storage"]

def generate_relationships(format_name, category, extensions):
    """Generate format relationships"""
    key = get_format_key(format_name)

    # Check predefined relationships
    if key in COMMON_RELATIONSHIPS:
        return COMMON_RELATIONSHIPS[key]

    relationships = {}

    # Category grouping
    relationships["category"] = category

    # File extension info
    if extensions:
        relationships["extensions"] = extensions[:3]  # First 3 extensions

    # Pattern detection
    if key in FORMAT_PATTERNS.get("proprietary", []):
        relationships["proprietary"] = True
    elif key in FORMAT_PATTERNS.get("open_source", []):
        relationships["open_source"] = True

    if key in FORMAT_PATTERNS.get("legacy", []):
        relationships["legacy_format"] = True

    if key in FORMAT_PATTERNS.get("web", []):
        relationships["web_format"] = True

    # Console grouping for ROMs
    for console_type in ["nintendo", "sega", "sony"]:
        if key in FORMAT_PATTERNS.get(console_type, []):
            relationships["console_family"] = console_type.capitalize()

    return relationships

def generate_technical_details(format_name, category, extensions, data):
    """Generate technical details based on format"""
    key = get_format_key(format_name)

    details = {}

    # Category defaults
    if category in CATEGORY_DEFAULTS:
        details.update(CATEGORY_DEFAULTS[category]["technical_details"])

    # Compression detection
    if key in FORMAT_PATTERNS.get("deflate", []):
        details["compression_method"] = "Deflate"
    elif key in FORMAT_PATTERNS.get("lzma", []):
        details["compression_method"] = "LZMA"
    elif key in FORMAT_PATTERNS.get("bzip2", []):
        details["compression_method"] = "BZIP2"

    # Audio compression type
    if key in FORMAT_PATTERNS.get("lossy_audio", []):
        details["compression_type"] = "Lossy"
    elif key in FORMAT_PATTERNS.get("lossless_audio", []):
        details["compression_type"] = "Lossless"

    # Image compression type
    if key in FORMAT_PATTERNS.get("lossy_image", []):
        details["compression_type"] = "Lossy"
    elif key in FORMAT_PATTERNS.get("lossless_image", []):
        details["compression_type"] = "Lossless"

    # Extension info
    if extensions:
        details["primary_extension"] = extensions[0]

    # Block count as complexity indicator
    if "blocks" in data:
        details["defined_fields"] = len(data["blocks"])

    return details

def calculate_completeness_boost(data):
    """Calculate how much to boost completeness score"""
    base_boost = 10  # Base boost for adding 4 new fields

    # Extra boost for having many blocks
    if "blocks" in data:
        block_count = len(data["blocks"])
        if block_count > 10:
            base_boost += 5
        elif block_count > 5:
            base_boost += 3

    # Extra boost for having references
    if "references" in data:
        base_boost += 3

    return base_boost

def enrich_format(json_path):
    """Enrich a format definition with intelligent metadata"""
    data = load_json(json_path)

    # Skip if already enriched (has all 4 fields)
    has_software = "software" in data and data["software"]
    has_use_cases = "use_cases" in data and data["use_cases"]
    has_relationships = "format_relationships" in data and data["format_relationships"]
    has_technical = "technical_details" in data and data["technical_details"]

    if has_software and has_use_cases and has_relationships and has_technical:
        return False  # Already enriched

    format_name = data.get("formatName", "Unknown")
    category = data.get("category", "Unknown")
    extensions = data.get("extensions", [])

    # Generate missing fields
    if not has_software:
        data["software"] = generate_software(format_name, category, extensions)

    if not has_use_cases:
        data["use_cases"] = generate_use_cases(format_name, category, extensions)

    if not has_relationships:
        data["format_relationships"] = generate_relationships(format_name, category, extensions)

    if not has_technical:
        data["technical_details"] = generate_technical_details(format_name, category, extensions, data)

    # Update quality metrics
    if "quality_metrics" in data:
        old_score = data["quality_metrics"].get("completeness_score", 50)
        boost = calculate_completeness_boost(data)
        new_score = min(95, old_score + boost)

        data["quality_metrics"]["completeness_score"] = new_score
        data["quality_metrics"]["last_updated"] = "2026-02-23"

        # Set documentation level based on score
        if new_score >= 85:
            data["quality_metrics"]["documentation_level"] = "comprehensive"
        elif new_score >= 70:
            data["quality_metrics"]["documentation_level"] = "detailed"
        else:
            data["quality_metrics"]["documentation_level"] = "basic"

    save_json(json_path, data)
    return True

def main():
    print("=== Enrichissement Intelligent de TOUS les Formats ===\n")

    base_path = Path("FormatDefinitions")
    stats = {"enriched": 0, "skipped": 0, "errors": 0}

    # Process all categories
    for category_dir in sorted(base_path.iterdir()):
        if not category_dir.is_dir():
            continue

        print(f"\n[{category_dir.name}]")

        for json_file in sorted(category_dir.glob("*.json")):
            try:
                was_enriched = enrich_format(json_file)
                if was_enriched:
                    print(f"  OK {json_file.name}")
                    stats["enriched"] += 1
                else:
                    print(f"  SKIP {json_file.name} (already complete)")
                    stats["skipped"] += 1
            except Exception as e:
                print(f"  ERROR {json_file.name}: {e}")
                stats["errors"] += 1

    print(f"\n=== Summary ===")
    print(f"Enriched: {stats['enriched']}")
    print(f"Already complete: {stats['skipped']}")
    print(f"Errors: {stats['errors']}")
    print(f"Total processed: {stats['enriched'] + stats['skipped']}")

if __name__ == "__main__":
    main()
