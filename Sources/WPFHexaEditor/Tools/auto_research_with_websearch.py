#!/usr/bin/env python3
"""
Automated Format Reference Research with WebSearch
Apache 2.0 - 2026

Uses Claude's WebSearch capability to automatically find technical specifications
and references for format definitions.

Usage:
    python auto_research_with_websearch.py --batch Game
    python auto_research_with_websearch.py --format ROM_NES
    python auto_research_with_websearch.py --all
"""

import json
import os
import sys
import argparse
from pathlib import Path
from typing import Dict, List, Optional, Tuple
import re

# Authoritative source domains (for validation)
AUTHORITATIVE_SOURCES = [
    "ietf.org", "rfc-editor.org",
    "iso.org", "iec.ch",
    "w3.org",
    "ecma-international.org",
    "ieee.org",
    "microsoft.com", "docs.microsoft.com",
    "apple.com",
    "adobe.com",
    "khronos.org",
    "xiph.org",
    "freedesktop.org",
    "github.com",
    "docs.fileformat.com",
    "fileformat.com",
    "nesdev.org", "gbdev.io",  # Game ROM specs
    "romhacking.net",
    "wikipedia.org"
]

# Categories to process (for batch mode)
PRIORITY_CATEGORIES = ["Game", "Images", "Audio", "Video", "Documents"]


def load_research_plan(plan_file: Path) -> Dict:
    """Load the research plan JSON"""
    with open(plan_file, 'r', encoding='utf-8') as f:
        return json.load(f)


def extract_specifications_from_text(text: str, format_name: str) -> List[str]:
    """Extract specification names from search results"""
    specifications = []

    # Common specification patterns
    patterns = [
        r'RFC\s+\d+',
        r'ISO/IEC\s+[\d\-:]+',
        r'ECMA-\d+',
        r'IEEE\s+[\d\.]+',
        r'ITU-T\s+[A-Z]\.\d+',
        r'\b[A-Z]{2,}\s+Specification\b',
        r'\b[A-Z]{2,}\s+Standard\b',
        r'\bVersion\s+[\d\.]+\s+Specification\b'
    ]

    for pattern in patterns:
        matches = re.findall(pattern, text, re.IGNORECASE)
        for match in matches:
            clean_match = match.strip()
            if clean_match and clean_match not in specifications:
                specifications.append(clean_match)

    # Add format-specific specification if found
    format_spec_pattern = rf'{re.escape(format_name)}\s+(?:Format\s+)?(?:Specification|Standard)'
    matches = re.findall(format_spec_pattern, text, re.IGNORECASE)
    for match in matches[:1]:  # Take first match
        if match not in specifications:
            specifications.insert(0, match.strip())

    return specifications[:5]  # Limit to top 5


def extract_urls_from_text(text: str) -> List[str]:
    """Extract URLs from search results, prioritizing authoritative sources"""
    url_pattern = r'https?://[^\s<>"{}|\\^`\[\]]+[^\s<>"{}|\\^`\[\].,;:!?)]'
    urls = re.findall(url_pattern, text)

    authoritative_urls = []
    other_urls = []

    for url in urls:
        # Clean URL
        url = url.rstrip('.,;:!?)')

        # Skip duplicates
        if url in authoritative_urls or url in other_urls:
            continue

        # Check if authoritative
        is_authoritative = any(source in url.lower() for source in AUTHORITATIVE_SOURCES)

        if is_authoritative:
            authoritative_urls.append(url)
        else:
            other_urls.append(url)

    # Prioritize authoritative sources
    result = authoritative_urls[:3] + other_urls[:2]
    return result[:5]  # Max 5 links


def research_format_with_websearch(format_info: Dict, category: str, dry_run: bool = False) -> Optional[Dict]:
    """
    Research a format using web search.

    NOTE: This function creates a TODO marker for Claude Code to perform the actual search.
    Claude Code will see this and execute WebSearch for each query.
    """
    format_name = format_info['format_name']
    queries = format_info['search_queries']

    print(f"\n  🔍 Researching: {format_name}")

    # This section will be replaced by actual WebSearch calls
    # For now, we create a structured request
    research_result = {
        "format_name": format_name,
        "category": category,
        "queries": queries,
        "specifications": [],
        "web_links": [],
        "status": "needs_websearch"
    }

    if dry_run:
        print(f"     [DRY RUN] Would search: {queries[0]}")
        return research_result

    # TODO: Integrate WebSearch here
    # For each query, we need to call WebSearch and aggregate results
    # This will be done by Claude Code using the WebSearch tool

    return research_result


def format_references_for_json(specifications: List[str], web_links: List[str]) -> Dict:
    """Format references for JSON insertion"""
    return {
        "specifications": specifications if specifications else [],
        "web_links": web_links if web_links else []
    }


