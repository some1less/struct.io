using Struct.BLL.Core.Compatibility;
using Struct.BLL.Core.Recommendation;
using Struct.BLL.Core.Recommendation.Models;
using Struct.BLL.Core.Scoring;
using Struct.DAL.Models;
using Struct.Tests.TestSupport;

namespace Struct.Tests;

/// <summary>
/// End-to-end characterization tests for the RecommendationEngine against a synthetic catalog
/// (Feedback - Recommendation Engine). Pins the correct behavior (full build, budget never
/// exceeded) and the known bugs (ActualTotalPrice = 0 on partial builds, invalid purpose silently
/// defaulting to Gaming) so Phase-2 fixes are deliberate.
/// </summary>
public class RecommendationEngineTests
{
    private static RecommendationEngine EngineFor(IEnumerable<Component> catalog) =>
        new(new StubComponentRepository(catalog), new CompatibilityEngine(), new PerformanceScorer());

    [Fact]
    public async Task FullCatalog_ProducesComplete8SlotBuild_WithinBudget()
    {
        var engine = EngineFor(CatalogFactory.FullCoherentCatalog());

        var result = await engine.GenerateRecommendationAsync(new RecommendationRequest { Budget = 12000, Purpose = "Gaming" });

        Assert.True(result.IsSuccess, result.Message);
        Assert.Equal(8, result.Slots.Count);
        Assert.True(result.ActualTotalPrice > 0);
        Assert.True(result.ActualTotalPrice <= result.TotalBudget,
            $"Total {result.ActualTotalPrice} exceeded budget {result.TotalBudget}");
    }

    [Fact]
    public async Task BudgetIsNeverExceeded_AcrossRangeOfBudgets()
    {
        var engine = EngineFor(CatalogFactory.FullCoherentCatalog());

        foreach (var budget in new[] { 6000m, 8000m, 12000m, 20000m })
        {
            var result = await engine.GenerateRecommendationAsync(new RecommendationRequest { Budget = budget, Purpose = "Gaming" });
            if (result.IsSuccess)
                Assert.True(result.ActualTotalPrice <= budget, $"Budget {budget}: spent {result.ActualTotalPrice}");
        }
    }

    [Fact]
    public async Task MissingCoolerCategory_FailsBuild_But_ActualTotalReflectsPickedComponents()
    {
        // Phase 2: a partial build must still report the price of the 7 components it did pick,
        // instead of the old silent ActualTotalPrice = 0.
        var catalog = CatalogFactory.WithoutCategory(Category.Cooler);
        var engine = EngineFor(catalog);

        var result = await engine.GenerateRecommendationAsync(new RecommendationRequest { Budget = 12000, Purpose = "Gaming" });

        Assert.False(result.IsSuccess);
        Assert.Equal(7, result.Slots.Count);                 // cooler slot still has no recommendation
        var expectedTotal = result.Slots.Sum(s => s.Recommendations.First().Component.Price);
        Assert.Equal(expectedTotal, result.ActualTotalPrice); // now reflects what was actually picked
        Assert.True(result.ActualTotalPrice > 0);
    }

    [Fact]
    public async Task FailedSlots_CaptureEveryFailedCategory_NotJustTheLast()
    {
        // Phase 2: with two unfillable slots, both must be reported. The old code only kept the
        // last failure in result.Message.
        var catalog = CatalogFactory.FullCoherentCatalog()
            .Where(c => c.Category != Category.Cooler && c.Category != Category.Ssd)
            .ToList();
        var engine = EngineFor(catalog);

        var result = await engine.GenerateRecommendationAsync(new RecommendationRequest { Budget = 12000, Purpose = "Gaming" });

        Assert.False(result.IsSuccess);
        var failedCategories = result.FailedSlots.Select(f => f.Category).ToList();
        Assert.Contains("Cooler", failedCategories);
        Assert.Contains("Ssd", failedCategories);
        Assert.All(result.FailedSlots, f => Assert.False(string.IsNullOrWhiteSpace(f.Reason)));
    }

    [Fact]
    public async Task DisplayedPerformanceScore_NeverExceedsOne()
    {
        // Phase 2: a maxed-out component scores a normalized 1.0; the internal budget-utilization
        // multiplier (up to x1.05) used for ranking must not leak into the displayed score.
        var catalog = CatalogFactory.FullCoherentCatalog()
            .Where(c => c.Category != Category.Ram)
            .Append(ComponentBuilder.New(Category.Ram, "G.Skill Trident Z5 128GB DDR5")
                .Brand("G.Skill").Price(800)
                .Spec("Type", "DDR5").Spec("Capacity", "128").Spec("Speed", "6000").Spec("Modules", "2"))
            .ToList();
        var engine = EngineFor(catalog);

        var result = await engine.GenerateRecommendationAsync(new RecommendationRequest { Budget = 12000, Purpose = "Gaming" });

        Assert.All(result.Slots, slot =>
            Assert.All(slot.Recommendations, r => Assert.True(r.PerformanceScore <= 1.0,
                $"{slot.Category} rank {r.Rank} score {r.PerformanceScore} exceeded 1.0")));
    }

    [Fact]
    public async Task InvalidPurpose_SilentlyDefaultsToGaming_KnownWart()
    {
        // Feedback: Purpose = "banana" should be a 400, but silently becomes Gaming today.
        var engine = EngineFor(CatalogFactory.FullCoherentCatalog());

        var result = await engine.GenerateRecommendationAsync(new RecommendationRequest { Budget = 12000, Purpose = "banana" });

        Assert.Equal("Gaming", result.Purpose);
    }
}
