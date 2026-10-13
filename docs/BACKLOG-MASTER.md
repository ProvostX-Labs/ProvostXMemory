# BACKLOG-MASTER — ProvostXMemory (per-repo work queue)

**Summary:** Active work queue. Items are owner-directed (chat orders 2026-10-13) recorded retrospectively with wave commits as the audit trail. New items ONLY via global IDEAS-MASTER → owner acceptance.

## Item schema
```
### BL-<n> · <title>
- Source: owner-directed (chat <date>) / <IDEAS-MASTER ID>
- Spec: one-two sentences
- Scope: files/areas allowed to touch
- Acceptance: which tests prove it
- Status: ready / in-progress / in-review / done
```

## Rules
- Owner is the only gate for new items and for any future merge-to-main / publish.
- Finished items keep a `done` line with commit + date for audit.

## Queue (retrospective record — waves built 2026-10-13)

### BL-01 · Greenfield build: host-agnostic memory/RAG library (owner chat 2026-10-13 08:46–08:54)
- Source: owner-directed in chat (create ProvostXMemory as MIT NuGet library, FAISS-equivalent own engine, encapsulated, scope-isolated)
- Spec: see CONCEPT.md + ARCHITECTURE.md. Layers L0–L3, swappable backends, scope isolation by construction.
- Scope: whole repo.
- Acceptance: full build 0 errors/warnings + full test suite green.
- Status: **done** — waves 1–3 (commits 305cce4, 7683750, f9dbf83, 17413c8, 69004bb, ccb5d0d, 6247001, 9be3d6d, 549208b): scaffold, Storage+Telemetry, Lexical+Fusion, VectorIndex (FlatJson + own SIMD engine Flat/IVF), Chunking, Graph, Consolidation, Correction+Reranking, Retrieval features, MemoryService facade.

### BL-02 · HNSW backend, patent-safe (owner chat 2026-10-13 08:54 + 13:07)
- Source: owner-directed (build own FAISS-equivalent incl. HNSW after patent review)
- Spec: HNSW in own engine with immutable-index + batch-rebuild semantics (patent addendum in ARCHITECTURE.md); conformance suite + recall quality tests.
- Scope: ProvostXMemory.VectorIndex.Engine + tests.
- Acceptance: recall@10 >= 0.90 on 2000×64 synthetic corpus; conformance suite green.
- Status: **done** (d70f086, recall@10 = 0.998, 3 bring-up bugs found+fixed).

### BL-03 · GPU acceleration via ILGPU with CPU fallback (owner chat 2026-10-13 13:07)
- Source: owner-directed (GPU matrix; later tightened brief after slow first run)
- Spec: GpuFlat + GpuIvf via ILGPU kernels, lazy guarded loading, transparent bit-exact CPU fallback when no GPU.
- Scope: ProvostXMemory.VectorIndex.Gpu + tests.
- Acceptance: shared conformance suite green incl. on CPU-only machine; determinism test.
- Status: **done** (f602200; ILGPU 1.5.3, NCSA license verified in-package; 21 GPU tests; two agents' work merged).

### BL-04 · Eval harness (owner chat 2026-10-13 12:09)
- Source: owner-directed (eval + full e2e tests, agnostic)
- Spec: Recall@k/Precision@k/MRR/NDCG over deterministic synthetic corpus; regression guard; JSON report.
- Scope: ProvostXMemory.Eval + tests.
- Acceptance: hand-verified metric math; Recall@10 >= 0.9 regression guard green.
- Status: **done** (ff7d1e2, 56 tests).

### BL-05 · Persist Reps/Meta + graph cluster write path (flagged during waves)
- Source: wave-agent findings (owner: "do all fixes")
- Spec: MemoryEntryModel persists Reps/Meta backward-compat; UpsertClusterAsync on IMemoryGraphStore + consolidation wiring.
- Scope: Abstractions, Storage, Graph, Consolidation, facade + all implementors updated in one commit.
- Acceptance: round-trip tests; old-format compatibility; consolidation→cluster wiring test.
- Status: **done** (722439a).

### BL-06 · Agnostic system tests: unit/integration/journey (owner chat 2026-10-13 13:07)
- Source: owner-directed (full e2e; NO ProvostX product references)
- Spec: three-tier suite through public surface only — unit gaps, real-composition integration across all backends (restart-persistence, multi-scope), three journeys (knowledge assistant w/ eval gate, multi-tenant isolation, lifecycle).
- Scope: tests/ProvostXMemory.SystemTests only.
- Acceptance: full suite green; zero product references.
- Status: **in-progress** (agent running; lands as wave 5).

### BL-07 · Configurable + audit-grade logging (owner chat 2026-10-13 13:49–13:54)
- Source: owner-directed (on/off toggle, configurable location/format; audit-conform: append-only, hash-chain tamper evidence, retention, content minimization)
- Spec: MemoryLoggingOptions (Enabled/Level/Sink/Directory/FileNamePattern/Format/IncludeContent/Integrity/RetentionDays); File+Console sinks; SHA-256 hash-chain + sequence numbers; LogChainVerifier; RetentionSweeper; facade wiring.
- Scope: ProvostXMemory.Logging + facade options wiring + tests.
- Acceptance: disabled = zero writes/allocations; tamper detection test; no content leak by default; rotation + retention tests.
- Status: **in-progress** (logging package agent running as wave 6; facade wiring + verifier follow-up).

### BL-08 · Closure paperwork (owner process: LDC + docs)
- Spec: regenerate API-INDEX.md / RELATIONSHIP-GRAPH.md / PACKAGE-MAP.md (LDC), README with public-surface example, ARCHITECTURE.md final status addendum.
- Acceptance: LDC generation passes; docs accurate against code.
- Status: **ready** (after BL-06 + BL-07 land).

### BL-09 · Docs/examples presentation wave (owner-directed 2026-10-13)
- Source: owner-directed (chat 2026-10-13): Microsoft-docs-quality documentation set for the GitHub/NuGet representation.
- Spec: rewrite README.md as landing page (badges, feature/backend tables, pipeline diagram, docs links); new docs/ set (getting-started, concepts, retrieval, backends, consolidation, evaluation, logging, scopes, api-glossary, faq); self-contained offline docs/wiki.html; THIRD-PARTY-NOTICES.md (ILGPU NCSA, verified against nupkg); backlog update.
- Scope: README.md, docs/*, THIRD-PARTY-NOTICES.md only. No src/ or tests/ changes; docs describe the committed public API only.
- Acceptance: all internal links resolve; wiki.html is standalone (no external requests); anonymity scan clean; committed as one docs commit.
- Status: **done — docs scope** (this commit; docs/ set + README + wiki.html + THIRD-PARTY-NOTICES landed). Examples-project presentation line stays open pending the parallel examples agent's report.

## Backlog (not started — owner-gated)
- NuGet packaging/publish configuration (needs owner ship decision; version floor + anonymity scan per release rules)
- Inference-backed IVectorEmbedder adapter package (separate, depends on integration decision)
- HNSW patent application status re-check (periodic)
