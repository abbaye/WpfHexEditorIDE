#!/usr/bin/env python3
"""
Add Technical References to Format Definitions
Apache 2.0 - 2026

Adds technical specification references and web links to format definition JSON files.
"""

import json
import os
import sys
from pathlib import Path
from typing import Dict, List, Optional

# Comprehensive reference database with web links
FORMAT_REFERENCES = {
    # ===== ARCHIVES =====
    "ZIP": {
        "specifications": [
            "PKWARE APPNOTE.TXT - ZIP File Format Specification",
            "ISO/IEC 21320-1:2015 - Document Container File"
        ],
        "web_links": [
            "https://pkware.cachefly.net/webdocs/casestudies/APPNOTE.TXT",
            "https://en.wikipedia.org/wiki/ZIP_(file_format)",
            "https://www.iso.org/standard/60101.html"
        ]
    },
    "RAR": {
        "specifications": [
            "RAR 5.0 Archive Format",
            "RAR File Format Technical Specification"
        ],
        "web_links": [
            "https://www.rarlab.com/technote.htm",
            "https://en.wikipedia.org/wiki/RAR_(file_format)"
        ]
    },
    "7Z": {
        "specifications": [
            "7-Zip Format Specification",
            "LZMA SDK Documentation"
        ],
        "web_links": [
            "https://www.7-zip.org/7z.html",
            "https://www.7-zip.org/sdk.html",
            "https://en.wikipedia.org/wiki/7z"
        ]
    },
    "GZIP": {
        "specifications": [
            "RFC 1952 - GZIP File Format Specification",
            "DEFLATE Compression Algorithm"
        ],
        "web_links": [
            "https://www.ietf.org/rfc/rfc1952.txt",
            "https://en.wikipedia.org/wiki/Gzip"
        ]
    },
    "BZIP2": {
        "specifications": [
            "bzip2 Format Specification",
            "Burrows-Wheeler Transform Algorithm"
        ],
        "web_links": [
            "https://sourceware.org/bzip2/",
            "https://en.wikipedia.org/wiki/Bzip2"
        ]
    },
    "TAR": {
        "specifications": [
            "POSIX.1-1988 (IEEE Std 1003.1)",
            "GNU tar Format Documentation",
            "UStar Format Specification"
        ],
        "web_links": [
            "https://www.gnu.org/software/tar/manual/",
            "https://en.wikipedia.org/wiki/Tar_(computing)",
            "https://pubs.opengroup.org/onlinepubs/9699919799/utilities/pax.html"
        ]
    },

    # ===== IMAGES =====
    "PNG": {
        "specifications": [
            "RFC 2325 - Portable Network Graphics (PNG) Specification",
            "ISO/IEC 15948:2004 - PNG Standard",
            "W3C PNG Specification"
        ],
        "web_links": [
            "https://www.w3.org/TR/PNG/",
            "https://www.ietf.org/rfc/rfc2083.txt",
            "https://en.wikipedia.org/wiki/Portable_Network_Graphics"
        ]
    },
    "JPEG": {
        "specifications": [
            "ISO/IEC 10918-1 - Digital Compression and Coding",
            "JFIF File Format Specification",
            "ITU-T T.81"
        ],
        "web_links": [
            "https://www.w3.org/Graphics/JPEG/",
            "https://jpeg.org/jpeg/",
            "https://en.wikipedia.org/wiki/JPEG"
        ]
    },
    "GIF": {
        "specifications": [
            "GIF89a Specification",
            "Graphics Interchange Format Version 89a"
        ],
        "web_links": [
            "https://www.w3.org/Graphics/GIF/spec-gif89a.txt",
            "https://en.wikipedia.org/wiki/GIF"
        ]
    },
    "BMP": {
        "specifications": [
            "Microsoft BMP File Format",
            "Windows Bitmap Format Specification"
        ],
        "web_links": [
            "https://docs.microsoft.com/en-us/windows/win32/gdi/bitmap-storage",
            "https://en.wikipedia.org/wiki/BMP_file_format"
        ]
    },
    "TIFF": {
        "specifications": [
            "TIFF Revision 6.0 Specification",
            "RFC 3302 - Tag Image File Format"
        ],
        "web_links": [
            "https://www.adobe.io/open/standards/TIFF.html",
            "https://www.ietf.org/rfc/rfc3302.txt",
            "https://en.wikipedia.org/wiki/TIFF"
        ]
    },
    "WEBP": {
        "specifications": [
            "WebP Container Specification",
            "WebP Lossy Bitstream Specification",
            "WebP Lossless Bitstream Specification"
        ],
        "web_links": [
            "https://developers.google.com/speed/webp/docs/riff_container",
            "https://developers.google.com/speed/webp/docs/webp_lossless_bitstream_specification",
            "https://en.wikipedia.org/wiki/WebP"
        ]
    },

    # ===== VIDEO =====
    "MP4": {
        "specifications": [
            "ISO/IEC 14496-12 - ISO Base Media File Format",
            "ISO/IEC 14496-14 - MP4 File Format"
        ],
        "web_links": [
            "https://mpeg.chiariglione.org/standards/mpeg-4",
            "https://en.wikipedia.org/wiki/MPEG-4_Part_14"
        ]
    },
    "AVI": {
        "specifications": [
            "Microsoft AVI RIFF File Format",
            "Audio Video Interleave Format"
        ],
        "web_links": [
            "https://docs.microsoft.com/en-us/windows/win32/directshow/avi-riff-file-reference",
            "https://en.wikipedia.org/wiki/Audio_Video_Interleave"
        ]
    },
    "MKV": {
        "specifications": [
            "Matroska Media Container Specification",
            "EBML (Extensible Binary Meta Language)"
        ],
        "web_links": [
            "https://www.matroska.org/technical/specs/index.html",
            "https://github.com/ietf-wg-cellar/matroska-specification",
            "https://en.wikipedia.org/wiki/Matroska"
        ]
    },

    # ===== AUDIO =====
    "MP3": {
        "specifications": [
            "ISO/IEC 11172-3 - MPEG-1 Audio Layer III",
            "ISO/IEC 13818-3 - MPEG-2 Audio",
            "ID3v2 Tag Specification"
        ],
        "web_links": [
            "https://www.mp3-tech.org/",
            "https://id3.org/",
            "https://en.wikipedia.org/wiki/MP3"
        ]
    },
    "WAV": {
        "specifications": [
            "Microsoft WAVE Audio File Format",
            "RFC 2361 - WAVE and AVI Codec Registries"
        ],
        "web_links": [
            "https://docs.microsoft.com/en-us/windows/win32/multimedia/waveform-audio-file-format",
            "https://www.ietf.org/rfc/rfc2361.txt",
            "https://en.wikipedia.org/wiki/WAV"
        ]
    },
    "FLAC": {
        "specifications": [
            "FLAC Format Specification",
            "Free Lossless Audio Codec Documentation"
        ],
        "web_links": [
            "https://xiph.org/flac/format.html",
            "https://xiph.org/flac/documentation.html",
            "https://en.wikipedia.org/wiki/FLAC"
        ]
    },
    "OGG": {
        "specifications": [
            "RFC 3533 - The Ogg Encapsulation Format",
            "Ogg Bitstream Format"
        ],
        "web_links": [
            "https://www.ietf.org/rfc/rfc3533.txt",
            "https://xiph.org/ogg/doc/",
            "https://en.wikipedia.org/wiki/Ogg"
        ]
    },

    # ===== EXECUTABLES =====
    "PE_EXE": {
        "specifications": [
            "Microsoft PE and COFF Specification",
            "Portable Executable Format"
        ],
        "web_links": [
            "https://docs.microsoft.com/en-us/windows/win32/debug/pe-format",
            "https://en.wikipedia.org/wiki/Portable_Executable"
        ]
    },
    "ELF": {
        "specifications": [
            "ELF-64 Object File Format",
            "System V Application Binary Interface",
            "Tool Interface Standard (TIS)"
        ],
        "web_links": [
            "https://refspecs.linuxfoundation.org/elf/elf.pdf",
            "https://en.wikipedia.org/wiki/Executable_and_Linkable_Format"
        ]
    },
    "MACH_O": {
        "specifications": [
            "Mach-O Programming Topics",
            "OS X ABI Mach-O File Format Reference"
        ],
        "web_links": [
            "https://developer.apple.com/library/archive/documentation/DeveloperTools/Conceptual/MachOTopics/",
            "https://en.wikipedia.org/wiki/Mach-O"
        ]
    },

    # ===== DOCUMENTS =====
    "PDF": {
        "specifications": [
            "ISO 32000-1:2008 - PDF 1.7 Standard",
            "PDF Reference, Sixth Edition (Version 1.7)"
        ],
        "web_links": [
            "https://www.adobe.com/devnet/pdf/pdf_reference.html",
            "https://www.iso.org/standard/51502.html",
            "https://en.wikipedia.org/wiki/PDF"
        ]
    },
    "DOCX": {
        "specifications": [
            "ECMA-376 - Office Open XML File Formats",
            "ISO/IEC 29500 - Information Technology"
        ],
        "web_links": [
            "https://www.ecma-international.org/publications-and-standards/standards/ecma-376/",
            "https://en.wikipedia.org/wiki/Office_Open_XML"
        ]
    },
    "EPUB": {
        "specifications": [
            "EPUB 3.2 Specification",
            "ISO/IEC TS 30135 - EPUB File Format"
        ],
        "web_links": [
            "https://www.w3.org/publishing/epub3/epub-spec.html",
            "https://en.wikipedia.org/wiki/EPUB"
        ]
    },

    # ===== GAME ROMS =====
    "ROM_NES": {
        "specifications": [
            "iNES ROM Format Specification",
            "NES 2.0 Format Specification"
        ],
        "web_links": [
            "https://wiki.nesdev.com/w/index.php/INES",
            "https://wiki.nesdev.com/w/index.php/NES_2.0",
            "https://en.wikipedia.org/wiki/INES"
        ]
    },
    "ROM_GB": {
        "specifications": [
            "Game Boy Programming Manual",
            "Pan Docs - Game Boy Technical Reference"
        ],
        "web_links": [
            "https://gbdev.io/pandocs/",
            "https://gekkio.fi/files/gb-docs/gbctr.pdf",
            "https://en.wikipedia.org/wiki/Game_Boy"
        ]
    },
    "ROM_GBA": {
        "specifications": [
            "Game Boy Advance Programming Manual",
            "GBATEK - GBA/NDS Technical Info"
        ],
        "web_links": [
            "https://www.akkit.org/info/gbatek.htm",
            "https://problemkaputt.de/gbatek.htm",
            "https://en.wikipedia.org/wiki/Game_Boy_Advance"
        ]
    },
    "ROM_N64": {
        "specifications": [
            "Nintendo 64 Programming Manual",
            "N64 ROM Format Documentation"
        ],
        "web_links": [
            "https://n64brew.dev/wiki/ROM",
            "https://en.wikipedia.org/wiki/Nintendo_64"
        ]
    },

    # ===== FONTS =====
    "TTF": {
        "specifications": [
            "TrueType Reference Manual",
            "ISO/IEC 14496-22 - Open Font Format",
            "Apple TrueType Specification"
        ],
        "web_links": [
            "https://developer.apple.com/fonts/TrueType-Reference-Manual/",
            "https://en.wikipedia.org/wiki/TrueType"
        ]
    },
    "OTF": {
        "specifications": [
            "OpenType Specification",
            "ISO/IEC 14496-22:2019 - Open Font Format"
        ],
        "web_links": [
            "https://docs.microsoft.com/en-us/typography/opentype/spec/",
            "https://en.wikipedia.org/wiki/OpenType"
        ]
    },
    "WOFF": {
        "specifications": [
            "WOFF File Format 1.0",
            "W3C Recommendation"
        ],
        "web_links": [
            "https://www.w3.org/TR/WOFF/",
            "https://en.wikipedia.org/wiki/Web_Open_Font_Format"
        ]
    },
    "WOFF2": {
        "specifications": [
            "WOFF File Format 2.0",
            "W3C Recommendation"
        ],
        "web_links": [
            "https://www.w3.org/TR/WOFF2/",
            "https://en.wikipedia.org/wiki/Web_Open_Font_Format"
        ]
    },
}


