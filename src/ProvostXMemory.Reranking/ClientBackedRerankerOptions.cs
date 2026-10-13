namespace ProvostXMemory.Reranking;

/// <summary>
/// Options for <see cref="ClientBackedReranker"/>: which client ids to trust and where
/// fallback keeps unlisted/dropped candidates.
/// </summary>
/// <param name="TrustClientCompletely">
/// When true, only candidates listed by the client survive. When false, unlisted or dropped
/// candidates keep fused order behind the client-ranked ones.
/// </param>
public sealed record ClientBackedRerankerOptions(bool TrustClientCompletely = true);