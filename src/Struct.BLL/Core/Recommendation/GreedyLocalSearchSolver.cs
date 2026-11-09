using Struct.BLL.Core.Compatibility;
using Struct.BLL.Core.Recommendation.Models;
using Struct.DAL.Models;

namespace Struct.BLL.Core.Recommendation;

/// <summary>
/// Greedy construction, then an upgrade pass: repeatedly applies the slot swap that raises <see cref="BuildObjective"/>
/// most per extra złoty while the build stays within budget and compatible. A swap that costs nothing extra is a pure
/// win and goes first. Spends what greedy left over where it buys the most, not on one pricey part with a tiny gain.
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
            (int Free, double Gain) bestRank = default;
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
                    if (delta <= 0) continue;

                    decimal extra = candidate.Price - chosen.Price;
                    var rank = extra <= 0 ? (1, delta) : (0, delta / (double)extra);
                    if (rank.CompareTo(bestRank) > 0)
                    {
                        bestRank = rank;
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