def update_json_with_references(json_file: Path, references: Dict, dry_run: bool = False) -> bool:
    """Add references to a JSON file"""
    try:
        with open(json_file, 'r', encoding='utf-8-sig') as f:
            data = json.load(f)

        # Skip if already has references
        if "references" in data and not dry_run:
            print(f"     ⏭️  Already has references")
            return False

        if dry_run:
            print(f"     [DRY RUN] Would add {len(references.get('specifications', []))} specs, "
                  f"{len(references.get('web_links', []))} links")
            return True

        # Insert references after 'author'
        data["references"] = references

        # Write back
        with open(json_file, 'w', encoding='utf-8-sig') as f:
            json.dump(data, f, indent=2, ensure_ascii=False)

        print(f"     ✅ Added references")
        return True

    except Exception as e:
        print(f"     ❌ Error: {e}")
        return False


def process_category(category: str, plan: Dict, format_defs_dir: Path, dry_run: bool = False, limit: int = None):
    """Process all formats in a category"""
    if category not in plan:
        print(f"❌ Category '{category}' not found in research plan")
        return

    formats = plan[category]
    if limit:
        formats = formats[:limit]

    print(f"\n{'='*60}")
    print(f"📦 Processing category: {category}")
    print(f"📊 Formats to research: {len(formats)}")
    print(f"{'='*60}")

    results = {
        "category": category,
        "total": len(formats),
        "researched": 0,
        "updated": 0,
        "skipped": 0,
        "failed": 0,
        "formats": []
    }

    for i, format_info in enumerate(formats, 1):
        print(f"\n[{i}/{len(formats)}] {format_info['format_name']}")

        # Research format
        research_result = research_format_with_websearch(format_info, category, dry_run)

        if research_result:
            results["researched"] += 1

            # Check if we have valid results
            if research_result.get("status") == "needs_websearch":
                results["formats"].append({
                    "name": format_info['format_name'],
                    "file": format_info['file'],
                    "status": "pending_websearch",
                    "queries": research_result['queries']
                })
            else:
                # We have specifications and links
                references = format_references_for_json(
                    research_result.get("specifications", []),
                    research_result.get("web_links", [])
                )

                # Update JSON file
                json_file = format_defs_dir / format_info['file']
                if update_json_with_references(json_file, references, dry_run):
                    results["updated"] += 1
                else:
                    results["skipped"] += 1
        else:
            results["failed"] += 1

    # Print summary
    print(f"\n{'='*60}")
    print(f"📊 Category '{category}' Summary:")
    print(f"{'='*60}")
    print(f"  Total formats:    {results['total']}")
    print(f"  Researched:       {results['researched']}")
    print(f"  Updated:          {results['updated']}")
    print(f"  Skipped:          {results['skipped']}")
    print(f"  Failed:           {results['failed']}")
    print(f"{'='*60}\n")

    return results


def main():
    parser = argparse.ArgumentParser(
        description='Automated format reference research with WebSearch'
    )
    parser.add_argument('--batch', type=str, help='Process entire category (e.g., Game, Images)')
    parser.add_argument('--format', type=str, help='Process single format by name')
    parser.add_argument('--all', action='store_true', help='Process all formats')
    parser.add_argument('--priority', action='store_true', help='Process only priority categories')
    parser.add_argument('--dry-run', action='store_true', help='Show what would be done without making changes')
    parser.add_argument('--limit', type=int, help='Limit number of formats to process per category')

    args = parser.parse_args()

    # Setup paths
    script_dir = Path(__file__).parent
    format_defs_dir = script_dir.parent / "FormatDefinitions"
    plan_file = script_dir / "ResearchPlan.json"

    if not plan_file.exists():
        print(f"❌ Research plan not found: {plan_file}")
        print("Run 'python auto_research_references.py' first to generate the plan.")
        sys.exit(1)

    # Load research plan
    plan = load_research_plan(plan_file)

    print("="*60)
    print("🔬 Automated Format Reference Research")
    print("="*60)
    if args.dry_run:
        print("⚠️  DRY RUN MODE - No files will be modified")
    print()

    # Process based on arguments
    if args.batch:
        process_category(args.batch, plan, format_defs_dir, args.dry_run, args.limit)

    elif args.priority:
        print("📌 Processing priority categories:")
        print(f"   {', '.join(PRIORITY_CATEGORIES)}\n")

        for category in PRIORITY_CATEGORIES:
            if category in plan:
                process_category(category, plan, format_defs_dir, args.dry_run, args.limit)

    elif args.all:
        for category in sorted(plan.keys()):
            process_category(category, plan, format_defs_dir, args.dry_run, args.limit)

    elif args.format:
        print(f"🔍 Searching for format: {args.format}")
        # Search for format in plan
        found = False
        for category, formats in plan.items():
            for fmt in formats:
                if args.format.lower() in fmt['format_name'].lower():
                    print(f"   Found in category: {category}")
                    research_result = research_format_with_websearch(fmt, category, args.dry_run)
                    found = True
                    break
            if found:
                break

        if not found:
            print(f"❌ Format '{args.format}' not found in research plan")

    else:
        print("❌ Please specify --batch, --format, --priority, or --all")
        parser.print_help()
        sys.exit(1)

    print("\n✅ Research session complete!")
    print("\nNOTE: This tool currently generates research tasks.")
    print("Use Claude Code's WebSearch capability to execute the actual searches.")


if __name__ == "__main__":
    main()
