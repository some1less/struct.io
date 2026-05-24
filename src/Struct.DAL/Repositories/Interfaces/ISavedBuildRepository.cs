using Struct.DAL.Models;

namespace Struct.DAL.Repositories.Interfaces;

public interface ISavedBuildRepository
{
    Task<IEnumerable<SavedBuild>> GetPagedAsync(int page, int pageSize);
    Task<SavedBuild?> GetByIdAsync(int id);
    Task AddAsync(SavedBuild entity);
    void Update(SavedBuild entity);
    void Delete(SavedBuild entity);
    Task SaveChangesAsync();
}
