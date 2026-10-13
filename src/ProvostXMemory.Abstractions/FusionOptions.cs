namespace ProvostXMemory.Abstractions;

/// <summary>Fusion options: RRF constant and stage weights.</summary>
public sealed record FusionOptions(
    double RrfK = 60.0,
    double DenseWeight = 1.0,
    double LexicalWeight = 1.0);
