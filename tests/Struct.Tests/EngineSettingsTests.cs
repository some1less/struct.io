using Struct.BLL.Core;
using Struct.BLL.Core.Recommendation.Models;
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
    public void ExactSolver_ShippedSettings_PassValidation()
    {
        var shipped = TestConfig.ExactSolver;

        Assert.Same(shipped, shipped.Validated());
    }

    [Theory]
    [InlineData(0, 20)]
    [InlineData(-1, 20)]
    [InlineData(10, 0)]
    public void ExactSolver_RejectsANonPositiveTimeLimitOrTopK(int seconds, int topK)
    {
        var settings = TestConfig.ExactSolver with { TimeLimit = TimeSpan.FromSeconds(seconds), TopKPerCategory = topK };

        Assert.Throws<InvalidOperationException>(() => settings.Validated());
    }

    [Fact]
    public void SlotBudgetTolerance_ComesFromSettings()
    {
        // Greedy alone: local search fills what the slot caps leave empty.
        var greedy = Engines.Greedy(TestConfig.Recommendation with { SlotBudgetTolerance = 0m });

        var solution = greedy.Solve(Engines.Problem(CatalogFactory.FullCoherentCatalog(), 12000m));

        Assert.Equal(8, solution.FailedSlots.Count);
    }
}
