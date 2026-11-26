namespace Struct.BLL.Core.Recommendation.Models;

public record ExactSolverSettings
{
    /// <summary>How many top-scoring candidates per category the exact search considers.</summary>
    public int TopKPerCategory { get; init; }

    /// <summary>Past this the search stops and returns its incumbent, flagged <see cref="BuildSolution.TimedOut"/>.</summary>
    public TimeSpan TimeLimit { get; init; }
}
