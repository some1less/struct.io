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

    [HttpGet("category/{category}")]
    public async Task<IActionResult> GetByCategory(string category, [FromQuery] int page = 1, [FromQuery] int pageSize = 50)
    {
        if (pageSize > 100) pageSize = 100;
        if (page < 1) page = 1;

        try
        {
            var components = await _componentService.GetComponentsByCategoryAsync(category, page, pageSize);
            return Ok(components);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(ex.Message);
        }
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> GetById(int id)
    {
        var component = await _componentService.GetByIdAsync(id);
        if (component == null) return NotFound();
        return Ok(component);
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] Struct.BLL.DTOs.CreateComponentDto dto)
    {
        var createdDto = await _componentService.AddAsync(dto);
        return CreatedAtAction(nameof(GetById), new { id = createdDto.Id }, createdDto);
    }

    [HttpPut("{id}")]
    public async Task<IActionResult> Update(int id, [FromBody] Struct.BLL.DTOs.ComponentDto dto)
    {
        if (id != dto.Id) return BadRequest("ID mismatch");
        var existing = await _componentService.GetByIdAsync(id);
        if (existing == null) return NotFound();

        await _componentService.UpdateAsync(dto);
        return NoContent();
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(int id)
    {
        var existing = await _componentService.GetByIdAsync(id);
        if (existing == null) return NotFound();

        await _componentService.DeleteAsync(id);
        return NoContent();
    }
}