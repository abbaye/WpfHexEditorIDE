#!/usr/bin/env python3
"""
Enrich priority file formats with metadata (software, use cases, quality metrics, etc.)
Uses PascalCase for property names (no JsonPropertyName attributes needed)
"""

import json
import os
from pathlib import Path

# Enrichment data for priority formats (Top 20 most common)
ENRICHMENT_DATA = {
    "ZIP": {
        "MimeTypes": ["application/zip", "application/x-zip-compressed"],
        "Software": ["WinZip", "7-Zip", "WinRAR", "Windows Explorer", "macOS Archive Utility"],
        "UseCases": ["File compression", "Software distribution", "Backup archives", "Document bundling"],
        "QualityMetrics": {
            "CompletenessScore": 95,
            "DocumentationLevel": "comprehensive",
            "BlocksDefined": 11,
            "ValidationRules": 3,
            "LastUpdated": "2025-01-15",
            "PriorityFormat": True,
            "AutoRefined": False
        },
        "TechnicalDetails": {
            "CompressionMethod": "DEFLATE, BZIP2, LZMA",
            "ArchivesFormat": True,
            "SupportsEncryption": True
        }
    },
    "PNG": {
        "MimeTypes": ["image/png"],
        "Software": ["Photoshop", "GIMP", "Paint.NET", "Preview", "Windows Photo Viewer"],
        "UseCases": ["Web graphics", "Lossless image storage", "Screenshots", "Icons", "Transparency support"],
        "QualityMetrics": {
            "CompletenessScore": 92,
            "DocumentationLevel": "comprehensive",
            "BlocksDefined": 8,
            "ValidationRules": 2,
            "LastUpdated": "2025-01-12",
            "PriorityFormat": True,
            "AutoRefined": False
        },
        "TechnicalDetails": {
            "ColorSpace": "RGB, RGBA, Grayscale, Indexed",
            "BitDepth": 8,
            "ImagesFormat": True,
            "SupportsEncryption": False
        }
    },
    "PDF": {
        "MimeTypes": ["application/pdf"],
        "Software": ["Adobe Acrobat", "Foxit Reader", "Chrome", "Edge", "Preview (macOS)"],
        "UseCases": ["Document distribution", "Forms", "Ebooks", "Print-ready files", "Archival"],
        "QualityMetrics": {
            "CompletenessScore": 88,
            "DocumentationLevel": "detailed",
            "BlocksDefined": 7,
            "ValidationRules": 2,
            "LastUpdated": "2025-01-10",
            "PriorityFormat": True,
            "AutoRefined": False
        },
        "TechnicalDetails": {
            "DocumentFormat": True,
            "SupportsEncryption": True
        }
    },
    "JPEG": {
        "MimeTypes": ["image/jpeg", "image/jpg"],
        "Software": ["Photoshop", "GIMP", "Lightroom", "Photos", "All modern browsers"],
        "UseCases": ["Photography", "Web images", "Social media", "Digital cameras", "Compression"],
        "QualityMetrics": {
            "CompletenessScore": 90,
            "DocumentationLevel": "comprehensive",
            "BlocksDefined": 6,
            "ValidationRules": 1,
            "LastUpdated": "2025-01-14",
            "PriorityFormat": True,
            "AutoRefined": False
        },
        "TechnicalDetails": {
            "ColorSpace": "YCbCr, RGB",
            "BitDepth": 8,
            "ImagesFormat": True,
            "CompressionMethod": "DCT (lossy)"
        }
    },
    "MP3": {
        "MimeTypes": ["audio/mpeg", "audio/mp3"],
        "Software": ["iTunes", "VLC", "Windows Media Player", "Spotify", "Audacity"],
        "UseCases": ["Music playback", "Podcasts", "Audiobooks", "Streaming", "Portable audio"],
        "QualityMetrics": {
            "CompletenessScore": 85,
            "DocumentationLevel": "detailed",
            "BlocksDefined": 5,
            "ValidationRules": 1,
            "LastUpdated": "2025-01-11",
            "PriorityFormat": True,
            "AutoRefined": False
        },
        "TechnicalDetails": {
            "SampleRate": 44100,
            "AudioFormat": True,
            "CompressionMethod": "MPEG-1 Audio Layer III (lossy)"
        }
    },
    "MP4": {
        "MimeTypes": ["video/mp4", "audio/mp4"],
        "Software": ["VLC", "Windows Media Player", "QuickTime", "Chrome", "All modern browsers"],
        "UseCases": ["Video streaming", "Mobile video", "Web video", "YouTube", "Social media"],
        "QualityMetrics": {
            "CompletenessScore": 87,
            "DocumentationLevel": "detailed",
            "BlocksDefined": 6,
            "ValidationRules": 2,
            "LastUpdated": "2025-01-13",
            "PriorityFormat": True,
            "AutoRefined": False
        },
        "TechnicalDetails": {
            "Container": "MPEG-4 Part 14",
            "VideoFormat": True,
            "SupportsEncryption": True
        }
    },
    "GIF": {
        "MimeTypes": ["image/gif"],
        "Software": ["Photoshop", "GIMP", "Web browsers", "Discord", "Slack"],
        "UseCases": ["Animations", "Simple graphics", "Memes", "Web banners", "Emojis"],
        "QualityMetrics": {
            "CompletenessScore": 80,
            "DocumentationLevel": "standard",
            "BlocksDefined": 4,
            "ValidationRules": 1,
            "LastUpdated": "2025-01-09",
            "PriorityFormat": True,
            "AutoRefined": False
        },
        "TechnicalDetails": {
            "ColorSpace": "Indexed (256 colors)",
            "BitDepth": 8,
            "ImagesFormat": True
        }
    },
    "7Z": {
        "MimeTypes": ["application/x-7z-compressed"],
        "Software": ["7-Zip", "WinRAR", "PeaZip", "Bandizip"],
        "UseCases": ["High compression", "Backup archives", "Large file compression", "Multi-volume archives"],
        "QualityMetrics": {
            "CompletenessScore": 90,
            "DocumentationLevel": "detailed",
            "BlocksDefined": 8,
            "ValidationRules": 2,
            "LastUpdated": "2025-01-12",
            "PriorityFormat": True,
            "AutoRefined": False
        },
        "TechnicalDetails": {
            "CompressionMethod": "LZMA, LZMA2, BZIP2, DEFLATE",
            "ArchivesFormat": True,
            "SupportsEncryption": True
        }
    },
    "RAR": {
        "MimeTypes": ["application/vnd.rar", "application/x-rar-compressed"],
        "Software": ["WinRAR", "7-Zip", "The Unarchiver", "PeaZip"],
        "UseCases": ["File compression", "Multi-volume archives", "Recovery records", "Backup"],
        "QualityMetrics": {
            "CompletenessScore": 88,
            "DocumentationLevel": "detailed",
            "BlocksDefined": 7,
            "ValidationRules": 2,
            "LastUpdated": "2025-01-10",
            "PriorityFormat": True,
            "AutoRefined": False
        },
        "TechnicalDetails": {
            "CompressionMethod": "RAR proprietary",
            "ArchivesFormat": True,
            "SupportsEncryption": True
        }
    },
    "TAR": {
        "MimeTypes": ["application/x-tar"],
        "Software": ["tar (Unix)", "7-Zip", "WinRAR", "The Unarchiver"],
        "UseCases": ["Unix/Linux archives", "Software distribution", "Backups", "Container images"],
        "QualityMetrics": {
            "CompletenessScore": 82,
            "DocumentationLevel": "standard",
            "BlocksDefined": 5,
            "ValidationRules": 1,
            "LastUpdated": "2025-01-08",
            "PriorityFormat": True,
            "AutoRefined": False
        },
        "TechnicalDetails": {
            "CompressionMethod": "None (often combined with GZIP)",
            "ArchivesFormat": True,
            "Platform": "Unix/Linux"
        }
    }
}

