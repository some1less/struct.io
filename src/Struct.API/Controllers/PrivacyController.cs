using Microsoft.AspNetCore.Mvc;
using Struct.BLL.DTOs;
using Struct.BLL.Services;

namespace Struct.API.Controllers;

[ApiController]
[Route("api/privacies")]
public class PrivacyController : ControllerBase
{
    private readonly IPrivacyService _privacyService;

    public PrivacyController(IPrivacyService privacyService)
    {
        _privacyService = privacyService;
    }

    [HttpGet]
    public async Task<IActionResult> GetAll()
    {
        var items = await _privacyService.GetAllAsync();
        return Ok(items);
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> GetById(int id)
    {
        var item = await _privacyService.GetByIdAsync(id);
        if (item == null) return NotFound();
        return Ok(item);
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreatePrivacyDto dto)
    {
        var createdDto = await _privacyService.AddAsync(dto);
        return CreatedAtAction(nameof(GetById), new { id = createdDto.Id }, createdDto);
    }

    [HttpPut("{id}")]
    public async Task<IActionResult> Update(int id, [FromBody] PrivacyDto dto)
    {
        if (id != dto.Id) return BadRequest("ID mismatch");
        var existing = await _privacyService.GetByIdAsync(id);
        if (existing == null) return NotFound();

        await _privacyService.UpdateAsync(dto);
        return NoContent();
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(int id)
    {
        var existing = await _privacyService.GetByIdAsync(id);
        if (existing == null) return NotFound();

        await _privacyService.DeleteAsync(id);
        return NoContent();
    }
}
