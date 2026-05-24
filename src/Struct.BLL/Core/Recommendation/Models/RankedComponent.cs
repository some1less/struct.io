using Struct.BLL.DTOs;

namespace Struct.BLL.Core.Recommendation.Models;

public class RankedComponent
{
    public int Rank { get; set; }
    public ComponentDto Component { get; set; } = null!;
    public double PerformanceScore { get; set; }
}