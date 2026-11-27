namespace Struct.BLL.Core.Recommendation.Models;

public record ExactSolverSettings
{
    /// <summary>How many top-scoring candidates per category the exact search considers.</summary>
    public int TopKPerCategory { get; init; }

    /// <summary>Past this the search stops and returns its incumbent, flagged <see cref="BuildSolution.TimedOut"/>.</summary>
    public TimeSpan TimeLimit { get; init; }

    /// <summary>Fails at startup on settings the search could not run with (a missing key binds to 0).</summary>
    public ExactSolverSettings Validated() => TimeLimit > TimeSpan.Zero && TopKPerCategory > 0
        ? this
        : throw new InvalidOperationException(
            $"Engine:ExactSolver needs a positive TimeLimit and TopKPerCategory, got {TimeLimit} and {TopKPerCategory}.");
}
