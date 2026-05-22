using Struct.BLL.DTOs;

namespace Struct.BLL.Services;

public interface IComponentService
{
    Task<IEnumerable<ComponentDto>> GetComponentsAsync(int page = 1, int pageSize = 50);
}