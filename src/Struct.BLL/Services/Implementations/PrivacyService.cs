using Mapster;
using Struct.BLL.DTOs;
using Struct.DAL.Models;
using Struct.DAL.Repositories;

namespace Struct.BLL.Services;

public class PrivacyService : IPrivacyService
{
    private readonly IPrivacyRepository _repository;

    public PrivacyService(IPrivacyRepository repository)
    {
        _repository = repository;
    }

    public async Task<IEnumerable<PrivacyDto>> GetAllAsync()
    {
        var entities = await _repository.GetAllAsync();
        return entities.Adapt<IEnumerable<PrivacyDto>>();
    }

    public async Task<PrivacyDto?> GetByIdAsync(int id)
    {
        var entity = await _repository.GetByIdAsync(id);
        return entity?.Adapt<PrivacyDto>();
    }

    public async Task<PrivacyDto> AddAsync(CreatePrivacyDto dto)
    {
        var entity = dto.Adapt<Privacy>();
        await _repository.AddAsync(entity);
        await _repository.SaveChangesAsync();
        return entity.Adapt<PrivacyDto>();
    }

    public async Task UpdateAsync(PrivacyDto dto)
    {
        var entity = await _repository.GetByIdAsync(dto.Id)
            ?? throw new KeyNotFoundException($"Privacy with ID {dto.Id} not found.");

        entity.Name = dto.Name;

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
