using Struct.BLL.Core;
using Struct.BLL.Core.Specs;
using Struct.DAL.Models;
using Struct.Evaluation;
using Struct.Tests.TestSupport;

namespace Struct.Tests;

public class EvaluationHarnessTests
{
    private static EvaluationHarness Harness(List<Component> catalog) =>
        new(catalog, TestConfig.Recommendation, TestConfig.Objective, TestConfig.Compatibility, TestConfig.ExactSolver);

    [Fact]
    public void Grid_RunsEverySolverPerPurpose_AgainstTheExactOptimum()
    {
        var rows = Harness(CatalogFactory.UpgradableCatalog()).Grid([9000m]).ToList();

        Assert.Equal(9, rows.Count);
        var gaming = rows.Where(r => r.Purpose == Purpose.Gaming).ToDictionary(r => r.Solver);
        Assert.Equal(100, gaming["exact"].PctOfOptimal!.Value, precision: 9);
        Assert.Contains("4060", gaming["greedy"].Gpu);
        // Catalog parts come with Id 0; the harness must number them or the upgrade pass skips every candidate.
        Assert.Contains("4090", gaming["greedy+ls"].Gpu);
        Assert.True(gaming["greedy"].PctOfOptimal < gaming["greedy+ls"].PctOfOptimal);
        Assert.All(gaming.Values, r => Assert.True(r.Complete && r.Price <= 9000m && r.Ms >= 0));
    }

    [Fact]
    public void Grid_NothingAffordable_LeavesPctEmpty()
    {
        var row = Harness(CatalogFactory.FullCoherentCatalog()).Grid([100m]).First();

        Assert.False(row.Complete);
        Assert.Null(row.PctOfOptimal);
        Assert.Equal("", row.Cpu);
    }

    [Fact]
    public void Grid_RunWithFewerSlotsThanTheBest_GetsNoPct()
    {
        // The catalog costs 7250 in total; at 7300 greedy's slot budgets strand money and leave one slot empty.
        var rows = Harness(CatalogFactory.FullCoherentCatalog()).Grid([7300m])
            .Where(r => r.Purpose == Purpose.Gaming).ToDictionary(r => r.Solver);

        Assert.Equal(7, rows["greedy"].Filled);
        Assert.Null(rows["greedy"].PctOfOptimal);
        Assert.Equal(8, rows["exact"].Filled);
        Assert.Equal(100, rows["exact"].PctOfOptimal!.Value, precision: 9);
    }

    [Fact]
    public void Sweeps_CoverEveryTopKAndLambda()
    {
        var harness = Harness(CatalogFactory.GreedyTrapCatalog());

        var topK = harness.TopKSweep([9000m]).ToList();
        var lambda = harness.LambdaSweep([9000m]).ToList();

        Assert.Equal(EvaluationHarness.TopKs.Length * 3, topK.Count);
        Assert.All(topK, r => Assert.Equal("exact", r.Solver));
        Assert.Equal(EvaluationHarness.TopKs, topK.Where(r => r.Purpose == Purpose.Gaming).Select(r => r.TopK!.Value));
        Assert.Equal(EvaluationHarness.Lambdas.Length * 3 * 3, lambda.Count);
        Assert.Equal(EvaluationHarness.Lambdas, lambda.Select(r => r.Lambda).Distinct());
    }

    [Fact]
    public void Csv_QuotesNames_AndLabelsTheUnboundedTopK()
    {
        var row = new EvaluationRow(5000m, Purpose.Work, "exact", EvaluationHarness.AllCandidates, 0.5,
            1.23456, 99.5, 4500m, true, 8, false, 1.5, "Ryzen \"X\", 8C", "");

        Assert.Equal("5000,Work,exact,all,0.5,1.2346,99.50,4500,90.00,true,8,false,1.50,\"Ryzen \"\"X\"\", 8C\",\"\"",
            row.ToCsv());
    }

    [Fact]
    public void FromRepo_LoadsTheShippedCatalog_ThroughTheSeederPipeline()
    {
        var catalog = EvaluationSetup.FromRepo(BenchmarkValidationTests.RepoRoot(), EvaluationSetup.DefaultCatalog).Catalog;

        Assert.Equal(Enum.GetValues<Category>().Length, catalog.Select(c => c.Category).Distinct().Count());
        Assert.Equal(catalog.Count, catalog.Select(c => c.Id).Distinct().Count());
        Assert.Contains(catalog, c => c.TechnicalSpecs.ContainsKey(Spec.BenchmarkScore));
    }

    [Fact]
    public void WriteAll_WritesGridAndBothSweeps()
    {
        var dir = Directory.CreateTempSubdirectory().FullName;

        Harness(CatalogFactory.FullCoherentCatalog()).WriteAll(dir);

        foreach (var file in new[] { "grid.csv", "topk.csv", "lambda.csv" })
            Assert.Equal(EvaluationRow.Header, File.ReadLines(Path.Combine(dir, file)).First());
    }
}
