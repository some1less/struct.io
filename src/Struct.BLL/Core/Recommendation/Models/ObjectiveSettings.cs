namespace Struct.BLL.Core.Recommendation.Models;

/// <summary>
/// Tunable parameters for <see cref="BuildObjective"/>. Centralized so the thesis has a single
/// weights/parameters table and the values are easy to sweep during evaluation.
/// </summary>
public class ObjectiveSettings
{
    /// <summary>λ — how hard CPU↔GPU imbalance is penalized.</summary>
    public double BottleneckWeight { get; init; } = 0.5;

    /// <summary>Allowed |cpuScore − gpuScore| gap before any penalty applies.</summary>
    public double BottleneckThreshold { get; init; } = 0.15;
}
