# Third-Party Notices

This project is licensed under the MIT License (see [LICENSE](LICENSE)).

## ILGPU

- **Package:** ILGPU (`ILGPU` NuGet package)
- **Version used:** 1.5.1 (referenced from `src/ProvostXMemory.VectorIndex.Gpu/ProvostXMemory.VectorIndex.Gpu.csproj`)
- **Used by:** `ProvostXMemory.VectorIndex.Gpu` (optional package; GPU acceleration kernels for the Flat and IVF backends)
- **License:** NCSA Open Source License (University of Illinois/NCSA Open Source License) — a permissive, MIT-compatible license; verified against the `LICENSE.txt` shipped inside the NuGet package
- **Copyright:** Copyright (c) 2016–2023 ILGPU Project
- **Source:** https://github.com/ILGPU/ILGPU

The ILGPU runtime is loaded lazily and only when the optional `ProvostXMemory.VectorIndex.Gpu` package is referenced. Hosts that reference only the core library and the `ProvostXMemory.VectorIndex.Engine` package do not take a dependency on ILGPU.

ILGPU is dynamically used (NuGet package reference), not modified or redistributed in source form; no notice text changes were made to its sources.

## Summary

| Component | License | Scope |
|---|---|---|
| ProvostXMemory (this repository) | MIT | all core packages |
| ILGPU | NCSA (MIT-compatible) | GPU acceleration (ships in the ProvostXMemory package) |

No other third-party runtime dependencies exist. The vector engine (Flat/IVF/HNSW) is implemented in this repository; no third-party vector-index library is used.

## Primer Syntax Colors (GitHub)

- **Used by:** `docs/api-site` (DocFX API reference site) — code syntax highlighting color palette in `.docfx-template/styles/main.css` (v1.15)
- **Scope:** color values only (hex), no code, no assets; applied as CSS overrides on top of the DocFX default template
- **License:** MIT (https://github.com/primer/primer/blob/main/LICENSE)
- **Source:** https://github.com/primer/primer (markdown-reading-view / syntax highlighting tokens)
- **Note:** palette verified WCAG 2.1 AA (≥ 4.5:1) against the site's own light/dark code surfaces
