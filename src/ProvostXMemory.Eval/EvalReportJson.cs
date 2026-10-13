using System;
using System.Collections.Generic;
using System.Linq;

namespace ProvostXMemory.Eval;


/// <summary>JSON (de)serialization for <see cref="EvalReport"/> (System.Text.Json).</summary>
public sealed class EvalReportJson
{
    private static readonly System.Text.Json.JsonSerializerOptions Options =
        new() // pure factory; immutable after creation, no shared mutable state
        {
            WriteIndented = true,
            PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.SnakeCaseLower,
        };

    /// <summary>Serializes the report to indented snake_case JSON.</summary>
    public string Write(EvalReport report)
    {
        ArgumentNullException.ThrowIfNull(report);
        return System.Text.Json.JsonSerializer.Serialize(report, Options);
    }

    /// <summary>Deserializes a report; null when the payload is null.</summary>
    public EvalReport? Read(string json)
    {
        ArgumentNullException.ThrowIfNull(json);
        return System.Text.Json.JsonSerializer.Deserialize<EvalReport>(json, Options);
    }
}
