using Struct.BLL.Core.Compatibility;
using Struct.BLL.Core.Recommendation.Models;
using Struct.BLL.Core.Scoring;
using Struct.DAL.Models;

namespace Struct.BLL.Core.Recommendation;

/// <summary>
/// Scalar quality of a whole build: Σ(weightᵢ × scoreᵢ) minus a CPU↔GPU imbalance penalty.
/// Maximized by the local-search upgrade pass (and, later, the exact solver).
/// </summary>
public class BuildObjective
{
    private readonly IPerformanceScorer _scorer;
    private readonly ObjectiveSettings _settings;

    public BuildObjective(IPerformanceScorer scorer, ObjectiveSettings settings)
    {
        _scorer = scorer;
        _settings = settings;
    }

    public double Evaluate(BuildContext build, IReadOnlyDictionary<Category, double> weights, string purpose)
    {
        double sum = 0;
        foreach (var (category, weight) in weights)
        {
            var component = BuildContextOps.Get(build, category);
            if (component != null)
                sum += weight * _scorer.CalculateScore(component, purpose);
        }
        return sum - _settings.BottleneckWeight * BottleneckPenalty(build, purpose);
    }

    private double BottleneckPenalty(BuildContext build, string purpose)
    {
        if (build.Cpu == null || build.Gpu == null) return 0;
        double cpu = _scorer.CalculateScore(build.Cpu, purpose);
        double gpu = _scorer.CalculateScore(build.Gpu, purpose);
        double gap = Math.Abs(cpu - gpu);
        return Math.Max(0, gap - _settings.BottleneckThreshold);
    }
}
