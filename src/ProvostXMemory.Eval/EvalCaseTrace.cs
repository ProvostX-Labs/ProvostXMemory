using System;
using System.Collections.Generic;
using System.Linq;

namespace ProvostXMemory.Eval;


/// <summary>Per-case ranked outcome: returned ids with their graded relevance gains and
/// how many returned records came from a scope other than the case's own (isolation guard).</summary>
public sealed record EvalCaseTrace(
    string CaseId,
    string ScopeKind,
    string ScopeId,
    int ForeignRecordHits,
    IReadOnlyList<EvalCaseTraceHit> Hits);

