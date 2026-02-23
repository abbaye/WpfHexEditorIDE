#!/usr/bin/env python3
"""
Format Definition Accuracy Validator
Apache 2.0 - 2026

Validates format definitions against known technical specifications.
Checks magic bytes, essential blocks, and format coverage.
"""

import json
import os
import sys
from pathlib import Path
from datetime import datetime
from typing import List, Dict, Tuple, Optional
import re

# Known format specifications database
# This is a comprehensive database of format specifications including magic bytes,
# critical header fields, and essential structures
FORMAT_SPECS = {
    # ===== ARCHIVES =====
    "ZIP": {
        "magic": ["504B0304", "504B0506", "504B0708"],
        "magic_offset": 0,
        "essential_blocks": ["signature", "version", "flags", "compression", "crc32", "compressed_size", "uncompressed_size"],
        "min_header_size": 30,
        "references": ["PKWARE APPNOTE.TXT", "ISO/IEC 21320-1:2015"]
    },
    "RAR": {
        "magic": ["526172211A0700", "526172211A070100"],
        "magic_offset": 0,
        "essential_blocks": ["signature", "header_crc", "header_type", "flags"],
        "min_header_size": 7,
        "references": ["RAR 5.0 Archive Format"]
    },
    "7Z": {
        "magic": ["377ABCAF271C"],
        "magic_offset": 0,
        "essential_blocks": ["signature", "version", "start_header_crc", "next_header_offset"],
        "min_header_size": 32,
        "references": ["7-Zip Format Specification"]
    },
    "GZIP": {
        "magic": ["1F8B"],
        "magic_offset": 0,
        "essential_blocks": ["signature", "compression_method", "flags", "timestamp"],
        "min_header_size": 10,
        "references": ["RFC 1952"]
    },
    "BZIP2": {
        "magic": ["425A68"],
        "magic_offset": 0,
        "essential_blocks": ["signature", "version", "block_size"],
        "min_header_size": 4,
        "references": ["bzip2 Format Specification"]
    },
    "TAR": {
        "magic": ["7573746172"],  # "ustar"
        "magic_offset": 257,
        "essential_blocks": ["filename", "mode", "uid", "gid", "size", "mtime", "checksum", "typeflag"],
        "min_header_size": 512,
        "references": ["POSIX.1-1988", "GNU tar manual"]
    },

    # ===== IMAGES =====
    "PNG": {
        "magic": ["89504E470D0A1A0A"],
        "magic_offset": 0,
        "essential_blocks": ["signature", "IHDR_length", "IHDR_type", "width", "height", "bit_depth", "color_type"],
        "min_header_size": 33,
        "references": ["RFC 2325", "ISO/IEC 15948:2004"]
    },
    "JPEG": {
        "magic": ["FFD8FF"],
        "magic_offset": 0,
        "essential_blocks": ["signature", "APP0_marker", "JFIF_identifier", "version"],
        "min_header_size": 20,
        "references": ["ISO/IEC 10918-1", "JFIF Specification"]
    },
    "GIF": {
        "magic": ["474946383761", "474946383961"],  # GIF87a, GIF89a
        "magic_offset": 0,
        "essential_blocks": ["signature", "version", "width", "height", "flags"],
        "min_header_size": 13,
        "references": ["GIF89a Specification"]
    },
    "BMP": {
        "magic": ["424D"],
        "magic_offset": 0,
        "essential_blocks": ["signature", "file_size", "data_offset", "header_size", "width", "height"],
        "min_header_size": 54,
        "references": ["Microsoft BMP Format Specification"]
    },
    "TIFF": {
        "magic": ["49492A00", "4D4D002A"],  # Little-endian and big-endian
        "magic_offset": 0,
        "essential_blocks": ["byte_order", "magic_number", "ifd_offset"],
        "min_header_size": 8,
        "references": ["TIFF 6.0 Specification", "RFC 3302"]
    },
    "WEBP": {
        "magic": ["52494646", "57454250"],  # RIFF + WEBP
        "magic_offset": 0,
        "essential_blocks": ["RIFF_signature", "file_size", "WEBP_signature"],
        "min_header_size": 12,
        "references": ["WebP Container Specification"]
    },

    # ===== VIDEO =====
    "MP4": {
        "magic": ["66747970"],  # ftyp at offset 4
        "magic_offset": 4,
        "essential_blocks": ["box_size", "box_type", "major_brand", "minor_version"],
        "min_header_size": 8,
        "references": ["ISO/IEC 14496-12", "ISO/IEC 14496-14"]
    },
    "AVI": {
        "magic": ["52494646", "41564920"],  # RIFF + AVI_
        "magic_offset": 0,
        "essential_blocks": ["RIFF_signature", "file_size", "AVI_signature"],
        "min_header_size": 12,
        "references": ["Microsoft AVI Format Specification"]
    },
    "MKV": {
        "magic": ["1A45DFA3"],
        "magic_offset": 0,
        "essential_blocks": ["EBML_signature", "EBML_version", "doctype"],
        "min_header_size": 4,
        "references": ["Matroska Specification"]
    },

    # ===== AUDIO =====
    "MP3": {
        "magic": ["494433", "FFFB", "FFF3", "FFF2"],  # ID3 or MPEG sync
        "magic_offset": 0,
        "essential_blocks": ["signature", "version", "flags"],
        "min_header_size": 10,
        "references": ["ISO/IEC 11172-3", "ISO/IEC 13818-3", "ID3v2 Specification"]
    },
    "WAV": {
        "magic": ["52494646", "57415645"],  # RIFF + WAVE
        "magic_offset": 0,
        "essential_blocks": ["RIFF_signature", "file_size", "WAVE_signature", "fmt_chunk"],
        "min_header_size": 44,
        "references": ["Microsoft WAVE Format Specification", "RFC 2361"]
    },
    "FLAC": {
        "magic": ["664C6143"],
        "magic_offset": 0,
        "essential_blocks": ["signature", "metadata_block_header", "streaminfo"],
        "min_header_size": 42,
        "references": ["FLAC Format Specification"]
    },
    "OGG": {
        "magic": ["4F676753"],  # OggS
        "magic_offset": 0,
        "essential_blocks": ["capture_pattern", "version", "header_type", "granule_position"],
        "min_header_size": 27,
        "references": ["RFC 3533"]
    },

    # ===== EXECUTABLES =====
    "PE_EXE": {
        "magic": ["4D5A"],  # MZ
        "magic_offset": 0,
        "essential_blocks": ["DOS_signature", "PE_offset", "bytes_on_last_page", "pages_in_file"],
        "min_header_size": 64,
        "references": ["Microsoft PE Format Specification"]
    },
    "ELF": {
        "magic": ["7F454C46"],
        "magic_offset": 0,
        "essential_blocks": ["magic", "class", "data", "version", "OS_ABI"],
        "min_header_size": 52,
        "references": ["ELF Specification", "System V ABI"]
    },
    "MACH_O": {
        "magic": ["FEEDFACE", "FEEDFACF", "CEFAEDFE", "CFFAEDFE"],
        "magic_offset": 0,
        "essential_blocks": ["magic", "cpu_type", "cpu_subtype", "filetype", "ncmds"],
        "min_header_size": 28,
        "references": ["Mach-O File Format Reference"]
    },

    # ===== DOCUMENTS =====
    "PDF": {
        "magic": ["25504446"],  # %PDF
        "magic_offset": 0,
        "essential_blocks": ["signature", "version"],
        "min_header_size": 8,
        "references": ["ISO 32000-1:2008", "PDF Reference 1.7"]
    },
    "DOCX": {
        "magic": ["504B0304"],  # ZIP format
        "magic_offset": 0,
        "essential_blocks": ["ZIP_signature", "version", "flags", "compression"],
        "min_header_size": 30,
        "references": ["ECMA-376", "ISO/IEC 29500"]
    },
    "EPUB": {
        "magic": ["504B0304"],  # ZIP format
        "magic_offset": 0,
        "essential_blocks": ["ZIP_signature", "mimetype_file"],
        "min_header_size": 30,
        "references": ["EPUB 3.2 Specification", "ISO/IEC TS 30135"]
    },

    # ===== GAME ROMS =====
    "ROM_NES": {
        "magic": ["4E45531A"],  # NES\x1A
        "magic_offset": 0,
        "essential_blocks": ["magic", "prg_rom_size", "chr_rom_size", "flags6", "flags7"],
        "min_header_size": 16,
        "references": ["iNES Format Specification", "NES 2.0 Specification"]
    },
    "ROM_GB": {
        "magic": ["CEED6666CC0D000B03730083000C000D0008111F8889000EDCCC6EE6DDDDD999BBBB67636E0EECCCDDDC999FBBB9333E"],  # Nintendo logo
        "magic_offset": 260,  # 0x104
        "essential_blocks": ["nintendo_logo", "title", "cgb_flag", "cartridge_type", "rom_size", "ram_size"],
        "min_header_size": 80,
        "references": ["Game Boy Programming Manual", "Pan Docs"]
    },
    "ROM_GBA": {
        "magic": ["96"],  # Fixed value at 0xB2
        "magic_offset": 178,
        "essential_blocks": ["rom_entry_point", "nintendo_logo", "game_title", "game_code", "maker_code"],
        "min_header_size": 192,
        "references": ["GBA Technical Manual", "GBATEK Specification"]
    },
    "ROM_N64": {
        "magic": ["80371240", "37804012", "40123780", "12408037"],  # Different byte orders
        "magic_offset": 0,
        "essential_blocks": ["magic", "clock_rate", "boot_address", "release", "crc1", "crc2", "game_title"],
        "min_header_size": 64,
        "references": ["N64 Programming Manual"]
    },

    # ===== FONTS =====
    "TTF": {
        "magic": ["00010000", "74727565"],  # TrueType or 'true'
        "magic_offset": 0,
        "essential_blocks": ["version", "num_tables", "search_range", "entry_selector"],
        "min_header_size": 12,
        "references": ["TrueType Reference Manual", "ISO/IEC 14496-22"]
    },
    "OTF": {
        "magic": ["4F54544F"],  # OTTO
        "magic_offset": 0,
        "essential_blocks": ["signature", "num_tables", "search_range"],
        "min_header_size": 12,
        "references": ["OpenType Specification", "ISO/IEC 14496-22"]
    },
    "WOFF": {
        "magic": ["774F4646"],  # wOFF
        "magic_offset": 0,
        "essential_blocks": ["signature", "flavor", "length", "num_tables"],
        "min_header_size": 44,
        "references": ["WOFF File Format 1.0", "W3C Recommendation"]
    },
    "WOFF2": {
        "magic": ["774F4632"],  # wOF2
        "magic_offset": 0,
        "essential_blocks": ["signature", "flavor", "length", "num_tables"],
        "min_header_size": 48,
        "references": ["WOFF File Format 2.0", "W3C Recommendation"]
    },
}


