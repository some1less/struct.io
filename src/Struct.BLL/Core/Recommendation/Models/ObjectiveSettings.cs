namespace Struct.BLL.Core.Recommendation.Models;

public record ObjectiveSettings
{
    /// <summary>λ — how hard CPU↔GPU imbalance is penalized.</summary>
    public double BottleneckWeight { get; init; }

    /// <summary>Allowed |cpuScore − gpuScore| gap before any penalty applies.</summary>
    public double BottleneckThreshold { get; init; }
}
