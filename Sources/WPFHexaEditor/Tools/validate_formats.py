#!/usr/bin/env python3
"""
Format Definition Validator Script
Apache 2.0 - 2026
Validates all format definition JSON files
"""

import json
import os
import sys
from pathlib import Path
from datetime import datetime
from typing import List, Dict, Tuple

# Required top-level properties
REQUIRED_PROPERTIES = [
    "formatName",
    "version",
    "extensions",
    "description",
    "category",
    "author",
    "detection",
    "variables",
    "blocks"
]

VALID_BLOCK_TYPES = ["signature", "field", "conditional", "loop", "action"]


def validate_file(file_path: Path) -> Tuple[bool, List[str], List[str]]:
    """
    Validate a single JSON format definition file
    Returns: (is_valid, errors, warnings)
    """
    errors = []
    warnings = []

    try:
        with open(file_path, 'r', encoding='utf-8-sig') as f:
            data = json.load(f)

        # Check required properties
        for prop in REQUIRED_PROPERTIES:
            if prop not in data:
                errors.append(f"Missing required property: {prop}")
                continue

            value = data[prop]

            # Validate non-empty strings
            if prop in ["formatName", "version", "description", "category", "author"]:
                if not isinstance(value, str) or not value.strip():
                    errors.append(f"Property '{prop}' must be a non-empty string")

            # Validate extensions array
            elif prop == "extensions":
                if not isinstance(value, list) or len(value) == 0:
                    errors.append(f"Property 'extensions' must be a non-empty array")

            # Validate detection object
            elif prop == "detection":
                if not isinstance(value, dict):
                    errors.append(f"Property 'detection' must be an object")
                else:
                    if "signature" not in value:
                        errors.append("Detection section missing required property: signature")
                    elif not isinstance(value["signature"], str):
                        errors.append("Detection.signature must be a string")
                    else:
                        sig = value["signature"]
                        if len(sig) % 2 != 0:
                            errors.append("Detection.signature must have even number of hex digits")
                        if not all(c in "0123456789ABCDEFabcdef" for c in sig):
                            errors.append("Detection.signature contains invalid hex characters")

                    if "offset" not in value:
                        errors.append("Detection section missing required property: offset")
                    elif not isinstance(value["offset"], int):
                        errors.append("Detection.offset must be a number")
                    elif value["offset"] < 0:
                        errors.append("Detection.offset cannot be negative")

                    if "required" in value and not isinstance(value["required"], bool):
                        errors.append("Detection.required must be a boolean")

            # Validate variables object
            elif prop == "variables":
                if not isinstance(value, dict):
                    errors.append(f"Property 'variables' must be an object")

            # Validate blocks array
            elif prop == "blocks":
                if not isinstance(value, list) or len(value) == 0:
                    errors.append(f"Property 'blocks' must be a non-empty array")
                else:
                    # Validate first block
                    for idx, block in enumerate(value):
                        if not isinstance(block, dict):
                            errors.append(f"Block[{idx}] must be an object")
                            continue

                        if "type" not in block:
                            errors.append(f"Block[{idx}] missing required property: type")
                        elif block["type"] not in VALID_BLOCK_TYPES:
                            warnings.append(f"Block[{idx}] has unknown type: {block['type']}")

                        # For signature and field blocks, check common properties
                        if block.get("type") in ["signature", "field"]:
                            for req_prop in ["name", "offset", "length", "color", "description"]:
                                if req_prop not in block:
                                    warnings.append(f"Block[{idx}] missing recommended property: {req_prop}")

    except json.JSONDecodeError as e:
        errors.append(f"JSON parsing error: {e}")
    except Exception as e:
        errors.append(f"Unexpected error: {e}")

    is_valid = len(errors) == 0
    return is_valid, errors, warnings


