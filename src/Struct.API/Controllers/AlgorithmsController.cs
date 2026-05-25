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

    public AlgorithmsController(
        IRecommendationEngine recommendationEngine,
        ICompatibilityEngine compatibilityEngine,
        IComponentService componentService)
    {
        _recommendationEngine = recommendationEngine;
        _compatibilityEngine = compatibilityEngine;
        _componentService = componentService;
    }

    [HttpPost("recommend")]
    public async Task<IActionResult> Recommend([FromBody] RecommendationRequest request)
    {
        if (request.Budget < 2400)
        {
            return BadRequest(
                new { Message = "Budget is too low. Minimum budget for a valid PC build is 2400 PLN." });
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

            switch (component.Category)
            {
                case DAL.Models.Category.Cpu: buildContext.Cpu = component; break;
                case DAL.Models.Category.Gpu: buildContext.Gpu = component; break;
                case DAL.Models.Category.Motherboard: buildContext.Motherboard = component; break;
                case DAL.Models.Category.Ram: buildContext.Ram = component; break;
                case DAL.Models.Category.Psu: buildContext.Psu = component; break;
                case DAL.Models.Category.Case: buildContext.Case = component; break;
                case DAL.Models.Category.Cooler: buildContext.Cooler = component; break;
                case DAL.Models.Category.Ssd:
                case DAL.Models.Category.Hdd: buildContext.Storage = component; break;
            }
        }

        var validationReport = new List<string>();
        
        if (buildContext.Cpu != null) VerifyComponent(buildContext, buildContext.Cpu, validationReport);
        if (buildContext.Gpu != null) VerifyComponent(buildContext, buildContext.Gpu, validationReport);
        if (buildContext.Motherboard != null) VerifyComponent(buildContext, buildContext.Motherboard, validationReport);
        if (buildContext.Ram != null) VerifyComponent(buildContext, buildContext.Ram, validationReport);
        if (buildContext.Psu != null) VerifyComponent(buildContext, buildContext.Psu, validationReport);
        if (buildContext.Case != null) VerifyComponent(buildContext, buildContext.Case, validationReport);
        if (buildContext.Cooler != null) VerifyComponent(buildContext, buildContext.Cooler, validationReport);

        var distinctViolations = validationReport.Distinct().ToList();

        return Ok(new
        {
            IsCompatible = !distinctViolations.Any(),
            Violations = distinctViolations
        });
    }

    private void VerifyComponent(BuildContext context, DAL.Models.Component component, List<string> report)
    {
        var res = _compatibilityEngine.CheckCompatibility(context, component);
        if (!res.IsCompatible)
        {
            report.AddRange(res.Violations);
        }
    }
}