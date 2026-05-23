using Mapster;
using Struct.BLL.DTOs;
using Struct.DAL.Models;
using Struct.DAL.Repositories;

namespace Struct.BLL.Services;

public class ProfileService : IProfileService
{
    private readonly IProfileRepository _repository;

    public ProfileService(IProfileRepository repository)
    {
        _repository = repository;
    }

    public async Task<IEnumerable<ProfileDto>> GetPagedAsync(int page = 1, int pageSize = 50)
    {
        var entities = await _repository.GetPagedAsync(page, pageSize);
        return entities.Adapt<IEnumerable<ProfileDto>>();
    }

    public async Task<ProfileDto?> GetByIdAsync(int id)
    {
        var entity = await _repository.GetByIdAsync(id);
        return entity?.Adapt<ProfileDto>();
    }

    public async Task<ProfileDto> AddAsync(CreateProfileDto dto)
    {
        var entity = dto.Adapt<Profile>();
        await _repository.AddAsync(entity);
        await _repository.SaveChangesAsync();
        return entity.Adapt<ProfileDto>();
    }

    public async Task UpdateAsync(ProfileDto dto)
    {
        var entity = await _repository.GetByIdAsync(dto.Id)
            ?? throw new KeyNotFoundException($"Profile with ID {dto.Id} not found.");

        entity.FirstName = dto.FirstName;
        entity.LastName = dto.LastName;
        entity.Description = dto.Description;

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
