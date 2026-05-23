using Struct.BLL.DTOs;

namespace Struct.BLL.Services;

public interface IComponentService
{
    Task<ComponentDto?> GetByIdAsync(int id);
    Task<ComponentDto> AddAsync(CreateComponentDto dto);
    Task UpdateAsync(ComponentDto dto);
    Task DeleteAsync(int id);

    Task<IEnumerable<ComponentDto>> GetComponentsAsync(int page = 1, int pageSize = 50);
    Task<IEnumerable<ComponentDto>> GetComponentsByCategoryAsync(string categoryStr, int page = 1, int pageSize = 50);
}