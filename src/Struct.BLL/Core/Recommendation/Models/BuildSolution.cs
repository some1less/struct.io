using Struct.BLL.Core.Compatibility;
using Struct.DAL.Models;

namespace Struct.BLL.Core.Recommendation.Models;

public class BuildSolution
{
    public BuildContext Build { get; init; } = new();

    /// <summary>Slot budgets the solver ended with (greedy: after cascading; exact: the nominal weight share).</summary>
    public Dictionary<Category, decimal> AllocatedBudgets { get; init; } = new();

    public List<FailedSlot> FailedSlots { get; init; } = new();

    public decimal TotalPrice => Build.Components.Sum(c => c.Price);
}
