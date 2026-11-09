using Struct.BLL.Core.Recommendation.Models;
using Struct.DAL.Models;
using Struct.Tests.TestSupport;

namespace Struct.Tests;

public class BuildSolverTests
{
    private static double Objective(BuildProblem problem, BuildSolution solution) =>
        Engines.Objective().Evaluate(solution.Build, problem.Profile.Weights, problem.Purpose);

    [Fact]
    public void Exact_SingleOptionCatalog_ReturnsThatCompleteBuild_WithinBudget()
    {
        var problem = Engines.Problem(CatalogFactory.FullCoherentCatalog(), 12000m);

        var solution = Engines.Exact().Solve(problem);

        Assert.Empty(solution.FailedSlots);
        Assert.Equal(8, solution.Build.Components.Count());
        Assert.True(solution.TotalPrice <= problem.Budget);
    }

    [Fact]
    public void Exact_IsAtLeastAsGoodAs_GreedyLocalSearch_WhichIsAtLeastAsGoodAs_Greedy()
    {
        var problem = Engines.Problem(CatalogFactory.GreedyTrapCatalog(), 9000m);

        double greedy = Objective(problem, Engines.Greedy().Solve(problem));
        double local = Objective(problem, Engines.GreedyLocalSearch().Solve(problem));
        var exact = Engines.Exact().Solve(problem);

        Assert.Empty(exact.FailedSlots);
        Assert.True(Objective(problem, exact) >= local - 1e-9, $"exact {Objective(problem, exact)} < local {local}");
        Assert.True(local >= greedy - 1e-9, $"local {local} < greedy {greedy}");
    }

    [Fact]
    public void Greedy_LeavesBudgetUnspent_LocalSearchUpgradesWithIt()
    {
        var problem = Engines.Problem(CatalogFactory.UpgradableCatalog(), 9000m);

        var greedy = Engines.Greedy().Solve(problem);
        var local = Engines.GreedyLocalSearch().Solve(problem);

        Assert.Contains("4060", greedy.Build.Gpu!.Name);
        Assert.Contains("4090", local.Build.Gpu!.Name);
        Assert.True(Objective(problem, local) > Objective(problem, greedy));
        Assert.True(local.TotalPrice <= problem.Budget);
    }

    [Fact]
    public void Exact_ReportsEveryUnfillableSlot_WithNominalSlotBudgets()
    {
        var problem = Engines.Problem(CatalogFactory.FullCoherentCatalog(), 100m);

        var solution = Engines.Exact().Solve(problem);

        Assert.Equal(problem.Profile.Sequence.Select(c => c.ToString()), solution.FailedSlots.Select(f => f.Category));
        Assert.Equal(100m * (decimal)problem.Profile.Weights[Category.Gpu], solution.AllocatedBudgets[Category.Gpu]);
    }
}
