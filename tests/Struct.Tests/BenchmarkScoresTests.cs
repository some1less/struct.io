using Struct.BLL.Core.Scoring.Benchmarks;
using Struct.DAL.Models;
using Struct.Tests.TestSupport;

namespace Struct.Tests;

public class BenchmarkScoresTests
{
    [Fact]
    public void Normalize_StripsClockSuffix_Punctuation_AndCase()
    {
        Assert.Equal("intel core i9 10900k", BenchmarkScores.Normalize("Intel Core i9-10900K @ 3.70GHz"));
        Assert.Equal("amd ryzen 7 3700x", BenchmarkScores.Normalize("AMD Ryzen 7 3700X"));
    }

    [Fact]
    public void ExtractGpuChip_PullsParenthesisedChip_OrWholeName()
    {
        Assert.Equal("GeForce RTX 2080 SUPER", BenchmarkScores.ExtractGpuChip("Asus DUAL EVO OC (GeForce RTX 2080 SUPER)"));
        Assert.Equal("GeForce RTX 3090 Ti", BenchmarkScores.ExtractGpuChip("GeForce RTX 3090 Ti"));
    }

    [Fact]
    public void Enrich_WritesPerCategoryNormalizedScore_ForMatchedParts()
    {
        // CPU marks: the strong CPU should normalize to 1.0, the weaker one proportionally.
        var cpuMarks = new Dictionary<string, double>
        {
            ["AMD Ryzen 9 5950X"] = 46000,
            ["Intel Core i5-10400 @ 2.90GHz"] = 12000
        };
        var gpuMarks = new Dictionary<string, double> { ["GeForce RTX 2080 SUPER"] = 18000 };
        var bench = new BenchmarkScores(cpuMarks, gpuMarks);

        var strong = ComponentBuilder.New(Category.Cpu, "AMD Ryzen 9 5950X").Build();
        var weak = ComponentBuilder.New(Category.Cpu, "Intel Core i5-10400").Build();
        var gpu = ComponentBuilder.New(Category.Gpu, "MSI Gaming X (GeForce RTX 2080 SUPER)").Build();
        var unmatched = ComponentBuilder.New(Category.Cpu, "Some Exotic CPU").Build();

        bench.Enrich(new[] { strong, weak, gpu, unmatched });

        double Score(Component c) =>
            double.Parse(c.TechnicalSpecs["BenchmarkScore"], System.Globalization.CultureInfo.InvariantCulture);

        Assert.Equal(1.0, Score(strong), 6);                                  // max in category → 1.0
        Assert.Equal(12000.0 / 46000.0, Score(weak), 6);                      // proportional
        Assert.Equal(1.0, Score(gpu), 6);                                     // only GPU → max → 1.0
        Assert.False(unmatched.TechnicalSpecs.ContainsKey("BenchmarkScore"));  // unmatched untouched
    }

    [Fact]
    public void Enrich_MatchesCpu_ByContainment_PickingTheSpecificModelInAMarketingName()
    {
        // Scraped catalog names carry extra text ("4.2 GHz 8-Core Processor"); the benchmark key is the
        // bare model. Containment must still pick the SPECIFIC model (7800X3D, not the shorter 7700X).
        var cpuMarks = new Dictionary<string, double>
        {
            ["AMD Ryzen 7 7800X3D"] = 35000,
            ["AMD Ryzen 7 7700X"] = 30000
        };
        var bench = new BenchmarkScores(cpuMarks, new Dictionary<string, double>());

        var x3d = ComponentBuilder.New(Category.Cpu, "AMD Ryzen 7 7800X3D 4.2 GHz 8-Core Processor").Build();
        var x = ComponentBuilder.New(Category.Cpu, "AMD Ryzen 7 7700X 4.5 GHz 8-Core Processor").Build();
        bench.Enrich(new[] { x3d, x });

        double Score(Component c) =>
            double.Parse(c.TechnicalSpecs["BenchmarkScore"], System.Globalization.CultureInfo.InvariantCulture);

        Assert.Equal(1.0, Score(x3d), 6);                  // 35000 is the category max
        Assert.Equal(30000.0 / 35000.0, Score(x), 6);      // matched 7700X, not the longer 7800X3D
    }

    [Fact]
    public void Enrich_MatchesGpu_ByChipsetSpec_WhenNameHasNoParentheses()
    {
        // New catalog GPU names are full board-partner strings with no parentheses; the chip lives in
        // the Chipset spec. Matching must use that spec.
        var gpuMarks = new Dictionary<string, double> { ["GeForce RTX 4070 SUPER"] = 27000 };
        var bench = new BenchmarkScores(new Dictionary<string, double>(), gpuMarks);

        var gpu = ComponentBuilder.New(Category.Gpu, "Gigabyte WINDFORCE OC GeForce RTX 4070 SUPER 12 GB")
            .Spec("Chipset", "GeForce RTX 4070 SUPER").Build();
        bench.Enrich(new[] { gpu });

        Assert.True(gpu.TechnicalSpecs.ContainsKey("BenchmarkScore"));
        Assert.Equal(1.0,
            double.Parse(gpu.TechnicalSpecs["BenchmarkScore"], System.Globalization.CultureInfo.InvariantCulture), 6);
    }
}
