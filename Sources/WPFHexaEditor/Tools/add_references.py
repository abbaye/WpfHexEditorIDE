#!/usr/bin/env python3
import json, sys
from pathlib import Path
import urllib.parse

KNOWN_SPECS = {
    "3DS": {"specs": ["3D Studio Format"], "links": ["https://en.wikipedia.org/wiki/.3ds"]},
    "STL": {"specs": ["STL Format"], "links": ["https://en.wikipedia.org/wiki/STL_(file_format)"]},
}

def load_json(p):
    with open(p, 'r', encoding='utf-8-sig') as f: return json.load(f)

def save_json(p, d):
    with open(p, 'w', encoding='utf-8-sig') as f: json.dump(d, f, indent=2, ensure_ascii=False)

def get_refs(name, exts):
    n = name.upper().replace(" ", "_")
    if n in KNOWN_SPECS: return KNOWN_SPECS[n]
    ext = exts[0].lstrip('.').upper() if exts else name
    return {"specs": [f"{name} Format"], "links": [f"https://en.wikipedia.org/wiki/{urllib.parse.quote(ext)}"]}

def process(p, dry=False):
    d = load_json(p)
    if "references" in d:
        print(f"  Skip {p.name}")
        return False
    r = get_refs(d.get("formatName", "?"), d.get("extensions", []))
    if "author" not in d: d["author"] = "WPFHexaEditor Team"
    nd = {}
    for k in d:
        nd[k] = d[k]
        if k == "author": nd["references"] = {"specifications": r["specs"], "web_links": r["links"]}
    if dry:
        print(f"  Would add {len(r['links'])} refs to {p.name}")
        return True
    save_json(p, nd)
    print(f"  OK {p.name}")
    return True

fd = Path("FormatDefinitions")
st = {"ok": 0, "skip": 0}
dry = "--dry-run" in sys.argv

for cat in sorted(fd.iterdir()):
    if not cat.is_dir(): continue
    print(f"\n[{cat.name}]")
    for jf in sorted(cat.glob("*.json")):
        if process(jf, dry): st["ok"] += 1
        else: st["skip"] += 1

print(f"\nProcessed: {st['ok']}, Skipped: {st['skip']}")
