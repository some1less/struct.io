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

    // division matrix
    /* GAMING: GPU(35%) CPU(22%) Motherboard(12%) ...
       WORK: CPU(35%) GPU(15%) RAM(18%) ...
       OFFICE: CPU(25%) Motherboard(15%) RAM(15%) ... gpu minimum
     
     */
    private static readonly Dictionary<string, Dictionary<Category, double>> BaseWeights = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Gaming"] = new()
        {
            { Category.Gpu, 0.35 }, { Category.Cpu, 0.22 }, { Category.Motherboard, 0.12 },
            { Category.Ram, 0.10 }, { Category.Ssd, 0.07 }, { Category.Psu, 0.06 },
            { Category.Case, 0.05 }, { Category.Cooler, 0.03 }
        },
        ["Work"] = new()
        {
            { Category.Cpu, 0.35 }, { Category.Gpu, 0.15 }, { Category.Ram, 0.18 },
            { Category.Motherboard, 0.12 }, { Category.Ssd, 0.08 }, { Category.Psu, 0.05 },
            { Category.Case, 0.04 }, { Category.Cooler, 0.03 }
        },
        ["Office"] = new()
        {
            { Category.Cpu, 0.25 }, { Category.Motherboard, 0.15 }, { Category.Ram, 0.15 },
            { Category.Ssd, 0.15 }, { Category.Gpu, 0.10 }, { Category.Psu, 0.08 },
            { Category.Case, 0.07 }, { Category.Cooler, 0.05 }
        }
    };

    // order of placing
    private static readonly Dictionary<string, List<Category>> Sequences = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Gaming"] = new() { Category.Gpu, Category.Cpu, Category.Motherboard, 
            Category.Ram, Category.Psu, Category.Ssd, 
            Category.Case, Category.Cooler },
        
        ["Work"] = new() { Category.Cpu, Category.Gpu, Category.Motherboard, 
            Category.Ram, Category.Psu, Category.Ssd, 
            Category.Case, Category.Cooler },
        
        ["Office"] = new() { Category.Cpu, Category.Motherboard, Category.Ram, 
            Category.Ssd, Category.Psu, Category.Case, 
            Category.Cooler, Category.Gpu }
    };

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
        var purpose = BaseWeights.ContainsKey(request.Purpose) ? request.Purpose : "Gaming";
        var sequence = Sequences[purpose];
        var weights = new Dictionary<Category, double>(BaseWeights[purpose]);

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

            AssignToContext(currentBuild, best);
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
            var chosen = GetComponent(currentBuild, category);
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
        // Implemented in Task 3.
    }

    private static BuildContext CloneBuild(BuildContext b) => new()
    {
        Cpu = b.Cpu, Gpu = b.Gpu, Motherboard = b.Motherboard, Ram = b.Ram,
        Psu = b.Psu, Case = b.Case, Cooler = b.Cooler, Storage = b.Storage
    };

    private static Component? GetComponent(BuildContext b, Category c) => c switch
    {
        Category.Cpu => b.Cpu,
        Category.Gpu => b.Gpu,
        Category.Motherboard => b.Motherboard,
        Category.Ram => b.Ram,
        Category.Psu => b.Psu,
        Category.Case => b.Case,
        Category.Cooler => b.Cooler,
        Category.Ssd or Category.Hdd => b.Storage,
        _ => null
    };

    private void AssignToContext(BuildContext context, Component candidate)
    {
        switch (candidate.Category)
        {
            case Category.Cpu: context.Cpu = candidate; break;
            case Category.Gpu: context.Gpu = candidate; break;
            case Category.Motherboard: context.Motherboard = candidate; break;
            case Category.Ram: context.Ram = candidate; break;
            case Category.Psu: context.Psu = candidate; break;
            case Category.Case: context.Case = candidate; break;
            case Category.Cooler: context.Cooler = candidate; break;
            case Category.Ssd:
            case Category.Hdd: context.Storage = candidate; break;
        }
    }
}