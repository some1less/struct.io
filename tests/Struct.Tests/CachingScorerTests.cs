using Struct.BLL.Core.Scoring;
using Struct.DAL.Models;
using Struct.Tests.TestSupport;

namespace Struct.Tests;

public class CachingScorerTests
{
    private sealed class CountingScorer : IPerformanceScorer
    {
        public int Calls;

        public double CalculateScore(Component component, string purpose = "Gaming")
        {
            Calls++;
            return component.Name.Length + (purpose == "Work" ? 100 : 0);
        }
    }

    [Fact]
    public void RepeatedCalls_HitInnerScorerOnce()
    {
        var inner = new CountingScorer();
        var scorer = new CachingScorer(inner);
        Component cpu = ComponentBuilder.New(Category.Cpu, "Ryzen");

        double first = scorer.CalculateScore(cpu, "Gaming");
        double second = scorer.CalculateScore(cpu, "Gaming");

        Assert.Equal(first, second);
        Assert.Equal(1, inner.Calls);
    }

    [Fact]
    public void DifferentPurposes_AreCachedSeparately()
    {
        var inner = new CountingScorer();
        var scorer = new CachingScorer(inner);
        Component cpu = ComponentBuilder.New(Category.Cpu, "Ryzen");

        Assert.NotEqual(scorer.CalculateScore(cpu, "Gaming"), scorer.CalculateScore(cpu, "Work"));
        Assert.Equal(2, inner.Calls);
    }

    [Fact]
    public void DistinctComponentsWithSameId_AreNotConflated()
    {
        var scorer = new CachingScorer(new CountingScorer());
        Component a = ComponentBuilder.New(Category.Cpu, "A");
        Component b = ComponentBuilder.New(Category.Cpu, "Much longer name");

        Assert.NotEqual(scorer.CalculateScore(a), scorer.CalculateScore(b));
    }
}
