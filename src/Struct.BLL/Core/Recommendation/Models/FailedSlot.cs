namespace Struct.BLL.Core.Recommendation.Models;

/// <summary>
/// A category the engine could not fill, with the reason. Collected so partial builds report
/// every missing slot instead of only the last failure overwriting <see cref="RecommendationResult.Message"/>.
/// </summary>
public class FailedSlot
{
    public string Category { get; set; } = string.Empty;
    public string Reason { get; set; } = string.Empty;
}
