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
    private readonly IBuildSolver? _seed;

    /// <param name="seed">Its build is the first incumbent, so the search only has to prove it optimal or beat it.</param>
    public ExactBuildSolver(ICompatibilityEngine compat, BuildObjective objective,
        IPerformanceScorer scorer, ExactSolverSettings settings, IBuildSolver? seed = null)
    {
        _compat = compat;
        _objective = objective;
        _scorer = scorer;
        _settings = settings;
        _seed = seed;
    }

    public BuildSolution Solve(BuildProblem problem)
    {
        var (candidatesByCategory, profile, purpose, budget) = problem;
        // CPU and GPU first: once both are decided the bottleneck penalty is fixed, so the bound can count it.
        var categories = profile.Sequence.OrderBy(c => c is Category.Cpu or Category.Gpu ? 0 : 1).ToList();
        int penaltyKnownAt = categories.Count(c => c is Category.Cpu or Category.Gpu);
        var weights = profile.Weights;
        int n = categories.Count;

        // Each category's top-K by score, best first, and its cheapest price.
        var pools = new List<(Component Part, double Score)>[n];
        var minPrice = new decimal[n];
        for (int i = 0; i < n; i++)
        {
            pools[i] = (candidatesByCategory.TryGetValue(categories[i], out var raw) ? raw : Array.Empty<Component>())
                .Select(c => (Part: c, Score: _scorer.CalculateScore(c, purpose)))
                .OrderByDescending(p => p.Score)
                .Take(_settings.TopKPerCategory)
                .ToList();
            minPrice[i] = pools[i].Count > 0 ? pools[i].Min(p => p.Part.Price) : 0m;
        }
        var minSuffix = new decimal[n + 1];
        for (int i = n - 1; i >= 0; i--) minSuffix[i] = minSuffix[i + 1] + minPrice[i];

        var bestBuild = new BuildContext();
        int bestFilled = -1;
        double bestObjective = double.NegativeInfinity;
        if (_seed != null)
        {
            bestBuild = _seed.Solve(problem).Build;
            bestFilled = bestBuild.Components.Count();
            bestObjective = _objective.Evaluate(bestBuild, weights, purpose);
        }
        var clock = Stopwatch.StartNew();
        bool timedOut = false;

        // Optimistic Σ weight·score over categories[idx..]: per slot the best part the money left can still buy, and
        // no bottleneck penalty (never negative), so no real completion of the branch can score higher.
        double Bound(int idx, decimal spent)
        {
            // Against a complete incumbent a branch only counts if it fills every remaining slot, so each part
            // must also leave room for the cheapest part of every other remaining slot.
            bool fillAll = bestFilled == n;
            double bound = 0;
            for (int i = idx; i < n; i++)
            {
                decimal cap = budget - spent - (fillAll ? minSuffix[idx] - minPrice[i] : 0m);
                foreach (var (part, score) in pools[i])
                {
                    if (part.Price > cap) continue;
                    bound += weights[categories[i]] * score;
                    break;
                }
            }
            return bound;
        }

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
            if (reachable == bestFilled)
            {
                double soFar = idx >= penaltyKnownAt ? _objective.Evaluate(partial, weights, purpose) : weightedSoFar;
                if (soFar + Bound(idx, spent) <= bestObjective) return;
            }

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
            foreach (var (candidate, score) in pools[idx])
            {
                if (spent + candidate.Price > budget) continue;
                if (!_compat.CheckCompatibility(partial, candidate).IsCompatible) continue;

                var next = partial.Clone();
                next.Assign(candidate);
                Recurse(idx + 1, next, spent + candidate.Price, weightedSoFar + weights[cat] * score, filled + 1);
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
            FailedSlots = profile.Sequence.Where(c => bestBuild[c] == null)
                .Select(c => new FailedSlot
                {
                    Category = c.ToString(),
                    Reason = timedOut && candidatesByCategory.GetValueOrDefault(c)?.Count > 0
                        ? $"The search stopped at its time limit before it found a {c} that fits."
                        : $"No compatible {c} fits within {budget:F2} PLN alongside the rest of the build."
                })
                .ToList()
        };
    }
}
