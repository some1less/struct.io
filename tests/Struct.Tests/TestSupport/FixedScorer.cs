using Struct.BLL.Core.Scoring;
using Struct.DAL.Models;

namespace Struct.Tests.TestSupport;

/// <summary>Returns a pre-set score per component name, so objective math is fully deterministic.</summary>
public sealed class FixedScorer : IPerformanceScorer
{
    private readonly Dictionary<string, double> _scores;
    public FixedScorer(Dictionary<string, double> scores) => _scores = scores;

    public double CalculateScore(Component component, string purpose = "Gaming") =>
        _scores.TryGetValue(component.Name, out var s) ? s : 0.0;
}