def find_format_key(file_path: Path, json_data: dict) -> Optional[str]:
    """Find matching format key from references database"""
    format_name = json_data.get("formatName", "").upper()
    filename = file_path.stem.upper()

    # Direct match by key
    for key in FORMAT_REFERENCES.keys():
        if key in filename or key.replace("_", "") in filename.replace("_", ""):
            return key
        if key in format_name or key.replace("_", "") in format_name.replace(" ", ""):
            return key

    return None


def add_references_to_json(file_path: Path, dry_run: bool = False) -> bool:
    """Add references section to JSON file if applicable"""
    try:
        with open(file_path, 'r', encoding='utf-8-sig') as f:
            data = json.load(f)

        # Check if references already exist
        if "references" in data or "specifications" in data:
            return False  # Already has references

        # Find matching format
        format_key = find_format_key(file_path, data)
        if not format_key:
            return False  # No reference available

        refs = FORMAT_REFERENCES[format_key]

        # Add references section (after author, before detection)
        new_data = {}
        for key, value in data.items():
            new_data[key] = value
            if key == "author":
                # Add references after author
                new_data["references"] = {
                    "specifications": refs["specifications"],
                    "web_links": refs["web_links"]
                }

        if not dry_run:
            # Write back with proper formatting
            with open(file_path, 'w', encoding='utf-8-sig') as f:
                json.dump(new_data, f, indent=4, ensure_ascii=False)

        return True

    except Exception as e:
        print(f"ERROR processing {file_path}: {e}")
        return False


