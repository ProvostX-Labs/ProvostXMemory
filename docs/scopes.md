# Scopes and Multi-Tenancy

Scopes are the isolation unit of ProvostXMemory. This page covers scope kinds, the on-disk layout, hierarchy semantics, and patterns for multi-tenant hosts.

## Scope kinds

`MemoryScopeKind` has five values; they are labels for intent, not behavior switches:

| Kind | Typical use |
|---|---|
| `Platform` | Root scope of a host installation or product surface. |
| `Session` | One conversation / working session. |
| `Tenant` | One customer or organization in a multi-tenant host. |
| `Purpose` | A functional partition (e.g. curated long-term notes vs. scratch). |
| `Custom` | Host-defined partitioning. |

## Path layout

`MemoryRootLayout` resolves every scope to a unique directory subtree under the configured root:

```
<root>/
  manifest.json
  <scope-kind>/<scope-id>/
    records.jsonl
    chunks/
    vectors/<model-hash>/
      index.json          (FlatJson backend)
      index artifacts     (Engine / GPU backends)
    graph/
      entities.jsonl
      relations.jsonl
      clusters.jsonl
```

Because isolation is implemented as path resolution, cross-scope access is structurally impossible: an operation bound to scope `Session/user-42` never reads or writes outside `Session/user-42/`. There is no "global override", and none is needed — see the multi-tenancy patterns below for legitimate cross-scope aggregation.

## Hierarchy

`MemoryScope` is `(Kind, Id, ParentScope?)`:

```csharp
var tenant = new MemoryScope(MemoryScopeKind.Tenant, "acme");
var session = new MemoryScope(MemoryScopeKind.Session, "chat-1", tenant);

session.Chain();       // [Tenant/acme, Session/chat-1] — outermost first
tenant.Contains(session);   // true
session.Contains(tenant);   // false
```

Semantics pinned by tests: chains flatten outermost-first; `Contains` is reflexive and transitive over the chain; scopes are immutable (`WithParent` returns a copy). Persistence equality is kind + id — parent chains are host-side metadata used for `Contains` decisions and query-plan fan-out, and do not change where artifacts live.

## Multi-tenancy patterns

**Strict isolation (default).** One scope per tenant; every `RecallAsync` and `RememberAsync` carries the tenant's scope. Nothing crosses. Use `QueryPlan` fan-out only when you deliberately want a merged view.

**Scoped aggregation via plans.** To answer a question "over a tenant and the current session", issue a `RetrievalQuery` with a `QueryPlan` whose steps target both scope ids. The plan runner fuses results with scope-qualified ids — the same record id in two scopes stays distinct — and structural isolation is preserved because fan-out is an explicit, declared operation, not ambient access.

**Root + children.** Model shared knowledge at the root (`Platform`) and per-context children beneath it (`Tenant`, `Session`). Children are separate scopes with their own artifacts; "inheritance" of knowledge is expressed as plan fan-out, not implicit reads.

**Enumeration.** `IMemoryService.Scopes()` lists existing scopes under the root (via the store's `IScopesAware` hook, with a directory-layout fallback), enabling admin-style overviews without breaching per-scope read isolation.

## Testing your isolation story

The library's test suite pins cross-scope invisibility at the storage, retrieval, and journey levels. For host-side confidence, assert in your own tests that a query scoped to tenant A never returns tenant B record ids — `RecallAsync` makes this a data assertion, no configuration required.
