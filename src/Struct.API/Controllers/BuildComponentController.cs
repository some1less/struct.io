using Microsoft.AspNetCore.Mvc;
using Struct.BLL.DTOs;
using Struct.BLL.Services.Interfaces;

namespace Struct.API.Controllers;

[ApiController]
[Route("api/build-components")]
public class BuildComponentController : ControllerBase
{
    private readonly IBuildComponentService _buildComponentService;

    public BuildComponentController(IBuildComponentService buildComponentService)
    {
        _buildComponentService = buildComponentService;
    }

    [HttpGet]
    public async Task<IActionResult> GetAll([FromQuery] int page = 1, [FromQuery] int pageSize = 50)
    {
        if (pageSize > 100) pageSize = 100;
        if (page < 1) page = 1;

        var items = await _buildComponentService.GetPagedAsync(page, pageSize);
        return Ok(items);
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> GetById(int id)
    {
        var item = await _buildComponentService.GetByIdAsync(id);
        if (item == null) return NotFound();
        return Ok(item);
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateBuildComponentDto dto)
    {
        var createdDto = await _buildComponentService.AddAsync(dto);
        return CreatedAtAction(nameof(GetById), new { id = createdDto.Id }, createdDto);
    }

    [HttpPut("{id}")]
    public async Task<IActionResult> Update(int id, [FromBody] BuildComponentDto dto)
    {
        if (id != dto.Id) return BadRequest("ID mismatch");
        var existing = await _buildComponentService.GetByIdAsync(id);
        if (existing == null) return NotFound();

        await _buildComponentService.UpdateAsync(dto);
        return NoContent();
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(int id)
    {
        var existing = await _buildComponentService.GetByIdAsync(id);
        if (existing == null) return NotFound();

        await _buildComponentService.DeleteAsync(id);
        return NoContent();
    }
}
