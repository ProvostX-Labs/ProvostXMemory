using System;
using System.Collections.Generic;

namespace ProvostXMemory.VectorIndex;


/// <summary>One IVF member: id, vector, assigned cluster.</summary>
public sealed record IvfMember(string Id, float[] Vector, int Cluster);
