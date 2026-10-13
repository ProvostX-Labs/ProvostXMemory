# ProvostXMemory.Fusion.API.md

Types: 2  |  LOC: 88  |  ~109 tokens

---

Dependencies: ProvostXMemory.Abstractions

### Interface: IResultFuser
> A single ranked list input to fusion: source name plus ranked hits.
Methods:
  - IReadOnlyList<ScoredId> Fuse(IReadOnlyList<RankedList> rankedLists, FusionOptions options)
Cross-package deps: ProvostXMemory.Abstractions

### Class: ReciprocalRankFuser
> Reciprocal Rank Fusion: each item scores sum over lists of weight / (k + rank),
Implements: IResultFuser
Cross-package deps: ProvostXMemory.Abstractions
