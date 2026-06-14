using Struct.BLL.Core.Scoring;
using Struct.DAL.Models;
using Struct.Tests.TestSupport;

namespace Struct.Tests;

/// <summary>
/// Characterization tests for the PerformanceScorer (Feedback - Performance Scorer). Pins both
/// the behavior that is correct (purpose-awareness, normalized range) and the known distortions
/// (DDR5-6000 parsing, untiered GPUs collapsing to 0) so Phase-2 fixes are intentional and visible.
/// </summary>
public class PerformanceScorerTests
{
    private readonly PerformanceScorer _scorer = new();

    private static Component X3dCpu() => ComponentBuilder.New(Category.Cpu, "AMD Ryzen 7 7800X3D")
        .Spec("Socket", "AM5").Spec("Cores", "8").Spec("Threads", "16")
        .Spec("BaseClock", "4.2").Spec("BoostClock", "5.0");

    private static Component I9Cpu() => ComponentBuilder.New(Category.Cpu, "Intel Core i9-14900K")
        .Spec("Socket", "LGA1700").Spec("Cores", "24").Spec("Threads", "32")
        .Spec("BaseClock", "3.2").Spec("BoostClock", "6.0");

    // ---- Purpose-awareness (correct behavior worth defending) ----------

    [Fact]
    public void Gaming_PrefersX3D_Over_24CoreI9()
    {
        Assert.True(_scorer.CalculateScore(X3dCpu(), "Gaming") > _scorer.CalculateScore(I9Cpu(), "Gaming"));
    }

    [Fact]
    public void Work_Prefers24CoreI9_Over_X3D()
    {
        Assert.True(_scorer.CalculateScore(I9Cpu(), "Work") > _scorer.CalculateScore(X3dCpu(), "Work"));
    }

    // ---- Normalized output range ---------------------------------------

    [Theory]
    [InlineData("Gaming")]
    [InlineData("Work")]
    public void Score_StaysWithinUnitInterval(string purpose)
    {
        foreach (var c in new[] { X3dCpu(), I9Cpu() })
        {
            double score = _scorer.CalculateScore(c, purpose);
            Assert.InRange(score, 0.0, 1.0);
        }
    }

    // ---- Storage parsing (correct: TB normalization) -------------------

    [Fact]
    public void Storage_OneTb_ScoresSameAs_ThousandGb()
    {
        Component tb = ComponentBuilder.New(Category.Ssd, "NVMe 1TB").Spec("Capacity", "1 TB").Spec("Interface", "NVMe");
        Component gb = ComponentBuilder.New(Category.Ssd, "NVMe 1000GB").Spec("Capacity", "1000 GB").Spec("Interface", "NVMe");

        Assert.Equal(_scorer.CalculateScore(gb), _scorer.CalculateScore(tb), precision: 6);
    }

    // ---- KNOWN DISTORTIONS (pin current behavior) ----------------------

    [Fact]
    public void Ram_DirtySpeedString_SaturatesScore_KnownParsingBug()
    {
        // Feedback: Speed "DDR5-6000" parses as 56000 MT/s (all digits concatenated),
        // saturating the score to 1.0 versus 0.757 for the clean "6000".
        Component dirty = ComponentBuilder.New(Category.Ram, "Kit A")
            .Spec("Type", "DDR5").Spec("Capacity", "32").Spec("Modules", "2").Spec("Speed", "DDR5-6000");
        Component clean = ComponentBuilder.New(Category.Ram, "Kit B")
            .Spec("Type", "DDR5").Spec("Capacity", "32").Spec("Modules", "2").Spec("Speed", "6000");

        Assert.Equal(1.0, _scorer.CalculateScore(dirty), precision: 3);
        Assert.True(_scorer.CalculateScore(clean) < 1.0);
    }

    [Fact]
    public void Gpu_NotInTierList_CollapsesToZero_KnownGap()
    {
        // Feedback: a GT 1030 (and any untiered/future GPU) scores exactly 0.000.
        Component gt1030 = ComponentBuilder.New(Category.Gpu, "GeForce GT 1030")
            .Spec("VRAM", "2").Spec("CoreClock", "1227");

        Assert.Equal(0.0, _scorer.CalculateScore(gt1030, "Gaming"), precision: 6);
    }

    // ---- Benchmark-backed scoring (Phase 3a part 2) ----

    [Fact]
    public void Cpu_WithBenchmarkScore_UsesIt_OverHeuristic()
    {
        // BenchmarkScore is a pre-normalized 0..1 ratio; the scorer applies the √ curve for UX parity.
        Component c = ComponentBuilder.New(Category.Cpu, "anything").Spec("BenchmarkScore", "0.25");
        Assert.Equal(0.5, _scorer.CalculateScore(c, "Gaming"), precision: 6); // sqrt(0.25)
    }

    [Fact]
    public void Gpu_WithBenchmarkScore_UsesIt_OverHeuristic()
    {
        Component c = ComponentBuilder.New(Category.Gpu, "anything").Spec("BenchmarkScore", "1");
        Assert.Equal(1.0, _scorer.CalculateScore(c, "Gaming"), precision: 6); // sqrt(1)
    }

    [Fact]
    public void Cpu_WithoutBenchmarkScore_FallsBackToHeuristic()
    {
        // No BenchmarkScore spec ⇒ heuristic path still runs (purpose-aware, non-zero).
        Assert.True(_scorer.CalculateScore(X3dCpu(), "Gaming") > 0);
    }
}
