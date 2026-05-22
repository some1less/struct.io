using Microsoft.AspNetCore.Mvc;
using Struct.BLL.Services;

namespace Struct.API.Controllers;

[ApiController]
[Route("api/components")]
public class ComponentController : ControllerBase
{
    private readonly IComponentService _componentService;

    public ComponentController(IComponentService componentService)
    {
        _componentService = componentService;
    }

    // GET: api/components?page=1&pageSize=50
    [HttpGet]
    public async Task<IActionResult> GetAll([FromQuery] int page = 1, [FromQuery] int pageSize = 50)
    {
        if (pageSize > 100) pageSize = 100;
        if (page < 1) page = 1;

        var components = await _componentService.GetComponentsAsync(page, pageSize);
        
        return Ok(components);
    }
}