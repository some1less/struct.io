using Struct.BLL.Core.Compatibility;
using Struct.BLL.Core.Recommendation.Models;
using Struct.BLL.Core.Scoring;
using Struct.DAL.Models;

namespace Struct.BLL.Core.Recommendation;

/// <summary>
/// Branch-and-bound solver: finds the build maximizing <see cref="BuildObjective"/> subject to a
/// total budget and pairwise compatibility, over the top-K candidates per category. An evaluation
/// tool to benchmark the greedy+local-search engine; not wired into the live API.
/// </summary>
public class ExactBuildSolver
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

    public ExactBuildResult Solve(
        IReadOnlyDictionary<Category, IReadOnlyList<Component>> candidatesByCategory,
        IReadOnlyList<Category> categories,
        IReadOnlyDictionary<Category, double> weights,
        string purpose,
        decimal budget)
    {
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

        var best = new ExactBuildResult { Build = new BuildContext(), Objective = double.NegativeInfinity, IsComplete = false };

        void Recurse(int idx, BuildContext partial, decimal spent, double weightedSoFar)
        {
            if (idx == n)
            {
                double obj = _objective.Evaluate(partial, weights, purpose);
                if (obj > best.Objective)
                    best = new ExactBuildResult
                    {
                        Build = BuildContextOps.Clone(partial),
                        Objective = obj,
                        IsComplete = IsComplete(partial, categories)
                    };
                return;
            }

            // Optimistic upper bound: even if every remaining slot scored its best and no penalty
            // applied, this branch could not beat the incumbent ⇒ prune.
            if (weightedSoFar + suffix[idx] <= best.Objective) return;

            var cat = categories[idx];
            foreach (var candidate in pools[cat])
            {
                if (spent + candidate.Price > budget) continue;
                if (!_compat.CheckCompatibility(partial, candidate).IsCompatible) continue;

                var next = BuildContextOps.Clone(partial);
                BuildContextOps.Assign(next, candidate);
                Recurse(idx + 1, next, spent + candidate.Price,
                    weightedSoFar + weights[cat] * _scorer.CalculateScore(candidate, purpose));
            }

            // Leave this category empty: only ever wins when no candidate is feasible, keeping the
            // solver robust enough to return the best partial build when a full build is impossible.
            Recurse(idx + 1, partial, spent, weightedSoFar);
        }

        Recurse(0, new BuildContext(), 0m, 0.0);
        return best;
    }

    private static bool IsComplete(BuildContext b, IReadOnlyList<Category> categories) =>
        categories.All(c => BuildContextOps.Get(b, c) != null);
}
