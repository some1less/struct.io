using Microsoft.AspNetCore.Mvc;
using Struct.BLL.DTOs;
using Struct.BLL.Services.Interfaces;

namespace Struct.API.Controllers;

[ApiController]
[Route("api/saved-builds")]
public class SavedBuildController : ControllerBase
{
    private readonly ISavedBuildService _savedBuildService;

    public SavedBuildController(ISavedBuildService savedBuildService)
    {
        _savedBuildService = savedBuildService;
    }

    [HttpGet]
    public async Task<IActionResult> GetAll([FromQuery] int page = 1, [FromQuery] int pageSize = 50)
    {
        if (pageSize > 100) pageSize = 100;
        if (page < 1) page = 1;

        var items = await _savedBuildService.GetPagedAsync(page, pageSize);
        return Ok(items);
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> GetById(int id)
    {
        var item = await _savedBuildService.GetByIdAsync(id);
        if (item == null) return NotFound();
        return Ok(item);
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateSavedBuildDto dto)
    {
        var createdDto = await _savedBuildService.AddAsync(dto);
        return CreatedAtAction(nameof(GetById), new { id = createdDto.Id }, createdDto);
    }

    [HttpPut("{id}")]
    public async Task<IActionResult> Update(int id, [FromBody] SavedBuildDto dto)
    {
        if (id != dto.Id) return BadRequest("ID mismatch");
        var existing = await _savedBuildService.GetByIdAsync(id);
        if (existing == null) return NotFound();

        await _savedBuildService.UpdateAsync(dto);
        return NoContent();
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(int id)
    {
        var existing = await _savedBuildService.GetByIdAsync(id);
        if (existing == null) return NotFound();

        await _savedBuildService.DeleteAsync(id);
        return NoContent();
    }
}
