using Microsoft.EntityFrameworkCore;
using Struct.DAL.Context;
using Struct.DAL.Models;
using Struct.DAL.Repositories.Interfaces;

namespace Struct.DAL.Repositories.Implementations;

public class AccountRepository : IAccountRepository
{
    private readonly AppDbContext _context;

    public AccountRepository(AppDbContext context)
    {
        _context = context;
    }

    public async Task<IEnumerable<Account>> GetPagedAsync(int page, int pageSize)
    {
        return await _context.Accounts
            .AsNoTracking()
            .OrderBy(a => a.Id)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();
    }

    public async Task<Account?> GetByIdAsync(int id)
    {
        return await _context.Accounts.FindAsync(id);
    }

    public async Task AddAsync(Account entity)
    {
        await _context.Accounts.AddAsync(entity);
    }

    public void Update(Account entity)
    {
        _context.Accounts.Update(entity);
    }

    public void Delete(Account entity)
    {
        _context.Accounts.Remove(entity);
    }

    public async Task SaveChangesAsync()
    {
        await _context.SaveChangesAsync();
    }
}
