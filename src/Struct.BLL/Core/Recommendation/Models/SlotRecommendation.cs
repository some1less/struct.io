namespace Struct.BLL.Core.Recommendation.Models;

public class SlotRecommendation
{
    public string Category { get; set; } = string.Empty;
    public decimal AllocatedBudget { get; set; }
    public List<RankedComponent> Recommendations { get; set; } = new();
}