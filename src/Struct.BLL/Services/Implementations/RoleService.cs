using Mapster;
using Struct.BLL.DTOs;
using Struct.BLL.Services.Interfaces;
using Struct.DAL.Models;
using Struct.DAL.Repositories;
using Struct.DAL.Repositories.Interfaces;

namespace Struct.BLL.Services.Implementations;

public class RoleService : IRoleService
{
    private readonly IRoleRepository _repository;

    public RoleService(IRoleRepository repository)
    {
        _repository = repository;
    }

    public async Task<IEnumerable<RoleDto>> GetAllAsync()
    {
        var entities = await _repository.GetAllAsync();
        return entities.Adapt<IEnumerable<RoleDto>>();
    }

    public async Task<RoleDto?> GetByIdAsync(int id)
    {
        var entity = await _repository.GetByIdAsync(id);
        return entity?.Adapt<RoleDto>();
    }

    public async Task<RoleDto> AddAsync(CreateRoleDto dto)
    {
        var entity = dto.Adapt<Role>();
        await _repository.AddAsync(entity);
        await _repository.SaveChangesAsync();
        return entity.Adapt<RoleDto>();
    }

    public async Task UpdateAsync(RoleDto dto)
    {
        var entity = await _repository.GetByIdAsync(dto.Id)
            ?? throw new KeyNotFoundException($"Role with ID {dto.Id} not found.");

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
