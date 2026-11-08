using Struct.BLL.Core.Recommendation.Models;

namespace Struct.BLL.Core.Recommendation;

/// <summary>Picks one part per category of the profile: <see cref="GreedySolver"/> (baseline),
/// <see cref="GreedyLocalSearchSolver"/> (live API) or <see cref="ExactBuildSolver"/> (optimum, for evaluation).</summary>
public interface IBuildSolver
{
    BuildSolution Solve(BuildProblem problem);
}
