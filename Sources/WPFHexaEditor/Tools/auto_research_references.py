#!/usr/bin/env python3
"""
Automatic Format Reference Research Tool
Apache 2.0 - 2026

Uses web search to automatically find technical specifications and references
for format definitions that don't have them yet.
"""

import json
import os
import sys
from pathlib import Path
from typing import Dict, List, Optional, Tuple
import re

# Format categories with common search patterns
FORMAT_SEARCH_PATTERNS = {
    "3D": ["{} file format specification", "{} 3D format technical documentation"],
    "Archives": ["{} archive format specification", "{} compression format RFC"],
    "Audio": ["{} audio format specification", "{} audio codec documentation"],
    "CAD": ["{} CAD format specification", "{} engineering file format"],
    "Certificates": ["{} certificate format specification", "{} PKI format"],
    "Crypto": ["{} cryptographic format specification", "{} encryption format"],
    "Data": ["{} data format specification", "{} serialization format"],
    "Database": ["{} database format specification", "{} database file structure"],
    "Disk": ["{} disk image format", "{} filesystem specification"],
    "Documents": ["{} document format specification", "{} file format standard"],
    "Executables": ["{} executable format specification", "{} binary format"],
    "Fonts": ["{} font format specification", "{} typography format"],
    "Game": ["{} ROM format specification", "{} game file format"],
    "Images": ["{} image format specification", "{} graphics format standard"],
    "Medical": ["{} medical imaging format", "{} DICOM specification"],
    "Network": ["{} network capture format", "{} packet format specification"],
    "Programming": ["{} object file format", "{} compiled format specification"],
    "Science": ["{} scientific data format", "{} research data specification"],
    "System": ["{} system file format", "{} OS file specification"],
    "Video": ["{} video format specification", "{} video codec documentation"]
}

# Common specification sources (to recognize authoritative sources)
AUTHORITATIVE_SOURCES = [
    "ietf.org", "rfc-editor.org",  # IETF RFCs
    "iso.org", "iec.ch",  # ISO/IEC standards
    "w3.org",  # W3C specifications
    "ecma-international.org",  # ECMA standards
    "ieee.org",  # IEEE standards
    "microsoft.com/docs",  # Microsoft documentation
    "apple.com/developer",  # Apple documentation
    "adobe.com",  # Adobe specifications
    "khronos.org",  # Khronos standards (OpenGL, etc.)
    "xiph.org",  # Xiph.Org (Vorbis, FLAC, etc.)
    "freedesktop.org",  # FreeDesktop.org specs
    "github.com",  # Official repositories
    "docs.fileformat.com",  # File format documentation
    "wikipedia.org"  # Wikipedia (as fallback)
]


def extract_format_info(json_data: dict) -> Tuple[str, str, List[str]]:
    """Extract format name, category, and extensions from JSON"""
    format_name = json_data.get("formatName", "Unknown")
    category = json_data.get("category", "")
    extensions = json_data.get("extensions", [])
    return format_name, category, extensions


def generate_search_queries(format_name: str, category: str, extensions: List[str]) -> List[str]:
    """Generate search queries for finding format specifications"""
    queries = []

    # Clean format name for search
    clean_name = format_name.replace(" Archive", "").replace(" Format", "").replace(" File", "")

    # Get category-specific patterns
    patterns = FORMAT_SEARCH_PATTERNS.get(category, [
        "{} file format specification",
        "{} format technical documentation"
    ])

    # Generate queries from patterns
    for pattern in patterns:
        queries.append(pattern.format(clean_name))

    # Add extension-based queries
    if extensions:
        main_ext = extensions[0].replace(".", "").upper()
        queries.append(f"{main_ext} file format specification")
        queries.append(f"{main_ext} format RFC")

    # Add direct queries
    queries.extend([
        f"{clean_name} specification site:ietf.org",
        f"{clean_name} specification site:iso.org",
        f"{clean_name} format site:w3.org",
        f"{clean_name} technical specification",
        f"what is {clean_name} format"
    ])

    return queries[:5]  # Limit to top 5 queries


def parse_search_results(search_text: str, format_name: str) -> Tuple[List[str], List[str]]:
    """
    Parse search results to extract specifications and links
    This is a placeholder - in real implementation, you would use WebSearch tool
    """
    specifications = []
    web_links = []

    # Extract patterns that look like specifications
    spec_patterns = [
        r'RFC\s+\d+',
        r'ISO/IEC\s+\d+[-\d]*',
        r'ECMA-\d+',
        r'IEEE\s+\d+',
        r'W3C\s+\w+',
        r'\w+\s+Specification',
        r'\w+\s+Standard'
    ]

    for pattern in spec_patterns:
        matches = re.findall(pattern, search_text, re.IGNORECASE)
        for match in matches:
            if match not in specifications:
                specifications.append(match)

    # Extract URLs
    url_pattern = r'https?://[^\s<>"{}|\\^`\[\]]+'
    urls = re.findall(url_pattern, search_text)

    # Filter for authoritative sources
    for url in urls:
        if any(source in url.lower() for source in AUTHORITATIVE_SOURCES):
            if url not in web_links:
                web_links.append(url)

    return specifications[:5], web_links[:5]  # Limit to top 5 each


