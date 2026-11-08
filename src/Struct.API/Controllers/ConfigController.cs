using Microsoft.AspNetCore.Mvc;
using Struct.BLL.Core;
using Struct.BLL.Core.Recommendation.Models;

namespace Struct.API.Controllers;

[ApiController]
[Route("api/config")]
public class ConfigController : ControllerBase
{
    private readonly RecommendationSettings _recommendation;
    private readonly ObjectiveSettings _objective;

    public ConfigController(RecommendationSettings recommendation, ObjectiveSettings objective)
    {
        _recommendation = recommendation;
        _objective = objective;
    }

    [HttpGet]
    public IActionResult Get() => Ok(new
    {
        _recommendation.MinBudget,
        Purposes = Enum.GetValues<Purpose>(),
        _objective.BottleneckThreshold
    });
}
