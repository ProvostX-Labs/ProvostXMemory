#!/usr/bin/env python3
"""Regenerate API-INDEX.md, RELATIONSHIP-GRAPH.md, PACKAGE-MAP.md for ProvostXMemory.

Code-driven (parses .cs files), fixes the previously hand-noted generator bugs:
- nested public types listed as Outer.Nested (no flattened bare `Options` entries)
- Examples package included
- LOC = code LOC excluding comments/blank lines (labeled in PACKAGE-MAP)
- dependencies from csproj ProjectReferences (source of truth)
- generic constraints rendered correctly (no mangled `Comparer : X>` entries)
"""
import re, pathlib, datetime

ROOT = pathlib.Path(__file__).resolve().parent.parent

def parse_cs_file(path):
    lines = path.read_text().splitlines()
    types = []
    ns = None
    block = False
    for i, raw in enumerate(lines):
        s = raw.strip()
        if block:
            if "*/" in s: block = False
            continue
        if s.startswith("/*"):
            block = not (s.endswith("*/") and len(s) > 2)
            continue
        nm = re.match(r"^namespace\s+([\w.]+);", s)
        if nm: ns = nm.group(1)
        m = re.match(r"^(public|internal)\s+((?:sealed\s+|static\s+|abstract\s+|partial\s+|readonly\s+)*)(class|record|interface|enum|struct)\s+(\w+)", raw)
        if not m: continue
        kind, name = m.group(3), m.group(4)
        # base list: text after ':' until '{', '(' or ';'
        rest = s[m.end():]
        stop = min([x for x in (rest.find("{"), rest.find("("), rest.find(";")) if x >= 0] or [len(rest)])
        head = rest[:stop]
        base = None
        if ":" in head:
            base = re.sub(r"\s+where\s+.*$", "", head[head.index(":")+1:]).strip().rstrip(",")
            base = re.sub(r"\s+", " ", base)
        # ctor/record param types
        ctor = []
        blob = rest
        j = i
        while "{" not in blob and ";" not in blob and j + 1 < len(lines):
            j += 1; blob += " " + lines[j].strip()
        cm = re.search(r"\(([^()]*)\)", blob)
        if cm and kind in ("record", "class"):
            params = re.sub(r"=\s*[^,]+", "", cm.group(1))
            for p in params.split(","):
                p = p.strip()
                if not p: continue
                pm = re.match(r"(?:\[.*?\]\s*)?([A-Za-z_]\w*(?:<[^()<>]*>)?(?:\[\])?)\s+\w+$", p)
                if pm: ctor.append(pm.group(1))
        # nested public types (immediately following block, until close)
        nested = []
        k = i + 1
        depth = 0
        started = False
        while k < len(lines):
            l2 = lines[k]
            depth += l2.count("{") - l2.count("}")
            if "{" in l2: started = True
            if started and depth <= 0:
                break
            l2s = l2.strip()
            mm = re.match(r"^(public|internal)\s+.*?\b(class|record|interface|enum|struct)\s+(\w+)", l2s)
            if mm and depth >= 1:
                nested.append(mm.group(3))
            k += 1
        types.append(dict(name=name, kind=kind, base=base, ctor=ctor, nested=nested, ns=ns))
    return types

def code_loc(path):
    loc, block = 0, False
    for ln in path.read_text().splitlines():
        s = ln.strip()
        if block:
            if "*/" in s: block = False
            continue
        if not s or s.startswith("//"): continue
        if s.startswith("/*"):
            if not (s.endswith("*/") and len(s) > 2): block = True
            continue
        loc += 1
    return loc

packages = {}
for base in (ROOT / "src", ROOT / "tests"):
    for csproj in sorted(base.rglob("*.csproj")):
        pkg = csproj.parent.name
        files = [f for f in csproj.parent.rglob("*.cs") if "obj" not in f.parts and "bin" not in f.parts]
        types, loc = [], 0
        for f in files:
            types.extend(parse_cs_file(f))
            loc += code_loc(f)
        deps = re.findall(r'Include="[\./]+([\w.]+)/[\w.]+\.csproj"', csproj.read_text())
        packages[pkg] = dict(types=types, loc=loc, deps=deps, csproj=csproj)

