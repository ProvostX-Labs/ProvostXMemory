using System;
using System.Collections.Generic;

using ProvostXMemory.Abstractions;
using ProvostXMemory.Retrieval;

namespace ProvostXMemory;


/// <summary>Implemented by stores that can enumerate their scopes natively.</summary>
public interface IScopesAware
{
    /// <summary>All scopes currently known to the store.</summary>
    System.Collections.Generic.IReadOnlyList<MemoryScope> Scopes();
}
