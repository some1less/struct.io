using Struct.BLL.Core;
using Struct.BLL.Core.Compatibility;
using Struct.BLL.Core.Recommendation;
using Struct.BLL.Core.Recommendation.Models;
using Struct.BLL.Core.Scoring;
using Struct.DAL.Models;

namespace Struct.Tests.TestSupport;

/// <summary>The engines wired as in Program.cs, with the shipped appsettings.json.</summary>
public static class Engines
{
    private static readonly PerformanceScorer Scorer = new();

    private static CompatibilityEngine Compat() => new(TestConfig.Compatibility);

    public static BuildObjective Objective() => new(Scorer, TestConfig.Objective);

    public static GreedySolver Greedy(RecommendationSettings? settings = null) =>
        new(Compat(), Scorer, settings ?? TestConfig.Recommendation);

    public static GreedyLocalSearchSolver GreedyLocalSearch(RecommendationSettings? settings = null) =>
        new(Greedy(settings), Compat(), Objective());

    public static ExactBuildSolver Exact(ExactSolverSettings? settings = null) =>
        new(Compat(), Objective(), Scorer, settings ?? TestConfig.ExactSolver);

    public static RecommendationEngine Recommendation(IEnumerable<Component> catalog, RecommendationSettings? settings = null) =>
        new(new StubComponentRepository(catalog), GreedyLocalSearch(settings), Scorer, settings ?? TestConfig.Recommendation);

    /// <summary>Candidates per sequence category with unique ids, as the engine loads them from the repository.</summary>
    public static BuildProblem Problem(IEnumerable<Component> catalog, decimal budget, Purpose purpose = Purpose.Gaming)
    {
        var parts = catalog.ToList();
        for (int i = 0; i < parts.Count; i++) parts[i].Id = i + 1;
        var profile = TestConfig.Recommendation.Profiles[purpose];
        var candidates = profile.Sequence.ToDictionary(c => c,
            c => (IReadOnlyList<Component>)parts.Where(p => p.Category == c).ToList());
        return new BuildProblem(candidates, profile, purpose, budget);
    }
}
