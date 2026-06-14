using Struct.BLL.Core.Compatibility;
using Struct.BLL.Core.Recommendation;
using Struct.BLL.Core.Recommendation.Models;
using Struct.DAL.Models;
using Struct.Tests.TestSupport;

namespace Struct.Tests;

public class BuildObjectiveTests
{
    private static readonly Dictionary<Category, double> EqualWeights = new()
    {
        { Category.Cpu, 0.5 }, { Category.Gpu, 0.5 }
    };

    private static BuildContext Build(double cpuScore, double gpuScore, out FixedScorer scorer)
    {
        scorer = new FixedScorer(new() { ["CPU"] = cpuScore, ["GPU"] = gpuScore });
        return new BuildContext
        {
            Cpu = ComponentBuilder.New(Category.Cpu, "CPU").Build(),
            Gpu = ComponentBuilder.New(Category.Gpu, "GPU").Build()
        };
    }

    [Fact]
    public void BalancedBuild_WithinThreshold_HasNoBottleneckPenalty()
    {
        var build = Build(0.80, 0.80, out var scorer);
        var objective = new BuildObjective(scorer, new ObjectiveSettings());

        // Σ(0.5·0.8 + 0.5·0.8) = 0.80, gap 0 ≤ 0.15 ⇒ no penalty
        Assert.Equal(0.80, objective.Evaluate(build, EqualWeights, "Gaming"), precision: 6);
    }

    [Fact]
    public void ImbalancedBuild_BeyondThreshold_IsPenalized()
    {
        var build = Build(0.80, 0.50, out var scorer);
        var objective = new BuildObjective(scorer, new ObjectiveSettings()); // λ=0.5, threshold=0.15

        // weighted = 0.5·0.8 + 0.5·0.5 = 0.65; gap 0.30 ⇒ penalty 0.5·(0.30−0.15)=0.075
        Assert.Equal(0.575, objective.Evaluate(build, EqualWeights, "Gaming"), precision: 6);
    }

    [Fact]
    public void MissingCpuOrGpu_AppliesNoBottleneckPenalty()
    {
        var scorer = new FixedScorer(new() { ["CPU"] = 0.9 });
        var build = new BuildContext { Cpu = ComponentBuilder.New(Category.Cpu, "CPU").Build() };
        var objective = new BuildObjective(scorer, new ObjectiveSettings());

        // Only CPU present: 0.5·0.9 = 0.45, no penalty (GPU missing)
        Assert.Equal(0.45, objective.Evaluate(build, EqualWeights, "Gaming"), precision: 6);
    }
}
