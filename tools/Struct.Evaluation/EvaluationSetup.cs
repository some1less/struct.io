using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Struct.API.Extensions.Seeding;
using Struct.API.Extensions.Seeding.Parsers;
using Struct.BLL.Core.Compatibility;
using Struct.BLL.Core.Recommendation.Models;
using Struct.BLL.Core.Scoring.Benchmarks;
using Struct.DAL.Context;

namespace Struct.Evaluation;

/// <summary>Builds the harness from the API's shipped settings and a catalog run through the seeder's pipeline, with no database.</summary>
public static class EvaluationSetup
{
    public const string DefaultCatalog = "src/Struct.API/Extensions/Seeding/clean-db/clean_database.json";

    public static EvaluationHarness FromRepo(string repoRoot, string catalogPath)
    {
        var apiDir = Path.Combine(repoRoot, "src", "Struct.API");
        var config = new ConfigurationBuilder().AddJsonFile(Path.Combine(apiDir, "appsettings.json")).Build();

        T Bind<T>(string section) where T : class =>
            config.GetSection(section).Get<T>(o => o.ErrorOnUnknownConfiguration = true)
            ?? throw new InvalidOperationException($"Missing config section '{section}'.");

        // ParseCatalog never touches the database, so the context needs no provider.
        var seeder = new DatabaseSeeder(new AppDbContext(new DbContextOptions<AppDbContext>()),
            [new CpuParser(), new GpuParser(), new MotherboardParser(), new RamParser(),
             new PsuParser(), new CaseParser(), new StorageParser(), new CoolerParser()],
            Bind<PriceBoundsSettings>("Seeding:PriceBounds"), NullLogger<DatabaseSeeder>.Instance);

        var benchmarksDir = Path.Combine(apiDir, "Extensions", "Seeding", "benchmarks");
        var benchmarks = BenchmarkScores.FromCsvFiles(
            Path.Combine(benchmarksDir, "CPU_benchmark_v4.csv"), Path.Combine(benchmarksDir, "GPU_benchmarks_v7.csv"));
        var catalog = seeder.PrepareCatalog(File.ReadAllText(Path.Combine(repoRoot, catalogPath)), benchmarks);

        return new EvaluationHarness(catalog, Bind<RecommendationSettings>("Engine:Recommendation"),
            Bind<ObjectiveSettings>("Engine:Objective"), Bind<CompatibilitySettings>("Engine:Compatibility"),
            Bind<ExactSolverSettings>("Engine:ExactSolver"));
    }
}
