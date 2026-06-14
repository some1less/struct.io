using Struct.BLL.Core.Compatibility;
using Struct.BLL.Core.Recommendation;
using Struct.BLL.Core.Recommendation.Models;
using Struct.BLL.Core.Scoring;
using Struct.DAL.Models;
using Struct.Tests.TestSupport;

namespace Struct.Tests;

public class ExactBuildSolverTests
{
    private static ExactBuildSolver Solver() =>
        new(new CompatibilityEngine(), new BuildObjective(new PerformanceScorer(), new ObjectiveSettings()),
            new PerformanceScorer(), new ExactSolverSettings());

    private static Dictionary<Category, IReadOnlyList<Component>> Pools(IEnumerable<Component> catalog)
    {
        int id = 1;
        var list = catalog.ToList();
        foreach (var c in list) c.Id = id++;
        return list.GroupBy(c => c.Category)
                   .ToDictionary(g => g.Key, g => (IReadOnlyList<Component>)g.ToList());
    }

    [Fact]
    public void SingleOptionCatalog_ReturnsThatCompleteBuild_WithinBudget()
    {
        var result = Solver().Solve(
            Pools(CatalogFactory.FullCoherentCatalog()),
            RecommendationProfiles.Sequences["Gaming"],
            RecommendationProfiles.Weights["Gaming"],
            "Gaming", budget: 12000m);

        Assert.True(result.IsComplete);
        Assert.NotNull(result.Build.Cpu);
        Assert.NotNull(result.Build.Gpu);
    }
}
