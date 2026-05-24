namespace Struct.BLL.Core.Recommendation.Models;

public class RecommendationResult
{
    public string Purpose { get; set; } = string.Empty;
    public decimal TotalBudget { get; set; }
    public decimal ActualTotalPrice { get; set; }
    public bool IsSuccess { get; set; }
    public string Message { get; set; } = string.Empty;
    public List<SlotRecommendation> Slots { get; set; } = new();
}