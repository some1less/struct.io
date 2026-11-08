using Struct.DAL.Models;

namespace Struct.BLL.Core.Scoring;

// Not thread-safe: meant to live for one request (registered Scoped).
public sealed class CachingScorer : IPerformanceScorer
{
    private readonly IPerformanceScorer _inner;
    private readonly Dictionary<(Component, string), double> _scores = new();

    public CachingScorer(IPerformanceScorer inner) => _inner = inner;

    public double CalculateScore(Component component, string purpose = "Gaming")
    {
        if (!_scores.TryGetValue((component, purpose), out var score))
            _scores[(component, purpose)] = score = _inner.CalculateScore(component, purpose);
        return score;
    }
}
