using Struct.DAL.Models;

namespace Struct.BLL.Core.Recommendation.Models;

public record RecommendationSettings
{
    public decimal MinBudget { get; init; }

    /// <summary>A pick may exceed its slot budget by this factor (still capped by the remaining total).</summary>
    public decimal SlotBudgetTolerance { get; init; }

    public Dictionary<Purpose, PurposeProfile> Profiles { get; init; } = new();
}

/// <summary>Sequence order is deliberate: CPU/GPU before PSU (TDP known), GPU before Case (length known).</summary>
public record PurposeProfile
{
    public List<Category> Sequence { get; init; } = new();
    public Dictionary<Category, double> Weights { get; init; } = new();
}
