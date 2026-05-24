using Struct.DAL.Models;

namespace Struct.DAL.Repositories.Interfaces;

public interface IProfileRepository
{
    Task<IEnumerable<Profile>> GetPagedAsync(int page, int pageSize);
    Task<Profile?> GetByIdAsync(int id);
    Task AddAsync(Profile entity);
    void Update(Profile entity);
    void Delete(Profile entity);
    Task SaveChangesAsync();
}
