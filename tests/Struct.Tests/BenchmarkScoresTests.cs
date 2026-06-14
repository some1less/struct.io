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
}
