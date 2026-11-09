namespace Struct.BLL.Core.Recommendation.Models;

public class SlotRecommendation
{
    public string Category { get; set; } = string.Empty;
    public decimal AllocatedBudget { get; set; }

    /// <summary>The chosen part's price as a fraction of the build's total.</summary>
    public decimal PriceShare { get; set; }
    public List<RankedComponent> Recommendations { get; set; } = new();
}