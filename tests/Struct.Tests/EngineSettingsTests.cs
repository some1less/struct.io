using Struct.BLL.Core;
using Struct.BLL.Core.Compatibility;
using Struct.BLL.Core.Recommendation;
using Struct.BLL.Core.Recommendation.Models;
using Struct.BLL.Core.Scoring;
using Struct.Tests.TestSupport;

namespace Struct.Tests;

public class EngineSettingsTests
{
    [Fact]
    public void ShippedProfiles_CoverEveryPurpose_WithWeightsSummingToOne()
    {
        var profiles = TestConfig.Recommendation.Profiles;
        foreach (var purpose in Enum.GetValues<Purpose>())
            Assert.Equal(1.0, profiles[purpose].Weights.Values.Sum(), precision: 9);
    }

    [Fact]
    public void ShippedProfiles_FillEveryWeightedCategoryExactlyOnce()
    {
        foreach (var profile in TestConfig.Recommendation.Profiles.Values)
            Assert.Equal(profile.Weights.Keys.Order(), profile.Sequence.Order());
    }

    [Fact]
    public async Task SlotBudgetTolerance_ComesFromSettings()
    {
        var engine = new RecommendationEngine(
            new StubComponentRepository(CatalogFactory.FullCoherentCatalog()),
            new CompatibilityEngine(TestConfig.Compatibility), new PerformanceScorer(),
            new BuildObjective(new PerformanceScorer(), TestConfig.Objective),
            TestConfig.Recommendation with { SlotBudgetTolerance = 0m });

        var result = await engine.GenerateRecommendationAsync(new RecommendationRequest { Budget = 12000 });

        Assert.Equal(8, result.FailedSlots.Count);
    }
}