def normalize_hex(hex_str: str) -> str:
    """Normalize hex string (remove spaces, convert to uppercase)"""
    return hex_str.replace(" ", "").replace("0x", "").upper()


def validate_format_accuracy(file_path: Path, json_data: dict) -> Tuple[bool, List[str], List[str], Dict]:
    """
    Validate format definition accuracy against known specifications
    Returns: (is_accurate, errors, warnings, details)
    """
    errors = []
    warnings = []
    details = {
        "magic_valid": False,
        "magic_offset_valid": False,
        "essential_blocks_coverage": 0.0,
        "min_header_size_met": False,
        "spec_available": False
    }

    format_name = json_data.get("formatName", "")

    # Try to match format to known specs
    spec_key = None
    for key in FORMAT_SPECS.keys():
        if key.upper() in format_name.upper() or format_name.upper().replace(" ", "_") == key:
            spec_key = key
            break

    # Also try matching by filename
    if not spec_key:
        filename = file_path.stem.upper()
        for key in FORMAT_SPECS.keys():
            if key in filename or filename.replace("_", "") == key.replace("_", ""):
                spec_key = key
                break

    if not spec_key:
        # No spec available for this format
        warnings.append(f"No technical specification available for validation")
        return True, errors, warnings, details

    spec = FORMAT_SPECS[spec_key]
    details["spec_available"] = True
    details["spec_key"] = spec_key
    details["references"] = spec.get("references", [])

    # Validate magic bytes
    detection = json_data.get("detection", {})
    json_signature = normalize_hex(detection.get("signature", ""))
    json_offset = detection.get("offset", 0)

    spec_magics = [normalize_hex(m) for m in spec["magic"]]
    if json_signature in spec_magics:
        details["magic_valid"] = True
    else:
        errors.append(f"Magic bytes mismatch. Expected one of {spec['magic']}, got '{json_signature}'")
        details["expected_magic"] = spec["magic"]
        details["actual_magic"] = json_signature

    # Validate magic offset
    if json_offset == spec["magic_offset"]:
        details["magic_offset_valid"] = True
    else:
        errors.append(f"Magic offset mismatch. Expected {spec['magic_offset']}, got {json_offset}")
        details["expected_offset"] = spec["magic_offset"]
        details["actual_offset"] = json_offset

    # Check essential blocks coverage
    blocks = json_data.get("blocks", [])
    block_names = [b.get("name", "").lower() for b in blocks]
    essential = spec.get("essential_blocks", [])

    covered = 0
    missing = []
    for essential_block in essential:
        # Check if any block name contains the essential block name
        found = any(essential_block.lower() in name for name in block_names)
        if found:
            covered += 1
        else:
            missing.append(essential_block)

    coverage = (covered / len(essential) * 100) if essential else 100
    details["essential_blocks_coverage"] = coverage
    details["essential_blocks_covered"] = covered
    details["essential_blocks_total"] = len(essential)
    details["missing_blocks"] = missing

    if coverage < 50:
        errors.append(f"Low essential block coverage: {coverage:.1f}% ({covered}/{len(essential)} blocks)")
    elif coverage < 80:
        warnings.append(f"Moderate essential block coverage: {coverage:.1f}% ({covered}/{len(essential)} blocks)")

    if missing:
        warnings.append(f"Missing recommended blocks: {', '.join(missing[:5])}")

    # Check minimum header size coverage
    total_bytes_defined = 0
    for block in blocks:
        offset = block.get("offset", 0)
        length = block.get("length", 0)
        if isinstance(offset, int) and isinstance(length, int):
            total_bytes_defined = max(total_bytes_defined, offset + length)

    min_size = spec.get("min_header_size", 0)
    if total_bytes_defined >= min_size:
        details["min_header_size_met"] = True
    else:
        warnings.append(f"Header coverage incomplete: {total_bytes_defined} bytes defined, minimum {min_size} bytes recommended")

    details["bytes_defined"] = total_bytes_defined
    details["min_bytes_required"] = min_size

    is_accurate = len(errors) == 0
    return is_accurate, errors, warnings, details


