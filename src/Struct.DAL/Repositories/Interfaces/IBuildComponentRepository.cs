using Struct.DAL.Models;

namespace Struct.DAL.Repositories.Interfaces;

public interface IBuildComponentRepository
{
    Task<IEnumerable<BuildComponent>> GetPagedAsync(int page, int pageSize);
    Task<BuildComponent?> GetByIdAsync(int id);
    Task AddAsync(BuildComponent entity);
    void Update(BuildComponent entity);
    void Delete(BuildComponent entity);
    Task SaveChangesAsync();
}
