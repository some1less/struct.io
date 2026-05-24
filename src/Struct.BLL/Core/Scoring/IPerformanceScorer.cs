using Struct.DAL.Models;

namespace Struct.BLL.Core.Scoring;

public interface IPerformanceScorer
{
    double CalculateScore(Component component, string purpose = "Gaming");
}