using Struct.BLL.Core.Compatibility;
using Struct.BLL.Core.Recommendation.Models;
using Struct.DAL.Models;

namespace Struct.BLL.Core.Recommendation;

/// <summary>
/// Greedy construction, then an upgrade pass: repeatedly applies the single slot swap that most increases
/// <see cref="BuildObjective"/> while the build stays within budget and compatible. Spends what greedy left over.
/// </summary>
public class GreedyLocalSearchSolver : IBuildSolver
{
    private readonly GreedySolver _greedy;
    private readonly ICompatibilityEngine _compatibilityEngine;
    private readonly BuildObjective _objective;

    public GreedyLocalSearchSolver(GreedySolver greedy, ICompatibilityEngine compatibilityEngine, BuildObjective objective)
    {
        _greedy = greedy;
        _compatibilityEngine = compatibilityEngine;
        _objective = objective;
    }

    public BuildSolution Solve(BuildProblem problem)
    {
        var solution = _greedy.Solve(problem);
        var build = solution.Build;
        var weights = problem.Profile.Weights;
        decimal total = solution.TotalPrice;

        bool improved = true;
        while (improved)
        {
            improved = false;
            double currentObjective = _objective.Evaluate(build, weights, problem.Purpose);
            double bestDelta = 0;
            Component? bestSwap = null;

            foreach (var category in problem.Profile.Sequence)
            {
                var chosen = build[category];
                if (chosen == null) continue; // failed slot — nothing to upgrade

                foreach (var candidate in problem.Candidates[category])
                {
                    if (candidate.Id == chosen.Id) continue;
                    if (total - chosen.Price + candidate.Price > problem.Budget) continue;

                    // CheckCompatibility clones the build and overwrites this slot, so the rest stays fixed.
                    if (!_compatibilityEngine.CheckCompatibility(build, candidate).IsCompatible) continue;

                    var trial = build.Clone();
                    trial.Assign(candidate);
                    double delta = _objective.Evaluate(trial, weights, problem.Purpose) - currentObjective;

                    if (delta > bestDelta)
                    {
                        bestDelta = delta;
                        bestSwap = candidate;
                    }
                }
            }

            if (bestSwap != null)
            {
                total = total - build[bestSwap.Category]!.Price + bestSwap.Price;
                build.Assign(bestSwap);
                improved = true; // objective strictly increased; loop is bounded ⇒ terminates
            }
        }

        return solution;
    }
}
