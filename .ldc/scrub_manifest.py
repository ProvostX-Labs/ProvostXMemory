#!/usr/bin/env python3
"""scrub_manifest.py — remove machine-identifying source paths from the
generated DocFX manifest (source_base_path carries the build machine's
absolute path; the committed site must stay anonymous). Run after docfx build."""
import json, sys
p = sys.argv[1] if len(sys.argv) > 1 else "docs/api-site/manifest.json"
d = json.load(open(p))
removed = d.pop("source_base_path", None)
json.dump(d, open(p, "w"), indent=2)
print("scrubbed source_base_path:", removed is not None)
