using Struct.BLL.Core;
using Struct.BLL.Core.Compatibility;
using Struct.BLL.Core.Recommendation;
using Struct.BLL.Core.Recommendation.Models;
using Struct.BLL.Core.Scoring;
using Struct.DAL.Models;
using Struct.Evaluation;
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
        Assert.All(solution.FailedSlots, f => Assert.Contains("time limit", f.Reason));
    }

    [Fact]
    public void Exact_PastItsTimeLimit_KeepsTheSeedBuild()
    {
        var problem = Engines.Problem(CatalogFactory.UpgradableCatalog(), 9000m);
        var seed = Engines.MultiStart().Solve(problem);

        var solution = Engines.Exact(TestConfig.ExactSolver with { TimeLimit = TimeSpan.Zero }, Engines.MultiStart()).Solve(problem);

        Assert.True(solution.TimedOut);
        Assert.Empty(solution.FailedSlots);
        Assert.Equal(seed.Build.Components.Select(c => c.Id), solution.Build.Components.Select(c => c.Id));
    }

    public static TheoryData<int, Purpose> BruteForceCases()
    {
        var cases = new TheoryData<int, Purpose>();
        foreach (var purpose in Enum.GetValues<Purpose>())
        foreach (var budget in new[] { 5000, 7000, 8000, 9000, 10500, 13000 })
            cases.Add(budget, purpose);
        return cases;
    }

    // Several parts per slot at different prices, so the budget-aware bound cuts real branches.
    private static List<Component> BruteForceCatalog()
    {
        var catalog = CatalogFactory.GreedyTrapCatalog();
        catalog.AddRange([
            ComponentBuilder.New(Category.Gpu, "NVIDIA RTX 4060").Price(1200).Spec("TDP", "115").Spec("Length", "200"),
            ComponentBuilder.New(Category.Ram, "Corsair Vengeance 64GB DDR5").Price(1800)
                .Spec("Type", "DDR5").Spec("Capacity", "64").Spec("Speed", "6000").Spec("Modules", "2"),
            ComponentBuilder.New(Category.Ssd, "Samsung 990 Pro 2TB").Price(650).Spec("Capacity", "2 TB").Spec("Interface", "NVMe")
        ]);
        return catalog;
    }

    // Office ships without the bottleneck penalty; turning it on everywhere keeps Office, which fills the GPU last,
    // exercising the search order and the penalty-aware bound.
    private static readonly ObjectiveSettings PenaltyEverywhere = TestConfig.Objective with { BottleneckPurposes = null };

    [Theory]
    [MemberData(nameof(BruteForceCases))]
    public void Exact_WithOrWithoutASeed_FindsTheBruteForceOptimum(int budget, Purpose purpose)
    {
        var problem = Engines.Problem(BruteForceCatalog(), budget, purpose);
        var objective = Engines.Objective(PenaltyEverywhere);
        var best = BruteForce(problem, objective);

        foreach (var exact in new[] { Engines.Exact(objective: PenaltyEverywhere), Engines.Exact(seed: Engines.MultiStart(), objective: PenaltyEverywhere) })
        {
            var solution = exact.Solve(problem);
            Assert.False(solution.TimedOut);
            Assert.Equal(best.Filled, solution.Build.Components.Count());
            Assert.Equal(best.Objective, objective.Evaluate(solution.Build, problem.Profile.Weights, purpose), precision: 9);
        }
    }

    [Fact]
    public void BruteForceCatalog_HasOfficeOptimaTheBottleneckPenaltyShapes()
    {
        // Without a binding penalty the theory above would never exercise the penalty-aware bound.
        var noPenalty = new BuildObjective(new PerformanceScorer(), PenaltyEverywhere with { BottleneckWeight = 0 });
        var withPenalty = Engines.Objective(PenaltyEverywhere);

        var penalised = BruteForceCases().Select(c => Engines.Problem(BruteForceCatalog(), (int)c[0], (Purpose)c[1]))
            .Where(p => p.Purpose == Purpose.Office)
            .Count(p =>
            {
                var build = BruteForce(p, withPenalty).Build;
                return noPenalty.Evaluate(build, p.Profile.Weights, p.Purpose) - withPenalty.Evaluate(build, p.Profile.Weights, p.Purpose) > 1e-9;
            });

        Assert.True(penalised > 0, "no Office optimum pays a bottleneck penalty");
    }

    // Every combination, each slot filled with any affordable compatible part or left empty; best by (filled, objective).
    private static (int Filled, double Objective, BuildContext Build) BruteForce(BuildProblem problem, BuildObjective objective)
    {
        var compat = new CompatibilityEngine(TestConfig.Compatibility);
        var sequence = problem.Profile.Sequence;
        (int, double) best = (-1, double.NegativeInfinity);
        var bestBuild = new BuildContext();
        void Walk(int i, BuildContext build, decimal spent)
        {
            if (i == sequence.Count)
            {
                var score = (build.Components.Count(), objective.Evaluate(build, problem.Profile.Weights, problem.Purpose));
                if (score.CompareTo(best) > 0) (best, bestBuild) = (score, build);
                return;
            }
            foreach (var part in problem.Candidates[sequence[i]])
            {
                if (spent + part.Price > problem.Budget || !compat.CheckCompatibility(build, part).IsCompatible) continue;
                var next = build.Clone();
                next.Assign(part);
                Walk(i + 1, next, spent + part.Price);
            }
            Walk(i + 1, build, spent);
        }
        Walk(0, new BuildContext(), 0m);
        return (best.Item1, best.Item2, bestBuild);
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
    public void LocalSearch_TellsPartsApart_WhenEveryCatalogIdIsZero()
    {
        // As in the harness: rows parsed from the catalog file never get a database Id.
        var catalog = CatalogFactory.UpgradableCatalog();
        var profile = TestConfig.Recommendation.Profiles[Purpose.Gaming];
        var problem = new BuildProblem(profile.Sequence.ToDictionary(c => c,
            c => (IReadOnlyList<Component>)catalog.Where(p => p.Category == c).ToList()), profile, Purpose.Gaming, 9000m);

        var build = Engines.GreedyLocalSearch().Solve(problem).Build;

        Assert.All(catalog, c => Assert.Equal(0, c.Id));
        Assert.Contains("4090", build.Gpu!.Name);
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
        // The 4070 outscores the CPU by far more than the threshold; the cheaper 4060 loses 0.35·0.39 of GPU score
        // but cuts the penalty by 0.5·(√0.64 − √0.25) = 0.15, at a negative Δprice.
        var catalog = CatalogFactory.FullCoherentCatalog();
        catalog.Add(ComponentBuilder.New(Category.Gpu, "NVIDIA RTX 4060").Price(1200)
            .Spec("VRAM", "8").Spec("TDP", "115").Spec("Length", "200"));
        var scores = catalog.ToDictionary(c => c.Name, _ => 0.5);
        scores["AMD Ryzen 7 7800X3D"] = 0.01;
        scores["NVIDIA RTX 4070"] = 0.64;
        scores["NVIDIA RTX 4060"] = 0.25;

        var solution = Engines.GreedyLocalSearch(new FixedScorer(scores)).Solve(Engines.Problem(catalog, 7500m));

        Assert.Equal("NVIDIA RTX 4060", solution.Build.Gpu!.Name);
        Assert.Equal(7250m - 1400m, solution.TotalPrice);
    }

    [Fact]
    public void Greedy_ReservesTheCheapestPart_ForEveryLaterSlot()
    {
        // With slot caps out of the way, GPU-first greedy would buy the 4090 (4000) and leave 3000 for parts costing 4900.
        var settings = TestConfig.Recommendation with { SlotBudgetTolerance = 10m };

        var solution = Engines.Greedy(settings).Solve(Engines.Problem(CatalogFactory.UpgradableCatalog(), 7000m));

        Assert.Empty(solution.FailedSlots);
        Assert.Contains("4060", solution.Build.Gpu!.Name);
    }

    [Fact]
    public void Greedy_KeepsTheMoney_ForASlotItSkipped_AndLocalSearchFillsIt()
    {
        // The catalog costs 7250; at 7300 the CPU slot cap (~1750) skips the only CPU (1800). The SSD slot cap could
        // afford the 2 TB drive (650), but then the skipped CPU would no longer fit.
        var catalog = CatalogFactory.FullCoherentCatalog();
        catalog.Add(ComponentBuilder.New(Category.Ssd, "Samsung 990 Pro 2TB").Price(650)
            .Spec("Capacity", "2 TB").Spec("Interface", "NVMe"));
        var problem = Engines.Problem(catalog, 7300m);

        var greedy = Engines.Greedy().Solve(problem);
        var local = Engines.GreedyLocalSearch().Solve(problem);

        Assert.Equal("Cpu", Assert.Single(greedy.FailedSlots).Category);
        Assert.Contains("1TB", greedy.Build[Category.Ssd]!.Name);
        Assert.Empty(local.FailedSlots);
        Assert.Equal(7250m, local.TotalPrice);
    }

    [Fact]
    public void LocalSearch_RepairsTheBuild_ByChangingAPart_SoTheEmptySlotFits()
    {
        // No case fits the 4090 (304 mm > 280 mm), so the reserve has no case to price and greedy buys the 4090.
        // The case cannot go in alone; together with the 4060 (200 mm) it can.
        var catalog = CatalogFactory.UpgradableCatalog().Where(c => c.Category != Category.Case).ToList();
        catalog.Add(ComponentBuilder.New(Category.Case, "Compact mid-tower").Price(400)
            .Spec("SupportedMotherboards", "ATX, Micro-ATX, Mini-ITX").Spec("MaxGpuLength", "280").Spec("MaxCoolerHeight", "170"));
        var problem = Engines.Problem(catalog, 9000m);
        var settings = TestConfig.Recommendation with { SlotBudgetTolerance = 10m };

        var greedy = Engines.Greedy(settings).Solve(problem);
        var local = Engines.GreedyLocalSearch(settings).Solve(problem);

        Assert.Equal("Case", Assert.Single(greedy.FailedSlots).Category);
        Assert.Contains("4090", greedy.Build.Gpu!.Name);
        Assert.Empty(local.FailedSlots);
        Assert.Contains("4060", local.Build.Gpu!.Name);
    }

    [LocalCatalogFact]
    public void LocalSearch_CompletesTheBuild_WhereExactDid()
    {
        // Harness v3 cells where exact filled more slots than greedy+LS.
        var catalog = EvaluationSetup.FromRepo(BenchmarkValidationTests.RepoRoot(), EvaluationSetup.DefaultCatalog).Catalog;
        (decimal Budget, Purpose Purpose, int Slots)[] cells =
        [
            (3000m, Purpose.Gaming, 8), (4000m, Purpose.Gaming, 8), (5000m, Purpose.Gaming, 8), (6000m, Purpose.Gaming, 8),
            (3000m, Purpose.Work, 8), (4000m, Purpose.Work, 8), (3000m, Purpose.Office, 7),
        ];

        var misses = cells
            .Select(c => (Cell: c, Solution: Engines.GreedyLocalSearch().Solve(Engines.Problem(catalog, c.Budget, c.Purpose))))
            .Where(r => r.Solution.Build.Components.Count() < r.Cell.Slots)
            .Select(r => $"{r.Cell.Purpose} @ {r.Cell.Budget}: missing {string.Join(", ", r.Solution.FailedSlots.Select(f => f.Category))}" +
                         $" at {r.Solution.TotalPrice} zł — {string.Join(" | ", r.Solution.Build.Components.Select(p => $"{p.Category} {p.Name} {p.Price}"))}")
            .ToList();

        Assert.True(misses.Count == 0, string.Join("\n", misses));
    }

    [Fact]
    public void MultiStart_RunsInnerOncePerPlatform_EachRestrictedToOnePlatform()
    {
        // Spec strings differ in spaces and case, as in scraped data; a part without the spec joins no platform.
        var catalog = CatalogFactory.FullCoherentCatalog();
        catalog.AddRange([
            ComponentBuilder.New(Category.Cpu, "Intel Core i5-12400F").Price(600)
                .Spec("Socket", "LGA 1700").Spec("MemoryType", "DDR4, DDR5").Spec("TDP", "65"),
            ComponentBuilder.New(Category.Cpu, "CPU without socket").Price(500),
            ComponentBuilder.New(Category.Motherboard, "Gigabyte B760 DDR4").Price(450)
                .Spec("Socket", "LGA1700").Spec("RamType", "ddr4").Spec("FormFactor", "ATX"),
            ComponentBuilder.New(Category.Motherboard, "Board without RamType").Price(400).Spec("Socket", "AM5"),
            ComponentBuilder.New(Category.Ram, "Kingston DDR4 16GB").Price(200).Spec("Type", "DDR4")
        ]);
        var problem = Engines.Problem(catalog, 12000m);
        var recorder = new RecordingSolver();

        new MultiStartSolver(recorder, Engines.Objective()).Solve(problem);

        Assert.Same(problem, recorder.Calls[0]);
        var starts = recorder.Calls.Skip(1).ToList();
        Assert.Equal(2, starts.Count);
        var lga = Assert.Single(starts, p => p.Candidates[Category.Motherboard].Any(b => b.Name == "Gigabyte B760 DDR4"));
        var am5 = Assert.Single(starts, p => p != lga);
        Assert.Equal(["Intel Core i5-12400F"], lga.Candidates[Category.Cpu].Select(c => c.Name));
        Assert.Equal(["Gigabyte B760 DDR4"], lga.Candidates[Category.Motherboard].Select(c => c.Name));
        Assert.Equal(["Kingston DDR4 16GB"], lga.Candidates[Category.Ram].Select(c => c.Name));
        Assert.Equal(["AMD Ryzen 7 7800X3D"], am5.Candidates[Category.Cpu].Select(c => c.Name));
        Assert.Equal(["MSI B650 Tomahawk"], am5.Candidates[Category.Motherboard].Select(c => c.Name));
        Assert.Equal(["Corsair Vengeance 32GB DDR5"], am5.Candidates[Category.Ram].Select(c => c.Name));
        foreach (var start in starts)
        foreach (var slot in problem.Candidates.Keys.Except([Category.Cpu, Category.Motherboard, Category.Ram]))
            Assert.Same(problem.Candidates[slot], start.Candidates[slot]);
    }

    [Fact]
    public void MultiStart_FindsTheBuildOnAnotherPlatform_ThatGreedyLocalSearchMisses()
    {
        // Work fills the CPU first: the stronger AM5 CPU still leaves room for its DDR5 kit and the weak GPU, so greedy
        // takes it, and the DDR5 money is what the strong GPU needs. Moving to AM4 changes CPU, board and RAM at once.
        var catalog = CatalogFactory.FullCoherentCatalog()
            .Where(c => c.Category is not (Category.Cpu or Category.Gpu or Category.Motherboard or Category.Ram)).ToList();
        catalog.AddRange([
            ComponentBuilder.New(Category.Cpu, "AM5 CPU").Price(1000).Spec("Socket", "AM5").Spec("MemoryType", "DDR5").Spec("TDP", "105"),
            ComponentBuilder.New(Category.Cpu, "AM4 CPU").Price(600).Spec("Socket", "AM4").Spec("MemoryType", "DDR4").Spec("TDP", "65"),
            ComponentBuilder.New(Category.Motherboard, "AM5 board").Price(500).Spec("Socket", "AM5").Spec("RamType", "DDR5").Spec("FormFactor", "ATX"),
            ComponentBuilder.New(Category.Motherboard, "AM4 board").Price(400).Spec("Socket", "AM4").Spec("RamType", "DDR4").Spec("FormFactor", "ATX"),
            ComponentBuilder.New(Category.Ram, "DDR5 kit").Price(900).Spec("Type", "DDR5"),
            ComponentBuilder.New(Category.Ram, "DDR4 kit").Price(200).Spec("Type", "DDR4"),
            ComponentBuilder.New(Category.Gpu, "Weak GPU").Price(800).Spec("TDP", "100").Spec("Length", "200"),
            ComponentBuilder.New(Category.Gpu, "Strong GPU").Price(2000).Spec("TDP", "200").Spec("Length", "240")
        ]);
        var scores = catalog.ToDictionary(c => c.Name, _ => 0.5);
        (scores["AM5 CPU"], scores["AM4 CPU"], scores["Weak GPU"], scores["Strong GPU"]) = (0.9, 0.8, 0.4, 0.9);
        var scorer = new FixedScorer(scores);
        var problem = Engines.Problem(catalog, 4800m, Purpose.Work);
        double Objective(BuildSolution s) => new BuildObjective(scorer, TestConfig.Objective).Evaluate(s.Build, problem.Profile.Weights, Purpose.Work);

        var local = Engines.GreedyLocalSearch(scorer).Solve(problem);
        var multi = Engines.MultiStart(scorer).Solve(problem);

        Assert.Equal(("AM5 CPU", "Weak GPU"), (local.Build.Cpu!.Name, local.Build.Gpu!.Name));
        Assert.Equal(("AM4 CPU", "Strong GPU"), (multi.Build.Cpu!.Name, multi.Build.Gpu!.Name));
        Assert.Equal(8, multi.Build.Components.Count());
        Assert.True(Objective(multi) > Objective(local), $"multi {Objective(multi)} <= local {Objective(local)}");
    }

    [Fact]
    public void MultiStart_OnOnePlatform_ReturnsTheInnerBuild()
    {
        var problem = Engines.Problem(CatalogFactory.UpgradableCatalog(), 9000m);

        var local = Engines.GreedyLocalSearch().Solve(problem);
        var multi = Engines.MultiStart().Solve(problem);

        Assert.Equal(local.Build.Components.Select(c => c.Id), multi.Build.Components.Select(c => c.Id));
    }

    private sealed class RecordingSolver : IBuildSolver
    {
        public List<BuildProblem> Calls { get; } = [];

        public BuildSolution Solve(BuildProblem problem)
        {
            Calls.Add(problem);
            return new BuildSolution();
        }
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
