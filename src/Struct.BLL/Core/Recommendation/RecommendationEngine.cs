using Mapster;
using Struct.BLL.Core.Recommendation.Models;
using Struct.BLL.Core.Scoring;
using Struct.BLL.DTOs;
using Struct.DAL.Models;
using Struct.DAL.Repositories.Interfaces;

namespace Struct.BLL.Core.Recommendation;

/// <summary>Loads the candidates for the purpose's profile, runs the build solver and shapes the API result.</summary>
public class RecommendationEngine : IRecommendationEngine
{
    private readonly IComponentRepository _repository;
    private readonly IBuildSolver _solver;
    private readonly IPerformanceScorer _scorer;
    private readonly RecommendationSettings _settings;

    public RecommendationEngine(IComponentRepository repository, IBuildSolver solver, IPerformanceScorer scorer,
        RecommendationSettings settings)
    {
        _repository = repository;
        _solver = solver;
        _scorer = scorer;
        _settings = settings;
    }

    public async Task<RecommendationResult> GenerateRecommendationAsync(RecommendationRequest request)
    {
        var purpose = request.Purpose;
        var profile = _settings.Profiles[purpose];

        var candidates = new Dictionary<Category, IReadOnlyList<Component>>();
        foreach (var category in profile.Sequence)
            candidates[category] = (await _repository.GetByCategoryAsync(category, 1, 5000)).ToList();

        var solution = _solver.Solve(new BuildProblem(candidates, profile, purpose, request.Budget));

        var result = new RecommendationResult
        {
            Purpose = purpose,
            TotalBudget = request.Budget,
            ActualTotalPrice = solution.TotalPrice,
            IsSuccess = solution.FailedSlots.Count == 0,
            FailedSlots = solution.FailedSlots
        };

        foreach (var category in profile.Sequence)
        {
            var chosen = solution.Build[category];
            if (chosen == null) continue;

            result.Slots.Add(new SlotRecommendation
            {
                Category = category.ToString(),
                AllocatedBudget = Math.Round(solution.AllocatedBudgets[category], 2, MidpointRounding.AwayFromZero),
                Recommendations = new List<RankedComponent>
                {
                    new RankedComponent
                    {
                        Rank = 1,
                        Component = chosen.Adapt<ComponentDto>(),
                        PerformanceScore = Math.Round(_scorer.CalculateScore(chosen, purpose), 3)
                    }
                }
            });
        }

        result.Message = result.IsSuccess
            ? "Build generated successfully."
            : $"Incomplete build: could not fill {string.Join(", ", result.FailedSlots.Select(f => f.Category))}.";

        return result;
    }
}
