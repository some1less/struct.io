using Struct.BLL.DTOs;

namespace Struct.BLL.Services;

public interface IPrivacyService
{
    Task<IEnumerable<PrivacyDto>> GetAllAsync();
    Task<PrivacyDto?> GetByIdAsync(int id);
    Task<PrivacyDto> AddAsync(CreatePrivacyDto dto);
    Task UpdateAsync(PrivacyDto dto);
    Task DeleteAsync(int id);
}
