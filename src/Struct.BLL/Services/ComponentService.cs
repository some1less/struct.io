using Mapster;
using Struct.BLL.DTOs;
using Struct.DAL.Repositories;

namespace Struct.BLL.Services;

public class ComponentService : IComponentService
{
    private readonly IComponentRepository _repository;

    public ComponentService(IComponentRepository repository)
    {
        _repository = repository;
    }

    public async Task<IEnumerable<ComponentDto>> GetComponentsAsync(int page = 1, int pageSize = 50)
    {
        var components = await _repository.GetPagedAsync(page, pageSize);
        return components.Adapt<IEnumerable<ComponentDto>>(); /* Mapster in action */
    }
}