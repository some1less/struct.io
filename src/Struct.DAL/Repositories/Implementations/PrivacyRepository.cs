using Microsoft.EntityFrameworkCore;
using Struct.DAL.Context;
using Struct.DAL.Models;
using Struct.DAL.Repositories.Interfaces;

namespace Struct.DAL.Repositories.Implementations;

public class PrivacyRepository : IPrivacyRepository
{
    private readonly AppDbContext _context;

    public PrivacyRepository(AppDbContext context)
    {
        _context = context;
    }

    public async Task<IEnumerable<Privacy>> GetAllAsync()
    {
        return await _context.Privacies.ToListAsync();
    }

    public async Task<Privacy?> GetByIdAsync(int id)
    {
        return await _context.Privacies.FindAsync(id);
    }

    public async Task AddAsync(Privacy entity)
    {
        await _context.Privacies.AddAsync(entity);
    }

    public void Update(Privacy entity)
    {
        _context.Privacies.Update(entity);
    }

    public void Delete(Privacy entity)
    {
        _context.Privacies.Remove(entity);
    }

    public async Task SaveChangesAsync()
    {
        await _context.SaveChangesAsync();
    }
}
