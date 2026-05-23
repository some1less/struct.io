using Struct.DAL.Models;

namespace Struct.DAL.Repositories;

public interface IAccountRepository
{
    Task<IEnumerable<Account>> GetPagedAsync(int page, int pageSize);
    Task<Account?> GetByIdAsync(int id);
    Task AddAsync(Account entity);
    void Update(Account entity);
    void Delete(Account entity);
    Task SaveChangesAsync();
}