def main():
    script_dir = Path(__file__).parent
    format_defs_dir = script_dir.parent / "FormatDefinitions"

    if not format_defs_dir.exists():
        print(f"ERROR: FormatDefinitions directory not found at: {format_defs_dir}")
        sys.exit(1)

    print("=== WPF HexaEditor - Format Accuracy Validator ===")
    print()
    print(f"Format Definitions Path: {format_defs_dir}")
    print(f"Known Specifications: {len(FORMAT_SPECS)} formats")
    print()

    json_files = list(format_defs_dir.rglob("*.json"))
    print(f"Found {len(json_files)} JSON files")
    print()

    # Validate each file
    total_files = 0
    accurate_files = 0
    inaccurate_files = 0
    no_spec_files = 0
    total_errors = 0
    total_warnings = 0
    detailed_results = []

    for json_file in json_files:
        total_files += 1
        relative_path = json_file.relative_to(format_defs_dir)

        try:
            with open(json_file, 'r', encoding='utf-8-sig') as f:
                data = json.load(f)

            is_accurate, errors, warnings, details = validate_format_accuracy(json_file, data)

            if not details.get("spec_available"):
                no_spec_files += 1
            elif is_accurate:
                accurate_files += 1
            else:
                inaccurate_files += 1
                print(f"  [!] INACCURATE - {relative_path}")
                for error in errors:
                    print(f"      ERROR: {error}")

            total_errors += len(errors)
            total_warnings += len(warnings)

            detailed_results.append({
                "file": str(relative_path),
                "format_name": data.get("formatName", "Unknown"),
                "is_accurate": is_accurate,
                "errors": errors,
                "warnings": warnings,
                "details": details
            })

        except Exception as e:
            print(f"  [X] ERROR parsing {relative_path}: {e}")
            inaccurate_files += 1

    # Calculate statistics
    validated_files = accurate_files + inaccurate_files
    accuracy_rate = (accurate_files / validated_files * 100) if validated_files > 0 else 0

    # Display summary
    print()
    print("=== Accuracy Validation Summary ===")
    print(f"Total Files:           {total_files}")
    print(f"Validated Files:       {validated_files} (with known specs)")
    print(f"Accurate Files:        {accurate_files}")
    print(f"Inaccurate Files:      {inaccurate_files}")
    print(f"No Spec Available:     {no_spec_files}")
    print(f"Total Errors:          {total_errors}")
    print(f"Total Warnings:        {total_warnings}")
    print(f"Accuracy Rate:         {accuracy_rate:.1f}%")
    print()

    # Generate detailed report
    report_path = script_dir / "AccuracyReport.md"
    with open(report_path, 'w', encoding='utf-8') as f:
        f.write("# Format Definition Accuracy Report\n\n")
        f.write(f"**Generated:** {datetime.now().strftime('%Y-%m-%d %H:%M:%S')}\n")
        f.write(f"**Path:** {format_defs_dir}\n")
        f.write(f"**Known Specifications:** {len(FORMAT_SPECS)} formats\n\n")

        f.write("## Summary\n\n")
        f.write(f"- **Total Files:** {total_files}\n")
        f.write(f"- **Validated Files:** {validated_files} (with known specs)\n")
        f.write(f"- **Accurate Files:** {accurate_files}\n")
        f.write(f"- **Inaccurate Files:** {inaccurate_files}\n")
        f.write(f"- **No Spec Available:** {no_spec_files}\n")
        f.write(f"- **Total Errors:** {total_errors}\n")
        f.write(f"- **Total Warnings:** {total_warnings}\n")
        f.write(f"- **Accuracy Rate:** {accuracy_rate:.1f}%\n\n")

        # Inaccurate files section
        if inaccurate_files > 0:
            f.write("## Inaccurate Formats\n\n")
            f.write("The following formats have accuracy issues:\n\n")
            for result in detailed_results:
                if result["details"].get("spec_available") and not result["is_accurate"]:
                    f.write(f"### [ERROR] {result['format_name']} - {result['file']}\n\n")

                    if result["errors"]:
                        f.write("**Errors:**\n")
                        for error in result["errors"]:
                            f.write(f"- {error}\n")
                        f.write("\n")

                    if result["warnings"]:
                        f.write("**Warnings:**\n")
                        for warning in result["warnings"]:
                            f.write(f"- {warning}\n")
                        f.write("\n")

                    details = result["details"]
                    if "references" in details:
                        f.write("**Technical References:**\n")
                        for ref in details["references"]:
                            f.write(f"- {ref}\n")
                        f.write("\n")

        # Validated formats with warnings
        f.write("## Validated Formats with Warnings\n\n")
        for result in detailed_results:
            if result["details"].get("spec_available") and result["is_accurate"] and result["warnings"]:
                f.write(f"### [WARNING] {result['format_name']} - {result['file']}\n\n")

                details = result["details"]
                f.write(f"- **Magic Bytes:** {'✓ Valid' if details.get('magic_valid') else '✗ Invalid'}\n")
                f.write(f"- **Magic Offset:** {'✓ Valid' if details.get('magic_offset_valid') else '✗ Invalid'}\n")
                f.write(f"- **Essential Blocks Coverage:** {details.get('essential_blocks_coverage', 0):.1f}%\n")
                f.write(f"- **Header Size Coverage:** {'✓ Met' if details.get('min_header_size_met') else '✗ Incomplete'}\n\n")

                if result["warnings"]:
                    f.write("**Warnings:**\n")
                    for warning in result["warnings"]:
                        f.write(f"- {warning}\n")
                    f.write("\n")

        # Statistics by category
        f.write("## Statistics by Category\n\n")
        category_stats = {}
        for result in detailed_results:
            file_path = Path(result["file"])
            category = file_path.parts[0] if len(file_path.parts) > 0 else "Unknown"

            if category not in category_stats:
                category_stats[category] = {
                    "total": 0,
                    "validated": 0,
                    "accurate": 0,
                    "no_spec": 0
                }

            category_stats[category]["total"] += 1
            if result["details"].get("spec_available"):
                category_stats[category]["validated"] += 1
                if result["is_accurate"]:
                    category_stats[category]["accurate"] += 1
            else:
                category_stats[category]["no_spec"] += 1

        for category, stats in sorted(category_stats.items()):
            validated = stats["validated"]
            accuracy = (stats["accurate"] / validated * 100) if validated > 0 else 0
            f.write(f"### {category}\n\n")
            f.write(f"- Total: {stats['total']}\n")
            f.write(f"- Validated: {stats['validated']}\n")
            f.write(f"- Accurate: {stats['accurate']}\n")
            f.write(f"- No Spec: {stats['no_spec']}\n")
            f.write(f"- Accuracy Rate: {accuracy:.1f}%\n\n")

        # Known specifications reference
        f.write("## Known Format Specifications\n\n")
        f.write(f"This validator currently has technical specifications for {len(FORMAT_SPECS)} formats:\n\n")
        for spec_key, spec in sorted(FORMAT_SPECS.items()):
            f.write(f"### {spec_key}\n\n")
            f.write(f"- **Magic Bytes:** {', '.join(spec['magic'])}\n")
            f.write(f"- **Magic Offset:** {spec['magic_offset']}\n")
            f.write(f"- **Essential Blocks:** {len(spec.get('essential_blocks', []))}\n")
            f.write(f"- **Min Header Size:** {spec.get('min_header_size', 0)} bytes\n")
            if "references" in spec:
                f.write(f"- **References:** {', '.join(spec['references'])}\n")
            f.write("\n")

        f.write("## Next Steps\n\n")
        f.write("To improve format accuracy:\n\n")
        f.write("1. **Fix Inaccurate Formats:** Review and correct formats with errors\n")
        f.write("2. **Add Missing Specs:** Expand FORMAT_SPECS database with more formats\n")
        f.write("3. **Complete Block Coverage:** Add missing essential blocks to definitions\n")
        f.write("4. **Verify Magic Bytes:** Ensure all signatures match official specifications\n")
        f.write("5. **Test with Real Files:** Validate against actual file samples\n\n")

    print(f"Detailed report saved to: {report_path}")

    # Exit code
    sys.exit(0 if inaccurate_files == 0 else 1)


if __name__ == "__main__":
    main()
