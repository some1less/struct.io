using Struct.DAL.Models;

namespace Struct.DAL.Repositories;

public interface IComponentRepository
{
    Task<IEnumerable<Component>> GetPagedAsync(int page, int pageSize);
}