def enrich_json_file(file_path, format_name):
    """Enrich a single JSON file with metadata"""
    try:
        # Read existing JSON
        with open(file_path, 'r', encoding='utf-8-sig') as f:
            data = json.load(f)

        # Get enrichment data for this format
        enrichment = ENRICHMENT_DATA.get(format_name)
        if not enrichment:
            return False

        # Add enriched properties (PascalCase for C# compatibility)
        data['MimeTypes'] = enrichment['MimeTypes']
        data['Software'] = enrichment['Software']
        data['UseCases'] = enrichment['UseCases']
        data['QualityMetrics'] = enrichment['QualityMetrics']
        data['TechnicalDetails'] = enrichment['TechnicalDetails']

        # Write back with proper formatting
        with open(file_path, 'w', encoding='utf-8') as f:
            json.dump(data, f, indent=2, ensure_ascii=False)

        return True

    except Exception as e:
        print(f"ERROR processing {file_path.name}: {e}")
        return False

def main():
    # Find FormatDefinitions directory
    script_dir = Path(__file__).parent
    format_defs_dir = script_dir.parent / 'FormatDefinitions'

    if not format_defs_dir.exists():
        print(f"ERROR: {format_defs_dir} not found")
        return

    print(f"Enriching priority formats in {format_defs_dir}...")
    print("=" * 60)

    enriched_count = 0

    # Map format names to file paths
    format_files = {
        "ZIP": format_defs_dir / "Archives" / "ZIP.json",
        "7Z": format_defs_dir / "Archives" / "7Z.json",
        "RAR": format_defs_dir / "Archives" / "RAR.json",
        "TAR": format_defs_dir / "Archives" / "TAR.json",
        "PNG": format_defs_dir / "Images" / "PNG.json",
        "JPEG": format_defs_dir / "Images" / "JPEG.json",
        "GIF": format_defs_dir / "Images" / "GIF.json",
        "PDF": format_defs_dir / "Documents" / "PDF.json",
        "MP3": format_defs_dir / "Audio" / "MP3.json",
        "MP4": format_defs_dir / "Video" / "MP4.json"
    }

    # Enrich each format
    for format_name, file_path in format_files.items():
        if file_path.exists():
            if enrich_json_file(file_path, format_name):
                enriched_count += 1
                print(f"[OK] Enriched: {format_name}")
        else:
            print(f"[SKIP] Not found: {format_name}")

    print("=" * 60)
    print(f"Enriched {enriched_count}/{len(format_files)} priority formats")
    print("=" * 60)

if __name__ == '__main__':
    main()
