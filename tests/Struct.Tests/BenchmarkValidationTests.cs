using Struct.BLL.Core;
using System.Globalization;
using System.Text.Json;
using Struct.BLL.Core.Scoring;
using Struct.BLL.Core.Scoring.Benchmarks;
using Struct.DAL.Models;
using Xunit.Abstractions;
using Struct.BLL.Core.Specs;

namespace Struct.Tests;

/// <summary>
/// Validation study (thesis evidence): how well does the hand-tuned heuristic ordering agree with
/// real PassMark scores? Reports Spearman rank correlation over the actual catalog + benchmark files.
/// </summary>
public class BenchmarkValidationTests
{
    private readonly ITestOutputHelper _out;
    public BenchmarkValidationTests(ITestOutputHelper output) => _out = output;

    [LocalCatalogFact]
    public void Heuristic_PositivelyCorrelatesWith_PassMark_ForCpusAndGpus()
    {
        var root = RepoRoot();
        var seedDir = Path.Combine(root, "src", "Struct.API", "Extensions", "Seeding");
        var components = LoadCatalog(LocalCatalogFactAttribute.CatalogPath);
        var bench = BenchmarkScores.FromCsvFiles(
            Path.Combine(seedDir, "benchmarks", "CPU_benchmark_v4.csv"),
            Path.Combine(seedDir, "benchmarks", "GPU_benchmarks_v7.csv"));

        var scorer = new PerformanceScorer();
        // Heuristic scores BEFORE enrichment (no benchmark specs ⇒ heuristic path).
        var gaming = components.ToDictionary(c => c, c => scorer.CalculateScore(c, Purpose.Gaming));
        var work = components.ToDictionary(c => c, c => scorer.CalculateScore(c, Purpose.Work));
        bench.Enrich(components);

        // Each purpose is checked against the benchmark it is scored by: Gaming caps cores at 8 on purpose,
        // so against multi-thread CPU Mark the 14 Threadrippers alone would sink rho.
        double rhoCpuWork = SpearmanForCategory(components, work, Category.Cpu, Spec.BenchmarkScore, out int nWork);
        double rhoCpuGaming = SpearmanForCategory(components, gaming, Category.Cpu, Spec.SingleThreadScore, out int nGaming);
        double rhoGpu = SpearmanForCategory(components, gaming, Category.Gpu, Spec.BenchmarkScore, out int nGpu);

        _out.WriteLine($"CPU Work vs CPU Mark: matched {nWork}, Spearman rho = {rhoCpuWork:F3}");
        _out.WriteLine($"CPU Gaming vs single-thread: matched {nGaming}, Spearman rho = {rhoCpuGaming:F3}");
        _out.WriteLine($"GPU: matched {nGpu}, Spearman rho = {rhoGpu:F3}");

        // Defensible invariant: the hand-tuned heuristic is positively correlated with real benchmarks.
        Assert.True(rhoCpuWork > 0.3, $"CPU Work rho {rhoCpuWork:F3} too low");
        Assert.True(rhoCpuGaming > 0.3, $"CPU Gaming rho {rhoCpuGaming:F3} too low");
        Assert.True(rhoGpu > 0.3, $"GPU rho {rhoGpu:F3} too low");
    }

    private static double SpearmanForCategory(
        List<Component> components, Dictionary<Component, double> heuristic, Category category, string spec, out int n)
    {
        var matched = components
            .Where(c => c.Category == category && c.TechnicalSpecs.ContainsKey(spec))
            .ToList();
        n = matched.Count;
        if (n < 3) return 0;

        var h = matched.Select(c => heuristic[c]).ToArray();
        var b = matched.Select(c => double.Parse(c.TechnicalSpecs[spec], CultureInfo.InvariantCulture)).ToArray();
        return Spearman(h, b);
    }

    // ---- helpers ----

    private static List<Component> LoadCatalog(string jsonPath)
    {
        var components = new List<Component>();
        using var doc = JsonDocument.Parse(File.ReadAllText(jsonPath));
        foreach (var el in doc.RootElement.EnumerateArray())
        {
            if (!el.TryGetProperty("Category", out var catEl)) continue;
            var cat = catEl.GetString();
            if (cat != "Cpu" && cat != "Gpu") continue;

            var specs = new Dictionary<string, string>();
            if (el.TryGetProperty("TechnicalSpecs", out var ts) && ts.ValueKind == JsonValueKind.Object)
                foreach (var p in ts.EnumerateObject())
                    specs[p.Name] = p.Value.ValueKind == JsonValueKind.String ? p.Value.GetString()! : p.Value.ToString();

            components.Add(new Component
            {
                Name = el.GetProperty("Name").GetString() ?? "",
                Category = Enum.Parse<Category>(cat),
                Brand = "n/a",
                TechnicalSpecs = specs
            });
        }
        return components;
    }

    private static double Spearman(double[] x, double[] y)
    {
        var rx = Ranks(x);
        var ry = Ranks(y);
        return Pearson(rx, ry);
    }

    private static double[] Ranks(double[] v)
    {
        var idx = Enumerable.Range(0, v.Length).OrderBy(i => v[i]).ToArray();
        var ranks = new double[v.Length];
        int i = 0;
        while (i < idx.Length)
        {
            int j = i;
            while (j + 1 < idx.Length && v[idx[j + 1]] == v[idx[i]]) j++;
            double avg = (i + j) / 2.0 + 1; // average rank for ties (1-based)
            for (int k = i; k <= j; k++) ranks[idx[k]] = avg;
            i = j + 1;
        }
        return ranks;
    }

    private static double Pearson(double[] x, double[] y)
    {
        double mx = x.Average(), my = y.Average();
        double cov = 0, vx = 0, vy = 0;
        for (int i = 0; i < x.Length; i++)
        {
            double dx = x[i] - mx, dy = y[i] - my;
            cov += dx * dy; vx += dx * dx; vy += dy * dy;
        }
        return (vx == 0 || vy == 0) ? 0 : cov / Math.Sqrt(vx * vy);
    }

    internal static string RepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null && !File.Exists(Path.Combine(dir.FullName, "Struct.sln")))
            dir = dir.Parent;
        return dir?.FullName ?? throw new DirectoryNotFoundException("Could not locate repo root (Struct.sln).");
    }
}

/// <summary>A fact over the real catalog: data/ is the author's dataset and stays out of git, so CI skips it.</summary>
public sealed class LocalCatalogFactAttribute : FactAttribute
{
    public static string CatalogPath => Path.Combine(BenchmarkValidationTests.RepoRoot(), "data", "clean_database.json");

    public LocalCatalogFactAttribute()
    {
        if (!File.Exists(CatalogPath)) Skip = "needs the local catalog data/clean_database.json";
    }
}
