namespace ProvostXMemory.Reranking;

/// <summary>
/// Options for <see cref="ScoreBoostReranker"/>. Deterministic, zero-dependency tuning.
/// </summary>
/// <param name="TagMatchBoost">Additive boost per distinct query term found among the record's tags.</param>
/// <param name="KeyTermBoost">Additive boost when a query key term appears in the record key.</param>
/// <param name="CategoryBoost">Additive boost when the record category equals the query category (ordinal, case-insensitive).</param>
/// <param name="ShortLengthPenalty">Penalty applied to texts below <see cref="MinLength"/>.</param>
/// <param name="LongLengthPenalty">Penalty applied to texts above <see cref="MaxLength"/>.</param>
/// <param name="MinLength">Texts shorter than this count as extremely short.</param>
/// <param name="MaxLength">Texts longer than this count as extremely long.</param>
public sealed record ScoreBoostRerankerOptions(
    double TagMatchBoost = 0.15,
    double KeyTermBoost = 0.10,
    double CategoryBoost = 0.05,
    double ShortLengthPenalty = 0.25,
    double LongLengthPenalty = 0.25,
    int MinLength = 8,
    int MaxLength = 4000);