using Struct.BLL.DTOs;

namespace Struct.BLL.Services;

public interface IProfileService
{
    Task<IEnumerable<ProfileDto>> GetPagedAsync(int page = 1, int pageSize = 50);
    Task<ProfileDto?> GetByIdAsync(int id);
    Task<ProfileDto> AddAsync(CreateProfileDto dto);
    Task UpdateAsync(ProfileDto dto);
    Task DeleteAsync(int id);
}
