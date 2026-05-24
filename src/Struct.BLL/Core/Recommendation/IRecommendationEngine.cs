using Struct.BLL.Core.Recommendation.Models;

namespace Struct.BLL.Core.Recommendation;

public interface IRecommendationEngine
{
    Task<RecommendationResult> GenerateRecommendationAsync(RecommendationRequest request);
}