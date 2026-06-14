using Mapster;
using Struct.BLL.Core.Compatibility;
using Struct.BLL.Core.Recommendation.Models;
using Struct.BLL.Core.Scoring;
using Struct.BLL.DTOs;
using Struct.DAL.Models;
using Struct.DAL.Repositories.Interfaces;

namespace Struct.BLL.Core.Recommendation;

public class RecommendationEngine : IRecommendationEngine
{
    private readonly IComponentRepository _repository;
    private readonly ICompatibilityEngine _compatibilityEngine;
    private readonly IPerformanceScorer _scorer;
    private readonly BuildObjective _objective;

    public RecommendationEngine(IComponentRepository repository,
        ICompatibilityEngine compatibilityEngine, IPerformanceScorer scorer, BuildObjective objective)
    {
        _repository = repository;
        _compatibilityEngine = compatibilityEngine;
        _scorer = scorer;
        _objective = objective;
    }

    public async Task<RecommendationResult> GenerateRecommendationAsync(RecommendationRequest request)
    {
        var purpose = RecommendationProfiles.Weights.ContainsKey(request.Purpose) ? request.Purpose : "Gaming";
        var sequence = RecommendationProfiles.Sequences[purpose];
        var weights = new Dictionary<Category, double>(RecommendationProfiles.Weights[purpose]);

        var allocatedBudgets = weights.ToDictionary(k => k.Key, v => request.Budget * (decimal)v.Value);

        var result = new RecommendationResult { Purpose = purpose, TotalBudget = request.Budget, IsSuccess = true };
        var currentBuild = new BuildContext();
        var candidatesByCategory = new Dictionary<Category, List<Component>>();
        decimal actualTotal = 0;

        // ---- Greedy construction (baseline) ----
        foreach (var category in sequence)
        {
            decimal slotBudget = allocatedBudgets[category];

            var candidates = (await _repository.GetByCategoryAsync(category, 1, 5000)).ToList();
            candidatesByCategory[category] = candidates;

            decimal maxAllowedPrice = slotBudget * 1.10m;
            decimal remainingGlobalBudget = request.Budget - actualTotal;
            if (maxAllowedPrice > remainingGlobalBudget) maxAllowedPrice = remainingGlobalBudget;
            if (maxAllowedPrice < 0) maxAllowedPrice = 0;

            var compatible = candidates
                .Where(c => c.Price <= maxAllowedPrice)
                .Where(c => _compatibilityEngine.CheckCompatibility(currentBuild, c).IsCompatible)
                .ToList();

            if (!compatible.Any())
            {
                result.IsSuccess = false;
                result.FailedSlots.Add(new FailedSlot
                {
                    Category = category.ToString(),
                    Reason = $"Could not find a compatible {category} within {slotBudget:F2} PLN."
                });
                continue;
            }

            var best = compatible.OrderByDescending(c => _scorer.CalculateScore(c, purpose)).First();

            BuildContextOps.Assign(currentBuild, best);
            actualTotal += best.Price;

            decimal savings = slotBudget - best.Price;
            var remainingCategories = sequence.Skip(sequence.IndexOf(category) + 1).ToList();
            if (savings != 0 && remainingCategories.Any())
            {
                double remainingWeightSum = remainingCategories.Sum(c => weights[c]);
                if (remainingWeightSum > 0)
                    foreach (var remCat in remainingCategories)
                        allocatedBudgets[remCat] += savings * (decimal)(weights[remCat] / remainingWeightSum);
            }
        }

        // ---- Local-search upgrade pass (spends leftover budget + rebalances) ----
        RunUpgradePass(currentBuild, candidatesByCategory, sequence, weights, purpose, request.Budget, ref actualTotal);

        // ---- Build result slots from the final build (rank-1 only) ----
        foreach (var category in sequence)
        {
            var chosen = BuildContextOps.Get(currentBuild, category);
            if (chosen == null) continue;

            result.Slots.Add(new SlotRecommendation
            {
                Category = category.ToString(),
                AllocatedBudget = Math.Round(allocatedBudgets[category], 2, MidpointRounding.AwayFromZero),
                Recommendations = new List<RankedComponent>
                {
                    new RankedComponent
                    {
                        Rank = 1,
                        Component = chosen.Adapt<ComponentDto>(),
                        PerformanceScore = Math.Round(_scorer.CalculateScore(chosen, purpose), 3)
                    }
                }
            });
        }

        result.ActualTotalPrice = actualTotal;
        result.Message = result.IsSuccess
            ? "Build generated successfully."
            : $"Incomplete build: could not fill {string.Join(", ", result.FailedSlots.Select(f => f.Category))}.";

        return result;
    }

    private void RunUpgradePass(
        BuildContext build,
        Dictionary<Category, List<Component>> candidatesByCategory,
        List<Category> sequence,
        Dictionary<Category, double> weights,
        string purpose,
        decimal budget,
        ref decimal actualTotal)
    {
        bool improved = true;
        while (improved)
        {
            improved = false;
            double currentObjective = _objective.Evaluate(build, weights, purpose);
            double bestDelta = 0;
            Category bestCategory = default;
            Component? bestSwap = null;

            foreach (var category in sequence)
            {
                var chosen = BuildContextOps.Get(build, category);
                if (chosen == null) continue; // failed slot — nothing to upgrade
                if (!candidatesByCategory.TryGetValue(category, out var pool)) continue;

                foreach (var candidate in pool)
                {
                    if (candidate.Id == chosen.Id) continue;

                    decimal newTotal = actualTotal - chosen.Price + candidate.Price;
                    if (newTotal > budget) continue; // must stay within total budget

                    // Compatibility against the full build (CheckCompatibility clones then overwrites
                    // this category with the candidate, so the rest of the build is held fixed).
                    if (!_compatibilityEngine.CheckCompatibility(build, candidate).IsCompatible) continue;

                    var trial = BuildContextOps.Clone(build);
                    BuildContextOps.Assign(trial, candidate);
                    double delta = _objective.Evaluate(trial, weights, purpose) - currentObjective;

                    if (delta > bestDelta)
                    {
                        bestDelta = delta;
                        bestCategory = category;
                        bestSwap = candidate;
                    }
                }
            }

            if (bestSwap != null)
            {
                var old = BuildContextOps.Get(build, bestCategory)!;
                actualTotal = actualTotal - old.Price + bestSwap.Price;
                BuildContextOps.Assign(build, bestSwap);
                improved = true; // objective strictly increased; loop is bounded ⇒ terminates
            }
        }
    }

}