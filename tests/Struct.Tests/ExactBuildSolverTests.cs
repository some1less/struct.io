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

    [Fact]
    public async Task ExactSolver_IsAtLeastAsGoodAs_GreedyPlusLocalSearch()
    {
        var scorer = new PerformanceScorer();
        var objective = new BuildObjective(scorer, new ObjectiveSettings());
        var weights = RecommendationProfiles.Weights["Gaming"];

        // Engine (greedy + local-search), re-scored with the same objective. Independent catalog
        // instance so the solver's id assignment doesn't mutate the engine's parts.
        var engineCatalog = CatalogFactory.GreedyTrapCatalog();
        var engine = new RecommendationEngine(new StubComponentRepository(engineCatalog),
            new CompatibilityEngine(), scorer, objective);
        var engineResult = await engine.GenerateRecommendationAsync(
            new RecommendationRequest { Budget = 9000m, Purpose = "Gaming" });
        var engineBuild = BuildFromSlots(engineResult, engineCatalog);
        double engineObjective = objective.Evaluate(engineBuild, weights, "Gaming");

        // Exact solver on its own catalog instance.
        var exact = new ExactBuildSolver(new CompatibilityEngine(), objective, scorer, new ExactSolverSettings())
            .Solve(Pools(CatalogFactory.GreedyTrapCatalog()),
                   RecommendationProfiles.Sequences["Gaming"], weights, "Gaming", 9000m);

        Assert.True(exact.IsComplete);
        Assert.True(exact.Objective >= engineObjective - 1e-9,
            $"exact {exact.Objective} should be >= engine {engineObjective}");
    }

    private static BuildContext BuildFromSlots(RecommendationResult result, List<Component> catalog)
    {
        var ctx = new BuildContext();
        foreach (var slot in result.Slots)
        {
            var comp = catalog.First(c => c.Name == slot.Recommendations.First().Component.Name);
            BuildContextOps.Assign(ctx, comp);
        }
        return ctx;
    }
}
