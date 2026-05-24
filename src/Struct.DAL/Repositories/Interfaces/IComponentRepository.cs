using Struct.DAL.Models;

namespace Struct.DAL.Repositories.Interfaces;

public interface IComponentRepository
{
    Task<Component?> GetByIdAsync(int id);
    Task AddAsync(Component entity);
    void Update(Component entity);
    void Delete(Component entity);
    Task SaveChangesAsync();

    Task<IEnumerable<Component>> GetPagedAsync(int page, int pageSize);
    Task<IEnumerable<Component>> GetByCategoryAsync(Category category, int page, int pageSize);
}