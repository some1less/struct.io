using Struct.DAL.Models;

namespace Struct.DAL.Repositories;

public interface IRoleRepository
{
    Task<IEnumerable<Role>> GetAllAsync();
    Task<Role?> GetByIdAsync(int id);
    Task AddAsync(Role entity);
    void Update(Role entity);
    void Delete(Role entity);
    Task SaveChangesAsync();
}
