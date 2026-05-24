using Struct.BLL.DTOs;

namespace Struct.BLL.Services.Interfaces;

public interface ISavedBuildService
{
    Task<IEnumerable<SavedBuildDto>> GetPagedAsync(int page = 1, int pageSize = 50);
    Task<SavedBuildDto?> GetByIdAsync(int id);
    Task<SavedBuildDto> AddAsync(CreateSavedBuildDto dto);
    Task UpdateAsync(SavedBuildDto dto);
    Task DeleteAsync(int id);
}
