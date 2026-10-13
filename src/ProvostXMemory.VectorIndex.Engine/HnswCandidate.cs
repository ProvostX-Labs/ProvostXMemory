using System;
using System.Collections.Generic;

namespace ProvostXMemory.VectorIndex.Engine;

/// <summary>A single candidate during graph traversal: node id + metric score.</summary>
public readonly record struct HnswCandidate(int Node, float Score);
