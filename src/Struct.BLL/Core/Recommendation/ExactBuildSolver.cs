using System.Diagnostics;
using Struct.BLL.Core.Compatibility;
using Struct.BLL.Core.Recommendation.Models;
using Struct.BLL.Core.Scoring;
using Struct.DAL.Models;

namespace Struct.BLL.Core.Recommendation;

/// <summary>
/// Branch-and-bound solver: finds the build maximizing <see cref="BuildObjective"/> subject to a
/// total budget and pairwise compatibility, over the top-K candidates per category. The reference
/// optimum the evaluation harness measures the other solvers against.
/// </summary>
public class ExactBuildSolver : IBuildSolver
{
    private readonly ICompatibilityEngine _compat;
    private readonly BuildObjective _objective;
    private readonly IPerformanceScorer _scorer;
    private readonly ExactSolverSettings _settings;

    public ExactBuildSolver(ICompatibilityEngine compat, BuildObjective objective,
        IPerformanceScorer scorer, ExactSolverSettings settings)
    {
        _compat = compat;
        _objective = objective;
        _scorer = scorer;
        _settings = settings;
    }

    public BuildSolution Solve(BuildProblem problem)
    {
        var (candidatesByCategory, profile, purpose, budget) = problem;
        var categories = profile.Sequence;
        var weights = profile.Weights;

        // Prune each category to its top-K by score (descending); record the best score per category.
        var pools = new Dictionary<Category, List<Component>>();
        var bestScore = new Dictionary<Category, double>();
        foreach (var cat in categories)
        {
            var list = (candidatesByCategory.TryGetValue(cat, out var raw) ? raw : Array.Empty<Component>())
                .OrderByDescending(c => _scorer.CalculateScore(c, purpose))
                .Take(_settings.TopKPerCategory)
                .ToList();
            pools[cat] = list;
            bestScore[cat] = list.Count > 0 ? _scorer.CalculateScore(list[0], purpose) : 0.0;
        }

        int n = categories.Count;
        // suffix[i] = optimistic Σ weight·bestScore for categories[i..]. Ignores the always-non-negative
        // bottleneck penalty, so it is a valid (admissible) upper bound on the true objective.
        var suffix = new double[n + 1];
        for (int i = n - 1; i >= 0; i--)
            suffix[i] = suffix[i + 1] + weights[categories[i]] * bestScore[categories[i]];

        var bestBuild = new BuildContext();
        int bestFilled = -1;
        double bestObjective = double.NegativeInfinity;
        var clock = Stopwatch.StartNew();
        bool timedOut = false;

        void Recurse(int idx, BuildContext partial, decimal spent, double weightedSoFar, int filled)
        {
            if (timedOut || clock.Elapsed >= _settings.TimeLimit)
            {
                timedOut = true;
                return;
            }

            // A missing slot outweighs any score: builds rank by filled slots first, objective second.
            int reachable = filled + n - idx;
            if (reachable < bestFilled) return;

            // Optimistic upper bound: even if every remaining slot scored its best and no penalty
            // applied, this branch could not beat the incumbent ⇒ prune.
            if (reachable == bestFilled && weightedSoFar + suffix[idx] <= bestObjective) return;

            if (idx == n)
            {
                double obj = _objective.Evaluate(partial, weights, purpose);
                if (filled > bestFilled || obj > bestObjective)
                {
                    bestFilled = filled;
                    bestObjective = obj;
                    bestBuild = partial.Clone();
                }
                return;
            }

            var cat = categories[idx];
            foreach (var candidate in pools[cat])
            {
                if (spent + candidate.Price > budget) continue;
                if (!_compat.CheckCompatibility(partial, candidate).IsCompatible) continue;

                var next = partial.Clone();
                next.Assign(candidate);
                Recurse(idx + 1, next, spent + candidate.Price,
                    weightedSoFar + weights[cat] * _scorer.CalculateScore(candidate, purpose), filled + 1);
            }

            // Leave this category empty: wins only when no build fills more slots, so an unaffordable
            // budget still returns the best partial build.
            Recurse(idx + 1, partial, spent, weightedSoFar, filled);
        }

        Recurse(0, new BuildContext(), 0m, 0.0, 0);
        return new BuildSolution
        {
            Build = bestBuild,
            TimedOut = timedOut,
            AllocatedBudgets = weights.ToDictionary(w => w.Key, w => budget * (decimal)w.Value),
            FailedSlots = categories.Where(c => bestBuild[c] == null)
                .Select(c => new FailedSlot
                {
                    Category = c.ToString(),
                    Reason = $"No compatible {c} fits within {budget:F2} PLN alongside the rest of the build."
                })
                .ToList()
        };
    }
}
