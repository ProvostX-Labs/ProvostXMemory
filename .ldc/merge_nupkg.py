#!/usr/bin/env python3
"""merge_nupkg.py — merge ALL ProvostXMemory layer assemblies into the facade nupkg.

The facade `ProvostXMemory` is THE single consumer package: `dotnet pack` produces
it with its own DLL/XML, then this script appends every project-referenced layer
assembly (DLL + XML docs) under lib/net8.0/. Deterministic: fixed zip timestamps,
sorted entries, no deps churn (the layer DLLs carry no NuGet dependencies).

Usage: python3 .ldc/merge_nupkg.py <nupkg-path> <bin-root-pattern>
  e.g. python3 .ldc/merge_nupkg.py src/ProvostXMemory/bin/Release/ProvostXMemory.1.0.0.nupkg 'src/*/bin/Release/net8.0'
"""
import glob, os, re, sys, zipfile

FROZEN_TS = (2026, 1, 1, 0, 0, 0)  # deterministic zip timestamps


def strip_deps(nuspec):
    """All layer assemblies are merged INTO this package — remove their
    (unresolvable) ProvostXMemory.* dependency entries; KEEP real package
    dependencies (ILGPU)."""
    nuspec = re.sub(r"\s*<dependency id=\"ProvostXMemory\.[A-Za-z.]*\" version=\"[^\"]*\"[^/]*/>", "", nuspec)
    nuspec2 = re.sub(r"\s*<group[^>]*>\s*</group>", "", nuspec)
    nuspec2 = re.sub(r"\s*<dependencies>\s*</dependencies>", "", nuspec2)
    return nuspec2


def main():
    nupkg, bin_glob = sys.argv[1], sys.argv[2]
    layers = {}
    for d in sorted(glob.glob(bin_glob)):
        name = os.path.basename(os.path.dirname(os.path.dirname(os.path.dirname(d))))
        if name in ("ProvostXMemory", "ProvostXMemory.Examples"):
            continue  # facade already packed; Examples = console app
        for ext in (".dll", ".xml"):
            p = os.path.join(d, name + ext)
            if os.path.exists(p):
                layers[f"lib/net8.0/{name}{ext}"] = p

    # Read the packed nupkg, rewrite it deterministically: merged layers in,
    # dependency group stripped (all layers are inside this one package).
    with zipfile.ZipFile(nupkg) as z:
        entries = {n: z.read(n) for n in z.namelist()}
    for arc, src in sorted(layers.items()):
        if arc in entries:
            raise SystemExit(f"already present: {arc}")
        with open(src, "rb") as f:
            entries[arc] = f.read()
    nuspec_name = next(n for n in entries if n.endswith(".nuspec"))
    entries[nuspec_name] = strip_deps(entries[nuspec_name].decode("utf-8")).encode("utf-8")

    info_cache = {}
    def zinfo(arc):
        if arc not in info_cache:
            i = zipfile.ZipInfo(arc, FROZEN_TS)
            i.compress_type = zipfile.ZIP_DEFLATED
            i.external_attr = 0o644 << 16
            info_cache[arc] = i
        return info_cache[arc]

    tmp = nupkg + ".tmp"
    with zipfile.ZipFile(tmp, "w", zipfile.ZIP_DEFLATED) as z:
        for arc in sorted(entries):
            z.writestr(zinfo(arc), entries[arc])
    os.replace(tmp, nupkg)
    with zipfile.ZipFile(nupkg) as z:
        names = z.namelist()
    ns = entries[nuspec_name]
    print(f"merged {len(layers)} layer files; total entries: {len(names)}; "
          f"ProvostXMemory.* deps stripped: {b'ProvostXMemory.' not in ns}; "
          f"package deps kept: {ns.count(b'<dependency id=')}")


if __name__ == "__main__":
    main()