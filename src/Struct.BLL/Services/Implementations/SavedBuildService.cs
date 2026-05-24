using Mapster;
using Struct.BLL.DTOs;
using Struct.BLL.Services.Interfaces;
using Struct.DAL.Models;
using Struct.DAL.Repositories;
using Struct.DAL.Repositories.Interfaces;

namespace Struct.BLL.Services.Implementations;

public class SavedBuildService : ISavedBuildService
{
    private readonly ISavedBuildRepository _repository;

    public SavedBuildService(ISavedBuildRepository repository)
    {
        _repository = repository;
    }

    public async Task<IEnumerable<SavedBuildDto>> GetPagedAsync(int page = 1, int pageSize = 50)
    {
        var entities = await _repository.GetPagedAsync(page, pageSize);
        return entities.Adapt<IEnumerable<SavedBuildDto>>();
    }

    public async Task<SavedBuildDto?> GetByIdAsync(int id)
    {
        var entity = await _repository.GetByIdAsync(id);
        return entity?.Adapt<SavedBuildDto>();
    }

    public async Task<SavedBuildDto> AddAsync(CreateSavedBuildDto dto)
    {
        var entity = dto.Adapt<SavedBuild>();
        await _repository.AddAsync(entity);
        await _repository.SaveChangesAsync();
        return entity.Adapt<SavedBuildDto>();
    }

    public async Task UpdateAsync(SavedBuildDto dto)
    {
        var entity = await _repository.GetByIdAsync(dto.Id)
            ?? throw new KeyNotFoundException($"SavedBuild with ID {dto.Id} not found.");

        entity.Name = dto.Name;
        entity.PrivacyId = dto.PrivacyId;

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
