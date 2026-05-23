using Mapster;
using Struct.BLL.DTOs;
using Struct.DAL.Models;
using Struct.DAL.Repositories;

namespace Struct.BLL.Services;

public class ComponentService : IComponentService
{
    private readonly IComponentRepository _repository;

    public ComponentService(IComponentRepository repository)
    {
        _repository = repository;
    }

    public async Task<ComponentDto?> GetByIdAsync(int id)
    {
        var component = await _repository.GetByIdAsync(id);
        return component?.Adapt<ComponentDto>();
    }

    public async Task<ComponentDto> AddAsync(CreateComponentDto dto)
    {
        var entity = dto.Adapt<Component>();
        await _repository.AddAsync(entity);
        await _repository.SaveChangesAsync();
        return entity.Adapt<ComponentDto>();
    }

    public async Task UpdateAsync(ComponentDto dto)
    {
        var entity = await _repository.GetByIdAsync(dto.Id)
            ?? throw new KeyNotFoundException($"Component with ID {dto.Id} not found.");

        entity.Name = dto.Name;
        entity.Brand = dto.Brand;
        entity.Price = dto.Price;
        entity.TechnicalSpecs = dto.TechnicalSpecs;

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

    public async Task<IEnumerable<ComponentDto>> GetComponentsAsync(int page = 1, int pageSize = 50)
    {
        var components = await _repository.GetPagedAsync(page, pageSize);
        return components.Adapt<IEnumerable<ComponentDto>>(); /* Mapster in action */
    }

    public async Task<IEnumerable<ComponentDto>> GetComponentsByCategoryAsync(string categoryStr, int page = 1, int pageSize = 50)
    {
        if (!Enum.TryParse<Category>(categoryStr, true, out var categoryEnum))
        {
            throw new ArgumentException($"Category '{categoryStr}' does not exist.");
        }

        var components = await _repository.GetByCategoryAsync(categoryEnum, page, pageSize);
        return components.Adapt<IEnumerable<ComponentDto>>();
    }
}