namespace Struct.BLL.Core.Recommendation.Models;

public record ExactSolverSettings
{
    /// <summary>How many top-scoring candidates per category the exact search considers.</summary>
    public int TopKPerCategory { get; init; }

    /// <summary>Past this the search stops and returns its incumbent, flagged <see cref="BuildSolution.TimedOut"/>.</summary>
    public TimeSpan TimeLimit { get; init; }

    /// <summary>Fails at startup on a time limit the search could not run with.</summary>
    public ExactSolverSettings Validated() => TimeLimit > TimeSpan.Zero
        ? this
        : throw new InvalidOperationException($"Engine:ExactSolver:TimeLimit must be positive, got {TimeLimit}.");
}
