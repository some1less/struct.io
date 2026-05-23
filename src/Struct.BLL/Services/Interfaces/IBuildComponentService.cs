using Struct.BLL.DTOs;

namespace Struct.BLL.Services;

public interface IBuildComponentService
{
    Task<IEnumerable<BuildComponentDto>> GetPagedAsync(int page = 1, int pageSize = 50);
    Task<BuildComponentDto?> GetByIdAsync(int id);
    Task<BuildComponentDto> AddAsync(CreateBuildComponentDto dto);
    Task UpdateAsync(BuildComponentDto dto);
    Task DeleteAsync(int id);
}
