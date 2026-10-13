using System;
using System.Collections.Generic;
using System.Linq;

namespace ProvostXMemory.Eval;


/// <summary>One returned id with its graded relevance gain for this case.</summary>
public sealed record EvalCaseTraceHit(
    string RecordId,
    double Gain);

