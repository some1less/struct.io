using Struct.BLL.Core.Compatibility;
using Struct.BLL.Core.Recommendation.Models;
using Struct.BLL.Core.Scoring;

namespace Struct.BLL.Core.Recommendation;

/// <summary>
/// Baseline: fills slots in the profile's sequence, each with the best-scoring compatible part within its
/// slot budget (weight share × tolerance, capped by what is left of the total). A slot's leftover or
/// overspend cascades onto the remaining slots in proportion to their weights.
/// </summary>
public class GreedySolver : IBuildSolver
{
    private readonly ICompatibilityEngine _compatibilityEngine;
    private readonly IPerformanceScorer _scorer;
    private readonly RecommendationSettings _settings;

    public GreedySolver(ICompatibilityEngine compatibilityEngine, IPerformanceScorer scorer, RecommendationSettings settings)
    {
        _compatibilityEngine = compatibilityEngine;
        _scorer = scorer;
        _settings = settings;
    }

    public BuildSolution Solve(BuildProblem problem)
    {
        var sequence = problem.Profile.Sequence;
        var weights = problem.Profile.Weights;
        var solution = new BuildSolution
        {
            AllocatedBudgets = weights.ToDictionary(w => w.Key, w => problem.Budget * (decimal)w.Value)
        };
        var budgets = solution.AllocatedBudgets;
        decimal spent = 0;

        for (int i = 0; i < sequence.Count; i++)
        {
            var category = sequence[i];
            decimal slotBudget = budgets[category];
            decimal maxPrice = Math.Max(0, Math.Min(slotBudget * _settings.SlotBudgetTolerance, problem.Budget - spent));

            var best = problem.Candidates[category]
                .Where(c => c.Price <= maxPrice)
                .Where(c => _compatibilityEngine.CheckCompatibility(solution.Build, c).IsCompatible)
                .OrderByDescending(c => _scorer.CalculateScore(c, problem.Purpose))
                .FirstOrDefault();

            if (best == null)
            {
                solution.FailedSlots.Add(new FailedSlot
                {
                    Category = category.ToString(),
                    Reason = $"Could not find a compatible {category} within {slotBudget:F2} PLN."
                });
                continue;
            }

            solution.Build.Assign(best);
            spent += best.Price;

            var remaining = sequence.Skip(i + 1).ToList();
            double remainingWeight = remaining.Sum(c => weights[c]);
            if (remainingWeight <= 0) continue;
            foreach (var next in remaining)
                budgets[next] += (slotBudget - best.Price) * (decimal)(weights[next] / remainingWeight);
        }

        return solution;
    }
}