def research_format(file_path: Path, json_data: dict) -> Optional[Dict]:
    """
    Research a format and return references
    Returns None if research should be skipped
    """
    # Check if already has references
    if "references" in json_data:
        return None

    format_name, category, extensions = extract_format_info(json_data)

    print(f"  [🔍] Researching: {format_name} ({category})")

    # Generate search queries
    queries = generate_search_queries(format_name, category, extensions)

    # In a real implementation, you would use WebSearch here
    # For now, we'll create a template structure

    # Placeholder: In real implementation, perform web searches
    # search_results = []
    # for query in queries:
    #     result = perform_web_search(query)
    #     search_results.extend(result)

    # For demonstration, return a template
    return {
        "specifications": [
            f"{format_name} Format Specification",
            f"{format_name} Technical Documentation"
        ],
        "web_links": [
            f"# TODO: Research needed for {format_name}",
            f"# Search queries: {', '.join(queries[:2])}"
        ],
        "research_status": "pending_manual_review"
    }


def create_research_plan(format_defs_dir: Path) -> Dict:
    """
    Create a research plan for all formats without references
    Groups by category for efficient batch research
    """
    plan = {}
    json_files = list(format_defs_dir.rglob("*.json"))

    for json_file in json_files:
        try:
            with open(json_file, 'r', encoding='utf-8-sig') as f:
                data = json.load(f)

            # Skip if already has references
            if "references" in data:
                continue

            format_name, category, extensions = extract_format_info(data)

            if category not in plan:
                plan[category] = []

            plan[category].append({
                "file": str(json_file.relative_to(format_defs_dir)),
                "format_name": format_name,
                "extensions": extensions,
                "search_queries": generate_search_queries(format_name, category, extensions)
            })
        except Exception as e:
            print(f"Error processing {json_file}: {e}")

    return plan


def main():
    script_dir = Path(__file__).parent
    format_defs_dir = script_dir.parent / "FormatDefinitions"

    if not format_defs_dir.exists():
        print(f"ERROR: FormatDefinitions directory not found at: {format_defs_dir}")
        sys.exit(1)

    print("=== Automatic Format Reference Research ===")
    print()
    print(f"Format Definitions Path: {format_defs_dir}")
    print()

    # Create research plan
    print("Creating research plan...")
    plan = create_research_plan(format_defs_dir)

    # Display statistics
    total_formats = sum(len(formats) for formats in plan.values())
    print(f"\nFormats needing research: {total_formats}")
    print(f"Categories: {len(plan)}")
    print()

    # Display plan by category
    for category, formats in sorted(plan.items()):
        print(f"\n{category}: {len(formats)} formats")
        for fmt in formats[:3]:  # Show first 3
            print(f"  • {fmt['format_name']}")
        if len(formats) > 3:
            print(f"  ... and {len(formats) - 3} more")

    # Save research plan
    plan_file = script_dir / "ResearchPlan.json"
    with open(plan_file, 'w', encoding='utf-8') as f:
        json.dump(plan, f, indent=2, ensure_ascii=False)

    print(f"\nResearch plan saved to: {plan_file}")
    print()
    print("=" * 60)
    print("NEXT STEPS:")
    print("=" * 60)
    print()
    print("1. MANUAL RESEARCH (Recommended for accuracy):")
    print("   - Review ResearchPlan.json")
    print("   - For each format, search using the provided queries")
    print("   - Add findings to FORMAT_REFERENCES in add_references.py")
    print("   - Run: python add_references.py")
    print()
    print("2. AUTOMATED RESEARCH (Using WebSearch):")
    print("   - Integrate WebSearch API calls in this script")
    print("   - Parse results to extract specifications and links")
    print("   - Validate extracted information")
    print("   - Auto-generate references")
    print()
    print("3. HYBRID APPROACH (Best of both):")
    print("   - Use WebSearch for initial research")
    print("   - Flag uncertain results for manual review")
    print("   - Maintain quality control")
    print()
    print("FORMAT CATEGORIES TO RESEARCH:")
    for i, (category, formats) in enumerate(sorted(plan.items()), 1):
        print(f"  {i:2d}. {category:20s} - {len(formats):3d} formats")
    print()


if __name__ == "__main__":
    main()
