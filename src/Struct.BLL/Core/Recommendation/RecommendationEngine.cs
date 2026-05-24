using Mapster;
using Struct.BLL.Core.Compatibility;
using Struct.BLL.Core.Recommendation.Models;
using Struct.BLL.Core.Scoring;
using Struct.DAL.Models;
using Struct.DAL.Repositories.Interfaces;

namespace Struct.BLL.Core.Recommendation;

public class RecommendationEngine : IRecommendationEngine
{
    private readonly IComponentRepository _repository;
    private readonly ICompatibilityEngine _compatibilityEngine;
    private readonly IPerformanceScorer _scorer;

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
        ICompatibilityEngine compatibilityEngine, IPerformanceScorer scorer)
    {
        _repository = repository;
        _compatibilityEngine = compatibilityEngine;
        _scorer = scorer;
    }

    public async Task<RecommendationResult> GenerateRecommendationAsync(RecommendationRequest request)
    {
        var purpose = BaseWeights.ContainsKey(request.Purpose) ? request.Purpose : "Gaming";
        var sequence = Sequences[purpose];
        var weights = new Dictionary<Category, double>(BaseWeights[purpose]);
        
        var allocatedBudgets = weights.
            ToDictionary(k => k.Key, 
                v => request.Budget * (decimal)v.Value);
        
        var result = new RecommendationResult { Purpose = purpose, TotalBudget = request.Budget, IsSuccess = true };
        var currentBuild = new BuildContext();
        decimal actualTotal = 0;

        foreach (var category in sequence)
        {
            decimal slotBudget = allocatedBudgets[category];
            
             /* pulling up data + putting pageSize up to 5000 (performance heavy operation)
              TODO: think about optimization
              */
            var candidates = await _repository.GetByCategoryAsync(category, 1, 5000);

            // 1. money filter
            var affordable = candidates.Where(c => c.Price <= slotBudget).ToList();

            // 2. compatibility filter
            var compatible = affordable
                .Where(c => _compatibilityEngine.CheckCompatibility(currentBuild, c).IsCompatible)
                .ToList();

            if (!compatible.Any())
            {
                result.IsSuccess = false;
                result.Message = $"Could not find a compatible {category} within {slotBudget:F2} PLN.";
                break; // Зупиняємо підбір, бо збірка неможлива
            }

            // 3. scoring and sorting 
            var ranked = compatible
                .Select(c => new { Component = c, Score = _scorer.CalculateScore(c, purpose) })
                .OrderByDescending(x => x.Score)
                .Take(2) // Беремо ТОП-2
                .ToList();

            var bestComponent = ranked.First().Component;

            // adding the beast variant to build-in memory current build
            AssignToContext(currentBuild, bestComponent);
            actualTotal += bestComponent.Price;

            // 4. packing result
            var slotRec = new SlotRecommendation
            {
                Category = category.ToString(),
                AllocatedBudget = slotBudget,
                Recommendations = ranked.Select((r, index) => new RankedComponent
                {
                    Rank = index + 1,
                    Component = r.Component.Adapt<Struct.BLL.DTOs.ComponentDto>(), // Використовуємо Mapster!
                    PerformanceScore = Math.Round(r.Score, 3)
                }).ToList()
            };
            result.Slots.Add(slotRec);

            // 5. dynamic budget handling (WSA Magic)
            decimal savings = slotBudget - bestComponent.Price;
            var remainingCategories = sequence.Skip(sequence.IndexOf(category) + 1).ToList();

            if (savings > 0 && remainingCategories.Any())
            {
                double remainingWeightSum = remainingCategories.Sum(c => weights[c]);
                foreach (var remCat in remainingCategories)
                {
                    // giving rest money to others 
                    decimal addedBudget = savings * (decimal)(weights[remCat] / remainingWeightSum);
                    allocatedBudgets[remCat] += addedBudget;
                }
            }
        }

        if (result.IsSuccess)
        {
            result.ActualTotalPrice = actualTotal;
            result.Message = "Build generated successfully.";
        }

        return result;
    }

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