def main():
    script_dir = Path(__file__).parent
    format_defs_dir = script_dir.parent / "FormatDefinitions"

    if not format_defs_dir.exists():
        print(f"ERROR: FormatDefinitions directory not found at: {format_defs_dir}")
        sys.exit(1)

    print("=== Add Technical References to Format Definitions ===")
    print()
    print(f"Format Definitions Path: {format_defs_dir}")
    print(f"Known References: {len(FORMAT_REFERENCES)} formats")
    print()

    # Check for dry run flag
    dry_run = "--dry-run" in sys.argv or "-n" in sys.argv
    if dry_run:
        print("DRY RUN MODE - No files will be modified")
        print()

    json_files = list(format_defs_dir.rglob("*.json"))
    print(f"Found {len(json_files)} JSON files")
    print()

    updated = 0
    skipped = 0
    no_ref = 0

    for json_file in json_files:
        relative_path = json_file.relative_to(format_defs_dir)

        if add_references_to_json(json_file, dry_run=dry_run):
            updated += 1
            print(f"  [+] UPDATED - {relative_path}")
        else:
            # Check why skipped
            try:
                with open(json_file, 'r', encoding='utf-8-sig') as f:
                    data = json.load(f)
                if "references" in data or "specifications" in data:
                    skipped += 1
                else:
                    no_ref += 1
            except:
                no_ref += 1

    print()
    print("=== Summary ===")
    print(f"Total Files:        {len(json_files)}")
    print(f"Updated:            {updated}")
    print(f"Already Has Refs:   {skipped}")
    print(f"No Reference Avail: {no_ref}")
    print()

    if dry_run:
        print("Run without --dry-run flag to apply changes")
    else:
        print("References added successfully!")


if __name__ == "__main__":
    main()
