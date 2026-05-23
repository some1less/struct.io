using Struct.BLL.DTOs;

namespace Struct.BLL.Services;

public interface IRoleService
{
    Task<IEnumerable<RoleDto>> GetAllAsync();
    Task<RoleDto?> GetByIdAsync(int id);
    Task<RoleDto> AddAsync(CreateRoleDto dto);
    Task UpdateAsync(RoleDto dto);
    Task DeleteAsync(int id);
}
