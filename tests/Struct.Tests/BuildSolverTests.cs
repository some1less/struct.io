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
        Assert.False(solution.TimedOut);
    }

    [Fact]
    public void Exact_PrefersACompleteBuild_OverAHigherScoringPartialOne()
    {
        // At 8500 the 4090 only fits if the case is dropped; a build without a case is not a PC.
        var problem = Engines.Problem(CatalogFactory.UpgradableCatalog(), 8500m);

        var solution = Engines.Exact().Solve(problem);

        Assert.Empty(solution.FailedSlots);
        Assert.Contains("4060", solution.Build.Gpu!.Name);
    }

    [Fact]
    public void Exact_PastItsTimeLimit_StopsAndFlagsTheSolution()
    {
        var problem = Engines.Problem(CatalogFactory.FullCoherentCatalog(), 12000m);

        var solution = Engines.Exact(TestConfig.ExactSolver with { TimeLimit = TimeSpan.Zero }).Solve(problem);

        Assert.True(solution.TimedOut);
        Assert.Empty(solution.Build.Components);
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
    public void LocalSearch_SpendsTheLeftoverWhereItBuysMostPerZloty_NotOnAnExpensiveSsd()
    {
        // Greedy leaves 2750 unspent. A 4 TB SSD for all of it gains 0.010; RAM (+1300) and board (+1200)
        // upgrades gain 0.008 + 0.006 together. Ranked by Δobjective alone, the SSD is bought and nothing else fits.
        var catalog = CatalogFactory.FullCoherentCatalog();
        catalog.AddRange([
            ComponentBuilder.New(Category.Ssd, "Samsung 990 Pro 4TB").Price(3150)
                .Spec("Capacity", "4 TB").Spec("Interface", "NVMe"),
            ComponentBuilder.New(Category.Ram, "Corsair Vengeance 64GB DDR5").Price(1800)
                .Spec("Type", "DDR5").Spec("Capacity", "64").Spec("Speed", "6000").Spec("Modules", "2"),
            ComponentBuilder.New(Category.Motherboard, "ASUS X670E Hero").Price(2000)
                .Spec("Socket", "AM5").Spec("RamType", "DDR5").Spec("FormFactor", "ATX").Spec("RamSlots", "4")
        ]);
        var scores = catalog.ToDictionary(c => c.Name, _ => 0.5);
        scores["Samsung 990 Pro 4TB"] = 0.5 + 0.010 / 0.07;
        scores["Corsair Vengeance 64GB DDR5"] = 0.5 + 0.008 / 0.10;
        scores["ASUS X670E Hero"] = 0.5 + 0.006 / 0.12;

        var build = Engines.GreedyLocalSearch(new FixedScorer(scores)).Solve(Engines.Problem(catalog, 10000m)).Build;

        Assert.Equal("Samsung 990 Pro 1TB", build[Category.Ssd]!.Name);
        Assert.Equal("Corsair Vengeance 64GB DDR5", build.Ram!.Name);
        Assert.Equal("ASUS X670E Hero", build.Motherboard!.Name);
    }

    [Fact]
    public void LocalSearch_TakesACheaperPartThatImprovesTheObjective()
    {
        // The 4070 outscores the CPU by far more than the threshold; the cheaper 4060 loses some GPU score
        // but cuts the bottleneck penalty by more, at a negative Δprice.
        var catalog = CatalogFactory.FullCoherentCatalog();
        catalog.Add(ComponentBuilder.New(Category.Gpu, "NVIDIA RTX 4060").Price(1200)
            .Spec("VRAM", "8").Spec("TDP", "115").Spec("Length", "200"));
        var scores = catalog.ToDictionary(c => c.Name, _ => 0.5);
        scores["AMD Ryzen 7 7800X3D"] = 0.3;
        scores["NVIDIA RTX 4070"] = 0.9;
        scores["NVIDIA RTX 4060"] = 0.6;

        var solution = Engines.GreedyLocalSearch(new FixedScorer(scores)).Solve(Engines.Problem(catalog, 7500m));

        Assert.Equal("NVIDIA RTX 4060", solution.Build.Gpu!.Name);
        Assert.Equal(7250m - 1400m, solution.TotalPrice);
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
