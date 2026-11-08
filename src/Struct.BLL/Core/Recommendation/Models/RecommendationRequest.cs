namespace Struct.BLL.Core.Recommendation.Models;

public class RecommendationRequest
{
    public decimal Budget { get; set; }
    
    /* Gaming (default), Work, Office*/
    public Purpose Purpose { get; set; } = Purpose.Gaming;
}