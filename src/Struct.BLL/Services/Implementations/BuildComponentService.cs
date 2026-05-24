using Mapster;
using Struct.BLL.DTOs;
using Struct.BLL.Services.Interfaces;
using Struct.DAL.Models;
using Struct.DAL.Repositories;

namespace Struct.BLL.Services.Implementations;

public class BuildComponentService : IBuildComponentService
{
    private readonly IBuildComponentRepository _repository;

    public BuildComponentService(IBuildComponentRepository repository)
    {
        _repository = repository;
    }

    public async Task<IEnumerable<BuildComponentDto>> GetPagedAsync(int page = 1, int pageSize = 50)
    {
        var entities = await _repository.GetPagedAsync(page, pageSize);
        return entities.Adapt<IEnumerable<BuildComponentDto>>();
    }

    public async Task<BuildComponentDto?> GetByIdAsync(int id)
    {
        var entity = await _repository.GetByIdAsync(id);
        return entity?.Adapt<BuildComponentDto>();
    }

    public async Task<BuildComponentDto> AddAsync(CreateBuildComponentDto dto)
    {
        var entity = dto.Adapt<BuildComponent>();
        await _repository.AddAsync(entity);
        await _repository.SaveChangesAsync();
        return entity.Adapt<BuildComponentDto>();
    }

    public async Task UpdateAsync(BuildComponentDto dto)
    {
        var entity = await _repository.GetByIdAsync(dto.Id)
            ?? throw new KeyNotFoundException($"BuildComponent with ID {dto.Id} not found.");

        entity.Quantity = dto.Quantity;
        entity.ComponentId = dto.ComponentId;
        entity.BuildId = dto.BuildId;

        await _repository.SaveChangesAsync();
    }

    public async Task DeleteAsync(int id)
    {
        var entity = await _repository.GetByIdAsync(id);
        if (entity != null)
        {
            _repository.Delete(entity);
            await _repository.SaveChangesAsync();
        }
    }
}
