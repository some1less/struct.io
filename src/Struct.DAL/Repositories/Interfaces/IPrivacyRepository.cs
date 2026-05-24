using Struct.DAL.Models;

namespace Struct.DAL.Repositories.Interfaces;

public interface IPrivacyRepository
{
    Task<IEnumerable<Privacy>> GetAllAsync();
    Task<Privacy?> GetByIdAsync(int id);
    Task AddAsync(Privacy entity);
    void Update(Privacy entity);
    void Delete(Privacy entity);
    Task SaveChangesAsync();
}
