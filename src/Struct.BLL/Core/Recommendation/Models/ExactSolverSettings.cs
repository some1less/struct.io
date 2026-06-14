namespace Struct.BLL.Core.Recommendation.Models;

/// <summary>Settings for <see cref="ExactBuildSolver"/>.</summary>
public class ExactSolverSettings
{
    /// <summary>How many top-scoring candidates per category the exact search considers.</summary>
    public int TopKPerCategory { get; init; } = 20;
}
