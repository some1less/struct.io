namespace Struct.BLL.Core.Recommendation.Models;

public record ExactSolverSettings
{
    /// <summary>How many top-scoring candidates per category the exact search considers.</summary>
    public int TopKPerCategory { get; init; }
}
