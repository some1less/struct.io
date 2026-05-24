using Microsoft.EntityFrameworkCore;
using Struct.DAL.Context;
using Struct.DAL.Models;
using Struct.DAL.Repositories.Interfaces;

namespace Struct.DAL.Repositories.Implementations;

public class ProfileRepository : IProfileRepository
{
    private readonly AppDbContext _context;

    public ProfileRepository(AppDbContext context)
    {
        _context = context;
    }

    public async Task<IEnumerable<Profile>> GetPagedAsync(int page, int pageSize)
    {
        return await _context.Profiles
            .AsNoTracking()
            .OrderBy(p => p.Id)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();
    }

    public async Task<Profile?> GetByIdAsync(int id)
    {
        return await _context.Profiles.FindAsync(id);
    }

    public async Task AddAsync(Profile entity)
    {
        await _context.Profiles.AddAsync(entity);
    }

    public void Update(Profile entity)
    {
        _context.Profiles.Update(entity);
    }

    public void Delete(Profile entity)
    {
        _context.Profiles.Remove(entity);
    }

    public async Task SaveChangesAsync()
    {
        await _context.SaveChangesAsync();
    }
}