def main():
    # Find FormatDefinitions directory
    script_dir = Path(__file__).parent
    format_defs_dir = script_dir.parent / "FormatDefinitions"

    if not format_defs_dir.exists():
        print(f"ERROR: FormatDefinitions directory not found at: {format_defs_dir}")
        sys.exit(1)

    print("=== WPF HexaEditor - Format Definition Validator ===")
    print()
    print(f"Format Definitions Path: {format_defs_dir}")
    print()

    # Find all JSON files
    json_files = list(format_defs_dir.rglob("*.json"))
    print(f"Found {len(json_files)} JSON files")
    print()

    # Validate each file
    total_files = 0
    valid_files = 0
    invalid_files = 0
    total_errors = 0
    total_warnings = 0
    invalid_files_list = []
    detailed_results = []

    for json_file in json_files:
        total_files += 1
        relative_path = json_file.relative_to(format_defs_dir)

        is_valid, errors, warnings = validate_file(json_file)

        if is_valid:
            valid_files += 1
        else:
            invalid_files += 1
            invalid_files_list.append(str(relative_path))
            print(f"  [X] INVALID - {relative_path}")
            for error in errors:
                print(f"      ERROR: {error}")

        total_errors += len(errors)
        total_warnings += len(warnings)

        detailed_results.append({
            "file": str(relative_path),
            "is_valid": is_valid,
            "errors": errors,
            "warnings": warnings
        })

    # Calculate success rate
    success_rate = (valid_files / total_files * 100) if total_files > 0 else 0

    # Display summary
    print()
    print("=== Validation Summary ===")
    print(f"Total Files:    {total_files}")
    print(f"Valid Files:    {valid_files}")
    print(f"Invalid Files:  {invalid_files}")
    print(f"Total Errors:   {total_errors}")
    print(f"Total Warnings: {total_warnings}")
    print(f"Success Rate:   {success_rate:.1f}%")
    print()

    # Generate Markdown report
    report_path = script_dir / "ValidationReport.md"
    with open(report_path, 'w', encoding='utf-8') as f:
        f.write("# Format Definition Validation Report\n\n")
        f.write(f"**Generated:** {datetime.now().strftime('%Y-%m-%d %H:%M:%S')}\n")
        f.write(f"**Path:** {format_defs_dir}\n\n")
        f.write("## Summary\n\n")
        f.write(f"- **Total Files:** {total_files}\n")
        f.write(f"- **Valid Files:** {valid_files}\n")
        f.write(f"- **Invalid Files:** {invalid_files}\n")
        f.write(f"- **Total Errors:** {total_errors}\n")
        f.write(f"- **Total Warnings:** {total_warnings}\n")
        f.write(f"- **Success Rate:** {success_rate:.1f}%\n\n")

        if invalid_files > 0:
            f.write("## Invalid Files\n\n")
            f.write("The following files have validation errors:\n\n")
            for file in invalid_files_list:
                f.write(f"- `{file}`\n")
            f.write("\n")

        f.write("## Detailed Results\n\n")
        for result in detailed_results:
            if not result["is_valid"] or len(result["warnings"]) > 0:
                status = "[WARNING] VALID (with warnings)" if result["is_valid"] else "[ERROR] INVALID"
                f.write(f"### {status} - {result['file']}\n\n")

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

        f.write("## Required Sections Reference\n\n")
        f.write("All format definition JSON files must contain these 9 top-level sections:\n\n")
        f.write("1. **formatName** (string) - Human-readable format name\n")
        f.write("2. **version** (string) - Definition version (e.g., \"1.0\")\n")
        f.write("3. **extensions** (array) - File extensions (e.g., [\".zip\"])\n")
        f.write("4. **description** (string) - Detailed format description\n")
        f.write("5. **category** (string) - Format category (e.g., \"Game\", \"Archives\")\n")
        f.write("6. **author** (string) - Definition author (typically \"WPFHexaEditor Team\")\n")
        f.write("7. **detection** (object) - Detection rules with signature, offset, required\n")
        f.write("8. **variables** (object) - State variables (can be empty {})\n")
        f.write("9. **blocks** (array) - Block definitions (at least 1 block required)\n")

    print(f"Report saved to: {report_path}")

    # Exit with appropriate code
    sys.exit(0 if invalid_files == 0 else 1)


if __name__ == "__main__":
    main()
