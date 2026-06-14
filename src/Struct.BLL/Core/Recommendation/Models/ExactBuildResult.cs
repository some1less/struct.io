using Struct.BLL.Core.Compatibility;

namespace Struct.BLL.Core.Recommendation.Models;

/// <summary>Outcome of <see cref="ExactBuildSolver"/>: the optimal build found, its objective, and
/// whether every category was filled.</summary>
public class ExactBuildResult
{
    public BuildContext Build { get; init; } = new();
    public double Objective { get; init; }
    public bool IsComplete { get; init; }
}
