using Mapster;
using Microsoft.AspNetCore.Mvc;
using Struct.BLL.Core.Compatibility;
using Struct.BLL.Core.Recommendation;
using Struct.BLL.Core.Recommendation.Models;
using Struct.BLL.Services.Interfaces;

namespace Struct.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AlgorithmsController : ControllerBase
{
    private readonly IRecommendationEngine _recommendationEngine;
    private readonly ICompatibilityEngine _compatibilityEngine;
    private readonly IComponentService _componentService;
    private readonly RecommendationSettings _settings;

    public AlgorithmsController(
        IRecommendationEngine recommendationEngine,
        ICompatibilityEngine compatibilityEngine,
        IComponentService componentService,
        RecommendationSettings settings)
    {
        _recommendationEngine = recommendationEngine;
        _compatibilityEngine = compatibilityEngine;
        _componentService = componentService;
        _settings = settings;
    }

    [HttpPost("recommend")]
    public async Task<IActionResult> Recommend([FromBody] RecommendationRequest request)
    {
        if (request.Budget < _settings.MinBudget)
        {
            return BadRequest(
                new { Message = $"Budget is too low. Minimum budget for a valid PC build is {_settings.MinBudget} PLN." });
        }

        var result = await _recommendationEngine.GenerateRecommendationAsync(request);

        if (!result.IsSuccess)
        {
            return UnprocessableEntity(result);
        }

        return Ok(result);
    }

    [HttpPost("validate-build")]
    public async Task<IActionResult> ValidateBuild([FromBody] List<int> componentIds)
    {
        if (componentIds == null || !componentIds.Any())
        {
            return BadRequest(new { Message = "Component ID list cannot be empty." });
        }

        var buildContext = new BuildContext();

        foreach (var id in componentIds)
        {
       
            var componentDto = await _componentService.GetByIdAsync(id);
            if (componentDto == null)
            {
                return NotFound(new { Message = $"Component with ID {id} not found." });
            }

            var component = componentDto.Adapt<DAL.Models.Component>();

            buildContext.Assign(component);
        }

        var distinctViolations = buildContext.Components
            .SelectMany(c => _compatibilityEngine.CheckCompatibility(buildContext, c).Violations)
            .Distinct()
            .ToList();

        return Ok(new
        {
            IsCompatible = !distinctViolations.Any(),
            Violations = distinctViolations
        });
    }
}
