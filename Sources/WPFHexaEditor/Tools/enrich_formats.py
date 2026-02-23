#!/usr/bin/env python3
"""
Comprehensive format enrichment tool.
Adds metadata, validation, relationships, and quality metrics to format definitions.
"""
import json, sys, os
from pathlib import Path
from typing import Dict, List

# Known format metadata
FORMAT_METADATA = {
    # Archives
    "ZIP": {
        "mime": ["application/zip", "application/x-zip-compressed"],
        "software": ["WinZip", "7-Zip", "WinRAR", "Info-ZIP"],
        "use_cases": ["file compression", "package distribution", "data archival"],
        "endianness": "little",
        "max_size": "4GB (ZIP64: unlimited)",
        "related": ["GZIP", "TAR", "RAR", "7Z"]
    },
    "PNG": {
        "mime": ["image/png"],
        "software": ["GIMP", "Photoshop", "Paint.NET", "IrfanView"],
        "use_cases": ["web graphics", "screenshots", "icons", "lossless images"],
        "endianness": "big",
        "compression": "DEFLATE",
        "related": ["APNG", "JPEG", "GIF", "WEBP"]
    },
    "PDF": {
        "mime": ["application/pdf"],
        "software": ["Adobe Acrobat", "Foxit Reader", "PDF-XChange"],
        "use_cases": ["documents", "forms", "ebooks", "print-ready files"],
        "related": ["PS", "EPS", "XPS"]
    },
    "MP4": {
        "mime": ["video/mp4", "audio/mp4"],
        "software": ["VLC", "FFmpeg", "QuickTime", "Windows Media Player"],
        "use_cases": ["video streaming", "video sharing", "mobile video"],
        "container_of": ["H.264", "H.265", "AAC", "MP3"],
        "related": ["MOV", "MKV", "AVI", "WEBM"]
    },
}

# Common MIME type patterns
MIME_PATTERNS = {
    "Archives": "application/x-",
    "Images": "image/",
    "Audio": "audio/",
    "Video": "video/",
    "Documents": "application/",
    "Executables": "application/x-executable"
}

def load_json(path):
    with open(path, 'r', encoding='utf-8-sig') as f:
        return json.load(f)

def save_json(path, data):
    with open(path, 'w', encoding='utf-8-sig') as f:
        json.dump(data, f, indent=2, ensure_ascii=False)

def generate_mime_types(format_name, category, extensions):
    """Generate probable MIME types based on format info."""
    mimes = []
    
    # Check known formats first
    if format_name.upper() in FORMAT_METADATA:
        return FORMAT_METADATA[format_name.upper()].get("mime", [])
    
    # Generate based on category
    if category in MIME_PATTERNS:
        prefix = MIME_PATTERNS[category]
        if extensions:
            ext = extensions[0].lstrip('.')
            mimes.append(f"{prefix}{ext}")
    
    return mimes

def get_metadata_enhancements(data):
    """Generate enhanced metadata for a format."""
    name = data.get("formatName", "")
    category = data.get("category", "")
    exts = data.get("extensions", [])
    
    # Check if we have known metadata
    meta = FORMAT_METADATA.get(name.upper(), {})
    
    enhancements = {}
    
    # MIME types
    mimes = meta.get("mime") or generate_mime_types(name, category, exts)
    if mimes:
        enhancements["mime_types"] = mimes
    
    # Software
    if "software" in meta:
        enhancements["software"] = meta["software"]
    
    # Use cases
    if "use_cases" in meta:
        enhancements["use_cases"] = meta["use_cases"]
    
    # Technical details
    tech = {}
    if "endianness" in meta:
        tech["endianness"] = meta["endianness"]
    if "compression" in meta:
        tech["compression"] = meta["compression"]
    if "max_size" in meta:
        tech["max_file_size"] = meta["max_size"]
    if tech:
        enhancements["technical_details"] = tech
    
    # Related formats
    if "related" in meta:
        enhancements["related_formats"] = meta["related"]
    if "container_of" in meta:
        enhancements["container_of"] = meta["container_of"]
    
    return enhancements

def enhance_detection(data):
    """Enhance detection section with validation."""
    detection = data.get("detection", {})
    
    # Add validation if signature exists
    if "signature" in detection:
        if "validation" not in detection:
            detection["validation"] = {
                "min_file_size": len(detection.get("signature", "")) // 2,
                "max_signature_offset": 1024
            }
    
    return detection

def add_quality_metrics(data):
    """Calculate and add quality metrics."""
    blocks = data.get("blocks", [])
    validation = data.get("validation_rules", {})
    
    completeness = 50  # Base score
    
    # Add points for features
    if data.get("description"): completeness += 5
    if data.get("references"): completeness += 10
    if data.get("mime_types"): completeness += 5
    if data.get("software"): completeness += 5
    if len(blocks) > 5: completeness += 10
    if len(blocks) > 10: completeness += 5
    if validation: completeness += 10
    
    # Determine documentation level
    doc_level = "basic"
    if len(blocks) > 10: doc_level = "detailed"
    elif len(blocks) > 5: doc_level = "moderate"
    
    return {
        "completeness_score": min(completeness, 100),
        "documentation_level": doc_level,
        "blocks_defined": len(blocks),
        "validation_rules": len(validation) if isinstance(validation, dict) else 0,
        "last_updated": "2026-02-23"
    }

def enrich_format(file_path, options):
    """Main enrichment function."""
    data = load_json(file_path)
    modified = False
    
    # Add metadata
    if options.get("metadata", True):
        meta = get_metadata_enhancements(data)
        for key, value in meta.items():
            if key not in data:
                data[key] = value
                modified = True
    
    # Enhance detection
    if options.get("detection", True):
        if "detection" in data:
            enhanced = enhance_detection(data)
            if enhanced != data.get("detection"):
                data["detection"] = enhanced
                modified = True
    
    # Add quality metrics
    if options.get("quality", True):
        if "quality_metrics" not in data:
            data["quality_metrics"] = add_quality_metrics(data)
            modified = True
    
    if modified:
        save_json(file_path, data)
        return True
    return False

# Main execution
if __name__ == "__main__":
    fd = Path("FormatDefinitions")
    stats = {"enhanced": 0, "skipped": 0}
    
    # Options
    opts = {
        "metadata": True,
        "detection": True,
        "quality": True
    }
    
    dry_run = "--dry-run" in sys.argv
    
    for cat in sorted(fd.iterdir()):
        if not cat.is_dir(): continue
        print(f"\n[{cat.name}]")
        
        for jf in sorted(cat.glob("*.json")):
            try:
                if dry_run:
                    # Check what would be added
                    data = load_json(jf)
                    meta = get_metadata_enhancements(data)
                    if meta or "quality_metrics" not in data:
                        print(f"  Would enhance {jf.name}")
                        stats["enhanced"] += 1
                    else:
                        print(f"  Skip {jf.name}")
                        stats["skipped"] += 1
                else:
                    if enrich_format(jf, opts):
                        print(f"  Enhanced {jf.name}")
                        stats["enhanced"] += 1
                    else:
                        print(f"  Skip {jf.name}")
                        stats["skipped"] += 1
            except Exception as e:
                print(f"  Error {jf.name}: {e}")
    
    print(f"\nEnhanced: {stats['enhanced']}, Skipped: {stats['skipped']}")