top_of = {}
for pkg, d in packages.items():
    for t in d["types"]:
        top_of.setdefault(t["name"], pkg)

now = datetime.datetime.now(datetime.timezone.utc).replace(microsecond=0).isoformat()
ICON = {"interface": "🔵", "class": "🟡", "record": "🟣", "enum": "🟠", "struct": "🟢"}

# ---------------- API-INDEX.md ----------------
api = [f"# API-INDEX.md — ProvostXMemory\n", f"Generated: {now}\n",
       f"Packages: {len(packages)}  |  Types: {sum(len(d['types']) for d in packages.values())}\n", "\n", "---\n", "\n"]
for pkg in sorted(packages):
    d = packages[pkg]
    api.append(f"## {pkg} ({len(d['types'])} types, {d['loc']} code LOC excl. comments)\n\n")
    for t in sorted(d["types"], key=lambda x: x["name"]):
        base = f" : {t['base']}" if t["base"] else ""
        line = f"- {ICON[t['kind']]} {t['name']}{base}  ({t['ns']})"
        if t["ctor"]:
            line += "  deps: [" + ", ".join(t["ctor"]) + "]"
        api.append(line + "\n")
        for n in sorted(set(t["nested"])):
            api.append(f"  - {t['name']}.{n}  ({t['ns']}) [nested]\n")
    api.append("\n")
(ROOT / "API-INDEX.md").write_text("".join(api))

# ---------------- PACKAGE-MAP.md ----------------
pm = [f"# PACKAGE-MAP.md — ProvostXMemory\n", f"Generated: {now}\n", "\n", "---\n", "\n",
      "| Package | Types | code LOC (excl. comments) | ~Tokens | Dependencies |\n|---|---|---|---|---|\n"]
for pkg in sorted(packages):
    d = packages[pkg]
    toks = sum(len(f.read_text()) for f in d["csproj"].parent.rglob("*.cs")
               if "obj" not in f.parts and "bin" not in f.parts) // 4
    deps = ", ".join(sorted(d["deps"])) if d["deps"] else "—"
    pm.append(f"| {pkg} | {len(d['types'])} | {d['loc']} | ~{toks} | {deps} |\n")
(ROOT / "PACKAGE-MAP.md").write_text("".join(pm))

# ---------------- RELATIONSHIP-GRAPH.md ----------------
edges = []
for pkg, d in packages.items():
    for t in d["types"]:
        if t["base"]:
            for b in [b.strip() for b in t["base"].split(",")]:
                bb = re.sub(r"<.*", "", b)
                if re.match(r"^I[A-Z]", bb):
                    edges.append((pkg, t["name"], "implements", b, top_of.get(bb)))
        for p in t["ctor"]:
            pb = re.sub(r"<.*", "", p).strip()
            if pb in top_of and pb != t["name"]:
                edges.append((pkg, t["name"], "uses", pb, top_of[pb]))

rg = [f"# RELATIONSHIP-GRAPH.md — ProvostXMemory\n", f"Generated: {now}\n",
      f"Edges: {len(edges)}  |  Packages: {len(packages)}\n", "\n", "---\n", "\n"]
for pkg in sorted(packages):
    d = packages[pkg]
    rg.append(f"## {pkg}\n\n")
    rg.append("Package deps: " + (", ".join(sorted(d["deps"])) if d["deps"] else "—") + "\n\n")
    mine = sorted(set((e[1], e[2], e[3], e[4]) for e in edges if e[0] == pkg))
    for name, rel, tgt, tpkg in mine:
        cross = " ← CROSS-PKG" if tpkg and tpkg != pkg else ""
        rg.append(f"- {name} ──{rel}──► {tgt} ({tpkg or '?'}){cross}\n")
    rg.append("\n")
(ROOT / "RELATIONSHIP-GRAPH.md").write_text("".join(rg))

print(f"Packages: {len(packages)}, edges: {len(edges)}")